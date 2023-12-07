using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserStreamForwardsConfiguration : IEntityTypeConfiguration<UserStreamForwards>
{
    public void Configure(EntityTypeBuilder<UserStreamForwards> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name);
        builder.Property(a => a.Target);

        builder.HasOne(a => a.User)
            .WithMany(a => a.Forwards)
            .HasForeignKey(a => a.UserPubkey)
            .HasPrincipalKey(a => a.PubKey);
    }
}
