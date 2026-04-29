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
                                amount = new { type = "number", description = "Tutar (sadece sayı)" },
                                type = new { type = "string", @enum = new[] { "Income", "Expense" }, description = "Gelir mi gider mi" },
                                category = new { type = "string", description = $"Kategori. Şunlardan biri: {categoryList}" },
                                description = new { type = "string", description = "Kısa açıklama" },
                                date = new { type = "string", description = $"Tarih YYYY-MM-DD formatında. Bugün: {today}" }
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
                                year = new { type = "integer", description = "Yıl" }
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
                        name = "get_budget_status",
                        description = "Bütçe durumunu göster. Örnek: 'bütçem nasıl', 'ne kadar bütçem kaldı'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                month = new { type = "integer", description = "Ay (1-12)" },
                                year = new { type = "integer", description = "Yıl" }
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
              $"Kullanıcı seninle muhabbet ederse (hava nasıl, nasılsın gibi) kısa sohbet et ama konuyu finansa çek. " +
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
                string description = args.TryGetProperty("description", out var descEl)
                    ? descEl.GetString() ?? "" : "";
                string date = args.TryGetProperty("date", out var dateEl)
                    ? dateEl.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd")
                    : DateTime.Now.ToString("yyyy-MM-dd");

                var catResponse = await apiClient.GetAsync("/api/category", ct);
                var catJson = await catResponse.Content.ReadAsStringAsync(ct);
                var categories = JsonSerializer.Deserialize<JsonElement>(catJson);

                int categoryId = 0;
                foreach (var cat in categories.EnumerateArray())
                {
                    var catName = cat.GetProperty("name").GetString() ?? "";
                    if (string.Equals(catName, category, StringComparison.OrdinalIgnoreCase))
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
                    type,
                    date = DateTime.Parse(date),
                    description,
                    categoryId,
                    currency = "TRY"
                };

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

                var response = await apiClient.GetAsync($"/api/transaction", ct);
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
                    string type = t.GetProperty("type").GetString() ?? "";
                    string catName = "";

                    if (t.TryGetProperty("category", out var catEl) &&
                        catEl.TryGetProperty("name", out var catNameEl))
                        catName = catNameEl.GetString() ?? "Diğer";

                    if (type == "Income") totalIncome += amount;
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
                var totalExpense = data.GetProperty("totalExpense").GetDecimal();

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
                    string type = t.GetProperty("type").GetString() ?? "";
                    decimal amount = t.GetProperty("amount").GetDecimal();
                    string dateStr = t.GetProperty("date").GetString() ?? "";
                    DateTime.TryParse(dateStr, out var date);
                    string catName = "";
                    string catIcon = "";

                    if (t.TryGetProperty("category", out var catEl))
                    {
                        catName = catEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        catIcon = catEl.TryGetProperty("icon", out var i) ? i.GetString() ?? "" : "";
                    }

                    string emoji = type == "Income" ? "💰" : "💸";
                    string sign = type == "Income" ? "+" : "-";
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