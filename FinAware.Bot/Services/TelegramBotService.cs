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
            Console.WriteLine($"✅ Bot bağlandı: @{me.Username}");
            Console.WriteLine("📡 Mesajlar dinleniyor...");

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

        // ANA YÖNLENDİRİCİ
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
                    text: "Merhaba! FinAware Asistan'a hoş geldin.\n\n" +
                          "Botu kullanmak için önce FinAware hesabınla bağlantı kurman gerekiyor.\n\n" +
                          "Nasıl bağlanırım?\n" +
                          "1. FinAware web sitesine gir\n" +
                          "2. Profil sayfasına git\n" +
                          "3. Telegram'a Bağlan butonuna tıkla\n" +
                          "4. Oluşan linki Telegram'da aç",
                    cancellationToken: ct
                );
                return;
            }

            link = await RefreshTokenIfNeeded(link, db, ct) ?? link;

            // Fotoğraf
            if (message.Photo != null && message.Photo.Length > 0)
            {
                await HandlePhotoMessage(botClient, chatId, message, link, ct);
                return;
            }

            if (message.Text is { } text)
            {
                var parts = text.Trim().Split(' ');
                var cmd = parts[0].ToLower();

                switch (cmd)
                {
                    case "/bakiye":
                        await HandleBakiyeCommand(botClient, chatId, link, ct);
                        return;
                    case "/islemler":
                        await HandleIslemlerCommand(botClient, chatId, link, ct);
                        return;
                    case "/butce":
                        await HandleButceCommand(botClient, chatId, link, ct);
                        return;
                    case "/birikim":
                        await HandleBirikimCommand(botClient, chatId, link, ct);
                        return;
                    case "/ozet":
                        await HandleOzetCommand(botClient, chatId, link, ct);
                        return;
                    case "/kategoriler":
                        await HandleKategorilerCommand(botClient, chatId, link, ct);
                        return;
                    case "/gelir":
                        await HandleGelirCommand(botClient, chatId, parts, link, ct);
                        return;
                    case "/gider":
                        await HandleGiderCommand(botClient, chatId, parts, link, ct);
                        return;
                    case "/butcelimit":
                        await HandleButceLimitCommand(botClient, chatId, parts, link, ct);
                        return;
                    case "/birikimyeni":
                        await HandleBirikimYeniCommand(botClient, chatId, parts, link, ct);
                        return;
                    case "/birikimyatir":
                        await HandleBirikimYatirCommand(botClient, chatId, parts, link, ct);
                        return;
                    case "/yeniuye":
                        await HandleYeniUyeCommand(botClient, chatId, link, ct);
                        return;
                    case "/yardim":
                        await HandleYardimCommand(botClient, chatId, ct);
                        return;
                }

                await HandleTextMessage(botClient, chatId, text, link, ct);
                return;
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: "Metin mesajı veya fatura fotoğrafı gönderebilirsin.",
                cancellationToken: ct
            );
        }

        // /bakiye
        private async Task HandleBakiyeCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var response = await client.GetAsync("/api/transaction", ct);
                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId, text: "İşlemler alınamadı.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                decimal gelir = 0, gider = 0;
                int ay = DateTime.Now.Month, yil = DateTime.Now.Year;
                decimal aylikGelir = 0, aylikGider = 0;

                foreach (var t in transactions.EnumerateArray())
                {
                    var amount = t.GetProperty("amount").GetDecimal();
                    var type = t.GetProperty("type").GetString();
                    var date = t.TryGetProperty("date", out var dateEl)
                        ? DateTime.Parse(dateEl.GetString() ?? DateTime.Now.ToString())
                        : DateTime.Now;

                    if (type == "Income") gelir += amount;
                    else gider += amount;

                    if (date.Month == ay && date.Year == yil)
                    {
                        if (type == "Income") aylikGelir += amount;
                        else aylikGider += amount;
                    }
                }

                var bakiye = gelir - gider;
                var aylikBakiye = aylikGelir - aylikGider;

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"💰 Finansal Durumun\n\n" +
                          $"📊 Genel Bakiye\n" +
                          $"Toplam Gelir: +₺{gelir:N2}\n" +
                          $"Toplam Gider: -₺{gider:N2}\n" +
                          $"Net Bakiye: ₺{bakiye:N2}\n\n" +
                          $"📅 Bu Ay ({DateTime.Now:MMMM yyyy})\n" +
                          $"Gelir: +₺{aylikGelir:N2}\n" +
                          $"Gider: -₺{aylikGider:N2}\n" +
                          $"Net: ₺{aylikBakiye:N2}",
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bakiye error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // /islemler 
        private async Task HandleIslemlerCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var response = await client.GetAsync("/api/transaction", ct);
                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId, text: "İşlemler alınamadı.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                var sb = new StringBuilder();
                sb.AppendLine("📋 Son 10 İşlem\n");

                int count = 0;
                foreach (var t in transactions.EnumerateArray())
                {
                    if (count >= 10) break;
                    var amount = t.GetProperty("amount").GetDecimal();
                    var type = t.GetProperty("type").GetString();
                    var desc = t.TryGetProperty("description", out var descEl) ? descEl.GetString() : "";
                    var catName = "";
                    if (t.TryGetProperty("category", out var catEl) && catEl.ValueKind != JsonValueKind.Null)
                        catName = catEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                    var date = t.TryGetProperty("date", out var dateEl)
                        ? DateTime.Parse(dateEl.GetString() ?? "").ToString("dd.MM.yyyy")
                        : "";

                    var prefix = type == "Income" ? "+" : "-";
                    var emoji = type == "Income" ? "💚" : "🔴";
                    sb.AppendLine($"{emoji} {prefix}₺{amount:N2} | {catName}");
                    if (!string.IsNullOrEmpty(desc)) sb.AppendLine($"   {desc}");
                    sb.AppendLine($"   {date}");
                    sb.AppendLine();
                    count++;
                }

                if (count == 0) sb.AppendLine("Henüz işlem yok.");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ İşlemler error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        //  /butce
        private async Task HandleButceCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var ay = DateTime.Now.Month;
                var yil = DateTime.Now.Year;
                var response = await client.GetAsync($"/api/budget?month={ay}&year={yil}", ct);

                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId, text: "Bütçe bilgileri alınamadı.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var sb = new StringBuilder();
                sb.AppendLine($"💳 Bütçe Durumu - {DateTime.Now:MMMM yyyy}\n");

                if (data.TryGetProperty("budgets", out var budgets))
                {
                    int count = 0;
                    foreach (var b in budgets.EnumerateArray())
                    {
                        var limit = b.GetProperty("limitAmount").GetDecimal();
                        var spent = b.TryGetProperty("spentAmount", out var spentEl) ? spentEl.GetDecimal() : 0;
                        var remaining = limit - spent;
                        var pct = limit > 0 ? (spent / limit) * 100 : 0;
                        var catName = "Genel";
                        if (b.TryGetProperty("category", out var catEl) && catEl.ValueKind != JsonValueKind.Null)
                            catName = catEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Genel" : "Genel";

                        var durum = pct >= 100 ? "🔴 AŞILDI" : pct >= 80 ? "⚠️ Dikkat" : "✅ Normal";
                        sb.AppendLine($"📂 {catName}");
                        sb.AppendLine($"Limit: ₺{limit:N2}");
                        sb.AppendLine($"Harcama: ₺{spent:N2} (%{pct:N0})");
                        sb.AppendLine($"Kalan: ₺{remaining:N2} | {durum}");
                        sb.AppendLine();
                        count++;
                    }
                    if (count == 0) sb.AppendLine("Bu ay için bütçe tanımlanmamış.\nYeni bütçe için /butcelimit komutunu kullan.");
                }

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bütçe error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // /birikim
        private async Task HandleBirikimCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var response = await client.GetAsync("/api/saving", ct);

                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId, text: "Birikim bilgileri alınamadı.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var savings = JsonSerializer.Deserialize<JsonElement>(json);

                var sb = new StringBuilder();
                sb.AppendLine("🐷 Birikim Hedeflerin\n");

                int count = 0;
                foreach (var s in savings.EnumerateArray())
                {
                    var goalName = s.GetProperty("goalName").GetString();
                    var target = s.GetProperty("targetAmount").GetDecimal();
                    var current = s.GetProperty("currentAmount").GetDecimal();
                    var progress = target > 0 ? (current / target) * 100 : 0;
                    var remaining = target - current;
                    var icon = s.TryGetProperty("icon", out var iconEl) ? iconEl.GetString() ?? "💰" : "💰";
                    var targetDate = s.TryGetProperty("targetDate", out var tdEl) && tdEl.ValueKind != JsonValueKind.Null
                        ? DateTime.Parse(tdEl.GetString() ?? "").ToString("dd.MM.yyyy")
                        : "Süresiz";

                    sb.AppendLine($"{icon} {goalName}");
                    sb.AppendLine($"Hedef: ₺{target:N2}");
                    sb.AppendLine($"Birikim: ₺{current:N2} (%{progress:N0})");
                    sb.AppendLine($"Kalan: ₺{remaining:N2}");
                    sb.AppendLine($"Tarih: {targetDate}");
                    sb.AppendLine();
                    count++;
                }

                if (count == 0)
                    sb.AppendLine("Henüz birikim hedefi yok.\nYeni hedef için /birikimyeni komutunu kullan.");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Birikim error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // /özet
        private async Task HandleOzetCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var response = await client.GetAsync("/api/transaction", ct);

                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId, text: "Veriler alınamadı.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);
                var ay = DateTime.Now.Month;
                var yil = DateTime.Now.Year;

                decimal aylikGelir = 0, aylikGider = 0;
                var kategoriHarcamalar = new Dictionary<string, decimal>();

                foreach (var t in transactions.EnumerateArray())
                {
                    var dateStr = t.TryGetProperty("date", out var dateEl) ? dateEl.GetString() : null;
                    if (dateStr == null) continue;
                    var date = DateTime.Parse(dateStr);
                    if (date.Month != ay || date.Year != yil) continue;

                    var amount = t.GetProperty("amount").GetDecimal();
                    var type = t.GetProperty("type").GetString();
                    var catName = "";
                    if (t.TryGetProperty("category", out var catEl) && catEl.ValueKind != JsonValueKind.Null)
                        catName = catEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Diğer" : "Diğer";

                    if (type == "Income") aylikGelir += amount;
                    else
                    {
                        aylikGider += amount;
                        if (!string.IsNullOrEmpty(catName))
                            kategoriHarcamalar[catName] = kategoriHarcamalar.GetValueOrDefault(catName) + amount;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"📊 Aylık Özet - {DateTime.Now:MMMM yyyy}\n");
                sb.AppendLine($"💚 Gelir: +₺{aylikGelir:N2}");
                sb.AppendLine($"🔴 Gider: -₺{aylikGider:N2}");
                sb.AppendLine($"💰 Net: ₺{(aylikGelir - aylikGider):N2}\n");

                if (kategoriHarcamalar.Any())
                {
                    sb.AppendLine("🏆 En Yüksek Harcamalar");
                    foreach (var kv in kategoriHarcamalar.OrderByDescending(x => x.Value).Take(5))
                        sb.AppendLine($"  {kv.Key}: ₺{kv.Value:N2}");
                }

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Özet error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        //  /kategoriler 
        private async Task HandleKategorilerCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var response = await client.GetAsync("/api/category", ct);

                if (!response.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId, text: "Kategoriler alınamadı.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var categories = JsonSerializer.Deserialize<JsonElement>(json);

                var gelirler = new List<string>();
                var giderler = new List<string>();

                foreach (var c in categories.EnumerateArray())
                {
                    var name = c.GetProperty("name").GetString() ?? "";
                    var type = c.GetProperty("type").GetString();
                    var icon = c.TryGetProperty("icon", out var iconEl) ? iconEl.GetString() ?? "" : "";

                    if (type == "Income") gelirler.Add($"{icon} {name}");
                    else giderler.Add($"{icon} {name}");
                }

                var sb = new StringBuilder();
                sb.AppendLine("📂 Kategorilerin\n");
                sb.AppendLine("💚 Gelir Kategorileri");
                foreach (var g in gelirler) sb.AppendLine($"  {g}");
                sb.AppendLine();
                sb.AppendLine("🔴 Gider Kategorileri");
                foreach (var g in giderler) sb.AppendLine($"  {g}");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Kategoriler error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        //  /gelir <miktar> <kategori> [açıklama]
        private async Task HandleGelirCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanım: /gelir <miktar> <kategori> [açıklama]\n\nÖrnek:\n/gelir 25000 Maaş Haziran maaşı",
                    cancellationToken: ct);
                return;
            }

            if (!decimal.TryParse(parts[1].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Geçersiz miktar. Örnek: /gelir 25000 Maaş", cancellationToken: ct);
                return;
            }

            var kategoriAdi = parts[2];
            var aciklama = parts.Length > 3 ? string.Join(" ", parts.Skip(3)) : "";

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            await EkleIslem(botClient, chatId, link, amount, "Income", kategoriAdi, aciklama, ct);
        }

        // /gider <miktar> <kategori> [açıklama]
        private async Task HandleGiderCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanım: /gider <miktar> <kategori> [açıklama]\n\nÖrnek:\n/gider 150 Market Haftalık alışveriş",
                    cancellationToken: ct);
                return;
            }

            if (!decimal.TryParse(parts[1].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Geçersiz miktar. Örnek: /gider 150 Market", cancellationToken: ct);
                return;
            }

            var kategoriAdi = parts[2];
            var aciklama = parts.Length > 3 ? string.Join(" ", parts.Skip(3)) : "";

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            await EkleIslem(botClient, chatId, link, amount, "Expense", kategoriAdi, aciklama, ct);
        }

        // İşlem Ekleme Yardımcı
        private async Task EkleIslem(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, decimal amount, string type,
            string kategoriAdi, string aciklama, CancellationToken ct)
        {
            try
            {
                var client = CreateApiClient(link);
                var catRes = await client.GetAsync("/api/category", ct);
                var catJson = await catRes.Content.ReadAsStringAsync(ct);
                var cats = JsonSerializer.Deserialize<JsonElement>(catJson);

                int categoryId = 0;
                foreach (var c in cats.EnumerateArray())
                {
                    var cName = c.GetProperty("name").GetString() ?? "";
                    var cType = c.GetProperty("type").GetString() ?? "";
                    if (cName.ToLower().Contains(kategoriAdi.ToLower()) &&
                        ((type == "Income" && cType == "Income") || (type == "Expense" && cType == "Expense")))
                    {
                        categoryId = c.GetProperty("categoryId").GetInt32();
                        break;
                    }
                }

                // Kategori bulunamazsa ilk uygun kategoriyi al
                if (categoryId == 0)
                {
                    foreach (var c in cats.EnumerateArray())
                    {
                        var cType = c.GetProperty("type").GetString() ?? "";
                        if ((type == "Income" && cType == "Income") || (type == "Expense" && cType == "Expense"))
                        {
                            categoryId = c.GetProperty("categoryId").GetInt32();
                            break;
                        }
                    }
                }

                var payload = new { amount, type, date = DateTime.Now, description = aciklama, categoryId, currency = "TRY" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/api/transaction", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    var emoji = type == "Income" ? "💚" : "🔴";
                    var tip = type == "Income" ? "Gelir" : "Gider";
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"{emoji} {tip} eklendi!\n\n" +
                              $"Miktar: ₺{amount:N2}\n" +
                              $"Kategori: {kategoriAdi}\n" +
                              (string.IsNullOrEmpty(aciklama) ? "" : $"Açıklama: {aciklama}\n") +
                              $"Tarih: {DateTime.Now:dd.MM.yyyy}",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "İşlem eklenemedi. Kategori adını kontrol et.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ İşlem ekle error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        //  /butcelimit <kategori> <limit> 
        private async Task HandleButceLimitCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanım: /butcelimit <kategori> <limit>\n\nÖrnek:\n/butcelimit Market 2000",
                    cancellationToken: ct);
                return;
            }

            var kategoriAdi = parts[1];
            if (!decimal.TryParse(parts[2].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal limit) || limit <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Geçersiz limit. Örnek: /butcelimit Market 2000", cancellationToken: ct);
                return;
            }

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var catRes = await client.GetAsync("/api/category", ct);
                var catJson = await catRes.Content.ReadAsStringAsync(ct);
                var cats = JsonSerializer.Deserialize<JsonElement>(catJson);

                int? categoryId = null;
                foreach (var c in cats.EnumerateArray())
                {
                    var cName = c.GetProperty("name").GetString() ?? "";
                    var cType = c.GetProperty("type").GetString() ?? "";
                    if (cName.ToLower().Contains(kategoriAdi.ToLower()) && cType == "Expense")
                    {
                        categoryId = c.GetProperty("categoryId").GetInt32();
                        break;
                    }
                }

                var payload = new
                {
                    limitAmount = limit,
                    month = DateTime.Now.Month,
                    year = DateTime.Now.Year,
                    categoryId
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/api/budget", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"✅ Bütçe limiti oluşturuldu!\n\n" +
                              $"Kategori: {kategoriAdi}\n" +
                              $"Limit: ₺{limit:N2}\n" +
                              $"Dönem: {DateTime.Now:MMMM yyyy}",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Bütçe limiti oluşturulamadı.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bütçe limit error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        //  /birikimyeni <hedef_adi> <miktar> [tarih] 
        private async Task HandleBirikimYeniCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanım: /birikimyeni <hedef_adı> <hedef_miktar> [bitiş_tarihi]\n\nÖrnek:\n/birikimyeni Tatil 15000 2026-12-31",
                    cancellationToken: ct);
                return;
            }

            var goalName = parts[1];
            if (!decimal.TryParse(parts[2].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal target) || target <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Geçersiz miktar.", cancellationToken: ct);
                return;
            }

            DateTime? targetDate = null;
            if (parts.Length > 3 && DateTime.TryParse(parts[3], out var td))
                targetDate = td;

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var payload = new { goalName, targetAmount = target, currentAmount = 0, targetDate, icon = "💰", color = "#1AAFA3" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/api/saving", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"✅ Birikim hedefi oluşturuldu!\n\n" +
                              $"💰 Hedef: {goalName}\n" +
                              $"Tutar: ₺{target:N2}\n" +
                              (targetDate.HasValue ? $"Bitiş: {targetDate.Value:dd.MM.yyyy}\n" : "") +
                              $"\nBirikime para eklemek için:\n/birikimyatir {goalName} <miktar>",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Birikim hedefi oluşturulamadı.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Birikim yeni error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // /birikimyatir <hedef_adi> <miktar> 
        private async Task HandleBirikimYatirCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanım: /birikimyatir <hedef_adı> <miktar>\n\nÖrnek:\n/birikimyatir Tatil 500",
                    cancellationToken: ct);
                return;
            }

            var goalName = parts[1];
            if (!decimal.TryParse(parts[2].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Geçersiz miktar.", cancellationToken: ct);
                return;
            }

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var savRes = await client.GetAsync("/api/saving", ct);
                var savJson = await savRes.Content.ReadAsStringAsync(ct);
                var savings = JsonSerializer.Deserialize<JsonElement>(savJson);

                int savingId = 0;
                decimal currentAmt = 0;
                string foundGoal = "";

                foreach (var s in savings.EnumerateArray())
                {
                    var name = s.GetProperty("goalName").GetString() ?? "";
                    if (name.ToLower().Contains(goalName.ToLower()))
                    {
                        savingId = s.GetProperty("savingId").GetInt32();
                        currentAmt = s.GetProperty("currentAmount").GetDecimal();
                        foundGoal = name;
                        break;
                    }
                }

                if (savingId == 0)
                {
                    await botClient.SendMessage(chatId: chatId, text: $"'{goalName}' adında birikim hedefi bulunamadı.\n/birikim ile listeni görebilirsin.", cancellationToken: ct);
                    return;
                }

                var payload = new { currentAmount = currentAmt + amount };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PutAsync($"/api/saving/{savingId}", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"✅ Birikime para eklendi!\n\n" +
                              $"💰 Hedef: {foundGoal}\n" +
                              $"Eklenen: +₺{amount:N2}\n" +
                              $"Yeni toplam: ₺{currentAmt + amount:N2}",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Birikim güncellenemedi.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Birikim yatır error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // /yeniuye
        private async Task HandleYeniUyeCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Merhaba {link.FinAwareUsername}! FinAware Asistan'a hoş geldin!\n\n" +
                      "Seni hızlıca başlatmak için birkaç adım:\n\n" +
                      "1️⃣ Bakiyeni Gör\n" +
                      "/bakiye - Anlık finansal durumun\n\n" +
                      "2️⃣ İlk Gelirini Ekle\n" +
                      "/gelir 25000 Maaş Haziran maaşı\n\n" +
                      "3️⃣ Harcama Ekle\n" +
                      "/gider 150 Market Haftalık alışveriş\n\n" +
                      "4️⃣ Bütçe Limiti Belirle\n" +
                      "/butcelimit Market 2000\n\n" +
                      "5️⃣ Birikim Hedefi Oluştur\n" +
                      "/birikimyeni Tatil 15000 2026-12-31\n\n" +
                      "6️⃣ Birikime Para Yatır\n" +
                      "/birikimyatir Tatil 500\n\n" +
                      "Tüm komutlar için /yardim yaz!\n\n" +
                      "Fatura fotoğrafı da gönderebilirsin, otomatik analiz ederim.",
                cancellationToken: ct
            );
        }

        // /yardim
        private async Task HandleYardimCommand(
            ITelegramBotClient botClient, long chatId, CancellationToken ct)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "FinAware Bot Komutları\n\n" +
                      "📊 Görüntüleme\n" +
                      "/bakiye - Anlık bakiye ve özet\n" +
                      "/islemler - Son 10 işlem\n" +
                      "/butce - Bu ayki bütçe durumu\n" +
                      "/birikim - Birikim hedefleri\n" +
                      "/ozet - Aylık gelir/gider özeti\n" +
                      "/kategoriler - Kategori listesi\n\n" +
                      "➕ İşlem Ekleme\n" +
                      "/gelir <miktar> <kategori> [açıklama]\n" +
                      "/gider <miktar> <kategori> [açıklama]\n\n" +
                      "💳 Bütçe\n" +
                      "/butcelimit <kategori> <limit>\n\n" +
                      "🐷 Birikim\n" +
                      "/birikimyeni <isim> <hedef> [tarih]\n" +
                      "/birikimyatir <isim> <miktar>\n\n" +
                      "👋 Diğer\n" +
                      "/yeniuye - Başlangıç rehberi\n" +
                      "/yardim - Bu menü\n\n" +
                      "💬 Doğal Dil\n" +
                      "Markete 150 lira harcadım\n" +
                      "Maaşım 25000 TL yattı\n" +
                      "Bu ay ne kadar harcadım?\n\n" +
                      "📸 Fatura / Fiş\n" +
                      "Fatura fotoğrafı gönder, otomatik analiz ederim.",
                cancellationToken: ct
            );
        }

        // FATURA FOTOĞRAFI
        private async Task HandlePhotoMessage(
            ITelegramBotClient botClient,
            long chatId,
            Message message,
            BotUserLink link,
            CancellationToken ct)
        {
            await botClient.SendMessage(chatId: chatId, text: "🔍 Fatura analiz ediliyor...", cancellationToken: ct);

            try
            {
                var photo = message.Photo!.OrderByDescending(p => p.FileSize).First();
                var file = await botClient.GetFile(photo.FileId, ct);
                var tempPath = Path.Combine(Path.GetTempPath(), $"finaware_bot_{Guid.NewGuid()}.jpg");

                await using (var fileStream = System.IO.File.OpenWrite(tempPath))
                {
                    await botClient.DownloadFile(file.FilePath!, fileStream, ct);
                }

                var apiClient = CreateApiClient(link);

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
                    await botClient.SendMessage(chatId: chatId,
                        text: "Fatura okunamadı. Manuel olarak yazabilirsin.\nÖrnek: Markete 150 lira harcadım",
                        cancellationToken: ct);
                    return;
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseText);
                bool success = result.TryGetProperty("success", out var successEl) && successEl.GetBoolean();

                if (!success)
                {
                    await botClient.SendMessage(chatId: chatId,
                        text: "Faturayı okuyamadım. Daha net bir fotoğraf çekmeyi dene.",
                        cancellationToken: ct);
                    return;
                }

                decimal amount = result.TryGetProperty("amount", out var amountEl) ? amountEl.GetDecimal() : 0;
                string category = result.TryGetProperty("category", out var catEl) ? catEl.GetString() ?? "Diğer" : "Diğer";
                string description = result.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
                string date = result.TryGetProperty("date", out var dateEl) ? dateEl.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"🧾 Fatura Okundu\n\n" +
                          $"Tutar: ₺{amount:N2}\n" +
                          $"Kategori: {category}\n" +
                          $"Açıklama: {(string.IsNullOrEmpty(description) ? "Belirtilmedi" : description)}\n" +
                          $"Tarih: {date}\n\n" +
                          $"Ekleyeyim mi? (evet / hayır)",
                    cancellationToken: ct
                );

                using var scope2 = _serviceProvider.CreateScope();
                var db2 = scope2.ServiceProvider.GetRequiredService<BotDbContext>();
                var userLink2 = await db2.UserLinks.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);
                if (userLink2 != null)
                {
                    userLink2.PendingInvoiceData = JsonSerializer.Serialize(new { amount, category, description, date, type = "Expense" });
                    await db2.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Photo error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // METİN MESAJI 
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
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // FATURA ONAY
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
                    string desc = invoiceData.GetProperty("description").GetString() ?? "";
                    string date = invoiceData.GetProperty("date").GetString() ?? DateTime.Now.ToString("yyyy-MM-dd");

                    var apiClient = CreateApiClient(userLink);
                    var catRes = await apiClient.GetAsync("/api/category", ct);
                    var catJson = await catRes.Content.ReadAsStringAsync(ct);
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

                    var payload = new { amount, type = "Expense", date = DateTime.Parse(date), description = desc, categoryId, currency = "TRY" };
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var res = await apiClient.PostAsync("/api/transaction", content, ct);

                    userLink.PendingInvoiceData = null;
                    await db.SaveChangesAsync(ct);

                    if (res.IsSuccessStatusCode)
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"✅ İşlem eklendi!\n\n🔴 -₺{amount:N2} - {category}\n{(string.IsNullOrEmpty(desc) ? "" : desc)}",
                            cancellationToken: ct);
                    }
                    else
                    {
                        await botClient.SendMessage(chatId: chatId, text: "İşlem eklenemedi.", cancellationToken: ct);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Invoice confirm error: {ex.Message}");
                    userLink.PendingInvoiceData = null;
                    await db.SaveChangesAsync(ct);
                    await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
                }
            }
            else if (lower == "hayır" || lower == "h" || lower == "no" || lower == "👎")
            {
                userLink.PendingInvoiceData = null;
                await db.SaveChangesAsync(ct);
                await botClient.SendMessage(chatId: chatId, text: "İptal edildi.", cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(chatId: chatId, text: "Lütfen evet veya hayır yaz.", cancellationToken: ct);
            }
        }

        // HESAP BAĞLAMA
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
                    text: "Merhaba! Ben FinAware Asistan.\n\n" +
                          "Hesabını bağlamak için FinAware profilinden Telegram'a Bağlan butonuna tıkla.",
                    cancellationToken: ct
                );
                return;
            }

            var linkToken = parts[1].Trim();
            Console.WriteLine($"🔗 Link token: {linkToken} | ChatId: {chatId}");

            try
            {
                var client = _httpClientFactory.CreateClient("FinAwareApi");
                var botSecret = _configuration["FinAwareApi__BotSecret"]
                              ?? _configuration["FinAwareApi:BotSecret"];

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
                    Console.WriteLine($"✅ Bağlantı: @{username} ↔ {finAwareUsername}");

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"✅ Hesabın bağlandı!\n\n" +
                              $"FinAware Kullanıcısı: {finAwareUsername}\n\n" +
                              $"Başlamak için /yeniuye yazabilirsin.\n" +
                              $"Tüm komutlar için /yardim yaz.",
                        cancellationToken: ct
                    );
                }
                else
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Bağlantı kurulamadı. Token geçersiz olabilir. Yeni link oluşturmayı dene.",
                        cancellationToken: ct
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bot link error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata oluştu.", cancellationToken: ct);
            }
        }

        // TOKEN YENİLE
        private async Task<BotUserLink?> RefreshTokenIfNeeded(
            BotUserLink link, BotDbContext db, CancellationToken ct)
        {
            if (link.TokenExpiresAt > DateTime.Now.AddDays(1))
                return link;

            try
            {
                var client = _httpClientFactory.CreateClient("FinAwareApi");
                var botSecret = _configuration["FinAwareApi__BotSecret"]
                              ?? _configuration["FinAwareApi:BotSecret"];

                var payload = new { botSecret, telegramChatId = link.TelegramChatId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/auth/bot-resolve", content, ct);

                if (!response.IsSuccessStatusCode) return null;

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

        // YARDIMCI
        private HttpClient CreateApiClient(BotUserLink link)
        {
            var client = _httpClientFactory.CreateClient("FinAwareApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", link.JwtToken);
            return client;
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