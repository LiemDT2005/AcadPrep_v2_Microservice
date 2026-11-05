using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public static class QuestionMediaUploadHelper
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    public static async Task<string?> SaveFileAsync(IWebHostEnvironment env, IFormFile? file, string subfolder)
    {
        if (file == null || file.Length <= 0) return null;

        var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? "dpfy23tda";
        var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") ?? "953398772632599";
        var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? "t1DWOpDR-k9AOKH0W8xVDKsOVzY";

        if (!string.IsNullOrWhiteSpace(cloudName) &&
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(apiSecret))
        {
            var cloudinaryUrl = await UploadToCloudinaryAsync(file, subfolder, cloudName, apiKey, apiSecret);
            if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
            {
                return cloudinaryUrl;
            }
        }

        // Fallback to local storage only if Cloudinary is unreachable
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

        var dir = Path.Combine(env.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{subfolder}/{fileName}";
    }

    private static async Task<string?> UploadToCloudinaryAsync(
        IFormFile file,
        string subfolder,
        string cloudName,
        string apiKey,
        string apiSecret)
    {
        try
        {
            var isAudio = string.Equals(subfolder, "audio", StringComparison.OrdinalIgnoreCase);
            var resourceType = isAudio ? "video" : "image";
            var folder = $"acadprep/{subfolder.Trim('/')}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            var stringToSign = $"folder={folder}&timestamp={timestamp}{apiSecret}";
            var hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(stringToSign));
            var signature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            using var content = new MultipartFormDataContent();
            await using var fileStream = file.OpenReadStream();
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", string.IsNullOrWhiteSpace(file.FileName) ? "upload.bin" : file.FileName);
            content.Add(new StringContent(apiKey), "api_key");
            content.Add(new StringContent(timestamp), "timestamp");
            content.Add(new StringContent(folder), "folder");
            content.Add(new StringContent(signature), "signature");

            var endpoint = $"https://api.cloudinary.com/v1_1/{cloudName}/{resourceType}/upload";
            using var response = await Http.PostAsync(endpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("secure_url", out var secureUrlProp))
            {
                return secureUrlProp.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
