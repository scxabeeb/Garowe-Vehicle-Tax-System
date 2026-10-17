using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VehicleTax.Web.Security;

/// <summary>
/// A form whose security token is out of date (opened before a sign-out or
/// restart) used to answer with an empty "400 Bad Request" white page. Send
/// the user back to the same page instead, with a short message.
/// </summary>
public sealed class ExpiredFormFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;

        var request = context.HttpContext.Request;
        if (request.Path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectResult("/Account/Login?expired=1" +
                (request.Query.TryGetValue("ReturnUrl", out var r) ? "&ReturnUrl=" + Uri.EscapeDataString(r!) : ""));
            return;
        }

        // Back to the same page (GET, without the form handler); the layout
        // shows a notice for "expired".
        var query = request.Query
            .Where(q => !q.Key.Equals("handler", StringComparison.OrdinalIgnoreCase) && !q.Key.Equals("expired", StringComparison.OrdinalIgnoreCase))
            .Select(q => new KeyValuePair<string, string?>(q.Key, q.Value.ToString()))
            .Append(new KeyValuePair<string, string?>("expired", "1"));
        context.Result = new RedirectResult(request.PathBase + request.Path + QueryString.Create(query));
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
