namespace FinAware.MVC.Models
{
    public class SavingViewModel
    {
        public int SavingId { get; set; }
        public string GoalName { get; set; } = "";
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime? TargetDate { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public double Progress { get; set; }
    }
}