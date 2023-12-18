using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Created)
            .IsRequired();

        builder.Property(a => a.LastUsed)
            .IsRequired();

        builder.Property(a => a.Pubkey)
            .IsRequired();
        
        builder.Property(a => a.Endpoint)
            .IsRequired();

        builder.Property(a => a.Auth)
            .IsRequired();
        
        builder.Property(a => a.Key)
            .IsRequired();

        builder.Property(a => a.Scope)
            .IsRequired();
    }
}
