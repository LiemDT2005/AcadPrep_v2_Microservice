using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

/// <summary>
/// HTTP Message Handler tự động bắt mã 401 Unauthorized và dùng Refresh Token
/// để lấy Access Token mới, cập nhật Session và thử lại request cũ một cách tự động (Seamless Re-auth).
/// </summary>
public class TokenRefreshHandler(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    ILogger<TokenRefreshHandler> logger) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        var currentToken = session?.GetString("jwt");

        // 1. Nếu request chưa có Authorization Header và session đang có JWT, tự động gắn vào request
        if (request.Headers.Authorization == null && !string.IsNullOrWhiteSpace(currentToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
        }

        // 2. Không can thiệp nếu request chính là các endpoint đăng nhập, làm mới token hoặc đăng xuất
        var requestPath = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (requestPath.Contains("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase) ||
            requestPath.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            requestPath.Contains("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // 3. Gửi request gốc
        var response = await base.SendAsync(request, cancellationToken);

        // 4. Nếu kết quả KHÔNG PHẢI 401 Unauthorized -> Trả về bình thường
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 5. Nếu bị 401 Unauthorized -> Kiểm tra xem session có Refresh Token để cứu vãn không
        var refreshToken = session?.GetString("refreshToken");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            logger.LogDebug("[TokenRefreshHandler] 401 received but no refresh token available in session.");
            return response;
        }

        logger.LogInformation("[TokenRefreshHandler] 401 Unauthorized for {Uri}. Attempting to refresh token...", request.RequestUri);

        // 6. Cơ chế Lock để tránh Race Condition (Nhiều request cùng 401 không được gọi refresh cùng lúc vì sẽ dính Replay Attack)
        await RefreshLock.WaitAsync(cancellationToken);
        string? newAccessToken = null;

        try
        {
            // Kiểm tra xem trong lúc chờ lock, đã có request khác làm mới token thành công chưa
            var latestToken = session?.GetString("jwt");
            if (!string.IsNullOrEmpty(latestToken) && latestToken != currentToken)
            {
                logger.LogInformation("[TokenRefreshHandler] Token already refreshed by another concurrent request. Reusing new token.");
                newAccessToken = latestToken;
            }
            else
            {
                // Gọi API refresh-token qua client riêng biệt (không dùng handler này để tránh đệ quy)
                var refreshClient = httpClientFactory.CreateClient("IdentityRefreshClient");
                var refreshResponse = await refreshClient.PostAsJsonAsync("/identity/api/auth/refresh-token", new { refreshToken }, cancellationToken);

                if (refreshResponse.IsSuccessStatusCode)
                {
                    var raw = await refreshResponse.Content.ReadAsStringAsync(cancellationToken);
                    var authData = JsonSerializer.Deserialize<AuthResponse>(raw, JsonOpts);

                    if (authData != null && session != null)
                    {
                        // Cập nhật lại toàn bộ Session với cặp token mới
                        session.SaveAuthentication(authData);
                        newAccessToken = authData.Token;
                        logger.LogInformation("[TokenRefreshHandler] Access Token successfully renewed for User {UserId}.", authData.UserId);
                    }
                }
                else
                {
                    var errorBody = await refreshResponse.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogWarning("[TokenRefreshHandler] Failed to refresh token. Status: {Status}, Error: {Error}. Clearing session.", refreshResponse.StatusCode, errorBody);
                    session?.Clear();
                    return response; // Trả về 401 gốc
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TokenRefreshHandler] Exception occurred while refreshing token.");
            return response;
        }
        finally
        {
            RefreshLock.Release();
        }

        // 7. Nếu đã có Token mới -> Clone lại request gốc, thay thế Header Authorization và gửi lại
        if (!string.IsNullOrWhiteSpace(newAccessToken))
        {
            response.Dispose(); // Hủy response 401 cũ
            using var retryRequest = await CloneRequestAsync(request);
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

            logger.LogInformation("[TokenRefreshHandler] Retrying original request to {Uri} with new token...", request.RequestUri);
            return await base.SendAsync(retryRequest, cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// Tạo bản sao đầy đủ của HttpRequestMessage để có thể gửi lại lần 2
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }
}

