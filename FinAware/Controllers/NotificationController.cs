using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinAware.API.Data;
using FinAware.API.DTOs;
using FinAware.API.Models;
using System.Security.Claims;

namespace FinAware.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/notification endpointi, kullanıcının bildirimlerini listeler
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    Icon = n.Icon,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // GET: api/notification/unread-count endpointi, kullanıcının okunmamış bildirim sayısını döner
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var count = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();

            return Ok(count);
        }

        // POST: api/notification endpointi, yeni bir bildirim oluşturur
        [HttpPost]
        public async Task<ActionResult> CreateNotification([FromBody] CreateNotificationDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var notification = new Notification
            {
                UserId = userId,
                Title = dto.Title,
                Message = dto.Message,
                Type = dto.Type,
                Icon = dto.Icon,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bildirim oluşturuldu" });
        }

        // PUT: api/notification/{id}/read endpointi, belirli bir bildirimi okundu olarak işaretler
        [HttpPut("{id}/read")]
        public async Task<ActionResult> MarkAsRead(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bildirim okundu olarak işaretlendi" });
        }

        // PUT: api/notification/mark-all-read endpointi, tüm bildirimleri okundu olarak işaretler
        [HttpPut("mark-all-read")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ForEachAsync(n => n.IsRead = true);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tüm bildirimler okundu" });
        }

        // DELETE: api/notification/{id} endpointi, belirli bir bildirimi siler
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNotification(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bildirim silindi" });
        }
    }
}