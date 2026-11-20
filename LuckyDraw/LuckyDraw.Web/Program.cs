using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Dapper;
using LuckyDraw.Web;
using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Hubs;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

// Dapper: cột snake_case (full_name) ↔ thuộc tính PascalCase (FullName)
DefaultTypeMap.MatchNamesWithUnderscores = true;

// ---------- Đa ngôn ngữ: vi-VN (mặc định), zh-TW, zh-CN, en-US ----------
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

// Câu báo lỗi khi bind dữ liệu (ô số để trống, nhập chữ vào ô số…) cũng dịch theo ngôn ngữ
builder.Services.AddOptions<MvcOptions>().Configure<IStringLocalizerFactory>((o, factory) =>
{
    var L = factory.Create(typeof(SharedResource));
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(x => L["Vui lòng nhập {0}.", x]);
    m.SetMissingBindRequiredValueAccessor(x => L["Vui lòng nhập {0}.", x]);
    m.SetAttemptedValueIsInvalidAccessor((v, x) => L["Giá trị '{0}' không hợp lệ cho {1}.", v, x]);
    m.SetValueIsInvalidAccessor(v => L["Giá trị '{0}' không hợp lệ.", v]);
    m.SetValueMustBeANumberAccessor(x => L["{0} phải là số.", x]);
    m.SetUnknownValueIsInvalidAccessor(x => L["Giá trị không hợp lệ cho {0}.", x]);
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(v => L["Giá trị '{0}' không hợp lệ.", v]);
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => L["Giá trị không hợp lệ."]);
    m.SetNonPropertyValueMustBeANumberAccessor(() => L["Giá trị phải là số."]);
});

// ---------- Đăng nhập bằng cookie ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        o.Cookie.Name = "luckydraw.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // Gọi API bằng fetch: trả 401/403 thay vì chuyển hướng sang trang đăng nhập
        o.Events.OnRedirectToLogin = ctx => ApiAwareRedirect(ctx, StatusCodes.Status401Unauthorized);
        o.Events.OnRedirectToAccessDenied = ctx => ApiAwareRedirect(ctx, StatusCodes.Status403Forbidden);
    });

static Task ApiAwareRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> ctx, int status)
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode = status;
        return Task.CompletedTask;
    }
    ctx.Response.Redirect(ctx.RedirectUri);
    return Task.CompletedTask;
}
builder.Services.AddAuthorization();

// Không mã hoá chữ Việt / chữ Hán thành &#x...; trong HTML (vẫn chống XSS cho < > & " ')
builder.Services.AddWebEncoders(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// ---------- Data (nơi duy nhất chứa SQL) ----------
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<EventRepository>();
builder.Services.AddScoped<DepartmentRepository>();
builder.Services.AddScoped<PrizeRepository>();
builder.Services.AddScoped<ParticipantRepository>();
builder.Services.AddScoped<ResultRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuditRepository>();
builder.Services.AddScoped<DrawRepository>();

// ---------- Services ----------
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<RealtimeNotifier>();
builder.Services.AddScoped<AdminEventContext>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<DrawService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddHostedService<AdminSeeder>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var cultures = SupportedCultures.All.Select(c => new CultureInfo(c)).ToList();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(SupportedCultures.Default),
    SupportedCultures = cultures,
    SupportedUICultures = cultures,
    // Mỗi trình duyệt tự chọn ngôn ngữ (lưu cookie). Không đoán theo Accept-Language.
    RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHub<DrawHub>("/hubs/draw");

app.Run();
