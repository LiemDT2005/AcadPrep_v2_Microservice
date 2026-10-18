using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Notifications.Application.Validation;

namespace Notifications.API.Validation;

// Runs before ApiController's automatic 400; applies only to the Admin create endpoint.
public sealed class AdminNotificationValidationFilter : ActionFilterAttribute
{
    public AdminNotificationValidationFilter() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var businessOnly = context.ModelState.Values.SelectMany(value => value.Errors).All(error =>
            error.ErrorMessage is ValidAudienceAttribute.TargetRequired or ValidAudienceAttribute.TargetForbidden);
        var controller = (ControllerBase)context.Controller;
        context.Result = controller.ValidationProblem(statusCode: businessOnly ? 422 : 400,
            modelStateDictionary: context.ModelState);
    }
}
