# Learning Microservice (`learning-service`)
**Hệ thống Nền tảng Ôn luyện TOEIC Trực tuyến — AcadPrep Microservices**  
**Thành viên phụ trách:** TV03 — Đăng Khoa  
**Port API:** `5005` (Swagger: `http://localhost:5005/swagger`)  
**Gateway Route:** `http://localhost:5080/learning/*`

---

## 1. Giới thiệu tổng quan

`learning-service` là phân hệ chuyên trách về **Thuật toán học thông minh (Spaced Repetition)**, **Hệ thống Gamification (Chuỗi ngày học & Danh hiệu)**, **Bảng xếp hạng (Leaderboard)**, **Phân tích hiệu suất học tập (Performance Analytics)** và **Báo cáo thống kê quản trị (Admin Dashboard)**.

Service được bóc tách từ hệ thống nguyên khối (Monolith) sang kiến trúc **Microservice chuẩn Clean Architecture 4 Layers**, giao tiếp bất đồng bộ qua **RabbitMQ Event Bus** và tối ưu hiệu năng bằng **Redis Cache**.

---

## 2. Bảng ánh xạ 15 Use Cases & Danh mục API Endpoints

| STT | Use Case | Nghiệp vụ chi tiết | Phương thức & URL | Ghi chú & Thuật toán |
|:---:|:---|:---|:---|:---|
| **1** | **View Leaderboard** | Bảng xếp hạng điểm & streak | `GET /api/leaderboard?sortBy=Score\|Streak` | Xếp hạng theo tổng điểm hoặc chuỗi ngày học; định danh hạng của user hiện tại |
| **2** | **View Performance Analysis** | Phân tích mạnh/yếu Listening - Reading | `GET /api/performance/analysis` | Điểm TB Nghe/Đọc và tỷ lệ thông thạo (% Mastery) từng Part 1 - 7 |
| **3** | **View Score Progress** | Biểu đồ tiến độ điểm thi qua các lần | `GET /api/performance/progress` | Danh sách lịch sử điểm thi; kiểm tra điều kiện tối thiểu 2 lần thi để vẽ biểu đồ |
| **4** | **Review Flashcards** | Ôn thẻ từ vựng Flashcards | `GET /api/vocabulary/flashcards?page=1&pageSize=10` | Lọc các từ đến hạn ôn (`NextReviewDate <= UtcNow`) |
| **5** | **Rate Memorization** | Đánh giá mức độ nhớ (Spaced Repetition SRS) | `POST /api/vocabulary/rate` | Nhớ: `Interval = Interval * 2`; Quên: `Interval = 1`; Tính ngày ôn tiếp theo |
| **6** | **Save Vocabulary** | Lưu từ vào sổ tay cá nhân | `POST /api/vocabulary/save` | Ràng buộc chống trùng lặp; tự động kích hoạt kiểm tra danh hiệu |
| **7** | **View Vocabulary List** | Danh sách các từ vựng đã lưu | `GET /api/vocabulary/notebook?searchTerm=...&page=1` | Phân trang, tìm kiếm theo từ khóa / định nghĩa; sắp xếp theo ngày lưu giảm dần |
| **8** | **Remove Saved Vocabulary**| Xóa từ khỏi sổ tay | `DELETE /api/vocabulary/{vocabId}` | Xóa từ khỏi sổ tay cá nhân |
| **9** | **View Vocabulary Passage** | Xem ngữ cảnh đoạn văn của từ | `GET /api/vocabulary/{vocabId}/passages` | Trả về định nghĩa + các đoạn văn đọc hiểu chứa từ vựng trong đề thi thực tế |
| **10** | **Look Up Vocabulary** | Tra cứu nghĩa / từ loại / ví dụ | `GET /api/vocabulary/lookup?keyword=...` | Tra cứu từ điển TOEIC theo từ khóa hoặc tiền tố |
| **11** | **View Achievements** | Xem danh sách huy hiệu & điều kiện | `GET /api/gamification/achievements` | Trạng thái đã mở khóa / ngày mở khóa / điều kiện mở khóa |
| **12** | **View Study Streak** | Chuỗi ngày học liên tục | `GET /api/gamification/streak` | Chuỗi hiện tại, kỷ lục streak; tự động reset về 0 sau 23:59:59 UTC+7 nếu bỏ lỡ |
| **13** | **Learning Progress Reports**| Admin: Báo cáo tiến độ học tổng hợp | `GET /api/admin/learning/reports?startDate=...&endDate=...` | **Lưu Cache Redis ≥ 1 giờ**; gom nhóm số lượt thi & điểm TB theo từng ngày |
| **14** | **View User Statistics** | Admin: Thống kê đăng ký mới, retention | `GET /api/admin/learning/user-stats` | **Lưu Cache Redis ≥ 1 giờ**; đếm đăng ký mới 30 ngày, người dùng tích cực 7 ngày |
| **15** | **View Exam Statistics** | Admin: Lượt làm, điểm TB, tỷ lệ hoàn thành | `GET /api/admin/learning/exam-stats?page=1&pageSize=10` | **Lưu Cache Redis ≥ 1 giờ**; thống kê chi tiết lượt thi, điểm Max/Min/TB từng đề |

