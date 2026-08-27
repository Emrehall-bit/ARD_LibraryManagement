# Library Management System

ASP.NET Core ve Angular kullanılarak geliştirilen modüler kütüphane yönetim sistemi.

## Architecture

Backend, Modular Monolith mimarisi kullanılarak geliştirilmiştir.

Başlangıç modülleri:

- Books
- Borrowing
- Identity

## Technologies

### Backend
- ASP.NET Core
- .NET 10
- Entity Framework Core

### Frontend
- Angular

### Database
- PostgreSQL / SQL Server

## Docker Compose

Tum stack'i Docker ile calistirmak icin local backend/frontend process'lerini kapatin ve repo kokunde sunu calistirin:

```powershell
docker compose up -d --build
```

Adresler:

- Frontend: http://localhost:4200
- Backend API: http://localhost:5074
- MinIO Console: http://localhost:9001
- MinIO API: http://localhost:9000
- PostgreSQL: localhost:5432

Sadece altyapiyi Docker'da calistirip backend/frontend'i localden calistirmak icin:

```powershell
docker compose up -d db minio
```

Full Docker modunda backend container icinden PostgreSQL'e `db:5432`, MinIO'ya `minio:9000` ile baglanir. Local backend ise repo kokundeki `.env` dosyasini otomatik okuyarak `localhost:9000` MinIO ayarlarini kullanir.

Temiz veritabaninda kitap kapaklarini OpenLibrary'den indirip MinIO'ya yuklemek icin:

```powershell
docker compose run --rm cover-import --limit 200
```

Veriler `data/postgres` ve `data/minio` altinda kalici tutulur. Bu klasorler silinirse veritabani ve MinIO icerigi yeniden olusur; kapak import'unu tekrar calistirmak gerekir.

Servisleri durdurmak icin:

```powershell
docker compose down
```
