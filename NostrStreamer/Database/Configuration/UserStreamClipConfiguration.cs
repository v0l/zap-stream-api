using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserStreamClipConfiguration : IEntityTypeConfiguration<UserStreamClip>
{
    public void Configure(EntityTypeBuilder<UserStreamClip> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Created)
            .IsRequired();

        builder.Property(a => a.TakenByPubkey)
            .IsRequired();

        builder.Property(a => a.Url)
            .IsRequired();

        builder.HasOne(a => a.UserStream)
            .WithMany()
            .HasForeignKey(a => a.UserStreamId)
            .HasPrincipalKey(a => a.Id);
    }
}
