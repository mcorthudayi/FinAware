namespace FinAware.MVC.Models.ViewModels
{
    public class BudgetViewModel
    {
        public int BudgetId { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = "Genel Bütçe";
        public string CategoryIcon { get; set; } = "💰";
        public decimal LimitAmount { get; set; }
        public decimal Spent { get; set; }
        public decimal Remaining { get; set; }
        public double Percentage { get; set; }
        public bool IsOver { get; set; }
        public bool IsWarning { get; set; }
        public bool IsInfo { get; set; } 
        public int Month { get; set; }
        public int Year { get; set; }
    }

    public class BudgetPageViewModel
    {
        public List<BudgetViewModel> Budgets { get; set; } = new();
        public decimal TotalExpense { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public List<CategoryViewModel> Categories { get; set; } = new();
    }
}