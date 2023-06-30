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
}
