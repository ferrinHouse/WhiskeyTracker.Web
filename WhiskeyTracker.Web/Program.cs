using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Hubs;
using WhiskeyTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(); // Keep the console provider
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions 
    { 
        Indented = false // Crucial: K8s logs are best as single-line JSON
    };
});

// FIX: Increase KeepAlive to prevent 502s from Nginx/Cloudflare
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(3);
    options.Limits.MaxRequestHeadersTotalSize = 128 * 1024; // Increase to 128KB
    options.Limits.MaxRequestLineSize = 32 * 1024; // Increase URL length limit
    options.Limits.MaxRequestHeaderCount = 200; // Allow more headers
});

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AuthorizeFolder("/Admin", "RequireAdminRole");
    options.Conventions.AllowAnonymousToFolder("/.well-known");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddScoped<WhiskeyTracker.Web.Services.CollectionViewModelService>();
builder.Services.AddScoped<WhiskeyTracker.Web.Services.LegacyMigrationService>();

// Email Service
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, WhiskeyTracker.Web.Services.EmailSender>();

// Authentication
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Authentication:Google:ClientId is not configured.");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Authentication:Google:ClientSecret is not configured.");
    });

var dbSection = builder.Configuration.GetSection("Database");
var provider = dbSection["Provider"]; 
var connectionString = dbSection["ConnectionString"] ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"--> Database Provider: {provider}");

switch (provider?.ToLower())
{
    case "postgres":
    case "postgresql":
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            // Suppress the PendingModelChangesWarning to allow startup even if there is a minor snapshot drift
            // between the Windows-generated migration and the Linux runtime.
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        break;
    default:
        // Default to In-Memory if config is missing or set to InMemory
        // Ensure you have run: dotnet add package Microsoft.EntityFrameworkCore.InMemory
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("WhiskeyTrackerInMemory"));
        break;
}

builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // SECURITY: Restrict trusted proxies to known private networks to prevent IP spoofing.
    // In Kubernetes, the Ingress Controller behaves as the proxy.
    // Since we don't know the exact Pod CIDR, we trust standard private ranges.
    // For simplicity in this environment, we will permit all proxies.
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    options.ForwardLimit = null; // Disable limit to handle Nginx -> K8s Ingress -> Pod
});
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<TastingSessionService>();

var app = builder.Build();

// ---------------------------------------------------------
// 2. DATA SEEDING (Config Driven)
// ---------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Migrate() applies any pending migrations and creates the DB if it doesn't exist
    if (context.Database.IsRelational())
    {
        context.Database.Migrate(); 
    }

    // Initialize roles and admin setup (always run)
    // Only seed broad data if the configuration explicitly says 'true'
    bool seedSampleData = dbSection.GetValue<bool>("SeedOnStartup");

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

    await DbInitializer.Initialize(context, userManager, roleManager, builder.Configuration, seedSampleData, logger, env);
}


app.UseForwardedHeaders();

// Fix for Kubernetes Ingress stripping X-Forwarded-Proto
app.Use(async (context, next) =>
{
    // If we see X-Forwarded-Host, we know we are behind the Nginx/Ingress proxy chain.
    // Since Nginx handles SSL, we force the scheme to https so redirects are correct.
    if (context.Request.Headers.ContainsKey("X-Forwarded-Host"))
    {
        context.Request.Scheme = "https";
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapRazorPages();
app.MapHub<TastingHub>("/tastingHub");

app.Run();