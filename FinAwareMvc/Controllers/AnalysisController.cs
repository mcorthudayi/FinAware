using Microsoft.AspNetCore.Mvc;
using FinAware.MVC.Models.ViewModels;
using FinAware.MVC.Services;
using FinAware.MVC.Filters;

namespace FinAware.MVC.Controllers
{
    [AuthorizeFilter]
    public class AnalysisController : Controller
    {
        private readonly IApiService _apiService;

        public AnalysisController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index(string period = "monthly")
        {
            var allTransactions = await _apiService.GetTransactionsAsync();

            DateTime startDate = period switch
            {
                "weekly" => DateTime.Now.AddDays(-7),
                "monthly" => DateTime.Now.AddMonths(-1),
                "6months" => DateTime.Now.AddMonths(-6),
                "yearly" => DateTime.Now.AddYears(-1),
                _ => DateTime.Now.AddMonths(-1)
            };

            var filteredTransactions = allTransactions
                .Where(t => t.Date >= startDate)
                .OrderByDescending(t => t.Date)
                .ToList();

            var categoryExpenses = filteredTransactions
                .Where(t => t.Type == "Expense")
                .GroupBy(t => string.IsNullOrWhiteSpace(t.CategoryName) ? "Diğer" : t.CategoryName.Trim())
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .ToDictionary(x => x.Category, x => x.Total);

            var totalIncome = filteredTransactions
                .Where(t => t.Type == "Income")
                .Sum(t => t.Amount);

            var totalExpenses = filteredTransactions
                .Where(t => t.Type == "Expense")
                .Sum(t => t.Amount);

            var model = new AnalysisViewModel
            {
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                Balance = totalIncome - totalExpenses,
                Transactions = filteredTransactions,
                CategoryExpenses = categoryExpenses,
                Period = period
            };

            return View(model);
        }
    }
}