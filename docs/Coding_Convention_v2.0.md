# Coding Convention v2.0 — ToeicLearning Microservices

> Áp dụng cho toàn bộ dự án **ToeicLearning Microservices** (Clean Architecture + Razor Pages + RabbitMQ + Redis + YARP).

---

## 1\. Quy định Chung (General)

### Git & Branching

- **Main branch**: `main` (Production), `develop` (Staging/Development).
- **Branch naming**:
  - `feat/feature-name`: Tính năng mới.
  - `fix/bug-name`: Sửa lỗi.
  - `refactor/name`: Tối ưu code.
- **Conventional Commits**:
  - `feat: ...`, `fix: ...`, `docs: ...`, `style: ...`, `refactor: ...`, `chore: ...`.
- **Pull Request**: Mỗi PR phải được ít nhất **1 thành viên khác review** trước khi merge. Không push thẳng lên `main` hoặc `develop`.

### General Naming

- Tên biến/hàm phải có ý nghĩa (Self-documenting). Tránh đặt `a`, `b`, `c`.
- Tên file C# = tên class/interface (1 type chính / file, trừ khi là nhóm record nhỏ như DTOs).

---

## 2\. Kiến trúc Solution (Microservices)

### Cấu trúc tổng quan

```text
src/
├── BuildingBlocks/                     # Shared libraries dùng chung
│   ├── BuildingBlocks.Common           # Result<T>, IntegrationEvent
│   └── BuildingBlocks.EventBus         # IEventBus, RabbitMQ implementation
├── Services/
│   ├── Identity/Identity.API           # Monolith nhỏ (không Clean Arch)
│   ├── Catalog/                        # Clean Architecture 4 layers
│   │   ├── Catalog.Domain
│   │   ├── Catalog.Application
│   │   ├── Catalog.Infrastructure
│   │   └── Catalog.API
│   ├── Exam/        (tương tự Catalog)
│   └── Progress/    (tương tự Catalog)
├── ApiGateways/ApiGateway              # YARP Reverse Proxy
└── Web/Storefront.Web                  # Razor Pages UI
```

### Quy tắc tổ chức Service

- **Mỗi service** là một bounded context độc lập, có **database riêng** (SQLite).
- Service tuân theo Clean Architecture (Catalog, Exam, Progress) có **4 project**:
  - `Domain` → `Application` → `Infrastructure` → `API`
- Service đơn giản (Identity) có thể dùng kiến trúc **monolith nhỏ** (1 project API duy nhất).

---

## 3\. Tiêu chuẩn Backend (C# / .NET 9)

### 3.1. Layer Domain

> **Namespace**: `{Service}.Domain.Entities`  
> **Dependency**: Không phụ thuộc bất kỳ layer nào khác.

- Chứa **Entities** (POCO classes).
- Không chứa logic phụ thuộc EF Core, HTTP, hoặc third-party.
- Mỗi entity đặt trong folder `Entities/`.
- **Naming**: PascalCase, danh từ số ít (`Course`, `Lesson`, `ExamAttempt`).

```csharp
// ✅ Đúng — Domain Entity
namespace Catalog.Domain.Entities;

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Lesson> Lessons { get; set; } = [];
}
```

### 3.2. Layer Application

> **Namespace**: `{Service}.Application.{Subfolder}`  
> **Dependency**: Chỉ phụ thuộc Domain + BuildingBlocks.Common.

| Folder | Nội dung |
|---|---|
| `Interfaces/` | Interface cho DbContext (ví dụ: `ICatalogDbContext`) |
| `Services/` | Application Services chứa business logic |
| `DTOs/` | Record classes cho input/output |
| `DependencyInjection.cs` | Extension method `AddApplication()` |

**Quy tắc**:
- Service trả về `Result<T>` (từ `BuildingBlocks.Common.Results`), **không throw exception** cho lỗi nghiệp vụ.
- Inject interface `I{Service}DbContext` (không inject trực tiếp DbContext class).
- DTO dùng **record** type, nhóm các DTO liên quan vào cùng file (ví dụ: `CourseDtos.cs`).
- Dùng **Primary Constructor** cho dependency injection.

```csharp
// ✅ Đúng — Application Service
public class CourseService(ICatalogDbContext db)
{
    public async Task<Result<List<CourseListItemDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.Courses.AsNoTracking()...;
        return Result.Success(items);
    }
}
```

