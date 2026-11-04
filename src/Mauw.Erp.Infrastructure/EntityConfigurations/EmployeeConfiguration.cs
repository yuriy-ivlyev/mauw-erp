using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.EmployeeNumber).HasMaxLength(50).IsRequired();
        builder.Property(employee => employee.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.LastName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.Qualification).HasMaxLength(150);
        builder.Property(employee => employee.ContractLevel).HasMaxLength(100);
        builder.Property(employee => employee.ContractType).HasMaxLength(100);
        builder.Property(employee => employee.AnnualGrossSalary).HasPrecision(18, 2);
        builder.Property(employee => employee.AnnualCompanyCost).HasPrecision(18, 2);
        builder.Property(employee => employee.HireDate);
        builder.Property(employee => employee.EndDate);
        builder.Property(employee => employee.StreetAddress).HasMaxLength(250);
        builder.Property(employee => employee.PostalCode).HasMaxLength(20);
        builder.Property(employee => employee.City).HasMaxLength(100);
        builder.Property(employee => employee.Province).HasMaxLength(100);
        builder.Property(employee => employee.CountryCode).HasMaxLength(2);
        builder.Property(employee => employee.Email).HasMaxLength(254);
        builder.Property(employee => employee.Phone).HasMaxLength(50);
        builder.Property(employee => employee.MobilePhone).HasMaxLength(50);
        builder.Property(employee => employee.IsActive).IsRequired();
        builder.Property(employee => employee.Notes).HasMaxLength(1000);
        builder.Property(employee => employee.Version).IsConcurrencyToken();
        builder.Property(employee => employee.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(employee => employee.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(employee => employee.EmployeeNumber).IsUnique();
        builder.HasIndex(employee => new { employee.IsActive, employee.EmployeeNumber });
        builder.HasMany(employee => employee.Documents)
            .WithOne(document => document.Employee)
            .HasForeignKey(document => document.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(employee => employee.TimeEntries)
            .WithOne(entry => entry.Employee)
            .HasForeignKey(entry => entry.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
