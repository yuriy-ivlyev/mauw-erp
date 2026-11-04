using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this WebApplication app)
    {
        var employees = app.MapGroup("/api/v1/employees")
            .WithTags("Employees")
            .RequireAuthorization(AppPolicies.AdminOnly);

        employees.MapGet("/", List).WithSummary("Elenca i dipendenti");
        employees.MapGet("/{id:guid}", Get).WithSummary("Legge un dipendente");
        employees.MapPost("/", Create).WithSummary("Registra un dipendente");
        employees.MapPut("/{id:guid}", Update).WithSummary("Aggiorna i dati di un dipendente");
        employees.MapPost("/{id:guid}/deactivate", Deactivate).WithSummary("Disattiva un dipendente");
        employees.MapGet("/{id:guid}/payroll-costs", ListPayrollCosts)
            .WithSummary("Elenca i costi mensili e i cedolini del dipendente");
        employees.MapPost("/{id:guid}/payroll-costs", CreatePayrollCost).DisableAntiforgery()
            .WithSummary("Registra il costo mensile e il cedolino del dipendente");
        employees.MapGet("/{id:guid}/documents", ListDocuments)
            .WithSummary("Elenca i documenti personali e gli attestati del dipendente");
        employees.MapPost("/{id:guid}/documents", CreateDocument).DisableAntiforgery()
            .WithSummary("Archivia un documento personale o un attestato");
        employees.MapPut("/{id:guid}/documents/{documentId:guid}", UpdateDocument)
            .WithSummary("Aggiorna i metadati di un documento dipendente");
        employees.MapGet("/{id:guid}/documents/{documentId:guid}/file", DownloadDocument)
            .WithSummary("Scarica un allegato personale del dipendente");
        employees.MapGet("/{id:guid}/time-entries", ListTimeEntries)
            .WithSummary("Elenca i cartellini giornalieri del dipendente");
        employees.MapPost("/{id:guid}/time-entries", CreateTimeEntry)
            .WithSummary("Registra un cartellino giornaliero");
        employees.MapPut("/{id:guid}/time-entries/{entryId:guid}", UpdateTimeEntry)
            .WithSummary("Aggiorna un cartellino giornaliero");
    }

    private static async Task<IResult> List(
        IEmployeeService service,
        int page = 1,
        int pageSize = 100,
        string? search = null,
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await service.ListAsync(page, pageSize, search, activeOnly, cancellationToken));

    private static async Task<IResult> Get(
        Guid id,
        IEmployeeService service,
        CancellationToken cancellationToken)
    {
        var employee = await service.GetAsync(id, cancellationToken);
        return employee is null ? TypedResults.NotFound() : TypedResults.Ok(employee);
    }

    private static async Task<IResult> Create(
        SaveEmployeeRequest request,
        ICurrentUserService currentUser,
        IEmployeeService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.CreateAsync(request, currentUser.UserId, cancellationToken), created: true);

    private static async Task<IResult> Update(
        Guid id,
        SaveEmployeeRequest request,
        ICurrentUserService currentUser,
        IEmployeeService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.UpdateAsync(id, request, currentUser.UserId, cancellationToken));

    private static async Task<IResult> Deactivate(
        Guid id,
        DeactivateEmployeeRequest request,
        ICurrentUserService currentUser,
        IEmployeeService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.DeactivateAsync(id, request, currentUser.UserId, cancellationToken));

    private static async Task<IResult> ListPayrollCosts(
        Guid id,
        IEmployeeService employees,
        IPayableDocumentService payables,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (await employees.GetAsync(id, cancellationToken) is null)
            return TypedResults.NotFound();
        return TypedResults.Ok(await payables.ListEmployeePayrollCostsAsync(
            id, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> CreatePayrollCost(
        Guid id,
        [FromForm] DateOnly period,
        [FromForm] decimal monthlyCompanyCost,
        [FromForm] string? notes,
        [FromForm] IFormFile? file,
        ICurrentUserService currentUser,
        IPayableDocumentService service,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return TypedResults.BadRequest("Il cedolino è obbligatorio.");
        await using var stream = file.OpenReadStream();
        return ToPayrollResult(await service.CreateEmployeePayrollCostAsync(
            id,
            new CreateEmployeePayrollCostRequest(period, monthlyCompanyCost, notes),
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            currentUser.UserId,
            cancellationToken), created: true);
    }

    private static IResult ToResult(OperationResult<EmployeeDto> result, bool created = false)
    {
        if (result.Success)
            return created
                ? TypedResults.Created($"/api/v1/employees/{result.Data!.Id}", result.Data)
                : TypedResults.Ok(result.Data);
        var error = result.Error ?? "Operazione sul dipendente non riuscita.";
        var status = error == "Dipendente non trovato." ? 404
            : error.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409
            : error.Contains("già assegnata", StringComparison.OrdinalIgnoreCase) ? 409
            : 400;
        return TypedResults.Problem(statusCode: status, detail: error);
    }

    private static IResult ToPayrollResult(
        OperationResult<PayableDocumentDto> result,
        bool created = false)
    {
        if (result.Success)
            return created
                ? TypedResults.Created($"/api/v1/payables/{result.Data!.Id}", result.Data)
                : TypedResults.Ok(result.Data);
        var error = result.Error ?? "Operazione sul costo mensile non riuscita.";
        var status = error == "Dipendente non trovato." ? 404
            : error.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409
            : error.Contains("già registrato", StringComparison.OrdinalIgnoreCase) ? 409
            : 400;
        return TypedResults.Problem(statusCode: status, detail: error);
    }

    private static async Task<IResult> ListDocuments(
        Guid id,
        IEmployeeService employees,
        IEmployeeDocumentService documents,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (await employees.GetAsync(id, cancellationToken) is null)
            return TypedResults.NotFound();
        return TypedResults.Ok(await documents.ListAsync(id, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> CreateDocument(
        Guid id,
        [FromForm] string kind,
        [FromForm] string title,
        [FromForm] string? documentNumber,
        [FromForm] string? issuerOrProvider,
        [FromForm] DateOnly? issuedOn,
        [FromForm] DateOnly? completedOn,
        [FromForm] DateOnly? expiresOn,
        [FromForm] string? notes,
        [FromForm] IFormFile? file,
        ICurrentUserService currentUser,
        IEmployeeDocumentService service,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return TypedResults.BadRequest("L'allegato è obbligatorio.");
        await using var stream = file.OpenReadStream();
        return ToDocumentResult(await service.CreateAsync(
            id,
            new SaveEmployeeDocumentRequest(
                kind, title, documentNumber, issuerOrProvider,
                issuedOn, completedOn, expiresOn, notes),
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            currentUser.UserId,
            cancellationToken), created: true);
    }

    private static async Task<IResult> UpdateDocument(
        Guid id,
        Guid documentId,
        SaveEmployeeDocumentRequest request,
        ICurrentUserService currentUser,
        IEmployeeDocumentService service,
        CancellationToken cancellationToken) =>
        ToDocumentResult(await service.UpdateAsync(
            id, documentId, request, currentUser.UserId, cancellationToken));

    private static async Task<IResult> DownloadDocument(
        Guid id,
        Guid documentId,
        IEmployeeDocumentService service,
        CancellationToken cancellationToken)
    {
        var file = await service.DownloadAsync(id, documentId, cancellationToken);
        return file is null
            ? TypedResults.NotFound()
            : TypedResults.File(file.Value.Stream, file.Value.ContentType, file.Value.FileName);
    }

    private static async Task<IResult> ListTimeEntries(
        Guid id,
        IEmployeeService employees,
        IEmployeeTimeEntryService service,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        int pageSize = 31,
        CancellationToken cancellationToken = default)
    {
        if (!from.HasValue || !to.HasValue || from > to
            || to.Value.DayNumber - from.Value.DayNumber > 366)
            return TypedResults.BadRequest("Indicare un intervallo valido di massimo 367 giorni.");
        if (await employees.GetAsync(id, cancellationToken) is null)
            return TypedResults.NotFound();
        return TypedResults.Ok(await service.ListAsync(
            id, from.Value, to.Value, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> CreateTimeEntry(
        Guid id,
        SaveEmployeeTimeEntryRequest request,
        ICurrentUserService currentUser,
        IEmployeeTimeEntryService service,
        CancellationToken cancellationToken) =>
        ToTimeEntryResult(await service.CreateAsync(
            id, request, currentUser.UserId, cancellationToken), created: true);

    private static async Task<IResult> UpdateTimeEntry(
        Guid id,
        Guid entryId,
        SaveEmployeeTimeEntryRequest request,
        ICurrentUserService currentUser,
        IEmployeeTimeEntryService service,
        CancellationToken cancellationToken) =>
        ToTimeEntryResult(await service.UpdateAsync(
            id, entryId, request, currentUser.UserId, cancellationToken));

    private static IResult ToDocumentResult(
        OperationResult<EmployeeDocumentDto> result,
        bool created = false)
    {
        if (result.Success)
            return created
                ? TypedResults.Created($"/api/v1/employees/{result.Data!.EmployeeId}/documents/{result.Data.Id}", result.Data)
                : TypedResults.Ok(result.Data);
        var error = result.Error ?? "Operazione sul documento dipendente non riuscita.";
        var status = error is "Dipendente non trovato." or "Documento dipendente non trovato." ? 404
            : error.StartsWith("Conflitto:", StringComparison.Ordinal) ? 409
            : 400;
        return TypedResults.Problem(statusCode: status, detail: error);
    }

    private static IResult ToTimeEntryResult(
        OperationResult<EmployeeTimeEntryDto> result,
        bool created = false)
    {
        if (result.Success)
            return created
                ? TypedResults.Created(
                    $"/api/v1/employees/{result.Data!.EmployeeId}/time-entries/{result.Data.Id}",
                    result.Data)
                : TypedResults.Ok(result.Data);
        var error = result.Error ?? "Operazione sul cartellino non riuscita.";
        var status = error is "Dipendente non trovato." or "Cartellino non trovato." ? 404
            : error.StartsWith("Conflitto:", StringComparison.Ordinal)
                || error.Contains("già un cartellino", StringComparison.OrdinalIgnoreCase) ? 409
            : 400;
        return TypedResults.Problem(statusCode: status, detail: error);
    }
}
