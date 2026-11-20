using System.Security.Claims;
using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class UsersController : AdminControllerBase
{
    private static readonly PasswordHasher<AppUser> Hasher = new();
    private readonly UserRepository _users;

    public UsersController(UserRepository users) => _users = users;

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> Index()
    {
        ViewBag.CurrentUserId = CurrentUserId;
        return View(await _users.ListAsync());
    }

    [HttpGet]
    public IActionResult Create() => View(new UserCreateModel());

    [HttpPost]
    public async Task<IActionResult> Create(UserCreateModel model)
    {
        if (model.Role is not (Roles.Admin or Roles.Operator)) ModelState.AddModelError(nameof(model.Role), L["Vai trò không hợp lệ."]);
        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            UserName = model.UserName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? null : model.DisplayName.Trim(),
            Role = model.Role,
            IsActive = true
        };
        user.PasswordHash = Hasher.HashPassword(user, model.Password);
        try
        {
            var id = await _users.InsertAsync(user);
            await Audit.LogAsync(AuditActions.UserCreate, null, "app_user", id, new { user.UserName, user.Role });
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.UniqueViolation))
        {
            ModelState.AddModelError(nameof(model.UserName), L["Tên đăng nhập đã tồn tại."]);
            return View(model);
        }
        Success(L["Đã tạo tài khoản."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var u = await _users.GetAsync(id);
        if (u is null) return NotFound();
        return View(new UserEditModel { UserId = u.UserId, UserName = u.UserName, DisplayName = u.DisplayName, Role = u.Role, IsActive = u.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserEditModel model)
    {
        var u = await _users.GetAsync(id);
        if (u is null) return NotFound();
        model.UserId = id;
        model.UserName = u.UserName;
        if (model.Role is not (Roles.Admin or Roles.Operator)) ModelState.AddModelError(nameof(model.Role), L["Vai trò không hợp lệ."]);

        // Không tự hạ quyền / khoá chính mình, không để hệ thống mất Admin cuối cùng
        var losesAdmin = u.Role == Roles.Admin && u.IsActive && (model.Role != Roles.Admin || !model.IsActive);
        if (losesAdmin && id == CurrentUserId)
            ModelState.AddModelError("", L["Không thể tự hạ quyền hoặc khoá tài khoản của chính mình."]);
        else if (losesAdmin && await _users.CountActiveAdminsAsync() <= 1)
            ModelState.AddModelError("", L["Phải còn ít nhất một Admin đang hoạt động."]);
        if (!ModelState.IsValid) return View(model);

        u.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? null : model.DisplayName.Trim();
        u.Role = model.Role;
        u.IsActive = model.IsActive;
        await _users.UpdateAsync(u);
        await Audit.LogAsync(AuditActions.UserUpdate, null, "app_user", id, new { u.UserName, u.Role, u.IsActive });
        Success(L["Đã lưu tài khoản."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Password(int id)
    {
        var u = await _users.GetAsync(id);
        return u is null ? NotFound() : View(new UserPasswordModel { UserId = id, UserName = u.UserName });
    }

    [HttpPost]
    public async Task<IActionResult> Password(int id, UserPasswordModel model)
    {
        var u = await _users.GetAsync(id);
        if (u is null) return NotFound();
        model.UserId = id;
        model.UserName = u.UserName;
        if (!ModelState.IsValid) return View(model);

        await _users.SetPasswordAsync(id, Hasher.HashPassword(u, model.Password));
        await Audit.LogAsync(AuditActions.UserPassword, null, "app_user", id, new { u.UserName });
        Success(L["Đã đổi mật khẩu cho {0}.", u.UserName]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var u = await _users.GetAsync(id);
        if (u is null) return NotFound();
        if (id == CurrentUserId)
        {
            Error(L["Không thể xoá tài khoản đang đăng nhập."]);
            return RedirectToAction(nameof(Index));
        }
        if (u.Role == Roles.Admin && u.IsActive && await _users.CountActiveAdminsAsync() <= 1)
        {
            Error(L["Phải còn ít nhất một Admin đang hoạt động."]);
            return RedirectToAction(nameof(Index));
        }
        await _users.DeleteAsync(id);
        await Audit.LogAsync(AuditActions.UserDelete, null, "app_user", id, new { u.UserName });
        Success(L["Đã xoá tài khoản {0}.", u.UserName]);
        return RedirectToAction(nameof(Index));
    }
}
