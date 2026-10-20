using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Application.MasterData;
using Qms.Application.Settings;
using Qms.Application.Users;
using Qms.Infrastructure.Auditing;
using Qms.Infrastructure.Collection;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;

namespace Qms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQmsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connection =
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "A connection string 'Default' não foi configurada."
            );

        services.AddDbContext<QmsDbContext>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(connection);
            else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connection);
            else
                throw new InvalidOperationException(
                    $"Provedor de banco de dados desconhecido: {provider}."
                );
        });
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<MasterDataService>();
        services.AddScoped<IMasterDataService>(provider =>
            provider.GetRequiredService<MasterDataService>()
        );
        services.AddScoped<IInstrumentService>(provider =>
            provider.GetRequiredService<MasterDataService>()
        );
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<ICepDatasetService, CepDatasetService>();
        services.AddScoped<ICepAnalysisService, CepAnalysisService>();
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<IStudyFileReader, StudyFileReader>();
        services.AddScoped<ICepOperationsService, CepOperationsService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<ITemplateService, TemplateService>();
        return services;
    }
}
