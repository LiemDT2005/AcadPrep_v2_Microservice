using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Storefront.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        // Redirect directly to the CMS Exam Management dashboard
        return RedirectToPage("/Admin/Exams/Index");
    }
}
