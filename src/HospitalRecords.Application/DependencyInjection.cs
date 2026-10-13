using HospitalRecords.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalRecords.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEncounterService, EncounterService>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services;
    }
}
