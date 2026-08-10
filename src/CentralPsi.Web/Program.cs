using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Hosting platform glue (Render, Railway, etc.) ----
// These platforms terminate HTTPS at their own edge proxy and hand the container a plain HTTP request on a
// port they choose via the PORT env var, so the app has to (a) listen on that port and (b) trust the proxy's
// X-Forwarded-* headers instead of trying to redirect to HTTPS itself.
var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(platformPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");
}

// ---- Configuration ----
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<TransbankOptions>(builder.Configuration.GetSection(TransbankOptions.SectionName));
builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<SuperSaludOptions>(builder.Configuration.GetSection(SuperSaludOptions.SectionName));

// ---- Data ----
// Render's managed Postgres hands out a single DATABASE_URL (postgres://user:pass@host:port/db) rather than
// the Npgsql keyword format used in appsettings.json - translate it automatically so deploying there doesn't
// require hand-building a connection string.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = string.IsNullOrEmpty(databaseUrl)
    ? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")
    : ConvertDatabaseUrlToNpgsqlConnectionString(databaseUrl);
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

// ---- Identity (admin dashboard + hidden internal calendar auth only - no patient accounts) ----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Account/Login";
    options.AccessDeniedPath = "/Admin/Account/Login";
    options.Cookie.Name = "CentralPsi.Admin";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Persist Data Protection keys in Postgres so admin sessions (and antiforgery tokens) survive container
// restarts - without this, every restart/redeploy silently invalidates every logged-in session.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

// ---- Application services ----
builder.Services.AddHttpClient();

builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddSingleton<ITimeZoneService, TimeZoneService>();
builder.Services.AddScoped<ISlotAvailabilityService, SlotAvailabilityService>();
builder.Services.AddScoped<IRefundCalculationService, RefundCalculationService>();

// Prefer Brevo's HTTPS API over raw SMTP when an API key is configured - several PaaS hosts (this one
// included) block outbound SMTP ports, which HTTPS doesn't run into.
var smtpApiKey = builder.Configuration["Smtp:ApiKey"];
if (!string.IsNullOrWhiteSpace(smtpApiKey))
{
    builder.Services.AddScoped<IEmailService, BrevoApiEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
}
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICertificateValidationService, SuperSaludCertificateValidationService>();
builder.Services.AddScoped<IPaymentService, TransbankWebpayService>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddHostedService<AttendanceConfirmationBackgroundService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Render/Railway's edge proxy isn't a fixed IP we can pre-register, so clear the default loopback-only
// allowlist and trust any forwarder - safe because the app is only reachable through that edge proxy,
// never directly from the internet.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string ConvertDatabaseUrlToNpgsqlConnectionString(string databaseUrl)
{
    // postgres://user:password@host:port/database -> Npgsql keyword format.
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var database = uri.AbsolutePath.TrimStart('/');
    return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={database};" +
           $"Username={Uri.UnescapeDataString(userInfo[0])};Password={Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "")};" +
           "SSL Mode=Require;Trust Server Certificate=true";
}
