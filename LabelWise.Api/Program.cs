using LabelWise.Application;
using LabelWise.Infrastructure.Extensions;
using LabelWise.Shared.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using LabelWise.Api.Swagger;
using LabelWise.Infrastructure.Persistence.Mongo;

Console.WriteLine("========================================");
Console.WriteLine("LabelWise API - Starting...");
Console.WriteLine("========================================");

try
{
    Console.WriteLine("[1/8] Creating WebApplication builder...");
    var builder = WebApplication.CreateBuilder(args);
   // builder.WebHost.UseUrls("http://0.0.0.0:8080");

    // Configuration
    Console.WriteLine("[2/8] Loading configuration...");
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                         .AddEnvironmentVariables();
    Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

    Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

    // CORS
    Console.WriteLine("[3/8] Configuring CORS...");
    builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Em desenvolvimento, permite qualquer origem (para Swagger e testes)
            options.AddPolicy("DefaultCorsPolicy", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
            Console.WriteLine("CORS: Configured for Development (Allow Any Origin)");
        }
        else
        {
            // Em produção, usa origins específicas
            var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new string[] { };
            options.AddPolicy("DefaultCorsPolicy", policy =>
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
            Console.WriteLine($"CORS: Configured for Production (Allowed Origins: {string.Join(", ", corsOrigins)})");
        }
    });

    // Shared Options
    Console.WriteLine("[4/8] Configuring options...");
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

    var applicationInsightsConnectionString =
        builder.Configuration["ApplicationInsights:ConnectionString"]
        ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

    if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = applicationInsightsConnectionString;
        });

        Console.WriteLine("Application Insights: enabled");
    }
    else
    {
        Console.WriteLine("Application Insights: disabled (no connection string configured)");
    }

    // Application & Infrastructure
    Console.WriteLine("[5/8] Registering Application services...");
    builder.Services.AddApplicationServices();

    Console.WriteLine("[6/8] Registering Infrastructure services...");
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Authentication - JWT
    Console.WriteLine("[7/8] Configuring JWT Authentication...");
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var key = jwtSection.GetValue<string>("Key");
    var issuer = jwtSection.GetValue<string>("Issuer");
    var audience = jwtSection.GetValue<string>("Audience");
    if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("JWT Key is not configured.");
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

    builder.Services.AddAuthorization();

    builder.Services.AddControllers();

    // Swagger with JWT support
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(cfg =>
    {
        cfg.SwaggerDoc("v1", new OpenApiInfo { Title = "LabelWise API", Version = "v1" });

        cfg.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);

        // Suporte para file uploads (IFormFile)
        cfg.OperationFilter<FileUploadOperationFilter>();

        var jwtSecurityScheme = new OpenApiSecurityScheme
        {
            Scheme = "bearer",
            BearerFormat = "JWT",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Description = "Enter JWT Bearer token only",
            Reference = new OpenApiReference
            {
                Id = JwtBearerDefaults.AuthenticationScheme,
                Type = ReferenceType.SecurityScheme
            }
        };

        cfg.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
        cfg.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { jwtSecurityScheme, Array.Empty<string>() }
        });
    });

    Console.WriteLine("[8/8] Building application...");
    var app = builder.Build();
    Console.WriteLine("Application built successfully!");

    if (args.Contains("--seed-mongo-bootstrap", StringComparer.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<MongoLegacyBootstrapSeeder>();
        await seeder.SeedAsync();
        Console.WriteLine("Mongo bootstrap concluído com sucesso.");
        return;
    }

    Console.WriteLine("Configuring middleware pipeline...");

    Console.WriteLine("Configuring middleware pipeline...");

    if (app.Environment.IsDevelopment())
    {
        Console.WriteLine("Running in Development mode");
        app.UseDeveloperExceptionPage();
    }

    app.UseCors("DefaultCorsPolicy");

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LabelWise API v1");
        c.RoutePrefix = "swagger";
    });

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Console.WriteLine("========================================");
    Console.WriteLine("LabelWise API is ready!");
    Console.WriteLine($"Swagger UI: {(app.Environment.IsDevelopment() ? "https://localhost:7001/swagger" : "/swagger")}");
    Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine("========================================");

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        Console.WriteLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
    }
    Console.WriteLine($"StackTrace: {ex.StackTrace}");
    Environment.Exit(1);
}
