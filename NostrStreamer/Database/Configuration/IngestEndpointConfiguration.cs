using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class IngestEndpointConfiguration : IEntityTypeConfiguration<IngestEndpoint>
{
    public void Configure(EntityTypeBuilder<IngestEndpoint> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired();

        builder.Property(a => a.App)
            .IsRequired();

        builder.Property(a => a.Forward)
            .IsRequired();

        builder.Property(a => a.Cost)
            .IsRequired();

        builder.Property(a => a.Capabilities)
            .IsRequired();

        builder.HasIndex(a => a.App)
            .IsUnique();
    }
}
