using FinAware.MVC.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAware.MVC.Controllers
{
    public class ArisController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ArisController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private HttpClient CreateApiClient()
        {
            var client = _httpClientFactory.CreateClient();
            var apiBase = _configuration["ApiBaseUrl"] ?? "https://finaware-uq2x.onrender.com";
            client.BaseAddress = new Uri(apiBase);
            var token = HttpContext.Session.GetString("AuthToken");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ArisChatRequest request)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return Unauthorized();

            try
            {
                var client = CreateApiClient();
                var username = HttpContext.Session.GetString("Username") ?? "Kullanıcı";

                // Kullanıcı verilerini çek
                var txTask = client.GetAsync("/api/transaction");
                var budgetTask = client.GetAsync($"/api/budget?month={DateTime.Now.Month}&year={DateTime.Now.Year}");
                var savingTask = client.GetAsync("/api/saving");

                await Task.WhenAll(txTask, budgetTask, savingTask);

                var txJson = await txTask.Result.Content.ReadAsStringAsync();
                var budgetJson = await budgetTask.Result.Content.ReadAsStringAsync();
                var savingJson = await savingTask.Result.Content.ReadAsStringAsync();

                // Özet hesapla
                var transactions = JsonSerializer.Deserialize<JsonElement>(txJson);
                decimal totalIncome = 0, totalExpense = 0, monthIncome = 0, monthExpense = 0;
                var categoryTotals = new Dictionary<string, decimal>();
                int ay = DateTime.Now.Month, yil = DateTime.Now.Year;

                foreach (var t in transactions.EnumerateArray())
                {
                    var amount = t.GetProperty("amount").GetDecimal();
                    var type = t.GetProperty("type").GetString();
                    var dateStr = t.TryGetProperty("date", out var de) ? de.GetString() : null;
                    var date = dateStr != null ? DateTime.Parse(dateStr) : DateTime.MinValue;
                    var catName = "";
                    if (t.TryGetProperty("category", out var ce) && ce.ValueKind != JsonValueKind.Null)
                        catName = ce.TryGetProperty("name", out var ne) ? ne.GetString() ?? "" : "";

                    if (type == "Income") totalIncome += amount;
                    else
                    {
                        totalExpense += amount;
                        if (!string.IsNullOrEmpty(catName))
                            categoryTotals[catName] = categoryTotals.GetValueOrDefault(catName) + amount;
                    }

                    if (date.Month == ay && date.Year == yil)
                    {
                        if (type == "Income") monthIncome += amount;
                        else monthExpense += amount;
                    }
                }

                var topCategories = categoryTotals
                    .OrderByDescending(x => x.Value)
                    .Take(3)
                    .Select(x => $"{x.Key}: ₺{x.Value:N2}")
                    .ToList();

                // Bütçe özeti
                var budgetData = JsonSerializer.Deserialize<JsonElement>(budgetJson);
                var budgetSummary = new List<string>();
                if (budgetData.TryGetProperty("budgets", out var budgets))
                {
                    foreach (var b in budgets.EnumerateArray())
                    {
                        var limit = b.GetProperty("limitAmount").GetDecimal();
                        var spent = b.TryGetProperty("spentAmount", out var se) ? se.GetDecimal() : 0;
                        var pct = limit > 0 ? (spent / limit) * 100 : 0;
                        var catName = "Genel";
                        if (b.TryGetProperty("category", out var bc) && bc.ValueKind != JsonValueKind.Null)
                            catName = bc.TryGetProperty("name", out var bn) ? bn.GetString() ?? "Genel" : "Genel";
                        budgetSummary.Add($"{catName}: ₺{spent:N2}/₺{limit:N2} (%{pct:N0})");
                    }
                }

                // Birikim özeti
                var savingData = JsonSerializer.Deserialize<JsonElement>(savingJson);
                var savingSummary = new List<string>();
                foreach (var s in savingData.EnumerateArray())
                {
                    var goalName = s.GetProperty("goalName").GetString();
                    var target = s.GetProperty("targetAmount").GetDecimal();
                    var current = s.GetProperty("currentAmount").GetDecimal();
                    var pct = target > 0 ? (current / target) * 100 : 0;
                    savingSummary.Add($"{goalName}: ₺{current:N2}/₺{target:N2} (%{pct:N0})");
                }

                // GPT-4o sistem promptu
                var systemPrompt = $@"Sen ARİS'sin — FinAware'in kişisel finansal asistanı. 
Kullanıcı adı: {username}
Bugün: {DateTime.Now:dd MMMM yyyy}

KULLANICI'NIN FİNANSAL DURUMU:
Genel Bakiye:
- Toplam Gelir: ₺{totalIncome:N2}
- Toplam Gider: ₺{totalExpense:N2}  
- Net Bakiye: ₺{(totalIncome - totalExpense):N2}

Bu Ay ({DateTime.Now:MMMM yyyy}):
- Gelir: ₺{monthIncome:N2}
- Gider: ₺{monthExpense:N2}
- Net: ₺{(monthIncome - monthExpense):N2}

En Yüksek Harcama Kategorileri:
{string.Join("\n", topCategories.Select(c => $"- {c}"))}

Bütçe Durumu:
{(budgetSummary.Any() ? string.Join("\n", budgetSummary.Select(b => $"- {b}")) : "- Tanımlı bütçe yok")}

Birikim Hedefleri:
{(savingSummary.Any() ? string.Join("\n", savingSummary.Select(s => $"- {s}")) : "- Birikim hedefi yok")}

KURALLAR:
- Kısa, net ve samimi cevaplar ver (max 3-4 cümle)
- Kullanıcının gerçek verilerine göre kişiselleştirilmiş tavsiye ver
- Türkçe konuş, resmi değil samimi ol
- Gerektiğinde uyarı ver (bütçe aşımı, tasarruf önerisi vb.)
- Rakamları her zaman ₺ ile göster";

                // OpenAI API çağrısı
                var openAiKey = _configuration["OpenAI:ApiKey"]
                             ?? _configuration["OpenAI__ApiKey"];

                var openAiPayload = new
                {
                    model = "gpt-4o",
                    max_tokens = 300,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = request.Message }
                    }
                };

                using var openAiClient = new HttpClient();
                openAiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
                var openAiJson = JsonSerializer.Serialize(openAiPayload);
                var openAiContent = new StringContent(openAiJson, Encoding.UTF8, "application/json");
                var openAiResponse = await openAiClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions", openAiContent);
                var openAiText = await openAiResponse.Content.ReadAsStringAsync();
                var openAiResult = JsonSerializer.Deserialize<JsonElement>(openAiText);

                var reply = openAiResult
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "Bir hata oluştu.";

                return Ok(new { reply });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ARİS error: {ex.Message}");
                return Ok(new { reply = "Şu an verilerine ulaşamıyorum, biraz sonra tekrar dene." });
            }
        }
    }

    public class ArisChatRequest
    {
        public string Message { get; set; } = "";
    }
}