using ExamCorrection.Clients;
using FluentValidation.AspNetCore;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.OpenApi;
using Refit;
using System.Reflection;
using Microsoft.AspNetCore.Http.Features; // ضروري لـ FormOptions
using Microsoft.EntityFrameworkCore;    // ضروري لـ UseSqlServer
using System.Text;                      // ضروري لـ Encoding
using Microsoft.IdentityModel.Tokens;   // ضروري لـ TokenValidationParameters
// ... باقي الـ usings
namespace ExamCorrection;

public static class Dependancies
{
    public static IServiceCollection AddDependecies(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
     options.UseSqlServer(
         connectionString,
         sqlOptions =>
         {
             sqlOptions.EnableRetryOnFailure(
                 maxRetryCount: 5,
                 maxRetryDelay: TimeSpan.FromSeconds(10),
                 errorNumbersToAdd: null
             );
         }
     ));


        services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

        services.AddHttpContextAccessor();
        services.AddAuthenticationConfig(configuration);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, EmailService>();
        services.AddScoped<IStudentServices, StudentServices>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<GradingService>();
        services.AddScoped<IExamAiService, ExamAiService>();
        services
    .AddRefitClient<IExamCorrectionClient>()
    .ConfigureHttpClient((sp, c) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["ExamCorrectionAiModel:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "ExamCorrectionAiModel:BaseUrl is missing in appsettings.json");

        c.BaseAddress = new Uri(baseUrl);
    });


        services.AddSwaggerServices();
        services.AddMapsterConfig();
        services.AddFluentValidationConfig();

        services.AddCors(options =>
        {
            options.AddPolicy("myPolicy", builder =>
            {
                builder
                    .WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>()!)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // مهم جدًا مع cookies/session
            });
        });


        return services;
    }

    private static IServiceCollection AddAuthenticationConfig(this IServiceCollection services,
    IConfiguration configuration)
    {
        services.AddSingleton<IJwtProvider, JwtProvider>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration("Jwt")
            .ValidateDataAnnotations();

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtOptions>();

        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key!)),
                ValidIssuer = jwtSettings?.Issuer,
                ValidAudience = jwtSettings?.Audience
            };
        });

        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 8;
            options.SignIn.RequireConfirmedEmail = false;
            options.User.RequireUniqueEmail = false;
        });

        return services;
    }

    //private static IServiceCollection AddBackgroundJobsConfig(this IServiceCollection services, IConfiguration configuration)
    //{
    //    // Add Hangfire services.
    //    services.AddHangfire(config => config
    //        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    //        .UseSimpleAssemblyNameTypeSerializer()
    //        .UseRecommendedSerializerSettings()
    //        .UseSqlServerStorage(configuration.GetConnectionString("HangfireConnection")));

    //    // Add the processing server as IHostedService
    //    services.AddHangfireServer();

    //    return services;    
    //}
    private static IServiceCollection AddFluentValidationConfig(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddFluentValidationAutoValidation();

        return services;
    }

    private static IServiceCollection AddMapsterConfig(this IServiceCollection services)
    {
        var mappingConfig = TypeAdapterConfig.GlobalSettings;
        mappingConfig.Scan(Assembly.GetExecutingAssembly());

        services.AddSingleton<IMapper>(new Mapper(mappingConfig));

        return services;
    }

    private static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Exam Correction",
                Description = "An ASP.NET Core Web API for managing ToDo items"
            });
        });

        return services;
    }
}