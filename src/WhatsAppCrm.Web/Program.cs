using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Api;
using WhatsAppCrm.Web.Components;
using WhatsAppCrm.Web.Data;
using WhatsAppCrm.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Database — supports DATABASE_URL env var (Render) or ConnectionStrings config
var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (connStr != null && connStr.StartsWith("postgresql://"))
{
    var uri = new Uri(connStr);
    var userInfo = uri.UserInfo.Split(':');
    var host = uri.Host;
    var dbPort = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');
    connStr = $"Host={host};Port={dbPort};Database={database};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr, npgsql =>
    {
        npgsql.CommandTimeout(30);
        npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
    }));

// Z-API Service — central WhatsApp gateway
builder.Services.AddHttpClient<IZApiService, ZApiService>();
builder.Services.AddScoped<IZApiService, ZApiService>();

// CampaignRunner as singleton BackgroundService
builder.Services.AddSingleton<ICampaignQueue, CampaignRunner>();
builder.Services.AddHostedService(sp => (CampaignRunner)sp.GetRequiredService<ICampaignQueue>());

// HttpClient for Blazor components to call local API
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.Services.AddHttpClient("LocalApi", client =>
{
    client.BaseAddress = new Uri($"http://localhost:{port}");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("LocalApi");
});

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// JSON serialization for Minimal APIs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Response compression for faster page loads
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Kestrel port
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Log Z-API configuration status
{
    var zapiInstance = Environment.GetEnvironmentVariable("ZAPI_INSTANCE_ID");
    var zapiToken = Environment.GetEnvironmentVariable("ZAPI_TOKEN");
    var zapiClient = Environment.GetEnvironmentVariable("ZAPI_CLIENT_TOKEN");
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    if (!string.IsNullOrEmpty(zapiInstance) && !string.IsNullOrEmpty(zapiToken) && !string.IsNullOrEmpty(zapiClient))
    {
        logger.LogInformation("Z-API configurada: Instance={InstanceId}", zapiInstance);
    }
    else
    {
        logger.LogWarning("Z-API NAO configurada — rodando em MODO SIMULACAO (demo). Configure ZAPI_INSTANCE_ID, ZAPI_TOKEN e ZAPI_CLIENT_TOKEN para ativar.");
    }
}

// ============================================================
// Database init in BACKGROUND — app starts accepting requests
// immediately instead of blocking on DB connect + seed
// ============================================================
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Connecting to database (background)...");
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Database ready.");

        if (!await db.Contacts.AnyAsync())
        {
            logger.LogInformation("Seeding database...");
            await DatabaseSeeder.SeedAsync(db);
            logger.LogInformation("Database seeded.");
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization failed. App will work once DB is available.");
    }
});

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseResponseCompression();
app.UseAntiforgery();
app.MapStaticAssets();

// Health check — responds instantly (Render uses this to detect the service is up)
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

// Diagnostic endpoint — temporary, to debug API 500 errors
app.MapGet("/diag", async (AppDbContext db) =>
{
    try
    {
        var count = await db.Contacts.CountAsync();
        return Results.Ok(new { ok = true, contacts = count });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace?[..500] });
    }
});

// Map Minimal API endpoints
app.MapConversationsApi();
app.MapMessagesApi();
app.MapPipelineApi();
app.MapCampaignsApi();
app.MapContactsApi();
app.MapTemplatesApi();
app.MapResetApi();
app.MapZApiWebhookApi();  // Z-API webhooks + status

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
