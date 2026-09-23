# TÀI LIỆU PHÂN CÔNG NHIỆM VỤ — ACADPREP
## Kiến trúc Microservice · Nhóm 05 thành viên

| Mục | Nội dung |
| :--- | :--- |
| **Dự án** | AcadPrep — Nền tảng hỗ trợ ôn luyện thi TOEIC trực tuyến |
| **Kiến trúc** | Microservice (5 bounded context / deployable services) |
| **Phiên bản tài liệu** | 1.0 |
| **Tham chiếu** | `Task.md` (phân hệ nghiệp vụ gốc), codebase `backend/` |

---

## 1. Đề tài (Mô tả đề tài)

### 1.1. Tên đề tài

**Xây dựng hệ thống nền tảng hỗ trợ ôn luyện thi TOEIC trực tuyến (AcadPrep) theo kiến trúc Microservice.**

### 1.2. Mô tả ngắn

AcadPrep là nền tảng web giúp người học ôn luyện TOEIC theo hướng **Freemium**: làm đề Full Test / Practice theo Part, ôn từ vựng (Flashcard + Spaced Repetition), theo dõi tiến độ và thành tích, đồng thời cung cấp **CMS** để Moderator/Admin quản trị ngân hàng đề–câu hỏi–đoạn văn, và **thanh toán VNPay** để nâng cấp gói Pro.

Hệ thống được phân rã thành **05 microservice** tương ứng 05 thành viên, giao tiếp qua **API Gateway**, **JWT** và **sự kiện (events)** để giảm chồng chéo schema/DB và tối ưu tích hợp.

### 1.3. Mục tiêu hệ thống

- Cho phép người học đăng ký/đăng nhập, làm bài thi mô phỏng TOEIC, xem kết quả và lịch sử.
- Hỗ trợ sổ tay từ vựng, flashcard, chuỗi ngày học (Study Streak) và huy hiệu.
- Cung cấp báo cáo hiệu suất cho Learner và thống kê tổng hợp cho Admin.
- Quản trị nội dung đề thi / câu hỏi / đoạn văn đọc hiểu với ràng buộc toàn vẹn dữ liệu.
- Tích hợp thanh toán VNPay, kiểm soát hạn mức Freemium, gửi thông báo hệ thống.
- Triển khai theo microservice: mỗi service có DB/domain riêng, hợp đồng API rõ ràng.

### 1.4. Công nghệ định hướng

| Lớp | Gợi ý kỹ thuật |
| :--- | :--- |
| Backend | .NET / Clean Architecture + CQRS (MediatR) |
| DB | SQL Server (schema tách theo service) |
| Auth | JWT / OAuth2 (Google) |
| Cache | Redis |
| Thanh toán | VNPay (HMAC-SHA512, IPN idempotent) |
| Tích hợp | API Gateway + Event bus (message between services) |
| Deploy | Docker Compose / container per service |

---

## 2. Danh sách Actor

| Mã | Actor | Mô tả | Role hệ thống |
| :---: | :--- | :--- | :--- |
| **A1** | **Guest** | Khách chưa đăng nhập: xem trang công khai, đăng ký, đăng nhập, quên mật khẩu | — |
| **A2** | **Learner** | Người học: làm bài, ôn vocab, xem tiến độ, nâng cấp Pro | `Learner` |
| **A3** | **Moderator** | Quản trị nội dung học liệu (CMS đề / câu hỏi / passage) | `Moderator` |
| **A4** | **Admin** | Quản trị toàn hệ thống: tài khoản, phân quyền, báo cáo, billing | `Admin` |

> **Ghi chú:** *Master Admin* không phải actor/role riêng — là ràng buộc đặc biệt trên một tài khoản `Admin` gốc (không cho hạ role / khóa). Hệ thống hiện có **3 role**: `Admin`, `Learner`, `Moderator`.

---

## 3. Danh sách chức năng (Use Cases)

### 3.1. Nhóm Identity & Tài khoản

| Mã UC | Tên chức năng | Mô tả ngắn |
| :--- | :--- | :--- |
| UC-1 | Register with Email | Đăng ký tài khoản bằng email |
| UC-2 | Verify OTP | Xác thực OTP 6 số để kích hoạt tài khoản |
| UC-5.1 | Login with Email | Đăng nhập email + mật khẩu |
| UC-5.2 | Login with Google | Đăng nhập OAuth2 Google, merge email trùng |
| UC-6 | Logout | Đăng xuất, hủy phiên / token |
| UC-7.1 | View Profile | Xem hồ sơ cá nhân |
| UC-7.2 | Edit Profile | Sửa tên hiển thị, ảnh đại diện |
| UC-7.3 | Change Password | Đổi mật khẩu |
| UC-8 | Forgot Password | Khôi phục mật khẩu qua OTP |
| UC-19.1 | View Accounts List | Admin xem danh sách tài khoản |
| UC-19.2 | View Account Detail | Admin xem chi tiết + lịch sử tài khoản |
| UC-19.3 | Update Account Status | Admin khóa / mở khóa tài khoản |
| UC-19.4 | Assign Roles | Admin gán role (Learner / Moderator / Admin) |

