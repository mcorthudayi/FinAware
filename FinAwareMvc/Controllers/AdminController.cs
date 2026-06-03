using Microsoft.AspNetCore.Mvc;
using FinAware.MVC.Services;
using FinAwareMvc.Filters;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAwareMvc.Controllers
{
    [AdminAuthorize]
    public class AdminController : Controller
    {
        private readonly IApiService _apiService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AdminController(
            IApiService apiService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _apiService = apiService;
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

        public async Task<IActionResult> Index()
        {
            ViewBag.Stats = await _apiService.GetAdminStatsAsync();
            ViewBag.Users = await _apiService.GetAdminUsersAsync();

            // Subscription istatistikleri
            try
            {
                var client = CreateApiClient();
                var res = await client.GetAsync("/api/admin/subscription-stats");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var subEl = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                    ViewBag.SubFree = subEl.TryGetProperty("free", out var f) ? f.GetInt32() : 0;
                    ViewBag.SubGold = subEl.TryGetProperty("gold", out var g) ? g.GetInt32() : 0;
                    ViewBag.SubPlatinum = subEl.TryGetProperty("platinum", out var p) ? p.GetInt32() : 0;
                }
            }
            catch { }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FreezeUser(int id)
        {
            await _apiService.AdminFreezeUserAsync(id);
            TempData["Success"] = "Kullanıcı donduruldu.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UnfreezeUser(int id)
        {
            await _apiService.AdminUnfreezeUserAsync(id);
            TempData["Success"] = "Kullanıcı aktif edildi.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            await _apiService.AdminDeleteUserAsync(id);
            TempData["Success"] = "Kullanıcı silindi.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ChangePlan(int id, string plan, int months = 1)
        {
            try
            {
                var client = CreateApiClient();
                var payload = new { plan, months };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"/api/admin/users/{id}/change-plan", content);

                if (response.IsSuccessStatusCode)
                    TempData["Success"] = $"Plan başarıyla güncellendi: {plan}";
                else
                    TempData["Error"] = "Plan güncellenemedi.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ChangePlan error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu.";
            }

            return RedirectToAction("Index");
        }
    }
}