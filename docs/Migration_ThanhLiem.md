# Chuyển BE/FE task Đặng Thanh Liêm từ AcadPrep sang microservice

Nguồn: `AcadPrep/backend/src`. Đích: `AcadPrep_v2_Microservice/src`.
Phạm vi theo `docs/PhanCong_NhiemVu_5TV.md`, tổng cộng 12 use cases.

## Đối chiếu chức năng

| UC | Backend | Frontend |
|---|---|---|
| 9.1 Full Test | Exam lấy snapshot Published từ Content, tạo attempt + deadline | Exams/Detail → Exams/Take |
| 9.2 Practice | Lọc Part/topic, giữ nguyên nhóm câu/bài đọc, có/không giới hạn giờ | Detail → Take?mode=practice |
| 9.3 Audio | Snapshot chứa URL và mốc đầu/cuối, checkpoint vị trí nghe | Full: nghe một lần theo nhóm, không pause/tua; Practice: phát lại/tua trong đoạn |
| 9.4 Submit | Chấm trên snapshot, lưu kết quả và event chờ trong cùng transaction | Nộp chủ động hoặc hết giờ → Results |
| 9.5 Resume | Khôi phục đáp án, deadline và vị trí audio từ DB | Lịch sử học → Tiếp tục phiên |
| 10.1 Review | Chỉ trả answer key/lời giải sau nộp, kiểm tra chủ sở hữu | Exams/Results, Exams/Review; lọc đúng/sai/chưa làm |
| 10.2 Performance | Learning nhận TestSubmittedEvent, tổng hợp Listening/Reading và Part | Performance/PerformanceAnalysis |
| 10.3 Score progress | Learning sắp xếp điểm theo thời điểm thực sự nộp | Performance/ScoreProgress |
| 14.1 Study history | Exam trả timeline thi/luyện tập; Web ghép lịch sử vocab từ Learning | Performance/StudyHistory |
| 14.2 Attempts | Exam trả lượt Full Test đã nộp của tài khoản hiện tại | Performance/ExamAttempts |
| 14.3 Incorrect | Gom câu sai/chưa trả lời theo Part, giữ lần sai gần nhất/câu | Performance/IncorrectAnswers |
| 20.3 Exam statistics | Learning đọc tổng hợp từ Exam qua HTTP nội bộ, cache Redis 1 giờ | Admin/ExamStats |

Các trang Results, Review, StudyHistory, ExamAttempts và IncorrectAnswers giữ markup và JS review của bản cũ; PageModel được đổi thành HTTP client. Các trang analysis/progress/stats đã có trong project đích được nối với dữ liệu từ luồng thi mới.

## Biên giới dữ liệu

- Content sở hữu đề, câu hỏi, đáp án và media. `GET /internal/exam-papers/{id}` chỉ nhận khóa dịch vụ. Learner API không có answer key.
- Exam lưu bản chụp đề trong `EXAM_SESSION_STATES`, kèm đáp án cá nhân, checkpoint audio, thời hạn và kết quả. Không đọc DB của Content hoặc Learning.
- Các row `EXAM_ATTEMPTS`/`PRACTICE_SESSIONS` được giữ để tương thích schema hiện có. Với phiên mới, `SessionState.ExamId` là ID bên Content; row Exam nội bộ chỉ đáp ứng FK cũ. Mọi truy vấn lượt thi mới dùng ID Content từ SessionState, không suy diễn từ FK cũ.
- Learning lưu projection cho phân tích điểm qua `TestSubmittedEvent`; Practice dùng đúng/tổng câu và không trộn vào biểu đồ thang TOEIC.
- Web chỉ gọi service qua YARP/ToeicApiClient; không dùng EF Core, DbContext hoặc handler MediatR từ monolith.

