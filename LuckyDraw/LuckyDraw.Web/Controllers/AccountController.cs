using System.Security.Claims;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserRepository _users;
    private readonly AuditService _audit;
    private readonly IStringLocalizer<SharedResource> L;
    private static readonly PasswordHasher<AppUser> Hasher = new();

    public AccountController(UserRepository users, AuditService audit, IStringLocalizer<SharedResource> localizer)
    {
        _users = users;
        _audit = audit;
        L = localizer;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectAfterLogin(returnUrl, User.IsInRole(Roles.Admin));
        return View(new LoginModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.GetByNameAsync(model.UserName.Trim());
        var ok = false;
        if (user is { IsActive: true })
        {
            var verify = Hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            ok = verify != PasswordVerificationResult.Failed;
            if (verify == PasswordVerificationResult.SuccessRehashNeeded)
            {
                await _users.SetPasswordAsync(user.UserId, Hasher.HashPassword(user, model.Password));
            }
        }

        if (!ok || user is null)
        {
            var failed = _audit.Build(AuditActions.LoginFailed, null, "app_user", null);
            failed.UserName = Truncate(model.UserName.Trim(), 50);
            await HttpContext.RequestServices.GetRequiredService<AuditRepository>().InsertAsync(failed);
            await Task.Delay(400); // làm chậm dò mật khẩu
            ModelState.AddModelError("", L["Sai tên đăng nhập hoặc mật khẩu, hoặc tài khoản đã bị khoá."]);
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new("display_name", user.DisplayName ?? user.UserName)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        await _users.TouchLoginAsync(user.UserId);

        var log = _audit.Build(AuditActions.Login, null, "app_user", user.UserId);
        log.UserName = user.UserName;
        await HttpContext.RequestServices.GetRequiredService<AuditRepository>().InsertAsync(log);

        return RedirectAfterLogin(model.ReturnUrl, user.Role == Roles.Admin);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Đổi ngôn ngữ: ghi cookie .AspNetCore.Culture rồi quay lại trang cũ.</summary>
    [HttpPost, AllowAnonymous]
    public IActionResult SetLanguage(string culture, string? returnUrl)
    {
        if (SupportedCultures.All.Contains(culture))
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectAfterLogin(string? returnUrl, bool isAdmin)
    {
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        return isAdmin ? Redirect("/admin") : RedirectToAction("Index", "Home");
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
