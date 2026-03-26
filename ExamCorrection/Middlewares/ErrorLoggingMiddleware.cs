using ExamCorrection.Services;
using System.Net;
using System.Text.Json;

namespace ExamCorrection.Middlewares;

public class ErrorLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISystemLogService systemLogService)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Log the exception into the database
            try
            {
                var details = $"Exception: {ex.Message}\nStackTrace: {ex.StackTrace}";
                var source = $"BACKEND_EXCEPTION_{ex.GetType().Name}";
                
                await systemLogService.LogErrorAsync(
                    "خطأ تقني غير متوقع في النظام",
                    details,
                    source,
                    context.User?.Identity?.IsAuthenticated == true ? context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null
                );
            }
            catch
            {
                // Fallback: If DB logging fails, we just don't want to crash the whole request-handling pipeline entirely
                // In a deeper implementation, we'd log to a local file here
            }

            // Return a standardized 500 Server Error response
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new 
            { 
                Message = "An unexpected server error occurred.", 
                Error = ex.Message 
            };
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
