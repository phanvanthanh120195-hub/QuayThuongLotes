using ClosedXML.Excel;
using LuckyDraw.Web.Models.Entities;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Services;

/// <summary>Xuất danh sách người trúng ra .xlsx (dùng ở Admin → Kết quả và nút "Tải kết quả" trên màn chiếu).</summary>
public static class ResultExporter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string FileName(Event ev) => $"ket-qua-su-kien-{ev.EventId}-{VnTime.From(DateTime.UtcNow):yyyyMMdd-HHmm}.xlsx";

    public static byte[] Build(Event ev, IReadOnlyList<DrawResultRow> rows, IStringLocalizer L)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(L["Kết quả"].Value);
        ws.Cell(1, 1).Value = LocalizedName.Get(ev);
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"{L["Ngày"].Value}: {ev.EventDate:dd/MM/yyyy} · {L["Xuất lúc"].Value}: {VnTime.Format(DateTime.UtcNow)}";

        string[] headers =
        {
            L["STT"], L["Giải thưởng"], L["Mã nhân viên"], L["Họ tên"], L["Tên tiếng Trung"], L["Bộ phận"], L["Chức vụ"],
            L["Thời gian quay"], L["Người quay"]
        };
        for (var i = 0; i < headers.Length; i++) ws.Cell(4, i + 1).Value = headers[i];
        var r = 5;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = r - 4;
            ws.Cell(r, 2).Value = LocalizedName.Prize(x);
            ws.Cell(r, 3).SetValue(x.EmployeeCode);
            ws.Cell(r, 4).Value = x.FullName;
            ws.Cell(r, 5).Value = x.FullNameZh ?? "";
            ws.Cell(r, 6).Value = LocalizedName.Department(x) ?? "";
            ws.Cell(r, 7).Value = x.Position ?? "";
            ws.Cell(r, 8).Value = VnTime.Format(x.DrawnAt, "dd/MM/yyyy HH:mm:ss");
            ws.Cell(r, 9).Value = x.DrawnBy;
            r++;
        }
        var header = ws.Range(4, 1, 4, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE7C8");
        if (rows.Count > 0) ws.Range(4, 1, r - 1, headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(4);
        ws.Columns().AdjustToContents(6.0, 45.0);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
