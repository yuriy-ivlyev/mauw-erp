using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Contact (Party) endpoints with Party-Role support, paged filtering and customer block management.
/// </summary>
public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/contacts")
            .WithName("Contacts");

        group.MapGet("/validate-vat", ValidateVat)
            .WithName("ValidateVat")
            .WithSummary("Validazione formale P.IVA / Tax ID + verifica VIES (solo paesi UE)");

        group.MapGet("/", ListContacts)
            .WithName("ListContacts")
            .WithSummary("Paginated contact list with role/status filters");

        group.MapPut("/agents/{agentId:guid}/customers/{customerId:guid}", AssignCustomerToAgent)
            .WithName("AssignCustomerToAgent")
            .WithSummary("Assign a customer to an agent");

        group.MapDelete("/agents/{agentId:guid}/customers/{customerId:guid}", RemoveCustomerFromAgent)
            .WithName("RemoveCustomerFromAgent")
            .WithSummary("Remove a customer from an agent");

        group.MapGet("/{id:guid}", GetContact)
            .WithName("GetContact")
            .WithSummary("Get full contact detail by ID");

        group.MapPost("/", CreateContact)
            .WithName("CreateContact")
            .WithSummary("Create a new contact");

        group.MapPut("/{id:guid}", UpdateContact)
            .WithName("UpdateContact")
            .WithSummary("Full update of an existing contact");

        group.MapDelete("/{id:guid}", DeleteContact)
            .WithName("DeleteContact")
            .WithSummary("Soft-delete a contact");

        group.MapPatch("/{id:guid}/customer-block", ToggleCustomerBlock)
            .WithName("ToggleCustomerBlock")
            .WithSummary("Activate or remove commercial block on a customer");

        // ── Nested address endpoints ──────────────────────────────────────────
        group.MapGet("/{id:guid}/addresses", GetAddresses)
            .WithName("GetContactAddresses")
            .WithSummary("Get all addresses for a contact");

        group.MapPost("/{id:guid}/addresses", AddAddress)
            .WithName("AddContactAddress")
            .WithSummary("Add an address to a contact");

        group.MapPut("/{id:guid}/addresses/{addressId:guid}", UpdateAddress)
            .WithName("UpdateContactAddress")
            .WithSummary("Update an address of a contact");

        group.MapDelete("/{id:guid}/addresses/{addressId:guid}", DeleteAddress)
            .WithName("DeleteContactAddress")
            .WithSummary("Delete an address (LegalHeadquarters cannot be deleted)");

        group.MapDelete("/{id:guid}/persons/{personId:guid}", DeletePerson)
            .WithName("DeleteContactPerson")
            .WithSummary("Delete a contact person (referente)");
    }

    private static async Task<Ok<VatValidationResultDto>> ValidateVat(
        string countryCode,
        string vatNumber,
        IVatValidationService vatService)
    {
        var result = await vatService.ValidateAsync(countryCode, vatNumber);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<PagedResult<ContactSummaryDto>>> ListContacts(
        IContactService contactService,
        ILogger<Program> logger,
        string? search = null,
        bool? isCustomer = null,
        bool? isSupplier = null,
        bool? isAgent = null,
        Guid? assignedAgentId = null,
        bool? hasAssignedAgent = null,
        string? status = null,
        string? sortBy = null,
        bool sortDescending = false,
        int page = 1,
        int pageSize = 20)
    {
        var request = new ContactFilterRequest
        {
            Search = search,
            IsCustomer = isCustomer,
            IsSupplier = isSupplier,
            IsAgent = isAgent,
            AssignedAgentId = assignedAgentId,
            HasAssignedAgent = hasAssignedAgent,
            Status = status,
            SortBy = sortBy,
            SortDescending = sortDescending,
            Page = page,
            PageSize = pageSize
        };
        request.Page = Math.Max(request.Page, 1);
        request.PageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);
        var result = await contactService.GetPagedAsync(request);
        logger.LogInformation("Listed contacts – total: {Total}", result.Data?.TotalCount);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> AssignCustomerToAgent(
        Guid agentId,
        Guid customerId,
        IContactService contactService)
    {
        var result = await contactService.AssignCustomerToAgentAsync(agentId, customerId);
        if (result.Success)
            return TypedResults.NoContent();
        if (result.Error is "Agente non trovato o non abilitato." or "Cliente non trovato o non abilitato.")
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> RemoveCustomerFromAgent(
        Guid agentId,
        Guid customerId,
        IContactService contactService)
    {
        var result = await contactService.RemoveCustomerFromAgentAsync(agentId, customerId);
        if (result.Success)
            return TypedResults.NoContent();
        if (result.Error == "Cliente non trovato o non abilitato.")
            return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<Ok<ContactDetailDto>, NotFound>> GetContact(
        Guid id,
        IContactService contactService)
    {
        var result = await contactService.GetByIdAsync(id);
        return result.Success
            ? TypedResults.Ok(result.Data!)
            : TypedResults.NotFound();
    }

    private static async Task<Results<Created<ContactDetailDto>, BadRequest<string>>> CreateContact(
        CreateContactRequest request,
        IContactService contactService,
        ILogger<Program> logger)
    {
        var result = await contactService.CreateAsync(request);

        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        logger.LogInformation("Contact created: {Id}", result.Data!.Id);
        return TypedResults.Created($"/api/v1/contacts/{result.Data.Id}", result.Data);
    }

    private static async Task<Results<Ok<ContactDetailDto>, BadRequest<string>, NotFound>> UpdateContact(
        Guid id,
        UpdateContactRequest request,
        IContactService contactService)
    {
        var result = await contactService.UpdateAsync(id, request);

        if (!result.Success)
        {
            if (result.Error == "Contact not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> DeleteContact(
        Guid id,
        IContactService contactService)
    {
        var result = await contactService.DeleteAsync(id);
        if (result.Success) return TypedResults.NoContent();
        if (result.Error == "Contact not found") return TypedResults.NotFound();
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> ToggleCustomerBlock(
        Guid id,
        ToggleBlockRequest request,
        IContactService contactService)
    {
        var result = await contactService.ToggleBlockCustomerAsync(id, request.IsBlocked, request.Reason);

        if (!result.Success)
        {
            if (result.Error == "Contact not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.NoContent();
    }

    // ── Address handlers ──────────────────────────────────────────────────────

    private static async Task<Results<Ok<List<ContactAddressDto>>, NotFound>> GetAddresses(
        Guid id, IContactAddressService addressService)
    {
        var result = await addressService.GetAddressesAsync(id);
        return result.Success ? TypedResults.Ok(result.Data!) : TypedResults.NotFound();
    }

    private static async Task<Results<Created<ContactAddressDto>, BadRequest<string>, NotFound>> AddAddress(
        Guid id, ContactAddressRequest request, IContactAddressService addressService)
    {
        var result = await addressService.AddAddressAsync(id, request);
        if (!result.Success)
        {
            if (result.Error == "Contact not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }
        return TypedResults.Created($"/api/v1/contacts/{id}/addresses/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<Ok<ContactAddressDto>, BadRequest<string>, NotFound>> UpdateAddress(
        Guid id, Guid addressId, ContactAddressRequest request, IContactAddressService addressService)
    {
        var result = await addressService.UpdateAddressAsync(id, addressId, request);
        if (!result.Success)
        {
            if (result.Error is "Contact not found" or "Address not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> DeleteAddress(
        Guid id, Guid addressId, IContactAddressService addressService)
    {
        var result = await addressService.DeleteAddressAsync(id, addressId);
        if (!result.Success)
        {
            if (result.Error is "Contact not found" or "Indirizzo non trovato.") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, BadRequest<string>, NotFound>> DeletePerson(
        Guid id, Guid personId, IContactAddressService addressService)
    {
        var result = await addressService.DeletePersonAsync(id, personId);
        if (!result.Success)
        {
            if (result.Error is "Contact not found" or "Person not found") return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }
        return TypedResults.NoContent();
    }
}

/// <summary>Request body for PATCH customer-block endpoint.</summary>
public record ToggleBlockRequest(bool IsBlocked, string? Reason);
