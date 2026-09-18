# DunyaBunya — Backend (Auth + API)

[![Build and test](https://github.com/dunya-bunya-team/dunyabunya_backend_auth/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/dunya-bunya-team/dunyabunya_backend_auth/actions/workflows/build-and-test.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Supabase-3ECF8E?logo=postgresql&logoColor=white)
![Tests](https://img.shields.io/badge/tests-45%20passing-brightgreen)

Toshkentdagi qurilish materiallari marketpleysi **DunyaBunya** uchun yagona backend xizmati. .NET 10 / ASP.NET Core Web API asosida qurilgan, PostgreSQL (Supabase) ma'lumotlar bazasi va Odoo ERP bilan integratsiyalashgan.

## Loyiha nima qiladi

- Foydalanuvchi ro'yxatdan o'tishi, login (parol yoki Google orqali), email tasdiqlash, parolni tiklash — JWT access/refresh token asosida.
- Odoo ERP bilan ikki tomonlama integratsiya: mijozlar (contacts) va mahsulotlar Odoo bilan sinxronlanadi, "Marketplace" tegi orqali ajratiladi.
- Sayt uchun to'liq kontent va savdo backend'i: mahsulotlar, kategoriyalar, buyurtmalar, sharhlar, bannerlar, xizmatlar, afzalliklar, statistika, hamkorlar, aloqa xabarlari.
- Ball/sovg'alar tizimi: har bir buyurtmadan ball yig'iladi, ballarga sovg'a kampaniyalarida sovg'a "sotib olish" mumkin.
- Admin panel uchun rol tizimi: **Admin** (to'liq huquq) va **Superuser** (bo'lim-bo'lim cheklangan huquq — masalan faqat "orders" yoki faqat "gifts").

## Texnologiyalar

- **.NET 10**, ASP.NET Core Web API
- **PostgreSQL** (Supabase, Npgsql orqali) + Entity Framework Core
- **JWT** autentifikatsiya (access + refresh token)
- **Google OAuth** login
- **Odoo** XML-RPC integratsiyasi (mijoz va mahsulot sinxronizatsiyasi)
- **Swagger / OpenAPI** — hujjatlashtirilgan API (sozlama orqali ochiq/yopiq)
- Windows Service sifatida productionda ishlaydi

## Loyiha tuzilishi

```
Controllers/    — API endpointlar (Auth, Products, Orders, Reviews, GiftClaims, Admin, ...)
Models/         — EF Core entity'lar va DTO'lar
Data/           — AppDbContext, migratsiyalar
Services/       — fon jarayonlari (masalan Odoo mahsulot sinxronizatsiyasi) va integratsiya servislari
Filters/        — авторизация filterlari (masalan Superuser bo'lim huquqi)
```

## Asosiy API bo'limlari

| Bo'lim | Nima uchun |
|---|---|
| `Auth` | Ro'yxatdan o'tish, login, Google login, email tasdiqlash, token yangilash |
| `Admin` | Xodimlar (Admin/Superuser) boshqaruvi |
| `Products` | Mahsulotlar (Odoo'dan sinxronlanadi), rasm yuklash/o'chirish |
| `Categories` | Kategoriya va subkategoriyalar |
| `Orders` | Buyurtma yaratish, holatini o'zgartirish, ball avtomatik qo'shiladi |
| `Reviews` / `ServiceReviews` | Mahsulot/xizmat sharhlari, admin javobi |
| `UserPoints` / `GiftTiers` / `GiftCampaigns` / `GiftClaims` | Ball va sovg'alar tizimi |
| `Banners` / `Advantages` / `Stats` / `Partners` / `Services` | Sayt kontenti |
| `ContactMessages` | Aloqa formasi xabarlari |
| `Notifications` | Foydalanuvchi bildirishnomalari |
| `OdooIntegration` | Odoo tomonidan chaqiriladigan ichki endpointlar |

To'liq va interaktiv hujjat uchun ilovani ishga tushirib `/swagger` manziliga kiring (Swagger sozlama orqali production'da yopiq turadi, kerak bo'lganda `Swagger:Enabled` orqali vaqtincha ochiladi).

## Ishga tushirish

1. `appsettings.Development.json` faylida quyidagilarni to'ldiring (bu fayl git'ga qo'shilmaydi — maxfiy ma'lumotlar uchun):
   - `ConnectionStrings:DefaultConnection` — PostgreSQL ulanish satri
   - `Odoo:*` — Odoo bazasi, foydalanuvchi va API kalitlari
   - `Jwt:Key` — token imzolash kaliti
   - `Smtp:*` / `Resend:*` — email yuborish uchun
   - `Recaptcha:SecretKey`
2. Migratsiyalarni qo'llash:
   ```
   dotnet ef database update
   ```
3. Ishga tushirish:
   ```
   dotnet run
   ```

## Muhim arxitektura qarorlari

- **Narx va ball hisob-kitoblari serverda bajariladi** — mijoz tomonidan yuboriladigan narx/ball qiymatlariga ishonilmaydi (masalan buyurtma narxi bazadagi mahsulot narxidan olinadi, ball yechish/qo'shish atomik SQL amallar orqali, poyga holatiga yo'l qo'ymaydi).
- **Mahsulotlar Odoo'dan fon jarayonida davriy sinxronlanadi** (`ProductSyncBackgroundService`) — frontend Odoo'ga to'g'ridan-to'g'ri murojaat qilmaydi, o'z bazamizdan tez javob oladi. Kutilmagan bo'sh natijalarga qarshi o'z-o'zini himoya qiluvchi mexanizm bor.
- **Superuser huquqlari bo'lim-bo'lim** (`RequireSection` filtri) — masalan bitta Superuser faqat buyurtmalarni, boshqasi faqat sovg'alar bo'limini boshqarishi mumkin.
- **Mahsulot nomi ikki xil manbadan**: `Name` (admin tahrirlashi mumkin) va `OdooOriginalName` (Odoo'dagi tarixiy, o'zgarmas birinchi nom) — bir-birining ustidan yozilmaydi.
- **Odoo ombordagi haqiqiy son** (`StockQuantity`) faqat admin endpointlarida qaytariladi — mijozga ochiq API'da hech qachon ko'rinmaydi (faqat `bor/yo'q`).

## Xavfsizlik

- Login/parol endpointlari uchun sliding-window rate limiting (`AuthPolicy`) + global limiter
- Aniq ruxsat etilgan domenlar bilan cheklangan CORS (wildcard emas)
- Barcha javoblarda xavfsizlik header'lari: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Strict-Transport-Security`
- Global exception handler — texnik tafsilotlar (stack trace) faqat serverning o'z logiga yoziladi, chaqiruvchiga hech qachon chiqmaydi
- Maxfiy ma'lumotlar (parol, API kalitlar) faqat gitignored `appsettings.Development.json`da — repo tarixida hech qachon bo'lmagan
