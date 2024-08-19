using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class StreamTicketsConfiguration : IEntityTypeConfiguration<StreamTickets>
{
    public void Configure(EntityTypeBuilder<StreamTickets> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Created)
            .IsRequired();
        
        builder.Property(a => a.Token)
            .IsRequired();

        builder.HasOne(a => a.UserStream)
            .WithMany()
            .HasForeignKey(a => a.UserStreamId);
    }
}