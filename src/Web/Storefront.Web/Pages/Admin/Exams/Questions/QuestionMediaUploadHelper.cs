using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public static class QuestionMediaUploadHelper
{
    public static async Task<string?> SaveFileAsync(IWebHostEnvironment env, IFormFile? file, string subfolder)
    {
        if (file == null || file.Length <= 0) return null;

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
}
