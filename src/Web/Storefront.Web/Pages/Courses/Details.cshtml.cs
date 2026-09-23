using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Courses;

public class DetailsModel(ToeicApiClient api) : PageModel
{
    public CourseDetail? Course { get; set; }
    public async Task OnGetAsync(Guid id) => Course = await api.GetCourseAsync(id);
}
