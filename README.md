# 💰 FinAware – Kişisel Finans Yönetimi Web Uygulaması

<div align="center">

![.NET](https://img.shields.io/badge/.NET_9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)
![Azure](https://img.shields.io/badge/Azure_SQL-0078D4?style=for-the-badge&logo=microsoft-azure&logoColor=white)
![Railway](https://img.shields.io/badge/Railway-0B0D0E?style=for-the-badge&logo=railway&logoColor=white)
![OpenAI](https://img.shields.io/badge/GPT--4o_Vision-412991?style=for-the-badge&logo=openai&logoColor=white)

**🌐 Canlı Demo → [finawaremvc-production.up.railway.app](https://finawaremvc-production.up.railway.app)**

*Gizlilik odaklı, platform bağımsız kişisel finans yönetim uygulaması*

</div>

---

## 📌 Proje Hakkında

FinAware, kullanıcıların finansal verilerini **banka hesabına bağlamadan** manuel olarak yönetebileceği, gizlilik öncelikli bir web uygulamasıdır. ASP.NET Core 9.0 MVC + .NET 9 Web API mimarisiyle geliştirilmiş olup Railway üzerinde canlıya alınmıştır.

> 🎓 İstanbul Beykent Üniversitesi — Bilgisayar Mühendisliği Bitirme Projesi (2026)  
> 👥 Mahmut Hüdayi Çört & Beyza Beyazkılıç Yum — Danışman: Dr. Fuat Candan

---

## ✨ Özellikler

| Modül | Açıklama |
|-------|----------|
| 💳 **Gelir / Gider Takibi** | İşlem ekleme, kategori bazlı filtreleme, çoklu döviz desteği (TCMB) |
| 🧾 **GPT-4o Vision OCR** | Fatura fotoğrafından otomatik tutar, tarih ve kategori çıkarımı |
| 🎯 **Birikim Hedefleri** | Hedef bazlı birikim takibi, dövizli yatırım, kar/zarar hesaplama |
| 📊 **Analiz & Grafikler** | Chart.js ile kategori bazlı harcama trendleri, haftalık/aylık/yıllık raporlar |
| 🔔 **Bildirim Sistemi** | Site içi bildirimler + SMTP e-posta uyarıları |
| 💹 **Bütçe Modülü** | Aylık harcama limiti, %50/%80/%100 aşım uyarıları |
| 🤖 **Telegram Bot** | Telegram üzerinden bakiye sorgulama ve harcama özeti |
| 👑 **Admin Paneli** | Kullanıcı yönetimi, dondurma/silme, sistem istatistikleri |
| 🌍 **Döviz Desteği** | TCMB XML servisi üzerinden günlük kur güncelleme |

---

## 🏗️ Mimari

```
┌─────────────────────────────────────────────────────┐
│                    Kullanıcı (Tarayıcı)              │
└──────────────────────┬──────────────────────────────┘
                       │ HTTP
┌──────────────────────▼──────────────────────────────┐
│         FinAwareMvc (ASP.NET Core 9.0 MVC)           │
│    Razor Views · Bootstrap 5 · Chart.js · AJAX       │
└──────────────────────┬──────────────────────────────┘
                       │ HTTPS + JWT
┌──────────────────────▼──────────────────────────────┐
│         FinAware.API (.NET 9 Web API)                │
│    7 Controller · EF Core 9 · JWT · BCrypt           │
│    GPT-4o Vision · TCMB · SMTP · Telegram Bot        │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│              Azure SQL Database                      │
│    Users · Categories · Transactions · Savings       │
│    Notifications · Budgets · SavingTransactions      │
└─────────────────────────────────────────────────────┘
```

---

## 🛠️ Teknoloji Stack

### Backend (FinAware.API)
- **Framework:** .NET 9 Web API
- **ORM:** Entity Framework Core 9 (Code-First, Migrations)
- **Kimlik Doğrulama:** JWT (HS256) + HttpOnly Cookie + BCrypt.Net
- **AI:** OpenAI GPT-4o Vision API (fatura analizi)
- **Döviz:** TCMB XML Servisi (günlük kur)
- **Bildirim:** SMTP (Gmail App Password)
- **Bot:** Telegram.Bot

### Frontend (FinAwareMvc)
- **Framework:** ASP.NET Core 9.0 MVC + Razor Views
- **UI:** Bootstrap 5.3
- **Grafikler:** Chart.js 4.4
- **Dependency Injection:** IApiService / ApiService

### Altyapı
- **Deployment:** Railway (Docker)
- **Veritabanı:** Azure SQL Database (Free Tier)
- **CI/CD:** GitHub → Railway otomatik deploy

---

## 🗄️ Veritabanı Şeması

```
Users ──── Categories ──── Transactions
  │               └──── Budgets
  ├──── Notifications
  └──── Savings ──── SavingTransactions
```

**7 tablo** · **11+ Migration** · **Cascade/Restrict FK kuralları**

---

## 🔐 Güvenlik

- JWT token → HttpOnly + Secure + SameSite=Strict cookie
- BCrypt (~120ms) → Brute-force koruması
- E-posta doğrulama (24 saatlik token)
- CORS politikası → Sadece kayıtlı origin'ler
- Rol tabanlı yetkilendirme (User / Admin / Frozen)
- Azure SQL Firewall kuralları

---

## 🚀 Kurulum (Local)

```bash
# Repoyu klonla
git clone https://github.com/mcorthudayi/FinAware.git
cd FinAware

# appsettings.json ayarlarını yap (API projesi)
# ConnectionStrings, JwtSettings, OpenAI:ApiKey, EmailSettings

# Migration uygula
cd FinAware.API
dotnet ef database update

# API'yi başlat
dotnet run
# → https://localhost:7061

# MVC'yi başlat (yeni terminal)
cd ../FinAwareMvc
dotnet run
# → https://localhost:7023
```

### Gerekli Environment Variables (API)
```
ConnectionStrings__DefaultConnection = Server=...
JwtSettings__SecretKey = ...
JwtSettings__Issuer = FinAwareAPI
JwtSettings__Audience = FinAwareMVC
OpenAI__ApiKey = sk-...
EmailSettings__SmtpHost = smtp.gmail.com
EmailSettings__SmtpPort = 587
EmailSettings__SmtpUser = ...
EmailSettings__SmtpPassword = ... (App Password)
TelegramBot__Token = ...
AppSettings__BaseUrl = https://...
AppSettings__MvcBaseUrl = https://...
```

---

## 📱 Telegram Bot Kullanımı

1. Hesap sayfasından **Telegram Bağla** butonuna tıkla
2. Oluşturulan derin linke tıklayarak botu başlat
3. `/start` komutunu gönder ve hesabı doğrula

**Komutlar:** `/bakiye` · `/ozet` · `/son` · `/yardim`

---

## 👨‍💻 Geliştirici

**Mahmut Hüdayi Çört**  
[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=flat&logo=linkedin&logoColor=white)](https://linkedin.com/in/mhudayicort)
[![GitHub](https://img.shields.io/badge/GitHub-100000?style=flat&logo=github&logoColor=white)](https://github.com/mcorthudayi)

---

## 📄 Lisans

Bu proje akademik amaçlı geliştirilmiştir. Ticari kullanım için izin gereklidir.
