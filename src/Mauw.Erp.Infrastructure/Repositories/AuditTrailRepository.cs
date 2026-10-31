using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class AuditTrailRepository(MauwDbContext dbContext) : IAuditTrailRepository
{
    public async Task AddAsync(DocumentAuditEntry entry) =>
        await dbContext.DocumentAuditEntries.AddAsync(entry);

    public async Task<IReadOnlyList<DocumentAuditEntry>> GetByDocumentAsync(string documentType, Guid documentId) =>
        await dbContext.DocumentAuditEntries
            .AsNoTracking()
            .Where(entry => entry.DocumentType == documentType && entry.DocumentId == documentId)
            .OrderBy(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.Id)
            .ToListAsync();
}
