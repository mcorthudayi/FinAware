using FinAware.MVC.Models;
using FinAware.MVC.Models.ViewModels;
using FinAware.MVC.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FinAware.MVC.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IApiService _apiService;
        private readonly IHttpClientFactory _httpClientFactory;

        public DashboardController(IApiService apiService, IHttpClientFactory httpClientFactory)
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

        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AuthToken")))
                return RedirectToAction("Login", "Auth");

            var model = new DashboardViewModel
            {
                TotalIncome = 0,
                TotalExpense = 0,
                TotalBalance = 0,
                RecentTransactions = new List<TransactionViewModel>(),
                RecentSavings = new List<SavingViewModel>()
            };

            try
            {
                var transactions = await _apiService.GetTransactionsAsync();
                if (transactions != null && transactions.Any())
                {
                    model.RecentTransactions = transactions.Take(5).ToList();
                    model.TotalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                    model.TotalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                    model.TotalBalance = model.TotalIncome - model.TotalExpense;
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ Dashboard transaction error: {ex.Message}"); }

            try
            {
                var client = CreateAuthClient();
                var savingResponse = await client.GetAsync("/api/saving");
                if (savingResponse.IsSuccessStatusCode)
                {
                    var savingJson = await savingResponse.Content.ReadAsStringAsync();
                    var savings = JsonSerializer.Deserialize<List<SavingViewModel>>(savingJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (savings != null)
                        model.RecentSavings = savings.Take(3).ToList();
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ Dashboard saving error: {ex.Message}"); }

            return View(model);
        }
    }
}