Deadline UTC được cố định khi bắt đầu. Đóng trang không dừng đồng hồ. Sau deadline, server không nhận đáp án mới và chỉ chấm checkpoint đã lưu trước đó. Worker kiểm tra mỗi 5 giây để tự nộp phiên hết hạn, kể cả khi trình duyệt đã đóng. Cần thấy trạng thái **Saved** trước khi rời trang; khi mất mạng, đáp án chưa gửi được giữ trong tab và sessionStorage để thử lại trước deadline.

Revision trên SessionState phát hiện ghi đồng thời. Nộp lặp trả kết quả đã lưu. Snapshot giữ nguyên dù CMS thay câu hỏi hoặc đáp án sau lúc bắt đầu. Cách tính điểm Full Test giữ thuật toán xấp xỉ của project cũ, không phải bảng quy đổi chính thức ETS.

Audio Full Test được giới hạn bằng UI và checkpoint tăng dần; đây không phải DRM hoặc cơ chế chống can thiệp bằng devtools. Media URL vẫn do Content cung cấp. Cần kiểm tra bằng trình duyệt thật với bộ audio thực tế trước demo.

## API qua Gateway

`{kind}` là `full-tests` hoặc `practice-sessions`.

| Method | URL | Body/kết quả |
|---|---|---|
| POST | `/exam/api/exams/{examId}/full-test` | `{}` → `{sessionId}` |
| POST | `/exam/api/exams/{examId}/practice` | `{selectedPartNumbers:[5],selectedTags:[],timeLimitMinutes:null}` |
| GET | `/exam/api/{kind}/{id}` | Câu hỏi, đáp án cá nhân, audio checkpoint, deadline; không có answer key |
| PUT | `/exam/api/{kind}/{id}/checkpoint` | `{answers:{"123":"A"},audioProgress:{"q-123":{position:12,completed:false}}}` |
| POST | `/exam/api/{kind}/{id}/submit` | Cùng body checkpoint; trả kết quả, nộp lặp không chấm lại |
| GET | `/exam/api/{kind}/{id}/review` | Chỉ có khi đã nộp và đúng chủ sở hữu |
| GET | `/exam/api/study-history/me` | Timeline Full Test/Practice, bao gồm liên kết resume |
| GET | `/exam/api/attempts/me` | Full Test đã nộp |
| GET | `/exam/api/incorrect-answers/me` | Câu sai/chưa làm theo Part |
| GET | `/learning/api/performance/analysis` | JWT, phân tích tài khoản hiện tại |
| GET | `/learning/api/performance/progress` | JWT, tiến độ tài khoản hiện tại |
| GET | `/learning/api/performance/study-history` | JWT, timeline từ vựng |
| GET | `/learning/api/admin/learning/exam-stats` | JWT role Admin, phân trang và cache 1 giờ |

Checkpoint `answers` là patch theo QuestionId; null xóa lựa chọn. Chỉ nhận option có trong câu hỏi. Không nhận UserId từ body. Razor POST handlers có antiforgery; JWT và khóa khách không đưa xuống JavaScript.

JWT lấy UserId từ claims. Nếu bật `ExamSessions:AllowGuests`, API chấp nhận `X-Exam-Guest-Key` dạng UUID 32 ký tự hex; Web giữ bí mật đó trong session. Đổi khóa khách trả 404 khi đọc phiên người khác. Phiên khách có kết quả/review/lịch sử riêng nhưng không gửi điểm vào Learning.

## Tích hợp còn phụ thuộc thành viên khác

- **Quota Billing:** theo quyết định của Thanh Liêm, để điểm nối qua `IExamQuotaPolicy`. `PendingBillingQuotaPolicy` hiện cho phép bắt đầu. Khi Gia Huy có contract, thay registration bằng adapter có kiểm tra/reserve quota; chưa áp dụng hạn mức Free/Pro.
- **Identity role:** UC-20.3 yêu cầu JWT có claim role `Admin`. Identity scaffold hiện chưa phát role trong token; theo xác nhận của Thanh Liêm, để Khoa hoàn thiện việc gán role và phát role claim. Endpoint thống kê vẫn chặn user không có role. Không bỏ phân quyền để demo.
- Đây là migration **code**. Không tự copy tài khoản, attempts hay vocab từ database monolith. Phiên đang làm có sẵn trong Exam DB được mở bằng adapter legacy; lịch sử/review mới lấy từ SessionState. Nếu cần nhập dữ liệu monolith, phải chốt mapping UserId Identity riêng.

