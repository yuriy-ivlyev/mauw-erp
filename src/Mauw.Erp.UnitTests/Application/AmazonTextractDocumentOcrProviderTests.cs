using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.Runtime;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Infrastructure.Ocr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class AmazonTextractDocumentOcrProviderTests
{
    [Fact]
    public async Task ParseAsync_MapsTableAndRemovesLettersAndSpacesFromQuantities()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.Is<AnalyzeDocumentRequest>(request =>
                    request.FeatureTypes.Count == 1 && request.FeatureTypes[0] == "TABLES"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTableResponse(
                (1, 1, "SKU / Modello"),
                (1, 2, "Descrizione Articolo (Modello / Colore / Taglia)"),
                (1, 3, "Pz"),
                (2, 1, "SUP-01"),
                (2, 2, "Vite"),
                (2, 3, "12 pz"),
                (3, 1, "SUP-02"),
                (3, 2, "Dado"),
                (3, 3, " 3 KG ")));

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1, 2, 3]);

        var result = await provider.ParseAsync(document);

        Assert.True(result.Success);
        Assert.Collection(result.Data!.Lines,
            line =>
            {
                Assert.Equal("SUP-01", line.SupplierCode);
                Assert.Equal("Vite", line.Description);
                Assert.Equal(12, line.Quantity);
            },
            line =>
            {
                Assert.Equal("SUP-02", line.SupplierCode);
                Assert.Equal("Dado", line.Description);
                Assert.Equal(3, line.Quantity);
            });
        client.VerifyAll();
    }

    [Fact]
    public async Task ParseAsync_RecognizesCommonItalianAlternativeHeaders()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.Is<AnalyzeDocumentRequest>(request =>
                    request.FeatureTypes.Count == 1 && request.FeatureTypes[0] == "TABLES"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTableResponse(
                (1, 1, "N. Art."),
                (1, 2, "Denominazione articolo"),
                (1, 3, "Pz"),
                (2, 1, "ART-01"),
                (2, 2, "Maglia"),
                (2, 3, "4 pz")));

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.True(result.Success);
        Assert.Equal(new("ART-01", "Maglia", 4), result.Data!.Lines.Single());
    }

    [Fact]
    public async Task ParseAsync_UsesConfiguredColumnAliases()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.IsAny<AnalyzeDocumentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTableResponse(
                (1, 1, "Rif. Art."),
                (1, 2, "Voce"),
                (1, 3, "Unità consegnata"),
                (2, 1, "RIF-01"),
                (2, 2, "Gomito"),
                (2, 3, "7 pz")));

        var provider = CreateProvider(client,
        [
            new(Guid.NewGuid(), "SupplierCode", "Rif. Art.", 0),
            new(Guid.NewGuid(), "Description", "Voce", 0),
            new(Guid.NewGuid(), "Quantity", "Unità consegnata", 0)
        ]);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.True(result.Success);
        Assert.Equal(new("RIF-01", "Gomito", 7), result.Data!.Lines.Single());
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureForFractionalQuantity()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.IsAny<AnalyzeDocumentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTableResponse(
                (1, 1, "Codice articolo"),
                (1, 2, "Descrizione"),
                (1, 3, "Quantità"),
                (2, 1, "SUP-01"),
                (2, 2, "Vite"),
                (2, 3, "2,5")));

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("intero positivo", result.Error);
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureWhenNoTableIsFound()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.IsAny<AnalyzeDocumentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalyzeDocumentResponse
            {
                Blocks = [new Block { BlockType = "WORD", Text = "Nessuna tabella" }]
            });

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("tabella", result.Error);
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureForEmptyDocumentWithoutCallingTextract()
    {
        var client = new Mock<IAmazonTextract>(MockBehavior.Strict);
        var provider = CreateProvider(client);
        await using var document = new MemoryStream();

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("vuoto", result.Error);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureWhenTextractCannotReadDocument()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.IsAny<AnalyzeDocumentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonTextractException("Documento non supportato."));

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("Amazon Textract", result.Error);
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureForAwsClientErrors()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.IsAny<AnalyzeDocumentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonClientException("AWS credentials are unavailable."));

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("credentials are unavailable", result.Error);
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureWhenRegionIsNotConfigured()
    {
        var provider = new AmazonTextractDocumentOcrProvider(new Lazy<IAmazonTextract>(
            () => throw new AmazonClientException("No RegionEndpoint or ServiceURL configured")),
            CreateAliasService().Object);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("No RegionEndpoint", result.Error);
    }

    [Fact]
    public void AddSupplierDocumentOcr_UsesTextractRegionInsteadOfDefaultAwsRegion()
    {
        var configuration = new ConfigurationManager();
        configuration["AWS:Region"] = "eu-south-1";
        configuration["AWS:TextractRegion"] = "eu-central-1";
        configuration["AWS:AccessKeyId"] = "test-access-key";
        configuration["AWS:SecretAccessKey"] = "test-secret-key";

        var services = new ServiceCollection();
        services.AddSupplierDocumentOcr(configuration);
        using var provider = services.BuildServiceProvider();

        var client = Assert.IsType<AmazonTextractClient>(
            provider.GetRequiredService<Lazy<IAmazonTextract>>().Value);

        Assert.Equal("eu-central-1", client.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public async Task ParseAsync_ReturnsFailureWhenTextractEndpointCannotBeReached()
    {
        var client = new Mock<IAmazonTextract>();
        client.Setup(textract => textract.AnalyzeDocumentAsync(
                It.IsAny<AnalyzeDocumentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(
                "Name or service not known (textract.eu-south-1.amazonaws.com:443)"));

        var provider = CreateProvider(client);
        await using var document = new MemoryStream([1]);

        var result = await provider.ParseAsync(document);

        Assert.False(result.Success);
        Assert.Contains("Impossibile raggiungere Amazon Textract", result.Error);
    }

    private static AnalyzeDocumentResponse CreateTableResponse(
        params (int Row, int Column, string Text)[] cells)
    {
        var blocks = new List<Block>();
        var cellIds = new List<string>();
        var index = 0;

        foreach (var (row, column, text) in cells)
        {
            var cellId = $"cell-{index}";
            var wordId = $"word-{index++}";
            cellIds.Add(cellId);
            blocks.Add(new Block
            {
                Id = cellId,
                BlockType = "CELL",
                RowIndex = row,
                ColumnIndex = column,
                Relationships = [new Relationship { Type = "CHILD", Ids = [wordId] }]
            });
            blocks.Add(new Block { Id = wordId, BlockType = "WORD", Text = text });
        }

        blocks.Add(new Block
        {
            Id = "table-1",
            BlockType = "TABLE",
            Relationships = [new Relationship { Type = "CHILD", Ids = cellIds }]
        });

        return new AnalyzeDocumentResponse { Blocks = blocks };
    }

    private static AmazonTextractDocumentOcrProvider CreateProvider(
        Mock<IAmazonTextract> client,
        IReadOnlyList<SupplierDocumentOcrHeaderAliasDto>? aliases = null) =>
        new(new Lazy<IAmazonTextract>(() => client.Object), CreateAliasService(aliases).Object);

    private static Mock<ISupplierDocumentOcrAliasService> CreateAliasService(
        IReadOnlyList<SupplierDocumentOcrHeaderAliasDto>? aliases = null)
    {
        var service = new Mock<ISupplierDocumentOcrAliasService>();
        service.Setup(aliasService => aliasService.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(aliases ??
            [
                new(Guid.NewGuid(), "SupplierCode", "Codice fornitore", 0),
                new(Guid.NewGuid(), "SupplierCode", "Codice articolo", 1),
                new(Guid.NewGuid(), "SupplierCode", "N. Art.", 2),
                new(Guid.NewGuid(), "SupplierCode", "SKU", 3),
                new(Guid.NewGuid(), "Description", "Descrizione", 0),
                new(Guid.NewGuid(), "Description", "Denominazione articolo", 1),
                new(Guid.NewGuid(), "Quantity", "Quantità", 0),
                new(Guid.NewGuid(), "Quantity", "N. pezzi", 1),
                new(Guid.NewGuid(), "Quantity", "Pz", 2)
            ]);
        return service;
    }
}
