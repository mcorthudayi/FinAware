using FinAware.Bot.Data;
using FinAware.Bot.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Requests;

var builder = WebApplication.CreateBuilder(args);

// Telegram Bot Client
var telegramToken = builder.Configuration["TelegramBot__Token"]
                 ?? builder.Configuration["TelegramBot:Token"];
if (string.IsNullOrEmpty(telegramToken))
    throw new Exception("❌ TelegramBot Token eksik!");

var botClient = new TelegramBotClient(telegramToken);

// Başlamadan önce eski instance'ları temizle
// MakeRequestAsync yerine bunu dene:
using var http = new HttpClient();
await http.GetAsync($"https://api.telegram.org/bot{telegramToken}/deleteWebhook?drop_pending_updates=true");
Console.WriteLine("🧹 Webhook temizlendi");
await Task.Delay(3000);

builder.Services.AddSingleton<ITelegramBotClient>(botClient);

// FinAware API için HttpClient
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

// Azure SQL
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration["ConnectionStrings__DefaultConnection"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
    ));

// Servisler
builder.Services.AddScoped<OpenAiService>();
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<TelegramBotService>());
builder.Services.AddControllers();

var app = builder.Build();

// DB oluştur
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("✅ Bot DB hazır");
}

app.MapControllers();
Console.WriteLine("🤖 FinAware Bot başlatılıyor...");
Console.WriteLine($"📡 FinAware API: {apiBaseUrl}");

app.Run();