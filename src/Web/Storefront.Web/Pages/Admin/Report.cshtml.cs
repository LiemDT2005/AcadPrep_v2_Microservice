using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin;

public class ReportModel(ToeicApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; }

    public ProgressReportDto Report { get; set; } = new([], 0, 0);

    public async Task<IActionResult> OnGetAsync()
    {
        if (StartDate == default)
            StartDate = DateTime.UtcNow.AddDays(-30);

        if (EndDate == default)
            EndDate = DateTime.UtcNow;

        var result = await api.GetProgressReportAsync(StartDate, EndDate);
        if (result != null)
        {
            Report = result;
        }

        return Page();
    }
}
