using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Services;

public enum ImportType
{
    Participants,
    Prizes,
    Departments
}

public class ImportPreviewRow
{
    public int RowNumber { get; init; }
    public string[] Values { get; init; } = Array.Empty<string>();
    public List<string> Errors { get; } = new();
}

public class ImportPreview
{
    public ImportType Type { get; init; }
    public string[] Columns { get; init; } = Array.Empty<string>();
    public List<ImportPreviewRow> Rows { get; } = new();
    /// <summary>Lỗi cấp file (thiếu cột, sai định dạng…)</summary>
    public List<string> FileErrors { get; } = new();
    public int ErrorRowCount => Rows.Count(r => r.Errors.Count > 0);
    public bool CanCommit => FileErrors.Count == 0 && Rows.Count > 0 && ErrorRowCount == 0;

    internal List<object> Items { get; } = new();
}

public record ImportCommitResult(int Inserted, int Updated, int Skipped);

/// <summary>
/// Import Excel (ClosedXML): Tải mẫu → Upload → Xem trước + báo lỗi từng dòng → Xác nhận → ghi DB trong 1 transaction.
/// File upload được giữ tạm ở App_Data/imports/{token}.xlsx giữa bước xem trước và xác nhận.
/// </summary>
public class ImportService
{
    public const int MaxRows = 5000;
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string[] ParticipantCols =
        { "employee_code*", "join_date*", "full_name*", "full_name_zh", "department_vi", "department_zh", "position" };

    /// <summary>
    /// Tên tiêu đề được chấp nhận cho từng cột người tham dự – để dùng thẳng file nhân sự song ngữ
    /// (ví dụ ô tiêu đề "工號 / Mã Số thẻ", "Bộ phận 2024.06", "Chức Vụ Mới 2024.06"). So khớp kiểu "chứa".
    /// Cột không khớp (STT, 工齡…) được bỏ qua.
    /// </summary>
    private static readonly Dictionary<string, string[]> ParticipantAliases = new()
    {
        ["employee_code"] = new[] { "employee_code", "mã số thẻ", "mã nhân viên", "mã nv", "工號", "工号" },
        ["join_date"] = new[] { "join_date", "ngày vào làm", "進廠日期", "进厂日期", "入職日期", "入职日期" },
        ["full_name"] = new[] { "full_name", "họ và tên", "họ tên" },
        ["full_name_zh"] = new[] { "full_name_zh", "姓名" },
        ["department_vi"] = new[] { "department_vi", "bộ phận" },
        ["department_zh"] = new[] { "department_zh", "現任單位", "现任单位" },
        ["position"] = new[] { "position", "chức vụ", "職務", "职务" }
    };

    /// <summary>Tiêu đề trong file mẫu người tham dự (song ngữ như file nhân sự).</summary>
    private static readonly string[] ParticipantTemplateHeaders =
        { "工號\nMã số thẻ *", "進廠日期\nNgày vào làm *", "Họ và tên *", "姓名", "Bộ phận", "現任單位", "Chức vụ" };
    private static readonly string[] PrizeCols =
    {
        "sort_order*", "name_vi*", "name_zh_tw", "name_zh_cn", "name_en", "quantity*", "draw_per_turn",
        "draw_type", "spin_seconds", "stop_seconds", "min_seniority_months", "max_seniority_months"
    };
    private static readonly string[] DepartmentCols = { "code*", "name_vi*", "name_zh_tw", "name_zh_cn", "name_en" };
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd", "dd-MM-yyyy" };

    private readonly DbConnectionFactory _db;
    private readonly DepartmentRepository _departments;
    private readonly ParticipantRepository _participants;
    private readonly PrizeRepository _prizes;
    private readonly AuditService _audit;
    private readonly AuditRepository _auditRepo;
    private readonly IStringLocalizer<SharedResource> L;
    private readonly string _tempDir;

    public ImportService(DbConnectionFactory db, DepartmentRepository departments, ParticipantRepository participants,
        PrizeRepository prizes, AuditService audit, AuditRepository auditRepo, IStringLocalizer<SharedResource> localizer,
        IWebHostEnvironment env)
    {
        _db = db;
        _departments = departments;
        _participants = participants;
        _prizes = prizes;
        _audit = audit;
        _auditRepo = auditRepo;
        L = localizer;
        _tempDir = Path.Combine(env.ContentRootPath, "App_Data", "imports");
    }