### 3.2. Nhóm Exam Engine (Làm bài & lịch sử thi)

| Mã UC | Tên chức năng | Mô tả ngắn |
| :--- | :--- | :--- |
| UC-3.1 | View Exams (Learner) | Xem danh sách đề thi công khai |
| UC-3.2 | View Exam Detail (Learner) | Xem cấu trúc / thời gian / số câu của đề |
| UC-3.3 | Search Exams (Learner) | Tìm đề theo từ khóa |
| UC-3.4 | Filter Exams (Learner) | Lọc đề theo năm, độ khó, lượt thi… |
| UC-9.1 | Start Full Test | Bắt đầu Full Test 7 Parts + đếm ngược |
| UC-9.2 | Practice by Part | Luyện tập theo từng Part 1–7 |
| UC-9.3 | Play Audio | Phát audio Listening; Full Test hạn chế tua/tạm dừng |
| UC-9.4 | Submit Test | Nộp bài (chủ động / hết giờ), chấm điểm, lưu |
| UC-9.5 | Resume Test | Tiếp tục bài thi bị gián đoạn |
| UC-10.1 | Review Test Results | Xem đúng/sai, đáp án, giải thích sau nộp |
| UC-14.1 | View Study History | Timeline lịch sử học tập |
| UC-14.2 | View Exam Attempts | Danh sách lượt thi + điểm |
| UC-14.3 | Review Incorrect Answers | Gom câu đã làm sai để ôn |

### 3.3. Nhóm Learning (Vocab, Gamification, Analytics)

| Mã UC | Tên chức năng | Mô tả ngắn |
| :--- | :--- | :--- |
| UC-4 | View Leaderboard | Bảng xếp hạng điểm / streak |
| UC-10.2 | View Performance Analysis | Phân tích mạnh/yếu Listening–Reading |
| UC-10.3 | View Score Progress | Biểu đồ tiến độ điểm qua các lần thi |
| UC-11.1 | Review Flashcards | Ôn thẻ từ vựng |
| UC-11.2 | Rate Memorization | Đánh giá nhớ → cập nhật Interval (SRS) |
| UC-11.3 | Save Vocabulary | Lưu từ vào sổ tay (chống trùng) |
| UC-11.4 | View Vocabulary List | Danh sách từ đã lưu |
| UC-11.5 | Remove Saved Vocabulary | Xóa từ khỏi sổ tay |
| UC-12.1 | View Vocabulary Passage | Xem ngữ cảnh đoạn văn của từ |
| UC-12.2 | Look Up Vocabulary | Tra cứu nghĩa / từ loại / phát âm / ví dụ |
| UC-13.1 | View Achievements | Xem huy hiệu đã đạt / điều kiện mở khóa |
| UC-13.2 | View Study Streak | Chuỗi ngày học (reset UTC+7 nếu không hoạt động) |
| UC-20.1 | View Learning Progress Reports | Admin: báo cáo tiến độ học tổng hợp |
| UC-20.2 | View User Statistics | Admin: đăng ký mới, kích hoạt, retention |
| UC-20.3 | View Exam Statistics | Admin: lượt làm, điểm TB, tỷ lệ hoàn thành đề |

### 3.4. Nhóm Content CMS

