using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAware.MVC.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public SubscriptionController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
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

        //  Fiyatlandırma sayfası 
        public async Task<IActionResult> Index()
        {
            try
            {
                var client = CreateApiClient();
                var response = await client.GetAsync("/api/subscription/my-plan");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    ViewBag.MyPlan = JsonSerializer.Deserialize<JsonElement>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }

            return View();
        }

        //  Ödeme başlat 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string plan)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            try
            {
                var client = CreateApiClient();
                var json = JsonSerializer.Serialize(new { plan });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/subscription/initialize", content);

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Ödeme başlatılamadı.";
                    return RedirectToAction("Index");
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                ViewBag.CheckoutFormContent = result.GetProperty("checkoutFormContent").GetString();
                ViewBag.Plan = plan;
                return View("Checkout");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Subscribe error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu.";
                return RedirectToAction("Index");
            }
        }

        // iyzico callback (ödeme sonucu)
        [HttpPost]
        public async Task<IActionResult> Callback(string token)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiBase = _configuration["ApiBaseUrl"] ?? "https://finaware-uq2x.onrender.com";
                client.BaseAddress = new Uri(apiBase);

                var content = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("token", token)
                });
                var response = await client.PostAsync("/api/subscription/callback", content);
                var text = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                bool success = result.TryGetProperty("success", out var s) && s.GetBoolean();
                ViewBag.Success = success;
                ViewBag.Plan = result.TryGetProperty("plan", out var p) ? p.GetString() : "";
                return View("CallbackResult");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Callback error: {ex.Message}");
                ViewBag.Success = false;
                return View("CallbackResult");
            }
        }

        // İptal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel()
        {
            try
            {
                var client = CreateApiClient();
                var response = await client.PostAsync("/api/subscription/cancel", null);
                if (response.IsSuccessStatusCode)
                    TempData["Success"] = "Aboneliğin iptal edildi.";
                else
                    TempData["Error"] = "İptal işlemi başarısız.";
            }
            catch { TempData["Error"] = "Bir hata oluştu."; }

            return RedirectToAction("Index");
        }
    }
}