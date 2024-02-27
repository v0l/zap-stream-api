using System.Security.Claims;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Nostr.Client.Client;
using NostrServices.Client;
using NostrStreamer.Database;
using NostrStreamer.Services;
using NostrStreamer.Services.Background;
using NostrStreamer.Services.Clips;
using NostrStreamer.Services.Dvr;
using NostrStreamer.Services.StreamManager;
using NostrStreamer.Services.Thumbnail;
using Prometheus;
using StackExchange.Redis;

namespace NostrStreamer;

internal static class Program
{
    private static void ConfigureSerializer(JsonSerializerSettings s)
    {
        s.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        s.Formatting = Formatting.None;
        s.NullValueHandling = NullValueHandling.Ignore;
        s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
        s.Converters = new List<JsonConverter>()
        {
            new UnixDateTimeConverter()
        };

        s.ContractResolver = new CamelCasePropertyNamesContractResolver();
    }

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var services = builder.Services;
        var config = builder.Configuration.GetSection("Config").Get<Config>();

        if (config == default)
        {
            throw new Exception("Config is missing!");
        }

        ConfigureDb(services, builder.Configuration);
        services.AddCors();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddRazorPages();
        services.AddControllers().AddNewtonsoftJson(opt => { ConfigureSerializer(opt.SerializerSettings); });

        services.AddSwaggerGen();
        services.AddSingleton(config);

        // Redis
        var cx = await ConnectionMultiplexer.ConnectAsync(config.Redis);
        services.AddSingleton(cx);
        services.AddTransient<IDatabase>(svc => svc.GetRequiredService<ConnectionMultiplexer>().GetDatabase());

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
            }, new[] { NostrAuth.Scheme });
        });

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(config.DataProtectionKeyPath));

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

        // dvr services
        services.AddTransient<IDvrStore, S3DvrStore>();
        services.AddHostedService<RecordingDeleter>();

        // thumbnail services
        services.AddTransient<IThumbnailService, S3ThumbnailService>();
        services.AddHostedService<ThumbnailGenerator>();

        // lnd services
        services.AddSingleton<LndNode>();
        services.AddHostedService<LndInvoicesStream>();

        // game services
        services.AddSingleton<GameDb>();

        // clip services
        services.AddTransient<ClipGenerator>();
        services.AddTransient<IClipService, S3ClipService>();

        // notifications services
        services.AddSingleton<PushSender>();
        services.AddHostedService<PushSenderService>();
        services.AddHostedService<EventStream>();

        // webhooks
        services.AddTransient<DiscordWebhook>();

        // snort api
        services.AddTransient<NostrServicesClient>();

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
        app.UseSwagger();
        app.UseSwaggerUI();

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