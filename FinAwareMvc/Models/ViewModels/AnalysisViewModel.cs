namespace FinAware.MVC.Models.ViewModels
{
    public class AnalysisViewModel
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal Balance { get; set; }
        public List<TransactionViewModel> Transactions { get; set; } = new();
        public Dictionary<string, decimal> CategoryExpenses { get; set; } = new();
        public string Period { get; set; } = "monthly"; 
    }
}