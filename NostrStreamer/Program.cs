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
        services.AddControllers();
        services.AddSingleton(config);
        
        // nostr services
        services.AddSingleton<NostrMultiWebsocketClient>();
        services.AddSingleton<INostrClient>(s => s.GetRequiredService<NostrMultiWebsocketClient>());
        services.AddSingleton<NostrListener>();
        services.AddHostedService<NostrListenerLifetime>();
        
        // streaming services
        services.AddTransient<StreamManager>();
        
        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StreamerContext>();
            await db.Database.MigrateAsync();
        }

        app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
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