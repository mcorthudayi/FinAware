using Microsoft.AspNetCore.Mvc;
using FinAware.MVC.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAware.MVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly IApiService _apiService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuthController(IApiService apiService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _apiService = apiService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_configuration["ApiBaseUrl"] ?? "https://localhost:7061");
            return client;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Index", "Dashboard");

            var rememberToken = Request.Cookies["FinAware_RememberToken"];
            if (!string.IsNullOrEmpty(rememberToken))
            {
                HttpContext.Session.SetString("AuthToken", rememberToken);

                try
                {
                    var client = CreateClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rememberToken);

                    var profileResponse = await client.GetAsync("/api/user/profile");
                    if (profileResponse.IsSuccessStatusCode)
                    {
                        var json = await profileResponse.Content.ReadAsStringAsync();
                        var profile = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (profile != null)
                        {
                            if (profile.ContainsKey("username"))
                                HttpContext.Session.SetString("Username", profile["username"].GetString() ?? "");
                            if (profile.ContainsKey("email"))
                                HttpContext.Session.SetString("Email", profile["email"].GetString() ?? "");
                            if (profile.ContainsKey("profilePhoto") &&
                                profile["profilePhoto"].ValueKind != JsonValueKind.Null)
                            {
                                var photoFileName = profile["profilePhoto"].GetString();
                                if (!string.IsNullOrEmpty(photoFileName))
                                {
                                    var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7061";
                                    HttpContext.Session.SetString("ProfilePhoto", $"{apiBaseUrl}/api/user/photo/{photoFileName}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Response.Cookies.Delete("FinAware_RememberToken");
                        HttpContext.Session.Clear();
                        return View();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Remember me restore failed: {ex.Message}");
                    Response.Cookies.Delete("FinAware_RememberToken");
                    HttpContext.Session.Clear();
                    return View();
                }

                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe = false)
        {
            try
            {
                var loginResponse = await _apiService.LoginAsync(email, password);

                if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                {
                    HttpContext.Session.SetString("AuthToken", loginResponse.Token);
                    HttpContext.Session.SetString("Username", loginResponse.Username);
                    HttpContext.Session.SetString("Email", loginResponse.Email);

                    if (rememberMe)
                    {
                        Response.Cookies.Append("FinAware_RememberToken", loginResponse.Token, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.Strict,
                            Expires = DateTimeOffset.UtcNow.AddDays(30)
                        });
                    }

                    try
                    {
                        var client = CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.Token);

                        var profileResponse = await client.GetAsync("/api/user/profile");
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var json = await profileResponse.Content.ReadAsStringAsync();
                            var profile = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (profile != null && profile.ContainsKey("profilePhoto") &&
                                profile["profilePhoto"].ValueKind != JsonValueKind.Null)
                            {
                                var photoFileName = profile["profilePhoto"].GetString();
                                if (!string.IsNullOrEmpty(photoFileName))
                                {
                                    var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7061";
                                    HttpContext.Session.SetString("ProfilePhoto", $"{apiBaseUrl}/api/user/photo/{photoFileName}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Profile photo fetch failed: {ex.Message}");
                    }

                    return RedirectToAction("Index", "Dashboard");
                }

                ViewBag.ErrorMessage = "E-posta veya şifre hatalı!";

                if (loginResponse == null)
                {
                    try
                    {
                        var client = CreateClient();
                        var payload = new { email, password };
                        var json = JsonSerializer.Serialize(payload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await client.PostAsync("/api/auth/login", content);
                        var responseText = await response.Content.ReadAsStringAsync();
                        var responseJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseText,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (responseJson != null && responseJson.ContainsKey("emailNotVerified"))
                        {
                            ViewBag.ErrorMessage = "E-posta adresiniz doğrulanmamış!";
                            ViewBag.ShowResendButton = true;
                            ViewBag.UnverifiedEmail = email;
                        }
                        else if (responseJson != null && responseJson.ContainsKey("message"))
                        {
                            ViewBag.ErrorMessage = responseJson["message"].GetString();
                        }
                    }
                    catch { }
                }

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MVC] ❌ Login exception: {ex.Message}");
                ViewBag.ErrorMessage = "Bir hata oluştu. Lütfen tekrar deneyin.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Index", "Dashboard");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Şifreler eşleşmiyor!";
                return View();
            }

            try
            {
                var result = await _apiService.RegisterAsync(username, email, password);

                if (result)
                {
                    ViewBag.SuccessMessage = $"Kayıt başarılı! {email} adresine doğrulama maili gönderildi.";
                    ViewBag.ShowResendButton = true;
                    ViewBag.UnverifiedEmail = email;
                    return View("Login");
                }

                ViewBag.ErrorMessage = "Kayıt başarısız! E-posta veya kullanıcı adı zaten kullanılıyor olabilir.";
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MVC] ❌ Register exception: {ex.Message}");
                ViewBag.ErrorMessage = "Bir hata oluştu. Lütfen tekrar deneyin.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"/api/auth/verify-email?token={token}");
                var responseText = await response.Content.ReadAsStringAsync();
                var responseJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode)
                {
                    ViewBag.Success = true;
                    ViewBag.Message = responseJson?["message"].GetString() ?? "E-posta adresiniz doğrulandı!";
                }
                else
                {
                    ViewBag.Success = false;
                    ViewBag.Message = responseJson?["message"].GetString() ?? "Doğrulama başarısız.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Verify email error: {ex.Message}");
                ViewBag.Success = false;
                ViewBag.Message = "Doğrulama sırasında bir hata oluştu.";
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendVerification(string email)
        {
            try
            {
                var client = CreateClient();
                var payload = new { email };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/auth/resend-verification", content);

                if (response.IsSuccessStatusCode)
                    ViewBag.SuccessMessage = $"Doğrulama maili {email} adresine tekrar gönderildi.";
                else
                    ViewBag.ErrorMessage = "Mail gönderilemedi. Lütfen tekrar deneyin.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Resend verification error: {ex.Message}");
                ViewBag.ErrorMessage = "Bir hata oluştu.";
            }

            return View("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("FinAware_RememberToken");
            return RedirectToAction("Login");
        }
    }
}