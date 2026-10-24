using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class ContactAddressServiceTests
{
    [Fact]
    public async Task GetAddressesReturnsMappedAddressesAndFailsForMissingContact()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        var warehouseId = Guid.NewGuid();
        var address = new ContactAddress(contact.Id, AddressPurpose.WarehouseSite,
            "Via Roma 1", "00100", "Roma", "RM", "IT", "Magazzino",
            isMainOperationalSite: true, phone: "061234567", email: "test@example.com",
            accessNotes: "Ingresso laterale", latitude: 41.9, longitude: 12.5,
            formattedAddress: "Via Roma 1, Roma", externalPlaceId: "place-1")
        {
            LinkedWarehouseId = warehouseId
        };
        contact.Addresses.Add(address);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var result = await service.GetAddressesAsync(contact.Id);
        var missing = await service.GetAddressesAsync(Guid.NewGuid());

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data!);
        Assert.Equal(address.Id, dto.Id);
        Assert.Equal("WarehouseSite", dto.Purpose);
        Assert.Equal("Magazzino", dto.SiteName);
        Assert.Equal("Ingresso laterale", dto.AccessNotes);
        Assert.Equal(41.9, dto.Latitude);
        Assert.Equal(12.5, dto.Longitude);
        Assert.Equal("Via Roma 1, Roma", dto.FormattedAddress);
        Assert.Equal(warehouseId, dto.LinkedWarehouseId);
        Assert.False(missing.Success);
        Assert.Equal("Contact not found", missing.Error);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AddAddressDefaultsUnknownPurposeToOperationalSiteAndPersists()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var result = await service.AddAddressAsync(contact.Id, new ContactAddressRequest
        {
            Purpose = "not-a-purpose",
            SiteName = "Deposito",
            Street = "Via Verdi 2",
            ZipCode = "20100",
            City = "Milano",
            CountryCode = null!,
            IsMainOperationalSite = true
        });

        Assert.True(result.Success);
        Assert.Equal("OperationalSite", result.Data!.Purpose);
        Assert.Equal("IT", result.Data.CountryCode);
        Assert.Equal("Deposito", result.Data.SiteName);
        Assert.True(result.Data.IsMainOperationalSite);
        Assert.Single(contact.Addresses);
        contacts.Verify(repository => repository.UpdateAsync(contact), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddAddressSetsOrReplacesLegalHeadquarters()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        var existing = contact.SetLegalHeadquarters("Via Vecchia 1", "00100", "Roma");
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var result = await service.AddAddressAsync(contact.Id, new ContactAddressRequest
        {
            Purpose = "legalheadquarters",
            Street = "Via Nuova 2",
            ZipCode = "20100",
            City = "Milano",
            Province = "MI",
            Phone = "021234567"
        });

        Assert.True(result.Success);
        Assert.Equal(existing.Id, result.Data!.Id);
        Assert.Single(contact.Addresses);
        Assert.Equal("Via Nuova 2", existing.Street);
        Assert.Equal("Milano", existing.City);
        Assert.Equal("MI", existing.Province);
        Assert.Equal("IT", existing.CountryCode);
        Assert.Equal("021234567", existing.Phone);
        contacts.Verify(repository => repository.UpdateAsync(contact), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddAddressFailsForMissingContactWithoutSaving()
    {
        var (service, unitOfWork, contacts) = CreateService();
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Contact?)null);

        var result = await service.AddAddressAsync(Guid.NewGuid(), new ContactAddressRequest());

        Assert.False(result.Success);
        Assert.Equal("Contact not found", result.Error);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAddressChangesAddressFieldsAndGeocodingMetadata()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        var address = contact.AddOperationalSite(AddressPurpose.OperationalSite,
            "Via Vecchia 1", "00100", "Roma");
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var result = await service.UpdateAddressAsync(contact.Id, address.Id, new ContactAddressRequest
        {
            SiteName = "Sede aggiornata",
            Street = "Via Nuova 2",
            ZipCode = "20100",
            City = "Milano",
            Province = "MI",
            CountryCode = null!,
            IsMainOperationalSite = true,
            Phone = "021234567",
            Email = "sede@example.com",
            AccessNotes = "Citofono 3",
            Latitude = 45.46,
            Longitude = 9.19,
            FormattedAddress = "Via Nuova 2, Milano",
            ExternalPlaceId = "place-2"
        });

        Assert.True(result.Success);
        Assert.Equal("Sede aggiornata", result.Data!.SiteName);
        Assert.Equal("Via Nuova 2", address.Street);
        Assert.Equal("20100", address.ZipCode);
        Assert.Equal("Milano", address.City);
        Assert.Equal("MI", address.Province);
        Assert.Equal("IT", address.CountryCode);
        Assert.True(address.IsMainOperationalSite);
        Assert.Equal("021234567", address.Phone);
        Assert.Equal("sede@example.com", address.Email);
        Assert.Equal("Citofono 3", address.AccessNotes);
        Assert.Equal(45.46, address.Latitude);
        Assert.Equal(9.19, address.Longitude);
        Assert.Equal("Via Nuova 2, Milano", address.FormattedAddress);
        Assert.Equal("place-2", address.ExternalPlaceId);
        contacts.Verify(repository => repository.UpdateAsync(contact), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAddressFailsForMissingContactOrAddress()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var missingContact = await service.UpdateAddressAsync(Guid.NewGuid(), Guid.NewGuid(),
            new ContactAddressRequest());
        var missingAddress = await service.UpdateAddressAsync(contact.Id, Guid.NewGuid(),
            new ContactAddressRequest());

        Assert.False(missingContact.Success);
        Assert.Equal("Contact not found", missingContact.Error);
        Assert.False(missingAddress.Success);
        Assert.Equal("Address not found", missingAddress.Error);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAddressPreventsDeletingLegalHeadquartersAndRemovesOperationalSite()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        var headquarters = contact.SetLegalHeadquarters("Via Roma 1", "00100", "Roma");
        var site = contact.AddOperationalSite(AddressPurpose.OperationalSite,
            "Via Milano 2", "20100", "Milano");
        SetId(headquarters, Guid.NewGuid());
        SetId(site, Guid.NewGuid());
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var protectedAddress = await service.DeleteAddressAsync(contact.Id, headquarters.Id);
        var deleted = await service.DeleteAddressAsync(contact.Id, site.Id);

        Assert.False(protectedAddress.Success);
        Assert.Equal("La Sede Legale non può essere eliminata.", protectedAddress.Error);
        Assert.True(deleted.Success);
        Assert.True(deleted.Data);
        Assert.DoesNotContain(contact.Addresses, address => address.Id == site.Id);
        Assert.Contains(contact.Addresses, address => address.Id == headquarters.Id);
        contacts.Verify(repository => repository.UpdateAsync(contact), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAddressAndPersonReportMissingRecords()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(It.Is<Guid>(id => id != contact.Id)))
            .ReturnsAsync((Contact?)null);

        var missingContact = await service.DeleteAddressAsync(Guid.NewGuid(), Guid.NewGuid());
        var missingAddress = await service.DeleteAddressAsync(contact.Id, Guid.NewGuid());
        var missingPerson = await service.DeletePersonAsync(contact.Id, Guid.NewGuid());
        var missingPersonContact = await service.DeletePersonAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal("Contact not found", missingContact.Error);
        Assert.Equal("Indirizzo non trovato.", missingAddress.Error);
        Assert.Equal("Person not found", missingPerson.Error);
        Assert.Equal("Contact not found", missingPersonContact.Error);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeletePersonRemovesExistingPersonAndPersists()
    {
        var (service, unitOfWork, contacts) = CreateService();
        var contact = new Contact("Cliente");
        var person = new ContactPerson(contact.Id, "Mario", "Rossi");
        contact.Persons.Add(person);
        contacts.Setup(repository => repository.GetByIdWithDetailsAsync(contact.Id))
            .ReturnsAsync(contact);

        var result = await service.DeletePersonAsync(contact.Id, person.Id);

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Empty(contact.Persons);
        contacts.Verify(repository => repository.UpdateAsync(contact), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    private static (ContactAddressService Service, Mock<IUnitOfWork> UnitOfWork,
        Mock<IContactRepository> Contacts) CreateService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var contacts = new Mock<IContactRepository>();
        unitOfWork.SetupGet(work => work.Contacts).Returns(contacts.Object);
        var service = new ContactAddressService(unitOfWork.Object);
        return (service, unitOfWork, contacts);
    }

    private static void SetId(Entity<Guid> entity, Guid id) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, id);
}
