using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace CradleSoft.DMS.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var appStoragePath = ResolveAppStoragePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(appStoragePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dbProvider = configuration["DatabaseProvider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            || dbProvider.Equals("MSSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
            return new AppDbContext(optionsBuilder.Options);
        }

        if (dbProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
            || dbProvider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var npgsqlAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == "Npgsql.EntityFrameworkCore.PostgreSQL")
                ?? Assembly.Load("Npgsql.EntityFrameworkCore.PostgreSQL");

            var extensionsType = npgsqlAssembly.GetType("Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions")
                ?? throw new InvalidOperationException("Npgsql provider extensions were not found.");

            var useNpgsqlMethod = extensionsType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "UseNpgsql") return false;
                    var parameters = m.GetParameters();
                    return parameters.Length >= 2
                           && parameters[0].ParameterType == typeof(DbContextOptionsBuilder)
                           && parameters[1].ParameterType == typeof(string);
                })
                ?? throw new InvalidOperationException("Compatible UseNpgsql overload was not found.");

            useNpgsqlMethod.Invoke(null, new object?[] { optionsBuilder, connectionString });
            return new AppDbContext(optionsBuilder.Options);
        }

        throw new InvalidOperationException($"Unsupported DatabaseProvider '{dbProvider}'. Use SqlServer or PostgreSql.");
    }

    private static string ResolveAppStoragePath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "App_Storage");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate App_Storage directory for design-time configuration.");
    }
}
