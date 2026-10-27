# Content Service

Content Service giữ toàn bộ code trong một project `Content`, chia trách nhiệm theo folder:

```text
Controllers → Services/Interfaces → Services
                                      ↓
                            Repositories/Interfaces
                                      ↓
                               Repositories
                                      ↓
                          Data/ContentDbContext → SQLite
```

- `Controllers` gọi `IContentService` cho CMS (`/api/cms/exams`) và `IExamService` cho Learner (`/api/exams`). Learner chỉ xem đề Published chưa bị xóa; DTO chỉ gồm metadata, cấu trúc Part và số lượt thi, không có câu hỏi/đáp án/lời giải.
- `Services` kiểm tra dữ liệu đầu vào, quyết định trạng thái, xóa mềm/xóa vĩnh viễn và khôi phục đề. Danh sách Learner hỗ trợ tìm tên đề, lọc series/năm và phân trang; chi tiết tổng hợp số câu, tên Part và topic tags từ cùng database CMS. Service gọi repository qua interface và không truy cập EF Core trực tiếp.
- `Repositories` thực hiện truy vấn và lưu dữ liệu, với `IExamRepository` và `IExamSeriesRepository`. Các method trả kết quả đã materialize, không đưa `DbSet` hoặc `IQueryable` ra service. `AddAsync`, `UpdateAsync`, `DeleteAsync` lưu ngay bằng `SaveChangesAsync`.
- `Data` chứa DbContext, interface DbContext và seeder; giữ nguyên schema và cấu hình SQLite.
- `Entities` chứa entity và enum; `DTOs` chứa hợp đồng request/response hiện tại.
- `Mappings/ContentMappingProfile.cs` cấu hình AutoMapper cho đề, bộ đề và nội dung lồng nhau. Repository dùng `ProjectTo` cho danh sách đề và bộ đề; service dùng `Map` cho chi tiết đề. DTO CMS giữ các trường đáp án đúng, lời giải và audio phục vụ quản trị.
- `Program.cs` đăng ký service/repository theo lifetime `Scoped`, AutoMapper qua `AddAutoMapper`, HTTP adapter qua `AddHttpClient`. Các repository dùng chung DbContext trong một request.

`IExamAttemptChecker` là adapter gọi Exam Service, không truy cập database của Exam. Cơ chế fallback số lượt thi demo trong `HttpExamAttemptChecker` được giữ nguyên khi endpoint chưa tồn tại hoặc không gọi được. Service dùng kết quả này để bổ sung số lượt thi và quyết định xóa mềm/xóa vĩnh viễn. Exam hiện đã cung cấp endpoint `/api/exams/{examId}/attempts/count`, nên khi kết nối thành công sẽ lấy số lượt thi thực tế. Lịch sử cá nhân thuộc Exam và được Storefront ghép riêng qua endpoint yêu cầu JWT `/api/exams/{examId}/attempts/me`.

API phía Learner:

```text
GET /api/exams?search=TOEIC&seriesName=ETS&year=2024&pageIndex=1&pageSize=8
GET /api/exams/{id}
```

Qua gateway, thêm tiền tố `/content`. `pageIndex` tối thiểu 1, `pageSize` từ 1–50. Response danh sách giữ `exams.items`, `pageNumber`, `totalPage`, `totalCount`, `hasPreviousPage`, `hasNextPage`, `seriesFilters`, `yearFilters`. Chi tiết trả 404 cho đề Draft, Hidden, đã xóa hoặc không tồn tại.

**Luồng đề thi:** Content cung cấp snapshot của đề Published cho Exam qua API nội bộ có khóa dịch vụ. Phiên mới dùng snapshot này, không cần đồng bộ ID câu hỏi giữa hai database. Chuyển code không tự nhập dữ liệu monolith đã có.

AutoMapper 16.2.0 đọc license key từ cấu hình `AutoMapper:LicenseKey` (biến môi trường `AutoMapper__LicenseKey`). Không đưa key vào source control. Xem [hướng dẫn license AutoMapper](https://docs.automapper.io/en/stable/License-configuration.html).

Chạy từ thư mục gốc solution:

```bash
dotnet run --project src/Services/Content --no-launch-profile
dotnet test tests/Content.Tests/Content.Tests.csproj
```

Bộ test dùng SQLite trong bộ nhớ, kiểm tra mapping, lọc/phân trang CMS và Learner, cấu trúc Part/tags, không lộ đáp án, CMS publish/ẩn có hiệu lực ngay cho Learner, tạo/sửa đề, đổi trạng thái, xóa/khôi phục, cascade dữ liệu và HTTP adapter/fallback demo.

## Tích hợp Exam Engine

Exam lấy đề Published tại `GET /internal/exam-papers/{id}` với header `X-Internal-Key`.
Endpoint này trả answer key cho service chấm bài; API learner tiếp tục không có đáp án.
Cấu hình `InternalApiKey` ở Content phải khớp `Content:ApiKey` ở Exam.
Exam lưu snapshot nên không cần copy DB hoặc đồng bộ ID câu hỏi của Content sang Exam.
