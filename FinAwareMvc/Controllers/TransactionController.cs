using FinAware.MVC.Models.ViewModels;
using FinAware.MVC.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAware.MVC.Controllers
{
    public class TransactionController : Controller
    {
        private readonly IApiService _apiService;
        private readonly IHttpClientFactory _httpClientFactory;

        public TransactionController(IApiService apiService, IHttpClientFactory httpClientFactory)
        {
            _apiService = apiService;
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
            var transactions = await _apiService.GetTransactionsAsync();
            return View(transactions);
        }

        public async Task<IActionResult> Create()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");
            var categories = await _apiService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransactionViewModel model)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();

            var transactionData = new
            {
                amount = model.Amount,
                type = model.Type,
                date = model.Date,
                description = model.Description ?? string.Empty,
                categoryId = model.CategoryId,
                currency = string.IsNullOrEmpty(model.Currency) ? "TRY" : model.Currency,
                manualRate = model.ManualRate
            };

            var json = JsonSerializer.Serialize(transactionData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/transaction", content);

            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseText);

                decimal displayAmount = model.Amount;
                string displayCurrency = model.Currency ?? "TRY";

                if (responseData.TryGetProperty("tryAmount", out var tryEl))
                    displayAmount = tryEl.GetDecimal();

                // ── Site içi işlem bildirimi ──────────────────────────────
                try
                {
                    var typeText = model.Type == "Income" ? "Gelir" : "Gider";
                    var icon = model.Type == "Income" ? "💰" : "💸";

                    string notifMessage = displayCurrency != "TRY"
                        ? $"{model.Amount} {displayCurrency} = ₺{displayAmount:N2} - {(string.IsNullOrEmpty(model.Description) ? "Açıklama yok" : model.Description)}"
                        : $"₺{displayAmount:N2} - {(string.IsNullOrEmpty(model.Description) ? "Açıklama yok" : model.Description)}";

                    var notifData = new
                    {
                        title = $"{icon} Yeni {typeText} Eklendi",
                        message = notifMessage,
                        type = model.Type,
                        icon
                    };
                    var notifJson = JsonSerializer.Serialize(notifData);
                    var notifContent = new StringContent(notifJson, Encoding.UTF8, "application/json");
                    await client.PostAsync("/api/notification", notifContent);
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ Notification failed: {ex.Message}"); }

                // ── Hatırlatıcı bildirimi ─────────────────────────────────
                if (model.ReminderDate.HasValue)
                {
                    try
                    {
                        var reminderData = new
                        {
                            title = $"⏰ {(model.Type == "Income" ? "Gelir" : "Gider")} Hatırlatıcı",
                            message = $"₺{displayAmount:N2} - {(string.IsNullOrEmpty(model.Description) ? "Açıklama yok" : model.Description)}",
                            reminderDate = model.ReminderDate.Value,
                            type = "Reminder",
                            icon = "⏰"
                        };
                        var reminderJson = JsonSerializer.Serialize(reminderData);
                        var reminderContent = new StringContent(reminderJson, Encoding.UTF8, "application/json");
                        await client.PostAsync("/api/notification", reminderContent);
                    }
                    catch (Exception ex) { Console.WriteLine($"⚠️ Reminder failed: {ex.Message}"); }
                }

                // ── Bütçe kontrolü (%50 / %80 / %100) ───────────────────
                try
                {
                    await _apiService.CheckBudgetAsync();
                    Console.WriteLine("✅ Budget check yapıldı");
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ Budget check failed: {ex.Message}"); }

                // ── Başarı mesajı ─────────────────────────────────────────
                if (!string.IsNullOrEmpty(displayCurrency) && displayCurrency != "TRY")
                    TempData["Success"] = $"{model.Amount} {displayCurrency} = ₺{displayAmount:N2} olarak eklendi!";
                else
                    TempData["Success"] = "İşlem başarıyla eklendi!";

                return RedirectToAction("Index");
            }

            TempData["Error"] = "İşlem eklenemedi";
            var categories = await _apiService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.GetAsync($"/api/transaction/{id}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var transaction = JsonSerializer.Deserialize<TransactionViewModel>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Kategori nested object'ten düzleştir
                if (transaction?.Category != null)
                {
                    transaction.CategoryId = transaction.Category.CategoryId;
                    transaction.CategoryName = transaction.Category.Name;
                    transaction.CategoryIcon = transaction.Category.Icon;
                }

                var categories = await _apiService.GetCategoriesAsync();
                ViewBag.Categories = categories;
                return View(transaction);
            }

            TempData["Error"] = "İşlem bulunamadı";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TransactionViewModel model)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();

            var transactionData = new
            {
                amount = model.Amount,
                type = model.Type,
                date = model.Date,
                description = model.Description ?? string.Empty,
                categoryId = model.CategoryId,
                currency = string.IsNullOrEmpty(model.Currency) ? "TRY" : model.Currency
            };

            var json = JsonSerializer.Serialize(transactionData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/transaction/{id}", content);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    await _apiService.CheckBudgetAsync();
                    Console.WriteLine("✅ Budget check yapıldı (edit)");
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ Budget check failed: {ex.Message}"); }

                TempData["Success"] = "İşlem başarıyla güncellendi!";
                return RedirectToAction("Index");
            }

            TempData["Error"] = "İşlem güncellenemedi";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            var response = await client.DeleteAsync($"/api/transaction/{id}");

            if (response.IsSuccessStatusCode) TempData["Success"] = "İşlem başarıyla silindi!";
            else TempData["Error"] = "İşlem silinemedi";

            return RedirectToAction("Index");
        }
        public async Task<IActionResult> Export(int? month, int? year, string? period)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();
            string url = "/api/transaction/export";
            var queryParams = new List<string>();

            if (month.HasValue && year.HasValue)
            {
                queryParams.Add($"month={month}");
                queryParams.Add($"year={year}");
            }
            else if (!string.IsNullOrEmpty(period))
            {
                var now = DateTime.Now;
                switch (period)
                {
                    case "weekly":
                        queryParams.Add($"startDate={now.AddDays(-7):yyyy-MM-dd}");
                        break;
                    case "monthly":
                        queryParams.Add($"month={now.Month}");
                        queryParams.Add($"year={now.Year}");
                        break;
                    case "6months":
                        queryParams.Add($"startDate={now.AddMonths(-6):yyyy-MM-dd}");
                        break;
                    case "yearly":
                        queryParams.Add($"year={now.Year}");
                        break;
                }
            }

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Excel dosyası oluşturulamadı.";
                return RedirectToAction("Index");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            string fileName = $"FinAware_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
    }