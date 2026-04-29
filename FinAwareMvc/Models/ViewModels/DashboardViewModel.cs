using FinAware.MVC.Models;

namespace FinAware.MVC.Models.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalBalance { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public List<TransactionViewModel> RecentTransactions { get; set; } = new();
        public List<SavingViewModel> RecentSavings { get; set; } = new();
    }

    public class LoginResponseViewModel
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}