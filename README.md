# TicketFlow API — Hệ thống Đặt giữ chỗ & Phát hành Vé Sự kiện Chịu tải Đồng thời

> Backend RESTful API cho nền tảng **đặt vé & giữ chỗ sự kiện** (concert, thể thao), được xây dựng trên **.NET 10 LTS** (`ASP.NET Core Web API`) với **EF Core 10** và **SQL Server 2022**. Hệ thống giải quyết 3 bài toán của ticketing: **Overselling (bán trùng ghế)**, **Duplicate Orders (trùng đơn do retry)** và **Ghost Holds (ghế bị khóa chết)**.

---

## 📌 Mục lục
- [Giới thiệu dự án](#-giới-thiệu-dự-án)
- [Công nghệ sử dụng (Tech Stack)](#-công-nghệ-sử-dụng-tech-stack)
- [Tính năng chính](#-tính-năng-chính)
- [Kiến trúc & Cơ sở dữ liệu](#-kiến-trúc--cơ-sở-dữ-liệu)
- [Cấu trúc thư mục](#-cấu-trúc-thư-mục)
- [Tài liệu API (API Endpoints)](#-tài-liệu-api-api-endpoints)
- [Hướng dẫn cài đặt & Chạy dự án](#-hướng-dẫn-cài-đặt--chạy-dự-án)
- [Cấu hình môi trường (Configuration)](#-cấu-hình-môi-trường-configuration)
- [Cơ chế kỹ thuật chính](#-cơ-chế-kỹ-thuật-chính)
- [Kiểm thử](#-kiểm-thử)

---

## 📖 Giới thiệu dự án

Trong các sự kiện mở bán vé, hàng chục đến hàng nghìn người dùng thường nhấp chọn cùng một ghế tại cùng thời điểm. Nếu không kiểm soát đồng thời chặt chẽ, hệ thống sẽ gặp:

1. **Overselling** — hai người dùng cùng xác nhận mua thành công một ghế duy nhất.
2. **Duplicate Orders** — mạng chập chờn khiến client gửi lại lệnh xác nhận và tạo ra hai đơn hàng cho cùng một giao dịch.
3. **Ghost Holds** — người dùng giữ ghế rồi thoát ứng dụng khiến ghế bị khóa vĩnh viễn.

**TicketFlow API** giải quyết cả 3 bài toán này bằng:

- **Optimistic Concurrency Control** (`RowVersion` trên SQL Server) + **Filtered Unique Index** bảo vệ tầng dữ liệu.
- **Idempotent Order Confirmation** dựa trên `Idempotency-Key` + `RequestHash` gắn theo người dùng.
- **Automated Expiration Worker (`BackgroundService`)** định kỳ thu hồi ghế quá hạn giữ chỗ an toàn bằng `ExecuteUpdateAsync`.

Quy tắc v1.0: **1 Hold = 1 Ghế, 1 Order = 1 Ghế**.

---

## 🛠️ Công nghệ sử dụng (Tech Stack)

| Tầng | Công nghệ |
|---|---|
| **Runtime & SDK** | .NET 10 LTS, C# (`Nullable` + `ImplicitUsings`, `TreatWarningsAsErrors`) |
| **Web API** | ASP.NET Core Controllers (ngoại trừ `/health` và `/openapi/v1.json` là minimal endpoints) |
| **OpenAPI / Swagger** | `Microsoft.AspNetCore.OpenApi` + `Swashbuckle.AspNetCore.SwaggerUI` (trỏ về `/openapi/v1.json`) |
| **ORM & Database** | Entity Framework Core 10, SQL Server 2022, Code-First Migrations, Fluent API |
| **Auth & Security** | JWT Bearer (HS256), `BCrypt.Net-Next` (work factor ≥ 11), Refresh Token Rotation + Reuse Detection |
| **Rate Limiting** | `Microsoft.AspNetCore.RateLimiting` — Fixed Window theo IP (Auth) và theo User/`sub` (Booking) |
| **Validation** | `FluentValidation` + filter tập trung + `JsonStringEnumConverter` toàn cục |
| **Health Checks** | `Microsoft.Extensions.Diagnostics.HealthChecks` + `DotNetDiag.HealthChecks.SqlServer` (`GET /health`) |
| **Background Jobs** | `ExpiredSeatReleaseWorker` — `BackgroundService` + `PeriodicTimer` + `ExecuteUpdateAsync` |
| **Container** | Multi-stage `Dockerfile` + `docker-compose.yml` (API + SQL Server) |
| **Testing** | xUnit, Moq, FluentAssertions, `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) |

---

## ✨ Tính năng chính

- **🔐 Xác thực & Phân quyền RBAC** — Register / Login / Refresh (rotation + reuse detection) / Revoke; 2 vai trò `Admin` và `Customer`; `POST /auth/register` không nhận `Role` từ payload.
- **📅 Quản lý Sự kiện** — Tạo sự kiện, chuyển trạng thái `Draft → Published → Cancelled` với ràng buộc nghiệp vụ (đủ ghế, `StartsAtUtc` ở tương lai, hủy `Published` tự giải phóng ghế `Held`).
- **💺 Quản lý Ghế** — Khởi tạo hàng loạt (bulk tối đa 500 ghế/request), `SeatCode` duy nhất trong phạm vi `EventId`, tìm kiếm/lọc/phân trang ghế theo trạng thái.
- **⏱️ Vòng đời giữ chỗ** — `Hold` mặc định **10 phút**, chiếm quyền khi `HeldUntilUtc` quá hạn, `Release` sớm bởi chủ giữ chỗ hoặc Admin.
- **🧾 Đặt vé Idempotent** — `ConfirmOrder` với `Idempotency-Key` + `RequestHash`: retry an toàn, không tạo đơn trùng.
- **🧹 Worker tự động** — `ExpiredSeatReleaseWorker` quét và trả ghế quá hạn về `Available` mỗi chu kỳ.
- **🛡️ Chống lạm dụng** — Rate limiting riêng cho nhóm Auth và nhóm Booking; lỗi trả về chuẩn **RFC 7807 ProblemDetails** qua `IExceptionHandler` tập trung.

---

## 🏗️ Kiến trúc & Cơ sở dữ liệu

Kiến trúc **Clean Architecture** / Vertical Slice với ranh giới phụ thuộc cứng (Hard Boundaries):

```
TicketFlow.Api            → chỉ phụ thuộc Application & Infrastructure (đăng ký DI)
TicketFlow.Application    → chỉ phụ thuộc Domain (Services, DTOs, Validators)
TicketFlow.Infrastructure → phụ thuộc Application & Domain (EF Core, Repositories, JWT, Worker)
TicketFlow.Domain         → ZERO NuGet package (Entities, Enums, Domain Exceptions)
```

**Các cơ chế bảo vệ dữ liệu trên SQL Server:**

- `RowVersion` (concurrency token) trên `Seats` → `DbUpdateConcurrencyException` → `ConcurrencyConflictException` (`HTTP 409`).
- Filtered Unique Index `WHERE [Status] = N'Confirmed'` → ngăn bán trùng ghế ở tầng DB.
- `UNIQUE (EventId, SeatCode)` → ràng buộc mã ghế.
- Refresh Token chỉ lưu **SHA-256 hex** (64 ký tự), không lưu token thô.
- Enum `Status` map `HasConversion<string>()` để khớp filtered index.
- Tiền tệ `decimal` + `.HasPrecision(18, 2)`.

**Entity chính:** `Users`, `RefreshTokens`, `Events`, `Seats`, `Orders` (+ `OrderItems`).

---

## 📁 Cấu trúc thư mục

```text
TicketFlow/
├── src/
│   ├── TicketFlow.Domain/                     # Entities, Enums, Domain Exceptions (zero deps)
│   ├── TicketFlow.Application/                # Services, Interfaces, DTOs, Validators, PagedResult<T>
│   ├── TicketFlow.Infrastructure/             # EF Core DbContext, Fluent API, Repositories, UoW, JWT, Worker
│   │   ├── Persistence/                       #   AppDbContext, UnitOfWork, Repositories
│   │   ├── Persistence/Configurations/        #   IEntityTypeConfiguration<T> cho từng entity
│   │   ├── Security/                          #   JwtTokenProvider, BCryptPasswordHasher
│   │   └── Workers/                           #   ExpiredSeatReleaseWorker
│   └── TicketFlow.Api/                        # Controllers, IExceptionHandler, Rate Limiting, Program.cs
├── tests/
│   └── TicketFlow.UnitTests/                  # xUnit + Moq + FakeTimeProvider
├── scripts/
│   └── concurrency-test.ps1                   # Kịch bản kiểm thử đồng thời 
├── Dockerfile                                 # Multi-stage build (sdk:10.0 → aspnet:10.0)
├── docker-compose.yml                         # API + SQL Server 2022
├── .env.example                               # Mẫu biến môi trường (KHÔNG commit .env thật)
└── TicketFlow.slnx
```

---

## 🌐 Tài liệu API (API Endpoints)

Tất cả endpoint nằm dưới tiền tố `/api/v1`. Chi tiết hợp đồng (DTO, mã lỗi RFC 7807).

### Auth — `/api/v1/auth`

| Method | Endpoint | Mô tả | Auth |
|---|---|---|---|
| POST | `/register` | Đăng ký tài khoản (luôn gán `Customer`) | Public |
| POST | `/login` | Đăng nhập, trả Access + Refresh Token | Public |
| POST | `/refresh` | Xoay vòng Refresh Token (reuse detection) | Public |
| POST | `/revoke` | Thu hồi phiên Refresh Token | Public |

### Events — `/api/v1/events`

| Method | Endpoint | Mô tả | Auth |
|---|---|---|---|
| GET | `/events` | Tìm kiếm/lọc/phân trang sự kiện | Public / Admin xem mọi trạng thái |
| GET | `/events/{eventId}` | Chi tiết sự kiện | Public |
| POST | `/events` | Tạo sự kiện (Admin) | `Admin` |
| PATCH | `/events/{eventId}/status` | Chuyển trạng thái `Draft → Published → Cancelled` | `Admin` |
| POST | `/events/{eventId}/seats/bulk` | Khởi tạo ghế hàng loạt (tối đa 500/request) | `Admin` |

### Seats — `/api/v1`

| Method | Endpoint | Mô tả | Auth |
|---|---|---|---|
| GET | `/events/{eventId}/seats` | Danh sách ghế theo trạng thái/phân trang | Public |
| POST | `/seats/{seatId}/hold` | Giữ chỗ ghế (10 phút, chiếm quyền khi quá hạn) | `Customer`/`Admin` |
| POST | `/seats/{seatId}/release` | Hủy giữ chỗ sớm (chủ giữ chỗ hoặc Admin) | `Customer`/`Admin` |

### Orders — `/api/v1/orders`

| Method | Endpoint | Mô tả | Auth |
|---|---|---|---|
| POST | `/orders/confirm` | Xác nhận đơn — bắt buộc header `Idempotency-Key` | `Customer`/`Admin` |
| GET | `/orders/me` | Đơn hàng của tôi | `Customer`/`Admin` |
| GET | `/orders/{orderId}` | Chi tiết đơn hàng | Owner hoặc `Admin` |

### Tài liệu tương tác

| | `dotnet run` | Docker Compose |
|---|---|---|
| **Swagger UI** | http://localhost:5168/swagger | http://localhost:8080/swagger |
| **OpenAPI JSON** | http://localhost:5168/openapi/v1.json | http://localhost:8080/openapi/v1.json |
| **Health Check** | http://localhost:5168/health | http://localhost:8080/health |

---

## 🚀 Hướng dẫn cài đặt & Chạy dự án

### Yêu cầu

- **.NET SDK 10** (`dotnet --version`)
- **SQL Server 2022** 
- **Docker Desktop**
- Git

### Clone repo
```powershell
git clone this repo
cd TicketFlow
```
### Cách 1

Yêu cầu: đã cài **SQL Server 2022** trên máy và bật SQL Server Authentication (login `sa` hoặc tài khoản SQL bất kỳ).

```powershell

# 1. Tạo file .env từ mẫu (điền giá trị thật, KHÔNG commit)
Copy-Item .env.example .env
#   → sửa ConnectionStrings__Default trỏ về SQL Server local của bạn, ví dụ:
#     Server=localhost,1433;Database=TicketFlow;User Id=sa;Password=<mat-khau>;TrustServerCertificate=True;
#   → đồng thời điền Jwt__SecretKey, Admin__Password

# 2. Chạy API (tự migrate DB + seed Admin khi Database__AutoMigrate=true)
dotnet run --project src/TicketFlow.Api
```

API chạy tại `http://localhost:5168`, Swagger UI tại `/swagger`. Database `TicketFlow` tự động được tạo qua EF Core migration khi `Database__AutoMigrate=true` (không cần tạo tay trong SSMS).

> 💡 Nếu dùng **Windows Authentication** thay vì `sa`, đổi connection string thành:
> `Server=your-server-name;Database=TicketFlow;Trusted_Connection=True;TrustServerCertificate=True;`

### Cách 2 — Chạy bằng Docker Compose (API + SQL Server)

```powershell
# 1. Tạo file .env từ mẫu (điền mật khẩu thật, KHÔNG commit)
Copy-Item .env.example .env

# 2. Sửa .env: SA_PASSWORD, Jwt__SecretKey, Admin__Password

# 3. Khởi động SQL Server + API
docker compose up --build
```

API chạy tại `http://localhost:8080`, Swagger UI tại `/swagger`. Migration EF Core tự động chạy khi `Database:AutoMigrate=true`.

### Áp dụng Migration thủ công (nếu cần)

```powershell
dotnet ef database update --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api
```

---

## ⚙️ Cấu hình môi trường (Configuration)

Cấu hình đọc từ `appsettings.json` và override bởi biến môi trường / file `.env` ở root (load tự động qua `DotNetEnv`).

| Biến môi trường | Mô tả | Mặc định |
|---|---|---|
| `ConnectionStrings__Default` | Connection string SQL Server | `appsettings.json` |
| `Jwt__SecretKey` | Khóa ký JWT HS256 (bắt buộc, ≥ 32 bytes) | `CHANGE_ME` |
| `Jwt__Issuer` / `Jwt__Audience` | Issuer & Audience của token | `TicketFlow` / `TicketFlow.Api` |
| `Admin__Email` / `Admin__Password` | Tài khoản Admin seed lần đầu | `admin@ticketflow.local` |
| `Booking__HoldDurationMinutes` | Thời gian giữ chỗ (phút) | `10` |
| `Booking__ReleaseWorkerIntervalSeconds` | Chu kỳ quét ghế quá hạn (giây) | `30` |
| `RateLimiting__AuthPermitLimit` / `AuthWindowSeconds` | Giới hạn request nhóm Auth | `10` / `60` |
| `RateLimiting__BookingPermitLimit` / `BookingWindowSeconds` | Giới hạn request nhóm Booking | `60` / `60` |
| `Database__AutoMigrate` | Tự chạy EF migration khi khởi động | `false` (compose: `true`) |
| `SA_PASSWORD` | Mật khẩu `sa` cho SQL Server container | Bắt buộc |

> ⚠️ **Bảo mật:** KHÔNG commit `.env`, connection string thật hoặc `Jwt__SecretKey` thật vào Git.

---

## 🔧 Cơ chế kỹ thuật chính

- **Optimistic Concurrency (`RowVersion`)** — Write path (Hold/Release/Confirm) load entity ở chế độ Tracking để EF Core đưa `RowVersion` vào `WHERE`; hai request đồng thời tranh một ghế → một thắng, một nhận `409 Conflict` (`ConcurrencyConflictException`).
- **Idempotent Confirm** — Client gửi header `Idempotency-Key`; server lưu kèm `RequestHash` (băm payload). Retry với cùng key + cùng payload trả lại đơn cũ; cùng key nhưng payload khác → `409`.
- **Refresh Token Rotation & Reuse Detection** — Mỗi lần refresh: token cũ bị `RevokedAtUtc` + `ReplacedByTokenHash`, phát hành cặp mới. Nếu token đã revoke bị dùng lại → **thu hồi toàn bộ phiên** của user + `401 TOKEN_REUSE_DETECTED`.
- **ExpiredSeatReleaseWorker** — `BackgroundService` dùng `PeriodicTimer` (không `Task.Delay`), tạo DI scope mới mỗi chu kỳ, thu hồi nguyên tử bằng `ExecuteUpdateAsync` với điều kiện `Status == Held && HeldUntilUtc <= utcNow`, bắt exception trong vòng lặp để tiếp tục chu kỳ.
- **State Machine nghiệp vụ** — `Event`: `Draft → Published → Cancelled` (không quay ngược); `Seat`: `Available ⇄ Held` với kiểm soát `HeldUntilUtc`.
- **RFC 7807 ProblemDetails** — Mọi lỗi 4xx/5xx qua `GlobalExceptionHandler` tập trung, kèm `code` định danh và `traceId`; không trả stack trace ở Production.

---

## ✅ Kiểm thử

```powershell
# Build toàn solution 
dotnet build TicketFlow.slnx

# Chạy toàn bộ unit tests (xUnit + Moq + FakeTimeProvider)
dotnet test TicketFlow.slnx

# Kiểm thử đồng thời Hold/Confirm (PowerShell)
./scripts/concurrency-test.ps1
```

Bộ test nằm trong `tests/TicketFlow.UnitTests` — phủ Auth, Event, Seat, Order services với `FakeTimeProvider` để kiểm soát thời gian.