```csharp
// ✅ Đúng — DTOs dùng record
public record CourseListItemDto(Guid Id, string Title, string Level, int TargetScore, int LessonCount, string Description);
public record CourseDetailDto(Guid Id, string Title, string Description, string Level, int TargetScore, List<LessonDto> Lessons);
```

### 3.3. Layer Infrastructure

> **Namespace**: `{Service}.Infrastructure.{Subfolder}`  
> **Dependency**: Phụ thuộc Application + Domain + BuildingBlocks.

| Folder | Nội dung |
|---|---|
| `Persistence/` | DbContext implementation, Seeder, EF Configuration |
| `Messaging/` | Integration Event handlers (`IIntegrationEventHandler<T>`) |
| `Caching/` | Redis cache implementation |
| `DependencyInjection.cs` | Extension method `AddInfrastructure(IConfiguration)` |

**Quy tắc EF Core**:
- Ưu tiên `.AsNoTracking()` cho truy vấn Read-only.
- Chỉ gọi `SaveChangesAsync()` **một lần** tại cuối Service/Action.
- Tách Seed data ra file riêng (ví dụ: `CatalogSeeder.cs`).

### 3.4. Layer API (Presentation)

> **Namespace**: `{Service}.API.Controllers`  
> **Dependency**: Phụ thuộc Application + Infrastructure (để DI registration).

| Folder | Nội dung |
|---|---|
| `Controllers/` | API Controllers |
| `Program.cs` | Host builder + Middleware pipeline |
| `Properties/` | launchSettings.json |

**Quy tắc Controller**:
- Inherit `ControllerBase`, đánh `[ApiController]` + `[Route("api/[controller]")]`.
- Controller **chỉ điều hướng** — không chứa logic nghiệp vụ. Gọi Service rồi map `Result<T>` sang HTTP status.
- Dùng **Primary Constructor** inject Service.
- Luôn truyền `CancellationToken` từ Action → Service.

```csharp
// ✅ Đúng — Controller mỏng
[ApiController]
[Route("api/[controller]")]
public class CoursesController(CourseService courseService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await courseService.GetAllAsync(ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await courseService.GetByIdAsync(id, ct);
        if (!result.IsSuccess) return NotFound(new { error = result.Error });
        return Ok(result.Value);
    }
}
```

### 3.5. Dependency Injection Convention

Mỗi layer có **1 static class `DependencyInjection`** với extension method:

| Layer | Method | Signature |
|---|---|---|
| Application | `AddApplication()` | `IServiceCollection → IServiceCollection` |
| Infrastructure | `AddInfrastructure(IConfiguration)` | `IServiceCollection → IServiceCollection` |

Gọi trong `Program.cs`:

```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
```

---

## 4\. BuildingBlocks (Shared Libraries)

### 4.1. BuildingBlocks.Common

| Folder | Nội dung |
|---|---|
| `Results/` | `Result`, `Result<T>` — pattern trả kết quả nghiệp vụ |
| `Events/` | `IntegrationEvent` base record + các event cụ thể (`ExamCompletedEvent`) |

**Quy tắc**:
- `IntegrationEvent` dùng **abstract record**, có `Id` + `OccurredOn`.
- Các event cụ thể kế thừa dùng **positional record**.
- `Result<T>` dùng cho **mọi** giá trị trả về từ Application Service.

### 4.2. BuildingBlocks.EventBus

| Folder | Nội dung |
|---|---|
| `Abstractions/` | `IEventBus`, `IIntegrationEventHandler<T>` |
| `RabbitMQ/` | `RabbitMqEventBus`, `RabbitMqOptions` |

**Quy tắc giao tiếp giữa services**:
- Giao tiếp async qua **RabbitMQ** (publish/subscribe `IntegrationEvent`).
- Service publish event → Service khác subscribe + handle.
- Handler đặt trong `Infrastructure/Messaging/`.
- Đăng ký subscription trong `DependencyInjection.SubscribeEvents()`.

```csharp
// Publish (Exam service)
await eventBus.PublishAsync(new ExamCompletedEvent(...));

// Subscribe (Progress service — trong DependencyInjection.cs)
bus.Subscribe<ExamCompletedEvent, ExamCompletedHandler>();
```

---

## 5\. Identity Service (Monolith nhỏ)

> Identity.API **không** dùng Clean Architecture 4 layers vì logic đơn giản.

