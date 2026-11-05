# Seed dữ liệu demo cho các service

Chạy từ thư mục `AcadPrep_v2_Microservice`:

```bash
dotnet run --project tools/SeedDemoData -- --local-sql
```

`--local-sql` lấy kết nối SQL Server local của Identity, dùng cùng server cho các database `AcadPrep_IdentityDb`, `AcadPrep_ExamDb`, `AcadPrep_CatalogDb`, `AcadPrep_LearningDb`, `AcadPrep_ProgressDb`. Content dùng SQLite theo cấu hình của Content.API. Các service phải trỏ vào chính các database này để thấy dữ liệu. Tool không khởi động API hoặc container. Docker Compose lưu SQL Server trong named volume `acadprep-sqlserver-data`, vì vậy tạo lại container hoặc `docker compose down` không làm mất dữ liệu. `docker compose down -v` sẽ xóa volume và cần seed lại.

Không có flag: đọc cấu hình từng service (`appsettings.json`, `appsettings.Development.json`). Có thể override kết nối bằng `Identity__ConnectionStrings__Default`, `Exam__ConnectionStrings__Default`, `Catalog__ConnectionStrings__Default`, `Learning__ConnectionStrings__Default`, `Progress__ConnectionStrings__Default`, `Content__ConnectionStrings__Sqlite`.

Chạy thử trên các database SQLite riêng, không đụng database local:

```bash
dotnet run --project tools/SeedDemoData -- --sqlite-dir /tmp/acadprep-demo-test
```

Tool chỉ thêm bản ghi còn thiếu. Không xóa dữ liệu; không đổi mật khẩu, role hoặc trạng thái tài khoản đã tồn tại. Nếu email demo đã tồn tại với ID khác, các dữ liệu mới dùng ID thực tế của email đó. Có thể chạy lại sau lỗi; các bảng được lưu theo từng nhóm. Nên chỉ chạy một tiến trình seed tại một thời điểm.

## Tài khoản

Mật khẩu cho **tài khoản demo mới**: `Demo@123456`.

| Email | Role / trạng thái |
| --- | --- |
| `hana@acadprep.com` | Learner, Active |
| `minh@acadprep.com` | Learner, Active |
| `hoang@acadprep.com` | Learner, Active |
| `lananh@acadprep.com` | Learner, Active |
| `duyliem@acadprep.com` | Learner, Active |
| `tuananh@acadprep.com` | Learner, Active |
| `moderator.demo@example.test` | Moderator, Active |
| `admin.demo@example.test` | Admin, Active |
| `banned.demo@example.test` | Learner, Banned — kiểm tra từ chối đăng nhập |
| `pending.demo@example.test` | Learner, PendingVerification — kiểm tra UI chưa xác minh |

Tool không gửi email hoặc cấp OTP cố định. Các địa chỉ `example.test` là dữ liệu giả lập. Tài khoản hệ thống `admin@acadprep.com` dùng seed sẵn có của project, không bị đổi mật khẩu.

## Dữ liệu bổ sung

- Content và Exam: cùng đề `AcadPrep Demo — Office English Full Test`, 200 câu với số lượng từng Part là 6/25/39/30/30/16/54, đáp án và nhóm câu/đoạn văn. ID nội bộ của hai database độc lập.
- Exam: mỗi learner có 4 lượt thi đã nộp, 200 câu trả lời/lượt, điểm tăng dần.
- Learning: 15 từ mới cộng 10 từ có sẵn, sổ từ vựng, flashcard đến hạn/chưa đến hạn, streak, huy hiệu, dữ liệu phân tích theo Part cho cùng learner.
- Progress: cùng AttemptId, UserId, điểm và thời gian với lịch sử Learning. Các ID GUID của dữ liệu demo được tạo ổn định từ ID int của Exam; đây là ánh xạ của công cụ seed, không thay đổi hợp đồng API/event.

Đề được tạo để thử giao diện và luồng thao tác; nội dung có mẫu lặp lại. Audio dùng clip mẫu có sẵn và không phải bản thu đầy đủ cho 200 câu. Các ngày học được tính khi thêm lần đầu, không tự cập nhật khi seed lại.
