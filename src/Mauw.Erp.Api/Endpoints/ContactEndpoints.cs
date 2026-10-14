using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Contact management endpoints for customers, suppliers, and agents.
/// Thin layer delegating to IContactService.
/// </summary>
public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/contacts")
            .WithName("Contacts");

        group.MapPost("/", CreateContact)
            .WithName("CreateContact")
            .WithSummary("Create a new contact");

        group.MapGet("/{id}", GetContact)
            .WithName("GetContact")
            .WithSummary("Get contact by ID");

        group.MapGet("/", ListContacts)
            .WithName("ListContacts")
            .WithSummary("List contacts with optional filtering");

        group.MapPut("/{id}", UpdateContact)
            .WithName("UpdateContact")
            .WithSummary("Update an existing contact");

        group.MapDelete("/{id}", DeleteContact)
            .WithName("DeleteContact")
            .WithSummary("Delete a contact");
    }

    private static async Task<Results<Created<ContactDto>, BadRequest<string>>> CreateContact(
        CreateContactRequest request,
        IContactService contactService)
    {
        var result = await contactService.CreateContactAsync(request);
        
        if (!result.Success)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.Created($"/api/v1/contacts/{result.Data!.Id}", result.Data);
    }

    private static async Task<Results<Ok<ContactDto>, NotFound>> GetContact(
        Guid id,
        IContactService contactService)
    {
        var result = await contactService.GetContactAsync(id);
        
        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Ok<PagedResult<ContactDto>>> ListContacts(
        IContactService contactService,
        int page = 1,
        int pageSize = 20,
        string? search = null)
    {
        var result = await contactService.GetContactsAsync(page, pageSize, search);
        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<Ok<ContactDto>, BadRequest<string>, NotFound>> UpdateContact(
        Guid id,
        UpdateContactRequest request,
        IContactService contactService)
    {
        var result = await contactService.UpdateContactAsync(id, request);
        
        if (!result.Success)
        {
            if (result.Error == "Contact not found")
                return TypedResults.NotFound();
            return TypedResults.BadRequest(result.Error);
        }

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteContact(
        Guid id,
        IContactService contactService)
    {
        var result = await contactService.DeleteContactAsync(id);
        
        if (!result.Success)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}
