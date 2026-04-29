using System.ComponentModel.DataAnnotations;

namespace FinAware.API.Models
{
    public class Budget
    {
        [Key]
        public int BudgetId { get; set; }

        [Required]
        public int UserId { get; set; }
        public int? CategoryId { get; set; }
        [Required]
        public decimal LimitAmount { get; set; }
        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public User User { get; set; } = null!;
        public Category? Category { get; set; }
    }
}