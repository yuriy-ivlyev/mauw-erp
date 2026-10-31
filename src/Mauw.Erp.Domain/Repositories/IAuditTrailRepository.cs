using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>Append-only store for document audit trail entries.</summary>
public interface IAuditTrailRepository
{
    Task AddAsync(DocumentAuditEntry entry);
    Task<IReadOnlyList<DocumentAuditEntry>> GetByDocumentAsync(string documentType, Guid documentId);
}
