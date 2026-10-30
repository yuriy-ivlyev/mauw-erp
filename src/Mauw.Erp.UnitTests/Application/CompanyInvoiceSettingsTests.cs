using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.Branding;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class CompanyInvoiceSettingsTests(PostgreSqlContainerFixture postgres)
{
    private const string Notes = "La merce viaggia con regolare imballaggio originale e identificativi barcode/SKU " +
        "per showroom/negozio. Trascorsi 8 giorni dalla ricezione merce non si accettano resi o contestazioni " +
        "su differenze taglia o capo non conforme.";

    private MauwDbContext CreateDb() => postgres.CreateDbContext();

    private static UpdateCompanySettingsRequest Request() => new()
    {
        BusinessName = "Mauw", ReaNumber = "591450", ReaOffice = "BO",
        ShareCapital = 10000.00m, IsShareCapitalFullyPaid = true, IsSoleShareholder = true,
        BankName = " Banca Prova ", Iban = "it60 x054 2811 1010 0000 0123 456",
        BicSwift = "bpititmm", InvoiceNotes = $" {Notes} ",
        LegalHeadquarters = new() { Street = "Via Roma 1", ZipCode = "00100", City = "Roma" }
    };

    [Fact]
    public async Task CompanyFieldsPersistAndRefreshCachedSettings()
    {
        await using var db = CreateDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var work = new UnitOfWork(db, cache);
        var service = new CompanySettingsService(work, cache, NullLogger<CompanySettingsService>.Instance);
        var result = await service.UpsertCompanyAsync(Request());
        Assert.True(result.Success, result.Error);
        Assert.Equal("IT60X0542811101000000123456", result.Data!.Iban);
        Assert.Equal("BPITITMM", result.Data.BicSwift);
        Assert.Equal("Banca Prova", result.Data.BankName);
        Assert.Equal(Notes, result.Data.InvoiceNotes);
        Assert.True(result.Data.IsShareCapitalFullyPaid);
        db.ChangeTracker.Clear();
        var stored = await db.Contacts.SingleAsync();
        Assert.Equal(result.Data, ContactMapper.MapToCompanySettings(stored) with
        {
            LegalHeadquarters = result.Data.LegalHeadquarters,
            OperationalSites = result.Data.OperationalSites
        });
        var request = Request();
        request.IsShareCapitalFullyPaid = false;
        request.Iban = null;
        request.InvoiceNotes = null;
        Assert.True((await service.UpsertCompanyAsync(request)).Success);
        var refreshed = (await service.GetCompanyAsync()).Data!;
        Assert.False(refreshed.IsShareCapitalFullyPaid);
        Assert.Null(refreshed.Iban);
        Assert.Null(refreshed.InvoiceNotes);
    }

    [Fact]
    public async Task AddingOperationalSiteThenSavingCompanyPreservesAddressReferences()
    {
        await using var db = CreateDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var company = new Contact("Mauw") { IsInternalCompany = true };
        var legalAddress = company.SetLegalHeadquarters("Via Roma 1", "00100", "Roma");
        db.Add(company);
        db.Add(new Warehouse("WH-1", "Magazzino", WarehouseType.Standard, legalAddress.Id, isSellable: true));
        await db.SaveChangesAsync();

        var service = new CompanySettingsService(new UnitOfWork(db, cache), cache,
            NullLogger<CompanySettingsService>.Instance);
        var siteResult = await service.AddOperationalSiteAsync(new CompanyAddressSiteRequest
        {
            Purpose = "WarehouseSite",
            SiteName = "Deposito",
            Street = "Via Milano 2",
            ZipCode = "20100",
            City = "Milano"
        });
        Assert.True(siteResult.Success, siteResult.Error);

        var result = await service.UpsertCompanyAsync(Request());

        Assert.True(result.Success, result.Error);
        var storedAddresses = await db.ContactAddresses.OrderBy(a => a.Purpose).ToListAsync();
        var warehouse = await db.Warehouses.SingleAsync();
        Assert.Equal(2, storedAddresses.Count);
        Assert.Contains(storedAddresses, a => a.Id == legalAddress.Id && a.Purpose == AddressPurpose.LegalHeadquarters);
        Assert.Contains(storedAddresses, a => a.Id == siteResult.Data!.Id && a.Purpose == AddressPurpose.WarehouseSite);
        Assert.Equal(legalAddress.Id, warehouse.ContactAddressId);
    }

    [Theory]
    [InlineData("iban")]
    [InlineData("bic")]
    [InlineData("capital")]
    [InlineData("notes")]
    public async Task InvalidSettingsDoNotMutateCompany(string invalid)
    {
        await using var db = CreateDb();
        var company = new Contact("Originale") { IsInternalCompany = true, BankName = "Banca originale" };
        db.Add(company);
        await db.SaveChangesAsync();
        var request = Request();
        switch (invalid)
        {
            case "iban": request.Iban = "IT60X0542811101000000123457"; break;
            case "bic": request.BicSwift = "invalid"; break;
            case "capital": request.ShareCapital = null; break;
            case "notes": request.InvoiceNotes = new string('x', 4001); break;
        }
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CompanySettingsService(new UnitOfWork(db, cache), cache,
            NullLogger<CompanySettingsService>.Instance);
        var result = await service.UpsertCompanyAsync(request);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal("Originale", company.BusinessName);
        Assert.Equal("Banca originale", company.BankName);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Single(db.Contacts);
    }

    [Theory]
    [InlineData("TD01", true, true, true)]
    [InlineData("ORDINE", false, true, true)]
    [InlineData("DDT", false, true, true)]
    [InlineData("NOTA CREDITO", false, true, true)]
    [InlineData("TD01", true, false, false)]
    [InlineData("ORDINE", false, false, false)]
    public async Task DocumentPdfsUseCompanyCapitalAndRealAgentWithInvoiceOnlyBankingAndNotes(
        string type, bool hasInvoiceDetails, bool hasAgent, bool fullyPaid)
    {
        await using var db = CreateDb();
        var calculator = new ItalianAccountingCalculator();
        var agent = new Contact("Rossi Rappresentanze");
        agent.EnableAsAgent();
        var customer = new Contact("Cliente");
        customer.EnableAsCustomer();
        var company = new Contact("Mauw") { IsInternalCompany = true };
        db.AddRange(company, customer, agent);
        customer.CustomerProfile!.DefaultAgentId = hasAgent ? agent.Id : null;
        var product = new Product(Guid.NewGuid(), "SUP", "SKU", "Articolo", 22m, 10m);
        db.Add(product);
        var variant = new ProductVariant(product.Id, "123", 10.00m, product.SupplierId);
        db.Add(variant);
        var invoice = new Invoice(customer.Id, "F-1");
        invoice.Items.Add(new(invoice.Id, variant.Id, 1, 10.00m, 22m));
        var order = new Order(
            customer.Id,
            "O-1",
            fulfillmentType: type == "ORDINE"
                ? OrderFulfillmentType.CashDesk
                : OrderFulfillmentType.StandardShipping);
        if (type == "ORDINE")
        {
            order.Items.Add(new OrderItem(order.Id, variant.Id, 1, 10.00m, 22m)
            {
                LineNetTotal = 10.00m
            });
            var shipping = order.SetShippingCharge(5.00m, 10m, null, calculator);
            Assert.True(shipping.Success, shipping.Error);
            order.FinalizeCashSale("Carta", "txn-123");
        }
        var ddt = new DdtDocument(customer.Id, "D-1");
        db.AddRange(invoice, order, ddt);
        var credit = new CreditNote(invoice.Id, "N-1")
        {
            TaxableTotal = 10.00m, VatTotal = 2.20m, GrossTotal = 12.20m, Reason = "Storno merce"
        };
        db.Add(credit);
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var work = new UnitOfWork(db, cache);
        var settings = new CompanySettingsService(work, cache, NullLogger<CompanySettingsService>.Instance);
        var request = Request();
        request.IsShareCapitalFullyPaid = fullyPaid;
        Assert.True((await settings.UpsertCompanyAsync(request)).Success);
        await DbInitializer.SeedCompanyLogosAsync(db);
        db.ChangeTracker.Clear();
        var services = new ServiceCollection();
        services.AddSingleton<ICompanyLogoService>(new CompanyLogoService(db));
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        var pdf = provider.GetRequiredService<IInvoicePdfGenerator>();
        var pricing = new DocumentPricingService(Mock.Of<Mauw.Erp.Domain.Repositories.IPriceListRepository>(), Mock.Of<IPriceListService>(), calculator);
        var invoices = new InvoiceService(work, calculator, settings, pdf);
        var orders = new OrderService(work, Mock.Of<IValidator<CreateOrderRequest>>(),
            new CreateOrderItemRequestValidator(), calculator, settings, pdf,
            NullLogger<OrderService>.Instance, pricing);
        var ddts = new DdtService(work, new CreateOrderItemRequestValidator(),
            calculator, settings, pdf, pricing);
        var result = type switch
        {
            "ORDINE" => await orders.GetOrderPdfAsync(order.Id),
            "DDT" => await ddts.GetDdtPdfAsync(ddt.Id),
            "NOTA CREDITO" => await invoices.GetInvoicePdfAsync(credit.Id),
            _ => await invoices.GetInvoicePdfAsync(invoice.Id)
        };
        Assert.True(result.Success, result.Error);
        using var document = UglyToad.PdfPig.PdfDocument.Open(result.Data!);
        var text = string.Join(" ", document.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
        var leftColumnText = string.Join(" ", document.GetPages().SelectMany(page => page.GetWords())
            .Where(word => word.BoundingBox.Left < 12.0 / 2.54 * 72.0).Select(word => word.Text));
        Assert.Contains("10.000,00", text);
        if (type == "ORDINE")
        {
            Assert.Contains("SPEDIZIONE", text);
            Assert.Contains("Spese di spedizione", text);
            Assert.Contains("17,70", text);
        }
        Assert.Contains("Capitale sociale", text);
        Assert.Equal(fullyPaid, text.Contains("i.v."));
        Assert.Contains("BO-591450", text);
        Assert.Contains("Soci: socio unico", text);
        Assert.Equal(hasAgent, text.Contains("Rossi Rappresentanze"));
        Assert.Equal(hasAgent, leftColumnText.Contains("Provvigione") && leftColumnText.Contains("committente"));
        Assert.Equal(hasInvoiceDetails, text.Contains("IT60X0542811101000000123456"));
        Assert.Equal(hasInvoiceDetails, text.Contains("BPITITMM"));
        Assert.Equal(hasInvoiceDetails, text.Contains("Banca Prova"));
        Assert.Equal(hasInvoiceDetails, text.Contains("Trascorsi 8 giorni"));
        if (type == "ORDINE")
        {
            Assert.Contains("RICEVUTA CASSA", text);
            Assert.Contains("TOTALE VENDITA", text);
            Assert.Contains("Carta", text);
        }
        var isFiscalDocument = type is "TD01" or "NOTA CREDITO";
        Assert.Equal(isFiscalDocument, text.Contains("trasmesso tramite"));
    }

    [Fact]
    public async Task InvoicePdfFailsWhenCompanyCapitalIsMissing()
    {
        await using var db = CreateDb();
        var company = new Contact("Mauw") { IsInternalCompany = true };
        company.SetLegalHeadquarters("Via Roma 1", "00100", "Roma");
        var customer = new Contact("Cliente");
        customer.EnableAsCustomer();
        db.AddRange(company, customer);
        await db.SaveChangesAsync();
        db.Add(new Invoice(customer.Id, "F-1"));
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var work = new UnitOfWork(db, cache);
        var settings = new CompanySettingsService(work, cache, NullLogger<CompanySettingsService>.Instance);
        var calculator = new ItalianAccountingCalculator();
        var service = new InvoiceService(
            work,
            calculator,
            settings,
            Mock.Of<IInvoicePdfGenerator>());

        var result = await service.GetInvoicePdfAsync(
            (await db.Invoices.SingleAsync()).Id);

        Assert.False(result.Success);
        Assert.Contains("capitale sociale", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidAssignedAgentFailsExplicitlyBeforeRenderingPdf()
    {
        await using var db = CreateDb();
        var customer = new Contact("Cliente");
        customer.EnableAsCustomer();
        customer.CustomerProfile!.DefaultAgentId = Guid.NewGuid();
        db.Add(customer);
        var invoice = new Invoice(customer.Id, "F-1");
        db.Add(invoice);
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var pdf = new Mock<IInvoicePdfGenerator>(MockBehavior.Strict);
        var service = new InvoiceService(new UnitOfWork(db, cache),
            new ItalianAccountingCalculator(), Mock.Of<ICompanySettingsService>(), pdf.Object);

        var result = await service.GetInvoicePdfAsync(invoice.Id);

        Assert.False(result.Success);
        Assert.Contains("agente", result.Error!);
        pdf.VerifyNoOtherCalls();
    }
}