| Mã UC | Tên chức năng | Mô tả ngắn |
| :--- | :--- | :--- |
| UC-16.1 | View Exams (CMS) | Danh sách đề + trạng thái ẩn/hiện |
| UC-16.2 | Create Exam | Tạo đề mới (tên, part, thời gian) |
| UC-16.3 | View Exam Detail (CMS) | Chi tiết cấu trúc + thống kê đề |
| UC-16.4 | Update Exam | Sửa đề / câu hỏi / xuất bản |
| UC-16.5 | Delete Exam | Soft delete nếu đã có lịch sử làm bài |
| UC-16.6 | Search Exams (CMS) | Tìm đề (back-office) |
| UC-16.7 | Filter Exams (CMS) | Lọc đề theo trạng thái quản trị |
| UC-17.1 | View Questions | Danh sách ngân hàng câu hỏi |
| UC-17.2 | Create Question | Tạo câu hỏi + đáp án + gán Part 1–7 |
| UC-17.3 | View Question Detail | Chi tiết câu hỏi + đề đang nhúng |
| UC-17.4 | Update Question | Sửa nội dung / audio / đáp án |
| UC-17.5 | Delete Question | Xóa; cảnh báo nếu thuộc đề đang active |
| UC-17.6 | Search Questions | Tìm theo nội dung text |
| UC-17.7 | Filter Questions | Lọc theo Part / độ khó |
| UC-18.1 | View Passages | Danh sách đoạn văn đọc hiểu |
| UC-18.2 | Create Passage | Tạo đoạn văn mới |
| UC-18.3 | View Passage Detail | Xem chi tiết đoạn văn |
| UC-18.4 | Update Passage | Cập nhật đoạn văn |
| UC-18.5 | Delete Passage | Xóa; ràng buộc Part 6–7 không orphan |

### 3.5. Nhóm Billing & Notification

| Mã UC | Tên chức năng | Mô tả ngắn |
| :--- | :--- | :--- |
| UC-15 | View Notifications | Xem danh sách thông báo |
| UC-15.1 | Mark Notification as Read | Đánh dấu đã đọc |
| UC-21.1 | View Pricing Plans | Xem bảng giá Free / Pro (tháng, quý) |
| UC-21.2 | Checkout VNPay | Tạo Order Pending + redirect VNPay |
| UC-21.3 | Handle Payment Return | Xác minh chữ ký Return URL, kích hoạt Pro (idempotent) |
| UC-21.4 | Handle Payment IPN | Nhận IPN server-to-server từ VNPay |
| UC-21.5 | Enforce Freemium Quotas | Giới hạn Full Test / Practice / Vocab theo gói |
| UC-21.6 | Admin View Orders / Grant Pro | Admin xem order + cấp Pro thủ công |
| UC-22 | AI QnA (optional scope) | Hỗ trợ hỏi–đáp / trợ lý học tập (nếu giữ trong scope) |

**Tổng:** ~67 mục chức năng (60 UC nghiệp vụ gốc + Billing/Notification/AI bổ sung).

---

## 4. Ma trận chức năng — Actor

Ký hiệu: **●** = Actor chính thực hiện · **○** = Tham gia / bị ảnh hưởng · trống = không liên quan

### 4.1. Identity & Tài khoản

| Chức năng | Guest | Learner | Moderator | Admin |
| :--- | :---: | :---: | :---: | :---: |
| UC-1 Register | ● | | | |
| UC-2 Verify OTP | ● | | | |
| UC-5.1 Login Email | ● | | | |
| UC-5.2 Login Google | ● | | | |
| UC-6 Logout | | ● | ● | ● |
| UC-7.1 View Profile | | ● | ● | ● |
| UC-7.2 Edit Profile | | ● | ● | ● |
| UC-7.3 Change Password | | ● | ● | ● |
| UC-8 Forgot Password | ● | | | |
| UC-19.1 Accounts List | | | | ● |
| UC-19.2 Account Detail | | | | ● |
| UC-19.3 Update Status | | ○ | ○ | ● |
| UC-19.4 Assign Roles | | ○ | ○ | ● |

### 4.2. Exam Engine

| Chức năng | Guest | Learner | Moderator | Admin |
| :--- | :---: | :---: | :---: | :---: |
| UC-3.1 View Exams | ○ | ● | | |
| UC-3.2 Exam Detail | ○ | ● | | |
| UC-3.3 Search Exams | ○ | ● | | |
| UC-3.4 Filter Exams | ○ | ● | | |
| UC-9.1 Start Full Test | | ● | | |
| UC-9.2 Practice by Part | | ● | | |
| UC-9.3 Play Audio | | ● | | |
| UC-9.4 Submit Test | | ● | | |
| UC-9.5 Resume Test | | ● | | |
| UC-10.1 Review Results | | ● | | |
| UC-14.1 Study History | | ● | | |
| UC-14.2 Exam Attempts | | ● | | |
| UC-14.3 Incorrect Answers | | ● | | |

### 4.3. Learning

