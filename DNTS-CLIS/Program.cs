using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DNTS_CLIS.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure database context
builder.Services.AddDbContext<DNTS_CLISContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DNTS_CLISContext")
    ?? throw new InvalidOperationException("Connection string 'DNTS_CLISContext' not found.")));

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddSession();  // Enable session
builder.Services.AddHttpContextAccessor();  // Required for session access

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
app.UseSession(); // Ensure session middleware is used
app.UseAuthorization();

// Define the default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