---

## 3. Kiến trúc Microservice (Clean Architecture 4 Layers)

```text
src/Services/Learning/
├── Dockerfile                        # Multi-stage Docker build
├── Learning.Domain/                  # Layer 1: Entities cốt lõi
│   └── Entities/
│       ├── Vocabulary.cs
│       ├── VocabPassage.cs
│       ├── SavedVocabulary.cs
│       ├── StudyStreak.cs
│       ├── Achievement.cs
│       ├── UserAchievement.cs
│       ├── ExamAttemptRecord.cs
│       └── UserProfileSnapshot.cs
│
├── Learning.Application/             # Layer 2: Business Logic & Thuật toán
│   ├── Common/                       # ToeicScoreConverter (Bảng quy đổi điểm chuẩn ETS 0-990)
│   ├── DTOs/                         # Record DTOs bất biến
│   ├── Interfaces/                   # ILearningDbContext, ILearningCache
│   └── Services/
│       ├── LeaderboardService.cs
│       ├── PerformanceService.cs
│       ├── VocabularyService.cs
│       ├── GamificationService.cs
│       └── LearningAdminService.cs
│
├── Learning.Infrastructure/          # Layer 3: Hạ tầng & Tích hợp
│   ├── BackgroundJobs/               # StudyStreakResetWorker (Quét reset streak hàng giờ)
│   ├── Caching/                      # RedisLearningCache (StackExchange.Redis)
│   ├── Messaging/                    # TestSubmittedHandler (Consume RabbitMQ từ TV02)
│   └── Persistence/                  # LearningDbContext, LearningSeeder
│
└── Learning.API/                     # Layer 4: HTTP API Presentation
    ├── Controllers/
    │   ├── LeaderboardController.cs
    │   ├── PerformanceController.cs
    │   ├── VocabularyController.cs
    │   ├── GamificationController.cs
    │   └── AdminLearningController.cs
    ├── Properties/launchSettings.json (Port 5005)
    ├── appsettings.json
    └── Program.cs
```

---

## 4. Đặc tả Thuật toán & Cơ chế tích hợp

### 4.1. Thuật toán Lặp lại Ngắt quãng (Spaced Repetition System - SRS)
Khi người học đánh giá từ vựng trong tính năng Ôn thẻ Flashcards (`POST /api/vocabulary/rate`):
- **Nếu nhớ (`IsRemembered = true`):**
  $$\text{Interval}_{\text{new}} = \max(2, \text{Interval}_{\text{current}} \times 2)$$
  $$\text{NextReviewDate} = \text{UtcNow} + \text{Interval}_{\text{new}} \text{ ngày}$$
- **Nếu quên (`IsRemembered = false`):**
  $$\text{Interval}_{\text{new}} = 1$$
  $$\text{NextReviewDate} = \text{UtcNow} + 1 \text{ ngày}$$

### 4.2. Chuỗi ngày học liên tục (Study Streak) & Múi giờ UTC+7
- Thời gian hoạt động tính theo múi giờ Việt Nam (`UTC+7`).
- Nếu người học nộp bài thi hoặc học từ vựng trong ngày:
  - Nếu hoạt động lần đầu trong ngày và hôm qua có học: $\text{CurrentStreak} = \text{CurrentStreak} + 1$.
  - Nếu hôm qua không học (khoảng cách > 1 ngày): $\text{CurrentStreak} = 1$.
  - Tự động cập nhật $\text{MaxStreak} = \max(\text{MaxStreak}, \text{CurrentStreak})$.
- **Background Worker (`StudyStreakResetWorker`):** Định kỳ kiểm tra và reset $\text{CurrentStreak} = 0$ nếu người học bỏ lỡ ngày hôm qua.

