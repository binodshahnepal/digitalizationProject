using System.Text;
using HospitalRecords.Application;
using HospitalRecords.Infrastructure;
using HospitalRecords.Infrastructure.Persistence;
using HospitalRecords.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Application and Infrastructure Services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add Controllers & JSON Serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// 3. Configure JWT Authentication (NFR-001, NFR-002)
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "BPKMCH_HOSPITAL_AI_SUPER_SECURE_KEY_2026_NEPAL_LONGITUDINAL_SECRET_STRICT";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "bpkmch.gov.np";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "bpkmch-clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. Configure Swagger / OpenAPI Documentation with JWT Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BPKMCH Hospital Records Digitization & Intelligence API",
        Version = "v1",
        Description = "Secure API for longitudinal patient records, document ingestion, and local AI intelligence."
    });

    var scheme = new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", scheme);

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

// 5. Configure CORS for Angular Web Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 6. Database Migration & Seeding Routine
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        logger.LogInformation("Applying database migrations if needed...");
        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(context, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations or seed data.");
    }
}

// 7. HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BPKMCH API v1");
    });
}

app.UseCors("AllowLocalAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
