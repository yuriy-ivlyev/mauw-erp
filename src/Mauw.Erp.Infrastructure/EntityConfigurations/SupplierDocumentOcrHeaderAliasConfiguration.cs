using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class SupplierDocumentOcrHeaderAliasConfiguration
    : IEntityTypeConfiguration<SupplierDocumentOcrHeaderAlias>
{
    public void Configure(EntityTypeBuilder<SupplierDocumentOcrHeaderAlias> builder)
    {
        builder.ToTable("supplier_document_ocr_header_aliases");
        builder.HasKey(alias => alias.Id);
        builder.Property(alias => alias.Column).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(alias => alias.Alias).HasMaxLength(100).IsRequired();
        builder.Property(alias => alias.NormalizedAlias).HasMaxLength(100).IsRequired();
        builder.HasIndex(alias => alias.NormalizedAlias).IsUnique();
        builder.HasIndex(alias => new { alias.Column, alias.Position });
        builder.HasData(CreateDefaultAliases());
    }

    private static IEnumerable<object> CreateDefaultAliases()
    {
        var defaults = new (SupplierDocumentOcrColumn Column, string[] Aliases)[]
        {
            (SupplierDocumentOcrColumn.SupplierCode,
            [
                "codicearticolo", "codart", "codicefornitore", "codiceprodotto", "codice",
                "suppliercode", "supplieritemcode", "itemcode", "itemnumber", "itemno",
                "productcode", "productnumber", "articlecode", "articleno", "numeroarticolo",
                "nrarticolo", "nart", "code", "sku", "reference", "ref", "partnumber"
            ]),
            (SupplierDocumentOcrColumn.Description,
            [
                "descrizione", "description", "itemdescription", "productdescription",
                "descrizionearticolo", "descrarticolo", "descrizionemerce", "denominazione",
                "nomearticolo", "dettaglio", "articolo", "productname", "itemname"
            ]),
            (SupplierDocumentOcrColumn.Quantity,
            [
                "quantita", "qta", "qtaord", "quantitaordinata", "qty", "qtyordered",
                "quantity", "quantities", "quantityordered", "npezzi", "pezzi", "pieces", "pcs", "pz"
            ])
        };
        var timestamp = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        var aliases = new List<object>();
        var id = 1;

        foreach (var (column, values) in defaults)
        {
            for (var position = 0; position < values.Length; position++)
            {
                var alias = values[position];
                aliases.Add(new
                {
                    Id = Guid.Parse($"d5510000-0000-0000-0000-{id++:D12}"),
                    Column = column,
                    Alias = alias,
                    NormalizedAlias = SupplierDocumentOcrHeaderAlias.NormalizeAlias(alias),
                    Position = position,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp
                });
            }
        }

        return aliases;
    }
}
