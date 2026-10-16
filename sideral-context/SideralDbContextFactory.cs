using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace sideral_context;

/// <summary>
/// Factory utilisée par dotnet-ef (migrations). La chaîne de connexion est, par ordre de priorité :
/// la variable d'environnement SIDERAL_CONNECTION_STRING, puis ConnectionStrings:Sideral de
/// sideral-api/appsettings.Development.json, puis sideral-api/appsettings.json.
/// </summary>
public class SideralDbContextFactory : IDesignTimeDbContextFactory<SideralDbContext>
{
    public SideralDbContext CreateDbContext(string[] args)
    {
        var apiFolder = _FindApiFolder();

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiFolder, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(apiFolder, "appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["SIDERAL_CONNECTION_STRING"]
            ?? config.GetConnectionString("Sideral")
            ?? throw new InvalidOperationException("No connection string: set SIDERAL_CONNECTION_STRING or ConnectionStrings:Sideral in sideral-api/appsettings.Development.json.");

        var builder = new DbContextOptionsBuilder<SideralDbContext>();
        SideralDbContext.Configure(builder, connectionString);
        return new SideralDbContext(builder.Options);
    }

    // dotnet-ef peut être lancé depuis la racine de la solution ou depuis un projet
    private static string _FindApiFolder()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "sideral-api");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
