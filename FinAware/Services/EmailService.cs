using System.Net;
using System.Net.Mail;

namespace FinAware.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var host = _configuration["EmailSettings:SmtpHost"]!;
                var port = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
                var user = _configuration["EmailSettings:SmtpUser"]!;
                var password = _configuration["EmailSettings:SmtpPassword"]!;
                var fromEmail = _configuration["EmailSettings:FromEmail"]!;
                var fromName = _configuration["EmailSettings:FromName"] ?? "FinAware";

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(user, password),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

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
            }
        }


        // ORTAK ŞABLON 
        private string BaseTemplate(string content) => $@"
<!DOCTYPE html>
<html lang='tr'>
<head>
  <meta charset='UTF-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
  <title>FinAware</title>
</head>
<body style='margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f6f8;padding:40px 0;'>
    <tr>
      <td align='center'>
        <table width='600' cellpadding='0' cellspacing='0'
               style='background:#ffffff;border-radius:8px;overflow:hidden;
                      box-shadow:0 2px 8px rgba(0,0,0,0.06);'>

          <!-- HEADER -->
          <tr>
            <td style='background:#1AAFA3;padding:32px 40px;'>
              <p style='margin:0;font-size:22px;font-weight:700;
                        color:#ffffff;letter-spacing:0.5px;'>FinAware</p>
              <p style='margin:4px 0 0;font-size:13px;color:rgba(255,255,255,0.75);'>
                Kişisel Finans Yönetimi
              </p>
            </td>
          </tr>

          <!-- CONTENT -->
          <tr>
            <td style='padding:40px;'>
              {content}
            </td>
          </tr>

          <!-- FOOTER -->
          <tr>
            <td style='background:#f9fafb;padding:24px 40px;
                       border-top:1px solid #e5e7eb;'>
              <p style='margin:0;font-size:12px;color:#9ca3af;line-height:1.6;'>
                Bu e-posta FinAware tarafından otomatik olarak gönderilmiştir.<br/>
                Herhangi bir sorunuz için destek ekibimizle iletişime geçebilirsiniz.
              </p>
              <p style='margin:12px 0 0;font-size:12px;color:#d1d5db;'>
                &copy; 2026 FinAware. Tüm hakları saklıdır.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        private string Heading(string text) =>
            $"<h2 style='margin:0 0 16px;font-size:20px;font-weight:700;color:#111827;'>{text}</h2>";

        private string Paragraph(string text) =>
            $"<p style='margin:0 0 16px;font-size:15px;color:#4b5563;line-height:1.7;'>{text}</p>";

        private string Button(string url, string label) => $@"
<table cellpadding='0' cellspacing='0' style='margin:24px 0;'>
  <tr>
    <td style='background:#1AAFA3;border-radius:6px;'>
      <a href='{url}'
         style='display:inline-block;padding:14px 32px;font-size:14px;
                font-weight:600;color:#ffffff;text-decoration:none;
                letter-spacing:0.3px;'>
        {label}
      </a>
    </td>
  </tr>
</table>";

        private string InfoBox(string content, string bg = "#f0fdf9", string border = "#1AAFA3") => $@"
<div style='background:{bg};border-left:4px solid {border};
            border-radius:4px;padding:16px 20px;margin:20px 0;'>
  {content}
</div>";

        private string Divider() =>
            "<hr style='border:none;border-top:1px solid #e5e7eb;margin:24px 0;'/>";

        private string StatRow(string label, string value, string valueColor = "#111827") => $@"
<tr>
  <td style='padding:10px 0;font-size:14px;color:#6b7280;
             border-bottom:1px solid #f3f4f6;'>{label}</td>
  <td style='padding:10px 0;font-size:14px;font-weight:600;
             color:{valueColor};text-align:right;
             border-bottom:1px solid #f3f4f6;'>{value}</td>
