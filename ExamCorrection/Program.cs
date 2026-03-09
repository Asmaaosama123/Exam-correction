
using Microsoft.AspNetCore.Server.Kestrel.Core; // أضف هذا السطر في أعلى Program.cs
using ExamCorrection;

var builder = WebApplication.CreateBuilder(args);

// 🌐 Configure Kestrel for large file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2147483648; // 2GB
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDependecies(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ExamCorrection.Entities.ApplicationUser>>();
    var adminEmail = "superadmin@wsyli.com";
    
    // Create the admin user if it doesn't exist already
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminUser = new ExamCorrection.Entities.ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Super",
            LastName = "Admin",
            PhoneNumber = "2221111111",
            IsDisabled = false
        };
        
        await userManager.CreateAsync(adminUser, "SuperAdmin@2026");
    }
}
// ⚠️ فعل Swagger لكل الـ environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Exam Correction v1");
    c.RoutePrefix = "swagger"; // رابط الواجهة: /swagger/index.html
});

//app.UseHttpsRedirection();

app.UseCors("myPolicy");

app.UseStaticFiles(); // Default wwwroot

// Safely serve the AI training dataset folder
var datasetPath = Path.Combine(builder.Environment.WebRootPath, "AI-Dataset");
if (!Directory.Exists(datasetPath))
{
    Directory.CreateDirectory(datasetPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(datasetPath),
    RequestPath = "/AI-Dataset"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
