using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class WarehouseServiceTests
{
    [Fact]
    public async Task GetPagedNormalizesPagingAndMapsWarehouseDetails()
    {
        var (service, _, warehouses, _) = CreateService();
        var warehouse = NewWarehouse();
        warehouses.Setup(repository => repository.GetPagedAsync(
                1, 100, "Roma", WarehouseType.Standard, true, "name", true))
            .ReturnsAsync((new[] { warehouse }.AsEnumerable(), 3));

        var result = await service.GetPagedAsync(0, 500, "Roma", (int)WarehouseType.Standard, true, "name", true);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Data!.PageNumber);
        Assert.Equal(100, result.Data.PageSize);
        Assert.Equal(3, result.Data.TotalCount);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(warehouse.Id, item.Id);
        Assert.Equal("WH-1", item.Code);
        Assert.Equal("Roma", item.City);
        Assert.Equal("Standard", item.TypeLabel);
    }

    [Fact]
    public async Task GetPagedRejectsInvalidTypeWithoutQueryingRepository()
    {
        var (service, _, warehouses, _) = CreateService();

        var result = await service.GetPagedAsync(1, 20, null, int.MaxValue, null);

        Assert.False(result.Success);
        Assert.Equal("Invalid warehouse type", result.Error);
        warehouses.Verify(repository => repository.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<WarehouseType?>(),
            It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdReturnsFailureForMissingWarehouse()
    {
        var (service, _, warehouses, _) = CreateService();
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Warehouse?)null);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Warehouse not found", result.Error);
    }

    [Theory]
    [InlineData("", "Name", (int)WarehouseType.Standard, "Code is required")]
    [InlineData("WH-1", "", (int)WarehouseType.Standard, "Name is required")]
    [InlineData("WH-1", "Name", 999, "Invalid warehouse type")]
    public async Task CreateRejectsInvalidRequiredFields(
        string code, string name, int type, string expectedError)
    {
        var (service, _, warehouses, _) = CreateService();

        var result = await service.CreateAsync(new(code, name, type, Guid.NewGuid(), false));

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);
        warehouses.Verify(repository => repository.AddAsync(It.IsAny<Warehouse>()), Times.Never);
    }

    [Fact]
    public async Task CreateRejectsOversizedFieldsAndUnknownAddress()
    {
        var (service, _, warehouses, contacts) = CreateService();
        var addressId = Guid.NewGuid();
        contacts.Setup(repository => repository.AddressExistsAsync(addressId)).ReturnsAsync(false);

        var longCode = await service.CreateAsync(new(new string('x', 21), "Name", 1, addressId, false));
        var longName = await service.CreateAsync(new("WH-1", new string('x', 201), 1, addressId, false));
        var longDescription = await service.CreateAsync(new("WH-1", "Name", 1, addressId, false,
            Description: new string('x', 501)));
        var missingAddress = await service.CreateAsync(new("WH-1", "Name", 1, addressId, false));

        Assert.Equal("Code cannot exceed 20 characters", longCode.Error);
        Assert.Equal("Name cannot exceed 200 characters", longName.Error);
        Assert.Equal("Description cannot exceed 500 characters", longDescription.Error);
        Assert.Equal("Contact address not found", missingAddress.Error);
        warehouses.Verify(repository => repository.AddAsync(It.IsAny<Warehouse>()), Times.Never);
    }

    [Fact]
    public async Task CreateNormalizesCodeAndUnsetsThePreviousDefault()
    {
        var (service, work, warehouses, contacts) = CreateService();
        var addressId = Guid.NewGuid();
        var oldDefault = NewWarehouse(isDefault: true);
        Warehouse? created = null;
        contacts.Setup(repository => repository.AddressExistsAsync(addressId)).ReturnsAsync(true);
        warehouses.Setup(repository => repository.CodeExistsAsync("WH-2", null)).ReturnsAsync(false);
        warehouses.Setup(repository => repository.GetDefaultWarehouseAsync()).ReturnsAsync(oldDefault);
        warehouses.Setup(repository => repository.AddAsync(It.IsAny<Warehouse>()))
            .Callback<Warehouse>(warehouse =>
            {
                created = warehouse;
                SetId(warehouse, Guid.NewGuid());
                SetAddress(warehouse, Address(addressId));
            })
            .Returns(Task.CompletedTask);
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new(" wh-2 ", " Nuovo ", 1, addressId, true, true, " deposito "));

        Assert.True(result.Success, result.Error);
        Assert.Equal("WH-2", created!.Code);
        Assert.Equal("Nuovo", created.Name);
        Assert.Equal("deposito", created.Description);
        Assert.False(oldDefault.IsDefault);
        Assert.True(result.Data!.IsDefault);
        Assert.Equal("Via Roma", result.Data.Street);
        warehouses.Verify(repository => repository.CodeExistsAsync("WH-2", null), Times.Once);
        work.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateRejectsDuplicateCodeAndReportsPersistenceFailures()
    {
        var (service, work, warehouses, contacts) = CreateService();
        var addressId = Guid.NewGuid();
        contacts.Setup(repository => repository.AddressExistsAsync(addressId)).ReturnsAsync(true);
        warehouses.Setup(repository => repository.CodeExistsAsync("WH-1", null)).ReturnsAsync(true);

        var duplicate = await service.CreateAsync(new(" wh-1 ", "Name", 1, addressId, false));

        Assert.Equal("Warehouse code already exists", duplicate.Error);
        warehouses.Verify(repository => repository.AddAsync(It.IsAny<Warehouse>()), Times.Never);

        warehouses.Setup(repository => repository.CodeExistsAsync("WH-1", null)).ReturnsAsync(false);
        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new ConcurrencyException("Concurrent update"));
        var conflict = await service.CreateAsync(new("WH-1", "Name", 1, addressId, false));
        Assert.Equal("Concurrent update", conflict.Error);

        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException());
        var failure = await service.CreateAsync(new("WH-1", "Name", 1, addressId, false));
        Assert.Equal("Unable to create warehouse", failure.Error);
    }

    [Fact]
    public async Task UpdateValidatesFieldsAndReturnsMissingWarehouse()
    {
        var (service, _, warehouses, contacts) = CreateService();
        var addressId = Guid.NewGuid();
        contacts.Setup(repository => repository.AddressExistsAsync(addressId)).ReturnsAsync(true);

        var invalid = await service.UpdateAsync(Guid.NewGuid(), new("", 1, addressId, false, true));
        var invalidType = await service.UpdateAsync(Guid.NewGuid(), new("Name", 99, addressId, false, true));
        var noAddress = await service.UpdateAsync(Guid.NewGuid(), new("Name", 1, Guid.NewGuid(), false, true));
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Warehouse?)null);
        var missing = await service.UpdateAsync(Guid.NewGuid(), new("Name", 1, addressId, false, true));

        Assert.Equal("Name is required", invalid.Error);
        Assert.Equal("Invalid warehouse type", invalidType.Error);
        Assert.Equal("Contact address not found", noAddress.Error);
        Assert.Equal("Warehouse not found", missing.Error);
    }

    [Fact]
    public async Task UpdatePersistsWarehouseChangesAndHandlesConcurrencyFailure()
    {
        var (service, work, warehouses, contacts) = CreateService();
        var warehouse = NewWarehouse();
        var addressId = warehouse.ContactAddressId;
        contacts.Setup(repository => repository.AddressExistsAsync(addressId)).ReturnsAsync(true);
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(warehouse.Id)).ReturnsAsync(warehouse);
        warehouses.Setup(repository => repository.UpdateAsync(warehouse)).Returns(Task.CompletedTask);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await service.UpdateAsync(warehouse.Id,
            new("Updated", (int)WarehouseType.Defective, addressId, true, false, " updated "));

        Assert.True(result.Success, result.Error);
        Assert.Equal("Updated", warehouse.Name);
        Assert.Equal(WarehouseType.Defective, warehouse.Type);
        Assert.False(warehouse.IsActive);
        Assert.Equal("updated", warehouse.Description);

        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new ConcurrencyException("Concurrent update"));
        var conflict = await service.UpdateAsync(warehouse.Id,
            new("Updated again", 1, addressId, true, true));
        Assert.Equal("Concurrent update", conflict.Error);
    }

    [Fact]
    public async Task DeleteProtectsDefaultWarehouseAndSoftDeletesOtherWarehouses()
    {
        var (service, work, warehouses, _) = CreateService();
        warehouses.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Warehouse?)null);
        var missing = await service.DeleteAsync(Guid.NewGuid());
        Assert.Equal("Warehouse not found", missing.Error);

        var defaultWarehouse = NewWarehouse(isDefault: true);
        warehouses.Setup(repository => repository.GetByIdAsync(defaultWarehouse.Id)).ReturnsAsync(defaultWarehouse);
        var protectedResult = await service.DeleteAsync(defaultWarehouse.Id);
        Assert.Equal("Default warehouse cannot be deleted", protectedResult.Error);

        var warehouse = NewWarehouse();
        warehouses.Setup(repository => repository.GetByIdAsync(warehouse.Id)).ReturnsAsync(warehouse);
        warehouses.Setup(repository => repository.UpdateAsync(warehouse)).Returns(Task.CompletedTask);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);
        var deleted = await service.DeleteAsync(warehouse.Id);
        Assert.True(deleted.Success, deleted.Error);
        Assert.True(warehouse.IsDeleted);
        Assert.Equal("system", warehouse.DeletedBy);
    }

    [Fact]
    public async Task DeleteReportsConcurrencyAndUnexpectedPersistenceErrors()
    {
        var (service, work, warehouses, _) = CreateService();
        var warehouse = NewWarehouse();
        warehouses.Setup(repository => repository.GetByIdAsync(warehouse.Id)).ReturnsAsync(warehouse);
        warehouses.Setup(repository => repository.UpdateAsync(warehouse)).Returns(Task.CompletedTask);
        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new ConcurrencyException("Concurrent update"));

        var conflict = await service.DeleteAsync(warehouse.Id);
        Assert.Equal("Concurrent update", conflict.Error);

        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException());
        var failure = await service.DeleteAsync(warehouse.Id);
        Assert.Equal("Unable to delete warehouse", failure.Error);
    }

    [Fact]
    public async Task SetDefaultUnsetsPreviousDefaultAndMapsWarehouse()
    {
        var (service, work, warehouses, _) = CreateService();
        var warehouse = NewWarehouse();
        var previousDefault = NewWarehouse(isDefault: true);
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(warehouse.Id)).ReturnsAsync(warehouse);
        warehouses.Setup(repository => repository.GetDefaultWarehouseAsync()).ReturnsAsync(previousDefault);
        warehouses.Setup(repository => repository.UpdateAsync(warehouse)).Returns(Task.CompletedTask);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await service.SetDefaultAsync(warehouse.Id);

        Assert.True(result.Success, result.Error);
        Assert.False(previousDefault.IsDefault);
        Assert.True(warehouse.IsDefault);
        Assert.Equal("Roma", result.Data!.City);
    }

    [Fact]
    public async Task SetDefaultReturnsNotFoundAndPersistenceFailures()
    {
        var (service, work, warehouses, _) = CreateService();
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Warehouse?)null);
        Assert.Equal("Warehouse not found", (await service.SetDefaultAsync(Guid.NewGuid())).Error);

        var warehouse = NewWarehouse();
        warehouses.Setup(repository => repository.GetByIdWithAddressAsync(warehouse.Id)).ReturnsAsync(warehouse);
        warehouses.Setup(repository => repository.GetDefaultWarehouseAsync()).ReturnsAsync((Warehouse?)null);
        warehouses.Setup(repository => repository.UpdateAsync(warehouse)).Returns(Task.CompletedTask);
        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new ConcurrencyException("Concurrent update"));
        Assert.Equal("Concurrent update", (await service.SetDefaultAsync(warehouse.Id)).Error);

        work.Setup(unit => unit.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException());
        Assert.Equal("Unable to set default warehouse", (await service.SetDefaultAsync(warehouse.Id)).Error);
    }

    private static (WarehouseService Service, Mock<IUnitOfWork> Work,
        Mock<IWarehouseRepository> Warehouses, Mock<IContactRepository> Contacts) CreateService()
    {
        var work = new Mock<IUnitOfWork>();
        var warehouses = new Mock<IWarehouseRepository>();
        var contacts = new Mock<IContactRepository>();
        work.SetupGet(unit => unit.Warehouses).Returns(warehouses.Object);
        work.SetupGet(unit => unit.Contacts).Returns(contacts.Object);
        return (new WarehouseService(work.Object, NullLogger<WarehouseService>.Instance),
            work, warehouses, contacts);
    }

    private static Warehouse NewWarehouse(bool isDefault = false)
    {
        var address = Address(Guid.NewGuid());
        var warehouse = new Warehouse("WH-1", "Magazzino", WarehouseType.Standard,
            address.Id, isSellable: true, isDefault);
        SetId(warehouse, Guid.NewGuid());
        SetAddress(warehouse, address);
        return warehouse;
    }

    private static ContactAddress Address(Guid id)
    {
        var address = new ContactAddress(Guid.NewGuid(), AddressPurpose.WarehouseSite,
            "Via Roma", "00100", "Roma", "RM", siteName: "Sede");
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(address, id);
        return address;
    }

    private static void SetAddress(Warehouse warehouse, ContactAddress address) =>
        typeof(Warehouse).GetProperty(nameof(Warehouse.Address))!.SetValue(warehouse, address);

    private static void SetId(Entity<Guid> entity, Guid id) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, id);
}
