using FinAware.Bot.Data;
using FinAware.Bot.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// ── TELEGRAM TOKEN ──
var telegramToken = builder.Configuration["TelegramBot__Token"]
                 ?? builder.Configuration["TelegramBot:Token"];

if (string.IsNullOrEmpty(telegramToken))
{
    Console.WriteLine("❌ TelegramBot Token eksik! Render Environment Variables kontrol et.");
    throw new Exception("TelegramBot Token eksik");
}

Console.WriteLine("✅ Telegram token bulundu");

// ── TELEGRAM CLIENT ──
var botClient = new TelegramBotClient(telegramToken);
builder.Services.AddSingleton<ITelegramBotClient>(botClient);

// ── FINAWARE API ──
var apiBaseUrl = builder.Configuration["FinAwareApi__BaseUrl"]
              ?? builder.Configuration["FinAwareApi:BaseUrl"]
              ?? "https://finaware-uq2x.onrender.com";

Console.WriteLine($"📡 API: {apiBaseUrl}");

builder.Services.AddHttpClient("FinAwareApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

builder.Services.AddHttpClient();

// ── AZURE SQL ──
var connStr = builder.Configuration["ConnectionStrings__DefaultConnection"]
           ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connStr))
{
    Console.WriteLine("❌ ConnectionString eksik!");
    throw new Exception("ConnectionString eksik");
}

builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlServer(connStr,
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)
    ));

// ── SERVİSLER ──
builder.Services.AddScoped<OpenAiService>();
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramBotService>());
builder.Services.AddControllers();

var app = builder.Build();

// ── DB ──
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("✅ Bot DB hazır");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ DB hatası (devam ediliyor): {ex.Message}");
}

// ── WEBHOOK TEMİZLE ──
try
{
    using var http = new HttpClient();
    http.Timeout = TimeSpan.FromSeconds(10);
    var result = await http.GetAsync(
        $"https://api.telegram.org/bot{telegramToken}/deleteWebhook?drop_pending_updates=true");
    Console.WriteLine($"🧹 Webhook temizlendi: {result.StatusCode}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Webhook temizleme atlandı: {ex.Message}");
}

// ── BOT BİLGİSİ ──
try
{
    var me = await botClient.GetMe();
    Console.WriteLine($"🤖 Bot: @{me.Username} ({me.FirstName})");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Bot bağlantı hatası: {ex.Message}");
    Console.WriteLine("Token'ı kontrol et!");
}

app.MapControllers();
app.MapGet("/", () => "FinAware Bot çalışıyor 🤖");
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.Now }));

Console.WriteLine("🚀 FinAware Bot başlatıldı!");
Console.WriteLine($"📡 API: {apiBaseUrl}");

app.Run();
