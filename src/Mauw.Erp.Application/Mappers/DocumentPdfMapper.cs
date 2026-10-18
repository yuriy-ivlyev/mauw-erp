using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Domain.Repositories;

namespace Mauw.Erp.Application.Mappers;

internal static class DocumentPdfMapper
{
    public static async Task<OperationResult<InvoicePdfAgentInfo?>> GetAgentAsync(
        IUnitOfWork unitOfWork, Guid customerId)
    {
        var customer = await unitOfWork.Contacts.GetByIdWithDetailsAsync(customerId);
        if (customer is null)
            return OperationResult<InvoicePdfAgentInfo?>.Fail("Cliente del documento non disponibile.");
        if (customer?.CustomerProfile?.DefaultAgentId is not { } agentId)
            return OperationResult<InvoicePdfAgentInfo?>.Ok(null);
        var agent = await unitOfWork.Contacts.GetByIdWithDetailsAsync(agentId);
        return agent?.AgentProfile is null || string.IsNullOrWhiteSpace(agent.BusinessName)
            ? OperationResult<InvoicePdfAgentInfo?>.Fail("L'agente associato al cliente non e disponibile o non ha un profilo agente.")
            : OperationResult<InvoicePdfAgentInfo?>.Ok(new(agent.BusinessName, null));
    }
}
