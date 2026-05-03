using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using FinAware.MVC.Models.ViewModels;

namespace FinAware.MVC.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
        }

        private void AddAuthorizationHeader()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("AuthToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<LoginResponse?> LoginAsync(string email, string password)
        {
            try
            {
                var payload = new { email, password };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseJson);
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                        return loginResponse;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApiService] ❌ Login Exception: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RegisterAsync(string username, string email, string password)
        {
            try
            {
                var payload = new { username, email, password };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/auth/register", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApiService] ❌ Register Exception: {ex.Message}");
                return false;
            }
        }

        public async Task<List<TransactionViewModel>> GetTransactionsAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync("api/transaction");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var transactions = JsonConvert.DeserializeObject<List<TransactionViewModel>>(json) ?? new();

                    foreach (var t in transactions)
                    {
                        if (t.Category != null)
                        {
                            t.CategoryName = t.Category.Name;
                            t.CategoryIcon = t.Category.Icon;
                            t.CategoryId = t.Category.CategoryId;
                        }
                    }

                    return transactions;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get transactions error: {ex.Message}");
            }

            return new List<TransactionViewModel>();
        }

        public async Task<bool> CreateTransactionAsync(TransactionViewModel transaction)
        {
            try
            {
                AddAuthorizationHeader();
                var dto = new
                {
                    categoryId = transaction.CategoryId,
                    amount = transaction.Amount,
                    type = transaction.Type,
                    description = transaction.Description ?? string.Empty,
                    date = transaction.Date
                };
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/transaction", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create transaction error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteTransactionAsync(int id)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"api/transaction/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete transaction error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<CategoryViewModel>> GetCategoriesAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync("api/category");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<CategoryViewModel>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get categories error: {ex.Message}");
            }

            return new List<CategoryViewModel>();
        }

        public async Task<bool> CreateCategoryAsync(CategoryViewModel category)
        {
            try
            {
                AddAuthorizationHeader();
                var dto = new { name = category.Name, type = category.Type, icon = category.Icon ?? "📁", color = category.Color ?? "#4DB6AC" };
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/category", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create category error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"api/category/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete category error: {ex.Message}");
                return false;
            }
        }

        public async Task<DashboardViewModel> GetDashboardSummaryAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var transactions = await GetTransactionsAsync();
                return new DashboardViewModel
                {
                    TotalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount),
                    TotalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                    TotalBalance = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount) - transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                    RecentTransactions = transactions.OrderByDescending(t => t.Date).Take(5).ToList()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get dashboard error: {ex.Message}");
                return new DashboardViewModel();
            }
        }

        public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            try
            {
                AddAuthorizationHeader();
                var payload = new { currentPassword, newPassword };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/user/change-password", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Change password error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<NotificationViewModel>> GetNotificationsAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync("api/notification");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<NotificationViewModel>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get notifications error: {ex.Message}");
            }

            return new List<NotificationViewModel>();
        }

        public async Task<int> GetUnreadCountAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync("api/notification/unread-count");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<int>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread count error: {ex.Message}");
            }

            return 0;
        }

        public async Task<bool> MarkAsReadAsync(int notificationId)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.PutAsync($"api/notification/{notificationId}/read", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mark as read error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MarkAllAsReadAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.PutAsync("api/notification/mark-all-read", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mark all as read error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"api/notification/{notificationId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete notification error: {ex.Message}");
                return false;
            }
        }

        public async Task<BudgetPageViewModel> GetBudgetsAsync(int month, int year)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync($"api/budget?month={month}&year={year}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(json);

                    var model = new BudgetPageViewModel
                    {
                        Month = month,
                        Year = year,
                        TotalExpense = (decimal)(data?.totalExpense ?? 0)
                    };

                    if (data?.budgets != null)
                    {
                        var budgetsJson = JsonConvert.SerializeObject(data.budgets);
                        model.Budgets = JsonConvert.DeserializeObject<List<BudgetViewModel>>(budgetsJson) ?? new List<BudgetViewModel>();
                    }

                    model.Categories = await GetCategoriesAsync();
                    return model;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get budgets error: {ex.Message}");
            }

            return new BudgetPageViewModel
            {
                Month = month,
                Year = year,
                Categories = await GetCategoriesAsync()
            };
        }

        public async Task<bool> CreateBudgetAsync(int? categoryId, decimal limitAmount, int month, int year)
        {
            try
            {
                AddAuthorizationHeader();
                var dto = new { categoryId, limitAmount, month, year };
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/budget", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create budget error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteBudgetAsync(int id)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"api/budget/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete budget error: {ex.Message}");
                return false;
            }
        }

        public async Task CheckBudgetAsync()
        {
            try
            {
                AddAuthorizationHeader();
                await _httpClient.PostAsync("api/budget/check", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Check budget error: {ex.Message}");
            }
        }
    public async Task<dynamic?> GetAdminStatsAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync("api/admin/stats");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<dynamic>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin stats error: {ex.Message}");
            }
            return null;
        }

        public async Task<List<dynamic>> GetAdminUsersAsync()
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync("api/admin/users");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<dynamic>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin users error: {ex.Message}");
            }
            return new List<dynamic>();
        }

        public async Task<bool> AdminFreezeUserAsync(int id)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.PostAsync($"api/admin/users/{id}/freeze", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Freeze user error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AdminUnfreezeUserAsync(int id)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.PostAsync($"api/admin/users/{id}/unfreeze", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unfreeze user error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AdminDeleteUserAsync(int id)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"api/admin/users/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin delete user error: {ex.Message}");
                return false;
            }
        }
    }
    }