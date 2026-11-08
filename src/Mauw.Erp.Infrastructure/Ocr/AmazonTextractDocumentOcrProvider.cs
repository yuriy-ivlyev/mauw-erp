using System.Globalization;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.Textract;
using Amazon.Textract.Model;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Infrastructure.Ocr;

public sealed class AmazonTextractDocumentOcrProvider : ISupplierDocumentOcrProvider
{
    private static readonly Regex QuantityTextNoise = new(@"[\p{L}\s]+", RegexOptions.Compiled);

    private readonly Lazy<IAmazonTextract> textract;
    private readonly ISupplierDocumentOcrAliasService aliasService;

    public AmazonTextractDocumentOcrProvider(
        Lazy<IAmazonTextract> textract,
        ISupplierDocumentOcrAliasService aliasService)
    {
        this.textract = textract;
        this.aliasService = aliasService;
    }

    public async Task<OperationResult<ParsedDocumentResponse>> ParseAsync(
        Stream document,
        CancellationToken cancellationToken = default)
    {
        if (document is null || !document.CanRead)
            return OperationResult<ParsedDocumentResponse>.Fail("Il documento non è leggibile.");

        try
        {
            var aliases = await aliasService.ListAsync(cancellationToken);
            if (aliases.Count == 0)
                return OperationResult<ParsedDocumentResponse>.Fail(
                    "Configurare gli alias OCR per codice, descrizione e quantità.");

            await using var documentBytes = new MemoryStream();
            await document.CopyToAsync(documentBytes, cancellationToken);
            if (documentBytes.Length == 0)
                return OperationResult<ParsedDocumentResponse>.Fail("Il documento è vuoto.");

            documentBytes.Position = 0;
            var response = await textract.Value.AnalyzeDocumentAsync(
                new AnalyzeDocumentRequest
                {
                    Document = new Document { Bytes = documentBytes },
                    FeatureTypes = ["TABLES"]
                },
                cancellationToken);

            return MapResponse(response, aliases);
        }
        catch (AmazonTextractException exception)
        {
            return OperationResult<ParsedDocumentResponse>.Fail(
                $"Amazon Textract non è riuscito a leggere il documento: {exception.Message}");
        }
        catch (AmazonClientException exception)
        {
            return OperationResult<ParsedDocumentResponse>.Fail(
                $"Errore nella richiesta AWS Textract: {exception.Message}");
        }
        catch (HttpRequestException exception)
        {
            return OperationResult<ParsedDocumentResponse>.Fail(
                $"Impossibile raggiungere Amazon Textract: {exception.Message}");
        }
        catch (IOException exception)
        {
            return OperationResult<ParsedDocumentResponse>.Fail(
                $"Impossibile leggere il documento o i secret AWS: {exception.Message}");
        }
    }

