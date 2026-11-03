# Exam Service

Exam Service giữ một project `Exam`, chia trách nhiệm theo folder:

```text
Controllers → Services/Interfaces → Services
                                      ↓
                            Repositories/Interfaces
                                      ↓
                               Repositories
                                      ↓
                            Data/ExamDbContext → Database
```

- `Controllers` phụ thuộc `IExamAttemptService` và `IExamLifecycleService`, xử lý HTTP và danh tính người dùng.
- `Services` xử lý nghiệp vụ: kiểm tra điều kiện bắt đầu bài thi, nhóm câu hỏi luyện tập, tính thời hạn và truy vấn lượt thi/lịch sử làm bài. Service không truy cập EF Core hay `IExamDbContext`.
- `Repositories` thực hiện truy vấn EF Core và lưu dữ liệu, thông qua các interface riêng cho đề thi, câu hỏi, lượt thi và phiên luyện tập. Interface trả kết quả đã materialize, không trả `DbSet` hay `IQueryable` ra service.
- `Mappings/ExamMappingProfile.cs` cấu hình AutoMapper cho lịch sử làm bài, câu hỏi, passage và đáp án lựa chọn. DTO phiên thi không chứa đáp án đúng hoặc lời giải.
- `Program.cs` đăng ký service và repository theo lifetime `Scoped`; các repository dùng chung DbContext trong một request. AutoMapper được đăng ký qua `AddAutoMapper`.

Danh sách, tìm kiếm/lọc đề và chi tiết cấu trúc phía Learner (UC-3.1–3.4) do Content Service cung cấp tại `/api/exams`. Exam không còn GET danh sách hoặc chi tiết đề tại route này.

Exam giữ các API lượt thi:

- `GET /api/exams/{examId}/attempts/count`: tổng lượt thi, dùng bởi HTTP adapter của Content.
- `GET /api/exams/{examId}/attempts/me`: lịch sử của tài khoản trong JWT, yêu cầu đăng nhập. Không nhận UserId từ query/body.
- Các route bắt đầu Full Test/Practice và đọc phiên thi giữ nguyên.

`AddAsync` của repository lượt thi và phiên luyện tập lưu dữ liệu ngay bằng `SaveChangesAsync`, để ID đã được tạo khi service trả kết quả.

AutoMapper 16.2.0 đọc license key từ cấu hình `AutoMapper:LicenseKey` (biến môi trường `AutoMapper__LicenseKey`). Không đưa key vào source control. Xem [hướng dẫn license của AutoMapper](https://docs.automapper.io/en/stable/License-configuration.html).

Chạy kiểm thử từ thư mục gốc solution:

```bash
dotnet test tests/Exam.Tests/Exam.Tests.csproj
```

Luồng thi mới dùng `IExamPaperSource` lấy snapshot từ Content và `ISessionStateRepository`
lưu câu hỏi/đáp án/audio/deadline/kết quả vào `EXAM_SESSION_STATES`.
`IExamSessionService` còn phục vụ tương thích với phiên đang làm được tạo trước migration.
`SessionStateSchema` thêm bảng vào SQLite/SQL Server hiện có mà không xóa dữ liệu.
`SubmissionOutboxWorker` gửi sự kiện đã commit, retry khi broker/consumer chưa sẵn sàng.
`ExamExpiryWorker` nộp các phiên đã hết hạn dù người học đã đóng trình duyệt.
Xem [phạm vi Thanh Liêm và contracts](../../../docs/Migration_ThanhLiem.md).
