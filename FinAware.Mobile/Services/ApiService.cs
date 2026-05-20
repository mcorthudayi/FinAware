using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using FinAware.Mobile.Models;

namespace FinAware.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public ApiService(AuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(Constants.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    private async Task AddAuthHeaderAsync()
    {
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    private StringContent ToJson(object obj)
        => new(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

    // AUTH
    public async Task<ApiResult<LoginResponse>> LoginAsync(string email, string password)
    {
        try
        {
            var content = ToJson(new { email, password });
            var response = await _httpClient.PostAsync("api/auth/login", content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<LoginResponse>(json);
                return ApiResult<LoginResponse>.Ok(data!);
            }

            var error = JsonConvert.DeserializeObject<ErrorResponse>(json);
            return ApiResult<LoginResponse>.Fail(error?.Message ?? "Giriş başarısız");
        }
        catch (Exception ex)
        {
            return ApiResult<LoginResponse>.Fail($"Bağlantı hatası: {ex.Message}");
        }
    }

    public async Task<ApiResult<string>> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var content = ToJson(new { username, email, password });
            var response = await _httpClient.PostAsync("api/auth/register", content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return ApiResult<string>.Ok("Kayıt başarılı!");

            var error = JsonConvert.DeserializeObject<ErrorResponse>(json);
            return ApiResult<string>.Fail(error?.Message ?? "Kayıt başarısız");
        }
        catch (Exception ex)
        {
            return ApiResult<string>.Fail($"Bağlantı hatası: {ex.Message}");
        }
    }

    // DASHBOARD 
    public async Task<ApiResult<List<TransactionModel>>> GetTransactionsAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/transaction");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<List<TransactionModel>>(json);
                return ApiResult<List<TransactionModel>>.Ok(data ?? new());
            }

            return ApiResult<List<TransactionModel>>.Fail("İşlemler alınamadı");
        }
        catch (Exception ex)
        {
            return ApiResult<List<TransactionModel>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> CreateTransactionAsync(CreateTransactionRequest req)
    {
        try
        {
            await AddAuthHeaderAsync();
            var content = ToJson(req);
            var response = await _httpClient.PostAsync("api/transaction", content);
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("İşlem eklenemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> DeleteTransactionAsync(int id)
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/transaction/{id}");
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("İşlem silinemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    // KATEGORİLER 
    public async Task<ApiResult<List<CategoryModel>>> GetCategoriesAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/category");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<List<CategoryModel>>(json);
                return ApiResult<List<CategoryModel>>.Ok(data ?? new());
            }

            return ApiResult<List<CategoryModel>>.Fail("Kategoriler alınamadı");
        }
        catch (Exception ex)
        {
            return ApiResult<List<CategoryModel>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> CreateCategoryAsync(string name, string type, string icon, string color)
    {
        try
        {
            await AddAuthHeaderAsync();
            var content = ToJson(new { name, type, icon, color });
            var response = await _httpClient.PostAsync("api/category", content);
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Kategori eklenemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> DeleteCategoryAsync(int id)
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/category/{id}");
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Kategori silinemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    // BÜTÇE
    public async Task<ApiResult<BudgetPageModel>> GetBudgetsAsync(int month, int year)
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"api/budget?month={month}&year={year}");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<BudgetPageModel>(json);
                return ApiResult<BudgetPageModel>.Ok(data ?? new());
            }

            return ApiResult<BudgetPageModel>.Fail("Bütçeler alınamadı");
        }
        catch (Exception ex)
        {
            return ApiResult<BudgetPageModel>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> CreateBudgetAsync(int? categoryId, decimal limitAmount, int month, int year)
    {
        try
        {
            await AddAuthHeaderAsync();
            var content = ToJson(new { categoryId, limitAmount, month, year });
            var response = await _httpClient.PostAsync("api/budget", content);
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Bütçe eklenemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> DeleteBudgetAsync(int id)
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/budget/{id}");
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Bütçe silinemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    // BİRİKİM 
    public async Task<ApiResult<List<SavingModel>>> GetSavingsAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/saving");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<List<SavingModel>>(json);
                return ApiResult<List<SavingModel>>.Ok(data ?? new());
            }

            return ApiResult<List<SavingModel>>.Fail("Birikimler alınamadı");
        }
        catch (Exception ex)
        {
            return ApiResult<List<SavingModel>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> CreateSavingAsync(string goalName, decimal targetAmount, DateTime? targetDate, string icon, string color)
    {
        try
        {
            await AddAuthHeaderAsync();
            var content = ToJson(new { goalName, targetAmount, targetDate, icon, color });
            var response = await _httpClient.PostAsync("api/saving", content);
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Birikim eklenemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> AddSavingAmountAsync(int id, decimal amount)
    {
        try
        {
            await AddAuthHeaderAsync();
            var content = ToJson(new { amount, currency = "TRY", date = DateTime.Now });
            var response = await _httpClient.PostAsync($"api/saving/{id}/add", content);
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Tutar eklenemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> DeleteSavingAsync(int id)
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/saving/{id}");
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Birikim silinemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    //  PROFİL
    public async Task<ApiResult<ProfileModel>> GetProfileAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/user/profile");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<ProfileModel>(json);
                return ApiResult<ProfileModel>.Ok(data!);
            }

            return ApiResult<ProfileModel>.Fail("Profil alınamadı");
        }
        catch (Exception ex)
        {
            return ApiResult<ProfileModel>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<bool>> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            await AddAuthHeaderAsync();
            var content = ToJson(new { currentPassword, newPassword });
            var response = await _httpClient.PostAsync("api/user/change-password", content);
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Ok(true)
                : ApiResult<bool>.Fail("Şifre değiştirilemedi");
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    // BİLDİRİMLER 
    public async Task<ApiResult<int>> GetUnreadCountAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/notification/unread-count");
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return ApiResult<int>.Ok(JsonConvert.DeserializeObject<int>(json));

            return ApiResult<int>.Ok(0);
        }
        catch
        {
            return ApiResult<int>.Ok(0);
        }
    }
}