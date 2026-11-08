using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.API.Data;
using Identity.API.DTOs;
using Identity.API.Hubs;
using Identity.API.IRepositories;
using Identity.API.IServices;
using Identity.API.Models;
using Identity.API.Repositories;
using Identity.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Tests;

public class IdentityTestSuite
{
    private readonly IServiceProvider _sp;
    private readonly TestEmailService _emailService;
    private readonly TestHubContext _hubContext;
    private readonly IConfiguration _config;
    private readonly string _projectRoot;

    private int _passCount = 0;
    private int _failCount = 0;

    private static void ResetOtpCooldown(string email)
    {
        var field = typeof(OtpService).GetField("LocalCooldowns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field?.GetValue(null) is System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> dict)
        {
            dict.TryRemove(email.Trim().ToLowerInvariant(), out _);
        }
    }

    public IdentityTestSuite(IServiceProvider sp, TestEmailService emailService, TestHubContext hubContext, IConfiguration config, string projectRoot)
    {
        _sp = sp;
        _emailService = emailService;
        _hubContext = hubContext;
        _config = config;
        _projectRoot = projectRoot;
    }

    public static async Task<IdentityTestSuite> InitializeAsync(string projectRoot)
    {
        var services = new ServiceCollection();
        var testDbFile = Path.Combine(AppContext.BaseDirectory, $"identity_test_{Guid.NewGuid():N}.db");

        services.AddDbContext<IdentityDbContext>(opt =>
            opt.UseSqlite($"Data Source={testDbFile}"));

        services.AddIdentity<AppUser, IdentityRole>(opt =>
        {
            opt.Password.RequiredLength = 6;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireDigit = false;
            opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            opt.Lockout.MaxFailedAccessAttempts = 5;
            opt.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        var config = TestConfiguration.Create(projectRoot);
        services.AddSingleton<IConfiguration>(config);

        services.AddDataProtection();
        services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(opt =>
        {
            opt.XmlRepository = new InMemoryXmlRepository();
        });

        var env = new TestWebHostEnvironment
        {
            ContentRootPath = Path.Combine(projectRoot, "src", "Services", "Identity", "Identity.API")
        };
        services.AddSingleton<IWebHostEnvironment>(env);

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHttpContextAccessor();
        services.AddAuthentication();

        var emailService = new TestEmailService();
        services.AddSingleton<IEmailService>(emailService);

        var hubContext = new TestHubContext();
        services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubContext<UserHub>>(hubContext);

        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            await IdentityDbInitializer.SeedAsync(scope.ServiceProvider);
        }

        return new IdentityTestSuite(sp, emailService, hubContext, config, projectRoot);
    }

    private void Assert(bool condition, string testName, string? details = null)
    {
        if (condition)
        {
            _passCount++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  [PASS] ");
            Console.ResetColor();
            Console.WriteLine(testName);
        }
        else
        {
            _failCount++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  [FAIL] ");
            Console.ResetColor();
            Console.WriteLine($"{testName} => {details ?? "Assertion failed"}");
        }
    }

    public async Task RunAllTestsAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=======================================================================");
        Console.WriteLine("       ACADPREP IDENTITY SERVICE - TOÀN BỘ KIỂM THỬ TỰ ĐỘNG            ");
        Console.WriteLine("=======================================================================");
        Console.ResetColor();

        await RunSuite1_SeedAndDatabaseInitializationAsync();
        await RunSuite2_RegisterFlowAsync();
        await RunSuite3_OtpVerificationAndResendAsync();
        await RunSuite4_LoginFlowAsync();
        await RunSuite5_ProfileAndChangePasswordAsync();
        await RunSuite6_TokenRefreshAndRotationAsync();
        await RunSuite7_LogoutAndBlacklistAsync();
        await RunSuite8_ForgotPasswordAndResetPasswordAsync();
        await RunSuite9_AdminUsersListAndCsvAsync();
        await RunSuite10_AdminStatusAndForceLogoutAsync();
        await RunSuite11_AdminRoleAssignmentAsync();
        await RunSuite12_SecurityRsaAndJwksAsync();
        await RunSuite13_ControllerEndpointsAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=======================================================================");
        Console.WriteLine($" KẾT QUẢ KIỂM THỬ: Tổng số: {_passCount + _failCount} | Đạt: {_passCount} | Lỗi: {_failCount}");
        Console.WriteLine("=======================================================================");
        Console.ResetColor();
    }

    // ----------------------------------------------------------------------------------
    // SUITE 1: Seed Data & Database Initialization
    // ----------------------------------------------------------------------------------
    private async Task RunSuite1_SeedAndDatabaseInitializationAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 1] Seed Data & Database Initialization");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        Assert(await roleManager.RoleExistsAsync("Admin"), "Seed: Role 'Admin' tồn tại");
        Assert(await roleManager.RoleExistsAsync("Learner"), "Seed: Role 'Learner' tồn tại");
        Assert(await roleManager.RoleExistsAsync("Moderator"), "Seed: Role 'Moderator' tồn tại");

        var admin = await userManager.FindByEmailAsync("admin@acadprep.com");
        Assert(admin != null, "Seed: Tài khoản Master Admin 'admin@acadprep.com' đã được tạo");
        Assert(admin?.Status == UserStatus.Active, "Seed: Master Admin có Status = Active");
        Assert(admin?.EmailConfirmed == true, "Seed: Master Admin có EmailConfirmed = true");

