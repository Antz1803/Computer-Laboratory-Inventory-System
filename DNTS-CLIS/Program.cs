using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DNTS_CLIS.Data;
using DNTS_CLIS.Models;

// Force PostgreSQL driver to support legacy/unspecified local DateTime types
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// === CONFIGURE DATABASE CONTEXT ===
builder.Services.AddDbContext<DNTS_CLISContext>(options =>
{
    // Production/Render Environment Check
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DNTS_CLISContext");
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        // Use PostgreSQL for Deployed Application
        options.UseNpgsql(connectionString);
    }
    else
    {
        // FALLBACK: Use local appsettings connection string (PostgreSQL Mode)
        connectionString = builder.Configuration.GetConnectionString("DNTS_CLISContext");
        options.UseNpgsql(connectionString ?? throw new InvalidOperationException("Connection string not found."));
    }

    /* // OLD LOCAL SQL SERVER CONFIGURATION (COMMENTED OUT)
    options.UseSqlServer(builder.Configuration.GetConnectionString("DNTS_CLISContext")
        ?? throw new InvalidOperationException("Connection string 'DNTS_CLISContext' not found."));
    */
});

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddSession();  // Enable session
builder.Services.AddHttpContextAccessor();  // Required for session access
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddTransient<EmailService>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    return new EmailService(
        config["EmailSettings:SmtpServer"]!,
        int.Parse(config["EmailSettings:SmtpPort"]!),
        config["EmailSettings:SmtpUsername"]!,
        config["EmailSettings:SmtpPassword"]!,
        config["EmailSettings:FromEmail"]!,
        config["EmailSettings:FromName"]!
    );
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // Enable HSTS for security
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); 
app.UseAuthorization();

// Define the default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

// Automatically apply database schema migrations on service launch
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DNTS_CLISContext>();
        context.Database.Migrate(); 
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while running database migrations.");
    }
}

app.Run();
