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

        // ExecuteAsync retry mekanizmasi ile polling
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var me = await _botClient.GetMe(stoppingToken);
                    Console.WriteLine($"Bot baglandi: @{me.Username}");
                    Console.WriteLine("Mesajlar dinleniyor...");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                    var receiverOptions = new ReceiverOptions
                    {
                        AllowedUpdates = new[] { UpdateType.Message },
                        DropPendingUpdates = true
                    };

                    _botClient.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        errorHandler: (b, ex, c) =>
                        {
                            Console.WriteLine($"Telegram hata: {ex.Message}");
                            cts.Cancel();
                            return Task.CompletedTask;
                        },
                        receiverOptions: receiverOptions,
                        cancellationToken: cts.Token
                    );

                    await Task.Delay(Timeout.Infinite, cts.Token)
                              .ContinueWith(_ => { }, CancellationToken.None);

                    Console.WriteLine("Polling durdu, yeniden baslaniyor...");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Bot baglantisi kesildi: {ex.Message}");
                    Console.WriteLine("5 saniye sonra yeniden baglaniyor...");
                    await Task.Delay(5000, stoppingToken)
                              .ContinueWith(_ => { }, CancellationToken.None);
                }
            }
        }

        // Ana Yönlendirici
        private async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Update update,
            CancellationToken ct)
        {
            if (update.Message is not { } message) return;

            var chatId = message.Chat.Id;
            var username = message.From?.Username ?? message.From?.FirstName ?? "Kullanici";

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
                    text: "Merhaba! FinAware Asistan'a hos geldin.\n\n" +
                          "Botu kullanmak icin once FinAware hesabinla baglanti kurman gerekiyor.\n\n" +
                          "Nasil baglanilir?\n" +
                          "1. FinAware web sitesine gir\n" +
                          "2. Profil sayfasina git\n" +
                          "3. Telegram'a Baglan butonuna tikla\n" +
                          "4. Olusan linki Telegram'da ac",
                    cancellationToken: ct
                );
                return;
            }

            link = await RefreshTokenIfNeeded(link, db, ct) ?? link;

            // Fotoğraf mesajı varsa önce onu işle, yoksa metin komutlarına geç
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
                    case "/analiz":
                        await HandleAnalizCommand(botClient, chatId, link, scope, ct);
                        return;
                    case "/profil":
                        await HandleProfilCommand(botClient, chatId, link, ct);
                        return;
                    case "/yeniuye":
                        await HandleYeniUyeCommand(botClient, chatId, link, ct);
                        return;
                    case "/yardim":
                        await HandleYardimCommand(botClient, chatId, ct);
                        return;
                }

                await HandleTextMessage(botClient, chatId, text, link, scope, ct);
                return;
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: "Metin mesaji veya fatura fotografı gonderebilirsin.",
                cancellationToken: ct
            );
        }

        // bakiye komutu
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
                    await botClient.SendMessage(chatId: chatId, text: "Islemler alinamadi.", cancellationToken: ct);
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
                var netEmoji = bakiye >= 0 ? "+" : "";
                var ayEmoji = aylikBakiye >= 0 ? "+" : "";

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Finansal Durumun\n\n" +
                          $"Genel Bakiye\n" +
                          $"Toplam Gelir: +{gelir:N2} TL\n" +
                          $"Toplam Gider: -{gider:N2} TL\n" +
                          $"Net Bakiye: {netEmoji}{bakiye:N2} TL\n\n" +
                          $"Bu Ay ({DateTime.Now:MMMM yyyy})\n" +
                          $"Gelir: +{aylikGelir:N2} TL\n" +
                          $"Gider: -{aylikGider:N2} TL\n" +
                          $"Net: {ayEmoji}{aylikBakiye:N2} TL",
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bakiye error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /işlemler
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
                    await botClient.SendMessage(chatId: chatId, text: "Islemler alinamadi.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                var sb = new StringBuilder();
                sb.AppendLine("Son 10 Islem\n");
                int count = 0;

                foreach (var t in transactions.EnumerateArray())
                {
                    if (count >= 10) break;
                    var amount = t.GetProperty("amount").GetDecimal();
                    var type = t.GetProperty("type").GetString();
                    var desc = t.TryGetProperty("description", out var descEl) ? descEl.GetString() : "";
                    var catName = t.TryGetProperty("categoryName", out var cnEl) ? cnEl.GetString() ?? "" : "";
                    var date = t.TryGetProperty("date", out var dateEl)
                        ? DateTime.Parse(dateEl.GetString() ?? "").ToString("dd.MM.yyyy")
                        : "";

                    var prefix = type == "Income" ? "+" : "-";
                    var tip = type == "Income" ? "Gelir" : "Gider";
                    sb.AppendLine($"{tip}: {prefix}{amount:N2} TL | {catName}");
                    if (!string.IsNullOrEmpty(desc)) sb.AppendLine($"   {desc}");
                    sb.AppendLine($"   {date}");
                    sb.AppendLine();
                    count++;
                }

                if (count == 0) sb.AppendLine("Henuz islem yok.");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Islemler error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /bütçe
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
                    await botClient.SendMessage(chatId: chatId, text: "Butce bilgileri alinamadi.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var sb = new StringBuilder();
                sb.AppendLine($"Butce Durumu - {DateTime.Now:MMMM yyyy}\n");

                var budgets = data.ValueKind == JsonValueKind.Array ? data :
                    (data.TryGetProperty("budgets", out var bEl) ? bEl : data);

                int count = 0;
                foreach (var b in budgets.EnumerateArray())
                {
                    var limit = b.TryGetProperty("limit", out var lEl) ? lEl.GetDecimal() :
                                b.TryGetProperty("limitAmount", out var laEl) ? laEl.GetDecimal() : 0;
                    var spent = b.TryGetProperty("spent", out var sEl) ? sEl.GetDecimal() :
                                b.TryGetProperty("spentAmount", out var saEl) ? saEl.GetDecimal() : 0;
                    var remaining = limit - spent;
                    var pct = limit > 0 ? (spent / limit) * 100 : 0;
                    var catName = b.TryGetProperty("categoryName", out var cnEl) ? cnEl.GetString() ?? "Genel" : "Genel";
                    var bar = BuildBar((int)pct);
                    var durum = pct >= 100 ? "ASILDI" : pct >= 80 ? "Dikkat" : "Normal";

                    sb.AppendLine($"{catName} - {durum}");
                    sb.AppendLine($"{bar} %{pct:N0}");
                    sb.AppendLine($"Limit: {limit:N2} TL");
                    sb.AppendLine($"Harcama: {spent:N2} TL");
                    sb.AppendLine($"Kalan: {remaining:N2} TL");
                    sb.AppendLine();
                    count++;
                }

                if (count == 0)
                    sb.AppendLine("Bu ay icin butce tanimlanmamis.\nYeni butce icin /butcelimit komutunu kullan.");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Butce error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
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
                    await botClient.SendMessage(chatId: chatId, text: "Birikim bilgileri alinamadi.", cancellationToken: ct);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var savings = JsonSerializer.Deserialize<JsonElement>(json);

                var sb = new StringBuilder();
                sb.AppendLine("Birikim Hedeflerin\n");
                int count = 0;

                foreach (var s in savings.EnumerateArray())
                {
                    var goalName = s.GetProperty("goalName").GetString();
                    var target = s.GetProperty("targetAmount").GetDecimal();
                    var current = s.TryGetProperty("currentAmount", out var cEl) ? cEl.GetDecimal() : 0;
                    var progress = target > 0 ? (current / target) * 100 : 0;
                    var remaining = target - current;
                    var icon = s.TryGetProperty("icon", out var iconEl) ? iconEl.GetString() ?? "" : "";
                    var bar = BuildBar((int)progress);

                    string tarih = "Suresiz";
                    if (s.TryGetProperty("targetDate", out var tdEl) && tdEl.ValueKind != JsonValueKind.Null && tdEl.GetString() != null)
                        tarih = DateTime.Parse(tdEl.GetString()!).ToString("dd.MM.yyyy");

                    sb.AppendLine($"{icon} {goalName}");
                    sb.AppendLine($"{bar} %{progress:N0}");
                    sb.AppendLine($"Hedef: {target:N2} TL");
                    sb.AppendLine($"Birikim: {current:N2} TL");
                    sb.AppendLine($"Kalan: {remaining:N2} TL");
                    sb.AppendLine($"Tarih: {tarih}");
                    sb.AppendLine();
                    count++;
                }

                if (count == 0)
                    sb.AppendLine("Henuz birikim hedefi yok.\nYeni hedef icin /birikimyeni komutunu kullan.");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Birikim error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
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
                    await botClient.SendMessage(chatId: chatId, text: "Veriler alinamadi.", cancellationToken: ct);
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
                    var catName = t.TryGetProperty("categoryName", out var cnEl) ? cnEl.GetString() ?? "Diger" : "Diger";

                    if (type == "Income") aylikGelir += amount;
                    else
                    {
                        aylikGider += amount;
                        if (!string.IsNullOrEmpty(catName))
                            kategoriHarcamalar[catName] = kategoriHarcamalar.GetValueOrDefault(catName) + amount;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Aylik Ozet - {DateTime.Now:MMMM yyyy}\n");
                sb.AppendLine($"Gelir: +{aylikGelir:N2} TL");
                sb.AppendLine($"Gider: -{aylikGider:N2} TL");
                sb.AppendLine($"Net: {(aylikGelir - aylikGider):N2} TL\n");

                if (kategoriHarcamalar.Any())
                {
                    sb.AppendLine("En Yuksek Harcamalar");
                    foreach (var kv in kategoriHarcamalar.OrderByDescending(x => x.Value).Take(5))
                    {
                        var pct = aylikGider > 0 ? (kv.Value / aylikGider) * 100 : 0;
                        sb.AppendLine($"  {kv.Key}: {kv.Value:N2} TL (%{pct:N0})");
                    }
                }

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ozet error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /kategoriler
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
                    await botClient.SendMessage(chatId: chatId, text: "Kategoriler alinamadi.", cancellationToken: ct);
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
                sb.AppendLine("Kategorilerin\n");
                sb.AppendLine("Gelir Kategorileri");
                foreach (var g in gelirler) sb.AppendLine($"  {g}");
                sb.AppendLine();
                sb.AppendLine("Gider Kategorileri");
                foreach (var g in giderler) sb.AppendLine($"  {g}");

                await botClient.SendMessage(chatId: chatId, text: sb.ToString(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kategoriler error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /gelir <miktar> <kategori> [açıklama]
        private async Task HandleGelirCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanim: /gelir <miktar> <kategori> [aciklama]\n\nOrnek:\n/gelir 25000 Maas Haziran maasi",
                    cancellationToken: ct);
                return;
            }

            if (!decimal.TryParse(parts[1].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Gecersiz miktar. Ornek: /gelir 25000 Maas", cancellationToken: ct);
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
                    text: "Kullanim: /gider <miktar> <kategori> [aciklama]\n\nOrnek:\n/gider 150 Market Haftalik alisveris",
                    cancellationToken: ct);
                return;
            }

            if (!decimal.TryParse(parts[1].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Gecersiz miktar. Ornek: /gider 150 Market", cancellationToken: ct);
                return;
            }

            var kategoriAdi = parts[2];
            var aciklama = parts.Length > 3 ? string.Join(" ", parts.Skip(3)) : "";

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            await EkleIslem(botClient, chatId, link, amount, "Expense", kategoriAdi, aciklama, ct);
        }

        // Islem ekleme ortak fonksiyonu (hem gelir hem gider için)
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
                    var tip = type == "Income" ? "Gelir" : "Gider";
                    var sign = type == "Income" ? "+" : "-";
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"{tip} eklendi!\n\n" +
                              $"Miktar: {sign}{amount:N2} TL\n" +
                              $"Kategori: {kategoriAdi}\n" +
                              (string.IsNullOrEmpty(aciklama) ? "" : $"Aciklama: {aciklama}\n") +
                              $"Tarih: {DateTime.Now:dd.MM.yyyy}",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Islem eklenemedi. Kategori adini kontrol et.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Islem ekle error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /butçelimit <kategori> <limit>
        private async Task HandleButceLimitCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanim: /butcelimit <kategori> <limit>\n\nOrnek:\n/butcelimit Market 2000",
                    cancellationToken: ct);
                return;
            }

            var kategoriAdi = parts[1];
            if (!decimal.TryParse(parts[2].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal limit) || limit <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Gecersiz limit. Ornek: /butcelimit Market 2000", cancellationToken: ct);
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

                var payload = new { limit, month = DateTime.Now.Month, year = DateTime.Now.Year, categoryId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/api/budget", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"Butce limiti olusturuldu!\n\n" +
                              $"Kategori: {kategoriAdi}\n" +
                              $"Limit: {limit:N2} TL\n" +
                              $"Donem: {DateTime.Now:MMMM yyyy}",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Butce limiti olusturulamadi.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Butce limit error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /birikimyeni <hedef_adı> <miktar> [tarih]
        private async Task HandleBirikimYeniCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanim: /birikimyeni <hedef_adi> <hedef_miktar> [bitis_tarihi]\n\nOrnek:\n/birikimyeni Tatil 15000 2026-12-31",
                    cancellationToken: ct);
                return;
            }

            var goalName = parts[1];
            if (!decimal.TryParse(parts[2].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal target) || target <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Gecersiz miktar.", cancellationToken: ct);
                return;
            }

            DateTime? targetDate = null;
            if (parts.Length > 3 && DateTime.TryParse(parts[3], out var td))
                targetDate = td;

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var payload = new { goalName, targetAmount = target, initialAmount = 0, targetDate, icon = "🎯", color = "#1AAFA3" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/api/saving", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"Birikim hedefi olusturuldu!\n\n" +
                              $"Hedef: {goalName}\n" +
                              $"Tutar: {target:N2} TL\n" +
                              (targetDate.HasValue ? $"Bitis: {targetDate.Value:dd.MM.yyyy}\n" : "") +
                              $"\nBirikime para eklemek icin:\n/birikimyatir {goalName} <miktar>",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Birikim hedefi olusturulamadi.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Birikim yeni error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /birikimyatir <hedef_adı> <miktar>
        private async Task HandleBirikimYatirCommand(
            ITelegramBotClient botClient, long chatId,
            string[] parts, BotUserLink link, CancellationToken ct)
        {
            if (parts.Length < 3)
            {
                await botClient.SendMessage(chatId: chatId,
                    text: "Kullanim: /birikimyatir <hedef_adi> <miktar>\n\nOrnek:\n/birikimyatir Tatil 500",
                    cancellationToken: ct);
                return;
            }

            var goalName = parts[1];
            if (!decimal.TryParse(parts[2].Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                await botClient.SendMessage(chatId: chatId, text: "Gecersiz miktar.", cancellationToken: ct);
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
                string foundGoal = "";

                foreach (var s in savings.EnumerateArray())
                {
                    var name = s.GetProperty("goalName").GetString() ?? "";
                    if (name.ToLower().Contains(goalName.ToLower()))
                    {
                        savingId = s.GetProperty("savingId").GetInt32();
                        foundGoal = name;
                        break;
                    }
                }

                if (savingId == 0)
                {
                    await botClient.SendMessage(chatId: chatId,
                        text: $"'{goalName}' adinda birikim hedefi bulunamadi.\n/birikim ile listeni gorebilirsin.",
                        cancellationToken: ct);
                    return;
                }

                var payload = new { amount };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync($"/api/saving/{savingId}/deposit", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"Birikime para eklendi!\n\n" +
                              $"Hedef: {foundGoal}\n" +
                              $"Eklenen: +{amount:N2} TL",
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId: chatId, text: "Birikim guncellenemedi.", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Birikim yatir error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /analiz  GPT kapsamlı rapor
        private async Task HandleAnalizCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, IServiceScope scope, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            await botClient.SendMessage(chatId, "Finansal analiz hazirlaniyor... Lutfen bekle.", cancellationToken: ct);
            try
            {
                var ai = scope.ServiceProvider.GetRequiredService<OpenAiService>();
                var result = await ai.GenerateAnalysisAsync(link, ct);
                await botClient.SendMessage(chatId: chatId, text: result, parseMode: ParseMode.Markdown, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Analiz error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Analiz olusturulamadi.", cancellationToken: ct);
            }
        }

        // /profil
        private async Task HandleProfilCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
            try
            {
                var client = CreateApiClient(link);
                var res = await client.GetAsync("/api/subscription/my-plan", ct);

                if (!res.IsSuccessStatusCode)
                {
                    await botClient.SendMessage(chatId: chatId,
                        text: $"Kullanici: {link.FinAwareUsername}\nPlan bilgisi alinamadi.",
                        cancellationToken: ct);
                    return;
                }

                var data = JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync(ct));
                var plan   = data.TryGetProperty("plan",      out var p)  ? p.GetString()  ?? "Free" : "Free";
                var expiry = data.TryGetProperty("expiry",    out var e)  ? e.GetString()  ?? ""     : "";
                var ocrU   = data.TryGetProperty("ocrUsage",  out var ou) ? ou.GetInt32()            : 0;
                var ocrL   = data.TryGetProperty("ocrLimit",  out var ol) ? ol.GetInt32()            : 0;
                var arisU  = data.TryGetProperty("arisUsage", out var au) ? au.GetInt32()            : 0;
                var arisL  = data.TryGetProperty("arisLimit", out var al) ? al.GetInt32()            : 0;

                var ocrStr  = ocrL  == -1 ? $"{ocrU}/Sinirsiz"  : $"{ocrU}/{ocrL}";
                var arisStr = arisL == -1 ? $"{arisU}/Sinirsiz" : $"{arisU}/{arisL}";

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Profil\n\n" +
                          $"Kullanici: {link.FinAwareUsername}\n" +
                          $"Plan: {plan}\n" +
                          (string.IsNullOrEmpty(expiry) ? "" : $"Bitis: {expiry}\n") +
                          $"\nBu Ayki Kullanim:\n" +
                          $"OCR: {ocrStr}\n" +
                          $"ARIS: {arisStr}",
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profil error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // /yeniüye
        private async Task HandleYeniUyeCommand(
            ITelegramBotClient botClient, long chatId,
            BotUserLink link, CancellationToken ct)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Merhaba {link.FinAwareUsername}! FinAware Asistan'a hos geldin!\n\n" +
                      "Hizlica baslamak icin:\n\n" +
                      "1 - Bakiyeni gor\n" +
                      "/bakiye\n\n" +
                      "2 - Ilk gelirini ekle\n" +
                      "/gelir 25000 Maas Haziran maasi\n\n" +
                      "3 - Harcama ekle\n" +
                      "/gider 150 Market Haftalik alisveris\n\n" +
                      "4 - Butce limiti belirle\n" +
                      "/butcelimit Market 2000\n\n" +
                      "5 - Birikim hedefi olustur\n" +
                      "/birikimyeni Tatil 15000 2026-12-31\n\n" +
                      "6 - Birikime para yatir\n" +
                      "/birikimyatir Tatil 500\n\n" +
                      "7 - Kapsamli analiz al\n" +
                      "/analiz\n\n" +
                      "Tum komutlar icin /yardim yaz!\n\n" +
                      "Fatura fotografı da gonderebilirsin, otomatik analiz ederim.",
                cancellationToken: ct
            );
        }

        // /yardım
        private async Task HandleYardimCommand(
            ITelegramBotClient botClient, long chatId, CancellationToken ct)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "FinAware Bot Komutlari\n\n" +
                      "Goruntuleme\n" +
                      "/bakiye - Anlik bakiye ve ozet\n" +
                      "/islemler - Son 10 islem\n" +
                      "/butce - Bu ayki butce durumu\n" +
                      "/birikim - Birikim hedefleri\n" +
                      "/ozet - Aylik gelir/gider ozeti\n" +
                      "/kategoriler - Kategori listesi\n" +
                      "/profil - Plan ve kullanim bilgisi\n\n" +
                      "Islem Ekleme\n" +
                      "/gelir <miktar> <kategori> [aciklama]\n" +
                      "/gider <miktar> <kategori> [aciklama]\n\n" +
                      "Butce\n" +
                      "/butcelimit <kategori> <limit>\n\n" +
                      "Birikim\n" +
                      "/birikimyeni <isim> <hedef> [tarih]\n" +
                      "/birikimyatir <isim> <miktar>\n\n" +
                      "Analiz\n" +
                      "/analiz - Kapsamli GPT finansal rapor\n\n" +
                      "Diger\n" +
                      "/yeniuye - Baslangic rehberi\n" +
                      "/yardim - Bu menu\n\n" +
                      "Dogal Dil\n" +
                      "Markete 150 lira harcadim\n" +
                      "Maasim 25000 TL yatti\n" +
                      "Bu ay ne kadar harcadim?\n\n" +
                      "Fatura / Fis\n" +
                      "Fatura fotografı gonder, otomatik analiz ederim.",
                cancellationToken: ct
            );
        }

        // Fatura fotoğrafı gonderildiğinde analiz etme
        private async Task HandlePhotoMessage(
            ITelegramBotClient botClient,
            long chatId,
            Message message,
            BotUserLink link,
            CancellationToken ct)
        {
            await botClient.SendMessage(chatId: chatId, text: "Fatura analiz ediliyor...", cancellationToken: ct);
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
                    var errCode = (int)response.StatusCode;
                    if (errCode == 403)
                        await botClient.SendMessage(chatId: chatId,
                            text: "Fatura OCR Gold/Platinum plana ozel.\nFinAware'den planini yukselt.",
                            cancellationToken: ct);
                    else
                        await botClient.SendMessage(chatId: chatId,
                            text: "Fatura okunamadi. Manuel yazabilirsin.\nOrnek: Markete 150 lira harcadim",
                            cancellationToken: ct);
                    return;
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseText);
                bool success = result.TryGetProperty("success", out var successEl) && successEl.GetBoolean();

                if (!success)
                {
                    await botClient.SendMessage(chatId: chatId,
                        text: "Faturanin metni cikarilamadi. Daha net bir fotograf dene.",
                        cancellationToken: ct);
                    return;
                }

                decimal amount = result.TryGetProperty("amount", out var amountEl) ? amountEl.GetDecimal() : 0;
                string category = result.TryGetProperty("category", out var catEl) ? catEl.GetString() ?? "Diger" : "Diger";
                string description = result.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
                string date = result.TryGetProperty("date", out var dateEl)
                    ? dateEl.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd")
                    : DateTime.Now.ToString("yyyy-MM-dd");

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Fatura Okundu\n\n" +
                          $"Tutar: {amount:N2} TL\n" +
                          $"Kategori: {category}\n" +
                          $"Aciklama: {(string.IsNullOrEmpty(description) ? "Belirtilmedi" : description)}\n" +
                          $"Tarih: {date}\n\n" +
                          $"Ekleyeyim mi? (evet / hayir)",
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
                Console.WriteLine($"Photo error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // Metin mesajı doğal dil veya fatura onay
        private async Task HandleTextMessage(
            ITelegramBotClient botClient,
            long chatId,
            string text,
            BotUserLink link,
            IServiceScope scope,
            CancellationToken ct)
        {
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
                Console.WriteLine($"AI error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // Fatura onay (evet / hayır)
        private async Task HandleInvoiceConfirmation(
            ITelegramBotClient botClient,
            long chatId,
            string text,
            BotUserLink userLink,
            BotDbContext db,
            CancellationToken ct)
        {
            var lower = text.Trim().ToLower();

            if (lower is "evet" or "e" or "yes" or "tamam" or "ok")
            {
                try
                {
                    var invoiceData = JsonSerializer.Deserialize<JsonElement>(userLink.PendingInvoiceData!);
                    decimal amount = invoiceData.GetProperty("amount").GetDecimal();
                    string category = invoiceData.GetProperty("category").GetString() ?? "Diger";
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
                        if (catName.ToLower().Contains(category.ToLower()) || category.ToLower().Contains(catName.ToLower()))
                        {
                            categoryId = cat.GetProperty("categoryId").GetInt32();
                            break;
                        }
                    }

                    if (categoryId == 0)
                    {
                        foreach (var cat in categories.EnumerateArray())
                        {
                            if ((cat.TryGetProperty("type", out var t) ? t.GetString() : "") == "Expense")
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
                            text: $"Islem eklendi!\n\n-{amount:N2} TL - {category}\n{(string.IsNullOrEmpty(desc) ? "" : desc)}",
                            cancellationToken: ct);
                    }
                    else
                    {
                        await botClient.SendMessage(chatId: chatId, text: "Islem eklenemedi.", cancellationToken: ct);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Invoice confirm error: {ex.Message}");
                    userLink.PendingInvoiceData = null;
                    await db.SaveChangesAsync(ct);
                    await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
                }
            }
            else if (lower is "hayir" or "h" or "no" or "iptal")
            {
                userLink.PendingInvoiceData = null;
                await db.SaveChangesAsync(ct);
                await botClient.SendMessage(chatId: chatId, text: "Iptal edildi.", cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(chatId: chatId, text: "Lutfen evet veya hayir yaz.", cancellationToken: ct);
            }
        }

        // Hesap bağlama (/start TOKEN)
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
                          "Hesabini baglamak icin FinAware profilinden Telegram'a Baglan butonuna tikla.",
                    cancellationToken: ct
                );
                return;
            }

            var linkToken = parts[1].Trim();
            Console.WriteLine($"Link token: {linkToken} | ChatId: {chatId}");

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

                    var finAwareUsername = result.GetProperty("username").GetString() ?? "Kullanici";
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
                    Console.WriteLine($"Baglanti: @{username} - {finAwareUsername}");

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"Hesabin baglandi!\n\n" +
                              $"FinAware Kullanicisi: {finAwareUsername}\n\n" +
                              $"Baslamak icin /yeniuye yazabilirsin.\n" +
                              $"Tum komutlar icin /yardim yaz.",
                        cancellationToken: ct
                    );
                }
                else
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Baglanti kurulamadi. Token gecersiz olabilir. Yeni link olusturmayı dene.",
                        cancellationToken: ct
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bot link error: {ex.Message}");
                await botClient.SendMessage(chatId: chatId, text: "Bir hata olustu.", cancellationToken: ct);
            }
        }

        // Token yenile
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

                Console.WriteLine($"Token yenilendi: {link.FinAwareUsername}");
                return link;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token yenileme hatasi: {ex.Message}");
                return null;
            }
        }

        // Yardımcılar
        private HttpClient CreateApiClient(BotUserLink link)
        {
            var client = _httpClientFactory.CreateClient("FinAwareApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", link.JwtToken);
            return client;
        }

        private static string BuildBar(int pct)
        {
            pct = Math.Clamp(pct, 0, 100);
            int filled = pct / 10;
            return new string('|', filled) + new string('.', 10 - filled);
        }

        private Task HandleErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken ct)
        {
            Console.WriteLine($"Telegram hata: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}