</tr>";

        // E-POSTA DOĞRULAMA 
        public async Task SendEmailVerificationAsync(string toEmail, string username, string verificationLink)
        {
            var subject = "FinAware — E-posta Adresinizi Doğrulayın";

            var content = $@"
{Heading("Hesabınızı Doğrulayın")}
{Paragraph($"Merhaba <strong>{username}</strong>,")}
{Paragraph("FinAware'e kaydolduğunuz için teşekkür ederiz. Hesabınızı etkinleştirmek ve tüm özelliklere erişmek için e-posta adresinizi doğrulamanız gerekmektedir.")}
{Button(verificationLink, "E-posta Adresimi Doğrula")}
{Divider()}
<p style='margin:0;font-size:13px;color:#9ca3af;'>
  Bu bağlantı <strong>24 saat</strong> geçerlidir. 
  Eğer bu isteği siz yapmadıysanız bu e-postayı dikkate almayınız.
</p>";

            await SendEmailAsync(toEmail, subject, BaseTemplate(content));
        }

        //  HOŞ GELDİNİZ
        public async Task SendWelcomeEmailAsync(string toEmail, string username)
        {
            var subject = "FinAware'e Hoş Geldiniz";

            var content = $@"
{Heading($"Hoş geldiniz, {username}.")}
{Paragraph("E-posta adresiniz başarıyla doğrulandı. Artık FinAware'in tüm özelliklerine erişebilirsiniz.")}
{InfoBox($@"
  <p style='margin:0 0 8px;font-size:13px;font-weight:600;color:#065f46;'>Başlangıç için öneriler</p>
  <ul style='margin:0;padding-left:18px;font-size:13px;color:#374151;line-height:2;'>
    <li>İlk gelir veya giderinizi kaydedin</li>
    <li>Harcama kategorilerinizi özelleştirin</li>
    <li>Aylık bütçe limiti belirleyin</li>
    <li>Birikim hedefi oluşturun</li>
  </ul>
")}
{Paragraph("Finansal hedeflerinize ulaşmanızda FinAware her adımda yanınızda.")}";

            await SendEmailAsync(toEmail, subject, BaseTemplate(content));
        }

        //  BÜTÇE UYARISI
        public async Task SendBudgetWarningAsync(string toEmail, string username, string categoryName, decimal percentage, decimal remaining)
        {
            var subject = $"FinAware — {categoryName} Bütçe Uyarısı";

            var content = $@"
{Heading("Bütçe Limitinize Yaklaşıyorsunuz")}
{Paragraph($"Merhaba <strong>{username}</strong>,")}
{Paragraph($"<strong>{categoryName}</strong> kategorisindeki bütçenizin <strong>%{Math.Round(percentage, 0)}'i</strong> kullanıldı. Harcamalarınızı gözden geçirmenizi öneririz.")}
{InfoBox($@"
  <table width='100%' cellpadding='0' cellspacing='0'>
    {StatRow("Kategori", categoryName)}
    {StatRow("Kullanım Oranı", $"%{Math.Round(percentage, 0)}", "#f59e0b")}
    {StatRow("Kalan Limit", $"₺{remaining:N2}", "#1AAFA3")}
  </table>
", "#fffbeb", "#f59e0b")}
{Paragraph("Bütçenizi aşmamak için FinAware üzerinden harcamalarınızı takip edebilirsiniz.")}";

            await SendEmailAsync(toEmail, subject, BaseTemplate(content));
        }

        // BÜTÇE AŞIMI 
        public async Task SendBudgetExceededAsync(string toEmail, string username, string categoryName, decimal limit, decimal spent)
        {
            var subject = $"FinAware — {categoryName} Bütçesi Aşıldı";

            var content = $@"
{Heading("Bütçe Limitiniz Aşıldı")}
{Paragraph($"Merhaba <strong>{username}</strong>,")}
{Paragraph($"<strong>{categoryName}</strong> kategorisinde belirlediğiniz bütçe limitini aştınız.")}
{InfoBox($@"
  <table width='100%' cellpadding='0' cellspacing='0'>
    {StatRow("Kategori", categoryName)}
    {StatRow("Belirlenen Limit", $"₺{limit:N2}")}
    {StatRow("Toplam Harcama", $"₺{spent:N2}", "#ef4444")}
    {StatRow("Aşım Miktarı", $"₺{(spent - limit):N2}", "#ef4444")}
  </table>
", "#fef2f2", "#ef4444")}
{Paragraph("Gelecek ay için bütçe planlamanızı güncellemenizi öneririz.")}";

            await SendEmailAsync(toEmail, subject, BaseTemplate(content));
        }

        public async Task SendTransactionNotificationAsync(string toEmail, string username, string type, decimal amount, string description, string categoryName)
        {
            var typeText = type == "Income" ? "Gelir" : "Gider";
            var subject = $"FinAware — Yeni {typeText} Kaydedildi";
            var amountColor = type == "Income" ? "#1AAFA3" : "#ef4444";
            var amountPrefix = type == "Income" ? "+" : "-";

            var content = $@"
{Heading($"Yeni {typeText} Kaydedildi")}
{Paragraph($"Merhaba <strong>{username}</strong>,")}
{Paragraph($"Hesabınıza yeni bir {typeText.ToLower()} kaydı eklendi.")}
{InfoBox($@"
  <table width='100%' cellpadding='0' cellspacing='0'>
    {StatRow("Tür", typeText)}
    {StatRow("Tutar", $"{amountPrefix}₺{amount:N2}", amountColor)}
    {StatRow("Kategori", categoryName)}
    {StatRow("Açıklama", string.IsNullOrEmpty(description) ? "—" : description)}
    {StatRow("Tarih", DateTime.Now.ToString("dd MMMM yyyy, HH:mm"))}
  </table>
")}
{Paragraph("Bu işlemi siz gerçekleştirmediyseniz lütfen hesabınızı kontrol edin.")}";

            await SendEmailAsync(toEmail, subject, BaseTemplate(content));
        }
    }
}