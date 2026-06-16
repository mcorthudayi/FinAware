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

        // Doğal dil işleme (function callingle)
        public async Task<string> ProcessMessageAsync(string userMessage, BotUserLink link, CancellationToken ct)
        {
            var openAiKey = _configuration["OpenAI__ApiKey"] ?? _configuration["OpenAI:ApiKey"];
            var apiClient = _httpClientFactory.CreateClient("FinAwareApi");
            apiClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", link.JwtToken);

            var categories = new List<string> { "Market", "Yemek", "Ulasim", "Faturalar", "Saglik", "Eglence", "Giyim", "Teknoloji", "Egitim", "Spor", "Maas", "Ek Gelir", "Diger" };
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
                        description = "Kullanici yeni bir gelir veya gider eklediginde cagir. " +
                                      "Ornek: 'markete 250 lira harcadim', 'maasim 15000 geldi'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                amount      = new { type = "number",  description = "Tutar (sadece sayi)" },
                                type        = new { type = "string",  @enum = new[] { "Income", "Expense" }, description = "Gelir mi gider mi" },
                                category    = new { type = "string",  description = $"Kategori. Sunlardan biri: {categoryList}" },
                                description = new { type = "string",  description = "Kisa aciklama" },
                                date        = new { type = "string",  description = $"Tarih YYYY-MM-DD formatinda. Bugun: {today}" }
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
                        description = "Bu ay veya belirli bir aydaki gelir/gider ozetini goster. " +
                                      "Ornek: 'bu ay ne harcadim', 'mart ayi ozeti'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                month = new { type = "integer", description = "Ay (1-12)" },
                                year  = new { type = "integer", description = "Yil" }
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
                        description = "Kullanicinin belirttigi aya ait detayli finansal raporu getirir. " +
                                      "Kategori bazli harcamalar, gelir/gider ozeti ve gecen ay karsilastirmasi icerir. " +
                                      "'bu ay ne haldeyim', 'aylik bilanco', 'harcamalarim nasil', 'detayli rapor' gibi sorularda kullan.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                year  = new { type = "integer", description = "Rapor yili (ornek: 2026)" },
                                month = new { type = "integer", description = "Rapor ayi 1-12 arasi (ornek: 5 = Mayis)" }
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
                        description = "Belirli bir firma, magaza veya aciklamaya gore ne kadar harcandigi. " +
                                      "'Migros'a ne kadar harcadim', 'Starbucks harcamam', 'A101 harcamalarim' gibi sorularda kullan.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                firm_name = new { type = "string",  description = "Aranacak firma veya aciklama adi" },
                                month     = new { type = "integer", description = "Ay (1-12), belirtilmezse tum zamanlar" },
                                year      = new { type = "integer", description = "Yil, belirtilmezse tum zamanlar" }
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
                        description = "Butce durumunu goster. Ornek: 'butcem nasil', 'ne kadar butcem kaldi'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                month = new { type = "integer", description = "Ay (1-12)" },
                                year  = new { type = "integer", description = "Yil" }
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
                        description = "Son islemleri listele. Ornek: 'son harcamalarim', 'son 5 islem'",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                count = new { type = "integer", description = "Kac islem gosterilsin (max 10)" }
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
                    content = $"Sen FinAware'in yapay zeka destekli finans asistanisin. " +
                              $"Kullanicinin adi {link.FinAwareUsername}, ona ismiyle hitap et. " +
                              $"Bugunun tarihi: {today}. " +
                              $"Konusma tarzi: samimi, arkadasca, bazen espirili ama her zaman yardımsever. " +
                              $"Kisa ve oz cevap ver, gereksiz uzatma. " +
                              $"Emoji kullan ama abartma. " +
                              $"Kullanici para harcadiginda bazen 'Hay aksi!', 'Cuzdan yine inledi' gibi yorumlar yapabilirsin. " +
                              $"Gelir eklendiginde 'Afiyet olsun!', 'Guzel, hesap sisiyor' gibi tepkiler ver. " +
                              $"Butce asildiginda ciddi ama nazikce uyar. " +
                              $"Turkce yaz, asla Ingilizce kullanma. " +
                              $"Mevcut kategoriler: {categoryList}. " +
                              $"Islem eklerken mutlaka listeden dogru kategoriyi sec. " +
                              $"Tutar olarak sadece sayi kullan, TL sembolu koyma. " +
                              $"Cevaplarin markdown formatinda olsun (bold icin *, italic icin _)."
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
                Console.WriteLine($"OpenAI error: {responseText}");
                return "AI servisi su an yanit vermiyor. Lutfen tekrar dene.";
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

                Console.WriteLine($"Tool call: {functionName}");

                return functionName switch
                {
                    "add_transaction"            => await ExecuteAddTransaction(args, apiClient, ct),
                    "get_monthly_summary"        => await ExecuteGetMonthlySummary(args, apiClient, ct),
                    "get_detailed_monthly_report"=> await ExecuteGetDetailedMonthlyReport(args, apiClient, ct),
                    "get_firm_expenses"          => await ExecuteGetFirmExpenses(args, apiClient, ct),
                    "get_budget_status"          => await ExecuteGetBudgetStatus(args, apiClient, ct),
                    "get_recent_transactions"    => await ExecuteGetRecentTransactions(args, apiClient, ct),
                    _                            => "Bilinmeyen komut."
                };
            }

            return gptMessage.GetProperty("content").GetString() ??
                   "Uzgunum, anlayamadim. Tekrar dener misin?";
        }

        // /analiz komutu için kapsamlı GPT raporu
        public async Task<string> GenerateAnalysisAsync(BotUserLink link, CancellationToken ct)
        {
            var openAiKey = _configuration["OpenAI__ApiKey"] ?? _configuration["OpenAI:ApiKey"];
            var apiClient = _httpClientFactory.CreateClient("FinAwareApi");
            apiClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", link.JwtToken);

            var now = DateTime.Now;
            string txJson     = "[]";
            string budgetJson = "[]";
            string savingJson = "[]";

            try
            {
                var txRes     = await apiClient.GetAsync("/api/transaction", ct);
                var budgetRes = await apiClient.GetAsync($"/api/budget?month={now.Month}&year={now.Year}", ct);
                var savingRes = await apiClient.GetAsync("/api/saving", ct);

                if (txRes.IsSuccessStatusCode)     txJson     = await txRes.Content.ReadAsStringAsync(ct);
                if (budgetRes.IsSuccessStatusCode) budgetJson = await budgetRes.Content.ReadAsStringAsync(ct);
                if (savingRes.IsSuccessStatusCode) savingJson = await savingRes.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) { Console.WriteLine($"Analiz veri hatasi: {ex.Message}"); }

            var stats = BuildDetailedStats(txJson, now);

            var systemPrompt = $@"Sen FinAware'in kisisel finans analistissin. Turkce, samimi, kisisel ve emojili bir analiz raporu hazirla.

Kullanici: {link.FinAwareUsername}
Analiz Tarihi: {now:dd MMMM yyyy}

Hesaplanan Istatistikler:
{stats}

Butce Limitleri: {budgetJson}
Birikim Hedefleri: {savingJson}

Asagidaki basliklari sirayla yaz, her biri 2-3 cumle olsun:

1. Bu Ayki Genel Durum
   (gelir/gider/net, genel degerlendirme)

2. En Yuksek Harcamalar
   (hangi kategorilerde ne kadar, normal mi degil mi)

3. Butce Performansi
   (limitler asildi mi, risk var mi)

4. Birikim Durumu
   (hedeflere ne kadar yakin, tempo yeterli mi)

5. Kisisel Tavsiyeler
   (3 somut, uygulanabilir oneri - verilere dayali)

6. Finansal Saglik Skoru
   (100 uzerinden bir skor ver ve kisa acikla)

Rakamlara dayali, kisisel, motive edici yaz. Genel kliselerden kacin. Markdown kullan.";

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", openAiKey);

                var body = new
                {
                    model = "gpt-4o",
                    max_tokens = 1000,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = "Finansal analizimi hazirla." }
                    }
                };

                var res = await httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
                    ct
                );

                if (!res.IsSuccessStatusCode)
                    return "Analiz olusturulamadi. Tekrar dene.";

                var resData = JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync(ct));
                return resData.GetProperty("choices")[0]
                              .GetProperty("message")
                              .GetProperty("content")
                              .GetString() ?? "Analiz bos dondu.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GenerateAnalysis error: {ex.Message}");
                return "Analiz sirasinda hata olustu.";
            }
        }

        // Detaylı istatistik hesapla (analiz için)
        private string BuildDetailedStats(string txJson, DateTime now)
        {
            try
            {
                var txs = JsonSerializer.Deserialize<JsonElement>(txJson);
                var sb  = new StringBuilder();

                decimal ayGelir = 0, ayGider = 0, gecenAyGelir = 0, gecenAyGider = 0;
                var katHar   = new Dictionary<string, decimal>();
                var aylikNet = new Dictionary<string, decimal>();
                int toplamTx = 0;
                var gecenAy  = now.AddMonths(-1);

                foreach (var t in txs.EnumerateArray())
                {
                    toplamTx++;
                    var amt  = t.TryGetProperty("amount",       out var a)  ? a.GetDecimal()        : 0;
                    var type = t.TryGetProperty("type",         out var tp) ? tp.GetString()         : "";
                    var cat  = t.TryGetProperty("categoryName", out var c)  ? c.GetString() ?? "Diger" : "Diger";
                    var date = t.TryGetProperty("date",         out var d)  ? d.GetString()          : "";

                    if (!DateTime.TryParse(date, out var parsedDate)) continue;

                    var monthKey = parsedDate.ToString("yyyy-MM");
                    if (!aylikNet.ContainsKey(monthKey)) aylikNet[monthKey] = 0;
                    aylikNet[monthKey] += type == "Income" ? amt : -amt;

                    if (parsedDate.Month == now.Month && parsedDate.Year == now.Year)
                    {
                        if (type == "Income") ayGelir += amt;
                        else { ayGider += amt; katHar[cat] = katHar.GetValueOrDefault(cat) + amt; }
                    }

                    if (parsedDate.Month == gecenAy.Month && parsedDate.Year == gecenAy.Year)
                    {
                        if (type == "Income") gecenAyGelir += amt; else gecenAyGider += amt;
                    }
                }

                var net      = ayGelir - ayGider;
                var giderFark = ayGider - gecenAyGider;
                var tasarruf = ayGelir > 0 ? (net / ayGelir) * 100 : 0;

                sb.AppendLine($"Bu ay ({now:MMMM yyyy}):");
                sb.AppendLine($"  Gelir: {ayGelir:N2} TL");
                sb.AppendLine($"  Gider: {ayGider:N2} TL");
                sb.AppendLine($"  Net: {net:N2} TL");
                sb.AppendLine($"  Tasarruf orani: %{tasarruf:N1}");
                sb.AppendLine($"  Gecen ay gider: {gecenAyGider:N2} TL (fark: {(giderFark >= 0 ? "+" : "")}{giderFark:N2} TL)");
                sb.AppendLine($"  Toplam kayitli islem: {toplamTx}");
                sb.AppendLine();

                sb.AppendLine("Kategori Harcamalari (bu ay):");
                foreach (var kv in katHar.OrderByDescending(x => x.Value))
                {
                    var pct = ayGider > 0 ? (kv.Value / ayGider) * 100 : 0;
                    sb.AppendLine($"  {kv.Key}: {kv.Value:N2} TL (%{pct:N0})");
                }

                sb.AppendLine();
                sb.AppendLine("Son 6 Ay Net:");
                foreach (var kv in aylikNet.OrderByDescending(x => x.Key).Take(6))
                    sb.AppendLine($"  {kv.Key}: {kv.Value:N2} TL");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BuildDetailedStats error: {ex.Message}");
                return "Veri hesaplanamadi.";
            }
        }

        // işlem ekleme komutu
        private async Task<string> ExecuteAddTransaction(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                decimal amount      = args.GetProperty("amount").GetDecimal();
                string  type        = args.GetProperty("type").GetString() ?? "Expense";
                string  category    = args.GetProperty("category").GetString() ?? "Diger";
                string  description = args.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
                string  date        = args.TryGetProperty("date", out var dateEl)
                    ? dateEl.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd")
                    : DateTime.Now.ToString("yyyy-MM-dd");

                var catResponse = await apiClient.GetAsync("/api/category", ct);
                var catJson     = await catResponse.Content.ReadAsStringAsync(ct);
                var categories  = JsonSerializer.Deserialize<JsonElement>(catJson);

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
                        var catType = cat.TryGetProperty("type", out var t) ? t.GetString() : "";
                        if (catType == type) { categoryId = cat.GetProperty("categoryId").GetInt32(); break; }
                    }
                }

                var payload = new { amount, type, date = DateTime.Parse(date), description, categoryId, currency = "TRY" };
                var json    = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res     = await apiClient.PostAsync("/api/transaction", content, ct);

                if (res.IsSuccessStatusCode)
                {
                    string emoji    = type == "Income" ? "💰" : "💸";
                    string typeText = type == "Income" ? "Gelir" : "Gider";
                    return $"{emoji} *{typeText} eklendi!*\n\n" +
                           $"Tutar: *{(type == "Income" ? "+" : "-")}₺{amount:N2}*\n" +
                           $"Kategori: *{category}*\n" +
                           $"Aciklama: *{(string.IsNullOrEmpty(description) ? "Belirtilmedi" : description)}*\n" +
                           $"Tarih: *{date}*";
                }

                return "Islem eklenemedi. Lutfen tekrar dene.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Add transaction error: {ex.Message}");
                return "Islem eklenirken hata olustu.";
            }
        }

        // aylık özet getirme komutu
        private async Task<string> ExecuteGetMonthlySummary(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int month = args.GetProperty("month").GetInt32();
                int year  = args.GetProperty("year").GetInt32();

                var response     = await apiClient.GetAsync("/api/transaction", ct);
                var json         = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                decimal totalIncome = 0, totalExpense = 0;
                var categoryTotals = new Dictionary<string, decimal>();

                foreach (var t in transactions.EnumerateArray())
                {
                    var dateStr = t.GetProperty("date").GetString() ?? "";
                    if (!DateTime.TryParse(dateStr, out var date)) continue;
                    if (date.Month != month || date.Year != year) continue;

                    decimal amount = t.GetProperty("amount").GetDecimal();
                    string  ttype  = t.GetProperty("type").GetString() ?? "";
                    string  catName = t.TryGetProperty("categoryName", out var cn) ? cn.GetString() ?? "Diger" : "Diger";

                    if (ttype == "Income") totalIncome += amount;
                    else
                    {
                        totalExpense += amount;
                        if (!string.IsNullOrEmpty(catName))
                            categoryTotals[catName] = categoryTotals.GetValueOrDefault(catName) + amount;
                    }
                }

                decimal balance      = totalIncome - totalExpense;
                string  balanceEmoji = balance >= 0 ? "📈" : "📉";
                string  balanceSign  = balance >= 0 ? "+" : "";

                var monthNames = new[] { "", "Ocak", "Subat", "Mart", "Nisan", "Mayis", "Haziran",
                                         "Temmuz", "Agustos", "Eylul", "Ekim", "Kasim", "Aralik" };
                string monthName = month <= 12 ? monthNames[month] : month.ToString();

                var sb = new StringBuilder();
                sb.AppendLine($"*{monthName} {year} Ozeti*\n");
                sb.AppendLine($"Gelir: *₺{totalIncome:N2}*");
                sb.AppendLine($"Gider: *₺{totalExpense:N2}*");
                sb.AppendLine($"{balanceEmoji} Bakiye: *{balanceSign}₺{balance:N2}*");

                if (categoryTotals.Any())
                {
                    sb.AppendLine("\n*Kategori Dagilimi:*");
                    foreach (var cat in categoryTotals.OrderByDescending(c => c.Value).Take(5))
                        sb.AppendLine($"  {cat.Key}: ₺{cat.Value:N2}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Monthly summary error: {ex.Message}");
                return "Ozet getirilirken hata olustu.";
            }
        }

        // detaylı aylık rapor getirme komutu
        private async Task<string> ExecuteGetDetailedMonthlyReport(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int year  = args.TryGetProperty("year",  out var y) ? y.GetInt32() : DateTime.Now.Year;
                int month = args.TryGetProperty("month", out var m) ? m.GetInt32() : DateTime.Now.Month;

                var response = await apiClient.GetAsync(
                    $"/api/transaction/monthly-report?year={year}&month={month}", ct);

                if (!response.IsSuccessStatusCode)
                    return "Rapor bilgisi alinamadi.";

                var json = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var monthNames = new[] { "", "Ocak", "Subat", "Mart", "Nisan", "Mayis", "Haziran",
                                         "Temmuz", "Agustos", "Eylul", "Ekim", "Kasim", "Aralik" };
                string monthName = month <= 12 ? monthNames[month] : month.ToString();

                decimal income     = data.GetProperty("income").GetDecimal();
                decimal expense    = data.GetProperty("expense").GetDecimal();
                decimal balance    = data.GetProperty("balance").GetDecimal();
                decimal prevExpense= data.GetProperty("prevExpense").GetDecimal();
                double  expChange  = data.GetProperty("expenseChange").GetDouble();
                int     txCount    = data.GetProperty("transactionCount").GetInt32();

                string balanceEmoji = balance >= 0 ? "📈" : "📉";
                string balanceSign  = balance >= 0 ? "+" : "";
                string changeEmoji  = expChange <= 0 ? "✅" : "⚠️";
                string changeSign   = expChange >= 0 ? "+" : "";

                var sb = new StringBuilder();
                sb.AppendLine($"*{monthName} {year} Detayli Rapor*\n");
                sb.AppendLine($"Gelir: *₺{income:N2}*");
                sb.AppendLine($"Gider: *₺{expense:N2}*");
                sb.AppendLine($"{balanceEmoji} Net Bakiye: *{balanceSign}₺{balance:N2}*");
                sb.AppendLine($"Toplam Islem: *{txCount}*\n");

                if (prevExpense > 0)
                {
                    sb.AppendLine("*Gecen Ay Karsilastirmasi*");
                    sb.AppendLine($"{changeEmoji} Gider degisimi: *{changeSign}%{expChange:N1}*");
                    sb.AppendLine($"  Gecen ay: ₺{prevExpense:N2} - Bu ay: ₺{expense:N2}\n");
                }

                var cats = data.GetProperty("categoryBreakdown");
                if (cats.EnumerateArray().Any())
                {
                    sb.AppendLine("*Kategori Bazli Harcamalar:*");
                    foreach (var cat in cats.EnumerateArray().Take(6))
                    {
                        string icon   = cat.GetProperty("icon").GetString() ?? "";
                        string name   = cat.GetProperty("categoryName").GetString() ?? "";
                        decimal catAmt = cat.GetProperty("amount").GetDecimal();
                        int    count  = cat.GetProperty("count").GetInt32();
                        double pct    = expense > 0 ? (double)(catAmt / expense * 100) : 0;
                        sb.AppendLine($"  {icon} *{name}*: ₺{catAmt:N2} ({count} islem, %{pct:N0})");
                    }
                }

                if (data.TryGetProperty("firmBreakdown", out var firms) && firms.EnumerateArray().Any())
                {
                    sb.AppendLine("\n*Firma Bazli Harcamalar:*");
                    foreach (var f in firms.EnumerateArray().Take(5))
                    {
                        string  fname  = f.GetProperty("name").GetString() ?? "";
                        decimal ftotal = f.GetProperty("total").GetDecimal();
                        int     fcount = f.GetProperty("count").GetInt32();
                        sb.AppendLine($"  *{fname}*: ₺{ftotal:N2} ({fcount} islem)");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Detailed report error: {ex.Message}");
                return "Detayli rapor getirilirken hata olustu.";
            }
        }

        // belirli bir firma/mağazaya ne kadar harcandığı komutu
        private async Task<string> ExecuteGetFirmExpenses(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                string firmName = args.GetProperty("firm_name").GetString() ?? "";
                int?   month    = args.TryGetProperty("month", out var m) ? m.GetInt32() : null;
                int?   year     = args.TryGetProperty("year",  out var y) ? y.GetInt32() : null;

                var response     = await apiClient.GetAsync("/api/transaction", ct);
                var json         = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);

                var matched = new List<(decimal amount, string date, string category)>();

                foreach (var t in transactions.EnumerateArray())
                {
                    string desc  = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    string ttype = t.GetProperty("type").GetString() ?? "";
                    if (ttype != "Expense") continue;
                    if (!desc.Contains(firmName, StringComparison.OrdinalIgnoreCase)) continue;

                    string dateStr = t.GetProperty("date").GetString() ?? "";
                    if (!DateTime.TryParse(dateStr, out var date)) continue;
                    if (month.HasValue && date.Month != month.Value) continue;
                    if (year.HasValue  && date.Year  != year.Value)  continue;

                    decimal amount  = t.GetProperty("amount").GetDecimal();
                    string  catName = t.TryGetProperty("categoryName", out var cn) ? cn.GetString() ?? "" : "";
                    matched.Add((amount, date.ToString("dd.MM.yyyy"), catName));
                }

                if (!matched.Any())
                    return $"*\"{firmName}\"* ile esleşen gider bulunamadi.";

                decimal total = matched.Sum(m => m.amount);

                var monthNames = new[] { "", "Ocak", "Subat", "Mart", "Nisan", "Mayis", "Haziran",
                                         "Temmuz", "Agustos", "Eylul", "Ekim", "Kasim", "Aralik" };
                string period = month.HasValue
                    ? $"{monthNames[month.Value]} {year ?? DateTime.Now.Year}"
                    : "Tum Zamanlar";

                var sb = new StringBuilder();
                sb.AppendLine($"*\"{firmName}\"* Harcamalari ({period})\n");
                sb.AppendLine($"Toplam: *₺{total:N2}* ({matched.Count} islem)\n");

                if (matched.Count <= 8)
                {
                    sb.AppendLine("*Detay:*");
                    foreach (var (amount, date, cat) in matched.OrderByDescending(m => m.date))
                        sb.AppendLine($"  {date}: ₺{amount:N2} ({cat})");
                }
                else
                {
                    sb.AppendLine($"Ortalama: *₺{(total / matched.Count):N2}* / islem");
                    sb.AppendLine($"En yuksek: *₺{matched.Max(m => m.amount):N2}*");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Firm expenses error: {ex.Message}");
                return "Firma harcamasi sorgulanirken hata olustu.";
            }
        }

        // bütçe durumunu gösterme komutu
        private async Task<string> ExecuteGetBudgetStatus(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int month = args.GetProperty("month").GetInt32();
                int year  = args.GetProperty("year").GetInt32();

                var response = await apiClient.GetAsync($"/api/budget?month={month}&year={year}", ct);
                var json     = await response.Content.ReadAsStringAsync(ct);
                var data     = JsonSerializer.Deserialize<JsonElement>(json);

                var budgets = data.ValueKind == JsonValueKind.Array ? data :
                    (data.TryGetProperty("budgets", out var b) ? b : data);

                if (!budgets.EnumerateArray().Any())
                    return "Bu ay icin tanimli butce yok.\n\nFinAware'den butce ekleyebilirsin!";

                var sb = new StringBuilder();
                sb.AppendLine("*Butce Durumu*\n");

                foreach (var b2 in budgets.EnumerateArray())
                {
                    string  catName  = b2.TryGetProperty("categoryName", out var cn) ? cn.GetString() ?? "Genel" : "Genel";
                    string  catIcon  = b2.TryGetProperty("categoryIcon", out var ci) ? ci.GetString() ?? "" : "";
                    decimal limit    = b2.TryGetProperty("limit",        out var l)  ? l.GetDecimal()  :
                                       b2.TryGetProperty("limitAmount",  out var la) ? la.GetDecimal() : 0;
                    decimal spent    = b2.TryGetProperty("spent",        out var s)  ? s.GetDecimal()  :
                                       b2.TryGetProperty("spentAmount",  out var sa) ? sa.GetDecimal() : 0;
                    decimal remaining = limit - spent;
                    double  pct      = limit > 0 ? (double)(spent / limit * 100) : 0;
                    bool    isOver   = spent > limit;

                    string statusEmoji = isOver ? "🚨" : pct >= 80 ? "⚠️" : pct >= 50 ? "📊" : "✅";
                    sb.AppendLine($"{statusEmoji} *{catIcon} {catName}*");
                    sb.AppendLine($"  Limit: ₺{limit:N2} | Harcanan: ₺{spent:N2}");
                    sb.AppendLine($"  Kalan: ₺{remaining:N2} (%{pct:N1})\n");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Budget status error: {ex.Message}");
                return "Butce bilgisi getirilirken hata olustu.";
            }
        }

        // son işlemler getirme komutu
        private async Task<string> ExecuteGetRecentTransactions(
            JsonElement args, HttpClient apiClient, CancellationToken ct)
        {
            try
            {
                int count = args.TryGetProperty("count", out var countEl)
                    ? Math.Min(countEl.GetInt32(), 10) : 5;

                var response     = await apiClient.GetAsync("/api/transaction", ct);
                var json         = await response.Content.ReadAsStringAsync(ct);
                var transactions = JsonSerializer.Deserialize<JsonElement>(json);
                var recent       = transactions.EnumerateArray().Take(count).ToList();

                if (!recent.Any())
                    return "Henuz hic islem yok.";

                var sb = new StringBuilder();
                sb.AppendLine($"*Son {count} Islem*\n");

                foreach (var t in recent)
                {
                    string  ttype   = t.GetProperty("type").GetString() ?? "";
                    decimal amount  = t.GetProperty("amount").GetDecimal();
                    string  dateStr = t.GetProperty("date").GetString() ?? "";
                    DateTime.TryParse(dateStr, out var date);
                    string  catName = t.TryGetProperty("categoryName", out var cn) ? cn.GetString() ?? "" : "";
                    string  catIcon = t.TryGetProperty("categoryIcon", out var ci) ? ci.GetString() ?? "" : "";

                    string emoji = ttype == "Income" ? "💰" : "💸";
                    string sign  = ttype == "Income" ? "+" : "-";
                    sb.AppendLine($"{emoji} {sign}₺{amount:N2} | {catIcon}{catName} | {date:dd.MM}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recent transactions error: {ex.Message}");
                return "Islemler getirilirken hata olustu.";
            }
        }
    }
}