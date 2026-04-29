using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinAware.MVC.Models;

namespace FinAware.MVC.Controllers
{
    public class SavingController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public SavingController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient CreateAuthClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://localhost:7061");
            var token = HttpContext.Session.GetString("AuthToken");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private bool IsAuthenticated() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken"));

        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.GetAsync("/api/saving");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var savings = JsonSerializer.Deserialize<List<SavingViewModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return View(savings);
            }

            TempData["Error"] = "Birikimler yüklenemedi";
            return View(new List<SavingViewModel>());
        }

        public IActionResult Create()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SavingViewModel model)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var savingData = new
            {
                goalName = model.GoalName,
                targetAmount = model.TargetAmount,
                targetDate = model.TargetDate,
                icon = model.Icon ?? "💰",
                color = model.Color ?? "#4DB6AC"
            };

            var json = JsonSerializer.Serialize(savingData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/saving", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Birikim hedefi başarıyla eklendi!";
                return RedirectToAction("Index");
            }

            TempData["Error"] = "Birikim eklenemedi";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAmount(int id, string amount, string currency, string? note, decimal? manualRate)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            if (!decimal.TryParse(amount?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal parsedAmount) || parsedAmount <= 0)
            {
                TempData["Error"] = "Geçerli bir tutar girin";
                return RedirectToAction("Index");
            }

            var client = CreateAuthClient();
            var data = new
            {
                amount = parsedAmount,
                currency = currency ?? "TRY",
                date = DateTime.Now,
                note,
                manualRate
            };

            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/saving/{id}/add", content);

            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseText);

                if (currency != null && currency != "TRY" &&
                    result.TryGetProperty("addedTry", out var addedTry) &&
                    result.TryGetProperty("exchangeRate", out var rate))
                {
                    TempData["Success"] = $"{parsedAmount} {currency} = ₺{addedTry.GetDecimal():N2} (Kur: {rate.GetDecimal():N4}) başarıyla eklendi!";
                }
                else
                {
                    TempData["Success"] = "Tutar başarıyla eklendi!";
                }
            }
            else
            {
                TempData["Error"] = "Tutar eklenemedi";
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.GetAsync($"/api/saving/{id}/details");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                ViewBag.DetailsJson = json;
                ViewBag.SavingId = id;
                return View();
            }

            TempData["Error"] = "Birikim detayları yüklenemedi";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> GetRates()
        {
            if (!IsAuthenticated()) return Unauthorized();

            var client = CreateAuthClient();
            var response = await client.GetAsync("/api/saving/rates");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }

            return StatusCode(500);
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTransaction(
            int savingId, int transactionId,
            string amount, string currency, string? note, decimal? manualRate, string? date)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            if (!decimal.TryParse(amount?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal parsedAmount) || parsedAmount <= 0)
            {
                TempData["Error"] = "Geçerli bir tutar girin";
                return RedirectToAction("Details", new { id = savingId });
            }

            var client = CreateAuthClient();
            var data = new
            {
                amount = parsedAmount,
                currency = currency ?? "TRY",
                note,
                manualRate,
                date = string.IsNullOrEmpty(date) ? (DateTime?)null : DateTime.Parse(date)
            };

            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/saving/transaction/{transactionId}", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "İşlem başarıyla güncellendi!";
            }
            else
            {
                var errText = await response.Content.ReadAsStringAsync();
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(errText);
                    TempData["Error"] = err.TryGetProperty("message", out var m)
                        ? m.GetString() : "İşlem güncellenemedi";
                }
                catch { TempData["Error"] = "İşlem güncellenemedi"; }
            }

            return RedirectToAction("Details", new { id = savingId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(int savingId, int transactionId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.DeleteAsync($"/api/saving/transaction/{transactionId}");

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "İşlem başarıyla silindi!";
            }
            else
            {
                var errText = await response.Content.ReadAsStringAsync();
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(errText);
                    TempData["Error"] = err.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "İşlem silinemedi";
                }
                catch
                {
                    TempData["Error"] = "İşlem silinemedi";
                }
            }

            return RedirectToAction("Details", new { id = savingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sell(int savingId, int depositTxId, string saleRate, string? note)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            if (!decimal.TryParse(saleRate?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal parsedRate) || parsedRate <= 0)
            {
                TempData["Error"] = "Geçerli bir satış kuru giriniz";
                return RedirectToAction("Details", new { id = savingId });
            }

            var client = CreateAuthClient();
            var data = new { saleRate = parsedRate, note };
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/saving/{savingId}/sell/{depositTxId}", content);

            if (response.IsSuccessStatusCode)
            {
                var respText = await response.Content.ReadAsStringAsync();
                try
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(respText);
                    decimal sale = result.GetProperty("saleAmount").GetDecimal();
                    decimal buy = result.GetProperty("buyAmount").GetDecimal();
                    decimal pl = result.GetProperty("realizedProfitLoss").GetDecimal();

                    string sign = pl >= 0 ? "+" : "";
                    string emoji = pl >= 0 ? "📈" : "📉";
                    TempData["Success"] = $"{emoji} Satış yapıldı: ₺{sale:N2} (Alış: ₺{buy:N2}) | Kar/Zarar: {sign}₺{pl:N2}";
                }
                catch
                {
                    TempData["Success"] = "Satış başarıyla yapıldı!";
                }
            }
            else
            {
                var errText = await response.Content.ReadAsStringAsync();
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(errText);
                    TempData["Error"] = err.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "Satış yapılamadı";
                }
                catch
                {
                    TempData["Error"] = "Satış yapılamadı";
                }
            }

            return RedirectToAction("Details", new { id = savingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReverseSale(int savingId, int saleTxId)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.DeleteAsync($"/api/saving/sale/{saleTxId}");

            if (response.IsSuccessStatusCode)
                TempData["Success"] = "Satış başarıyla geri alındı!";
            else
            {
                var errText = await response.Content.ReadAsStringAsync();
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(errText);
                    TempData["Error"] = err.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "Satış geri alınamadı";
                }
                catch
                {
                    TempData["Error"] = "Satış geri alınamadı";
                }
            }

            return RedirectToAction("Details", new { id = savingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.DeleteAsync($"/api/saving/{id}");

            if (response.IsSuccessStatusCode) TempData["Success"] = "Birikim hedefi silindi!";
            else TempData["Error"] = "Birikim silinemedi";

            return RedirectToAction("Index");
        }
    }
}