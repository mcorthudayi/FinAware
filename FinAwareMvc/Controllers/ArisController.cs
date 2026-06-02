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

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ArisChatRequest request)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return Unauthorized();

            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiBase = _configuration["ApiBaseUrl"] ?? "https://finaware-uq2x.onrender.com";
                client.BaseAddress = new Uri(apiBase);
                var token = HttpContext.Session.GetString("AuthToken");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var json = JsonSerializer.Serialize(new { message = request.Message });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/aris/chat", content);
                var responseText = await response.Content.ReadAsStringAsync();

                return Content(responseText, "application/json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ARİS MVC error: {ex.Message}");
                return Ok(new { reply = "Bağlantı hatası, biraz sonra tekrar dene." });
            }
        }
    }

    public class ArisChatRequest
    {
        public string Message { get; set; } = "";
    }
}