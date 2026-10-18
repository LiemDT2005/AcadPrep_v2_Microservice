using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams;

public class DetailModel(ToeicApiClient api) : PageModel
{
    public AdminExamDetail Exam { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var exam = await api.GetAdminExamAsync(id);
        if (exam == null)
        {
            return RedirectToPage("./Index");
        }

        Exam = exam;
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, string status)
    {
        var (success, error) = await api.ChangeExamStatusAsync(id, status);
        if (success)
        {
            return new JsonResult(new { success = true, message = $"Status changed to {status}." });
        }
        return new JsonResult(new { success = false, error = error ?? "Failed to change status." });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var (result, error) = await api.DeleteAdminExamAsync(id);
        if (result != null)
        {
            return new JsonResult(new { success = true, message = result.Message });
        }
        return new JsonResult(new { success = false, error = error ?? "Failed to delete exam." });
    }
}
