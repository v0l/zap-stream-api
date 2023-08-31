using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NostrStreamer.Database.Configuration;

public class PaymentsConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(a => a.PaymentHash);
        builder.Property(a => a.Invoice);

        builder.Property(a => a.IsPaid)
            .IsRequired();

        builder.Property(a => a.Amount)
            .IsRequired();

        builder.Property(a => a.Created)
            .IsRequired();

        builder.Property(a => a.Nostr);
        builder.Property(a => a.Type)
            .IsRequired();

        builder.HasOne(a => a.User)
            .WithMany(a => a.Payments)
            .HasForeignKey(a => a.PubKey);
    }
}
