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
                
                // 1. Extract custom Errors (from ResultExtensions)
                if (problem.Extensions.TryGetValue("errors", out var errorsObj) && errorsObj is Array errors)
                {
                    foreach (var err in errors)
                    {
                        if (err == null) continue;
                        var type = err.GetType();
                        var code = type.GetProperty("Code")?.GetValue(err)?.ToString() ?? "UNKNOWN_CODE";
                        var description = type.GetProperty("Description")?.GetValue(err)?.ToString() ?? "No description provided";
                        
                        await systemLogService.LogErrorAsync(description, $"Code: {code}", "BUSINESS_LOGIC", userId);
                    }
                }
                // 2. Extract standard Validation Errors (ModelState/ValidationProblemDetails)
                else if (problem is ValidationProblemDetails validationProblem)
                {
                    foreach (var keyValuePair in validationProblem.Errors)
                    {
                        var field = keyValuePair.Key;
                        foreach (var errorMessage in keyValuePair.Value)
                        {
                            await systemLogService.LogErrorAsync(
                                $"خطأ في إدخال بيانات: {errorMessage}", 
                                $"Field: {field}", 
                                "VALIDATION_ERROR", 
                                userId
                            );
                        }
                    }
                }
                // 3. Fallback for other ProblemDetails
                else if (!string.IsNullOrEmpty(problem.Title))
                {
                    await systemLogService.LogErrorAsync(problem.Title, problem.Detail ?? string.Empty, "BUSINESS_LOGIC", userId);
                }
            }
        }
    }
}