    public static string[] ColumnsOf(ImportType type) => type switch
    {
        ImportType.Participants => ParticipantCols,
        ImportType.Prizes => PrizeCols,
        _ => DepartmentCols
    };

    // ------------------------------------------------------------------ File mẫu
    public byte[] BuildTemplate(ImportType type)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Data");
        var cols = ColumnsOf(type);
        var headers = type == ImportType.Participants ? ParticipantTemplateHeaders : cols;
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        object[][] samples = type switch
        {
            ImportType.Participants => new object[][]
            {
                new object[] { "VN000032", new DateTime(2021, 4, 12), "Tô Văn Thọ", "蘇文壽", "Phòng Tổng Vụ", "总务课", "Nhân viên cơ điện" },
                new object[] { "VN000034", new DateTime(2021, 5, 3), "Bùi Văn Hiệp", "裴文協", "Bộ phận R&D", "研发部", "Tổ trưởng theo dõi sản phẩm" }
            },
            ImportType.Prizes => new object[][]
            {
                new object[] { 1, "Giải khuyến khích", "鼓勵獎", "鼓励奖", "Consolation Prize", 10, 5, 2, 5, 2, "", 6 },
                new object[] { 2, "Giải nhất", "頭獎", "头奖", "First Prize", 2, 1, 1, 8, 3, 6, "" }
            },
            _ => new object[][]
            {
                new object[] { "IT", "Công nghệ thông tin", "資訊部", "信息部", "IT" },
                new object[] { "ACC", "Kế toán", "會計部", "会计部", "Accounting" }
            }
        };
        for (var r = 0; r < samples.Length; r++)
            for (var c = 0; c < samples[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(samples[r][c]);

        if (type == ImportType.Participants)
        {
            // Mã NV để dạng chữ (không mất số 0 đầu); ngày vào làm là ô ngày thật của Excel
            ws.Column(1).Style.NumberFormat.Format = "@";
            ws.Column(2).Style.NumberFormat.Format = "dd/mm/yyyy";
            ws.Row(1).Style.Alignment.WrapText = true;
        }

        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE7C8");
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(8.0, 40.0);

        var help = wb.AddWorksheet(L["Hướng dẫn"].Value);
        help.Cell(1, 1).Value = L["Cột có dấu * là bắt buộc. Giữ nguyên dòng tiêu đề, không gộp ô, chỉ dùng sheet đầu tiên."].Value;
        var notes = type switch
        {
            ImportType.Participants => new[]
            {
                L["Mã số thẻ: mã nhân viên, không trùng trong file"].Value,
                L["Ngày vào làm: ô ngày của Excel, hoặc chữ dạng yyyy-MM-dd / dd/MM/yyyy"].Value,
                L["姓名, Bộ phận, 現任單位, Chức vụ: có thể để trống. Có thể dán thẳng file nhân sự (cột STT, 工齡 sẽ bỏ qua)."].Value
            },
            ImportType.Prizes => new[]
            {
                L["draw_type: 1 = Thủ công, 2 = Tự động (mặc định 1)"].Value,
                L["draw_per_turn: số người mỗi lượt quay (mặc định 1)"].Value,
                L["min/max_seniority_months: thâm niên tối thiểu / tối đa (tháng), để trống = không giới hạn"].Value
            },
            _ => new[] { L["code: mã bộ phận, không trùng"].Value }
        };
        for (var i = 0; i < notes.Length; i++) help.Cell(i + 3, 1).Value = notes[i];
        help.Column(1).Width = 100;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ File tạm
    public async Task<string> SaveTempAsync(IFormFile file)
    {
        Directory.CreateDirectory(_tempDir);
        CleanupOldTemps();
        var token = Guid.NewGuid().ToString("N");
        await using var fs = File.Create(TempPath(token));
        await file.CopyToAsync(fs);
        return token;
    }

    public bool TempExists(string token) => IsToken(token) && File.Exists(TempPath(token));

    public void DeleteTemp(string token)
    {
        if (IsToken(token) && File.Exists(TempPath(token))) File.Delete(TempPath(token));
    }

    private string TempPath(string token) => Path.Combine(_tempDir, token + ".xlsx");
    private static bool IsToken(string? token) => Guid.TryParseExact(token, "N", out _);

    private void CleanupOldTemps()
    {
        foreach (var f in Directory.EnumerateFiles(_tempDir, "*.xlsx"))
        {
            try { if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddHours(-12)) File.Delete(f); } catch (IOException) { }
        }
    }

    // ------------------------------------------------------------------ Đọc + kiểm tra
    public async Task<ImportPreview> PreviewAsync(string token, ImportType type, Event ev)
    {
        var preview = new ImportPreview { Type = type, Columns = ColumnsOf(type) };
        if (!TempExists(token))
        {
            preview.FileErrors.Add(L["File tạm đã hết hạn, vui lòng upload lại."]);
            return preview;
        }

        XLWorkbook wb;
        try { wb = new XLWorkbook(TempPath(token)); }
        catch (Exception)
        {
            preview.FileErrors.Add(L["Không đọc được file. Hãy lưu lại dưới dạng .xlsx."]);
            return preview;
        }

        using (wb)
        {
            var ws = wb.Worksheets.First();
            var cols = ColumnsOf(type);
            var required = cols.Where(c => c.EndsWith('*')).Select(c => c.TrimEnd('*')).ToList();

            // Dòng tiêu đề: dòng đầu tiên (trong 10 dòng đầu) có đủ cột bắt buộc – file nhân sự có thể có dòng tiêu đề lớn ở trên
            var candidates = ws.RowsUsed().Take(10).ToList();
            if (candidates.Count == 0)
            {
                preview.FileErrors.Add(L["File không có dữ liệu."]);
                return preview;
            }
            var headerRow = candidates[0];
            var map = MapHeader(headerRow, type);
            foreach (var row in candidates)
            {
                var m = MapHeader(row, type);
                if (required.All(m.ContainsKey)) { headerRow = row; map = m; break; }
            }
            foreach (var c in required)
            {
                if (!map.ContainsKey(c)) preview.FileErrors.Add(L["Thiếu cột bắt buộc: {0}", c]);
            }
            if (preview.FileErrors.Count > 0) return preview;

            var dataRows = ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()).ToList();
            if (dataRows.Count > MaxRows)
            {
                preview.FileErrors.Add(L["Tối đa {0} dòng mỗi lần import.", MaxRows]);
                return preview;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in dataRows)
            {
                IXLCell Cell(string name) => map.TryGetValue(name, out var col) ? row.Cell(col) : row.Cell(16384);
                string Text(string name) => map.ContainsKey(name) ? Cell(name).GetFormattedString().Trim() : "";

                var values = cols.Select(c => Text(c.TrimEnd('*'))).ToArray();
                if (values.All(string.IsNullOrEmpty)) continue;
                var pr = new ImportPreviewRow { RowNumber = row.RowNumber(), Values = values };

                switch (type)
                {
                    case ImportType.Participants:
                        pr.Values[1] = ParseDate(Cell("join_date"), out var jd) ? jd.ToString("yyyy-MM-dd") : values[1];
                        var p = ValidateParticipant(pr, values, Cell("join_date"), seen, ev);
                        if (p != null) preview.Items.Add(p);
                        break;
                    case ImportType.Prizes:
                        var z = ValidatePrize(pr, values, Cell, seen);
                        if (z != null) preview.Items.Add(z);
                        break;
                    default:
                        var d = ValidateDepartment(pr, values, seen);
                        if (d != null) preview.Items.Add(d);
                        break;
                }
                preview.Rows.Add(pr);
            }
            if (preview.Rows.Count == 0) preview.FileErrors.Add(L["File không có dữ liệu."]);
        }
        return preview;
    }