### 4.3. Consume sự kiện `TestSubmitted` từ TV02 (`exam-engine-service`)
Khi TV02 hoàn tất chấm thi và publish `ExamCompletedEvent` hoặc `TestSubmittedEvent` lên RabbitMQ:
1. `TestSubmittedHandler` đón nhận sự kiện.
2. Lưu bản ghi `ExamAttemptRecord` vào database của `learning-service`.
3. Tự động tính toán cập nhật `StudyStreak`.
4. Kích hoạt kiểm tra điều kiện mở khóa danh hiệu (`CheckAndGrantAchievementsAsync`):
   - `First Blood`: Hoàn thành 1 bài thi đầu tiên.
   - `Streak Master`: Đạt chuỗi 7 ngày học liên tục.
   - `Word Collector`: Lưu từ 50 từ vựng vào sổ tay.
   - `Perfect Score`: Đạt 990 điểm TOEIC.
5. Invalidate bộ nhớ đệm Redis cho Bảng xếp hạng và Báo cáo Admin.

### 4.4. Cơ chế Caching Redis cho Báo cáo Admin (≥ 1 giờ)
- Tất cả truy vấn thống kê của Admin (`/api/admin/learning/reports`, `/api/admin/learning/user-stats`, `/api/admin/learning/exam-stats`) được lưu vào Redis Cache với thời gian hết hạn (`TTL = 1 giờ`).
- Khi có sự kiện thi mới hoặc thay đổi, cache liên quan sẽ được làm mới (invalidation), giúp giảm tải 95% áp lực truy vấn nặng trên cơ sở dữ liệu.

---

## 5. Hướng dẫn cài đặt & Khởi chạy

### Cách 1: Chạy trực tiếp bằng PowerShell / Bash
Yêu cầu: .NET 9 SDK, Docker Desktop (chạy RabbitMQ + Redis).

```powershell
# Chạy script tự động toàn bộ microservice
.\scripts\run-all.ps1
```

Hoặc chạy riêng Learning.API:
```bash
# 1. Khởi động RabbitMQ và Redis
docker compose up -d

# 2. Khôi phục và chạy Learning API
dotnet run --project src/Services/Learning/Learning.API
```

Sau khi chạy, truy cập:
- Swagger UI: **http://localhost:5005/swagger**
- Gateway Route: **http://localhost:5080/learning/swagger** hoặc qua các endpoints `http://localhost:5080/learning/api/...`
- RabbitMQ Management: **http://localhost:15672** (User/Pass: `guest` / `guest`)
- Health Check: **http://localhost:5005/health**

---

## 6. Ví dụ gọi API (Curl Samples)

### 1. Bảng xếp hạng (UC-4)
```bash
curl -X GET "http://localhost:5005/api/leaderboard?sortBy=Score"
```

### 2. Phân tích điểm mạnh/yếu Listening - Reading (UC-10.2)
```bash
curl -X GET "http://localhost:5005/api/performance/analysis?userId=user-demo-1"
```

### 3. Biểu đồ tiến độ điểm thi (UC-10.3)
```bash
curl -X GET "http://localhost:5005/api/performance/progress?userId=user-demo-1"
```

### 4. Lấy thẻ từ vựng đến hạn ôn Flashcards (UC-11.1)
```bash
curl -X GET "http://localhost:5005/api/vocabulary/flashcards?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer <TOKEN>"
```

### 5. Đánh giá nhớ từ vựng SRS (UC-11.2)
```bash
curl -X POST "http://localhost:5005/api/vocabulary/rate" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{"vocabularyId": 1, "isRemembered": true}'
```

### 6. Lưu từ vào sổ tay cá nhân (UC-11.3)
```bash
curl -X POST "http://localhost:5005/api/vocabulary/save" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{"vocabularyId": 2}'
```

### 7. Tra cứu nghĩa & ví dụ từ vựng (UC-12.2)
```bash
curl -X GET "http://localhost:5005/api/vocabulary/lookup?keyword=accommodate"
```

### 8. Xem đoạn văn ngữ cảnh của từ (UC-12.1)
```bash
curl -X GET "http://localhost:5005/api/vocabulary/1/passages"
```

### 9. Xem chuỗi ngày học Study Streak (UC-13.2)
```bash
curl -X GET "http://localhost:5005/api/gamification/streak?userId=user-demo-1"
```

### 10. Admin: Báo cáo tiến độ học tổng hợp (UC-20.1 - Cache Redis)
```bash
curl -X GET "http://localhost:5005/api/admin/learning/reports?startDate=2026-09-01&endDate=2026-10-13"
```

### 11. Admin: Thống kê người dùng & tỷ lệ retention (UC-20.2 - Cache Redis)
```bash
curl -X GET "http://localhost:5005/api/admin/learning/user-stats"
```

### 12. Admin: Thống kê số lượt làm bài & điểm số từng đề (UC-20.3 - Cache Redis)
```bash
curl -X GET "http://localhost:5005/api/admin/learning/exam-stats?pageNumber=1&pageSize=10"
```
