using Mauw.Erp.Application.Validators;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Contact management endpoints for customers, suppliers, and agents.
/// </summary>
public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/contacts")
            .WithName("Contacts")
            .WithOpenApi();

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

    private static async Task<IResult> CreateContact(
        CreateContactRequest request,
        MauwDbContext dbContext,
        CreateContactRequestValidator validator)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.Errors);

        // Verify agent exists if specified
        if (request.AgentId.HasValue && request.AgentId != Guid.Empty)
        {
            var agent = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == request.AgentId);
            if (agent == null)
                return Results.NotFound("Specified agent not found");
        }

        // Parse contact type from int
        if (request.Type < 0 || request.Type > 2)
            return Results.BadRequest("Invalid contact type");
        var contactType = (ContactType)request.Type;

        // Create contact
        var contact = new Contact(
            type: contactType,
            businessName: request.BusinessName,
            vatNumber: request.VatNumber,
            sdiCode: request.SdiCode,
            defaultDiscountRate: request.DefaultDiscountRate,
            agentId: request.AgentId,
            defaultCommissionRate: request.DefaultCommissionRate);

        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        return Results.Created($"/api/v1/contacts/{contact.Id}", new ContactDto(
            contact.Id,
            contact.Type.ToString(),
            contact.BusinessName,
            contact.VatNumber,
            contact.SdiCode,
            contact.DefaultDiscountRate,
            contact.AgentId,
            contact.DefaultCommissionRate
        ));
    }

    private static async Task<IResult> GetContact(
        Guid id,
        MauwDbContext dbContext)
    {
        var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null)
            return Results.NotFound();

        return Results.Ok(new ContactDto(
            contact.Id,
            contact.Type.ToString(),
            contact.BusinessName,
            contact.VatNumber,
            contact.SdiCode,
            contact.DefaultDiscountRate,
            contact.AgentId,
            contact.DefaultCommissionRate
        ));
    }

    private static async Task<IResult> ListContacts(
        string? type = null,
        int page = 1,
        int pageSize = 10,
        MauwDbContext dbContext = null!)
    {
        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
            pageSize = maxPageSize;

        var query = dbContext.Contacts.AsQueryable();

        // Filter by type if specified
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<ContactType>(type, out var contactType))
        {
            query = query.Where(c => c.Type == contactType);
        }

        var totalCount = await query.CountAsync();
        var contacts = await query
            .OrderBy(c => c.BusinessName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = contacts.Select(c => new ContactDto(
            c.Id,
            c.Type.ToString(),
            c.BusinessName,
            c.VatNumber,
            c.SdiCode,
            c.DefaultDiscountRate,
            c.AgentId,
            c.DefaultCommissionRate
        )).ToList();

        return Results.Ok(new
        {
            items,
            pageInfo = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (decimal)pageSize)
            }
        });
    }

    private static async Task<IResult> UpdateContact(
        Guid id,
        UpdateContactRequest request,
        MauwDbContext dbContext,
        UpdateContactRequestValidator validator)
    {
        var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null)
            return Results.NotFound();

        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.Errors);

        // Verify agent exists if changing it
        if (request.AgentId.HasValue && request.AgentId != contact.AgentId)
        {
            if (request.AgentId != Guid.Empty)
            {
                var agent = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == request.AgentId);
                if (agent == null)
                    return Results.NotFound("Specified agent not found");
            }
        }

        // Update properties
        if (!string.IsNullOrEmpty(request.BusinessName))
            contact.BusinessName = request.BusinessName;

        if (!string.IsNullOrEmpty(request.VatNumber))
            contact.VatNumber = request.VatNumber;

        if (request.SdiCode != null)
            contact.SdiCode = request.SdiCode;

        if (request.DefaultDiscountRate.HasValue)
            contact.DefaultDiscountRate = request.DefaultDiscountRate.Value;

        if (request.DefaultCommissionRate.HasValue)
            contact.DefaultCommissionRate = request.DefaultCommissionRate.Value;

        if (request.AgentId.HasValue)
            contact.AgentId = request.AgentId == Guid.Empty ? null : request.AgentId;

        await dbContext.SaveChangesAsync();

        return Results.Ok(new ContactDto(
            contact.Id,
            contact.Type.ToString(),
            contact.BusinessName,
            contact.VatNumber,
            contact.SdiCode,
            contact.DefaultDiscountRate,
            contact.AgentId,
            contact.DefaultCommissionRate
        ));
    }

    private static async Task<IResult> DeleteContact(
        Guid id,
        MauwDbContext dbContext)
    {
        var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null)
            return Results.NotFound();

        try
        {
            dbContext.Contacts.Remove(contact);
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        }
        catch (DbUpdateException ex)
        {
            // Could not delete due to foreign key constraints
            return Results.BadRequest("Cannot delete contact: it has related orders, invoices, or commissions");
        }
    }
}
