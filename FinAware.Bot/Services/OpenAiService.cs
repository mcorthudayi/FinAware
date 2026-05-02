using FinAware.Bot.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAware.Bot.Services
{
    public class OpenAiService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public OpenAiService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> ProcessMessageAsync(string userMessage, BotUserLink link, CancellationToken ct)
        {
            var openAiKey = _configuration["OpenAI:ApiKey"];
            var apiClient = _httpClientFactory.CreateClient("FinAwareApi");
            apiClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", link.JwtToken);

            var categories = new List<string> { "Market", "Yemek", "Ulaşım", "Faturalar", "Sağlık", "Eğlence", "Giyim", "Teknoloji", "Eğitim", "Spor", "Maaş", "Ek Gelir", "Diğer" };

            try
            {
                var catResponse = await apiClient.GetAsync("/api/category", ct);
                if (catResponse.IsSuccessStatusCode)
                {
                    var catJson = await catResponse.Content.ReadAsStringAsync(ct);
                    var catData = JsonSerializer.Deserialize<JsonElement>(catJson);
                    categories = catData.EnumerateArray()
                        .Select(c => c.GetProperty("name").GetString() ?? "")
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();
                }
            }
            catch { }

            var categoryList = string.Join(", ", categories);
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            var tools = new object[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "add_transaction",
                        description = "Kullanıcı yeni bir gelir veya gider eklediğinde çağır. " +
                                      "Örnek: 'markete 250 lira harcadım', 'maaşım 15000 geldi'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                amount      = new { type = "number", description = "Tutar (sadece sayı)" },
                                type        = new { type = "string", @enum = new[] { "Income", "Expense" }, description = "Gelir mi gider mi" },
                                category    = new { type = "string", description = $"Kategori. Şunlardan biri: {categoryList}" },
                                description = new { type = "string", description = "Kısa açıklama" },
                                date        = new { type = "string", description = $"Tarih YYYY-MM-DD formatında. Bugün: {today}" }
                            },
                            required = new[] { "amount", "type", "category" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_monthly_summary",
                        description = "Bu ay veya belirli bir aydaki gelir/gider özetini göster. " +
                                      "Örnek: 'bu ay ne harcadım', 'mart ayı özeti'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                month = new { type = "integer", description = "Ay (1-12)" },
                                year  = new { type = "integer", description = "Yıl" }
                            },
                            required = new[] { "month", "year" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_detailed_monthly_report",
                        description = "Kullanıcının belirttiği aya ait detaylı finansal raporu getirir. " +
                                      "Kategori bazlı harcamalar, gelir/gider özeti ve geçen ay karşılaştırması içerir. " +
                                      "'bu ay ne haldeyim', 'aylık bilançom', 'bu ay nasıl gidiyorum', " +
                                      "'harcamalarım nasıl', 'detaylı rapor' gibi sorularda kullan.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                year  = new { type = "integer", description = "Rapor yılı (örn: 2026)" },
                                month = new { type = "integer", description = "Rapor ayı 1-12 arası (örn: 5 = Mayıs)" }
                            },
                            required = new[] { "year", "month" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_firm_expenses",
                        description = "Belirli bir firma, mağaza veya açıklamaya göre ne kadar harcandığını gösterir. " +
                                      "'Migros'a ne kadar harcadım', 'Starbucks harcamam ne kadar', " +
                                      "'A101 harcamalarım', 'market alışverişlerimin toplamı' gibi sorularda kullan.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                firm_name = new { type = "string", description = "Aranacak firma veya açıklama adı" },
                                month     = new { type = "integer", description = "Ay (1-12), belirtilmezse tüm zamanlar" },
                                year      = new { type = "integer", description = "Yıl, belirtilmezse tüm zamanlar" }
                            },
                            required = new[] { "firm_name" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_budget_status",
                        description = "Bütçe durumunu göster. Örnek: 'bütçem nasıl', 'ne kadar bütçem kaldı'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                month = new { type = "integer", description = "Ay (1-12)" },
                                year  = new { type = "integer", description = "Yıl" }
                            },
                            required = new[] { "month", "year" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_recent_transactions",
                        description = "Son işlemleri listele. Örnek: 'son harcamalarım', 'son 5 işlem'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                count = new { type = "integer", description = "Kaç işlem gösterilsin (max 10)" }
                            },
                            required = new[] { "count" }
                        }
                    }
                }
            };

            var messages = new[]
            {
                new
                {
                    role = "system",
                    content = $"Sen FinAware'in yapay zeka destekli finans asistanısın. " +
                              $"Kullanıcının adı {link.FinAwareUsername}, ona ismiyle hitap et. " +
                              $"Bugünün tarihi: {today}. " +
                              $"Konuşma tarzın: samimi, arkadaşça, bazen espirili ama her zaman yardımsever. " +
                              $"Kısa ve öz cevap ver, gereksiz uzatma. " +
                              $"Emoji kullan ama abartma. " +
                              $"Kullanıcı para harcadığında bazen 'Hay aksi! 😄', 'Cüzdan yine inledi 😅' gibi yorumlar yapabilirsin. " +
                              $"Gelir eklendiğinde 'Afiyet olsun! 💰', 'Güzel, hesap şişiyor 📈' gibi tepkiler ver. " +
                              $"Bütçe aşıldığında ciddi ama nazikçe uyar. " +
                              $"Kullanıcı seninle muhabbet ederse kısa sohbet et ama konuyu finansa çek. " +
                              $"Türkçe yaz, asla İngilizce kullanma. " +
                              $"Mevcut kategoriler: {categoryList}. " +
                              $"İşlem eklerken mutlaka listeden doğru kategoriyi seç. " +
                              $"Tutar olarak sadece sayı kullan, TL sembolü koyma. " +
                              $"Cevapların markdown formatında olsun (bold için *, italic için _)."
                },
                new { role = "user", content = userMessage }
            };

            var requestBody = new
            {
                model = "gpt-4o",
                messages,
                tools,
                tool_choice = "auto",
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(requestBody);
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", openAiKey);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ OpenAI error: {responseText}");
                return "⚠️ AI servisi şu an yanıt vermiyor. Lütfen tekrar dene.";
            }

            var gptResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
            var choice = gptResponse.GetProperty("choices")[0];
            var gptMessage = choice.GetProperty("message");
            var finishReason = choice.GetProperty("finish_reason").GetString();

            if (finishReason == "tool_calls" &&
                gptMessage.TryGetProperty("tool_calls", out var toolCalls))
            {
                var toolCall = toolCalls[0];
                var functionName = toolCall.GetProperty("function").GetProperty("name").GetString();
                var argsJson = toolCall.GetProperty("function").GetProperty("arguments").GetString();
                var args = JsonSerializer.Deserialize<JsonElement>(argsJson!);

                Console.WriteLine($"🔧 Tool call: {functionName}");

                return functionName switch
                {
                    "add_transaction" => await ExecuteAddTransaction(args, apiClient, ct),
                    "get_monthly_summary" => await ExecuteGetMonthlySummary(args, apiClient, ct),
                    "get_detailed_monthly_report" => await ExecuteGetDetailedMonthlyReport(args, apiClient, ct),
                    "get_firm_expenses" => await ExecuteGetFirmExpenses(args, apiClient, ct),
                    "get_budget_status" => await ExecuteGetBudgetStatus(args, apiClient, ct),
                    "get_recent_transactions" => await ExecuteGetRecentTransactions(args, apiClient, ct),
                    _ => "⚠️ Bilinmeyen komut."
                };
            }

            return gptMessage.GetProperty("content").GetString() ??
                   "Üzgünüm, anlayamadım. Tekrar dener misin? 😊";
        }

        private async Task<string> ExecuteAddTransaction(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                decimal amount = args.GetProperty("amount").GetDecimal();
                string type = args.GetProperty("type").GetString() ?? "Expense";
                string category = args.GetProperty("category").GetString() ?? "Diğer";
                string description = args.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
                string date = args.TryGetProperty("date", out var dateEl)
                    ? dateEl.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd")
                    : DateTime.Now.ToString("yyyy-MM-dd");

                var catResponse = await apiClient.GetAsync("/api/category", ct);
                var catJson = await catResponse.Content.ReadAsStringAsync(ct);
                var categories = JsonSerializer.Deserialize<JsonElement>(catJson);

                int categoryId = 0;
                foreach (var cat in categories.EnumerateArray())
                {
                    if (string.Equals(cat.GetProperty("name").GetString(), category, StringComparison.OrdinalIgnoreCase))
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

                var payload = new { amount, type, date = DateTime.Parse(date), description, categoryId, currency = "TRY" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await apiClient.PostAsync("/api/transaction", content, ct);

                if (response.IsSuccessStatusCode)
                {
                    string emoji = type == "Income" ? "💰" : "💸";
                    string typeText = type == "Income" ? "Gelir" : "Gider";
                    return $"{emoji} *{typeText} eklendi!*\n\n" +
                           $"💵 Tutar: *₺{amount:N2}*\n" +
                           $"🏷️ Kategori: *{category}*\n" +
                           $"📝 Açıklama: *{(string.IsNullOrEmpty(description) ? "Belirtilmedi" : description)}*\n" +
                           $"📅 Tarih: *{date}*\n\n" +
                           $"Başka bir şey eklemek ister misin? 😊";
                }

                return "❌ İşlem eklenemedi. Lütfen tekrar dene.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Add transaction error: {ex.Message}");
                return "⚠️ İşlem eklenirken hata oluştu.";
            }
        }

        private async Task<string> ExecuteGetMonthlySummary(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int month = args.GetProperty("month").GetInt32();
                int year = args.GetProperty("year").GetInt32();

                var response = await apiClient.GetAsync("/api/transaction", ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                decimal totalIncome = 0, totalExpense = 0;
                var categoryTotals = new Dictionary<string, decimal>();

                foreach (var t in transactions.EnumerateArray())
                {
                    var dateStr = t.GetProperty("date").GetString() ?? "";
                    if (!DateTime.TryParse(dateStr, out var date)) continue;
                    if (date.Month != month || date.Year != year) continue;

                    decimal amount = t.GetProperty("amount").GetDecimal();
                    string ttype = t.GetProperty("type").GetString() ?? "";
                    string catName = "";

                    if (t.TryGetProperty("category", out var catEl) &&
                        catEl.TryGetProperty("name", out var catNameEl))
                        catName = catNameEl.GetString() ?? "Diğer";

                    if (ttype == "Income") totalIncome += amount;
                    else
                    {
                        totalExpense += amount;
                        if (!string.IsNullOrEmpty(catName))
                        {
                            categoryTotals.TryGetValue(catName, out var existing);
                            categoryTotals[catName] = existing + amount;
                        }
                    }
                }

                decimal balance = totalIncome - totalExpense;
                string balanceEmoji = balance >= 0 ? "📈" : "📉";
                string balanceSign = balance >= 0 ? "+" : "";
                var monthNames = new[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                                               "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string monthName = month <= 12 ? monthNames[month] : month.ToString();

                var sb = new StringBuilder();
                sb.AppendLine($"📊 *{monthName} {year} Özeti*\n");
                sb.AppendLine($"💰 Gelir: *₺{totalIncome:N2}*");
                sb.AppendLine($"💸 Gider: *₺{totalExpense:N2}*");
                sb.AppendLine($"{balanceEmoji} Bakiye: *{balanceSign}₺{balance:N2}*");

                if (categoryTotals.Any())
                {
                    sb.AppendLine("\n📋 *Kategori Dağılımı:*");
                    foreach (var cat in categoryTotals.OrderByDescending(c => c.Value).Take(5))
                        sb.AppendLine($"  • {cat.Key}: ₺{cat.Value:N2}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Monthly summary error: {ex.Message}");
                return "⚠️ Özet getirilirken hata oluştu.";
            }
        }

        private async Task<string> ExecuteGetDetailedMonthlyReport(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int year = args.TryGetProperty("year", out var y) ? y.GetInt32() : DateTime.Now.Year;
                int month = args.TryGetProperty("month", out var m) ? m.GetInt32() : DateTime.Now.Month;

                var response = await apiClient.GetAsync(
                    $"/api/transaction/monthly-report?year={year}&month={month}", ct);

                if (!response.IsSuccessStatusCode)
                    return "⚠️ Rapor bilgisi alınamadı.";

                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var monthNames = new[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                                         "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string monthName = month <= 12 ? monthNames[month] : month.ToString();

                decimal income = data.GetProperty("income").GetDecimal();
                decimal expense = data.GetProperty("expense").GetDecimal();
                decimal balance = data.GetProperty("balance").GetDecimal();
                decimal prevExpense = data.GetProperty("prevExpense").GetDecimal();
                double expChange = data.GetProperty("expenseChange").GetDouble();
                int txCount = data.GetProperty("transactionCount").GetInt32();

                string balanceEmoji = balance >= 0 ? "📈" : "📉";
                string balanceSign = balance >= 0 ? "+" : "";
                string changeEmoji = expChange <= 0 ? "✅" : "⚠️";
                string changeSign = expChange >= 0 ? "+" : "";

                var sb = new StringBuilder();
                sb.AppendLine($"📊 *{monthName} {year} Detaylı Rapor*\n");
                sb.AppendLine($"💰 Gelir: *₺{income:N2}*");
                sb.AppendLine($"💸 Gider: *₺{expense:N2}*");
                sb.AppendLine($"{balanceEmoji} Net Bakiye: *{balanceSign}₺{balance:N2}*");
                sb.AppendLine($"📝 Toplam İşlem: *{txCount}*\n");

                if (prevExpense > 0)
                {
                    sb.AppendLine($"📅 *Geçen Ay Karşılaştırması*");
                    sb.AppendLine($"{changeEmoji} Gider değişimi: *{changeSign}%{expChange:N1}*");
                    sb.AppendLine($"  Geçen ay: ₺{prevExpense:N2} → Bu ay: ₺{expense:N2}\n");
                }

                var cats = data.GetProperty("categoryBreakdown");
                if (cats.EnumerateArray().Any())
                {
                    sb.AppendLine("🏷️ *Kategori Bazlı Harcamalar:*");
                    foreach (var cat in cats.EnumerateArray().Take(6))
                    {
                        string icon = cat.GetProperty("icon").GetString() ?? "💳";
                        string name = cat.GetProperty("categoryName").GetString() ?? "";
                        decimal amount = cat.GetProperty("amount").GetDecimal();
                        int count = cat.GetProperty("count").GetInt32();
                        double pct = expense > 0 ? (double)(amount / expense * 100) : 0;
                        sb.AppendLine($"  {icon} *{name}*: ₺{amount:N2} ({count} işlem, %{pct:N0})");
                    }
                }

                if (data.TryGetProperty("firmBreakdown", out var firms) && firms.EnumerateArray().Any())
                {
                    sb.AppendLine("\n🏢 *Firma Bazlı Harcamalar:*");
                    foreach (var f in firms.EnumerateArray().Take(5))
                    {
                        string fname = f.GetProperty("name").GetString() ?? "";
                        decimal ftotal = f.GetProperty("total").GetDecimal();
                        int fcount = f.GetProperty("count").GetInt32();
                        sb.AppendLine($"  • *{fname}*: ₺{ftotal:N2} ({fcount} işlem)");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Detailed report error: {ex.Message}");
                return "⚠️ Detaylı rapor getirilirken hata oluştu.";
            }
        }

        private async Task<string> ExecuteGetFirmExpenses(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                string firmName = args.GetProperty("firm_name").GetString() ?? "";
                int? month = args.TryGetProperty("month", out var m) ? m.GetInt32() : null;
                int? year = args.TryGetProperty("year", out var y) ? y.GetInt32() : null;

                var response = await apiClient.GetAsync("/api/transaction", ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                var matched = new List<(decimal amount, string date, string category)>();

                foreach (var t in transactions.EnumerateArray())
                {
                    string desc = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    string ttype = t.GetProperty("type").GetString() ?? "";

                    if (ttype != "Expense") continue;
                    if (!desc.Contains(firmName, StringComparison.OrdinalIgnoreCase)) continue;

                    string dateStr = t.GetProperty("date").GetString() ?? "";
                    if (!DateTime.TryParse(dateStr, out var date)) continue;
                    if (month.HasValue && date.Month != month.Value) continue;
                    if (year.HasValue && date.Year != year.Value) continue;

                    decimal amount = t.GetProperty("amount").GetDecimal();
                    string catName = "";
                    if (t.TryGetProperty("category", out var cat))
                        catName = cat.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

                    matched.Add((amount, date.ToString("dd.MM.yyyy"), catName));
                }

                if (!matched.Any())
                    return $"🔍 *\"{firmName}\"* ile eşleşen gider bulunamadı.";

                decimal total = matched.Sum(m => m.amount);
                var monthNames = new[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                                           "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string period = month.HasValue
                    ? $"{monthNames[month.Value]} {year ?? DateTime.Now.Year}"
                    : "Tüm Zamanlar";

                var sb = new StringBuilder();
                sb.AppendLine($"🏢 *\"{firmName}\"* Harcamaları ({period})\n");
                sb.AppendLine($"💸 Toplam: *₺{total:N2}* ({matched.Count} işlem)\n");

                if (matched.Count <= 8)
                {
                    sb.AppendLine("📋 *Detay:*");
                    foreach (var (amount, date, cat) in matched.OrderByDescending(m => m.date))
                        sb.AppendLine($"  • {date}: ₺{amount:N2} ({cat})");
                }
                else
                {
                    sb.AppendLine($"📊 Ortalama: *₺{(total / matched.Count):N2}* / işlem");
                    sb.AppendLine($"💰 En yüksek: *₺{matched.Max(m => m.amount):N2}*");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Firm expenses error: {ex.Message}");
                return "⚠️ Firma harcaması sorgulanırken hata oluştu.";
            }
        }

        private async Task<string> ExecuteGetBudgetStatus(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int month = args.GetProperty("month").GetInt32();
                int year = args.GetProperty("year").GetInt32();

                var response = await apiClient.GetAsync($"/api/budget?month={month}&year={year}", ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var budgets = data.GetProperty("budgets");

                if (!budgets.EnumerateArray().Any())
                    return "📭 Bu ay için tanımlı bütçe yok.\n\nFinAware'den bütçe ekleyebilirsin!";

                var sb = new StringBuilder();
                sb.AppendLine("💼 *Bütçe Durumu*\n");

                foreach (var b in budgets.EnumerateArray())
                {
                    string catName = b.GetProperty("categoryName").GetString() ?? "Genel";
                    string catIcon = b.GetProperty("categoryIcon").GetString() ?? "💰";
                    decimal limit = b.GetProperty("limitAmount").GetDecimal();
                    decimal spent = b.GetProperty("spent").GetDecimal();
                    decimal remaining = b.GetProperty("remaining").GetDecimal();
                    double pct = b.GetProperty("percentage").GetDouble();
                    bool isOver = b.GetProperty("isOver").GetBoolean();

                    string statusEmoji = isOver ? "🚨" : pct >= 80 ? "⚠️" : pct >= 50 ? "📊" : "✅";

                    sb.AppendLine($"{statusEmoji} *{catIcon} {catName}*");
                    sb.AppendLine($"  Limit: ₺{limit:N2} | Harcanan: ₺{spent:N2}");
                    sb.AppendLine($"  Kalan: ₺{remaining:N2} (%{pct:N1})\n");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Budget status error: {ex.Message}");
                return "⚠️ Bütçe bilgisi getirilirken hata oluştu.";
            }
        }

        private async Task<string> ExecuteGetRecentTransactions(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int count = args.TryGetProperty("count", out var countEl)
                    ? Math.Min(countEl.GetInt32(), 10) : 5;

                var response = await apiClient.GetAsync("/api/transaction", ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);
                var recent = transactions.EnumerateArray().Take(count).ToList();

                if (!recent.Any())
                    return "📭 Henüz hiç işlem yok.";

                var sb = new StringBuilder();
                sb.AppendLine($"📋 *Son {count} İşlem*\n");

                foreach (var t in recent)
                {
                    string ttype = t.GetProperty("type").GetString() ?? "";
                    decimal amount = t.GetProperty("amount").GetDecimal();
                    string dateStr = t.GetProperty("date").GetString() ?? "";
                    DateTime.TryParse(dateStr, out var date);
                    string catName = "", catIcon = "";

                    if (t.TryGetProperty("category", out var catEl))
                    {
                        catName = catEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        catIcon = catEl.TryGetProperty("icon", out var i) ? i.GetString() ?? "" : "";
                    }

                    string emoji = ttype == "Income" ? "💰" : "💸";
                    string sign = ttype == "Income" ? "+" : "-";
                    sb.AppendLine($"{emoji} {sign}₺{amount:N2} | {catIcon}{catName} | {date:dd.MM}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Recent transactions error: {ex.Message}");
                return "⚠️ İşlemler getirilirken hata oluştu.";
            }
        }
    }
}