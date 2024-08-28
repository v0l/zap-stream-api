using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(a => a.PubKey);
        builder.Property(a => a.StreamKey)
            .IsRequired();

        builder.Property(a => a.Balance)
            .IsRequired();

        builder.Property(a => a.TosAccepted);
        
        builder.Property(a => a.Version)
            .IsRowVersion();

        builder.Property(a => a.IsAdmin);
        builder.Property(a => a.IsBlocked);
    }
}
