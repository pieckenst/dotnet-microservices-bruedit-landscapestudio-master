using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LandscapeStudio.Web.Filters;

public sealed class SessionAuthorizationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        if (string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(context.HttpContext.Session.GetString("access_token")))
        {
            await next();
            return;
        }

        context.Result = new RedirectToActionResult("Login", "Auth", null);
    }
}
