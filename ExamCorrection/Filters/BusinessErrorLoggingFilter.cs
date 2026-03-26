using ExamCorrection.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace ExamCorrection.Filters;

public class BusinessErrorLoggingFilter(ISystemLogService systemLogService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultContext = await next();

        if (resultContext.Result is ObjectResult objectResult && 
            objectResult.Value is ProblemDetails problem)
        {
            // Log 400-499 errors (Validation, Conflict, NotFound, etc. from Result.Failure)
            // Also log if StatusCode is null (meaning it wasn't explicitly set but is still a ProblemDetails)
            if (objectResult.StatusCode == null || (objectResult.StatusCode >= 400 && objectResult.StatusCode < 500))
            {
                var userId = context.HttpContext.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                
                // Extract errors from ProblemDetails extensions (populated by ResultExtensions.ToProblem)
                if (problem.Extensions.TryGetValue("errors", out var errorsObj) && errorsObj is Array errors)
                {
                    foreach (var err in errors)
                    {
                        if (err == null) continue;

                        // Use reflection to get Code and Description since they are anonymous types in ResultExtensions
                        var type = err.GetType();
                        var code = type.GetProperty("Code")?.GetValue(err)?.ToString() ?? "UNKNOWN_CODE";
                        var description = type.GetProperty("Description")?.GetValue(err)?.ToString() ?? "No description provided";
                        
                        await systemLogService.LogErrorAsync(
                            code, 
                            description, 
                            "BUSINESS_LOGIC", 
                            userId
                        );
                    }
                }
            }
        }
    }
}
