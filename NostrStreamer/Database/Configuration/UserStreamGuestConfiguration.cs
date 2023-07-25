using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserStreamGuestConfiguration : IEntityTypeConfiguration<UserStreamGuest>
{
    public void Configure(EntityTypeBuilder<UserStreamGuest> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.PubKey)
            .IsRequired();

        builder.Property(a => a.Sig);
        builder.Property(a => a.Relay);
        builder.Property(a => a.Role);
        builder.Property(a => a.ZapSplit);

        builder.HasOne(a => a.Stream)
            .WithMany(a => a.Guests)
            .HasForeignKey(a => a.StreamId);
        
        builder.HasIndex(a => new {a.StreamId, a.PubKey})
            .IsUnique();
    }
}
