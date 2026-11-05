using Billing.API.Data.Converters;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class PaymentLogConfiguration : IEntityTypeConfiguration<PaymentLog>
{
    public void Configure(EntityTypeBuilder<PaymentLog> builder)
    {
        builder.ToTable("PaymentLogs", table =>
            table.HasCheckConstraint("CK_PaymentLogs_Type", "[Type] IN ('Create', 'Return', 'Ipn')"));
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.OrderCode).HasMaxLength(20);
        builder.Property(log => log.Type).HasMaxLength(10).IsRequired();
        builder.Property(log => log.RspCode).HasMaxLength(4);
        builder.Property(log => log.Result).HasMaxLength(200);
        builder.Property(log => log.CreatedAt).HasConversion<UtcDateTimeConverter>();
        builder.HasIndex(log => new { log.OrderCode, log.CreatedAt });
    }
}
