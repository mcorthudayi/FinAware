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

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ArisChatRequest request)
        {
            try
            {
                var userId = GetUserId();
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return Unauthorized();

                var now = DateTime.Now;
                var ay = now.Month;
                var yil = now.Year;

                // Verileri çek
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

                var categories = await _context.Categories
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                // Hesaplamalar
                decimal totalIncome = 0, totalExpense = 0, monthIncome = 0, monthExpense = 0;
                var categoryTotals = new Dictionary<string, decimal>();

                foreach (var t in transactions)
                {
                    if (t.Type == "Income") totalIncome += t.Amount;
                    else
                    {
                        totalExpense += t.Amount;
                        var cn = t.Category?.Name ?? "Diğer";
                        categoryTotals[cn] = categoryTotals.GetValueOrDefault(cn) + t.Amount;
                    }
                    if (t.Date.Month == ay && t.Date.Year == yil)
                    {
                        if (t.Type == "Income") monthIncome += t.Amount;
                        else monthExpense += t.Amount;
                    }
                }

                var topCats = categoryTotals.OrderByDescending(x => x.Value).Take(3)
                    .Select(x => $"{x.Key}: ₺{x.Value:N2}").ToList();

                var budgetSummary = budgets.Select(b => {
                    var spent = transactions
                        .Where(t => t.Type == "Expense" && t.Date.Month == ay && t.Date.Year == yil
                            && (b.CategoryId == null || t.CategoryId == b.CategoryId))
                        .Sum(t => t.Amount);
                    var pct = b.LimitAmount > 0 ? (spent / b.LimitAmount) * 100 : 0;
                    return $"{b.Category?.Name ?? "Genel"}: ₺{spent:N2}/₺{b.LimitAmount:N2} (%{pct:N0})";
                }).ToList();

                var savingSummary = savings.Select(s => {
                    var pct = s.TargetAmount > 0 ? (s.CurrentAmount / s.TargetAmount) * 100 : 0;
                    return $"{s.GoalName}: ₺{s.CurrentAmount:N2}/₺{s.TargetAmount:N2} (%{pct:N0})";
                }).ToList();

                var catList = categories.Select(c => $"{c.Name} ({c.Type})").ToList();

                // Sistem promptu 
                var systemPrompt = $@"Sen ARİS'sin — FinAware'in akıllı finansal asistanı.

Kullanıcı: {user.Username}
Tarih: {now:dd MMMM yyyy}

FİNANSAL DURUM:
- Toplam Gelir: ₺{totalIncome:N2} | Toplam Gider: ₺{totalExpense:N2} | Net: ₺{(totalIncome - totalExpense):N2}
- Bu Ay: Gelir ₺{monthIncome:N2} | Gider ₺{monthExpense:N2} | Net ₺{(monthIncome - monthExpense):N2}
- Top Harcama: {(topCats.Any() ? string.Join(", ", topCats) : "Veri yok")}
- Bütçe: {(budgetSummary.Any() ? string.Join(" | ", budgetSummary) : "Tanımlı bütçe yok")}
- Birikim: {(savingSummary.Any() ? string.Join(" | ", savingSummary) : "Birikim hedefi yok")}
- Kategoriler: {(catList.Any() ? string.Join(", ", catList) : "Kategori yok")}

YAPABİLECEKLERİN:
Kullanıcı isterse şu aksiyonları tetikleyebilirsin. JSON formatında yanıt ver:
{{""reply"": ""mesaj"", ""action"": {{""type"": ""aksiyon_tipi"", ...}}}}

Aksiyon tipleri:
- navigate: {{""type"":""navigate"",""url"":""/Saving/Index""}} — Sayfalara git
  Sayfalar: /Dashboard/Index, /Transaction/Index, /Transaction/Create, /Budget/Index, /Saving/Index, /Analysis/Index, /Category/Index, /Subscription/Index
- add_transaction: {{""type"":""add_transaction"",""txType"":""Income""|""Expense"",""amount"":1000,""category"":""Maaş"",""description"":""..."" }}
- create_budget: {{""type"":""create_budget"",""category"":""Market"",""limit"":2000}}
- create_saving: {{""type"":""create_saving"",""name"":""Tatil"",""target"":15000}}
- add_to_saving: {{""type"":""add_to_saving"",""name"":""Tatil"",""amount"":500}}
- open_modal: {{""type"":""open_modal""}} — İşlem ekleme modalını aç

Aksiyon gerektirmeyen sohbet sorularında sadece: {{""reply"": ""mesaj""}}

KURALLAR:
- Kısa ve net cevaplar ver (max 4 cümle)
- Türkçe, samimi ama profesyonel
- Rakamları ₺ ile göster
- Emoji kullanma
- Her zaman geçerli JSON döndür";

                // OpenAI çağrısı 
                var openAiKey = _configuration["OpenAI__ApiKey"] ?? _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(openAiKey))
                    return Ok(new { reply = "OpenAI API anahtarı bulunamadı.", action = (object?)null });

                var payload = new
                {
                    model = "gpt-4o",
                    max_tokens = 400,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = request.Message }
                    },
                    response_format = new { type = "json_object" }
                };

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var resText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ OpenAI error: {resText}");
                    return Ok(new { reply = "Yapay zeka servisine ulaşılamıyor.", action = (object?)null });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(resText);
                var rawReply = result
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "{}";

                // Aksiyonu parse et 
                try
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(rawReply);
                    var reply = parsed.TryGetProperty("reply", out var r) ? r.GetString() : rawReply;
                    var action = parsed.TryGetProperty("action", out var a) ? (object?)a : null;

                    // Aksiyon varsa işle
                    if (action != null)
                    {
                        var actionEl = (JsonElement)action;
                        var actionType = actionEl.TryGetProperty("type", out var t) ? t.GetString() : "";

                        if (actionType == "add_transaction")
                        {
                            await ExecuteAddTransaction(actionEl, userId);
                        }
                        else if (actionType == "create_budget")
                        {
                            await ExecuteCreateBudget(actionEl, userId);
                        }
                        else if (actionType == "create_saving")
                        {
                            await ExecuteCreateSaving(actionEl, userId);
                        }
                        else if (actionType == "add_to_saving")
                        {
                            await ExecuteAddToSaving(actionEl, userId);
                        }
                    }

                    Console.WriteLine($"✅ ARİS: {user.Username} → {actionType(action)}");
                    return Ok(new { reply, action });
                }
                catch
                {
                    return Ok(new { reply = rawReply, action = (object?)null });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ARİS error: {ex.Message}");
                return Ok(new { reply = "Bir hata oluştu, biraz sonra tekrar dene.", action = (object?)null });
            }
        }

        private string actionType(object? action)
        {
            if (action == null) return "chat";
            try
            {
                var el = (JsonElement)action;
                return el.TryGetProperty("type", out var t) ? t.GetString() ?? "chat" : "chat";
            }
            catch { return "chat"; }
        }

        private async Task ExecuteAddTransaction(JsonElement action, int userId)
        {
            try
            {
                var txType = action.TryGetProperty("txType", out var tt) ? tt.GetString() ?? "Expense" : "Expense";
                var amount = action.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0;
                var catName = action.TryGetProperty("category", out var cn) ? cn.GetString() ?? "" : "";
                var desc = action.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Name.ToLower().Contains(catName.ToLower()));

                var tx = new Models.Transaction
                {
                    UserId = userId,
                    Type = txType,
                    Amount = amount,
                    CategoryId = category?.CategoryId ?? 0,
                    Description = desc,
                    Date = DateTime.Now,
                    OriginalCurrency = "TRY",
                    CreatedAt = DateTime.Now
                };
                _context.Transactions.Add(tx);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ ARİS işlem ekledi: {txType} ₺{amount}");
            }
            catch (Exception ex) { Console.WriteLine($"❌ ARİS add_transaction: {ex.Message}"); }
        }

        private async Task ExecuteCreateBudget(JsonElement action, int userId)
        {
            try
            {
                var catName = action.TryGetProperty("category", out var cn) ? cn.GetString() ?? "" : "";
                var limit = action.TryGetProperty("limit", out var l) ? l.GetDecimal() : 0;

                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Name.ToLower().Contains(catName.ToLower()));

                var budget = new Models.Budget
                {
                    UserId = userId,
                    CategoryId = category?.CategoryId,
                    LimitAmount = limit,
                    Month = DateTime.Now.Month,
                    Year = DateTime.Now.Year,
                    CreatedAt = DateTime.Now
                };
                _context.Budgets.Add(budget);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ ARİS bütçe oluşturdu: {catName} ₺{limit}");
            }
            catch (Exception ex) { Console.WriteLine($"❌ ARİS create_budget: {ex.Message}"); }
        }

        private async Task ExecuteCreateSaving(JsonElement action, int userId)
        {
            try
            {
                var name = action.TryGetProperty("name", out var n) ? n.GetString() ?? "Hedef" : "Hedef";
                var target = action.TryGetProperty("target", out var t) ? t.GetDecimal() : 0;

                var saving = new Models.Saving
                {
                    UserId = userId,
                    GoalName = name,
                    TargetAmount = target,
                    CurrentAmount = 0,
                    Icon = "💰",
                    Color = "#1AAFA3",
                    CreatedAt = DateTime.Now
                };
                _context.Savings.Add(saving);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ ARİS birikim oluşturdu: {name} ₺{target}");
            }
            catch (Exception ex) { Console.WriteLine($"❌ ARİS create_saving: {ex.Message}"); }
        }

        private async Task ExecuteAddToSaving(JsonElement action, int userId)
        {
            try
            {
                var name = action.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var amount = action.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0;

                var saving = await _context.Savings
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.GoalName.ToLower().Contains(name.ToLower()));

                if (saving != null)
                {
                    saving.CurrentAmount += amount;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ ARİS birikime ekledi: {name} +₺{amount}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ ARİS add_to_saving: {ex.Message}"); }
        }
    }

    public class ArisChatRequest
    {
        public string Message { get; set; } = "";
    }
}