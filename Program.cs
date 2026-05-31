using Microsoft.AspNetCore.Http.Features;
using AddEiksInXlsxFile.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});
builder.Services.AddSingleton<XlsxService>();

var app = builder.Build();

// Resolve XlsxService to trigger its startup cleanup (delete existing .xlsx files)
app.Services.GetRequiredService<XlsxService>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Upload}/{action=Index}/{id?}");

app.Run();
