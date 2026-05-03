// SMTP için gerekli using'ler - Azure deploy'da kullanılacak
// using System.Net;
// using System.Net.Mail;

namespace FinAware.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /*
        // SMTP İMPLEMENTASYONU - Azure deploy için
        private SmtpClient CreateSmtpClient()
        {
            var host = _configuration["EmailSettings:SmtpHost"]!;
            var port = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
            var user = _configuration["EmailSettings:SmtpUser"]!;
            var password = _configuration["EmailSettings:SmtpPassword"]!;

            return new SmtpClient(host, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
        }

        public async Task SendEmailAsyncSmtp(string toEmail, string subject, string body)
        {
            try
            {
                var fromEmail = _configuration["EmailSettings:FromEmail"]!;
                var fromName = _configuration["EmailSettings:FromName"] ?? "FinAware";

                using var client = CreateSmtpClient();
                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);
                await client.SendMailAsync(message);
                Console.WriteLine($"✅ Email sent to: {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Email send error: {ex.Message}");
                Console.WriteLine($"❌ Inner: {ex.InnerException?.Message}");
                throw;
            }
        }
        */

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            // Railway'de SMTP devre dışı - Azure deploy'da SMTP aktif edilecek
            Console.WriteLine($"📧 Email skipped (Railway): {toEmail} - {subject}");
            await Task.CompletedTask;
        }

        public async Task SendEmailVerificationAsync(string toEmail, string username, string verificationLink)
        {
            var subject = "FinAware - E-posta Adresinizi Doğrulayın";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #4DB6AC 0%, #26A69A 100%); padding: 30px; border-radius: 12px; text-align: center; margin-bottom: 30px;'>
        <h1 style='color: white; margin: 0;'>💰 FinAware</h1>
        <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Kişisel Finans Yönetimi</p>
    </div>
    <h2 style='color: #333;'>Merhaba, {username}! 👋</h2>
    <p style='color: #666;'>FinAware'e hoş geldiniz! Hesabınızı aktifleştirmek için e-posta adresinizi doğrulamanız gerekmektedir.</p>
    <div style='text-align: center; margin: 30px 0;'>
        <a href='{verificationLink}' style='background: linear-gradient(135deg, #4DB6AC 0%, #26A69A 100%); color: white; padding: 15px 40px; border-radius: 8px; text-decoration: none; font-size: 16px; font-weight: bold;'>
            ✅ E-postamı Doğrula
        </a>
    </div>
    <p style='color: #999; font-size: 14px;'>Bu link <strong>24 saat</strong> geçerlidir.</p>
    <p style='color: #999; font-size: 14px;'>Bu maili siz istemediyseniz dikkate almayın.</p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
    <p style='color: #ccc; font-size: 12px; text-align: center;'>FinAware - Finansal özgürlüğünüz için</p>
</body>
</html>";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string username)
        {
            var subject = "FinAware'e Hoş Geldiniz! 🎉";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #4DB6AC 0%, #26A69A 100%); padding: 30px; border-radius: 12px; text-align: center; margin-bottom: 30px;'>
        <h1 style='color: white; margin: 0;'>💰 FinAware</h1>
        <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Kişisel Finans Yönetimi</p>
    </div>
    <h2 style='color: #333;'>Hoş geldiniz, {username}! 🎉</h2>
    <p style='color: #666;'>E-posta adresiniz doğrulandı. Artık FinAware'i kullanmaya başlayabilirsiniz!</p>
    <div style='background: #E8F5E9; padding: 20px; border-radius: 8px; margin: 20px 0;'>
        <h4 style='color: #4CAF50; margin: 0 0 15px 0;'>✅ Hesabınız Aktif!</h4>
        <p style='margin: 5px 0;'>💰 İşlem eklemeye başlayabilirsiniz</p>
        <p style='margin: 5px 0;'>🎯 Birikim hedefleri belirleyebilirsiniz</p>
        <p style='margin: 5px 0;'>📊 Harcamalarınızı analiz edebilirsiniz</p>
    </div>
    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
    <p style='color: #ccc; font-size: 12px; text-align: center;'>FinAware - Finansal özgürlüğünüz için</p>
</body>
</html>";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendBudgetWarningAsync(string toEmail, string username, string categoryName, decimal percentage, decimal remaining)
        {
            var subject = $"⚠️ FinAware - {categoryName} Bütçe Uyarısı";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #4DB6AC 0%, #26A69A 100%); padding: 30px; border-radius: 12px; text-align: center; margin-bottom: 30px;'>
        <h1 style='color: white; margin: 0;'>💰 FinAware</h1>
    </div>
    <h2 style='color: #FF9800;'>⚠️ Bütçe Uyarısı</h2>
    <p>Merhaba <strong>{username}</strong>,</p>
    <div style='background: #FFF3E0; border-left: 4px solid #FF9800; padding: 20px; border-radius: 8px; margin: 20px 0;'>
        <h3 style='color: #FF9800; margin: 0 0 10px 0;'>{categoryName} Bütçeniz</h3>
        <p style='margin: 5px 0;'>Bütçenizin <strong>%{Math.Round(percentage, 0)}'ini</strong> kullandınız.</p>
        <p style='margin: 5px 0;'>Kalan bütçeniz: <strong>₺{remaining:N2}</strong></p>
    </div>
    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
    <p style='color: #ccc; font-size: 12px; text-align: center;'>FinAware - Finansal özgürlüğünüz için</p>
</body>
</html>";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendBudgetExceededAsync(string toEmail, string username, string categoryName, decimal limit, decimal spent)
        {
            var subject = $"🚨 FinAware - {categoryName} Bütçesi Aşıldı!";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #4DB6AC 0%, #26A69A 100%); padding: 30px; border-radius: 12px; text-align: center; margin-bottom: 30px;'>
        <h1 style='color: white; margin: 0;'>💰 FinAware</h1>
    </div>
    <h2 style='color: #F44336;'>🚨 Bütçe Aşıldı!</h2>
    <p>Merhaba <strong>{username}</strong>,</p>
    <div style='background: #FFEBEE; border-left: 4px solid #F44336; padding: 20px; border-radius: 8px; margin: 20px 0;'>
        <h3 style='color: #F44336; margin: 0 0 10px 0;'>{categoryName} Bütçeniz Aşıldı!</h3>
        <p style='margin: 5px 0;'>Belirlediğiniz limit: <strong>₺{limit:N2}</strong></p>
        <p style='margin: 5px 0;'>Toplam harcamanız: <strong>₺{spent:N2}</strong></p>
        <p style='margin: 5px 0;'>Aşım miktarı: <strong style='color: #F44336;'>₺{(spent - limit):N2}</strong></p>
    </div>
    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
    <p style='color: #ccc; font-size: 12px; text-align: center;'>FinAware - Finansal özgürlüğünüz için</p>
</body>
</html>";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendTransactionNotificationAsync(string toEmail, string username, string type, decimal amount, string description, string categoryName)
        {
            var typeText = type == "Income" ? "Gelir" : "Gider";
            var subject = $"FinAware - Yeni {typeText} Eklendi";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #4DB6AC 0%, #26A69A 100%); padding: 30px; border-radius: 12px; text-align: center; margin-bottom: 30px;'>
        <h1 style='color: white; margin: 0;'>💰 FinAware</h1>
    </div>
    <h2 style='color: #333;'>Merhaba, {username}! 👋</h2>
    <p style='color: #666;'>Hesabınıza yeni bir {typeText.ToLower()} eklendi.</p>
    <p style='margin: 5px 0;'><strong>Tutar:</strong> ₺{amount:N2}</p>
    <p style='margin: 5px 0;'><strong>Kategori:</strong> {categoryName}</p>
    <p style='margin: 5px 0;'><strong>Açıklama:</strong> {(string.IsNullOrEmpty(description) ? "Açıklama yok" : description)}</p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
    <p style='color: #ccc; font-size: 12px; text-align: center;'>FinAware - Finansal özgürlüğünüz için</p>
</body>
</html>";
            await SendEmailAsync(toEmail, subject, body);
        }
    }
}