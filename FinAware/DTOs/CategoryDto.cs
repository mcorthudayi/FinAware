using System.ComponentModel.DataAnnotations;

namespace FinAware.API.DTOs
{
    public class CategoryDto
    {
        public int CategoryId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = "Expense";

        [StringLength(10)]
        public string Icon { get; set; } = "📁";

        [StringLength(10)]
        public string Color { get; set; } = "#4DB6AC";
    }
}