| Chức năng | Guest | Learner | Moderator | Admin |
| :--- | :---: | :---: | :---: | :---: |
| UC-4 Leaderboard | ○ | ● | | |
| UC-10.2 Performance Analysis | | ● | | |
| UC-10.3 Score Progress | | ● | | |
| UC-11.1–11.5 Flashcard / Vocab | | ● | | |
| UC-12.1–12.2 Tra cứu từ | | ● | | |
| UC-13.1 Achievements | | ● | | |
| UC-13.2 Study Streak | | ● | | |
| UC-20.1–20.3 Admin Reports | | | | ● |

### 4.4. Content CMS

| Chức năng | Guest | Learner | Moderator | Admin |
| :--- | :---: | :---: | :---: | :---: |
| UC-16.x Exam CMS | | | ● | ● |
| UC-17.x Question CMS | | | ● | ● |
| UC-18.x Passage CMS | | | ● | ● |

### 4.5. Billing & Notification

| Chức năng | Guest | Learner | Moderator | Admin |
| :--- | :---: | :---: | :---: | :---: |
| UC-15 / 15.1 Notifications | | ● | ● | ● |
| UC-21.1 Pricing | ○ | ● | | |
| UC-21.2 Checkout | | ● | | |
| UC-21.3 Payment Return | | ● | | |
| UC-21.4 Payment IPN | | ● | | |
| UC-21.5 Freemium Quotas | | ● | | |
| UC-21.6 Admin Billing | | | | ● |
| UC-22 AI QnA | | ● | | |

---

## 5. Phân công thành viên (theo ma trận & microservice)

### 5.1. Bảng tổng hợp

| TV | Họ tên | Microservice phụ trách | Nhóm UC chính | Actor phục vụ chính | 
| :---: | :--- | :--- | :--- | :--- | 
| **TV01** | **Gia Huy** | `identity-service` | UC-1, 2, 5.x, 6, 7.x, 8, 19.x | Guest, Learner, Moderator, Admin | 
| **TV02** | **Thanh Liêm** | `exam-engine-service` | UC-3.x, 9.x, 10.1, 14.x | Learner | 
| **TV03** | **Đăng Khoa** | `learning-service` | UC-4, 10.2–10.3, 11.x–13.x, 20.x | Learner, Admin | 
| **TV04** | **Tuấn Nguyên** | `content-service` | UC-16.x, 17.x, 18.x | Moderator, Admin | 
| **TV05** | **Khánh Duy** | `billing-service` | UC-15.x, 21.x, 22 | Learner, Admin | 

### 5.2. Chi tiết theo thành viên

#### TV01 — Gia Huy · Identity Service

- **Phạm vi:** Xác thực, JWT/OAuth, hồ sơ, phân quyền, quản trị tài khoản.
- **UC phụ trách:** UC-1, UC-2, UC-5.1, UC-5.2, UC-6, UC-7.1, UC-7.2, UC-7.3, UC-8, UC-19.1, UC-19.2, UC-19.3, UC-19.4.
- **DB / Entity gợi ý:** `Users`, `Roles`, OTP tokens, audit đăng nhập.
- **Cung cấp cho nhóm:** JWT / JWKS, claim `UserId` + `Role`; event `UserRegistered`, `AccountLocked`, `RoleChanged`.
- **Không phụ trách:** Notification (chuyển TV05).

#### TV02 — Thanh Liêm · Exam Engine Service

- **Phạm vi:** Bộ lõi làm bài Full Test / Practice, đếm ngược, resume, chấm điểm, lịch sử thi.
- **UC phụ trách:** UC-3.1–3.4, UC-9.1–9.5, UC-10.1, UC-14.1–14.3.
- **DB / Entity gợi ý:** `ExamAttempts`, `AttemptAnswers`, `PracticeSessions`.
- **Phụ thuộc:** Read API cấu trúc đề từ TV04; entitlement Freemium từ TV05 trước khi Start.
- **Publish event:** `TestSubmitted` → TV03 (streak/analytics), TV05 (notification nếu cần).

#### TV03 — Đăng Khoa · Learning Service

- **Phạm vi:** Vocab/Flashcard (SRS), Gamification, Analytics, Leaderboard, báo cáo Admin.
- **UC phụ trách:** UC-4, UC-10.2, UC-10.3, UC-11.1–11.5, UC-12.1–12.2, UC-13.1–13.2, UC-20.1–20.3.
- **DB / Entity gợi ý:** `Vocabulary`, `SavedVocabulary`, `StudyStreak`, `Achievements`, snapshot thống kê.
- **Consume event:** `TestSubmitted` từ TV02.
- **Logic bổ sung:** Cache ≥ 1 giờ cho báo cáo Admin; ma trận quy đổi điểm TOEIC (đồng bộ contract với TV02).

#### TV04 — Tuấn Nguyên · Content Service (CMS)

