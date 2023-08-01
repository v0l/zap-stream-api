using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class UserStreamRecordingConfiguration : IEntityTypeConfiguration<UserStreamRecording>
{
    public void Configure(EntityTypeBuilder<UserStreamRecording> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Url)
            .IsRequired();

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.Duration)
            .IsRequired();
        
        builder.HasOne(a => a.Stream)
            .WithMany()
            .HasForeignKey(a => a.UserStreamId);
    }
}
