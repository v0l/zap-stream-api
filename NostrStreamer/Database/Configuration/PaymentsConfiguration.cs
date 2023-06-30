using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class PaymentsConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(a => a.PubKey);
        builder.Property(a => a.Invoice)
            .IsRequired();

        builder.Property(a => a.IsPaid)
            .IsRequired();
    }
}
