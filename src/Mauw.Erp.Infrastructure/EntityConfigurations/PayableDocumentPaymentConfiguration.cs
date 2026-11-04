using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class PayableDocumentPaymentConfiguration : IEntityTypeConfiguration<PayableDocumentPayment>
{
    public void Configure(EntityTypeBuilder<PayableDocumentPayment> builder)
    {
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.PaymentDateUtc).IsRequired();
        builder.Property(payment => payment.PaymentMethod).HasMaxLength(50).IsRequired();
        builder.Property(payment => payment.TransactionDetails).HasMaxLength(500);
        builder.Property(payment => payment.CreatedByUserId).HasMaxLength(200);
        builder.Property(payment => payment.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(payment => payment.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(payment => new { payment.PayableDocumentId, payment.PaymentDateUtc });
    }
}
