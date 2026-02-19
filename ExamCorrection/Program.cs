using ExamCorrection;

var builder = WebApplication.CreateBuilder(args);

// 🌐 Configure Kestrel for large file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100MB
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDependecies(builder.Configuration);

var app = builder.Build();

// ⚠️ فعل Swagger لكل الـ environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Exam Correction v1");
    c.RoutePrefix = "swagger"; // رابط الواجهة: /swagger/index.html
});

//app.UseHttpsRedirection();

app.UseCors("myPolicy");

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
