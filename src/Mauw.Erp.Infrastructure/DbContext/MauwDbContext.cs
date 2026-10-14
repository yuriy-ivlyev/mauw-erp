using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity Framework Core DbContext for Mauw ERP system.
/// Configured with Npgsql for PostgreSQL/Aurora with snake_case naming conventions.
/// </summary>
public class MauwDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<DdtDocument> DdtDocuments { get; set; }
    public DbSet<DdtItem> DdtItems { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<AgentCommission> AgentCommissions { get; set; }

    public MauwDbContext(DbContextOptions<MauwDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming conventions for PostgreSQL
        var converter = new SnakeCaseNameConverter();
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(converter.ConvertName(entity.GetTableName()!));

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(converter.ConvertName(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(converter.ConvertName(key.GetName()!));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(converter.ConvertName(foreignKey.GetConstraintName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(converter.ConvertName(index.GetDatabaseName() ?? $"ix_{entity.GetTableName()}_{string.Join("_", index.Properties.Select(p => p.Name))}"));
            }
        }

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new ContactConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductVariantConfiguration());
        modelBuilder.ApplyConfiguration(new StockMovementConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new DdtDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new DdtItemConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceItemConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new AgentCommissionConfiguration());
    }

    /// <summary>
    /// Converts PascalCase names to snake_case for PostgreSQL.
    /// </summary>
    private class SnakeCaseNameConverter
    {
        public string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0)
                {
                    builder.Append('_');
                    builder.Append(char.ToLower(c));
                }
                else
                {
                    builder.Append(char.ToLower(c));
                }
            }
            return builder.ToString();
        }
    }
}
