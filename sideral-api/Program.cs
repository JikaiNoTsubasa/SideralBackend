using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using dependancies.Global;
using dependancies.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using sideral_api.Classes;
using sideral_context;
using sideral_context.Users;
using sideral_jobcode.Auth;
using sideral_jobcode.Users;

namespace sideral_api;

public partial class Program
{
    public const string ADMIN_POLICY = "Admin";
    public const string AUTH_RATE_LIMIT = "auth";
    public const string CORS_POLICY = "Frontend";

    public static async Task Main(string[] args)
    {
        // Outil CLI : dotnet run --project sideral-api -- --hash "motdepasse"
        if (args.Length == 2 && args[0] == "--hash")
        {
            Console.WriteLine(new HashPBKDF2().Hash(args[1]));
            return;
        }

        var builder = WebApplication.CreateBuilder(args);

        // ---- Configuration
        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SECTION).Get<JwtSettings>() ?? new JwtSettings();
        jwtSettings.Validate();
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SECTION));

        var connectionString = builder.Configuration.GetConnectionString("Sideral")
            ?? throw new InvalidOperationException("ConnectionStrings:Sideral is missing.");

        // ---- Services
        builder.Services.AddDbContext<SideralDbContext>(options => SideralDbContext.Configure(options, connectionString));
        builder.Services.AddMarkedServices(typeof(IHash).Assembly, typeof(AuthManager).Assembly);

        builder.Services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
        {
            // Les erreurs de validation de modèle n'ont pas de "detail" par défaut : on y agrège les messages
            if (ctx.ProblemDetails is Microsoft.AspNetCore.Mvc.ValidationProblemDetails validation && validation.Detail is null)
            {
                validation.Detail = string.Join(" ", validation.Errors.SelectMany(e => e.Value));
            }
        });
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddOpenApi();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthorizationTokenGenerator.LOGIN_CLAIM,
                    RoleClaimType = AuthorizationTokenGenerator.ROLE_CLAIM
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ADMIN_POLICY, policy => policy.RequireRole(Role.ADMIN));

        builder.Services.AddCors(options => options.AddPolicy(CORS_POLICY, policy =>
            policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders(PaginationMeta.HEADERS)));

        // Limite les tentatives de login / refresh par IP
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AUTH_RATE_LIMIT, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        var app = builder.Build();

        // ---- Pipeline
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(); // UI : /scalar
        }
        else
        {
            app.UseHttpsRedirection();
        }

        app.UseCors(CORS_POLICY);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await _SeedAsync(app);

        await app.RunAsync();
    }

    private static async Task _SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Initial seeding failed (is the database created and migrated?).");
        }
    }
}
