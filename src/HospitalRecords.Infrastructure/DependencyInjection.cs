using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Infrastructure.Persistence;
using HospitalRecords.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalRecords.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=bpkmch_records;Username=bpkmch_admin;Password=BpkmchSecure2026!;Include Error Detail=true";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IFileSecurityValidator, FileSecurityValidator>();
        services.AddSingleton<IObjectStorageService, FileSystemObjectStorageService>();
        services.AddHttpClient();
        services.AddScoped<IOcrProcessingService, OcrProcessingService>();
        services.AddScoped<IClinicalEntityExtractionService, ClinicalEntityExtractionService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IRagChatService, RagChatService>();

        return services;
    }
}
