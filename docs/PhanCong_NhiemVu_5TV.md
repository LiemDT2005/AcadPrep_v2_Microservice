UC-1	Register with Email	Dương Châu Đăng Khoa	2026-10-01	2026-10-16	Completed	identity-service · Đăng ký tài khoản email · DB: Users, OTP
UC-2	Verify OTP	Dương Châu Đăng Khoa	2026-10-01	2026-10-16	Completed	identity-service · Xác thực OTP 6 số kích hoạt tài khoản
UC-5.1	Login with Email	Dương Châu Đăng Khoa	2026-10-01	2026-10-16	Completed	identity-service · Đăng nhập email + password (JWT)
UC-5.2	Login with Google	Dương Châu Đăng Khoa	2026-10-08	2026-10-16	Completed	identity-service · Đăng nhập OAuth2 Google, merge email trùng
UC-6	Logout	Dương Châu Đăng Khoa	2026-10-08	2026-10-16	Completed	identity-service · Đăng xuất, revoking JWT/refresh token
UC-7.1	View Profile	Dương Châu Đăng Khoa	2026-10-12	2026-10-16	Completed	identity-service · Xem thông tin hồ sơ cá nhân
UC-7.2	Edit Profile	Dương Châu Đăng Khoa	2026-10-16	2026-10-21	In Progress	identity-service · Cập nhật tên hiển thị, avatar
UC-7.3	Change Password	Dương Châu Đăng Khoa	2026-10-16	2026-10-21	In Progress	identity-service · Đổi mật khẩu tài khoản
UC-8	Forgot Password	Dương Châu Đăng Khoa	2026-10-16	2026-10-21	In Progress	identity-service · Quên mật khẩu & khôi phục qua OTP
UC-19.1	View Accounts List	Dương Châu Đăng Khoa	2026-11-01	2026-11-07	Not Started	identity-service · Admin xem danh sách tài khoản toàn hệ thống
UC-19.2	View Account Detail	Dương Châu Đăng Khoa	2026-11-01	2026-11-07	Not Started	identity-service · Admin xem chi tiết & lịch sử tài khoản
UC-19.3	Update Account Status	Dương Châu Đăng Khoa	2026-11-01	2026-11-07	Not Started	identity-service · Admin khóa / mở khóa tài khoản (Event AccountLocked)
UC-19.4	Assign Roles	Dương Châu Đăng Khoa	2026-11-01	2026-11-07	Not Started	identity-service · Admin gán role (Learner, Moderator, Admin)
UC-20.3	View Exam Statistics	Đặng Thanh Liêm	2026-11-08	2026-11-14	Completed	learning-service · Admin: Lượt làm, điểm TB, tỷ lệ hoàn thành đề
UC-9.1	Start Full Test	Đặng Thanh Liêm	2026-10-16	2026-10-21	In Progress	exam-engine-service · Bắt đầu Full Test 7 Parts + đếm ngược (Check quota TV05)
UC-9.2	Practice by Part	Đặng Thanh Liêm	2026-10-16	2026-10-21	In Progress	exam-engine-service · Luyện tập theo từng Part 1–7
UC-10.2	View Performance Analysis	Đặng Thanh Liêm	2026-10-15	2026-10-21	In Progress	learning-service · Phân tích mạnh/yếu Listening-Reading
UC-10.3	View Score Progress	Đặng Thanh Liêm	2026-10-15	2026-10-21	In Progress	learning-service · Biểu đồ tiến độ điểm thi qua các lần
UC-9.3	Play Audio	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Phát audio Listening (Full Test hạn chế tua)
UC-9.4	Submit Test	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Nộp bài, chấm điểm, lưu DB & Publish TestSubmitted
UC-9.5	Resume Test	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Tiếp tục bài thi gián đoạn
UC-10.1	Review Test Results	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Xem đúng/sai, đáp án, giải thích chi tiết
UC-14.1	View Study History	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Timeline lịch sử thi & làm bài
UC-14.2	View Exam Attempts	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Danh sách các lượt thi + điểm số
UC-14.3	Review Incorrect Answers	Đặng Thanh Liêm	2026-10-22	2026-10-31	Not Started	exam-engine-service · Tập hợp các câu làm sai để ôn tập
UC-15.1	View Notifications	Đỗ Hồ Gia Huy	2026-10-07	2026-10-16	Completed	notification-service · Xem danh sách thông báo hệ thống
UC-15.2	Mark Notification as Read	Đỗ Hồ Gia Huy	2026-10-07	2026-10-16	Completed	notification-service · Đánh dấu thông báo đã đọc
UC-15.3	Delete Notification	Đỗ Hồ Gia Huy	2026-10-07	2026-10-16	Completed	notification-service · Xóa thông báo khỏi danh sách của người dùng
UC-15.4	Send System Notification	Đỗ Hồ Gia Huy	2026-10-07	2026-10-16	Completed	notification-service · Admin gửi thông báo toàn hệ thống / theo nhóm người dùng
UC-21.1	View Pricing Plans	Đỗ Hồ Gia Huy	2026-10-16	2026-10-21	In Progress	billing-service · Xem bảng giá gói Free / Pro
UC-21.2	Checkout VNPay	Đỗ Hồ Gia Huy	2026-10-16	2026-10-21	In Progress	billing-service · Tạo Order Pending + redirect cổng VNPay
UC-21.3	View Order History	Đỗ Hồ Gia Huy	2026-10-16	2026-10-21	In Progress	billing-service · Xem lịch sử đơn hàng và giao dịch đã thanh toán
UC-21.4	View Orders / Grant Pro	Đỗ Hồ Gia Huy	2026-10-22	2026-10-31	Not Started	billing-service · Admin xem danh sách order & cấp Pro thủ công
UC-21.5	View Revenue Statistics	Đỗ Hồ Gia Huy	l	2026-11-07	Not Started	billing-service · Admin: Thống kê doanh thu, số đơn theo trạng thái (Cache Redis)
UC-22.1	AI QnA (optional scope)	Đỗ Hồ Gia Huy	2026-10-22	2026-10-31	Not Started	ai-service · Trợ lý AI hỏi đáp học tập (Scope mở rộng)
UC-22.2	Manage AI Token Usage	Đỗ Hồ Gia Huy	2026-11-01	2026-11-07	Not Started	ai-service · Admin: Theo dõi, giới hạn và reset token AI theo tài khoản (phụ thuộc UC-22.1)
UC-23.1	View Subscription Details	Đỗ Hồ Gia Huy	2026-10-22	2026-10-31	Not Started	billing-service · Xem gói hiện tại, trạng thái, ngày hết hạn
UC-23.2	View Usage Limits	Đỗ Hồ Gia Huy	2026-10-22	2026-10-31	Not Started	billing-service · Xem hạn mức và lượng sử dụng còn lại (Full Test, Practice, Vocab; token AI lấy từ ai-service)
UC-23.3	Renew Subscription	Đỗ Hồ Gia Huy	2026-11-01	2026-11-07	Not Started	billing-service · Gia hạn / mua lại gói Pro, tái dùng Checkout VNPay; job nhắc gia hạn qua email
UC-16.1	View Exams (CMS)	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS xem danh sách đề + trạng thái ẩn/hiện
UC-16.2	Create Exam	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS tạo đề thi mới (tên, part, thời gian)
UC-16.3	View Exam Detail (CMS)	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS xem chi tiết cấu trúc & thống kê đề
UC-16.4	Update Exam	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS cập nhật đề / xuất bản đề thi
UC-16.5	Delete Exam	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS xóa đề (Soft delete nếu đã có attempt)
UC-16.6	Search Exams (CMS)	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS tìm kiếm đề thi
UC-16.7	Filter Exams (CMS)	Hồng Tuấn Nguyên	2026-10-01	2026-10-14	Completed	content-service · CMS lọc đề theo trạng thái quản trị
UC-17.1	View Questions	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Danh sách ngân hàng câu hỏi
UC-17.2	Create Question	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Tạo câu hỏi + đáp án + gán Part 1–7
UC-17.3	View Question Detail	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Chi tiết câu hỏi + các đề đang nhúng
UC-17.4	Update Question	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Cập nhật câu hỏi / audio / đáp án
UC-17.5	Delete Question	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Xóa câu hỏi (Cảnh báo nếu thuộc đề active)
UC-17.6	Search Questions	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Tìm câu hỏi theo từ khóa text
UC-17.7	Filter Questions	Hồng Tuấn Nguyên	2026-10-14	2026-10-16	Completed	content-service · Lọc câu hỏi theo Part 1–7 và độ khó
UC-3.1	View Exams (Learner)	Hồng Tuấn Nguyên	2026-10-07	2026-10-16	In Progress	content-service · Xem danh sách đề thi công khai
UC-3.2	View Exam Detail (Learner)	Hồng Tuấn Nguyên	2026-10-07	2026-10-16	In Progress	content-service · Xem cấu trúc/thời gian/số câu của đề
UC-3.3	Search Exams (Learner)	Hồng Tuấn Nguyên	2026-10-07	2026-10-16	In Progress	content-service · Tìm kiếm đề thi phía Learner
UC-3.4	Filter Exams (Learner)	Hồng Tuấn Nguyên	2026-10-07	2026-10-16	In Progress	content-service · Lọc đề theo năm, độ khó, lượt thi
UC-18.1	View Passages	Hồng Tuấn Nguyên	2026-10-16	2026-10-21	In Progress	content-service · Danh sách đoạn văn đọc hiểu Part 6–7
UC-18.2	Create Passage	Hồng Tuấn Nguyên	2026-10-16	2026-10-21	In Progress	content-service · Tạo đoạn văn mới
UC-18.3	View Passage Detail	Hồng Tuấn Nguyên	2026-10-16	2026-10-21	In Progress	content-service · Xem chi tiết đoạn văn đọc hiểu
UC-18.4	Update Passage	Hồng Tuấn Nguyên	2026-10-16	2026-10-21	In Progress	content-service · Cập nhật nội dung đoạn văn
UC-18.5	Delete Passage	Hồng Tuấn Nguyên	2026-10-16	2026-10-21	In Progress	content-service · Xóa đoạn văn (Ràng buộc Part 6-7 không orphan)
UC-11.3	Save Vocabulary	Hứa Khánh Duy	2026-10-10	2026-10-16	Completed	learning-service · Lưu từ vào sổ tay cá nhân
UC-11.4	View Vocabulary List	Hứa Khánh Duy	2026-10-10	2026-10-18	Completed	learning-service · Danh sách các từ vựng đã lưu
UC-11.5	Remove Saved Vocabulary	Hứa Khánh Duy	2026-10-10	2026-10-18	Completed	learning-service · Xóa từ khỏi sổ tay
UC-12.1	View Vocabulary Passage	Hứa Khánh Duy	2026-10-10	2026-10-18	Completed	learning-service · Xem ngữ cảnh đoạn văn của từ
UC-12.2	Look Up Vocabulary	Hứa Khánh Duy	2026-10-22	2026-10-31	Completed	learning-service · Tra cứu nghĩa / từ loại / ví dụ
UC-13.1	View Achievements	Hứa Khánh Duy	2026-11-01	2026-11-07	Completed	learning-service · Xem danh sách huy hiệu & điều kiện mở khóa
UC-13.2	View Study Streak	Hứa Khánh Duy	2026-11-01	2026-11-07	Completed	learning-service · Chuỗi ngày học (Consume TestSubmitted từ TV02)
UC-20.1	View Learning Progress Reports	Hứa Khánh Duy	2026-11-08	2026-11-14	Completed	learning-service · Admin: Báo cáo tiến độ học tổng hợp (Cache Redis)
UC-20.2	View User Statistics	Hứa Khánh Duy	2026-11-08	2026-11-14	Completed	learning-service · Admin: Thống kê đăng ký mới, retention
UC-4	View Leaderboard	Hứa Khánh Duy	2026-10-15	2026-10-21	In Progress	learning-service · Bảng xếp hạng điểm & streak
UC-11.1	Review Flashcards	Hứa Khánh Duy	2026-10-10	2026-10-16	In Progress	learning-service · Ôn thẻ từ vựng Flashcards
UC-11.2	Rate Memorization	Hứa Khánh Duy	2026-10-10	2026-10-16	In Progress	learning-service · Đánh giá mức độ nhớ → thuật toán SRS (Spaced Repetition)