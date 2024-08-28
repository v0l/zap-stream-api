using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserStreamKeyConfiguration : IEntityTypeConfiguration<UserStreamKey>
{
    public void Configure(EntityTypeBuilder<UserStreamKey> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key)
            .IsRequired();

        builder.Property(a => a.Created)
            .IsRequired();

        builder.Property(a => a.Expires)
            .IsRequired(false);

        builder.HasOne(a => a.UserStream)
            .WithOne(a => a.StreamKey)
            .HasPrincipalKey<UserStream>(a => a.Id)
            .HasForeignKey<UserStreamKey>(a => a.StreamId);

        builder.HasOne(a => a.User)
            .WithMany(a => a.StreamKeys)
            .HasForeignKey(a => a.UserPubkey)
            .HasPrincipalKey(a => a.PubKey);
    }
}