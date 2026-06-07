using Microsoft.AspNetCore.Http.Features;
using AddEiksInXlsxFile.Services;
using AddEiksInXlsxFile.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AutoValidateAntiforgeryTokenAttribute>();
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});
builder.Services.AddSingleton<XlsxService>();
builder.Services.AddSingleton<XlsxProcessingService>();
// StatisticsService depends on ApplicationDbContext; register DbContext and Identity
var connection = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost\\SQLEXPRESS;Database=AddEiksDb;Trusted_Connection=True";
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => 
{   options.SignIn.RequireConfirmedAccount = false; 
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>();
    //.AddDefaultTokenProviders();


// Configure cookie options explicitly to avoid SameSite/Secure issues in development

// builder.Services.ConfigureApplicationCookie(options =>
// {
//     options.Cookie.Name = ".AspNetCore.Identity.Application";
//     options.Cookie.HttpOnly = true;
//     options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
//     // During local development we may use HTTP; do not force Secure policy here.
//     options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
//     options.LoginPath = "/Account/Login";
// });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".AddEiksInXlsxFile.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<StatisticsService>();
builder.Services.AddSingleton<SearchService>();

var app = builder.Build();

// Resolve XlsxService to trigger its startup cleanup (delete existing .xlsx files)
app.Services.GetRequiredService<XlsxService>();

// Ensure roles and an initial admin user exist (best-effort; requires DB availability)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = services.GetRequiredService<UserManager<IdentityUser>>();

        db.Database.Migrate();

        var roles = new[] { "Admin", "User" };
        foreach (var r in roles)
        {
            if (!roleMgr.RoleExistsAsync(r).GetAwaiter().GetResult())
            {
                roleMgr.CreateAsync(new IdentityRole(r)).GetAwaiter().GetResult();
            }
        }

        // Seed an admin user if configured via environment or defaults
        var adminEmail = builder.Configuration["Admin:Email"] ?? "lazarina.paneva@apis.bg";
        var adminPw = builder.Configuration["Admin:Password"] ?? "Laz123!";
        var admin = userMgr.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
        if (admin == null)
        {
            admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var res = userMgr.CreateAsync(admin, adminPw).GetAwaiter().GetResult();
            if (res.Succeeded)
            {
                userMgr.AddToRoleAsync(admin, "Admin").GetAwaiter().GetResult();
            }
        }
    }
    catch (Exception ex)
    {
        throw new Exception("Seeding error: " + ex.Message, ex);
        // best-effort seeding; ignore failures (e.g., no DB present)
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    //pattern: "{controller=Account}/{action=Login}/{id?}");
    pattern: "{controller=Upload}/{action=Index}/{id?}");

app.Run();
