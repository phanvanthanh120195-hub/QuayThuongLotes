using LuckyDraw.Web.Data;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class ImportPreviewViewModel
{
    public string Token { get; init; } = "";
    public bool Overwrite { get; init; }
    public ImportPreview Preview { get; init; } = new();
}

public class ImportController : AdminControllerBase
{
    private readonly ImportService _import;
    public ImportController(ImportService import) => _import = import;

    public async Task<IActionResult> Index()
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        return View(ev);
    }

    [HttpGet]
    public IActionResult Template(ImportType type)
    {
        var bytes = _import.BuildTemplate(type);
        var name = type switch
        {
            ImportType.Participants => "mau-nguoi-tham-du.xlsx",
            ImportType.Prizes => "mau-giai-thuong.xlsx",
            _ => "mau-bo-phan.xlsx"
        };
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpPost]
    [RequestSizeLimit(ImportService.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(ImportType type, IFormFile? file, bool overwrite)
    {
        if (await WorkingEventAsync() is null) return NoEvent();
        if (file is null || file.Length == 0)
        {
            Error(L["Hãy chọn file Excel (.xlsx)."]);
            return RedirectToAction(nameof(Index));
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            Error(L["Chỉ nhận file .xlsx."]);
            return RedirectToAction(nameof(Index));
        }
        if (file.Length > ImportService.MaxFileBytes)
        {
            Error(L["File vượt quá {0} MB.", ImportService.MaxFileBytes / 1024 / 1024]);
            return RedirectToAction(nameof(Index));
        }
        var token = await _import.SaveTempAsync(file);
        return RedirectToAction(nameof(Preview), new { token, type, overwrite });
    }

    [HttpGet]
    public async Task<IActionResult> Preview(string token, ImportType type, bool overwrite)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        ViewBag.Event = ev;
        var preview = await _import.PreviewAsync(token, type, ev);
        return View(new ImportPreviewViewModel { Token = token, Overwrite = overwrite, Preview = preview });
    }

    [HttpPost]
    public async Task<IActionResult> Commit(string token, ImportType type, bool overwrite)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();

        // Đọc lại file và kiểm tra lại toàn bộ ngay trước khi ghi (dữ liệu DB có thể đã đổi)
        var preview = await _import.PreviewAsync(token, type, ev);
        if (!preview.CanCommit)
        {
            Error(L["File còn lỗi, chưa ghi dữ liệu nào."]);
            return RedirectToAction(nameof(Preview), new { token, type, overwrite });
        }

        try
        {
            var r = await _import.CommitAsync(preview, ev, overwrite);
            _import.DeleteTemp(token);
            await Realtime.ConfigChanged(ev.EventId);
            Success(L["Import xong: thêm mới {0}, cập nhật {1}, bỏ qua {2}.", r.Inserted, r.Updated, r.Skipped]);
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.ForeignKeyViolation))
        {
            Error(L["Không thay thế được: đã có kết quả quay gắn với giải cũ. Hãy chọn chế độ thêm vào cuối."]);
            return RedirectToAction(nameof(Preview), new { token, type, overwrite });
        }

        return type switch
        {
            ImportType.Participants => RedirectToAction("Index", "Participants"),
            ImportType.Prizes => RedirectToAction("Index", "Prizes"),
            _ => RedirectToAction("Index", "Departments")
        };
    }
}
