using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class SupplierPurchaseOrderService(
    MauwDbContext db,
    ILogger<SupplierPurchaseOrderService> logger) : ISupplierPurchaseOrderService
{
    private IQueryable<SupplierPurchaseOrder> Orders => db.SupplierPurchaseOrders
        .Include(x => x.Supplier)
        .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Product)
        .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Color)
        .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Size);

    public async Task<IReadOnlyList<SupplierPurchaseOrderDto>> ListAsync(Guid? supplierId, bool openOnly, CancellationToken ct)
    {
        var query = Orders.AsNoTracking();
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (openOnly)
            query = query.Where(x => x.Status == SupplierPurchaseOrderStatus.Confirmed
                || x.Status == SupplierPurchaseOrderStatus.PartiallyReceived);
        var orders = await query.OrderByDescending(x => x.OrderDate).ThenBy(x => x.OrderNumber)
            .Take(200).ToListAsync(ct);
        return orders.Select(Map).ToList();
    }

    public async Task<SupplierPurchaseOrderDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return order is null ? null : Map(order);
    }

    public async Task<OperationResult<SupplierPurchaseOrderDto>> CreateAsync(
        CreateSupplierPurchaseOrderRequest request, CancellationToken ct)
    {
        if (request.Lines is null || request.Lines.Count is 0 or > 1000
            || request.Notes?.Length > 2000 || request.OrderDate == default)
            return Fail("Testata non valida o il numero righe deve essere tra 1 e 1000.");
        if (!await db.Contacts.AnyAsync(x => x.Id == request.SupplierId && !x.IsDeleted
                && x.SupplierProfile != null && x.Status == ContactStatus.Active, ct))
            return Fail("Fornitore non valido o non attivo.");
        if (request.Lines.Any(x => x.ArticleVariantId == Guid.Empty || x.QuantityOrdered <= 0
                || !ValidPrice(x.UnitCost)))
            return Fail("Le righe devono avere variante, quantita positiva e costo con massimo 8 decimali.");
        if (request.Lines.Select(x => x.ArticleVariantId).Distinct().Count() != request.Lines.Count)
            return Fail("Ogni variante puo comparire una sola volta nell'ordine.");
        var variantIds = request.Lines.Select(x => x.ArticleVariantId).ToArray();
        if (await db.ProductVariants.CountAsync(x => variantIds.Contains(x.Id)
                && x.Product.SupplierId == request.SupplierId, ct) != variantIds.Length)
            return Fail("Una o piu varianti non appartengono al fornitore selezionato.");

        var nextNumber = await db.Database.SqlQueryRaw<long>(
            "SELECT nextval('supplier_purchase_order_number_seq') AS \"Value\"").SingleAsync(ct);
        var order = new SupplierPurchaseOrder
        {
            SupplierId = request.SupplierId,
            OrderNumber = $"OF-{DateTime.UtcNow:yyyyMMdd}-{nextNumber:D6}",
            OrderDate = DateTime.SpecifyKind(request.OrderDate.UtcDateTime, DateTimeKind.Utc),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lines = request.Lines.Select(x => new SupplierPurchaseOrderLine
            {
                ArticleVariantId = x.ArticleVariantId,
                QuantityOrdered = x.QuantityOrdered,
                UnitCost = x.UnitCost
            }).ToList()
        };
        db.SupplierPurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return OperationResult<SupplierPurchaseOrderDto>.Ok((await GetAsync(order.Id, ct))!);
    }

    public async Task<OperationResult<SupplierPurchaseOrderDto>> ConfirmAsync(Guid id, Guid version, CancellationToken ct)
    {
        var order = await Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Fail("Ordine fornitore non trovato.");
        if (order.Version != version || order.Status != SupplierPurchaseOrderStatus.Draft)
            return Fail("Conflitto: ordine modificato o non piu confermabile. Ricaricare l'ordine.");
        order.Confirm();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Concurrent supplier purchase order confirmation rejected for {Id}", id);
            return Fail("Conflitto: ordine modificato. Ricaricare l'ordine.");
        }
        return OperationResult<SupplierPurchaseOrderDto>.Ok((await GetAsync(id, ct))!);
    }

    public async Task<SupplierReconciliationDto?> ReconcileAsync(Guid id, CancellationToken ct)
    {
        var order = await Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return null;
        var notes = await db.SupplierDeliveryNotes.AsNoTracking()
            .Where(x => x.SupplierPurchaseOrderId == id && x.Status == SupplierDeliveryNoteStatus.Confirmed)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant!).ThenInclude(x => x.Product)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant!).ThenInclude(x => x.Color)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant!).ThenInclude(x => x.Size)
            .ToListAsync(ct);
        var receiptLines = notes.SelectMany(x => x.Lines).ToList();
        var issuedReturns = await db.SupplierReturnDraftLines.AsNoTracking()
            .Where(x => x.SupplierReturnDraft.SupplierPurchaseOrderId == id
                && x.SupplierReturnDraft.Status == SupplierReturnDraftStatus.Issued)
            .GroupBy(x => x.SupplierDeliveryNoteLineId)
            .Select(group => new { LineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToDictionaryAsync(x => x.LineId, x => x.Quantity, ct);
        foreach (var line in receiptLines)
            line.Quantity = Math.Max(0, line.Quantity - issuedReturns.GetValueOrDefault(line.Id));
        receiptLines = receiptLines.Where(x => x.Quantity > 0).ToList();
        var lines = new List<SupplierReconciliationLineDto>();
        foreach (var orderLine in order.Lines)
        {
            var matches = receiptLines.Where(x => x.SupplierPurchaseOrderLineId == orderLine.Id
                || (x.SupplierPurchaseOrderLineId is null && x.ArticleVariantId == orderLine.ArticleVariantId)).ToList();
            var received = matches.Sum(x => x.Quantity);
            var delta = received - orderLine.QuantityOrdered;
            lines.Add(new(orderLine.Id, orderLine.ArticleVariantId, orderLine.ArticleVariant.Barcode,
                ArticleDescription(orderLine.ArticleVariant),
                orderLine.QuantityOrdered, received, delta, State(delta),
                matches.Select(x => new SupplierReconciliationReceiptDto(
                    x.Id, x.SupplierDeliveryNoteId, x.SupplierDeliveryNote.DocumentNumber, x.Quantity,
                    x.SupplierPurchaseOrderLineId == orderLine.Id)).ToList()));
        }
        foreach (var extra in receiptLines.Where(x => x.SupplierPurchaseOrderLineId is null
            && !order.Lines.Any(line => line.ArticleVariantId == x.ArticleVariantId)))
        {
            if (lines.Any(x => x.ArticleVariantId == extra.ArticleVariantId && x.QuantityOrdered == 0))
            {
                var i = lines.FindIndex(x => x.ArticleVariantId == extra.ArticleVariantId && x.QuantityOrdered == 0);
                var previous = lines[i];
                lines[i] = previous with
                {
                    QuantityReceived = previous.QuantityReceived + extra.Quantity,
                    Delta = previous.Delta + extra.Quantity,
                    Status = "Surplus",
                    Receipts = previous.Receipts.Append(new SupplierReconciliationReceiptDto(
                        extra.Id, extra.SupplierDeliveryNoteId, extra.SupplierDeliveryNote.DocumentNumber,
                        extra.Quantity, false)).ToList()
                };
            }
            else
            {
                lines.Add(new(null, extra.ArticleVariantId!.Value, extra.ArticleVariant?.Barcode ?? "",
                    ArticleDescription(extra.ArticleVariant),
                    0, extra.Quantity, extra.Quantity, "Surplus",
                    [new SupplierReconciliationReceiptDto(extra.Id, extra.SupplierDeliveryNoteId,
                        extra.SupplierDeliveryNote.DocumentNumber, extra.Quantity, false)]));
            }
        }
        return new(id, order.OrderNumber, order.Status.ToString(), lines);
    }

    public async Task<IReadOnlyList<SupplierShortageLineDto>?> ShortagesAsync(Guid id, CancellationToken ct)
    {
        var order = await Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return order is null ? null : order.Lines
            .Where(x => x.QuantityOrdered > x.QuantityReceived)
            .Select(x => new SupplierShortageLineDto(x.ArticleVariantId, x.ArticleVariant.Barcode,
                ArticleDescription(x.ArticleVariant),
                x.QuantityOrdered - x.QuantityReceived)).ToList();
    }

    public async Task<IReadOnlyList<SupplierReturnDraftDto>?> ReturnDraftsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.SupplierPurchaseOrders.AnyAsync(x => x.Id == id, ct)) return null;
        var drafts = await db.SupplierReturnDrafts.AsNoTracking()
            .Where(x => x.SupplierPurchaseOrderId == id)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Product)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Color)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Size)
            .OrderByDescending(x => x.CreatedOn).ToListAsync(ct);
        return drafts.Select(x => MapReturnDraft(x)).ToList();
    }

    public async Task<SupplierReturnDraftDto?> GetReturnDraftAsync(Guid id, Guid draftId, CancellationToken ct)
    {
        var draft = await db.SupplierReturnDrafts.AsNoTracking()
            .Where(x => x.Id == draftId && x.SupplierPurchaseOrderId == id)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Product)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Color)
            .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant).ThenInclude(x => x.Size)
            .SingleOrDefaultAsync(ct);
        return draft is null ? null : MapReturnDraft(draft);
    }

    public async Task<OperationResult<SupplierReturnDraftDto>> CreateReturnDraftAsync(
        Guid id, CreateSupplierReturnDraftRequest request, CancellationToken ct)
    {
        if (request.Lines is null || request.Lines.Count == 0
            || request.Lines.Any(x => x.SupplierDeliveryNoteLineId == Guid.Empty || x.Quantity <= 0)
            || request.Lines.Select(x => x.SupplierDeliveryNoteLineId).Distinct().Count() != request.Lines.Count)
            return OperationResult<SupplierReturnDraftDto>.Fail("Selezionare righe di reso univoche con quantita positive.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var order = await db.SupplierPurchaseOrders.Include(x => x.Lines)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
            if (order is null)
                return OperationResult<SupplierReturnDraftDto>.Fail("Ordine fornitore non trovato.");

            var selectedIds = request.Lines.Select(x => x.SupplierDeliveryNoteLineId).ToArray();
            var receiptLines = await db.SupplierDeliveryNoteLines
                .Where(x => selectedIds.Contains(x.Id) && x.SupplierDeliveryNote.SupplierPurchaseOrderId == id
                    && x.SupplierDeliveryNote.Status == SupplierDeliveryNoteStatus.Confirmed)
                .Include(x => x.SupplierDeliveryNote)
                .Include(x => x.ArticleVariant!).ThenInclude(x => x.Product)
                .Include(x => x.ArticleVariant!).ThenInclude(x => x.Color)
                .Include(x => x.ArticleVariant!).ThenInclude(x => x.Size)
                .ToListAsync(ct);
            if (receiptLines.Count != request.Lines.Count)
                return OperationResult<SupplierReturnDraftDto>.Fail(
                    "Una riga non appartiene a un DDT confermato dell'ordine.");
            if (receiptLines.Select(x => x.SupplierDeliveryNoteId).Distinct().Count() != 1)
                return OperationResult<SupplierReturnDraftDto>.Fail(
                    "Creare il reso per righe appartenenti allo stesso DDT.");

            var draftedByLine = await db.SupplierReturnDraftLines
                .Where(x => selectedIds.Contains(x.SupplierDeliveryNoteLineId))
                .GroupBy(x => x.SupplierDeliveryNoteLineId)
                .Select(group => new { LineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
                .ToDictionaryAsync(x => x.LineId, x => x.Quantity, ct);
            foreach (var selected in request.Lines)
            {
                var source = receiptLines.Single(x => x.Id == selected.SupplierDeliveryNoteLineId);
                var alreadyDrafted = draftedByLine.GetValueOrDefault(source.Id);
                if (selected.Quantity > source.Quantity - alreadyDrafted)
                    return OperationResult<SupplierReturnDraftDto>.Fail(
                        "La quantita selezionata supera la riga DDT.");
            }

            foreach (var selectedGroup in request.Lines.GroupBy(selected =>
                         receiptLines.Single(line => line.Id == selected.SupplierDeliveryNoteLineId).ArticleVariantId))
            {
                var variantId = selectedGroup.Key!.Value;
                var ordered = order.Lines.Where(x => x.ArticleVariantId == variantId)
                    .Select(x => (int?)x.QuantityOrdered).SingleOrDefault() ?? 0;
                var receivedForVariant = await db.SupplierDeliveryNoteLines
                    .Where(x => x.SupplierDeliveryNote.SupplierPurchaseOrderId == id
                        && x.SupplierDeliveryNote.Status == SupplierDeliveryNoteStatus.Confirmed
                        && x.ArticleVariantId == variantId)
                    .SumAsync(x => (int?)x.Quantity, ct) ?? 0;
                var draftedForVariant = await db.SupplierReturnDraftLines
                    .Where(x => x.SupplierReturnDraft.SupplierPurchaseOrderId == id
                        && x.ArticleVariantId == variantId)
                    .SumAsync(x => (int?)x.Quantity, ct) ?? 0;
                var surplus = Math.Max(0, receivedForVariant - ordered - draftedForVariant);
                if (selectedGroup.Sum(x => x.Quantity) > surplus)
                    return OperationResult<SupplierReturnDraftDto>.Fail(
                        "La quantita selezionata supera l'eccedenza non ancora preparata per il reso.");
            }

            var draft = new SupplierReturnDraft
            {
                SupplierPurchaseOrderId = id,
                SupplierDeliveryNoteId = receiptLines[0].SupplierDeliveryNoteId,
                Lines = request.Lines.Select(x =>
                {
                    var source = receiptLines.Single(y => y.Id == x.SupplierDeliveryNoteLineId);
                    return new SupplierReturnDraftLine
                    {
                        SupplierDeliveryNoteLineId = source.Id,
                        ArticleVariantId = source.ArticleVariantId!.Value,
                        Quantity = x.Quantity
                    };
                }).ToList()
            };
            db.SupplierReturnDrafts.Add(draft);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<SupplierReturnDraftDto>.Ok(MapReturnDraft(draft, receiptLines));
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Concurrent supplier return draft rejected for order {Id}", id);
            return OperationResult<SupplierReturnDraftDto>.Fail("Conflitto: eccedenza gia preparata per il reso. Ricaricare la riconciliazione.");
        }
    }

    public async Task<OperationResult<SupplierReturnDraftDto>> IssueReturnDraftAsync(
        Guid id, Guid draftId, Guid version, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var draft = await db.SupplierReturnDrafts
                .Include(x => x.SupplierDeliveryNote)
                .Include(x => x.Lines).ThenInclude(x => x.SupplierDeliveryNoteLine)
                .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant)
                    .ThenInclude(x => x.Product)
                .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant)
                    .ThenInclude(x => x.Color)
                .Include(x => x.Lines).ThenInclude(x => x.ArticleVariant)
                    .ThenInclude(x => x.Size)
                .SingleOrDefaultAsync(x => x.Id == draftId && x.SupplierPurchaseOrderId == id, ct);
            if (draft is null) return OperationResult<SupplierReturnDraftDto>.Fail("Bozza di reso non trovata.");
            if (draft.Status != SupplierReturnDraftStatus.Draft || draft.Version != version)
                return OperationResult<SupplierReturnDraftDto>.Fail("Conflitto: bozza di reso modificata o gia emessa.");
            if (draft.Lines.Count == 0 || draft.Lines.Any(x => x.Quantity <= 0))
                return OperationResult<SupplierReturnDraftDto>.Fail("La bozza di reso non contiene righe valide.");
            var order = await db.SupplierPurchaseOrders.Include(x => x.Lines)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
            if (order is null) return OperationResult<SupplierReturnDraftDto>.Fail("Ordine fornitore non trovato.");
            var variantIds = draft.Lines.Select(x => x.ArticleVariantId).Distinct().ToArray();
            var stocks = await db.StockItems.Where(x => x.WarehouseId == draft.SupplierDeliveryNote.WarehouseId
                    && variantIds.Contains(x.ProductVariantId))
                .ToDictionaryAsync(x => x.ProductVariantId, ct);
            foreach (var group in draft.Lines.GroupBy(x => x.ArticleVariantId))
            {
                if (!stocks.TryGetValue(group.Key, out var stock)
                    || stock.QuantityOnHand != decimal.Truncate(stock.QuantityOnHand)
                    || stock.QuantityOnHand < group.Sum(x => (long)x.Quantity))
                    return OperationResult<SupplierReturnDraftDto>.Fail("Giacenza insufficiente o non intera per emettere il reso.");
                var orderLine = order.Lines.SingleOrDefault(x => x.ArticleVariantId == group.Key);
                if (orderLine is not null && orderLine.QuantityReceived < group.Sum(x => (long)x.Quantity))
                    return OperationResult<SupplierReturnDraftDto>.Fail("Quantita reso superiore a quella ricevuta registrata.");
            }
            foreach (var line in draft.Lines)
            {
                stocks[line.ArticleVariantId].ApplyMovement(-line.Quantity);
                db.StockMovements.Add(StockMovement.FromSupplierReturnDraft(draft, line));
                var orderLine = order.Lines.SingleOrDefault(x => x.ArticleVariantId == line.ArticleVariantId);
                if (orderLine is not null)
                    orderLine.QuantityReceived -= line.Quantity;
            }
            draft.Status = SupplierReturnDraftStatus.Issued;
            draft.IssuedOn = DateTime.UtcNow;
            draft.Version = Guid.NewGuid();
            order.RefreshProgress();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<SupplierReturnDraftDto>.Ok(MapReturnDraft(draft));
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Concurrent supplier return issuance rejected for {Id}", draftId);
            return OperationResult<SupplierReturnDraftDto>.Fail("Conflitto: bozza di reso o giacenza modificata. Ricaricare il documento.");
        }
    }

    public async Task<OperationResult<SupplierPurchaseOrderDto>> CloseAsync(Guid id, Guid version, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Fail("Ordine fornitore non trovato.");
        if (order.Version != version || order.Status is SupplierPurchaseOrderStatus.Draft
                or SupplierPurchaseOrderStatus.ClosedWithDiscrepancies)
            return Fail("Conflitto: ordine modificato o non chiudibile. Ricaricare l'ordine.");
        if (await db.SupplierDeliveryNotes.AnyAsync(x => x.SupplierPurchaseOrderId == id
                && (x.Status == SupplierDeliveryNoteStatus.Draft || x.Status == SupplierDeliveryNoteStatus.Parsed
                    || x.Status == SupplierDeliveryNoteStatus.Parsing), ct))
            return Fail("Non e possibile chiudere l'ordine mentre esistono DDT fornitore in bozza.");
        var discrepant = order.Lines.Any(x => x.QuantityReceived != x.QuantityOrdered);
        if (discrepant) order.CloseWithDiscrepancies();
        else
        {
            order.Status = SupplierPurchaseOrderStatus.Completed;
            order.Version = Guid.NewGuid();
        }
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<SupplierPurchaseOrderDto>.Ok((await GetAsync(id, ct))!);
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Concurrent supplier purchase order close rejected for {Id}", id);
            return Fail("Conflitto: ordine modificato. Ricaricare l'ordine.");
        }
    }

    private static SupplierPurchaseOrderDto Map(SupplierPurchaseOrder x) => new(
        x.Id, x.OrderNumber, x.SupplierId, x.Supplier.BusinessName, new(x.OrderDate, TimeSpan.Zero),
        x.Status.ToString(), x.Notes, x.Version,
        x.Lines.OrderBy(l => l.CreatedAt).Select(l => new SupplierPurchaseOrderLineDto(
            l.Id, l.ArticleVariantId, l.ArticleVariant.Barcode, ArticleDescription(l.ArticleVariant), l.QuantityOrdered,
            l.QuantityReceived, l.UnitCost)).ToList());

    private static SupplierReturnDraftDto MapReturnDraft(
        SupplierReturnDraft draft, IReadOnlyCollection<SupplierDeliveryNoteLine>? sourceLines = null) =>
        new(draft.Id, draft.SupplierPurchaseOrderId, draft.SupplierDeliveryNoteId,
            new(draft.CreatedOn, TimeSpan.Zero),
            draft.IssuedOn.HasValue ? new(draft.IssuedOn.Value, TimeSpan.Zero) : null,
            draft.Status.ToString(), draft.Version,
            draft.Lines.OrderBy(x => x.CreatedAt).Select(line =>
            {
                var source = sourceLines?.SingleOrDefault(x => x.Id == line.SupplierDeliveryNoteLineId)
                    ?? line.SupplierDeliveryNoteLine;
                return new SupplierReturnDraftLineDto(line.SupplierDeliveryNoteLineId, line.ArticleVariantId,
                    (source?.ArticleVariant ?? line.ArticleVariant)?.Barcode ?? "",
                    ArticleDescription(source?.ArticleVariant ?? line.ArticleVariant), line.Quantity);
            }).ToList());

    private static string ArticleDescription(ProductVariant? variant)
    {
        if (variant is null) return "Variante non disponibile";
        var details = string.Join(" / ", new[] { variant.Color?.Name, variant.Size?.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return details.Length == 0 ? variant.Product.Name : $"{variant.Product.Name} - {details}";
    }

    private static string State(int delta) => delta == 0 ? "InLine" : delta < 0 ? "Shortage" : "Surplus";
    private static bool ValidPrice(decimal value) => value >= 0m && value < 10000000000m
        && decimal.Round(value, 8, MidpointRounding.AwayFromZero) == value;
    private static OperationResult<SupplierPurchaseOrderDto> Fail(string error) =>
        OperationResult<SupplierPurchaseOrderDto>.Fail(error);

    private static bool IsConcurrencyConflict(Exception ex)
    {
        for (Exception? error = ex; error is not null; error = error.InnerException)
            if (error is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" or "40P01" or "23505" })
                return true;
        return false;
    }
}
