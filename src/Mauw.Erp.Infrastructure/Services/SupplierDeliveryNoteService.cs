using System.Data;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class SupplierDeliveryNoteService(
    MauwDbContext db, ISupplierDocumentStorage storage, ILogger<SupplierDeliveryNoteService> logger)
    : ISupplierDeliveryNoteService
{
    public const long MaximumFileSize = 20 * 1024 * 1024;
    private const string Missing = "Documento non trovato.";
    private const string Conflict = "Conflitto: documento modificato o non piu modificabile. Ricaricare il documento.";

    private IQueryable<SupplierDeliveryNote> Documents => db.SupplierDeliveryNotes
        .Include(x => x.Supplier).Include(x => x.Warehouse)
        .Include(x => x.Lines).ThenInclude(x => x.StockMovement)
        .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant!).ThenInclude(x => x.Product)
        .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant!).ThenInclude(x => x.Color)
        .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant!).ThenInclude(x => x.Size);

    public async Task<PagedResult<SupplierDeliveryNoteListItemDto>> ListAsync(
        int page, int pageSize, string? search, Guid? supplierId, string? status,
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.SupplierDeliveryNotes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.DocumentNumber.ToLower().Contains(term)
                || x.Supplier.BusinessName.ToLower().Contains(term)
                || x.Warehouse.Name.ToLower().Contains(term));
        }
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (Enum.TryParse<SupplierDeliveryNoteStatus>(status, true, out var parsedStatus))
            query = query.Where(x => x.Status == parsedStatus);
        if (dateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(dateFrom.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.DocumentDate >= from);
        }
        if (dateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(dateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.DocumentDate < to);
        }
        var count = await query.CountAsync(ct);
        var rows = await query.AsNoTracking()
            .OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.DocumentNumber, x.DocumentDate,
                SupplierName = x.Supplier.BusinessName,
                WarehouseName = x.Warehouse.Name,
                LineCount = x.Lines.Count,
                PieceCount = x.Lines.Sum(line => (int?)line.Quantity) ?? 0,
                x.Status
            }).ToListAsync(ct);
        var items = rows.Select(x => new SupplierDeliveryNoteListItemDto(
            x.Id, x.DocumentNumber, new(x.DocumentDate, TimeSpan.Zero), x.SupplierName,
            x.WarehouseName, x.LineCount, x.PieceCount, x.Status.ToString())).ToList();
        return new(items, page, pageSize, count);
    }

    public async Task<SupplierDeliveryNoteDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var note = await Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return note is null ? null : Map(note);
    }

    public async Task<OperationResult<SupplierArticleMatchDto>> FindArticleAsync(
        Guid id, string supplierItemCode, CancellationToken ct)
    {
        var note = await db.SupplierDeliveryNotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (note is null) return OperationResult<SupplierArticleMatchDto>.Fail(Missing);
        if (!Editable(note, note.Version)) return OperationResult<SupplierArticleMatchDto>.Fail(Conflict);
        if (string.IsNullOrWhiteSpace(supplierItemCode) || supplierItemCode.Length > 100)
            return OperationResult<SupplierArticleMatchDto>.Fail("Inserire un codice prodotto fornitore di massimo 100 caratteri.");

        var code = supplierItemCode.Trim();
        var mappings = await db.SupplierArticleMappings.AsNoTracking()
            .Where(x => x.SupplierId == note.SupplierId && x.SupplierItemCode == code)
            .ToDictionaryAsync(x => x.SupplierItemCode, x => x.ArticleVariantId, ct);
        Guid? mappedId = mappings.TryGetValue(code, out var mapped) ? mapped : null;
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(x => x.Product).Include(x => x.Color).Include(x => x.Size)
            .Where(x => x.Product.SupplierId == note.SupplierId
                && (x.SupplierCode == code || x.Product.SupplierCode == code || x.Id == mappedId || x.Barcode == code))
            .ToListAsync(ct);
        var match = ResolveArticle(code, variants, mappings);
        if (!match.Success) return OperationResult<SupplierArticleMatchDto>.Fail(match.Error!);
        if (match.Data is not Guid variantId)
            return OperationResult<SupplierArticleMatchDto>.Fail(
                "Nessun articolo univoco trovato per questo codice prodotto fornitore. Verificare il codice o associare manualmente la variante.");

        var variant = variants.Single(x => x.Id == variantId);
        return OperationResult<SupplierArticleMatchDto>.Ok(new(variant.Id, ArticleDescription(variant)!));
    }

    public async Task<OperationResult<SupplierDeliveryNoteDto>> UploadAsync(
        UploadSupplierDeliveryNoteRequest request, Stream stream, string fileName,
        string contentType, long length, CancellationToken ct)
    {
        if (length <= 0 || length > MaximumFileSize)
            return Fail("Caricare un documento non vuoto di massimo 20 MB.");
        if (request.DocumentDate == default || (request.DocumentNumber?.Length ?? 0) > 100)
            return Fail("Data obbligatoria e numero documento di massimo 100 caratteri.");
        if (!await SupplierExists(request.SupplierId, ct) || !await WarehouseExists(request.WarehouseId, ct))
            return Fail("Fornitore o magazzino non valido o non attivo.");
        if (request.SupplierPurchaseOrderId.HasValue
            && !await db.SupplierPurchaseOrders.AnyAsync(x => x.Id == request.SupplierPurchaseOrderId.Value
                && x.SupplierId == request.SupplierId
                && (x.Status == SupplierPurchaseOrderStatus.Confirmed
                    || x.Status == SupplierPurchaseOrderStatus.PartiallyReceived), ct))
            return Fail("Ordine fornitore non valido, non aperto o di un altro fornitore.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if ((contentType, extension) is not (("application/pdf", ".pdf") or ("image/png", ".png")
            or ("image/jpeg", ".jpg") or ("image/jpeg", ".jpeg")))
            return Fail("Sono ammessi solo PDF, PNG e JPEG.");

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaximumFileSize) return Fail("Il documento supera 20 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (!ValidSignature(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), contentType))
            return Fail("Contenuto del file incompatibile con il formato dichiarato.");
        buffer.Position = 0;
        var note = new SupplierDeliveryNote
        {
            SupplierId = request.SupplierId, WarehouseId = request.WarehouseId,
            SupplierPurchaseOrderId = request.SupplierPurchaseOrderId,
            DocumentNumber = request.DocumentNumber?.Trim() ?? "",
            DocumentDate = DocumentDay(request.DocumentDate), ContentType = contentType
        };
        note.FilePath = await storage.UploadAsync(note.Id, buffer, contentType, ct);
        try
        {
            db.SupplierDeliveryNotes.Add(note);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            try { await storage.DeleteAsync(note.FilePath, CancellationToken.None); }
            catch (Exception cleanupError)
            {
                logger.LogError(cleanupError, "Unable to clean up supplier document {Key}", note.FilePath);
            }
            throw;
        }
        return OperationResult<SupplierDeliveryNoteDto>.Ok((await GetAsync(note.Id, ct))!);
    }

    public async Task<OperationResult<SupplierDeliveryNoteDto>> SaveLinesAsync(
        Guid id, SaveSupplierDeliveryNoteLinesRequest request, bool parsed, CancellationToken ct)
    {
        var note = await Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (note is null) return Fail(Missing);
        if (!Editable(note, request.Version)) return Fail(Conflict);
        if (request.DocumentNumber is null || request.DocumentNumber.Length > 100 || request.DocumentDate == default
            || request.Lines is null || request.Lines.Count > 1000)
            return Fail("Testata non valida o piu di 1000 righe.");
        var newLines = new List<SupplierDeliveryNoteLine>();
        var mappings = await db.SupplierArticleMappings.AsNoTracking()
            .Where(x => x.SupplierId == note.SupplierId).ToDictionaryAsync(x => x.SupplierItemCode, x => x.ArticleVariantId, ct);
        var variants = await db.ProductVariants.AsNoTracking().Include(x => x.Product)
            .Where(x => x.Product.SupplierId == note.SupplierId).ToListAsync(ct);
        foreach (var input in request.Lines)
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 1000
                || input.SupplierItemCode is null || input.SupplierItemCode.Length > 100
                || input.Quantity <= 0 || !ValidPrice(input.UnitPrice))
                return Fail("Riga non valida: descrizione obbligatoria, quantita intera positiva e prezzo non negativo (massimo 8 decimali).");
            var code = input.SupplierItemCode.Trim();
            Guid? variantId = input.ArticleVariantId;
            SupplierPurchaseOrderLine? orderLine = null;
            if (input.SupplierPurchaseOrderLineId.HasValue)
            {
                if (note.SupplierPurchaseOrderId is null)
                    return Fail("La riga ordine puo essere associata solo a un DDT collegato a un ordine.");
                orderLine = await db.SupplierPurchaseOrderLines.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == input.SupplierPurchaseOrderLineId.Value
                        && x.SupplierPurchaseOrderId == note.SupplierPurchaseOrderId, ct);
                if (orderLine is null || (variantId.HasValue && variantId.Value != orderLine.ArticleVariantId))
                    return Fail("La riga d'ordine selezionata non appartiene all'ordine o alla variante indicata.");
                variantId = orderLine.ArticleVariantId;
            }
            if (variantId.HasValue && !variants.Any(x => x.Id == variantId))
                return Fail("La variante selezionata non appartiene al fornitore del documento.");
            if (!variantId.HasValue && code.Length > 0)
            {
                var match = ResolveArticle(code, variants, mappings);
                if (!match.Success) return Fail(match.Error!);
                variantId = match.Data;
            }
            if (variantId.HasValue && !variants.Any(x => x.Id == variantId))
                return Fail("Mapping fornitore non piu valido: correggere il catalogo.");
            newLines.Add(new()
            {
                SupplierDeliveryNoteId = id, Position = newLines.Count,
                SupplierItemCode = code, ArticleVariantId = variantId, Description = input.Description.Trim(),
                SupplierPurchaseOrderLineId = orderLine?.Id,
                Quantity = input.Quantity, UnitPrice = input.UnitPrice,
                LineStatus = variantId.HasValue ? SupplierDeliveryNoteLineStatus.Matched : SupplierDeliveryNoteLineStatus.NewArticleNeeded
            });
        }
        if (newLines.Where(x => x.SupplierItemCode.Length > 0).GroupBy(x => x.SupplierItemCode)
            .Any(g => g.Where(x => x.ArticleVariantId.HasValue).Select(x => x.ArticleVariantId).Distinct().Count() > 1))
            return Fail("Uno stesso codice fornitore non puo identificare varianti diverse.");
        db.SupplierDeliveryNoteLines.RemoveRange(note.Lines);
        db.SupplierDeliveryNoteLines.AddRange(newLines);
        note.Lines = newLines;
        note.DocumentNumber = request.DocumentNumber.Trim();
        note.DocumentDate = DocumentDay(request.DocumentDate);
        note.Status = parsed ? SupplierDeliveryNoteStatus.Parsed : SupplierDeliveryNoteStatus.Draft;
        note.Version = Guid.NewGuid();
        return await SaveAsync(note, ct);
    }

    public async Task<OperationResult<SupplierDeliveryNoteDto>> ConfirmAsync(Guid id, Guid version, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var note = await Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (note is null) return Fail(Missing);
            if (!Editable(note, version)) return Fail(Conflict);
            if (string.IsNullOrWhiteSpace(note.DocumentNumber) || note.Lines.Count == 0
                || note.Lines.Any(x => !x.ArticleVariantId.HasValue || x.Quantity <= 0
                    || !ValidPrice(x.UnitPrice) || x.LineStatus != SupplierDeliveryNoteLineStatus.Matched))
                return Fail("Specificare numero DDT e almeno una riga; tutte le righe devono essere associate con quantita intere positive.");
            if (!await SupplierExists(note.SupplierId, ct) || !await WarehouseExists(note.WarehouseId, ct))
                return Fail("Fornitore o magazzino non valido o non attivo.");
            SupplierPurchaseOrder? purchaseOrder = null;
            if (note.SupplierPurchaseOrderId.HasValue)
            {
                purchaseOrder = await db.SupplierPurchaseOrders.Include(x => x.Lines)
                    .SingleOrDefaultAsync(x => x.Id == note.SupplierPurchaseOrderId, ct);
                if (purchaseOrder is null || purchaseOrder.SupplierId != note.SupplierId
                    || purchaseOrder.Status is not (SupplierPurchaseOrderStatus.Confirmed or SupplierPurchaseOrderStatus.PartiallyReceived))
                    return Fail("L'ordine collegato non e piu aperto o non corrisponde al fornitore.");
                if (note.Lines.Any(line => line.SupplierPurchaseOrderLineId.HasValue
                    && !purchaseOrder.Lines.Any(orderLine => orderLine.Id == line.SupplierPurchaseOrderLineId
                        && orderLine.ArticleVariantId == line.ArticleVariantId)))
                    return Fail("Una riga DDT non corrisponde alla variante dell'ordine.");
                foreach (var orderLine in purchaseOrder.Lines)
                {
                    var incoming = note.Lines.Where(line => line.SupplierPurchaseOrderLineId == orderLine.Id
                        || (line.SupplierPurchaseOrderLineId is null
                            && line.ArticleVariantId == orderLine.ArticleVariantId)).Sum(line => (long)line.Quantity);
                    if ((long)orderLine.QuantityReceived + incoming > int.MaxValue)
                        return Fail("Quantita ricevuta superiore al limite supportato.");
                }
            }
            var ids = note.Lines.Select(x => x.ArticleVariantId!.Value).Distinct().ToArray();
            if (await db.ProductVariants.CountAsync(x => ids.Contains(x.Id) && x.Product.SupplierId == note.SupplierId, ct) != ids.Length)
                return Fail("Una variante non appartiene piu al fornitore.");
            var stocks = await db.StockItems.Where(x => x.WarehouseId == note.WarehouseId && ids.Contains(x.ProductVariantId))
                .ToDictionaryAsync(x => x.ProductVariantId, ct);
            foreach (var group in note.Lines.GroupBy(x => x.ArticleVariantId!.Value))
            {
                var quantity = stocks.GetValueOrDefault(group.Key)?.QuantityOnHand ?? 0m;
                if (quantity != decimal.Truncate(quantity) || quantity + group.Sum(x => (long)x.Quantity) > int.MaxValue)
                    return Fail("Giacenza non intera o superiore al limite supportato.");
            }
            var mappings = await db.SupplierArticleMappings.Where(x => x.SupplierId == note.SupplierId)
                .ToDictionaryAsync(x => x.SupplierItemCode, ct);
            foreach (var line in note.Lines)
            {
                if (line.SupplierItemCode.Length > 0 && mappings.TryGetValue(line.SupplierItemCode, out var existing)
                    && existing.ArticleVariantId != line.ArticleVariantId)
                    return Fail("Codice fornitore gia associato a una variante diversa.");
            }
            // Validate the entire document before mutating stock or creating movements.
            foreach (var line in note.Lines.OrderBy(x => x.Position))
            {
                var variantId = line.ArticleVariantId!.Value;
                if (!stocks.TryGetValue(variantId, out var stock))
                {
                    stock = new StockItem(note.WarehouseId, variantId);
                    db.StockItems.Add(stock);
                    stocks.Add(variantId, stock);
                }
                stock.ApplyMovement(line.Quantity);
                stock.SetUnitCost(line.UnitPrice);
                db.StockMovements.Add(StockMovement.FromSupplierDeliveryNote(note, line));
                if (purchaseOrder is not null)
                {
                    var orderLine = purchaseOrder.Lines.SingleOrDefault(x => x.Id == line.SupplierPurchaseOrderLineId)
                        ?? purchaseOrder.Lines.SingleOrDefault(x => x.ArticleVariantId == variantId);
                    if (orderLine is not null)
                    {
                        orderLine.QuantityReceived += line.Quantity;
                    }
                }
                if (line.SupplierItemCode.Length > 0 && !mappings.ContainsKey(line.SupplierItemCode))
                {
                    var mapping = new SupplierArticleMapping
                    {
                        SupplierId = note.SupplierId, SupplierItemCode = line.SupplierItemCode, ArticleVariantId = variantId
                    };
                    db.SupplierArticleMappings.Add(mapping);
                    mappings.Add(line.SupplierItemCode, mapping);
                }
                purchaseOrder?.RefreshProgress();
            }
            note.Status = SupplierDeliveryNoteStatus.Confirmed;
            note.Version = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<SupplierDeliveryNoteDto>.Ok(Map(note));
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Concurrent supplier confirmation rejected for {Id}", id);
            return Fail(Conflict);
        }
    }

    public async Task<OperationResult<SupplierDeliveryNoteDto>> CancelAsync(Guid id, Guid version, CancellationToken ct)
    {
        var note = await Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (note is null) return Fail(Missing);
        if (!Editable(note, version)) return Fail(Conflict);
        note.Status = SupplierDeliveryNoteStatus.Cancelled;
        note.Version = Guid.NewGuid();
        return await SaveAsync(note, ct);
    }

    public async Task<(Stream Stream, string ContentType)?> DownloadAsync(Guid id, CancellationToken ct)
    {
        var note = await db.SupplierDeliveryNotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return note is null ? null : await storage.DownloadAsync(note.FilePath, ct);
    }

    private async Task<OperationResult<SupplierDeliveryNoteDto>> SaveAsync(SupplierDeliveryNote note, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            var updated = await Documents.AsNoTracking().SingleAsync(x => x.Id == note.Id, ct);
            return OperationResult<SupplierDeliveryNoteDto>.Ok(Map(updated));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Concurrent supplier document edit rejected for {Id}", note.Id);
            return Fail(Conflict);
        }
    }

    private Task<bool> SupplierExists(Guid id, CancellationToken ct) =>
        db.Contacts.AnyAsync(x => x.Id == id && !x.IsDeleted && x.SupplierProfile != null
            && x.Status == ContactStatus.Active, ct);
    private Task<bool> WarehouseExists(Guid id, CancellationToken ct) =>
        db.Warehouses.AnyAsync(x => x.Id == id && x.IsActive && !x.IsDeleted, ct);
    private static bool Editable(SupplierDeliveryNote note, Guid version) => note.Version == version
        && note.Status is SupplierDeliveryNoteStatus.Draft or SupplierDeliveryNoteStatus.Parsed;
    private static bool ValidPrice(decimal value) => value >= 0m && value < 10000000000m
        && decimal.Round(value, 8, MidpointRounding.AwayFromZero) == value;
    private static DateTime DocumentDay(DateTimeOffset date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    private static bool ValidSignature(ReadOnlySpan<byte> bytes, string type) => type switch
    {
        "application/pdf" => bytes.StartsWith("%PDF-"u8),
        "image/png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => bytes.StartsWith(new byte[] { 255, 216, 255 }),
        _ => false
    };
    private static bool IsConcurrencyConflict(Exception ex)
    {
        for (Exception? error = ex; error is not null; error = error.InnerException)
        {
            if (error is DbUpdateConcurrencyException
                or PostgresException { SqlState: "40001" or "40P01" or "23505" })
                return true;
        }
        return false;
    }
    private static OperationResult<SupplierDeliveryNoteDto> Fail(string error) => OperationResult<SupplierDeliveryNoteDto>.Fail(error);
    private static OperationResult<Guid?> ResolveArticle(
        string code, IReadOnlyCollection<ProductVariant> variants, IReadOnlyDictionary<string, Guid> mappings)
    {
        var candidates = variants.Where(x => x.SupplierCode == code).Take(2).ToList();
        Guid? variantId = candidates.Count == 1 ? candidates[0].Id : null;
        if (variantId.HasValue && mappings.TryGetValue(code, out var existingMapping) && existingMapping != variantId.Value)
            return OperationResult<Guid?>.Fail("Il codice fornitore della variante e gia associato a un'altra variante.");
        if (candidates.Count == 0)
        {
            if (mappings.TryGetValue(code, out var mapped)) variantId = mapped;
            else
            {
                var productCandidates = variants.Where(x => x.Product.SupplierCode == code).Take(2).ToList();
                if (productCandidates.Count == 1) variantId = productCandidates[0].Id;
                else if (productCandidates.Count == 0)
                {
                    var barcodeCandidates = variants.Where(x => x.Barcode == code).Take(2).ToList();
                    if (barcodeCandidates.Count == 1) variantId = barcodeCandidates[0].Id;
                }
            }
        }
        if (variantId.HasValue && !variants.Any(x => x.Id == variantId))
            return OperationResult<Guid?>.Fail("Mapping fornitore non piu valido: correggere il catalogo.");
        return OperationResult<Guid?>.Ok(variantId);
    }

    private static SupplierDeliveryNoteDto Map(SupplierDeliveryNote x) => new(
        x.Id, x.SupplierId, x.Supplier.BusinessName, x.WarehouseId, x.Warehouse.Name,
        x.DocumentNumber, new(x.DocumentDate, TimeSpan.Zero), new(x.UploadedAt, TimeSpan.Zero),
        x.SupplierPurchaseOrderId, x.FilePath, x.Status.ToString(), x.Version,
        x.Lines.OrderBy(l => l.Position).Select(l => new SupplierDeliveryNoteLineDto(
            l.Id, l.SupplierItemCode, l.ArticleVariantId, ArticleDescription(l.ArticleVariant),
            l.Description, l.SupplierPurchaseOrderLineId, l.Quantity, l.UnitPrice,
            l.LineStatus.ToString(), l.StockMovement?.Id)).ToList());

    private static string? ArticleDescription(ProductVariant? variant)
    {
        if (variant is null) return null;
        var product = variant.Product.Name;
        var details = string.Join(" / ", new[] { variant.Color?.Name, variant.Size?.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return details.Length == 0 ? product : $"{product} - {details}";
    }
}