| Folder | Nội dung |
|---|---|
| `Data/` | `AppUser`, `IdentityDbContext` |
| `Models/` | Request/Response DTOs (`AuthDtos.cs`) |
| `Services/` | `TokenService` (JWT generation) |
| `Controllers/` | Auth endpoints |

**Quy tắc**:
- Dùng **ASP.NET Identity** + JWT Bearer.
- Token secret, Issuer, Audience config trong `appsettings.json`.

---

## 6\. API Gateway (YARP)

- Dùng **YARP Reverse Proxy** để route request từ client đến microservices.
- Config routing trong `appsettings.json` (ReverseProxy section).
- Không chứa business logic — chỉ routing + forward headers.

| Path trên Gateway | Downstream Service |
|---|---|
| `/identity/*` | Identity `:5001` |
| `/catalog/*` | Catalog `:5002` |
| `/exam/*` | Exam `:5003` |
| `/progress/*` | Progress `:5004` |

---

## 7\. Frontend (Razor Pages — Storefront.Web)

### Cấu trúc Folder

| Folder | Nội dung |
|---|---|
| `Pages/` | Razor Pages theo feature folder (`Account/`, `Courses/`, `Exams/`, `Progress/`) |
| `Pages/Shared/` | Layout, Partial views dùng chung (`_Layout.cshtml`) |
| `Services/` | HTTP client gọi API qua Gateway (`ToeicApiClient.cs`) |
| `Models/` | View Models / DTOs cho frontend |
| `wwwroot/` | Static files (CSS, JS, images) |

### Quy tắc Razor Pages

- Mỗi trang có cặp `.cshtml` + `.cshtml.cs` (PageModel).
- **Không gọi API trực tiếp** từ Razor Page — gọi qua `ToeicApiClient` (inject service).
- Tổ chức Pages theo **feature folder** (không flat).
- Static files đặt trong `wwwroot/`.

### Naming Conventions

- **PageModel**: PascalCase, đặt cùng tên file `.cshtml` (ví dụ: `Index.cshtml.cs`).
- **View files**: PascalCase (ví dụ: `Login.cshtml`, `Details.cshtml`).
- **Feature folders**: PascalCase (ví dụ: `Account/`, `Courses/`).
- **Variables/Methods**: camelCase (private), PascalCase (public).

---

## 8\. RESTful API Convention

- URL: Số nhiều, chữ thường (`/api/courses`, `/api/exams`).
- Route constraint cho Guid: `{id:guid}`.
- Method:

| Method | Mục đích | Ví dụ |
|---|---|---|
| `GET` | Read | `GET /api/courses` |
| `POST` | Create | `POST /api/exams/submit` |
| `PUT` | Update All | `PUT /api/courses/{id}` |
| `PATCH` | Partial Update | `PATCH /api/courses/{id}` |
| `DELETE` | Delete | `DELETE /api/courses/{id}` |

---

## 9\. Security

- **Authentication**: JWT Bearer. Token cấp từ Identity.API, lưu trong Session (Razor Pages).
- **Validation**:
  - BE: Kiểm tra trong Application Service hoặc FluentValidation.
  - FE: Kiểm tra client-side trước khi gửi request.
- **Secrets**: Không push `.env` hoặc `appsettings.json` chứa Key/Secret lên Git.

---

## 10\. Result & Exception Pattern

- **Result Pattern**: Mọi Application Service trả về `Result<T>`. Không dùng exception cho lỗi logic (UserNotFound, InvalidInput...).
- **Exception**: Chỉ dùng cho lỗi bất khả kháng (DB sập, network lỗi). Bắt tập trung tại Middleware.
- Controller map `Result<T>` sang HTTP response:

```csharp
// ✅ Đúng
if (!result.IsSuccess) return NotFound(new { error = result.Error });
return Ok(result.Value);

// ❌ Sai — throw exception cho lỗi nghiệp vụ
throw new NotFoundException("Course not found");
```

---

## 11\. Quy trình Review & Testing

- **Pull Request (PR)**: Mỗi PR phải được ít nhất 1 thành viên khác review trước khi merge.
  Không push thẳng lên `main` hoặc `develop`, tạo nhánh riêng theo quy ước rồi tạo PR.
- **Unit Test**: Viết test cho các Logic nghiệp vụ phức tạp ở Application Service.
- **Verify**: Đảm bảo `dotnet build` pass toàn solution trước khi tạo PR.
