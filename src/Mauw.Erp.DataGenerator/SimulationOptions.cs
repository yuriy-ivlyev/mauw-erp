using Mauw.Erp.Domain.Accounting;

namespace Mauw.Erp.DataGenerator;

public sealed record SimulationOptions
{
    public DateOnly EndDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int Years { get; init; } = 5;
    public int Seed { get; init; } = 42;
    public int Customers { get; init; } = 5000;
    public int ActiveCustomers { get; init; } = 1000;
    public int Suppliers { get; init; } = 500;
    public int Agents { get; init; } = 200;
    public int ActiveAgents { get; init; } = 50;
    public int Products { get; init; } = 20000;
    public int InitialProducts { get; init; } = 4000;
    public int BatchSize { get; init; } = 1000;
    /// <summary>Multiplies the 300-600 weekly received pieces; 4 gives about one million movements in five years.</summary>
    public int WeeklyPiecesFactor { get; init; } = 4;
    /// <summary>Days a received piece stays on the shelf before regular sales can draw on it.</summary>
    public int SalesLagDays { get; init; } = 60;
    /// <summary>Share of the pieces eligible for regular sale that is actually sold.</summary>
    public int SellThroughPercent { get; init; } = 75;
    /// <summary>Share of the residual commercial stock kept on the shelves at year end; the rest is liquidated.</summary>
    public int YearEndCarryoverPercent { get; init; } = 50;

    public AccountingResult<SimulationOptions> Validate()
    {
        if (Years < 1 || Years >= EndDate.Year)
            return Invalid("Years must be positive and the start date must be representable.");
        if (Customers < 10 || Customers % 10 != 0 || ActiveCustomers < 10 ||
            ActiveCustomers % 10 != 0 || ActiveCustomers > Customers)
            return Invalid("Customer counts must be positive multiples of ten; active customers cannot exceed customers.");
        if (Suppliers < 2 || Agents < 1 || ActiveAgents < 1 || ActiveAgents > Agents ||
            ActiveAgents > ActiveCustomers * 3L / 10)
            return Invalid("At least two suppliers and one active agent are required; active agents cannot exceed assigned active customers.");
        if (Products < 5 || Products % 5 != 0 || InitialProducts < 5 ||
            InitialProducts % 5 != 0 || InitialProducts > Products)
            return Invalid("Product and opening model counts must be positive multiples of five; opening models cannot exceed products.");
        if (BatchSize < 1 || BatchSize > 100000)
            return Invalid("BatchSize must be between one and 100000.");
        if (WeeklyPiecesFactor < 1 || WeeklyPiecesFactor > 20)
            return Invalid("WeeklyPiecesFactor must be between one and 20.");
        if (SalesLagDays is < 0 or > 365)
            return Invalid("SalesLagDays must be between zero and 365.");
        if (SellThroughPercent is < 50 or > 95)
            return Invalid("SellThroughPercent must be between 50 and 95.");
        if (YearEndCarryoverPercent is < 0 or > 90)
            return Invalid("YearEndCarryoverPercent must be between zero and 90.");
        return AccountingResult<SimulationOptions>.Ok(this);

        static AccountingResult<SimulationOptions> Invalid(string error) =>
            AccountingResult<SimulationOptions>.Fail(error);
    }
}

public sealed record SimulationSummary(
    DateOnly StartDate,
    DateOnly EndDate,
    int Customers,
    int ActiveCustomers,
    int Suppliers,
    int DominantSuppliers,
    int Agents,
    int ActiveAgents,
    int AssignedCustomers,
    int Products,
    int IntroducedProducts,
    int Variants,
    int Receipts,
    long PurchasedUnits,
    long DominantSupplierUnits,
    long SupplierReturnUnits,
    long DefectiveUnits,
    long RegularSoldUnits,
    long LiquidatedUnits,
    long CommercialStockUnits,
    long DefectiveStockUnits,
    int Orders,
    int Ddts,
    int Invoices,
    long StockMovements,
    bool StockReconciled);
