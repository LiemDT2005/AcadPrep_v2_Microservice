using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Billing;

public class SubscriptionModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    protected override string RequiredRole => "Learner";
    public IActionResult OnGet() => RedirectToPage("/Billing/Plans", pageHandler: null, routeValues: null, fragment: "my-plan");
}
