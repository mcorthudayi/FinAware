using System.ComponentModel.DataAnnotations;

namespace FinAware.API.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = "Expense";

        [StringLength(10)]
        public string Icon { get; set; } = "📁";

        [StringLength(10)]
        public string Color { get; set; } = "#4DB6AC";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User User { get; set; } = null!;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}