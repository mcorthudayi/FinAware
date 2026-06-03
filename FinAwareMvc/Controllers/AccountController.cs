using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinAware.MVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient CreateAuthClient()
        {
            var client = _httpClientFactory.CreateClient();
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiBaseUrl = config["ApiBaseUrl"] ?? "https://localhost:7061";
            client.BaseAddress = new Uri(apiBaseUrl);
            var token = HttpContext.Session.GetString("AuthToken");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            var client = CreateAuthClient();

            //  Kullanıcı profili
            try
            {
                var response = await client.GetAsync("/api/user/profile");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var userProfile = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (userProfile != null)
                    {
                        ViewBag.Username = userProfile.ContainsKey("username")
                            ? userProfile["username"].GetString()
                            : HttpContext.Session.GetString("Username");
                        ViewBag.Email = userProfile.ContainsKey("email")
                            ? userProfile["email"].GetString()
                            : HttpContext.Session.GetString("Email");
                        ViewBag.EmailNotificationsEnabled = userProfile.ContainsKey("emailNotificationsEnabled")
                            && userProfile["emailNotificationsEnabled"].GetBoolean();
                        ViewBag.TelegramLinked = userProfile.ContainsKey("telegramLinked")
                            && userProfile["telegramLinked"].GetBoolean();
                        ViewBag.TelegramLinkedAt = userProfile.ContainsKey("telegramLinkedAt")
                            && userProfile["telegramLinkedAt"].ValueKind != JsonValueKind.Null
                            ? userProfile["telegramLinkedAt"].GetString() : null;

                        if (userProfile.ContainsKey("profilePhoto")
                            && userProfile["profilePhoto"].ValueKind != JsonValueKind.Null)
                        {
                            var photoFileName = userProfile["profilePhoto"].GetString();
                            if (!string.IsNullOrEmpty(photoFileName))
                            {
                                var apiBaseUrl = HttpContext.RequestServices
                                    .GetRequiredService<IConfiguration>()["ApiBaseUrl"] ?? "https://localhost:7061";
                                var photoUrl = $"{apiBaseUrl}/api/user/photo/{photoFileName}";
                                ViewBag.ProfilePhoto = photoUrl;
                                HttpContext.Session.SetString("ProfilePhoto", photoUrl);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ Get profile error: {ex.Message}"); }

            // Abonelik planı 
            try
            {
                var subRes = await client.GetAsync("/api/subscription/my-plan");
                if (subRes.IsSuccessStatusCode)
                {
                    var subJson = await subRes.Content.ReadAsStringAsync();
                    var subData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(subJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (subData != null)
                    {
                        ViewBag.SubscriptionPlan = subData.ContainsKey("plan") ? subData["plan"].GetString() : "Free";
                        ViewBag.SubscriptionExpiry = subData.ContainsKey("expiry") ? subData["expiry"].GetString() : null;
                        ViewBag.OcrUsage = subData.ContainsKey("ocrUsage") ? subData["ocrUsage"].GetInt32() : 0;
                        ViewBag.ArisUsage = subData.ContainsKey("arisUsage") ? subData["arisUsage"].GetInt32() : 0;
                        ViewBag.OcrLimit = subData.ContainsKey("ocrLimit") ? subData["ocrLimit"].GetInt32() : 0;
                        ViewBag.ArisLimit = subData.ContainsKey("arisLimit") ? subData["arisLimit"].GetInt32() : 0;

                        Console.WriteLine($"✅ Subscription loaded: {ViewBag.SubscriptionPlan}");
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"⚠️ SubPlan error: {ex.Message}"); }

            // Default değer — sub plan hiç yüklenmediyse
            if (ViewBag.SubscriptionPlan == null)
                ViewBag.SubscriptionPlan = "Free";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(IFormFile photo)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            if (photo == null || photo.Length == 0) { TempData["Error"] = "Lütfen bir fotoğraf seçin"; return RedirectToAction("Index"); }
            if (photo.Length > 5 * 1024 * 1024) { TempData["Error"] = "Dosya çok büyük! Maksimum 5MB"; return RedirectToAction("Index"); }

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(photo.ContentType.ToLower())) { TempData["Error"] = "Sadece JPG, PNG veya WEBP!"; return RedirectToAction("Index"); }

            try
            {
                var client = CreateAuthClient();
                using var formData = new MultipartFormDataContent();
                using var fileContent = new StreamContent(photo.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(photo.ContentType);
                formData.Add(fileContent, "file", photo.FileName);

                var response = await client.PostAsync("/api/user/upload-profile-photo", formData);
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseText);
                    if (result != null && result.ContainsKey("photoUrl"))
                    {
                        var photoUrl = result["photoUrl"].GetString();
                        if (!string.IsNullOrEmpty(photoUrl))
                            HttpContext.Session.SetString("ProfilePhoto", $"https://localhost:7061{photoUrl}");
                    }
                    TempData["Success"] = "Profil fotoğrafı başarıyla güncellendi!";
                }
                else TempData["Error"] = "Fotoğraf yüklenemedi";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Upload photo error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string username, string email)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            try
            {
                var client = CreateAuthClient();
                var json = JsonSerializer.Serialize(new { username, email });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PutAsync("/api/user/update-profile", content);

                if (response.IsSuccessStatusCode)
                {
                    HttpContext.Session.SetString("Username", username);
                    HttpContext.Session.SetString("Email", email);
                    TempData["Success"] = "Profil bilgileri başarıyla güncellendi!";
                }
                else TempData["Error"] = "Profil güncellenemedi";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Update profile error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            if (newPassword != confirmPassword) { TempData["Error"] = "Yeni şifreler eşleşmiyor!"; return RedirectToAction("Index"); }

            try
            {
                var client = CreateAuthClient();
                var json = JsonSerializer.Serialize(new { currentPassword, newPassword });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/user/change-password", content);

                if (response.IsSuccessStatusCode) TempData["Success"] = "Şifre başarıyla değiştirildi!";
                else TempData["Error"] = "Şifre değiştirilemedi. Mevcut şifrenizi kontrol edin.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Change password error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEmailNotifications()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            try
            {
                var client = CreateAuthClient();
                var response = await client.PostAsync("/api/user/toggle-email-notifications", null);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        await response.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    TempData["Success"] = result?["message"].GetString() ?? "Bildirim tercihi güncellendi";
                }
                else TempData["Error"] = "Bildirim tercihi güncellenemedi";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Toggle notifications error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateTelegramLink()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            try
            {
                var client = CreateAuthClient();
                var response = await client.PostAsync("/api/auth/generate-link-token", null);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        await response.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    TempData["TelegramDeepLink"] = result?["deepLink"].GetString();
                    TempData["TelegramLinkToken"] = result?["linkToken"].GetString();
                    TempData["Success"] = "Bağlantı linki oluşturuldu! Aşağıdaki butona tıklayın.";
                }
                else TempData["Error"] = "Link oluşturulamadı";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Generate telegram link error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlinkTelegram()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            try
            {
                var client = CreateAuthClient();
                var response = await client.PostAsync("/api/auth/unlink-telegram", null);

                if (response.IsSuccessStatusCode) TempData["Success"] = "Telegram bağlantısı kaldırıldı.";
                else TempData["Error"] = "Bağlantı kaldırılamadı";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unlink telegram error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(string password)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            try
            {
                var client = CreateAuthClient();
                var json = JsonSerializer.Serialize(new { password });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Delete, "/api/user/delete-account") { Content = content };
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    HttpContext.Session.Clear();
                    TempData["Success"] = "Hesabınız başarıyla silindi.";
                    return RedirectToAction("Login", "Auth");
                }

                var error = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    await response.Content.ReadAsStringAsync());
                TempData["Error"] = error?["message"].GetString() ?? "Hesap silinemedi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Delete account error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu";
                return RedirectToAction("Index");
            }
        }
    }
}