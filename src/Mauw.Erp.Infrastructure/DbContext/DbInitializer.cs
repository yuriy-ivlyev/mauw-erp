using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
    }
}
