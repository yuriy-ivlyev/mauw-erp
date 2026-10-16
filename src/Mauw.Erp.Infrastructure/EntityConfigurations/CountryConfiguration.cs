using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries");
        builder.HasKey(c => c.IsoCode);
        builder.Property(c => c.IsoCode).HasMaxLength(2).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.DisplayOrder);

        builder.HasData(Seed());
    }

    private static Country[] Seed() =>
    [
        // ── Italia first ───────────────────────────────────────
        new() { IsoCode = "IT", Name = "Italia",              IsEuMember = true,  DisplayOrder = 1  },
        // ── UE – più comuni ────────────────────────────────────
        new() { IsoCode = "DE", Name = "Germania",            IsEuMember = true,  DisplayOrder = 10 },
        new() { IsoCode = "FR", Name = "Francia",             IsEuMember = true,  DisplayOrder = 11 },
        new() { IsoCode = "ES", Name = "Spagna",              IsEuMember = true,  DisplayOrder = 12 },
        new() { IsoCode = "NL", Name = "Paesi Bassi",         IsEuMember = true,  DisplayOrder = 13 },
        new() { IsoCode = "BE", Name = "Belgio",              IsEuMember = true,  DisplayOrder = 14 },
        new() { IsoCode = "PT", Name = "Portogallo",          IsEuMember = true,  DisplayOrder = 15 },
        new() { IsoCode = "AT", Name = "Austria",             IsEuMember = true,  DisplayOrder = 16 },
        new() { IsoCode = "GR", Name = "Grecia",              IsEuMember = true,  DisplayOrder = 17 },
        new() { IsoCode = "PL", Name = "Polonia",             IsEuMember = true,  DisplayOrder = 18 },
        new() { IsoCode = "SE", Name = "Svezia",              IsEuMember = true,  DisplayOrder = 20 },
        new() { IsoCode = "DK", Name = "Danimarca",           IsEuMember = true,  DisplayOrder = 21 },
        new() { IsoCode = "FI", Name = "Finlandia",           IsEuMember = true,  DisplayOrder = 22 },
        new() { IsoCode = "IE", Name = "Irlanda",             IsEuMember = true,  DisplayOrder = 23 },
        new() { IsoCode = "CZ", Name = "Repubblica Ceca",     IsEuMember = true,  DisplayOrder = 24 },
        new() { IsoCode = "HU", Name = "Ungheria",            IsEuMember = true,  DisplayOrder = 25 },
        new() { IsoCode = "RO", Name = "Romania",             IsEuMember = true,  DisplayOrder = 26 },
        new() { IsoCode = "BG", Name = "Bulgaria",            IsEuMember = true,  DisplayOrder = 27 },
        new() { IsoCode = "HR", Name = "Croazia",             IsEuMember = true,  DisplayOrder = 28 },
        new() { IsoCode = "SK", Name = "Slovacchia",          IsEuMember = true,  DisplayOrder = 29 },
        new() { IsoCode = "SI", Name = "Slovenia",            IsEuMember = true,  DisplayOrder = 30 },
        new() { IsoCode = "LT", Name = "Lituania",            IsEuMember = true,  DisplayOrder = 31 },
        new() { IsoCode = "LV", Name = "Lettonia",            IsEuMember = true,  DisplayOrder = 32 },
        new() { IsoCode = "EE", Name = "Estonia",             IsEuMember = true,  DisplayOrder = 33 },
        new() { IsoCode = "CY", Name = "Cipro",               IsEuMember = true,  DisplayOrder = 34 },
        new() { IsoCode = "MT", Name = "Malta",               IsEuMember = true,  DisplayOrder = 35 },
        new() { IsoCode = "LU", Name = "Lussemburgo",         IsEuMember = true,  DisplayOrder = 36 },
        // ── Europa extra-UE ────────────────────────────────────
        new() { IsoCode = "CH", Name = "Svizzera",            IsEuMember = false, DisplayOrder = 50 },
        new() { IsoCode = "GB", Name = "Regno Unito",         IsEuMember = false, DisplayOrder = 51 },
        new() { IsoCode = "NO", Name = "Norvegia",            IsEuMember = false, DisplayOrder = 52 },
        new() { IsoCode = "IS", Name = "Islanda",             IsEuMember = false, DisplayOrder = 53 },
        new() { IsoCode = "AL", Name = "Albania",             IsEuMember = false, DisplayOrder = 54 },
        new() { IsoCode = "BA", Name = "Bosnia Erzegovina",   IsEuMember = false, DisplayOrder = 55 },
        new() { IsoCode = "RS", Name = "Serbia",              IsEuMember = false, DisplayOrder = 56 },
        new() { IsoCode = "ME", Name = "Montenegro",          IsEuMember = false, DisplayOrder = 57 },
        new() { IsoCode = "MK", Name = "Macedonia del Nord",  IsEuMember = false, DisplayOrder = 58 },
        new() { IsoCode = "TR", Name = "Turchia",             IsEuMember = false, DisplayOrder = 59 },
        new() { IsoCode = "UA", Name = "Ucraina",             IsEuMember = false, DisplayOrder = 60 },
        new() { IsoCode = "RU", Name = "Russia",              IsEuMember = false, DisplayOrder = 61 },
        // ── Resto del mondo ────────────────────────────────────
        new() { IsoCode = "US", Name = "Stati Uniti",         IsEuMember = false, DisplayOrder = 80 },
        new() { IsoCode = "CA", Name = "Canada",              IsEuMember = false, DisplayOrder = 81 },
        new() { IsoCode = "MX", Name = "Messico",             IsEuMember = false, DisplayOrder = 82 },
        new() { IsoCode = "BR", Name = "Brasile",             IsEuMember = false, DisplayOrder = 83 },
        new() { IsoCode = "AR", Name = "Argentina",           IsEuMember = false, DisplayOrder = 84 },
        new() { IsoCode = "CN", Name = "Cina",                IsEuMember = false, DisplayOrder = 90 },
        new() { IsoCode = "JP", Name = "Giappone",            IsEuMember = false, DisplayOrder = 91 },
        new() { IsoCode = "IN", Name = "India",               IsEuMember = false, DisplayOrder = 92 },
        new() { IsoCode = "KR", Name = "Corea del Sud",       IsEuMember = false, DisplayOrder = 93 },
        new() { IsoCode = "AU", Name = "Australia",           IsEuMember = false, DisplayOrder = 95 },
        new() { IsoCode = "ZA", Name = "Sudafrica",           IsEuMember = false, DisplayOrder = 96 },
        new() { IsoCode = "AE", Name = "Emirati Arabi Uniti", IsEuMember = false, DisplayOrder = 97 },
        new() { IsoCode = "SA", Name = "Arabia Saudita",      IsEuMember = false, DisplayOrder = 98 },
    ];
}
