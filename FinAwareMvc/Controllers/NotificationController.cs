using Microsoft.AspNetCore.Mvc;
using FinAware.MVC.Services;
using FinAware.MVC.Filters;

namespace FinAware.MVC.Controllers
{
    [AuthorizeFilter]
    public class NotificationController : Controller
    {
        private readonly IApiService _apiService;

        public NotificationController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            var notifications = await _apiService.GetNotificationsAsync();
            return View(notifications);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _apiService.GetUnreadCountAsync();
            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var notifications = await _apiService.GetNotificationsAsync();
            var recent = notifications.Take(5).ToList();
            return Json(recent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var success = await _apiService.MarkAsReadAsync(id);

            if (success)
            {
                TempData["Success"] = "Bildirim okundu olarak işaretlendi";
            }
            else
            {
                TempData["Error"] = "İşlem başarısız oldu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var success = await _apiService.MarkAllAsReadAsync();

            if (success)
            {
                TempData["Success"] = "Tüm bildirimler okundu olarak işaretlendi";
            }
            else
            {
                TempData["Error"] = "İşlem başarısız oldu";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _apiService.DeleteNotificationAsync(id);

            if (success)
            {
                TempData["Success"] = "Bildirim silindi";
            }
            else
            {
                TempData["Error"] = "Bildirim silinemedi";
            }

            return RedirectToAction("Index");
        }
    }
}