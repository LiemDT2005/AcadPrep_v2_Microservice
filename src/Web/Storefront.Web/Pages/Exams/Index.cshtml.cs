using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Exams;

public class IndexModel(ToeicApiClient api) : PageModel
{
    public List<ExamListItem> Exams { get; set; } = [];
    public async Task OnGetAsync() => Exams = await api.GetExamsAsync();
}
