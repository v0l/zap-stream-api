using Microsoft.EntityFrameworkCore;

namespace NostrStreamer.Database;

public class StreamerContext : DbContext
{
    public StreamerContext()
    {
    }

    public StreamerContext(DbContextOptions<StreamerContext> ctx) : base(ctx)
    {
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StreamerContext).Assembly);
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<UserStream> Streams => Set<UserStream>();

    public DbSet<UserStreamGuest> Guests => Set<UserStreamGuest>();

    public DbSet<IngestEndpoint> Endpoints => Set<IngestEndpoint>();

    public DbSet<UserStreamRecording> Recordings => Set<UserStreamRecording>();
}
