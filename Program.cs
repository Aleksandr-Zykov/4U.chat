using _4U.chat.Components;
using _4U.chat.Data;
using _4U.chat.Models;
using _4U.chat.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddJsonFile("openroutersettings.json", optional: false, reloadOnChange: true);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MVC for authentication
builder.Services.AddControllersWithViews();

// Configure database with hosted PostgreSQL
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    // Try multiple connection string names for Azure compatibility
    var connectionString = builder.Configuration.GetConnectionString("HostedPostgreSQL") 
                          ?? builder.Configuration.GetConnectionString("DefaultConnection")
                          ?? Environment.GetEnvironmentVariable("HostedPostgreSQL")
                          ?? Environment.GetEnvironmentVariable("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        var availableConnectionStrings = builder.Configuration.GetSection("ConnectionStrings").GetChildren()
            .Select(x => x.Key).ToArray();
        
        throw new InvalidOperationException(
            $"No PostgreSQL connection string found. Checked: HostedPostgreSQL, DefaultConnection. " +
            $"Available connection strings: [{string.Join(", ", availableConnectionStrings)}]. " +
            $"Please configure the connection string in Azure App Service Configuration.");
    }
    
    options.UseNpgsql(connectionString);
});

// Also add regular DbContext for Identity which requires it
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("HostedPostgreSQL") 
                          ?? builder.Configuration.GetConnectionString("DefaultConnection")
                          ?? Environment.GetEnvironmentVariable("HostedPostgreSQL")
                          ?? Environment.GetEnvironmentVariable("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Add Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    
    // Username requirements
    options.User.RequireUniqueEmail = false; // We're not using email for login
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure application cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";  // Redirect to login page first
    options.LogoutPath = "/logout";   // Use Blazor logout page
    options.AccessDeniedPath = "/login";  // Also redirect access denied to login
    options.Cookie.SameSite = SameSiteMode.Lax;  // Prevent cookie issues
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;  // Allow HTTP in development
});

// Add OpenRouter service
builder.Services.AddHttpClient<OpenRouterService>();
builder.Services.AddSingleton<OpenRouterService>();

// Add Markdown service
builder.Services.AddScoped<MarkdownService>();

// Add Notification Service
builder.Services.AddSingleton<NotificationService>();

// Add Background Streaming Service
builder.Services.AddSingleton<BackgroundStreamingService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BackgroundStreamingService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Map MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure database exists
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize database. Please check your connection string in appsettings.json");
        throw;
    }
}

app.Run();