    private static OperationResult<ParsedDocumentResponse> MapResponse(
        AnalyzeDocumentResponse response,
        IReadOnlyList<SupplierDocumentOcrHeaderAliasDto> aliases)
    {
        var blocks = response.Blocks ?? [];
        var tables = blocks.Where(block => block.BlockType == "TABLE").ToList();
        if (tables.Count == 0)
            return OperationResult<ParsedDocumentResponse>.Fail("Nel documento non è stata trovata alcuna tabella.");

        var blocksById = blocks
            .Where(block => !string.IsNullOrWhiteSpace(block.Id))
            .GroupBy(block => block.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var lines = new List<ParsedDocumentLine>();
        var foundRecognizedTable = false;

        foreach (var table in tables)
        {
            var rows = ReadTableRows(table, blocksById);
            var header = rows
                .Select(row => (Row: row, Columns: FindColumns(row.Value, aliases)))
                .FirstOrDefault(candidate => candidate.Columns is not null);
            if (header.Columns is null)
                continue;

            var columns = header.Columns.Value;
            foundRecognizedTable = true;
            foreach (var (rowIndex, cells) in rows)
            {
                if (rowIndex <= header.Row.Key || FindColumns(cells, aliases) is not null)
                    continue;

                var supplierCode = GetCell(cells, columns.SupplierCode);
                var description = GetCell(cells, columns.Description);
                var rawQuantity = GetCell(cells, columns.Quantity);
                if (string.IsNullOrWhiteSpace(supplierCode) &&
                    string.IsNullOrWhiteSpace(description) &&
                    string.IsNullOrWhiteSpace(rawQuantity))
                    continue;

                if (string.IsNullOrWhiteSpace(supplierCode) || string.IsNullOrWhiteSpace(description))
                    return OperationResult<ParsedDocumentResponse>.Fail(
                        $"La riga {rowIndex} non contiene codice fornitore o descrizione.");

                var quantityText = QuantityTextNoise.Replace(rawQuantity, string.Empty);
                if (!int.TryParse(quantityText, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) ||
                    quantity <= 0)
                    return OperationResult<ParsedDocumentResponse>.Fail(
                        $"La quantità della riga {rowIndex} non è un intero positivo valido.");

                lines.Add(new ParsedDocumentLine(supplierCode.Trim(), description.Trim(), quantity));
            }
        }

        if (!foundRecognizedTable)
            return OperationResult<ParsedDocumentResponse>.Fail(
                "Non sono state riconosciute le colonne Codice, Descrizione e Quantità.");
        if (lines.Count == 0)
            return OperationResult<ParsedDocumentResponse>.Fail("La tabella non contiene righe articolo valide.");

        return OperationResult<ParsedDocumentResponse>.Ok(new ParsedDocumentResponse(lines));
    }

    private static SortedDictionary<int, SortedDictionary<int, string>> ReadTableRows(
        Block table,
        IReadOnlyDictionary<string, Block> blocksById)
    {
        var rows = new SortedDictionary<int, SortedDictionary<int, string>>();
        foreach (var childId in GetChildIds(table))
        {
            if (!blocksById.TryGetValue(childId, out var cell) || cell.BlockType != "CELL" ||
                cell.RowIndex is not > 0 || cell.ColumnIndex is not > 0)
                continue;

            var rowIndex = cell.RowIndex.Value;
            var columnIndex = cell.ColumnIndex.Value;
            if (!rows.TryGetValue(rowIndex, out var row))
            {
                row = new SortedDictionary<int, string>();
                rows.Add(rowIndex, row);
            }

            row[columnIndex] = ReadCellText(cell, blocksById);
        }

        return rows;
    }

    private static string ReadCellText(Block cell, IReadOnlyDictionary<string, Block> blocksById)
    {
        var words = GetChildIds(cell)
            .Where(blocksById.ContainsKey)
            .Select(id => blocksById[id])
            .Where(block => block.BlockType == "WORD" && !string.IsNullOrWhiteSpace(block.Text))
            .Select(block => block.Text!.Trim());

        return string.Join(" ", words);
    }

    private static IEnumerable<string> GetChildIds(Block block) =>
        block.Relationships?
            .Where(relationship => relationship.Type == "CHILD")
            .SelectMany(relationship => relationship.Ids ?? [])
        ?? [];

    private static (int SupplierCode, int Description, int Quantity)? FindColumns(
        IReadOnlyDictionary<int, string> cells,
        IReadOnlyList<SupplierDocumentOcrHeaderAliasDto> aliases)
    {
        var supplierCode = FindColumn(cells, aliases, SupplierDocumentOcrColumn.SupplierCode);
        var description = FindColumn(cells, aliases, SupplierDocumentOcrColumn.Description);
        var quantity = FindColumn(cells, aliases, SupplierDocumentOcrColumn.Quantity);

        if (supplierCode is null || description is null || quantity is null ||
            supplierCode == description || supplierCode == quantity || description == quantity)
            return null;

        return (supplierCode.Value, description.Value, quantity.Value);
    }

    private static int? FindColumn(
        IReadOnlyDictionary<int, string> cells,
        IReadOnlyList<SupplierDocumentOcrHeaderAliasDto> aliases,
        SupplierDocumentOcrColumn columnType)
    {
        var headers = aliases
            .Where(alias => string.Equals(alias.Column, columnType.ToString(), StringComparison.Ordinal))
            .Select(alias => SupplierDocumentOcrHeaderAlias.NormalizeAlias(alias.Alias))
            .ToArray();

        foreach (var (columnIndex, value) in cells)
        {
            var normalized = SupplierDocumentOcrHeaderAlias.NormalizeAlias(value);
            if (headers.Any(header => header == "articolo"
                    ? normalized == header || normalized.StartsWith(header, StringComparison.Ordinal)
                    : normalized.Contains(header, StringComparison.Ordinal)))
                return columnIndex;
        }

        return null;
    }

    private static string GetCell(IReadOnlyDictionary<int, string> cells, int columnIndex) =>
        cells.TryGetValue(columnIndex, out var value) ? value : string.Empty;

}
