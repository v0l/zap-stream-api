using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Nostr.Client.Client;
using NostrStreamer.Database;
using NostrStreamer.Services;

namespace NostrStreamer;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var services = builder.Services;
        var config = builder.Configuration.GetSection("Config").Get<Config>();
        
        ConfigureDb(services, builder.Configuration);
        services.AddCors();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddControllers().AddNewtonsoftJson();
        services.AddSingleton(config);
        
        // nostr auth
        services.AddTransient<NostrAuthHandler>();
        services.AddAuthentication(o =>
        {
            o.DefaultChallengeScheme = NostrAuth.Scheme;
            o.AddScheme<NostrAuthHandler>(NostrAuth.Scheme, "Nostr");
        });

        services.AddAuthorization(o =>
        {
            o.DefaultPolicy = new AuthorizationPolicy(new[]
            {
                new ClaimsAuthorizationRequirement(ClaimTypes.Name, null)
            }, new[] {NostrAuth.Scheme});
        });
        
        // nostr services
        services.AddSingleton<NostrMultiWebsocketClient>();
        services.AddSingleton<INostrClient>(s => s.GetRequiredService<NostrMultiWebsocketClient>());
        services.AddSingleton<NostrListener>();
        services.AddHostedService<NostrListenerLifetime>();
        
        // streaming services
        services.AddTransient<StreamManager>();
        services.AddTransient<SrsApi>();
        
        // lnd services
        services.AddSingleton<LndNode>();
        services.AddHostedService<LndInvoicesStream>();
        
        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
            await db.Database.MigrateAsync();
        }

        app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
    
    private static void ConfigureDb(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StreamerContext>(o => o.UseNpgsql(configuration.GetConnectionString("Database")));
    }

    /// <summary>
    /// Dummy method for EF core migrations
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var dummyHost = Host.CreateDefaultBuilder(args);
        dummyHost.ConfigureServices((ctx, svc) => { ConfigureDb(svc, ctx.Configuration); });

        return dummyHost;
    }
}