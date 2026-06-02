using FinAware.Bot.Data;
using FinAware.Bot.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// ── Telegram Bot Client ───────────────────────────────────
var telegramToken = builder.Configuration["TelegramBot__Token"]
                   ?? builder.Configuration["TelegramBot:Token"];

if (string.IsNullOrEmpty(telegramToken))
    throw new Exception("❌ TelegramBot Token eksik!");

builder.Services.AddSingleton<ITelegramBotClient>(
    new TelegramBotClient(telegramToken));

// ── FinAware API için HttpClient ──────────────────────────
var apiBaseUrl = builder.Configuration["FinAwareApi__BaseUrl"]
              ?? builder.Configuration["FinAwareApi:BaseUrl"]
              ?? "https://finaware-uq2x.onrender.com";

builder.Services.AddHttpClient("FinAwareApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

builder.Services.AddHttpClient();

// ── SQLite ────────────────────────────────────────────────
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlite("Data Source=finaware_bot.db"));

// ── Servisler ─────────────────────────────────────────────
builder.Services.AddScoped<OpenAiService>();
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<TelegramBotService>());

builder.Services.AddControllers();

var app = builder.Build();

// ── DB oluştur ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("✅ Bot DB hazır");
}

app.MapControllers();

Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine("🤖 FinAware Bot başlatılıyor...");
Console.WriteLine($"📡 FinAware API: {apiBaseUrl}");
Console.WriteLine("═══════════════════════════════════════");

app.Run();