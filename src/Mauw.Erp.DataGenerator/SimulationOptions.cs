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
