using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Common;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Exams;

public class IndexModel(ToeicApiClient apiClient) : PageModel
{
    // ===== Bind Properties (Query String Parameters) =====
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SelectedSeries { get; set; } = "All";

    [BindProperty(SupportsGet = true)]
    public string SelectedYear { get; set; } = "All";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = PaginationConstants.DefaultPageIndex;

    public int PageSize { get; set; } = PaginationConstants.DefaultPageSize;

    // ===== Filter Options (Dynamic from API) =====
    public List<string> SeriesFilters { get; set; } = ["All"];
    public List<string> YearFilters { get; set; } = ["All"];

    // ===== Data Properties =====
    public PaginatedList<GetExamDto>? ExamsPaginated { get; set; }
    public IReadOnlyCollection<GetExamDto> Exams => ExamsPaginated?.Items ?? [];
    public int TotalPages => ExamsPaginated?.TotalPage ?? 1;
    public bool HasPreviousPage => ExamsPaginated?.HasPreviousPage ?? false;
    public bool HasNextPage => ExamsPaginated?.HasNextPage ?? false;
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var response = await apiClient.GetExamListAsync(
            Search,
            SelectedSeries,
            SelectedYear,
            CurrentPage,
            PageSize
        );

        if (response != null)
        {
            ExamsPaginated = response.Exams;
            SeriesFilters = response.SeriesFilters;
            YearFilters = response.YearFilters;
        }
        else
        {
            ErrorMessage = "Failed to load exam list. Please check the Exam service or API Gateway.";
            ExamsPaginated = new PaginatedList<GetExamDto>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                TotalPage = 1
            };
        }
    }
}
