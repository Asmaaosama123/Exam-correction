using ExamCorrection;

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();

// ⚠️ UseCors لازم يكون قبل UseAuthorization
app.UseCors("myPolicy");

app.UseAuthorization();

app.MapControllers();

app.Run();
