<#
.SYNOPSIS
    Script kiểm tra tự động các API của Learning Service (TV03) trên AcadPrep Microservices.
.DESCRIPTION
    Script thực hiện gọi kiểm tra các API endpoints trực tiếp (port 5005) và qua API Gateway (port 5080).
#>

param(
    [string]$BaseUrl = "http://localhost:5005",
    [string]$GatewayUrl = "http://localhost:5080/learning"
)

$passed = 0
$failed = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [object]$Body = $null
    )

    Write-Host -NoNewline "[$Method] $Name ($Url) ... "
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            ContentType = "application/json"
            ErrorAction = "Stop"
        }
        if ($Body) {
            $params["Body"] = ($Body | ConvertTo-Json)
        }
        $res = Invoke-RestMethod @params
        Write-Host "PASS" -ForegroundColor Green
        $global:passed++
        return $res
    }
    catch {
        Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $global:failed++
        return $null
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     KIỂM TRA TỰ ĐỘNG LEARNING SERVICE (TV03 - ACADPREP)  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Đang kiểm tra kết nối tới: $BaseUrl`n"

# 1. Health Check
Test-Endpoint -Name "Health Check" -Url "$BaseUrl/health"

# 2. Vocabulary Service
Test-Endpoint -Name "Tra cứu từ vựng (Lookup)" -Url "$BaseUrl/api/vocabulary/lookup?keyword=Accommodate"
Test-Endpoint -Name "Xem ngữ cảnh đoạn văn (Passages)" -Url "$BaseUrl/api/vocabulary/1/passages"
Test-Endpoint -Name "Danh sách thẻ ôn tập (Flashcards)" -Url "$BaseUrl/api/vocabulary/flashcards?page=1&pageSize=5"

# 3. Leaderboard Service
Test-Endpoint -Name "Bảng xếp hạng theo Điểm (Score)" -Url "$BaseUrl/api/leaderboard?sortBy=Score"
Test-Endpoint -Name "Bảng xếp hạng theo Streak" -Url "$BaseUrl/api/leaderboard?sortBy=Streak"

# 4. Performance Analytics Service
Test-Endpoint -Name "Phân tích mạnh/yếu L&R (user-demo-1)" -Url "$BaseUrl/api/performance/analysis?userId=user-demo-1"
Test-Endpoint -Name "Tiến độ điểm thi (user-demo-1)" -Url "$BaseUrl/api/performance/progress?userId=user-demo-1"

# 5. Gamification Service
Test-Endpoint -Name "Chuỗi ngày học Streak (user-demo-1)" -Url "$BaseUrl/api/gamification/streak?userId=user-demo-1"
Test-Endpoint -Name "Danh sách danh hiệu Achievements" -Url "$BaseUrl/api/gamification/achievements?userId=user-demo-1"

# 6. Admin Reports (Redis Cache)
Test-Endpoint -Name "Admin: Báo cáo tiến độ học" -Url "$BaseUrl/api/admin/learning/reports"
Test-Endpoint -Name "Admin: Thống kê người dùng" -Url "$BaseUrl/api/admin/learning/user-stats"
Test-Endpoint -Name "Admin: Thống kê bài thi" -Url "$BaseUrl/api/admin/learning/exam-stats"

# 7. Gateway Route (nếu Gateway đang chạy)
Write-Host "`n--- Kiểm tra qua YARP API Gateway ($GatewayUrl) ---" -ForegroundColor Yellow
Test-Endpoint -Name "Gateway Route Health Check" -Url "$GatewayUrl/health"

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "KẾT QUẢ: $passed PASSED | $failed FAILED" -ForegroundColor ($failed -eq 0 ? "Green" : "Yellow")
Write-Host "==========================================================" -ForegroundColor Cyan
