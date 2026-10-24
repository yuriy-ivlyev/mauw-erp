using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class AgentCustomerAssignmentTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task AssignCustomerToAgentPersistsOnlyForCustomerAndAgentProfiles()
    {
        var agent = NewContact("Agente");
        agent.EnableAsAgent();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(agent.Id)).ReturnsAsync(agent);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);
        contacts.Setup(repository => repository.UpdateAsync(customer)).Returns(Task.CompletedTask);
        unitOfWork.Setup(work => work.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await service.AssignCustomerToAgentAsync(agent.Id, customer.Id);

        Assert.True(result.Success, result.Error);
        Assert.Equal(agent.Id, customer.CustomerProfile!.DefaultAgentId);
        contacts.Verify(repository => repository.UpdateAsync(customer), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AssigningCustomerToTheSameAgentIsIdempotent()
    {
        var agent = NewContact("Agente");
        agent.EnableAsAgent();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        customer.CustomerProfile!.DefaultAgentId = agent.Id;
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(agent.Id)).ReturnsAsync(agent);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);

        var result = await service.AssignCustomerToAgentAsync(agent.Id, customer.Id);

        Assert.True(result.Success, result.Error);
        Assert.Equal(agent.Id, customer.CustomerProfile.DefaultAgentId);
        contacts.Verify(repository => repository.UpdateAsync(It.IsAny<Contact>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignmentRejectsContactsWithoutRequiredRoles()
    {
        var nonAgent = NewContact("Contatto senza ruolo agente");
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        var nonCustomer = NewContact("Fornitore");
        nonCustomer.EnableAsSupplier();
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(nonAgent.Id)).ReturnsAsync(nonAgent);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(nonCustomer.Id)).ReturnsAsync(nonCustomer);

        var invalidAgent = await service.AssignCustomerToAgentAsync(nonAgent.Id, customer.Id);
        var invalidCustomer = await service.AssignCustomerToAgentAsync(nonAgent.Id, nonCustomer.Id);

        Assert.False(invalidAgent.Success);
        Assert.False(invalidCustomer.Success);
        Assert.Null(customer.CustomerProfile!.DefaultAgentId);
        contacts.Verify(repository => repository.UpdateAsync(It.IsAny<Contact>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignmentDoesNotReplaceAnotherAgent()
    {
        var currentAgent = Guid.NewGuid();
        var agent = NewContact("Nuovo agente");
        agent.EnableAsAgent();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        customer.CustomerProfile!.DefaultAgentId = currentAgent;
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(agent.Id)).ReturnsAsync(agent);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);

        var result = await service.AssignCustomerToAgentAsync(agent.Id, customer.Id);

        Assert.False(result.Success);
        Assert.Equal(currentAgent, customer.CustomerProfile.DefaultAgentId);
        contacts.Verify(repository => repository.UpdateAsync(It.IsAny<Contact>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task RemovingCustomerFromAgentClearsAssignmentAndPersistsIt()
    {
        var agentId = Guid.NewGuid();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        customer.CustomerProfile!.DefaultAgentId = agentId;
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);
        contacts.Setup(repository => repository.UpdateAsync(customer)).Returns(Task.CompletedTask);
        unitOfWork.Setup(work => work.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await service.RemoveCustomerFromAgentAsync(agentId, customer.Id);

        Assert.True(result.Success, result.Error);
        Assert.Null(customer.CustomerProfile.DefaultAgentId);
        contacts.Verify(repository => repository.UpdateAsync(customer), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AgentAssignmentReturnsConcurrencyFailure()
    {
        var agent = NewContact("Agente");
        agent.EnableAsAgent();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(agent.Id)).ReturnsAsync(agent);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);
        contacts.Setup(repository => repository.UpdateAsync(customer)).Returns(Task.CompletedTask);
        unitOfWork.Setup(work => work.SaveChangesAsync())
            .ThrowsAsync(new ConcurrencyException("Aggiornamento concorrente."));

        var result = await service.AssignCustomerToAgentAsync(agent.Id, customer.Id);

        Assert.False(result.Success);
        Assert.Equal("Aggiornamento concorrente.", result.Error);
        contacts.Verify(repository => repository.UpdateAsync(customer), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RemovingCustomerRequiresThatItBelongsToTheAgent()
    {
        var agentId = Guid.NewGuid();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        customer.CustomerProfile!.DefaultAgentId = Guid.NewGuid();
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);

        var result = await service.RemoveCustomerFromAgentAsync(agentId, customer.Id);

        Assert.False(result.Success);
        Assert.NotEqual(agentId, customer.CustomerProfile.DefaultAgentId);
        contacts.Verify(repository => repository.UpdateAsync(It.IsAny<Contact>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ContactCreateAndUpdateRejectNonAgentAssignments()
    {
        var nonAgent = NewContact("Contatto");
        var (service, _, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(nonAgent.Id)).ReturnsAsync(nonAgent);

        var create = await service.CreateAsync(new CreateContactRequest
        {
            BusinessName = "Nuovo cliente",
            TaxCode = "TAXCODE",
            CustomerProfile = new() { DefaultAgentId = nonAgent.Id }
        });
        var update = await service.UpdateAsync(Guid.NewGuid(), new UpdateContactRequest
        {
            CustomerProfile = new() { DefaultAgentId = nonAgent.Id }
        });

        Assert.False(create.Success);
        Assert.False(update.Success);
        Assert.Contains("agente", create.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("agente", update.Error, StringComparison.OrdinalIgnoreCase);
        contacts.Verify(repository => repository.AddAsync(It.IsAny<Contact>()), Times.Never);
    }

    [Fact]
    public async Task AgentCannotBeDisabledWhileCustomersRemainAssigned()
    {
        var agent = NewContact("Agente");
        agent.EnableAsAgent();
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.CountCustomersAssignedToAgentAsync(agent.Id)).ReturnsAsync(1);

        var result = await service.UpdateAsync(agent.Id, new UpdateContactRequest { DisableAgent = true });

        Assert.False(result.Success);
        Assert.Contains("Rimuovere i clienti associati", result.Error);
        contacts.Verify(repository => repository.UpdateAsync(It.IsAny<Contact>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AgentWithAssignedCustomersCannotBeDeleted()
    {
        var agent = NewContact("Agente");
        agent.EnableAsAgent();
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdAsync(agent.Id)).ReturnsAsync(agent);
        contacts.Setup(repository => repository.CountCustomersAssignedToAgentAsync(agent.Id)).ReturnsAsync(1);

        var result = await service.DeleteAsync(agent.Id);

        Assert.False(result.Success);
        Assert.False(agent.IsDeleted);
        Assert.Contains("Rimuovere i clienti associati", result.Error);
        contacts.Verify(repository => repository.UpdateAsync(It.IsAny<Contact>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CustomerListCanBeFilteredByAssignedOrUnassignedAgent()
    {
        await using var db = postgres.CreateDbContext();
        var agentId = Guid.NewGuid();
        var assigned = NewContact("Cliente assegnato");
        assigned.EnableAsCustomer();
        assigned.CustomerProfile!.DefaultAgentId = agentId;
        var unassigned = NewContact("Cliente libero");
        unassigned.EnableAsCustomer();
        var supplier = NewContact("Fornitore");
        supplier.EnableAsSupplier();
        await db.Contacts.AddRangeAsync(assigned, unassigned, supplier);
        await db.SaveChangesAsync();
        var repository = new ContactRepository(db);

        var assignedResult = await repository.GetPagedAsync(1, 10, isCustomer: true, assignedAgentId: agentId);
        var unassignedResult = await repository.GetPagedAsync(1, 10, isCustomer: true, hasAssignedAgent: false);

        Assert.Equal(new[] { assigned.Id }, assignedResult.Items.Select(contact => contact.Id));
        Assert.Equal(new[] { unassigned.Id }, unassignedResult.Items.Select(contact => contact.Id));
    }

    [Fact]
    public async Task AgentAssignmentAndRemovalRejectConcurrentUpdates()
    {
        var agentId = Guid.NewGuid();
        var customer = NewContact("Cliente");
        customer.EnableAsCustomer();
        customer.CustomerProfile!.DefaultAgentId = agentId;
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(customer.Id)).ReturnsAsync(customer);
        contacts.Setup(repository => repository.UpdateAsync(customer)).Returns(Task.CompletedTask);
        unitOfWork.Setup(work => work.SaveChangesAsync())
            .ThrowsAsync(new ConcurrencyException("Modifica concorrente."));

        var result = await service.RemoveCustomerFromAgentAsync(agentId, customer.Id);

        Assert.False(result.Success);
        Assert.Equal("Modifica concorrente.", result.Error);
        contacts.Verify(repository => repository.UpdateAsync(customer), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    private static (ContactService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IContactRepository> Contacts)
        CreateService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var contacts = new Mock<IContactRepository>();
        unitOfWork.SetupGet(work => work.Contacts).Returns(contacts.Object);
        var service = new ContactService(
            unitOfWork.Object,
            new CreateContactRequestValidator(),
            new UpdateContactRequestValidator(),
            NullLogger<ContactService>.Instance,
            Mock.Of<IPriceListRepository>());
        return (service, unitOfWork, contacts);
    }

    private static Contact NewContact(string businessName)
    {
        var contact = new Contact(businessName);
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(contact, Guid.NewGuid());
        return contact;
    }
}
