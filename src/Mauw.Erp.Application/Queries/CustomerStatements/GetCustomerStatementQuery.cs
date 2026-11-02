using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using MediatR;

namespace Mauw.Erp.Application.Queries.CustomerStatements;

public sealed record GetCustomerStatementQuery(Guid CustomerId) : IRequest<OperationResult<CustomerStatementDto>>;
