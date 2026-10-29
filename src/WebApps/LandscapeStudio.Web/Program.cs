using LandscapeStudio.Web.Filters;
using LandscapeStudio.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options => options.Filters.Add<SessionAuthorizationFilter>());
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".LandscapeStudio.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<LandscapeApiClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var gateway = configuration["Gateway:BaseUrl"] ?? "http://localhost:8010/";
    if (!gateway.EndsWith("/", StringComparison.Ordinal)) gateway += "/";
    client.BaseAddress = new Uri(gateway, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapGet("/health", () => Results.Ok(new { service = "Landscape Studio MVC", status = "healthy" }));
app.Run();

public partial class Program { }
