using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class ContactServiceTests
{
    [Fact]
    public async Task GetPagedAndGetByIdMapContactSummariesAndDetails()
    {
        var fixture = new Fixture();
        var contact = new Contact("Cliente");
        contact.EnableAsCustomer();
        contact.EnableAsAgent();
        contact.SetLegalHeadquarters("Via Roma", "00100", "Roma", "RM");
        var agentId = Guid.NewGuid();
        fixture.Contacts.Setup(repository => repository.GetPagedAsync(
                2, 10, "Roma", true, false, true, "Active", "name", true, agentId, false))
            .ReturnsAsync((new[] { contact }.AsEnumerable(), 1));
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var page = await fixture.Service.GetPagedAsync(new ContactFilterRequest
        {
            Page = 2,
            PageSize = 10,
            Search = "Roma",
            IsCustomer = true,
            IsSupplier = false,
            IsAgent = true,
            Status = "Active",
            SortBy = "name",
            SortDescending = true,
            AssignedAgentId = agentId,
            HasAssignedAgent = false
        });
        var detail = await fixture.Service.GetByIdAsync(contact.Id);
        var missing = await fixture.Service.GetByIdAsync(Guid.NewGuid());

        Assert.True(page.Success, page.Error);
        Assert.Equal(1, page.Data!.TotalCount);
        Assert.Equal("Roma", Assert.Single(page.Data.Items).City);
        Assert.True(detail.Success, detail.Error);
        Assert.True(detail.Data!.CustomerProfile is not null);
        Assert.True(detail.Data.AgentProfile is not null);
        Assert.Equal("00100", Assert.Single(detail.Data.Addresses).ZipCode);
        Assert.Equal("Contact not found", missing.Error);
    }

    [Fact]
    public async Task CreateNormalizesContactAndAppliesProfilesAddressesAndPersons()
    {
        var fixture = new Fixture();
        Contact? created = null;
        fixture.Contacts.Setup(repository => repository.AddAsync(It.IsAny<Contact>()))
            .Callback<Contact>(contact => created = contact)
            .Returns(Task.CompletedTask);
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);
        fixture.Work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);
        var request = new CreateContactRequest
        {
            BusinessName = " Cliente ",
            TradeName = " Negozio ",
            VatNumber = " IT123 ",
            TaxCode = " CF123 ",
            SdiCode = " ABC123 ",
            PecEmail = " pec@example.test ",
            CountryCode = "FR",
            FiscalRegime = " RF01 ",
            Status = "Active",
            CustomerProfile = new()
            {
                FinancialDiscountRate = 2m,
                CreditLimit = 1000m,
                IsBlocked = true,
                BlockReason = " insoluto ",
                DeliveryNotes = " consegna "
            },
            SupplierProfile = new() { Iban = " IT00 ", BicSwift = " BANKIT " },
            AgentProfile = new() { DefaultCommissionRate = 5m, OperatingArea = " Lazio " },
            Addresses =
            [
                new() { Purpose = "LegalHeadquarters", Street = " Via Roma ", ZipCode = "00100", City = "Roma" },
                new() { Purpose = "invalid", Street = "Ignored", ZipCode = "00000", City = "Ignored" }
            ],
            Persons = [new() { FirstName = " Ada ", LastName = " Rossi ", Role = "Amministrazione", IsPrimary = true }]
        };

        var result = await fixture.Service.CreateAsync(request);

        Assert.True(result.Success, result.Error);
        Assert.Equal(" Cliente ", created!.BusinessName);
        Assert.Equal("Negozio", created.TradeName);
        Assert.Equal("IT123", created.VatNumber);
        Assert.Equal("CF123", created.TaxCode);
        Assert.Equal("ABC123", created.SdiCode);
        Assert.Equal("pec@example.test", created.PecEmail);
        Assert.Equal("FR", created.CountryCode);
        Assert.Equal("RF01", created.FiscalRegime);
        Assert.Equal(ContactStatus.Active, created.Status);
        Assert.Equal(2m, created.CustomerProfile!.FinancialDiscountRate);
        Assert.Equal(" insoluto ", created.CustomerProfile.BlockReason);
        Assert.Equal(" IT00 ", created.SupplierProfile!.Iban);
        Assert.Equal(" BANKIT ", created.SupplierProfile.BicSwift);
        Assert.Equal(5m, created.AgentProfile!.DefaultCommissionRate);
        Assert.Single(created.Addresses);
        Assert.Single(created.Persons);
        Assert.Equal(" Via Roma ", Assert.Single(result.Data!.Addresses).Street);
        Assert.Equal(" Ada ", Assert.Single(result.Data.Persons).FirstName);
        fixture.Work.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateStopsOnValidationAgentAndMissingPriceListErrors()
    {
        var fixture = new Fixture();
        fixture.CreateValidator.Setup(validator => validator.Validate(It.IsAny<CreateContactRequest>()))
            .Returns(ValidationResult.Failure(new ValidationError("BusinessName", "is required")));
        var invalid = await fixture.Service.CreateAsync(new());
        Assert.Equal("BusinessName: is required", invalid.Error);

        fixture.CreateValidator.Setup(validator => validator.Validate(It.IsAny<CreateContactRequest>()))
            .Returns(ValidationResult.Success());
        var agentId = Guid.NewGuid();
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(agentId))
            .ReturnsAsync((Contact?)null);
        var invalidAgent = await fixture.Service.CreateAsync(new()
        {
            BusinessName = "Cliente",
            CustomerProfile = new() { DefaultAgentId = agentId }
        });
        Assert.Contains("non esiste", invalidAgent.Error);

        var priceListId = Guid.NewGuid();
        fixture.PriceLists.Setup(repository => repository.GetAsync(priceListId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);
        var missingPriceList = await fixture.Service.CreateAsync(new()
        {
            BusinessName = "Cliente",
            CustomerProfile = new() { PriceListId = priceListId }
        });
        Assert.Equal("Listino cliente non trovato.", missingPriceList.Error);
        fixture.Work.Verify(unit => unit.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateReturnsConcurrencyFailure()
    {
        var fixture = new Fixture();
        fixture.Contacts.Setup(repository => repository.AddAsync(It.IsAny<Contact>()))
            .Returns(Task.CompletedTask);
        fixture.Work.Setup(unit => unit.SaveChangesAsync())
            .ThrowsAsync(new ConcurrencyException("Concurrent update"));

        var result = await fixture.Service.CreateAsync(new() { BusinessName = "Cliente" });

        Assert.Equal("Concurrent update", result.Error);
    }

    [Fact]
    public async Task UpdateReplacesRolesAddressesAndPersonsAndNormalizesFields()
    {
        var fixture = new Fixture();
        var contact = new Contact("Originale");
        contact.EnableAsCustomer();
        contact.EnableAsSupplier();
        contact.EnableAsAgent();
        contact.SetLegalHeadquarters("Via Vecchia", "00100", "Roma");
        contact.Persons.Add(new ContactPerson(contact.Id, "Old", "Person"));
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);
        fixture.Contacts.Setup(repository => repository.UpdateAsync(contact)).Returns(Task.CompletedTask);
        fixture.Work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await fixture.Service.UpdateAsync(contact.Id, new UpdateContactRequest
        {
            BusinessName = " Aggiornato ",
            TradeName = " ",
            VatNumber = " IT456 ",
            TaxCode = " ",
            SdiCode = " SDI ",
            PecEmail = " pec@test ",
            CountryCode = "DE",
            FiscalRegime = " RF02 ",
            Status = "Active",
            CustomerProfile = new()
            {
                FinancialDiscountRate = 3m,
                CreditLimit = 500m,
                DeliveryNotes = " spedizione "
            },
            SupplierProfile = new() { Iban = " IT99 ", BicSwift = " BIC " },
            AgentProfile = new() { DefaultCommissionRate = 7m, OperatingArea = " Nord " },
            Addresses =
            [
                new() { Purpose = "OperationalSite", Street = "Via Nuova", ZipCode = "20100", City = "Milano" },
                new() { Purpose = "unsupported", Street = "Ignored" }
            ],
            Persons = [new() { FirstName = "Grace", LastName = "Hopper", IsPrimary = true }]
        });

        Assert.True(result.Success, result.Error);
        Assert.Equal(" Aggiornato ", contact.BusinessName);
        Assert.Null(contact.TradeName);
        Assert.Equal("IT456", contact.VatNumber);
        Assert.Null(contact.TaxCode);
        Assert.Equal("SDI", contact.SdiCode);
        Assert.Equal("pec@test", contact.PecEmail);
        Assert.Equal("DE", contact.CountryCode);
        Assert.Equal("RF02", contact.FiscalRegime);
        Assert.Equal(ContactStatus.Active, contact.Status);
        Assert.Equal(3m, contact.CustomerProfile!.FinancialDiscountRate);
        Assert.Equal(" spedizione ", contact.CustomerProfile.DeliveryNotes);
        Assert.Equal(" IT99 ", contact.SupplierProfile!.Iban);
        Assert.Equal(7m, contact.AgentProfile!.DefaultCommissionRate);
        Assert.Equal(AddressPurpose.OperationalSite, Assert.Single(contact.Addresses).Purpose);
        Assert.Equal("Grace", Assert.Single(contact.Persons).FirstName);
        fixture.Work.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateRejectsInvalidAssignmentsPriceListsAndMissingContacts()
    {
        var fixture = new Fixture();
        fixture.UpdateValidator.Setup(validator => validator.Validate(It.IsAny<UpdateContactRequest>()))
            .Returns(ValidationResult.Failure(new ValidationError("BusinessName", "invalid")));
        Assert.Equal("BusinessName: invalid",
            (await fixture.Service.UpdateAsync(Guid.NewGuid(), new())).Error);

        fixture.UpdateValidator.Setup(validator => validator.Validate(It.IsAny<UpdateContactRequest>()))
            .Returns(ValidationResult.Success());
        var agentId = Guid.NewGuid();
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(agentId))
            .ReturnsAsync((Contact?)null);
        var invalidAgent = await fixture.Service.UpdateAsync(Guid.NewGuid(), new()
        {
            CustomerProfile = new() { DefaultAgentId = agentId }
        });
        Assert.Contains("non esiste", invalidAgent.Error);

        var contactId = Guid.NewGuid();
        fixture.Contacts.Setup(repository => repository.CountCustomersAssignedToAgentAsync(contactId))
            .ReturnsAsync(2);
        var assignedCustomers = await fixture.Service.UpdateAsync(contactId, new() { DisableAgent = true });
        Assert.Contains("Rimuovere i clienti", assignedCustomers.Error);

        var priceListId = Guid.NewGuid();
        fixture.PriceLists.Setup(repository => repository.GetAsync(priceListId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);
        var missingPriceList = await fixture.Service.UpdateAsync(contactId, new()
        {
            CustomerProfile = new() { PriceListId = priceListId }
        });
        Assert.Equal("Listino cliente non trovato.", missingPriceList.Error);

        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contactId))
            .ReturnsAsync((Contact?)null);
        var missingContact = await fixture.Service.UpdateAsync(contactId, new());
        Assert.Equal("Contact not found", missingContact.Error);
    }

    [Fact]
    public async Task UpdateAndDeleteReportConcurrencyAndAgentReferences()
    {
        var fixture = new Fixture();
        var contact = new Contact("Cliente");
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id)).ReturnsAsync(contact);
        fixture.Contacts.Setup(repository => repository.UpdateAsync(contact)).Returns(Task.CompletedTask);
        fixture.Work.Setup(unit => unit.SaveChangesAsync())
            .ThrowsAsync(new ConcurrencyException("Concurrent update"));
        var update = await fixture.Service.UpdateAsync(contact.Id, new() { BusinessName = "Nuovo" });
        Assert.Equal("Concurrent update", update.Error);

        fixture.Contacts.Setup(repository => repository.GetByIdAsync(contact.Id)).ReturnsAsync(contact);
        fixture.Contacts.Setup(repository => repository.CountCustomersAssignedToAgentAsync(contact.Id))
            .ReturnsAsync(1);
        var assigned = await fixture.Service.DeleteAsync(contact.Id);
        Assert.Contains("clienti associati", assigned.Error);

        fixture.Contacts.Setup(repository => repository.CountCustomersAssignedToAgentAsync(contact.Id))
            .ReturnsAsync(0);
        var delete = await fixture.Service.DeleteAsync(contact.Id);
        Assert.Equal("Concurrent update", delete.Error);
        Assert.True(contact.IsDeleted);
    }

    [Fact]
    public async Task DeleteAndCustomerBlockingHandleMissingProfilesAndPersistUpdates()
    {
        var fixture = new Fixture();
        fixture.Contacts.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Contact?)null);
        Assert.Equal("Contact not found", (await fixture.Service.DeleteAsync(Guid.NewGuid())).Error);

        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Contact?)null);
        Assert.Equal("Contact not found",
            (await fixture.Service.ToggleBlockCustomerAsync(Guid.NewGuid(), true, "limit")).Error);

        var contact = new Contact("Fornitore");
        fixture.Contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id)).ReturnsAsync(contact);
        Assert.Equal("Contact does not have a customer profile",
            (await fixture.Service.ToggleBlockCustomerAsync(contact.Id, true, "limit")).Error);

        contact.EnableAsCustomer();
        fixture.Contacts.Setup(repository => repository.UpdateAsync(contact)).Returns(Task.CompletedTask);
        fixture.Work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);
        var blocked = await fixture.Service.ToggleBlockCustomerAsync(contact.Id, true, " insoluto ");
        Assert.True(blocked.Success, blocked.Error);
        Assert.True(contact.CustomerProfile!.IsBlocked);
        Assert.Equal(" insoluto ", contact.CustomerProfile.BlockReason);

        var unblocked = await fixture.Service.ToggleBlockCustomerAsync(contact.Id, false, "ignored");
        Assert.True(unblocked.Success, unblocked.Error);
        Assert.False(contact.CustomerProfile.IsBlocked);
        Assert.Null(contact.CustomerProfile.BlockReason);
    }

    private sealed class Fixture
    {
        public Mock<IUnitOfWork> Work { get; } = new();
        public Mock<IContactRepository> Contacts { get; } = new();
        public Mock<IValidator<CreateContactRequest>> CreateValidator { get; } = new();
        public Mock<IValidator<UpdateContactRequest>> UpdateValidator { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public ContactService Service { get; }

        public Fixture()
        {
            Work.SetupGet(unit => unit.Contacts).Returns(Contacts.Object);
            CreateValidator.Setup(validator => validator.Validate(It.IsAny<CreateContactRequest>()))
                .Returns(ValidationResult.Success());
            UpdateValidator.Setup(validator => validator.Validate(It.IsAny<UpdateContactRequest>()))
                .Returns(ValidationResult.Success());
            Service = new ContactService(Work.Object, CreateValidator.Object, UpdateValidator.Object,
                NullLogger<ContactService>.Instance, PriceLists.Object);
        }
    }
}
