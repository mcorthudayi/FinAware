using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinAware.API.Data;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FinAware.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ArisController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public ArisController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ArisChatRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return Unauthorized();

                var username = user.Username;
                var now = DateTime.Now;
                var ay = now.Month;
                var yil = now.Year;

                // ── Verileri çek ─────────────────────────────────────────
                var transactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == userId)
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                var budgets = await _context.Budgets
                    .Include(b => b.Category)
                    .Where(b => b.UserId == userId && b.Month == ay && b.Year == yil)
                    .ToListAsync();

                var savings = await _context.Savings
                    .Where(s => s.UserId == userId)
                    .ToListAsync();

                // ── Hesaplamalar ──────────────────────────────────────────
                decimal totalIncome = 0, totalExpense = 0;
                decimal monthIncome = 0, monthExpense = 0;
                var categoryTotals = new Dictionary<string, decimal>();

                foreach (var t in transactions)
                {
                    if (t.Type == "Income") totalIncome += t.Amount;
                    else
                    {
                        totalExpense += t.Amount;
                        var catName = t.Category?.Name ?? "Diğer";
                        categoryTotals[catName] = categoryTotals.GetValueOrDefault(catName) + t.Amount;
                    }

                    if (t.Date.Month == ay && t.Date.Year == yil)
                    {
                        if (t.Type == "Income") monthIncome += t.Amount;
                        else monthExpense += t.Amount;
                    }
                }

                var topCategories = categoryTotals
                    .OrderByDescending(x => x.Value)
                    .Take(3)
                    .Select(x => $"{x.Key}: ₺{x.Value:N2}")
                    .ToList();

                // ── Bütçe özeti ───────────────────────────────────────────
                var budgetSummary = new List<string>();
                foreach (var b in budgets)
                {
                    var spent = transactions
                        .Where(t => t.Type == "Expense"
                            && t.Date.Month == ay && t.Date.Year == yil
                            && (b.CategoryId == null || t.CategoryId == b.CategoryId))
                        .Sum(t => t.Amount);
                    var pct = b.LimitAmount > 0 ? (spent / b.LimitAmount) * 100 : 0;
                    var catName = b.Category?.Name ?? "Genel";
                    budgetSummary.Add($"{catName}: ₺{spent:N2}/₺{b.LimitAmount:N2} (%{pct:N0})");
                }

                // ── Birikim özeti ─────────────────────────────────────────
                var savingSummary = savings.Select(s =>
                {
                    var pct = s.TargetAmount > 0 ? (s.CurrentAmount / s.TargetAmount) * 100 : 0;
                    return $"{s.GoalName}: ₺{s.CurrentAmount:N2}/₺{s.TargetAmount:N2} (%{pct:N0})";
                }).ToList();

                // ── Son 5 işlem ───────────────────────────────────────────
                var recentTx = transactions.Take(5).Select(t =>
                    $"{(t.Type == "Income" ? "+" : "-")}₺{t.Amount:N2} {t.Category?.Name} - {t.Description}").ToList();

                // ── Sistem promptu ────────────────────────────────────────
                var systemPrompt = $@"Sen ARİS'sin — FinAware'in kişisel finansal asistanı. Bir finansal analist gibi düşünürsün, verilerle savaşır ve kullanıcıya net, dürüst tavsiyeler verirsin.

Kullanıcı: {username}
Tarih: {now:dd MMMM yyyy}

FİNANSAL DURUM:
Genel:
- Toplam Gelir: ₺{totalIncome:N2}
- Toplam Gider: ₺{totalExpense:N2}
- Net Bakiye: ₺{(totalIncome - totalExpense):N2}

Bu Ay ({now:MMMM yyyy}):
- Gelir: ₺{monthIncome:N2}
- Gider: ₺{monthExpense:N2}
- Net: ₺{(monthIncome - monthExpense):N2}

En Yüksek Harcama Kategorileri:
{(topCategories.Any() ? string.Join("\n", topCategories.Select(c => $"- {c}")) : "- Veri yok")}

Bütçe Durumu:
{(budgetSummary.Any() ? string.Join("\n", budgetSummary.Select(b => $"- {b}")) : "- Tanımlı bütçe yok")}

Birikim Hedefleri:
{(savingSummary.Any() ? string.Join("\n", savingSummary.Select(s => $"- {s}")) : "- Birikim hedefi yok")}

Son İşlemler:
{(recentTx.Any() ? string.Join("\n", recentTx.Select(t => $"- {t}")) : "- İşlem yok")}

KURALLAR:
- Kısa ve net cevaplar ver (max 3-4 cümle)
- Kullanıcının gerçek verilerine göre kişiselleştirilmiş tavsiye ver
- Türkçe konuş, samimi ama profesyonel ol
- Gerektiğinde uyarı ver (bütçe aşımı, yüksek harcama vb.)
- Rakamları ₺ ile göster
- Emoji kullanma";

                // ── OpenAI çağrısı ────────────────────────────────────────
                var openAiKey = _configuration["OpenAI__ApiKey"]
                             ?? _configuration["OpenAI:ApiKey"];

                if (string.IsNullOrEmpty(openAiKey))
                    return Ok(new { reply = "OpenAI API anahtarı bulunamadı." });

                var payload = new
                {
                    model = "gpt-4o",
                    max_tokens = 300,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = request.Message }
                    }
                };

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ OpenAI error: {responseText}");
                    return Ok(new { reply = "Yapay zeka servisine ulaşılamıyor, biraz sonra tekrar dene." });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseText);
                var reply = result
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "Bir hata oluştu.";

                Console.WriteLine($"✅ ARİS yanıt verdi: {username}");
                return Ok(new { reply });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ARİS error: {ex.Message}");
                return Ok(new { reply = "Bir hata oluştu, biraz sonra tekrar dene." });
            }
        }
    }

    public class ArisChatRequest
    {
        public string Message { get; set; } = "";
    }
}