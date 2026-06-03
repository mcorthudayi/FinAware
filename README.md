# 💰 FinAware – Kişisel Finans Yönetimi Web Uygulaması

FinAware, gelir-gider takibi, bütçe planlama, birikim hedefleri ve yapay zeka destekli finansal analiz sunan modern bir kişisel finans yönetim platformudur.

🌐 **Canlı Demo:** [finaware-mvc.onrender.com](https://finaware-mvc.onrender.com)

---

## 🚀 Özellikler

### 💳 Temel Özellikler
- Gelir & gider işlem takibi (döviz desteği ile — USD, EUR, GBP, Altın, Gümüş)
- TCMB güncel kur entegrasyonu ile otomatik TL dönüşümü
- Aylık bütçe planlama ve limit takibi
- Birikim hedefleri ve ilerleme takibi
- Kategori yönetimi (özel ikonlar ve renkler)
- Excel (.xlsx) dışa aktarma
- Hatırlatıcı sistemi (e-posta bildirimleri)

### 🤖 Yapay Zeka
- **ARİS Finansal Asistan** — GPT-4o tabanlı, doğal dille işlem ekleme, analiz, sayfa navigasyonu
- **Fatura OCR** — GPT-4o Vision ile fatura/fiş fotoğrafından otomatik işlem oluşturma

### 📱 Telegram Bot
- Bakiye sorgulama, işlem ekleme, bütçe ve birikim takibi
- `/gelir`, `/gider`, `/butce`, `/birikim`, `/ozet` komutları
- Fatura fotoğrafı gönderip otomatik analiz ettirme
- Doğal dil ile AI destekli sohbet

### 💎 Abonelik Sistemi
- **Free** — Sınırsız manuel işlem, bütçe ve birikim
- **Gold** — Fatura OCR (60/ay), ARİS (30 soru/ay), Telegram Bot, Excel export
- **Platinum** — Her şey sınırsız
- İyzico sandbox ödeme entegrasyonu

### 🛡️ Admin Paneli
- Kullanıcı yönetimi (dondurma, silme, plan değiştirme)
- Aylık aktivite grafikleri (Chart.js)
- Abonelik istatistikleri

---

## 🛠️ Tech Stack

### Backend
| Katman | Teknoloji |
|--------|-----------|
| API | ASP.NET Core 9 Web API |
| MVC | ASP.NET Core 9 MVC |
| ORM | Entity Framework Core 9 |
| Veritabanı | Azure SQL (Microsoft SQL Server) |
| Kimlik Doğrulama | JWT Bearer Token |
| Şifreleme | BCrypt.Net |
| E-posta | MailKit / SMTP |
| Yapay Zeka | OpenAI GPT-4o & GPT-4o Vision |
| Döviz Kurları | TCMB XML API |
| Ödeme | İyzico .NET SDK |
| Telegram | Telegram.Bot |

### Frontend
| Katman | Teknoloji |
|--------|-----------|
| UI Framework | Bootstrap 5.3 |
| İkonlar | Bootstrap Icons |
| Grafikler | Chart.js 4.4 |
| Stil | Custom CSS (Finaware Design System) |

---

## ☁️ Deployment

Proje **Render.com** üzerinde 3 ayrı servis olarak çalışmaktadır:

| Servis | URL |
|--------|-----|
| MVC (Web Arayüzü) | [finaware-mvc.onrender.com](https://finaware-mvc.onrender.com) |
| API | [finaware-uq2x.onrender.com](https://finaware-uq2x.onrender.com) |
| Telegram Bot | [finaware-bot.onrender.com](https://finaware-bot.onrender.com) |

**Veritabanı:** Azure SQL — `finaware-server.database.windows.net`

---

## 🏗️ Proje Yapısı

```
FinAware/
├── FinAware/           # ASP.NET Core 9 Web API
│   ├── Controllers/    # API endpoint'leri
│   ├── Models/         # Entity modelleri
│   ├── Data/           # DbContext & migrations
│   └── Services/       # İş mantığı servisleri
├── FinAwareMvc/        # ASP.NET Core 9 MVC
│   ├── Controllers/    # MVC controller'ları
│   ├── Views/          # Razor view'ları
│   ├── Models/         # ViewModel'ler
│   └── Services/       # API proxy servisleri
└── FinAware.Bot/       # Telegram Bot
    ├── Services/        # Bot & OpenAI servisleri
    └── Data/            # Bot SQLite context
```

---

## 🔐 Ortam Değişkenleri

### API Servisi
```env
ConnectionStrings__DefaultConnection=Server=...
JwtSettings__SecretKey=...
JwtSettings__Issuer=FinAwareAPI
JwtSettings__Audience=FinAwareMVC
OpenAI__ApiKey=sk-...
Iyzico__ApiKey=sandbox-...
Iyzico__SecretKey=sandbox-...
Iyzico__BaseUrl=https://sandbox-api.iyzipay.com
AppSettings__MvcBaseUrl=https://finaware-mvc.onrender.com
AppSettings__ApiBaseUrl=https://finaware-uq2x.onrender.com
```

### MVC Servisi
```env
ApiBaseUrl=https://finaware-uq2x.onrender.com
```

### Bot Servisi
```env
TelegramBot__Token=...
FinAwareApi__BaseUrl=https://finaware-uq2x.onrender.com
FinAwareApi__BotSecret=...
OpenAI__ApiKey=sk-...
ConnectionStrings__DefaultConnection=Server=...
```

---

## 👨‍💻 Geliştirici

**Mahmut Hüdayi Çört**
- GitHub: [@mcorthudayi](https://github.com/mcorthudayi)
- LinkedIn: [linkedin.com/in/mhudayicort](https://linkedin.com/in/mhudayicort)

**Beyza Beyazkılıç Yum** — Proje Ortağı

**Danışman:** Dr. Fuat Candan — İstanbul Beykent Üniversitesi

---

## 📄 Lisans

Bu proje İstanbul Beykent Üniversitesi Bilgisayar Mühendisliği bitirme projesi kapsamında geliştirilmiştir.
