using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class PriceFormulaConfiguration : IEntityTypeConfiguration<PriceFormula>
{
    public const string SingletonProperty = "IsSingleton";

    public void Configure(EntityTypeBuilder<PriceFormula> builder)
    {
        builder.ToTable("price_formulas", table =>
            table.HasCheckConstraint("ck_price_formulas_is_singleton", "is_singleton"));
        builder.HasKey(formula => formula.Id);
        builder.Property(formula => formula.Id).ValueGeneratedNever();
        builder.Property(formula => formula.RoundingStep).HasPrecision(5, 2).IsRequired();
        builder.Property(formula => formula.UpdatedByUserId).HasMaxLength(200);
        // Colonna costante + indice univoco + CHECK: a livello DB puo esistere una sola formula.
        // Il sentinel false fa usare il default DB (true) quando EF non imposta il valore.
        builder.Property<bool>(SingletonProperty).HasColumnName("is_singleton")
            .HasDefaultValue(true).HasSentinel(false);
        builder.HasIndex(SingletonProperty).IsUnique().HasDatabaseName("ux_price_formulas_is_singleton");
        builder.HasMany(formula => formula.Steps).WithOne().HasForeignKey(step => step.FormulaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(formula => formula.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PriceFormulaStepConfiguration : IEntityTypeConfiguration<PriceFormulaStep>
{
    public void Configure(EntityTypeBuilder<PriceFormulaStep> builder)
    {
        builder.ToTable("price_formula_steps", table =>
        {
            table.HasCheckConstraint("ck_price_formula_steps_position", "\"position\" >= 1");
            table.HasCheckConstraint("ck_price_formula_steps_percentage", "percentage >= 0 AND percentage <= 9999");
        });
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Id).ValueGeneratedNever();
        builder.Property(step => step.FormulaId).HasColumnName("price_formula_id");
        builder.Property(step => step.Label).HasMaxLength(100).IsRequired();
        builder.Property(step => step.Percentage).HasPrecision(6, 2);
        builder.HasIndex(step => new { step.FormulaId, step.Position })
            .IsUnique().HasDatabaseName("ux_price_formula_steps_price_formula_id_position");
    }
}
