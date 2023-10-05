using System.Security.Claims;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Nostr.Client.Client;
using NostrStreamer.Database;
using NostrStreamer.Services;
using NostrStreamer.Services.Background;
using NostrStreamer.Services.Dvr;
using NostrStreamer.Services.StreamManager;
using NostrStreamer.Services.Thumbnail;
using Prometheus;

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
        services.AddRazorPages();
        services.AddControllers().AddNewtonsoftJson();
        services.AddSingleton(config);

        // GeoIP
        services.AddSingleton<IGeoIP2DatabaseReader>(_ => new DatabaseReader(config.GeoIpDatabase));
        services.AddTransient<EdgeSteering>();

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
        services.AddTransient<ZapService>();

        // streaming services
        services.AddTransient<SrsApi>();
        services.AddHostedService<BackgroundStreamManager>();
        services.AddSingleton<ViewCounter>();
        services.AddHostedService<ViewCounterDecay>();
        services.AddTransient<StreamEventBuilder>();
        services.AddTransient<StreamManagerFactory>();
        services.AddTransient<UserService>();

        services.AddTransient<IThumbnailService, S3ThumbnailService>();
        services.AddHostedService<ThumbnailGenerator>();
        services.AddTransient<IDvrStore, S3DvrStore>();

        // lnd services
        services.AddSingleton<LndNode>();
        services.AddHostedService<LndInvoicesStream>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
            await db.Database.MigrateAsync();
        }

        app.UseRouting();
        app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
        app.UseHttpMetrics();
        app.UseAuthorization();

        app.MapRazorPages();
        app.MapControllers();
        app.MapMetrics();

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
