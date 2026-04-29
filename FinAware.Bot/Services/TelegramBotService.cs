using FinAware.Bot.Data;
using FinAware.Bot.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FinAware.Bot.Services
{
    public class TelegramBotService : BackgroundService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<TelegramBotService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public TelegramBotService(
            ITelegramBotClient botClient,
            ILogger<TelegramBotService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _botClient = botClient;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var me = await _botClient.GetMe(stoppingToken);

            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"✅ Bot bağlandı: @{me.Username}");
            Console.WriteLine("📡 Mesajlar dinleniyor (long polling)...");
            Console.WriteLine("═══════════════════════════════════════");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message },
                DropPendingUpdates = true
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Update update,
            CancellationToken ct)
        {
            if (update.Message is not { } message) return;

            var chatId = message.Chat.Id;
            var username = message.From?.Username ?? message.From?.FirstName ?? "Kullanıcı";

            if (message.Text?.StartsWith("/start") == true)
            {
                await HandleStartCommand(botClient, chatId, username, message.Text, ct);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var link = await db.UserLinks.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);

            if (link == null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "👋 Merhaba! FinAware Asistan'a hoş geldin.\n\n" +
                          "Botu kullanmak için önce FinAware hesabınla bağlantı kurman gerekiyor.\n\n" +
                          "📱 *Nasıl bağlanırım?*\n" +
                          "1. FinAware web sitesine gir\n" +
                          "2. Profil sayfasına git\n" +
                          "3. 'Telegram'a Bağlan' butonuna tıkla\n" +
                          "4. Oluşan linki Telegram'da aç\n\n" +
                          "Bağlandıktan sonra benimle konuşabilirsin! 🚀",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
                return;
            }

            if (message.Photo != null && message.Photo.Length > 0)
            {
                await HandlePhotoMessage(botClient, chatId, message, link, ct);
                return;
            }

            if (message.Text is { } text)
            {
                await HandleTextMessage(botClient, chatId, text, link, ct);
                return;
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: "📝 Metin mesajı veya fatura fotoğrafı gönderebilirsin!",
                cancellationToken: ct
            );
        }

        // ════════════════════════════════════════════════════════════════
        // FOTOĞRAF İŞLEME
        // ════════════════════════════════════════════════════════════════
        private async Task HandlePhotoMessage(
            ITelegramBotClient botClient,
            long chatId,
            Message message,
            BotUserLink link,
            CancellationToken ct)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "📸 Fatura/fiş analiz ediliyor... ⏳",
                cancellationToken: ct
            );

            try
            {
                var photo = message.Photo!.OrderByDescending(p => p.FileSize).First();
                var file = await botClient.GetFile(photo.FileId, ct);

                var tempPath = Path.Combine(Path.GetTempPath(), $"finaware_bot_{Guid.NewGuid()}.jpg");
                await using (var fileStream = System.IO.File.OpenWrite(tempPath))
                {
                    await botClient.DownloadFile(file.FilePath!, fileStream, ct);
                }

                Console.WriteLine($"📸 Fotoğraf indirildi: {tempPath}");

                var apiClient = _httpClientFactory.CreateClient("FinAwareApi");
                apiClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", link.JwtToken);

                using var form = new MultipartFormDataContent();
                await using var fileContent = System.IO.File.OpenRead(tempPath);
                var streamContent = new StreamContent(fileContent);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                form.Add(streamContent, "file", "invoice.jpg");

                var response = await apiClient.PostAsync("/api/transaction/analyze-invoice", form, ct);
                var responseText = await response.Content.ReadAsStringAsync(ct);

                try { System.IO.File.Delete(tempPath); } catch { }

                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Fatura okunamadı. Manuel olarak eklemek ister misin?\n\n" +
                              "Örnek: `bugün markete 150 lira harcadım`",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct
                    );
                    return;
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseText);
                bool success = result.TryGetProperty("success", out var successEl) && successEl.GetBoolean();

                if (!success)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "🤔 Faturayı okuyamadım. Lütfen daha net bir fotoğraf çek veya manuel ekle.\n\n" +
                              "Örnek: `bugün markete 150 lira harcadım`",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct
                    );
                    return;
                }

                decimal amount = result.TryGetProperty("amount", out var amountEl) ? amountEl.GetDecimal() : 0;
                string category = result.TryGetProperty("category", out var catEl) ? catEl.GetString() ?? "Diğer" : "Diğer";
                string description = result.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
                string date = result.TryGetProperty("date", out var dateEl) ? dateEl.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");

                Console.WriteLine($"📄 Fatura: {amount}₺ | {category} | {description}");

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"📄 *Fatura Okundu!*\n\n" +
                          $"💰 Tutar: *₺{amount:N2}*\n" +
                          $"🏷️ Kategori: *{category}*\n" +
                          $"📝 Açıklama: *{(string.IsNullOrEmpty(description) ? "Belirtilmedi" : description)}*\n" +
                          $"📅 Tarih: *{date}*\n\n" +
                          $"✅ Bu işlemi ekleyeyim mi?\n" +
                          $"👉 *evet* yazarsan eklerim, *hayır* yazarsan iptal ederim.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );

                using var scope2 = _serviceProvider.CreateScope();
                var db2 = scope2.ServiceProvider.GetRequiredService<BotDbContext>();
                var userLink2 = await db2.UserLinks.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
                if (userLink2 != null)
                {
                    userLink2.PendingInvoiceData = JsonSerializer.Serialize(new
                    {
                        amount,
                        category,
                        description,
                        date,
                        type = "Expense"
                    });
                    await db2.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Photo processing error: {ex.Message}");
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Bir hata oluştu. Lütfen tekrar dene.",
                    cancellationToken: ct
                );
            }
        }

        private async Task HandleTextMessage(
            ITelegramBotClient botClient,
            long chatId,
            string text,
            BotUserLink link,
            CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var userLink = await db.UserLinks.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);

            if (userLink != null && !string.IsNullOrEmpty(userLink.PendingInvoiceData))
            {
                await HandleInvoiceConfirmation(botClient, chatId, text, userLink, db, ct);
                return;
            }

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);

            try
            {
                link = await RefreshTokenIfNeeded(link, db, ct) ?? link;

                var aiService = scope.ServiceProvider.GetRequiredService<OpenAiService>();
                var result = await aiService.ProcessMessageAsync(text, link, ct);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: result,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI error: {ex.Message}");
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Bir hata oluştu. Lütfen tekrar dene.",
                    cancellationToken: ct
                );
            }
        }
        private async Task HandleInvoiceConfirmation(
            ITelegramBotClient botClient,
            long chatId,
            string text,
            BotUserLink userLink,
            BotDbContext db,
            CancellationToken ct)
        {
            var lower = text.Trim().ToLower();

            if (lower == "evet" || lower == "e" || lower == "yes" || lower == "👍")
            {
                try
                {
                    var invoiceData = JsonSerializer.Deserialize<JsonElement>(userLink.PendingInvoiceData!);
                    decimal amount = invoiceData.GetProperty("amount").GetDecimal();
                    string category = invoiceData.GetProperty("category").GetString() ?? "Diğer";
                    string description = invoiceData.GetProperty("description").GetString() ?? "";
                    string date = invoiceData.GetProperty("date").GetString() ?? DateTime.Now.ToString("yyyy-MM-dd");

                    var apiClient = _httpClientFactory.CreateClient("FinAwareApi");
                    apiClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", userLink.JwtToken);

                    var catResponse = await apiClient.GetAsync("/api/category", ct);
                    var catJson = await catResponse.Content.ReadAsStringAsync(ct);
                    var categories = JsonSerializer.Deserialize<JsonElement>(catJson);

                    int categoryId = 0;
                    foreach (var cat in categories.EnumerateArray())
                    {
                        var catName = cat.GetProperty("name").GetString() ?? "";
                        if (catName.ToLower() == category.ToLower())
                        {
                            categoryId = cat.GetProperty("categoryId").GetInt32();
                            break;
                        }
                    }

                    if (categoryId == 0)
                    {
                        foreach (var cat in categories.EnumerateArray())
                        {
                            if (cat.GetProperty("name").GetString() == "Diğer")
                            {
                                categoryId = cat.GetProperty("categoryId").GetInt32();
                                break;
                            }
                        }
                    }

                    var payload = new
                    {
                        amount,
                        type = "Expense",
                        date = DateTime.Parse(date),
                        description,
                        categoryId,
                        currency = "TRY"
                    };

                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await apiClient.PostAsync("/api/transaction", content, ct);

                    userLink.PendingInvoiceData = null;
                    await db.SaveChangesAsync(ct);

                    if (response.IsSuccessStatusCode)
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"✅ *İşlem eklendi!*\n\n" +
                                  $"💸 ₺{amount:N2} - {category}\n" +
                                  $"📝 {(string.IsNullOrEmpty(description) ? "Açıklama yok" : description)}\n\n" +
                                  $"Başka bir şey eklemek ister misin? 😊",
                            parseMode: ParseMode.Markdown,
                            cancellationToken: ct
                        );
                    }
                    else
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: "❌ İşlem eklenemedi. Lütfen tekrar dene.",
                            cancellationToken: ct
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Invoice confirm error: {ex.Message}");
                    userLink.PendingInvoiceData = null;
                    await db.SaveChangesAsync(ct);
                    await botClient.SendMessage(chatId: chatId, text: "⚠️ Hata oluştu.", cancellationToken: ct);
                }
            }
            else if (lower == "hayır" || lower == "h" || lower == "no" || lower == "👎")
            {
                userLink.PendingInvoiceData = null;
                await db.SaveChangesAsync(ct);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ İptal edildi. Başka bir şey yapabilir miyim? 😊",
                    cancellationToken: ct
                );
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❓ Lütfen *evet* veya *hayır* yaz.\n\nFaturayı ekleyeyim mi?",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
            }
        }
        private async Task HandleStartCommand(
            ITelegramBotClient botClient,
            long chatId,
            string username,
            string text,
            CancellationToken ct)
        {
            var parts = text.Split(' ');
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "👋 Merhaba! Ben *FinAware Asistan*!\n\n" +
                          "Seninle hesabını bağlamak için FinAware profilinden " +
                          "'Telegram'a Bağlan' butonuna tıkla ve linki aç! 🔗",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
                return;
            }

            var linkToken = parts[1].Trim();
            Console.WriteLine($"🔗 Link token alındı: {linkToken} | ChatId: {chatId}");

            try
            {
                var client = _httpClientFactory.CreateClient("FinAwareApi");
                var botSecret = _configuration["FinAwareApi:BotSecret"];

                var payload = new { botSecret, linkToken, telegramChatId = chatId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/auth/bot-link", content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<JsonElement>(responseText);

                    var finAwareUsername = result.GetProperty("username").GetString() ?? "Kullanıcı";
                    var finAwareUserId = result.GetProperty("userId").GetInt32();
                    var jwtToken = result.GetProperty("token").GetString() ?? "";

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                    var existing = await db.UserLinks.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
                    if (existing != null)
                    {
                        existing.FinAwareUserId = finAwareUserId;
                        existing.FinAwareUsername = finAwareUsername;
                        existing.JwtToken = jwtToken;
                        existing.LinkedAt = DateTime.Now;
                        existing.TokenExpiresAt = DateTime.Now.AddDays(7);
                        existing.TelegramUsername = username;
                    }
                    else
                    {
                        db.UserLinks.Add(new BotUserLink
                        {
                            TelegramChatId = chatId,
                            TelegramUsername = username,
                            FinAwareUserId = finAwareUserId,
                            FinAwareUsername = finAwareUsername,
                            JwtToken = jwtToken,
                            LinkedAt = DateTime.Now,
                            TokenExpiresAt = DateTime.Now.AddDays(7)
                        });
                    }

                    await db.SaveChangesAsync(ct);
                    Console.WriteLine($"✅ Bağlantı başarılı: @{username} ↔ {finAwareUsername}");

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"🎉 *Harika! Hesabın başarıyla bağlandı!*\n\n" +
                              $"👤 FinAware Kullanıcısı: *{finAwareUsername}*\n\n" +
                              $"Artık benimle konuşarak:\n" +
                              $"💸 Harcama ekleyebilirsin\n" +
                              $"📸 Fatura fotoğrafı atabilirsin\n" +
                              $"📊 Bütçeni sorgulayabilirsin\n" +
                              $"💰 Birikimlerini takip edebilirsin\n\n" +
                              $"Hadi başlayalım! Bir şey yaz veya fatura fotoğrafı at 📸",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct
                    );
                }
                else
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Bağlantı kurulamadı.\n\n" +
                              "Token geçersiz veya süresi dolmuş olabilir.\n" +
                              "FinAware profilinden yeni bir link oluşturmayı dene! 🔄",
                        cancellationToken: ct
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bot link exception: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "⚠️ Bir hata oluştu, lütfen tekrar dene.", cancellationToken: ct);
            }
        }
        private async Task<BotUserLink?> RefreshTokenIfNeeded(
            BotUserLink link, BotDbContext db, CancellationToken ct)
        {
            if (link.TokenExpiresAt > DateTime.Now.AddDays(1))
                return link;

            try
            {
                var client = _httpClientFactory.CreateClient("FinAwareApi");
                var botSecret = _configuration["FinAwareApi:BotSecret"];

                var payload = new { botSecret, telegramChatId = link.TelegramChatId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/auth/bot-resolve", content, ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                var responseText = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<JsonElement>(responseText);

                link.JwtToken = result.GetProperty("token").GetString() ?? "";
                link.TokenExpiresAt = DateTime.Now.AddDays(7);
                await db.SaveChangesAsync(ct);

                Console.WriteLine($"✅ Token yenilendi: {link.FinAwareUsername}");
                return link;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Token yenileme hatası: {ex.Message}");
                return null;
            }
        }

        private Task HandleErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken ct)
        {
            Console.WriteLine($"❌ Telegram hata: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}