# TOEIC Learning Microservices

Project học TOEIC theo kiến trúc **microservices + Clean Architecture + Razor Pages**.

## Công nghệ demo

| Thành phần | Công nghệ |
|---|---|
| Frontend | ASP.NET Core **Razor Pages** |
| API Gateway | **YARP** Reverse Proxy |
| Auth | ASP.NET Identity + **JWT** |
| Catalog / Progress | **Clean Architecture** (Domain → Application → Infrastructure → API) |
| Content / Exam | Một project/service, chia folder Controllers → Services → Repositories → Data |
| Database | **SQL Server 2022** + EF Core |
| Message bus | **RabbitMQ** (`ExamCompletedEvent`) |
| Cache | **Redis** (Progress summary) |
| Orchestration local | `docker compose` + `scripts/run-all.sh` |

## Cấu trúc solution

```text
ToeicLearning.Microservices/
├── ToeicLearning.sln
├── docker-compose.yml          # SQL Server + RabbitMQ + Redis và một số app
├── scripts/run-all.sh / run-all.ps1
├── src/
│   ├── BuildingBlocks/
│   │   ├── BuildingBlocks.Common      # Result, IntegrationEvent
│   │   └── BuildingBlocks.EventBus    # RabbitMQ publisher/subscriber
│   ├── Services/
│   │   ├── Identity/Identity.API
│   │   ├── Catalog/   (Domain, Application, Infrastructure, API)
│   │   ├── Exam/      (một project: sessions, attempts)
│   │   ├── Content/   (một project: CMS và xem đề Learner)
│   │   ├── Progress/  (Domain, Application, Infrastructure, API)
│   │   └── Learning/  (Domain, Application, Infrastructure, API - TV03)
│   ├── ApiGateways/ApiGateway
│   └── Web/Storefront.Web             # Razor Pages UI
```

## Luồng demo (full stack)

