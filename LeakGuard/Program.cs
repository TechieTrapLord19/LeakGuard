using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using LeakGuard.Data;
using LeakGuard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddDbContext<LeakGuardDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cookie-based Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "LeakGuard.Auth";
        options.Cookie.HttpOnly = true;
    });

// Background services
builder.Services.AddHostedService<AuthSeederService>();
builder.Services.AddHostedService<EndpointScannerService>();

var app = builder.Build();

// Automatically apply database migrations on startup for cloud deployment
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<LeakGuardDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        System.IO.File.WriteAllText(Path.Combine(env.WebRootPath, "startup_error.txt"), ex.ToString());
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<LeakGuard.Hubs.IncidentHub>("/incidentHub");

app.Run();