        var roles = await userManager.GetRolesAsync(admin!);
        Assert(roles.Contains("Admin"), "Seed: Master Admin được gán role 'Admin'");

        var passwordValid = await userManager.CheckPasswordAsync(admin!, "Admin@123456");
        Assert(passwordValid, "Seed: Mật khẩu mặc định của Master Admin đúng (Admin@123456)");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 2: UC-1 User Registration
    // ----------------------------------------------------------------------------------
    private async Task RunSuite2_RegisterFlowAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 2] UC-1 User Registration");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Case 1: Mật khẩu không khớp
        var mismatchReq = new RegisterRequest(Email: "test_mismatch@acadprep.com", Password: "Pass123!", FullName: "Test User", ConfirmPassword: "Pass456!");
        var (s1, err1, resp1) = await authService.RegisterAsync(mismatchReq);
        Assert(!s1 && err1 == "Passwords do not match.", "Đăng ký thất bại khi Password và ConfirmPassword không khớp");

        // Case 2: Đăng ký hợp lệ cho Learner 1
        var validReq = new RegisterRequest(Email: "learner1@acadprep.com", Password: "Learner@123", FullName: "Nguyen Van Learner", ConfirmPassword: "Learner@123");
        var (s2, err2, resp2) = await authService.RegisterAsync(validReq);
        Assert(s2 && resp2 != null, "Đăng ký thành công với thông tin hợp lệ (learner1@acadprep.com)", err2);

        var user = await userManager.FindByEmailAsync("learner1@acadprep.com");
        Assert(user != null, "User được lưu trong database");
        Assert(user?.Status == UserStatus.PendingVerification, "Trạng thái ban đầu là PendingVerification");
        Assert(user?.EmailConfirmed == false, "EmailConfirmed ban đầu là false");
        Assert(!string.IsNullOrEmpty(user?.OtpCode), "Mã OTP 6 số đã được sinh và lưu trong user entity");

        var roles = await userManager.GetRolesAsync(user!);
        Assert(roles.Contains("Learner"), "Tài khoản mới tự động được gán role 'Learner'");

        // Case 3: Email OTP đã được gửi
        Assert(_emailService.SentOtps.ContainsKey("learner1@acadprep.com"), "Email Service đã gửi OTP kích hoạt đến hòm thư");

        // Case 4: Đăng ký trùng email
        var dupReq = new RegisterRequest(Email: "learner1@acadprep.com", Password: "AnotherPass123!", FullName: "Duplicate User", ConfirmPassword: "AnotherPass123!");
        var (s4, err4, resp4) = await authService.RegisterAsync(dupReq);
        Assert(!s4 && err4 == "This email is already registered.", "Từ chối đăng ký trùng email");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 3: UC-2 OTP Verification & Resend Flow
    // ----------------------------------------------------------------------------------
    private async Task RunSuite3_OtpVerificationAndResendAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 3] UC-2 OTP Verification & Resend Flow");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Đăng ký tài khoản phụ để test brute force OTP
        var bfEmail = "learner_bf@acadprep.com";
        await authService.RegisterAsync(new RegisterRequest(Email: bfEmail, Password: "Learner@123", FullName: "BF User", ConfirmPassword: "Learner@123"));

        // Case 1: Xác thực OTP với email không tồn tại
        var (s1, err1, _) = await authService.VerifyOtpAsync(new VerifyOtpRequest("unknown@acadprep.com", "123456"), "127.0.0.1");
        Assert(!s1 && err1 == "Account with this email was not found.", "Từ chối OTP với email không tồn tại");

        // Case 2: Nhập sai OTP lần 1
        var (s2, err2, _) = await authService.VerifyOtpAsync(new VerifyOtpRequest(bfEmail, "000000"), "127.0.0.1");
        Assert(!s2 && (err2?.Contains("Incorrect OTP") ?? false), "Báo lỗi và hiển thị số lần thử còn lại khi nhập sai OTP");

        // Case 3: Nhập sai 5 lần liên tiếp -> OTP bị vô hiệu hóa
        for (int i = 0; i < 4; i++)
        {
            await authService.VerifyOtpAsync(new VerifyOtpRequest(bfEmail, "000000"), "127.0.0.1");
        }
        var (s3, err3, _) = await authService.VerifyOtpAsync(new VerifyOtpRequest(bfEmail, "000000"), "127.0.0.1");
        Assert(!s3 && (err3?.Contains("incorrect OTP") ?? false), "Khóa / Vô hiệu hóa OTP sau 5 lần nhập sai liên tiếp");

        // Case 4: Kiểm tra Cooldown chống spam ngay sau khi vừa sinh OTP (60s)
        var (sCooldown, errCooldown, _) = await authService.ResendOtpAsync(new ResendOtpRequest(bfEmail));
        Assert(!sCooldown && (errCooldown?.Contains("Please wait") ?? false), "Chặn spam resend OTP trong thời gian Cooldown (60s)");

        // Case 5: Hết Cooldown -> Resend OTP thành công
        ResetOtpCooldown(bfEmail);
        var (sResend, _, msgResend) = await authService.ResendOtpAsync(new ResendOtpRequest(bfEmail));
        Assert(sResend, "Gửi lại mã OTP thành công sau khi hết cooldown");

        // Case 6: Xác thực đúng OTP cho learner1
        var learner1Email = "learner1@acadprep.com";
        var user1 = await userManager.FindByEmailAsync(learner1Email);
        var validOtp = user1?.OtpCode!;

        var (sVerify, _, authResp) = await authService.VerifyOtpAsync(new VerifyOtpRequest(learner1Email, validOtp), "127.0.0.1");
        Assert(sVerify && authResp != null, "Xác thực đúng OTP thành công");
        Assert(!string.IsNullOrEmpty(authResp?.Token), "Cấp Access Token JWT sau khi verify OTP thành công");
        Assert(!string.IsNullOrEmpty(authResp?.RefreshToken), "Cấp Refresh Token sau khi verify OTP thành công");
        Assert(authResp?.Status == "Active", "Trạng thái người dùng chuyển sang 'Active'");

        var updatedUser1 = await userManager.FindByEmailAsync(learner1Email);
        Assert(updatedUser1?.EmailConfirmed == true, "EmailConfirmed được cập nhật thành true");
        Assert(string.IsNullOrEmpty(updatedUser1?.OtpCode), "Mã OTP đã được dọn dẹp sau khi kích hoạt thành công");

        // Case 7: Xác thực lại tài khoản đã Active
        var (sActive, errActive, _) = await authService.VerifyOtpAsync(new VerifyOtpRequest(learner1Email, "123456"), "127.0.0.1");
        Assert(!sActive && errActive == "This account has already been activated.", "Từ chối verify OTP cho tài khoản đã kích hoạt");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 4: UC-5.1 Login with Email & Password
    // ----------------------------------------------------------------------------------
    private async Task RunSuite4_LoginFlowAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 4] UC-5.1 Login Flow");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        // Case 1: Đăng nhập email không tồn tại
        var (s1, err1, _, _) = await authService.LoginAsync(new LoginRequest("noexist@acadprep.com", "Pass123!"), "127.0.0.1");
        Assert(!s1 && err1 == "Invalid email or password.", "Đăng nhập thất bại khi email không tồn tại");

        // Case 2: Đăng nhập sai mật khẩu (hiển thị số lần còn lại)
        var (s2, err2, _, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "WrongPass!"), "127.0.0.1");
        Assert(!s2 && (err2?.Contains("Invalid email or password") ?? false), "Đăng nhập sai mật khẩu thông báo số lần thử còn lại");

        // Case 3: Đăng nhập tài khoản chưa kích hoạt (PendingVerification)
        var pendingEmail = "learner_pending@acadprep.com";
        await authService.RegisterAsync(new RegisterRequest(Email: pendingEmail, Password: "Learner@123", FullName: "Pending User", ConfirmPassword: "Learner@123"));
        var (sPending, errPending, _, _) = await authService.LoginAsync(new LoginRequest(pendingEmail, "Learner@123"), "127.0.0.1");
        Assert(!sPending && (errPending?.Contains("Account not activated") ?? false), "Chặn đăng nhập đối với tài khoản PendingVerification");

        // Case 4: Đăng nhập thành công với tài khoản Active
        var (sLogin, _, authResp, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "Learner@123"), "127.0.0.1");
        Assert(sLogin && authResp != null, "Đăng nhập thành công với tài khoản active");
        Assert(!string.IsNullOrEmpty(authResp?.Token), "Trả về Access Token JWT hợp lệ");
        Assert(!string.IsNullOrEmpty(authResp?.RefreshToken), "Trả về Refresh Token hợp lệ");
        Assert(authResp?.Roles.Contains("Learner") ?? false, "AuthResponse chứa role 'Learner'");

        // Case 5: Đăng nhập Master Admin thành công
        var (sAdmin, _, adminResp, _) = await authService.LoginAsync(new LoginRequest("admin@acadprep.com", "Admin@123456"), "127.0.0.1");
        Assert(sAdmin && (adminResp?.Roles.Contains("Admin") ?? false), "Đăng nhập Master Admin thành công, chứa role 'Admin'");

        // Case 6: Lockout chống brute force (nhập sai mật khẩu liên tiếp)
        var bruteUser = "learner_lockout@acadprep.com";
        var (_, _, regLockout) = await authService.RegisterAsync(new RegisterRequest(Email: bruteUser, Password: "Learner@123", FullName: "Lockout User", ConfirmPassword: "Learner@123"));
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var bUser = await userManager.FindByEmailAsync(bruteUser);
        await authService.VerifyOtpAsync(new VerifyOtpRequest(bruteUser, bUser?.OtpCode!), "127.0.0.1");

        bool lockoutTriggered = false;
        for (int i = 0; i < 5; i++)
        {
            var (sWrong, _, _, isLocked) = await authService.LoginAsync(new LoginRequest(bruteUser, "WrongPass"), "127.0.0.1");
            if (isLocked) lockoutTriggered = true;
        }
        var (_, _, _, finalLocked) = await authService.LoginAsync(new LoginRequest(bruteUser, "WrongPass"), "127.0.0.1");
        Assert(lockoutTriggered || finalLocked, "Kích hoạt Lockout chống Brute Force khi nhập sai mật khẩu 5 lần");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 5: UC-7 Profile & Password Management
    // ----------------------------------------------------------------------------------
    private async Task RunSuite5_ProfileAndChangePasswordAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 5] UC-7 Profile & Password Management");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync("learner1@acadprep.com");

        // Case 1: Lấy Profile (UC-7.1)
        var (sProf, _, profile) = await authService.GetProfileAsync(user!.Id);
        Assert(sProf && profile?.Email == "learner1@acadprep.com", "Lấy thông tin User Profile thành công");
        Assert(profile?.Roles.Contains("Learner") ?? false, "Profile chứa danh sách vai trò chính xác");

        // Case 2: Cập nhật Profile (UC-7.2)
        var updateReq = new UpdateProfileRequest("Nguyen Van Learner Updated", "https://cdn.acadprep.com/avatar1.jpg");
        var (sUp, _, updatedProf) = await authService.UpdateProfileAsync(user.Id, updateReq);
        Assert(sUp && updatedProf?.FullName == "Nguyen Van Learner Updated", "Cập nhật FullName thành công");
        Assert(updatedProf?.Avatar == "https://cdn.acadprep.com/avatar1.jpg", "Cập nhật Avatar URL thành công");

        // Case 3: Đổi mật khẩu sai mật khẩu hiện tại (UC-7.3)
        var badPassReq = new ChangePasswordRequest("WrongCurrentPass", "NewPass@123", "NewPass@123");
        var (sBadPass, errBadPass) = await authService.ChangePasswordAsync(user.Id, badPassReq);
        Assert(!sBadPass && errBadPass == "Current password is incorrect.", "Báo lỗi khi nhập sai mật khẩu hiện tại");

        // Case 4: Đổi mật khẩu không khớp ConfirmPassword
        var mismatchReq = new ChangePasswordRequest("Learner@123", "NewPass@123", "DifferentPass@123");
        var (sMis, errMis) = await authService.ChangePasswordAsync(user.Id, mismatchReq);
        Assert(!sMis && errMis == "Passwords do not match.", "Báo lỗi khi mật khẩu mới và xác nhận không khớp");

        // Case 5: Đổi mật khẩu thành công
        var validPassReq = new ChangePasswordRequest("Learner@123", "NewPass@123", "NewPass@123");
        var (sChange, _) = await authService.ChangePasswordAsync(user.Id, validPassReq);
        Assert(sChange, "Đổi mật khẩu thành công");

        // Case 6: Kiểm tra đăng nhập với mật khẩu mới
        var (sNewLogin, _, _, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "NewPass@123"), "127.0.0.1");
        Assert(sNewLogin, "Đăng nhập thành công với mật khẩu mới");

        // Case 7: Đăng nhập với mật khẩu cũ phải thất bại
        var (sOldLogin, _, _, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "Learner@123"), "127.0.0.1");
        Assert(!sOldLogin, "Đăng nhập với mật khẩu cũ thất bại");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 6: Token Refresh & Token Rotation
    // ----------------------------------------------------------------------------------
    private async Task RunSuite6_TokenRefreshAndRotationAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 6] Token Refresh & Token Rotation");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var rfRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // Đăng nhập lấy cặp token ban đầu
        var (_, _, loginResp, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "NewPass@123"), "127.0.0.1");
        var oldRefreshToken = loginResp!.RefreshToken;

        // Case 1: Refresh token không tồn tại
        var (s1, err1, _) = await authService.RefreshTokenAsync(new RefreshTokenRequest("non_existent_token"), "127.0.0.1");
        Assert(!s1 && err1 == "Refresh token not found.", "Từ chối khi refresh token không tồn tại");

        // Case 2: Refresh token hợp lệ -> Cấp cặp token mới & Thu hồi token cũ (Rotation)
        var (s2, _, refreshResp) = await authService.RefreshTokenAsync(new RefreshTokenRequest(oldRefreshToken), "127.0.0.1");
        Assert(s2 && refreshResp != null, "Token Rotation thành công, cấp Access Token và Refresh Token mới");
        Assert(refreshResp?.RefreshToken != oldRefreshToken, "Mã Refresh Token mới khác mã cũ");

        var revokedOld = await rfRepo.GetByTokenAsync(oldRefreshToken);
        Assert(revokedOld?.IsRevoked == true, "Token cũ đã được đánh dấu thu hồi (IsRevoked = true)");
        Assert(revokedOld?.ReplacedByToken == refreshResp?.RefreshToken, "Token cũ lưu ReplacedByToken trỏ đến token mới");

        // Case 3: Replay attack - Tái sử dụng token đã thu hồi -> Kích hoạt thu hồi toàn bộ phiên (Session Termination)
        var (sReplay, errReplay, _) = await authService.RefreshTokenAsync(new RefreshTokenRequest(oldRefreshToken), "127.0.0.1");
        Assert(!sReplay && (errReplay?.Contains("Session terminated due to security policy") ?? false),
            "Phát hiện Replay Attack: Chặn tái sử dụng token đã thu hồi và hủy toàn bộ phiên của người dùng");

        var newRevoked = await rfRepo.GetByTokenAsync(refreshResp!.RefreshToken);
        Assert(newRevoked?.IsRevoked == true, "Token đang hoạt động của user đã bị thu hồi đồng loạt để bảo vệ");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 7: UC-6 Logout & Access Token Blacklist
    // ----------------------------------------------------------------------------------
    private async Task RunSuite7_LogoutAndBlacklistAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 7] UC-6 Logout & Access Token Blacklist");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var rfRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var blacklistService = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();

        // Đăng nhập lấy phiên mới
        var (_, _, loginResp, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "NewPass@123"), "127.0.0.1");
        var jwt = loginResp!.Token;
        var rToken = loginResp.RefreshToken;

        // Trích xuất JTI từ JWT
        var handler = new JwtSecurityTokenHandler();
        var tokenObj = handler.ReadJwtToken(jwt);
        var jti = tokenObj.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        Assert(!await blacklistService.IsBlacklistedAsync(jti), "Access Token ban đầu chưa bị blacklist");

        // Case 1: Thực hiện Logout
        var authHeader = $"Bearer {jwt}";
        var (sLogout, _) = await authService.LogoutAsync(authHeader, rToken, "127.0.0.1");
        Assert(sLogout, "LogoutAsync hoàn thành thành công");

        // Case 2: Kiểm tra Refresh Token đã bị thu hồi
        var tokenEntity = await rfRepo.GetByTokenAsync(rToken);
        Assert(tokenEntity?.IsRevoked == true, "Refresh Token được đánh dấu thu hồi khi Logout");
        Assert(tokenEntity?.ReasonRevoked == "User explicitly logged out", "Lý do thu hồi là 'User explicitly logged out'");

        // Case 3: Kiểm tra Access Token JTI đã được đưa vào Blacklist
        Assert(await blacklistService.IsBlacklistedAsync(jti), "JTI của Access Token đã được đưa vào Blacklist thành công");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 8: UC-8 Forgot Password & Reset Password
    // ----------------------------------------------------------------------------------
    private async Task RunSuite8_ForgotPasswordAndResetPasswordAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 8] UC-8 Forgot Password & Reset Password");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Case 1: Forgot password với email không tồn tại -> Vẫn trả về 200/success (chống user enumeration)
        var (sNoUser, _, msgNoUser) = await authService.ForgotPasswordAsync(new ForgotPasswordRequest("nonexistent@acadprep.com"));
        Assert(sNoUser && (msgNoUser?.Contains("dispatched") ?? false), "Chống rà quét tài khoản (User Enumeration): Trả về thông báo thành công dù email không tồn tại");

        // Case 2: Forgot password với email hợp lệ
        var targetEmail = "learner1@acadprep.com";
        ResetOtpCooldown(targetEmail);
        var (sForgot, _, _) = await authService.ForgotPasswordAsync(new ForgotPasswordRequest(targetEmail));
        Assert(sForgot, "Gửi yêu cầu quên mật khẩu thành công");

        var user = await userManager.FindByEmailAsync(targetEmail);
        Assert(!string.IsNullOrEmpty(user?.OtpCode), "Mã OTP khôi phục mật khẩu 6 số đã được sinh và lưu trữ");
        Assert(_emailService.SentOtps.ContainsKey(targetEmail), "Email OTP khôi phục đã được gửi qua email service");

        // Case 3: Reset password với OTP sai
        var badResetReq = new ResetPasswordRequest(targetEmail, "000000", "ResetPass@123", "ResetPass@123");
        var (sBadReset, errBadReset, _) = await authService.ResetPasswordAsync(badResetReq);
        Assert(!sBadReset && (errBadReset?.Contains("Incorrect OTP") ?? false), "Báo lỗi khi nhập sai OTP khôi phục mật khẩu");

        // Case 4: Reset password với OTP đúng
        var goodResetReq = new ResetPasswordRequest(targetEmail, user!.OtpCode!, "ResetPass@123", "ResetPass@123");
        var (sReset, _, msgReset) = await authService.ResetPasswordAsync(goodResetReq);
        Assert(sReset && (msgReset?.Contains("Password reset successfully") ?? false), "Khôi phục mật khẩu thành công với OTP hợp lệ");

        // Case 5: Đăng nhập với mật khẩu mới vừa reset
        var (sLoginAfterReset, _, _, _) = await authService.LoginAsync(new LoginRequest(targetEmail, "ResetPass@123"), "127.0.0.1");
        Assert(sLoginAfterReset, "Đăng nhập thành công với mật khẩu mới sau khi Reset Password");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 9: UC-19.1 & 19.2 Admin CMS - View Users & Export CSV
    // ----------------------------------------------------------------------------------
    private async Task RunSuite9_AdminUsersListAndCsvAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 9] UC-19.1 & 19.2 Admin CMS - View Users & Export CSV");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Case 1: Lấy danh sách phân trang mặc định
        var pagedResult = await userService.GetUsersAsync(search: null, role: null, status: null, pageIndex: 1, pageSize: 10);
        Assert(pagedResult.TotalCount >= 3, $"Lấy danh sách người dùng phân trang thành công (TotalCount = {pagedResult.TotalCount})");
        Assert(pagedResult.Items.Count > 0, "Danh sách items không rỗng");

        // Case 2: Tìm kiếm theo từ khóa
        var searchResult = await userService.GetUsersAsync(search: "admin", role: null, status: null, pageIndex: 1, pageSize: 10);
        Assert(searchResult.Items.Any(u => u.Email.Contains("admin")), "Tìm kiếm người dùng theo từ khóa 'admin' thành công");

        // Case 3: Lọc theo vai trò (Role = "Admin")
        var adminResult = await userService.GetUsersAsync(search: null, role: "Admin", status: null, pageIndex: 1, pageSize: 10);
        Assert(adminResult.Items.All(u => u.Roles.Contains("Admin")), "Bộ lọc vai trò 'Admin' hoạt động chính xác");

        // Case 4: Lọc theo trạng thái (Status = Active)
        var activeResult = await userService.GetUsersAsync(search: null, role: null, status: UserStatus.Active, pageIndex: 1, pageSize: 10);
        Assert(activeResult.Items.All(u => u.Status == "Active"), "Bộ lọc trạng thái 'Active' hoạt động chính xác");

        // Case 5: Xuất danh sách CSV (UC-19.1)
        var (csvBytes, fileName) = await userService.ExportUsersCsvAsync();
        Assert(csvBytes != null && csvBytes.Length > 0, "Tạo mảng byte CSV thành công");
        Assert(fileName.StartsWith("acadprep_users_") && fileName.EndsWith(".csv"), "Tên file CSV đúng định dạng");

        var csvText = Encoding.UTF8.GetString(csvBytes!);
        Assert(csvText.Contains("ID,Email,Full Name,Status,Auth Provider,Roles,Created At,Last Login"), "Header file CSV chứa đầy đủ các trường yêu cầu");
        Assert(csvText.Contains("admin@acadprep.com"), "File CSV chứa bản ghi dữ liệu thực tế");

        // Case 6: Xem chi tiết tài khoản & phiên thiết bị (UC-19.2)
        var admin = await userManager.FindByEmailAsync("admin@acadprep.com");
        var (sDet, _, detail) = await userService.GetUserDetailAsync(admin!.Id);
        Assert(sDet && detail != null, "Lấy chi tiết tài khoản Admin thành công");
        Assert(detail?.Email == "admin@acadprep.com", "Chi tiết tài khoản chứa email chính xác");
        Assert(detail?.Roles.Contains("Admin") ?? false, "Chi tiết tài khoản chứa vai trò chính xác");
        Assert(detail?.RecentSessions != null, "Chi tiết tài khoản trả về danh sách phiên đăng nhập gần nhất");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 10: UC-19.3 Admin CMS - Update Status & SignalR Force Logout
    // ----------------------------------------------------------------------------------
    private async Task RunSuite10_AdminStatusAndForceLogoutAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 10] UC-19.3 Admin CMS - Update Status & SignalR Force Logout");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var rfRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        var admin = await userManager.FindByEmailAsync("admin@acadprep.com");
        var learner = await userManager.FindByEmailAsync("learner1@acadprep.com");

        // Đăng nhập learner để tạo active session
        var (_, _, learnerLogin, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "ResetPass@123"), "127.0.0.1");

        // Case 1: Bảo vệ Master Admin - Không cho phép ban Master Admin
        var banAdminReq = new UpdateUserStatusRequest(UserStatus.Banned, "Test Ban Admin");
        var (sBanMaster, errBanMaster, _, _) = await userService.UpdateStatusAsync(admin!.Id, banAdminReq, admin.Id, "127.0.0.1");
        Assert(!sBanMaster && (errBanMaster?.Contains("Cannot change status of Master Admin account") ?? false),
            "Bảo vệ Master Admin: Chặn Admin tự ban hoặc đổi trạng thái tài khoản admin@acadprep.com");

        // Case 2: Bảo vệ Anti-Self-Ban - Admin không được tự ban chính mình
        var (sSelfBan, errSelfBan, _, _) = await userService.UpdateStatusAsync(admin.Id, banAdminReq, admin.Id, "127.0.0.1");
        Assert(!sSelfBan, "Bảo vệ Anti-Self-Ban: Admin không được tự ban tài khoản của mình");

        // Case 3: Ban tài khoản Learner
        var banLearnerReq = new UpdateUserStatusRequest(UserStatus.Banned, "Vi phạm quy định sử dụng");
        var (sBan, _, msgBan, bannedStatus) = await userService.UpdateStatusAsync(learner!.Id, banLearnerReq, admin.Id, "127.0.0.1");
        Assert(sBan && bannedStatus == UserStatus.Banned, "Admin khóa tài khoản Learner thành công (Status = Banned)");

        // Case 4: Kiểm tra toàn bộ Refresh Tokens của Learner đã bị thu hồi
        var activeTokens = await rfRepo.GetActiveTokensByUserIdAsync(learner.Id);
        Assert(activeTokens.Count == 0, "Toàn bộ Refresh Tokens của Learner bị thu hồi ngay lập tức");

        // Case 5: Kiểm tra SignalR ForceLogout event đã được bắn tới Learner ID
        Assert(_hubContext.SentMessages.ContainsKey(learner.Id), "SignalR Hub đã bắn thông điệp ForceLogout tới User ID bị khóa");
        var sentList = _hubContext.SentMessages[learner.Id];
        Assert(sentList.Any(m => m.Method == "ForceLogout"), "Tên phương thức SignalR là 'ForceLogout'");

        // Case 5b: User ID bị đưa vào blacklist ngay lập tức để chặn Access Token hiện tại
        var blacklistService = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();
        var isUserBlacklisted = await blacklistService.IsUserBlacklistedAsync(learner.Id);
        Assert(isUserBlacklisted, "User ID bị đưa vào Token Blacklist ngay lập tức để vô hiệu hóa Access Token hiện tại");

        // Case 5c: Gọi GetProfile bị chặn ngay lập tức
        var (sProfile, errProfile, _) = await authService.GetProfileAsync(learner.Id);
        Assert(!sProfile && (errProfile?.Contains("suspended") ?? false), "Gọi GetProfile bị chặn ngay lập tức khi tài khoản Banned");

        // Case 6: Tài khoản bị Ban không thể đăng nhập
        var (sBannedLogin, errBannedLogin, _, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "ResetPass@123"), "127.0.0.1");
        Assert(!sBannedLogin && (errBannedLogin?.Contains("suspended") ?? false), "Chặn đăng nhập đối với tài khoản đang bị Banned");

        // Case 7: Mở khóa tài khoản (Unban)
        var unbanReq = new UpdateUserStatusRequest(UserStatus.Active, "Đã hoàn thành xem xét");
        var (sUnban, _, _, unbannedStatus) = await userService.UpdateStatusAsync(learner.Id, unbanReq, admin.Id, "127.0.0.1");
        Assert(sUnban && unbannedStatus == UserStatus.Active, "Admin mở khóa tài khoản Learner thành công (Status = Active)");

        var isBlacklistedAfterUnban = await blacklistService.IsUserBlacklistedAsync(learner.Id);
        Assert(!isBlacklistedAfterUnban, "User ID được gỡ khỏi Blacklist sau khi unban");

        // Case 8: Sau khi mở khóa, người dùng đăng nhập lại bình thường
        var (sLoginAfterUnban, _, _, _) = await authService.LoginAsync(new LoginRequest("learner1@acadprep.com", "ResetPass@123"), "127.0.0.1");
        Assert(sLoginAfterUnban, "Người dùng đăng nhập thành công sau khi được mở khóa");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 11: UC-19.4 Admin CMS - Role Assignment
    // ----------------------------------------------------------------------------------
    private async Task RunSuite11_AdminRoleAssignmentAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 11] UC-19.4 Admin CMS - Role Assignment");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var admin = await userManager.FindByEmailAsync("admin@acadprep.com");
        var learner = await userManager.FindByEmailAsync("learner1@acadprep.com");

        // Case 1: Gán danh sách vai trò rỗng
        var emptyReq = new AssignRolesRequest(new List<string>());
        var (sEmpty, errEmpty, _) = await userService.AssignRolesAsync(learner!.Id, emptyReq, admin!.Id);
        Assert(!sEmpty && errEmpty == "User must have at least one assigned role.", "Từ chối khi danh sách vai trò rỗng");

        // Case 2: Gán vai trò không hợp lệ
        var invalidReq = new AssignRolesRequest(new List<string> { "SuperUser" });
        var (sInv, errInv, _) = await userService.AssignRolesAsync(learner.Id, invalidReq, admin.Id);
        Assert(!sInv && (errInv?.Contains("is invalid") ?? false), "Từ chối khi gán vai trò không tồn tại trong hệ thống");

        // Case 3: Bảo vệ Master Admin - Chặn tước quyền Admin của admin@acadprep.com
        var revokeMasterReq = new AssignRolesRequest(new List<string> { "Learner" });
        var (sRevokeMaster, errRevokeMaster, _) = await userService.AssignRolesAsync(admin.Id, revokeMasterReq, admin.Id);
        Assert(!sRevokeMaster && (errRevokeMaster?.Contains("Cannot revoke Admin role from the Master Admin account") ?? false),
            "Bảo vệ Master Admin: Chặn tước quyền Admin của admin@acadprep.com");

        // Case 4: Admin không thể tự tước quyền Admin của chính mình
        var (sSelfRevoke, errSelfRevoke, _) = await userService.AssignRolesAsync(admin.Id, revokeMasterReq, admin.Id);
        Assert(!sSelfRevoke, "Admin không thể tự tước quyền Admin của chính mình");

        // Case 5: Phân quyền vai trò Moderator cho Learner
        var assignModReq = new AssignRolesRequest(new List<string> { "Moderator", "Learner" });
        var (sAssign, _, msgAssign) = await userService.AssignRolesAsync(learner.Id, assignModReq, admin.Id);
        Assert(sAssign, "Admin phân quyền 'Moderator' và 'Learner' cho người dùng thành công");

        var updatedRoles = await userManager.GetRolesAsync(learner);
        Assert(updatedRoles.Contains("Moderator") && updatedRoles.Contains("Learner"), "Database ghi nhận đầy đủ các vai trò mới được phân");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 12: Security Validation & RSA / JWKS Verification
    // ----------------------------------------------------------------------------------
    private async Task RunSuite12_SecurityRsaAndJwksAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 12] Security Validation & RSA / JWKS Verification");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var admin = await userManager.FindByEmailAsync("admin@acadprep.com");
        var roles = await userManager.GetRolesAsync(admin!);

        // Case 1: Ký token JWT RS256 bằng RSA Private Key
        var (jwtToken, expiresAt) = tokenService.CreateToken(admin!, roles);
        Assert(!string.IsNullOrEmpty(jwtToken), "TokenService tạo chuỗi JWT RS256 thành công");
        Assert(expiresAt > DateTime.Now, "Thời hạn hết hạn hợp lệ");

        // Case 2: Kiểm tra các Claims trong JWT
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwtToken);
        Assert(token.Header.Alg == "RS256", "Thuật toán ký là RS256");
        Assert(token.Header.Kid == "acadprep-rsa-key-1", "KeyId là 'acadprep-rsa-key-1'");
        Assert(token.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == admin!.Id), "Claim 'sub' chính xác");
        Assert(token.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == admin!.Email), "Claim 'email' chính xác");
        Assert(token.Claims.Any(c => c.Type == "status" && c.Value == "Active"), "Claim 'status' chính xác");
        Assert(token.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Admin"), "Claim role 'Admin' chính xác");

        // Case 3: Xác thực JWT bằng RSA Public Key
        var publicKeyPath = Path.Combine(_projectRoot, "src", "Services", "Identity", "Identity.API", "keys", "public_key.pem");
        var publicKeyPem = await File.ReadAllTextAsync(publicKeyPath);
        var rsaVerify = RSA.Create();
        rsaVerify.ImportFromPem(publicKeyPem);
        var rsaSecurityKey = new RsaSecurityKey(rsaVerify) { KeyId = "acadprep-rsa-key-1" };

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "ToeicLearning.Identity",
            ValidAudience = "ToeicLearning.Clients",
            IssuerSigningKey = rsaSecurityKey
        };

        var principal = handler.ValidateToken(jwtToken, validationParams, out var validatedToken);
        Assert(principal != null && validatedToken != null, "Xác thực chữ ký JWT bằng Public Key RS256 thành công");

        // Case 4: Kiểm tra JWKS JSON Web Key format
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaSecurityKey);
        jwk.Alg = "RS256";
        jwk.Use = "sig";
        Assert(jwk.Kid == "acadprep-rsa-key-1" && jwk.Kty == "RSA" && jwk.Alg == "RS256", "Cấu trúc JWKS tương thích OIDC chuẩn hóa");
    }

    // ----------------------------------------------------------------------------------
    // SUITE 13: Controller HTTP Action Results & Status Codes
    // ----------------------------------------------------------------------------------
    private async Task RunSuite13_ControllerEndpointsAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[SUITE 13] Controller HTTP Action Results & Status Codes");
        Console.ResetColor();

        using var scope = _sp.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var authLogger = scope.ServiceProvider.GetRequiredService<ILogger<Identity.API.Controllers.AuthController>>();
        var usersLogger = scope.ServiceProvider.GetRequiredService<ILogger<Identity.API.Controllers.UsersController>>();

        var admin = await userManager.FindByEmailAsync("admin@acadprep.com");

        // Tạo AuthController
        var authController = new Identity.API.Controllers.AuthController(authService, authLogger);
        var authHttp = new DefaultHttpContext();
        authHttp.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin!.Id),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));
        authController.ControllerContext = new ControllerContext { HttpContext = authHttp };

        // 1. AuthController.Register -> OkObjectResult
        var ctrlRegReq = new RegisterRequest(Email: "ctrl_test@acadprep.com", Password: "Password@123", FullName: "Controller Test User", ConfirmPassword: "Password@123");
        var regResult = await authController.Register(ctrlRegReq);
        Assert(regResult.Result is OkObjectResult, "AuthController.Register trả về HTTP 200 OK");

        // 2. AuthController.Login -> OkObjectResult
        var loginResult = await authController.Login(new LoginRequest("admin@acadprep.com", "Admin@123456"));
        Assert(loginResult.Result is OkObjectResult, "AuthController.Login trả về HTTP 200 OK");

        // 3. AuthController.GetProfile -> OkObjectResult
        var meResult = await authController.GetProfile();
        Assert(meResult.Result is OkObjectResult, "AuthController.GetProfile (GET /me) trả về HTTP 200 OK");

        // 4. AuthController.UpdateProfile -> OkObjectResult
        var upProfResult = await authController.UpdateProfile(new UpdateProfileRequest("Master Admin Updated", null));
        Assert(upProfResult.Result is OkObjectResult, "AuthController.UpdateProfile trả về HTTP 200 OK");

        // 5. AuthController.Logout -> OkObjectResult
        var logoutResult = await authController.Logout(null);
        Assert(logoutResult is OkObjectResult, "AuthController.Logout trả về HTTP 200 OK");

        // Tạo UsersController
        var usersController = new Identity.API.Controllers.UsersController(userService, usersLogger);
        var usersHttp = new DefaultHttpContext();
        usersHttp.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));
        usersController.ControllerContext = new ControllerContext { HttpContext = usersHttp };

        // 6. UsersController.GetUsers -> OkObjectResult
        var getUsersResult = await usersController.GetUsers();
        Assert(getUsersResult.Result is OkObjectResult, "UsersController.GetUsers trả về HTTP 200 OK");

        // 7. UsersController.ExportUsersCsv -> FileContentResult
        var csvResult = await usersController.ExportUsersCsv();
        Assert(csvResult is FileContentResult fileRes && fileRes.ContentType == "text/csv; charset=utf-8",
            "UsersController.ExportUsersCsv trả về FileContentResult MIME 'text/csv; charset=utf-8'");

        // 8. UsersController.GetUserDetail -> OkObjectResult
        var detailResult = await usersController.GetUserDetail(admin.Id);
        Assert(detailResult.Result is OkObjectResult, "UsersController.GetUserDetail trả về HTTP 200 OK");

        // 9. UsersController.GetUserDetail non-existent -> NotFoundObjectResult
        var notFoundResult = await usersController.GetUserDetail("non_existent_id");
        Assert(notFoundResult.Result is NotFoundObjectResult, "UsersController.GetUserDetail ID không tồn tại trả về HTTP 404 NotFound");

        // 10. UsersController.AssignRoles -> OkObjectResult
        var assignResult = await usersController.AssignRoles(admin.Id, new AssignRolesRequest(new List<string> { "Admin", "Moderator" }));
        Assert(assignResult is OkObjectResult, "UsersController.AssignRoles trả về HTTP 200 OK");
    }
}