## Event, thống kê và cấu hình

`TestSubmittedEvent` được lưu cùng kết quả, có AttemptId GUID riêng và ExamId GUID ánh xạ ổn định từ ID Content. Outbox gửi lại mỗi 5 giây khi broker lỗi hoặc chưa có queue nhận; broker confirm trước khi đánh dấu đã gửi. Consumer Learning xử lý được cả hai overload sự kiện, reconnect khi broker xuất hiện muộn, chống nhân đôi attempt theo AttemptId và dùng thời điểm nộp gốc khi sự kiện đến trễ.

Thống kê đề lấy **số lượt đã nộp / số lượt bắt đầu**, không dùng độ chính xác câu trả lời để tính tỷ lệ hoàn thành. Điểm trung bình/cao/thấp chỉ tính lượt đã nộp. Learning cache từng trang thống kê 1 giờ; số mới có thể chưa hiện cho đến khi cache hết hạn.

| Service | Cấu hình | Giá trị local |
|---|---|---|
| Content | `InternalApiKey` | Khớp `Content:ApiKey` ở Exam |
| Exam | `Content:BaseUrl` | `http://localhost:5006/` |
| Exam | `Content:ApiKey`, `InternalApiKey` | Khớp Content và `Exam:ApiKey` ở Learning |
| Learning | `Exam:BaseUrl` | `http://localhost:5003/` |
| Learning | `Exam:ApiKey` | Khớp `InternalApiKey` ở Exam |

Appsettings có khóa mẫu local để chạy ngay. Khi triển khai, dùng biến môi trường tương ứng (`Content__ApiKey`, `InternalApiKey`, `Exam__ApiKey`); Docker Compose hỗ trợ `ACADPREP_INTERNAL_API_KEY`. Không dùng khóa mẫu ngoài môi trường local. URL media tương đối được giải quyết trên host Web, nên `/audio/sample-listening.wav` nằm trong `Storefront.Web/wwwroot/audio`.

## Chạy và kiểm tra

```bash
dotnet build ToeicLearning.sln -m:1
dotnet test ToeicLearning.sln -m:1
python3 scripts/verify-exam-migration.py
```

Script cuối tự chạy Content, Exam, Gateway và Web với SQLite tạm/cổng 25003, 25006, 25080, 25100; kiểm tra start/checkpoint/resume/submit/review, quyền sở hữu, antiforgery, các trang kết quả/lịch sử và file audio; sau đó dừng process và xóa DB tạm. Không chạy SQL Server/RabbitMQ/Redis thật và không thao tác dữ liệu development hiện có. Bộ test C# kiểm tra projection Learning và cache bằng SQLite/fake cache.

Chạy ứng dụng đầy đủ với hạ tầng: `./scripts/run-all.sh` hoặc `docker compose up --build`.
Đăng nhập để điểm Full Test được gửi vào Learning; dùng phiên khách để thử riêng luồng thi mà không cần Identity.

## Kết quả kiểm tra tại workspace

- Build solution: thành công, 0 warning / 0 error.
- C# tests: 25 Exam/Learning projection và 38 Content, tổng 63 ca đều qua.
- Smoke HTTP/Razor trên SQLite tạm: qua cả Full Test và Practice, checkpoint/resume, submit lặp, results/review/history, ownership, CSRF và truy cập file audio.
- `docker compose config --quiet`, kiểm tra cú pháp JavaScript và `git diff --check`: qua.
- Chưa chạy image Docker, SQL Server/RabbitMQ/Redis thật hoặc xác minh hành vi phát audio bằng trình duyệt thật.
