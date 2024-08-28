using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserStreamConfiguration : IEntityTypeConfiguration<UserStream>
{
    public void Configure(EntityTypeBuilder<UserStream> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Starts)
            .IsRequired();

        builder.Property(a => a.Ends);

        builder.Property(a => a.State)
            .IsRequired();

        builder.Property(a => a.Event)
            .IsRequired();

        builder.Property(a => a.Thumbnail);

        builder.Property(a => a.EdgeIp)
            .IsRequired();

        builder.Property(a => a.ForwardClientId)
            .IsRequired();

        builder.Property(a => a.LastSegment)
            .IsRequired();

        builder.Property(a => a.AdmissionCost);

        builder.Property(a => a.Title);
        builder.Property(a => a.Image);
        builder.Property(a => a.Summary);
        builder.Property(a => a.ContentWarning);
        builder.Property(a => a.Tags);
        builder.Property(a => a.Goal);

        builder.HasOne(a => a.Endpoint)
            .WithMany()
            .HasForeignKey(a => a.EndpointId);

        builder.HasOne(a => a.User)
            .WithMany(a => a.Streams)
            .HasForeignKey(a => a.PubKey);
    }
}