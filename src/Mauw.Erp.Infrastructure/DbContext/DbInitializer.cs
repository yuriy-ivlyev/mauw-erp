using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Infrastructure.Branding;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Database seeding utility. Populates initial data after migrations are applied.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(MauwDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed Colors if empty
        if (!await context.Colors.AnyAsync())
        {
            List<Color> colors =
            [
                new("BLK", "Nero", "#1A1A1A"),
                new("WHT", "Bianco", "#FFFFFF"),
                new("GRY", "Grigio", "#808080"),
                new("YLW", "Giallo", "#FFFF00"),
                new("VLT", "Viola", "#800080"),
                new("RED", "Rosso", "#FF0000")
            ];

            await context.Colors.AddRangeAsync(colors);
            await context.SaveChangesAsync();
        }
        // Seed Sizes if empty
        if (!await context.Sizes.AnyAsync())
        {
            List<Size> sizes =
            [
                new("NA___", "N/A"),
                new("XXS__", "XXS"),
                new("XS___", "XS"),
                new("SM___", "SM"),
                new("MD___", "MD"),
                new("LG___", "LG"),
                new("XL___", "XL"),
                new("XXL__", "XXL"),
                new("XXXL_", "XXXL")
            ];

            await context.Sizes.AddRangeAsync(sizes);
            await context.SaveChangesAsync();
        }

        // Seed azienda principale (MAUW S.R.L.) – solo se non già presente
        if (!await context.Contacts.IgnoreQueryFilters().AnyAsync(c => c.IsInternalCompany))
        {
            var mauw = new Contact("MAUW S.R.L.", vatNumber: "04414081200", taxCode: "04414081200")
            {
                IsInternalCompany  = true,
                Status             = ContactStatus.Active,
                PecEmail           = "mauw@pec.it",
                SdiCode            = null,            // da configurare dopo inizio attività
                CountryCode        = "IT",
                FiscalRegime       = "RF01",          // Regime Ordinario
                ReaOffice          = "BO",
                ReaNumber          = "591450",        // BO-591450
                IsSoleShareholder  = false,
                IsInLiquidation    = false
            };

            // Sede Legale: Via Antonio Gramsci 30, 40054 Budrio (BO)
            mauw.SetLegalHeadquarters(
                street:      "Via Antonio Gramsci 30",
                zipCode:     "40054",
                city:        "Budrio",
                province:    "BO",
                countryCode: "IT"
            );

            context.Contacts.Add(mauw);
            await context.SaveChangesAsync();
        }

        await SeedCompanyLogosAsync(context);

        if (!await context.Warehouses.IgnoreQueryFilters().AnyAsync())
        {
            var internalCompany = await context.Contacts
                .IgnoreQueryFilters()
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c => c.IsInternalCompany && !c.IsDeleted);

            if (internalCompany?.Addresses.FirstOrDefault() is { } address)
            {
                var warehouses = new List<Warehouse>
                {
                    new("WH-CENTRALE", "Magazzino Centrale", WarehouseType.Standard, address.Id, isSellable: true, isDefault: true),
                    new("WH-FALLATI", "Magazzino Capi Fallati", WarehouseType.Defective, address.Id, isSellable: false, isDefault: false)
                };

                await context.Warehouses.AddRangeAsync(warehouses);
                await context.SaveChangesAsync();
            }

        }
    }

    public static async Task SeedCompanyLogosAsync(MauwDbContext context, CancellationToken ct = default)
    {
        var company = await context.Contacts.SingleAsync(contact => contact.IsInternalCompany, ct);
        company.HorizontalLogoPng ??= DefaultCompanyLogos.ReadPng("horizontal");
        company.CompactLogoPng ??= DefaultCompanyLogos.ReadPng("compact");
        await context.SaveChangesAsync(ct);
    }
}