- **Phạm vi:** Back-office quản trị đề thi, ngân hàng câu hỏi, đoạn văn đọc hiểu.
- **UC phụ trách:** UC-16.1–16.7, UC-17.1–17.7, UC-18.1–18.5.
- **DB / Entity gợi ý:** `Exams`, `Questions`, `Passages`, `QuestionOptions` (source of truth nội dung).
- **Cung cấp:** OpenAPI/DTO cây `Exam → Passage → Question` cho TV02; event `ExamPublished` / `ContentUpdated`.
- **Ràng buộc:** Soft delete đề đã có attempt; Part 6–7 bắt buộc gắn Passage.

#### TV05 — Khánh Duy · Billing & Notification

- **Phạm vi:** VNPay, Freemium, Subscription, Notification (và AI QnA nếu trong scope).
- **UC phụ trách:** UC-15, UC-15.1, UC-21.1–21.6, UC-22 (nếu trong scope).
- **DB / Entity gợi ý:** `Plans`, `Orders`, `UserSubscriptions`, `PaymentWebhookLogs`, `Notifications`.
- **Cung cấp:** `CanStartTest` / quota check; kích hoạt Pro qua Return/IPN; kênh gửi thông báo.

### 5.3. Ma trận UC — Thành viên (tóm tắt)

| Nhóm chức năng | TV01 Gia Huy | TV02 Thanh Liêm | TV03 Đăng Khoa | TV04 Tuấn Nguyên | TV05 Khánh Duy |
| :--- | :---: | :---: | :---: | :---: | :---: |
| Identity & Account (UC-1…8, 19.x) | ● | | | | |
| Exam Engine (UC-3.x, 9.x, 10.1, 14.x) | | ● | | | |
| Learning / Analytics (UC-4, 10.2–13.x, 20.x) | | | ● | | |
| Content CMS (UC-16…18) | | | | ● | |
| Notification (UC-15.x) | | | | | ● |
| Billing / Freemium (UC-21.x) | | | | | ● |
| AI QnA (UC-22) | | | | | ● |

### 5.4. Pipeline phối hợp giữa các thành viên

```text
Client
  │
  ▼
  ├── identity-service     (TV01 · Gia Huy)
  ├── content-service      (TV04 · Tuấn Nguyên)
  ├── exam-engine-service  (TV02 · Thanh Liêm) ──read──► content-service
  ├── learning-service     (TV03 · Đăng Khoa)  ◄──event TestSubmitted── exam-engine
  └── billing-service      (TV05 · Khánh Duy)  ◄──quota check── exam / learning
```

1. **TV01 → mọi service:** chuẩn JWT; các service chỉ tin `UserId` / `Role` từ token.
2. **TV04 ↔ TV02:** chốt contract đề–câu hỏi sớm; exam-engine **không** ghi DB content.
3. **TV02 → TV03:** sau `Submit Test` phát `TestSubmitted` để cập nhật streak / performance / leaderboard.
4. **TV05 ↔ TV02 / TV03:** kiểm tra Freemium trước Start Test / Save Vocab; sau thanh toán phát `SubscriptionActivated`.
5. **TV05 → các service:** Notification khi OTP (phối hợp TV01), kết quả thi, hết hạn Pro, v.v.

### 5.5. Gợi ý thứ tự triển khai chung

| Phase | Việc ưu tiên |
| :---: | :--- |
| **P0** | TV01: Register/Login JWT · TV04: schema Exam/Question/Passage |
| **P1** | TV02: Start/Submit/Resume · TV03: Vocab + Streak · TV05: VNPay sandbox + Freemium |
| **P2** | Event `TestSubmitted` · CMS publish · Admin reports · Notification |
| **P3** | E2E integration · harden IPN · cache · soft-delete & toàn vẹn CMS |

---

## 6. Phụ lục

### 6.1. Role hệ thống (code hiện tại)

```text
Admin = 1
Learner = 2
Moderator = 3
```

### 6.2. Tài liệu liên quan trong repo

- `Task.md` — đặc tả UC gốc (chia 4 thành viên / monolith)
- `docs/Billing_VNPay.md` — mô hình Freemium + VNPay
- `docs/architecture/` — Clean Architecture & checklist
- `docs/Coding_Convention_v1.0.md` — quy ước code

---

*Tài liệu này dùng để phân công nhiệm vụ nhóm 05 thành viên theo hướng microservice. Có thể cập nhật khi điều chỉnh scope (ví dụ bỏ UC-22 AI QnA) hoặc đổi contract giữa các service.*