    /// <summary>Cột → số thứ tự cột. Người tham dự so khớp theo bí danh (chứa), loại khác khớp đúng tên cột.</summary>
    private static Dictionary<string, int> MapHeader(IXLRow row, ImportType type)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in row.CellsUsed())
        {
            var text = Normalize(cell.GetFormattedString());
            if (text.Length == 0) continue;
            if (type == ImportType.Participants)
            {
                var key = ParticipantAliases.FirstOrDefault(a => a.Value.Any(x => text.Contains(Normalize(x)))).Key;
                if (key != null) map.TryAdd(key, cell.Address.ColumnNumber);
            }
            else
            {
                map.TryAdd(text, cell.Address.ColumnNumber);
            }
        }
        return map;
    }

    /// <summary>Chuẩn hoá tiêu đề: NFC (tiếng Việt gõ tổ hợp), chữ thường, bỏ dấu *, gộp khoảng trắng / xuống dòng.</summary>
    private static string Normalize(string s)
    {
        s = s.Normalize(NormalizationForm.FormC).Replace("*", " ").ToLowerInvariant();
        return string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private Participant? ValidateParticipant(ImportPreviewRow pr, string[] v, IXLCell joinCell, HashSet<string> seen, Event ev)
    {
        var code = v[0];
        var name = v[2];

        if (code.Length == 0) pr.Errors.Add(L["Thiếu mã nhân viên"]);
        else if (code.Length > 30) pr.Errors.Add(L["Mã nhân viên tối đa 30 ký tự"]);
        else if (!seen.Add(code)) pr.Errors.Add(L["Mã nhân viên bị trùng trong file"]);

        if (name.Length == 0) pr.Errors.Add(L["Thiếu họ tên"]);
        else if (name.Length > 200) pr.Errors.Add(L["Họ tên tối đa 200 ký tự"]);

        if (v.Skip(3).Any(x => x.Length > 200)) pr.Errors.Add(L["Tên tối đa 200 ký tự"]);

        if (!ParseDate(joinCell, out var joinDate)) pr.Errors.Add(L["Ngày vào làm không hợp lệ (yyyy-MM-dd)"]);
        else if (joinDate.Date > ev.EventDate.Date) pr.Errors.Add(L["Ngày vào làm sau ngày sự kiện"]);

        return pr.Errors.Count > 0 ? null : new Participant
        {
            EventId = ev.EventId,
            EmployeeCode = code,
            FullName = name,
            FullNameZh = Blank(v[3]),
            DepartmentVi = Blank(v[4]),
            DepartmentZh = Blank(v[5]),
            Position = Blank(v[6]),
            JoinDate = joinDate.Date
        };
    }

    private Prize? ValidatePrize(ImportPreviewRow pr, string[] v, Func<string, IXLCell> cell, HashSet<string> seen)
    {
        int? Int(string col, bool required, int min, int max)
        {
            var c = cell(col);
            var s = c.GetFormattedString().Trim();
            if (s.Length == 0)
            {
                if (required) pr.Errors.Add(L["Thiếu {0}", col]);
                return null;
            }
            int n;
            bool ok;
            if (c.DataType == XLDataType.Number)
            {
                var d = c.GetDouble();
                ok = d % 1 == 0 && d >= int.MinValue && d <= int.MaxValue;
                n = ok ? (int)d : 0;
            }
            else
            {
                ok = int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
            }
            if (!ok || n < min || n > max)
            {
                pr.Errors.Add(L["{0} phải là số nguyên từ {1} đến {2}", col, min, max]);
                return null;
            }
            return n;
        }

        var sort = Int("sort_order", true, 1, 999);
        var name = v[1];
        if (name.Length == 0) pr.Errors.Add(L["Thiếu tên giải"]);
        else if (name.Length > 200) pr.Errors.Add(L["Tên giải tối đa 200 ký tự"]);
        else if (!seen.Add(name)) pr.Errors.Add(L["Tên giải bị trùng trong file"]);
        var qty = Int("quantity", true, 1, 10000);
        var perTurn = Int("draw_per_turn", false, 1, 500) ?? 1;
        var drawType = Int("draw_type", false, 1, 2) ?? 1;
        var spin = Int("spin_seconds", false, 1, 120) ?? 5;
        var stop = Int("stop_seconds", false, 0, 60) ?? 2;
        var minM = Int("min_seniority_months", false, 0, 600);
        var maxM = Int("max_seniority_months", false, 1, 600);
        if (minM.HasValue && maxM.HasValue && maxM <= minM) pr.Errors.Add(L["Thâm niên tối đa phải lớn hơn tối thiểu"]);
        if (qty.HasValue && perTurn > qty) pr.Errors.Add(L["Số người mỗi lượt không được lớn hơn số lượng giải"]);

        return pr.Errors.Count > 0 ? null : new Prize
        {
            SortOrder = sort!.Value,
            NameVi = name,
            NameZhTw = Blank(v[2]),
            NameZhCn = Blank(v[3]),
            NameEn = Blank(v[4]),
            Quantity = qty!.Value,
            DrawPerTurn = perTurn,
            DrawType = (short)drawType,
            SpinSeconds = spin,
            StopSeconds = stop,
            MinSeniorityMonths = minM,
            MaxSeniorityMonths = maxM
        };
    }

    private Department? ValidateDepartment(ImportPreviewRow pr, string[] v, HashSet<string> seen)
    {
        var code = v[0];
        if (code.Length == 0) pr.Errors.Add(L["Thiếu mã bộ phận"]);
        else if (code.Length > 30) pr.Errors.Add(L["Mã bộ phận tối đa 30 ký tự"]);
        else if (!seen.Add(code)) pr.Errors.Add(L["Mã bộ phận bị trùng trong file"]);
        if (v[1].Length == 0) pr.Errors.Add(L["Thiếu tên bộ phận"]);
        if (v.Skip(1).Any(x => x.Length > 200)) pr.Errors.Add(L["Tên tối đa 200 ký tự"]);

        return pr.Errors.Count > 0 ? null : new Department
        {
            Code = code,
            NameVi = v[1],
            NameZhTw = Blank(v[2]),
            NameZhCn = Blank(v[3]),
            NameEn = Blank(v[4])
        };
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool ParseDate(IXLCell cell, out DateTime date)
    {
        date = default;
        try
        {
            switch (cell.DataType)
            {
                case XLDataType.DateTime:
                    date = cell.GetDateTime().Date;
                    return true;
                case XLDataType.Number:
                    var d = cell.GetDouble();
                    if (d < 1 || d > 2958465) return false;
                    date = DateTime.FromOADate(d).Date;
                    return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
        return DateTime.TryParseExact(cell.GetFormattedString().Trim(), DateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    // ------------------------------------------------------------------ Ghi DB
    /// <param name="overwrite">Người tham dự / bộ phận: true = ghi đè dòng trùng mã, false = bỏ qua.
    /// Giải thưởng: true = xoá hết giải cũ rồi thêm, false = thêm vào cuối.</param>
    public async Task<ImportCommitResult> CommitAsync(ImportPreview preview, Event ev, bool overwrite)
    {
        if (!preview.CanCommit) throw new InvalidOperationException("Preview has errors");

        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        ImportCommitResult result;
        switch (preview.Type)
        {
            case ImportType.Participants:
            {
                var rows = preview.Items.Cast<Participant>().ToList();
                var (ins, upd) = await _participants.UpsertManyAsync(ev.EventId, rows, overwrite, conn, tx);
                result = new(ins, upd, rows.Count - ins - upd);
                break;
            }
            case ImportType.Prizes:
            {
                var rows = preview.Items.Cast<Prize>().ToList();
                var n = await _prizes.InsertManyAsync(ev.EventId, rows, overwrite, conn, tx);
                result = new(n, 0, 0);
                break;
            }
            default:
            {
                var rows = preview.Items.Cast<Department>().ToList();
                var (ins, upd) = await _departments.UpsertManyAsync(rows, overwrite, conn, tx);
                result = new(ins, upd, rows.Count - ins - upd);
                break;
            }
        }

        await _auditRepo.InsertAsync(
            _audit.Build(AuditActions.Import, ev.EventId, preview.Type.ToString(), null,
                new { overwrite, result.Inserted, result.Updated, result.Skipped }), conn, tx);
        await tx.CommitAsync();
        return result;
    }
}
