
using Microsoft.AspNetCore.Server.Kestrel.Core; // أضف هذا السطر في أعلى Program.cs
using ExamCorrection;
using ExamCorrection.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// 🌐 Configure Kestrel for large file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2147483648; // 2GB
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ExamCorrection.Filters.BusinessErrorLoggingFilter>();
});
builder.Services.AddDependecies(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<ExamCorrection.Entities.ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ExamCorrection.Entities.ApplicationUser>>();

    // 1. Ensure Roles Exist
    string[] roles = ["Admin", "AITrainer", "Teacher"];
    foreach (var roleName in roles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new ExamCorrection.Entities.ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpper() });
        }
    }

    // 2. Seed Super Admin
    var adminEmail = "superadmin@wsyli.com";
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
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // 3. Seed AI Trainer
    var trainerEmail = "trainer@wsyli.com";
    if (await userManager.FindByEmailAsync(trainerEmail) == null)
    {
        var trainerUser = new ExamCorrection.Entities.ApplicationUser
        {
            UserName = trainerEmail,
            Email = trainerEmail,
            FirstName = "AI",
            LastName = "Trainer",
            PhoneNumber = "5551111111",
            IsDisabled = false
        };
        
        await userManager.CreateAsync(trainerUser, "AITrainer@2026");
        await userManager.AddToRoleAsync(trainerUser, "AITrainer");
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

// Add global error logging
app.UseMiddleware<ErrorLoggingMiddleware>();

var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".mp4"] = "video/mp4";
provider.Mappings[".mov"] = "video/quicktime";
provider.Mappings[".avi"] = "video/x-msvideo";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Context.Response.Headers["Access-Control-Allow-Headers"] = "Origin, X-Requested-With, Content-Type, Accept, Range";
        ctx.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        ctx.Context.Response.Headers["Accept-Ranges"] = "bytes";
    }
}); // Default wwwroot

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
