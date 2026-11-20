using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class DepartmentsController : AdminControllerBase
{
    private readonly DepartmentRepository _departments;
    public DepartmentsController(DepartmentRepository departments) => _departments = departments;

    public async Task<IActionResult> Index() => View(await _departments.ListAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var next = (await _departments.ListAsync()).Select(d => d.SortOrder).DefaultIfEmpty(0).Max() + 1;
        return View("Form", new DepartmentFormModel { SortOrder = next });
    }

    [HttpPost]
    public async Task<IActionResult> Create(DepartmentFormModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        var d = ToEntity(model);
        try
        {
            var id = await _departments.InsertAsync(d);
            await Audit.LogAsync(AuditActions.DepartmentCreate, null, "department", id, new { d.Code, d.NameVi });
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.UniqueViolation))
        {
            ModelState.AddModelError(nameof(model.Code), L["Mã bộ phận đã tồn tại."]);
            return View("Form", model);
        }
        Success(L["Đã thêm bộ phận."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await _departments.GetAsync(id);
        return d is null ? NotFound() : View("Form", DepartmentFormModel.From(d));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, DepartmentFormModel model)
    {
        model.DepartmentId = id;
        if (!ModelState.IsValid) return View("Form", model);
        var d = ToEntity(model);
        try
        {
            if (!await _departments.UpdateAsync(d)) return NotFound();
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.UniqueViolation))
        {
            ModelState.AddModelError(nameof(model.Code), L["Mã bộ phận đã tồn tại."]);
            return View("Form", model);
        }
        await Audit.LogAsync(AuditActions.DepartmentUpdate, null, "department", id, new { d.Code, d.NameVi });
        Success(L["Đã lưu bộ phận."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            if (await _departments.DeleteAsync(id))
            {
                await Audit.LogAsync(AuditActions.DepartmentDelete, null, "department", id);
                Success(L["Đã xoá bộ phận."]);
            }
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.ForeignKeyViolation))
        {
            Error(L["Bộ phận đang có người tham dự nên không xoá được."]);
        }
        return RedirectToAction(nameof(Index));
    }

    private static Department ToEntity(DepartmentFormModel m) => new()
    {
        DepartmentId = m.DepartmentId,
        Code = m.Code.Trim(),
        NameVi = m.NameVi.Trim(),
        NameZhTw = string.IsNullOrWhiteSpace(m.NameZhTw) ? null : m.NameZhTw.Trim(),
        NameZhCn = string.IsNullOrWhiteSpace(m.NameZhCn) ? null : m.NameZhCn.Trim(),
        NameEn = string.IsNullOrWhiteSpace(m.NameEn) ? null : m.NameEn.Trim(),
        SortOrder = m.SortOrder
    };
}