```text
1. User đăng ký / đăng nhập trên Razor Pages
      → Identity.API cấp JWT (lưu Session)

2. Xem khóa học
      → Web → YARP `/catalog/*` → Catalog.API (Clean Arch + SQL Server seed)

3. Luyện đề → Chi tiết đề
      → Web → YARP `/content/api/exams` → Content (metadata, Part, tags)
      → lịch sử cá nhân: Web → `/exam/api/exams/{id}/attempts/me` → Exam (JWT)
   Start Full Test / Start Practice
      → Web → YARP `/exam/*` → Exam
      → lưu ExamAttempt / PracticeSession → trang làm bài
      → chọn đáp án, chuyển câu, bảng câu hỏi, đếm ngược tùy chế độ
      → chấm điểm, lưu kết quả, publish TestSubmittedEvent lên RabbitMQ

4. CMS Đề thi (Admin / Content Moderator)
      → Web → YARP `/content/*` → Content (/api/cms/exams)
      → CRUD đề thi, câu hỏi, bài đọc, series, publish / soft delete

5. Learning.API subscribe event TestSubmitted / ExamCompletedEvent
      → ghi ExamAttemptRecord
      → cập nhật StudyStreak (chuỗi ngày học theo UTC+7)
      → tự động đánh giá & mở khóa danh hiệu (Achievements)
      → invalidate / cache trên Redis

6. Xem Bảng xếp hạng & Tiến độ & Từ vựng Flashcards
      → Web → YARP `/learning/*` → Learning.API (đọc DB + Redis)
```

## Ports

| Service | URL |
|---|---|
| Storefront.Web | http://localhost:5100 |
| ApiGateway | http://localhost:5080 |
| Identity.API | http://localhost:5001/swagger |
| Catalog.API | http://localhost:5002/swagger |
| Exam | http://localhost:5003/swagger |
| Content | http://localhost:5006/swagger |
| Progress.API | http://localhost:5004/swagger |
| Learning.API (TV03) | http://localhost:5005/swagger |
| RabbitMQ UI | http://localhost:15672 (`guest` / `guest`) |
| Redis | localhost:6379 |

## Cách chạy (hướng dẫn)

### Yêu cầu

- .NET SDK **9**
- Docker và Docker Compose (cho SQL Server, RabbitMQ, Redis)

### Chỉ xem homepage (không cần database hoặc Docker)

```bash
cd AcadPrep_v2_Microservice
dotnet run --project src/Web/Storefront.Web --no-launch-profile
```

Mở http://localhost:5100. Homepage giữ UI từ monolith, không gọi API.
Font, icon, ảnh và Tailwind hiện tải từ CDN nên cần Internet để hiển thị đầy đủ.
Các trang đăng nhập, khóa học, luyện đề và tiến độ cần chạy backend theo các bước dưới.
Các thẻ đề, điểm số và bảng xếp hạng trên homepage là nội dung minh họa từ bản cũ;
thẻ đề dẫn tới danh sách đề của service mới, bảng xếp hạng chưa có chức năng.

### Bước 1 — Clone / copy project

```bash
# copy cả thư mục ToeicLearning.Microservices sang repo mới, rồi:
cd AcadPrep_v2_Microservice
dotnet restore
dotnet build
```

### Bước 2 — Hạ tầng

```bash
docker compose up -d sqlserver rabbitmq redis
```

### Bước 3 — Chạy tất cả service

```bash
chmod +x scripts/run-all.sh
./scripts/run-all.sh
```

Script đợi hạ tầng sẵn sàng rồi chạy 6 ứng dụng local. Ctrl+C dừng các ứng dụng;
container hạ tầng vẫn chạy, dùng `docker compose stop sqlserver rabbitmq redis` để dừng.
Không chạy toàn bộ `docker compose up -d` cùng script vì sẽ trùng cổng 5003/5080/5100.

Hoặc chạy từng project trong terminal riêng (thêm `--no-launch-profile` để dùng đúng port appsettings):

```bash
dotnet run --project src/Services/Identity/Identity.API --no-launch-profile
dotnet run --project src/Services/Catalog/Catalog.API --no-launch-profile
dotnet run --project src/Services/Exam --no-launch-profile
dotnet run --project src/Services/Content --no-launch-profile
dotnet run --project src/Services/Progress/Progress.API --no-launch-profile
dotnet run --project src/Services/Learning/Learning.API --no-launch-profile
dotnet run --project src/ApiGateways/ApiGateway --no-launch-profile
dotnet run --project src/Web/Storefront.Web --no-launch-profile
```

### Bước 4 — Demo trên UI

1. Mở http://localhost:5100  
2. Có thể làm bài bằng **phiên khách**, không cần chạy Identity.API hoặc đăng nhập
3. Vào **Khóa học** → xem seed data Catalog  
4. Vào **Luyện đề** → chọn đề → tab **Full Test** → **Start Full Test**.
5. Hoặc tab **Practice by Part** → chọn Part, topic (tùy chọn), thời gian (hoặc **No limit**) → **START PRACTICE**.
6. Chọn đáp án, dùng **Previous / Next** hoặc bảng số câu để di chuyển.

Full Test lấy toàn bộ câu hỏi và thời lượng từ đề (seed chuẩn: 200 câu, 120 phút;
một số đề seed rút gọn có ít câu hơn, UI hiển thị số câu thực tế).
Practice lọc theo Part và, nếu có chọn topic, chỉ lấy các câu khớp topic trong các Part đã chọn.
Giữ thứ tự nhóm câu hỏi và hiển thị ảnh, bài đọc chung (kể cả bộ nhiều bài đọc).
Khi hết giờ, UI khóa chọn đáp án. Mỗi lần Start tạo một phiên mới.

Đáp án tự lưu vào Exam DB; trạng thái `Saved`/lỗi kết nối hiển thị ngay trên trang.
Có thể tiếp tục qua **Lịch sử học**. Deadline do server quản lý và tiếp tục khi đóng trang;
worker tự nộp bài hết hạn, kể cả khi trình duyệt không còn mở.
Full Test phát audio từng câu/nhóm một lần, không có nút pause/tua; Practice hỗ trợ phát lại và tua trong đoạn.
Nộp bài mở trang Results và Review với đáp án, lời giải, bộ lọc đúng/sai/chưa trả lời.
Giao diện Results, Review, StudyHistory, ExamAttempts, IncorrectAnswers được chuyển từ monolith;
PageModel gọi HTTP qua Gateway, không tham chiếu DbContext hoặc MediatR của monolith.

Xem phạm vi 12 UC của **Đặng Thanh Liêm**, contract API và cách kiểm tra tại
[Migration Thanh Liêm](docs/Migration_ThanhLiem.md).


Identity dùng UserId chuỗi. Exam đồng bộ UserId chuỗi cho `EXAM_ATTEMPTS` và
`PRACTICE_SESSIONS`; khi khởi động, seeder tự đổi cột legacy `int` sang `nvarchar(450)`
nếu cần, giữ dữ liệu cũ. ID mẫu `1/2/3` không đại diện cho tài khoản Identity thực.
Sau khi cập nhật source, khởi động lại Content, Exam và Storefront.Web để dùng code mới.

## Gateway routing

| Path trên Gateway | Downstream |
|---|---|
| `/identity/*` | Identity `:5001` |
| `/catalog/*` | Catalog `:5002` |
| `/exam/*` | Exam `:5003` (sessions/attempts) |
| `/content/*` | Content `:5006` (CMS và danh sách/chi tiết đề Learner) |
| `/progress/*` | Progress `:5004` |
| `/learning/*` | Learning `:5005` |

Ví dụ: `GET http://localhost:5080/catalog/api/courses`

## Gợi ý mở rộng đề tài

- Thêm Listening audio / upload file (Object storage)
- Payment / Subscription service
- Duende IdentityServer thay JWT đơn giản
- MongoDB cho các service phù hợp
- Kubernetes manifests + CI/CD
- gRPC giữa Exam ↔ Catalog
- Unit / Integration / Contract tests

## JWT demo

Tất cả service dùng chung key (chỉ cho demo local):

```text
ToeicLearning_SuperSecret_Key_For_Demo_2026!
Issuer: ToeicLearning.Identity
Audience: ToeicLearning.Clients
```


## API xem danh sách và chi tiết đề (UC-3.1–3.4)

- `GET /content/api/exams?search=TOEIC&seriesName=ETS&year=2024&pageIndex=1&pageSize=8`
- `GET /content/api/exams/{id}`

Hai API đọc cùng database với CMS, chỉ trả đề Published chưa xóa. Các trường metadata, tổng số câu, Part và tags do Content quản lý. Tổng lượt thi lấy từ Exam qua HTTP; lịch sử cá nhân được Web ghép từ `GET /exam/api/exams/{id}/attempts/me` với JWT. CMS vẫn dùng `/content/api/cms/exams`.

Khi bắt đầu bài thi, Exam lấy bản chụp đề Published từ Content qua API nội bộ có khóa dịch vụ. Câu hỏi, đáp án, audio, passages và thời lượng trong bản chụp được giữ nguyên cho phiên đó. Không cần đồng bộ ID câu hỏi giữa hai database. Đổi đề trong CMS chỉ tác động các phiên bắt đầu sau lần đổi đó.

## API bắt đầu phiên và tải câu hỏi

Mặc định bật `ExamSessions:AllowGuests` trong Exam. UI tự tạo mã khách ngẫu nhiên
và giữ trong Session của trình duyệt; không cần chạy Identity.API để làm bài.
Khi gọi API trực tiếp, gửi header `X-Exam-Guest-Key` với một UUID dạng 32 ký tự hex
(ví dụ tạo bằng `python3 -c 'import uuid; print(uuid.uuid4().hex)'`). Giữ nguyên mã
cho cả POST và GET của cùng phiên; mã này là bí mật truy cập phiên, không dùng chung.
Thiếu/sai định dạng mã trả 401; dùng mã khác đọc phiên trả 404.

Nếu đã có JWT hợp lệ, API ưu tiên UserId từ JWT. Muốn bắt buộc đăng nhập sau này,
đặt `ExamSessions:AllowGuests` thành `false`. API không nhận UserId từ body/query.
Mã khách được lưu với tiền tố `guest:` để tách khỏi tài khoản Identity.
Xóa cookie hoặc hết hạn Session sẽ mất quyền truy cập phiên khách từ trình duyệt đó.

| Method | Path trên Gateway | Chức năng |
|---|---|---|
| POST | `/exam/api/exams/{examId}/full-test` | Tạo Full Test, trả 201 `{ "sessionId": ... }` |
| GET | `/exam/api/full-tests/{sessionId}` | Tải câu hỏi Full Test của tài khoản hiện tại |
| POST | `/exam/api/exams/{examId}/practice` | Tạo Practice, trả 201 `{ "sessionId": ... }` |
| GET | `/exam/api/practice-sessions/{sessionId}` | Tải câu hỏi Practice của tài khoản hiện tại |

Body Practice:

```json
{
  "selectedPartNumbers": [5, 6],
  "selectedTags": [],
  "timeLimitMinutes": 30
}
```

Phải chọn ít nhất một Part trong 1–7. `selectedTags: []` bỏ lọc topic.
`timeLimitMinutes: null` không giới hạn; nếu đặt, nhận số nguyên 1–120 phút.
Đề chưa publish, không có câu hỏi, lựa chọn không hợp lệ hoặc không khớp câu hỏi trả 400.
GET trả `examTitle`, `startedAt`, `expiresAt` (UTC/null), `questions` kèm options,
ảnh, passages và metadata audio; không trả đáp án đúng hoặc lời giải. GET khôi phục đáp án đã lưu và vị trí phát audio. Chỉ API review sau khi đã nộp mới trả đáp án/lời giải.

## Kiểm tra chức năng làm bài

```bash
dotnet test tests/Content.Tests
dotnet test tests/Exam.Tests
# Tự chạy 4 service với SQLite tạm, kiểm tra HTTP và Razor rồi dừng:
python3 scripts/verify-exam-migration.py
# Sau khi đã chạy Content, Exam, Gateway và Web (không cần Identity cho phiên khách):
python3 scripts/smoke-exam-sessions.py
# Hoặc chỉ định URL gateway và web riêng:
python3 scripts/smoke-exam-sessions.py http://localhost:5080 http://localhost:5100
```

Bộ test dùng SQLite in-memory để kiểm tra tạo phiên, quyền sở hữu, lựa chọn sai,
thời hạn UTC, bộ lọc Part/topic, bài đọc chung và không lộ đáp án.
Smoke test tạo phiên khách thử trong DB local; không gọi Identity.API.
Kiểm tra API qua gateway, quyền sở hữu mã khách, Razor Pages không đăng nhập,
antiforgery token và render trang câu hỏi của cả hai chế độ.

Các API lưu phiên/nộp/review và lịch sử được mô tả trong [Migration Thanh Liêm](docs/Migration_ThanhLiem.md).
Để chạy toàn bộ các service cần cho task này bằng Docker: `docker compose up --build`.
Quota Billing hiện dùng `PendingBillingQuotaPolicy` theo phạm vi đã thống nhất; adapter thực tế sẽ thay registration `IExamQuotaPolicy`.
