using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class PushSubscriptionTargetConfiguration : IEntityTypeConfiguration<PushSubscriptionTarget>
{
    public void Configure(EntityTypeBuilder<PushSubscriptionTarget> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TargetPubkey)
            .IsRequired();

        builder.Property(a => a.SubscriberPubkey)
            .IsRequired();

        builder.HasIndex(a => a.TargetPubkey);

        builder.HasIndex(a => new {a.SubscriberPubkey, a.TargetPubkey})
            .IsUnique();
    }
}
