using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FinAware.API.Data;
using FinAware.API.DTOs;
using FinAware.API.Models;

namespace FinAware.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim!);
        }

        // GET: api/category endpointi, kullanıcının kategorilerini döner
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var userId = GetUserId();

            var categories = await _context.Categories
                .Where(c => c.UserId == userId)
                .Select(c => new CategoryDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Type = c.Type,
                    Icon = c.Icon,
                    Color = c.Color
                })
                .ToListAsync();

            return Ok(categories);
        }

        // POST: api/category endpointi, yeni kategori oluşturur
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();

            var category = new Category
            {
                UserId = userId,
                Name = dto.Name,
                Type = dto.Type,
                Icon = dto.Icon,
                Color = dto.Color,
                CreatedAt = DateTime.Now
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            dto.CategoryId = category.CategoryId;
            return CreatedAtAction(nameof(GetCategories), new { id = category.CategoryId }, dto);
        }

        // DELETE: api/category/5 endpointi, belirtilen id'ye sahip kategoriyi siler
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var userId = GetUserId();

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);

            if (category == null)
                return NotFound(new { message = "Kategori bulunamadı" });

            // Bağlı işlem var mı kontrol et (DeleteBehavior.Restrict olduğu için şart)
            var hasTransactions = await _context.Transactions
                .AnyAsync(t => t.CategoryId == id);

            if (hasTransactions)
                return BadRequest(new { message = "Bu kategoriye ait işlemler var. Önce işlemleri silin veya başka kategoriye taşıyın." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kategori silindi" });
        }
    }
}