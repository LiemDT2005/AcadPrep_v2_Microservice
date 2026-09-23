using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Courses;

public class IndexModel(ToeicApiClient api) : PageModel
{
    public List<CourseListItem> Courses { get; set; } = [];
    public async Task OnGetAsync() => Courses = await api.GetCoursesAsync();
}
