using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class SupplierDocumentOcrAliasService(MauwDbContext db)
    : ISupplierDocumentOcrAliasService
{
    public async Task<IReadOnlyList<SupplierDocumentOcrHeaderAliasDto>> ListAsync(
        CancellationToken cancellationToken)
    {
        var aliases = await db.SupplierDocumentOcrHeaderAliases
            .AsNoTracking()
            .OrderBy(alias => alias.Column)
            .ThenBy(alias => alias.Position)
            .ThenBy(alias => alias.Alias)
            .ToListAsync(cancellationToken);
        return aliases.Select(ToDto).ToArray();
    }

    public async Task<OperationResult<SupplierDocumentOcrHeaderAliasDto>> AddAsync(
        CreateSupplierDocumentOcrHeaderAliasRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || !Enum.TryParse<SupplierDocumentOcrColumn>(request.Column, true, out var column) ||
            !Enum.IsDefined(column))
            return OperationResult<SupplierDocumentOcrHeaderAliasDto>.Fail("Campo OCR non valido.");

        var alias = request.Alias?.Trim();
        if (string.IsNullOrWhiteSpace(alias) || alias.Length > 100)
            return OperationResult<SupplierDocumentOcrHeaderAliasDto>.Fail(
                "L'alias è obbligatorio e non può superare 100 caratteri.");

        var normalizedAlias = SupplierDocumentOcrHeaderAlias.NormalizeAlias(alias);
        if (normalizedAlias.Length == 0)
            return OperationResult<SupplierDocumentOcrHeaderAliasDto>.Fail(
                "L'alias deve contenere almeno una lettera o un numero.");

        if (await db.SupplierDocumentOcrHeaderAliases.AnyAsync(
                existing => existing.NormalizedAlias == normalizedAlias,
                cancellationToken))
            return OperationResult<SupplierDocumentOcrHeaderAliasDto>.Fail(
                "L'alias è già configurato per un altro campo.");

        var lastPosition = await db.SupplierDocumentOcrHeaderAliases
            .Where(existing => existing.Column == column)
            .Select(existing => (int?)existing.Position)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new SupplierDocumentOcrHeaderAlias(column, alias, lastPosition + 1);
        db.SupplierDocumentOcrHeaderAliases.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(entity).State = EntityState.Detached;
            return OperationResult<SupplierDocumentOcrHeaderAliasDto>.Fail(
                "L'alias è già configurato per un altro campo.");
        }

        return OperationResult<SupplierDocumentOcrHeaderAliasDto>.Ok(ToDto(entity));
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var alias = await db.SupplierDocumentOcrHeaderAliases
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (alias is null)
            return OperationResult<bool>.Fail("Alias OCR non trovato.");

        if (await db.SupplierDocumentOcrHeaderAliases
                .CountAsync(candidate => candidate.Column == alias.Column, cancellationToken) <= 1)
            return OperationResult<bool>.Fail(
                "Ogni campo OCR deve mantenere almeno un alias configurato.");

        db.SupplierDocumentOcrHeaderAliases.Remove(alias);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    private static SupplierDocumentOcrHeaderAliasDto ToDto(SupplierDocumentOcrHeaderAlias alias) =>
        new(alias.Id, alias.Column.ToString(), alias.Alias, alias.Position);
}
