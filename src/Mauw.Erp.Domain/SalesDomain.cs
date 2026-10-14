namespace Mauw.Erp.Domain;

public enum ContactType
{
    Customer,
    Supplier,
    Agent
}

public enum AgentCommissionStatus
{
    Pending,
    Accrued,
    Paid
}

public sealed class Contact
{
    public Guid Id { get; }
    public ContactType Type { get; }
    public string BusinessName { get; }
    public string? VatNumber { get; }
    public string? SdiCode { get; }
    public decimal DefaultDiscountRate { get; }
    public Guid? AgentId { get; private set; }
    public Contact? Agent { get; private set; }
    public decimal DefaultCommissionRate { get; }

    public Contact(
        ContactType type,
        string businessName,
        string? vatNumber = null,
        string? sdiCode = null,
        decimal defaultDiscountRate = 0m,
        Guid? agentId = null,
        decimal defaultCommissionRate = 0m,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new ArgumentException("Business name is required.", nameof(businessName));
        }

        Id = id ?? Guid.NewGuid();
        Type = type;
        BusinessName = businessName;
        VatNumber = vatNumber;
        SdiCode = sdiCode;
        DefaultDiscountRate = defaultDiscountRate;
        DefaultCommissionRate = defaultCommissionRate;
        AgentId = agentId;
    }

    public void SetAgent(Contact? agent)
    {
        if (agent is not null && agent.Type != ContactType.Agent)
        {
            throw new InvalidOperationException("Only contacts of type Agent can be assigned as an agent.");
        }

        Agent = agent;
        AgentId = agent?.Id;
    }
}

public sealed class OrderItem
{
    public Guid Id { get; }
    public string Description { get; }
    public decimal Quantity { get; }
    public decimal UnitPrice { get; }
    public decimal DiscountRate { get; }
    public decimal TaxRate { get; }
    public bool IsExpense { get; }

    public OrderItem(
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountRate = 0m,
        decimal taxRate = 0m,
        bool isExpense = false,
        Guid? id = null)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (unitPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        Id = id ?? Guid.NewGuid();
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountRate = discountRate;
        TaxRate = taxRate;
        IsExpense = isExpense;
    }

    public decimal NetBase => Quantity * UnitPrice * (1m - DiscountRate / 100m);
    public decimal VatAmount => IsExpense ? 0m : NetBase * (TaxRate / 100m);
    public decimal GrossAmount => NetBase + VatAmount;
    public decimal CommissionableBase => IsExpense ? 0m : NetBase;
}

public static class PricingEngine
{
    public static decimal CalculateNetTaxableBase(IEnumerable<OrderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Sum(item => item.CommissionableBase);
    }

    public static decimal CalculateGrossTotal(IEnumerable<OrderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Sum(item => item.GrossAmount);
    }

    public static decimal CalculateVatTotal(IEnumerable<OrderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Sum(item => item.VatAmount);
    }
}

public sealed class Order
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public Guid? AgentId { get; }
    public List<OrderItem> Items { get; } = new();

    public Order(Guid customerId, Guid? agentId = null, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        CustomerId = customerId;
        AgentId = agentId;
    }

    public void AddItem(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Items.Add(item);
    }

    public decimal NetTaxableBase => PricingEngine.CalculateNetTaxableBase(Items);
    public decimal GrossTotal => PricingEngine.CalculateGrossTotal(Items);
    public decimal VatTotal => PricingEngine.CalculateVatTotal(Items);
}

public sealed class StockMovement
{
    public Guid Id { get; }
    public string ProductName { get; }
    public decimal Quantity { get; }
    public DateTimeOffset MovementDate { get; }
    public string Direction { get; }

    public StockMovement(string productName, decimal quantity, DateTimeOffset? movementDate = null, string direction = "Outbound", Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", nameof(productName));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        Id = id ?? Guid.NewGuid();
        ProductName = productName;
        Quantity = quantity;
        MovementDate = movementDate ?? DateTimeOffset.UtcNow;
        Direction = direction;
    }
}

public sealed class DdtDocument
{
    public Guid Id { get; }
    public Guid OrderId { get; }
    public DateTimeOffset IssueDate { get; }
    public List<StockMovement> StockMovements { get; } = new();

    public DdtDocument(Guid orderId, IEnumerable<OrderItem>? items = null, DateTimeOffset? issueDate = null, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        OrderId = orderId;
        IssueDate = issueDate ?? DateTimeOffset.UtcNow;

        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            StockMovements.Add(new StockMovement(item.Description, item.Quantity, IssueDate, "Outbound"));
        }
    }
}

public sealed class Payment
{
    public Guid Id { get; }
    public Guid InvoiceId { get; }
    public decimal Amount { get; }
    public DateTime PaymentDate { get; }

    public Payment(Guid invoiceId, decimal amount, DateTime? paymentDate = null, Guid? id = null)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Id = id ?? Guid.NewGuid();
        InvoiceId = invoiceId;
        Amount = amount;
        PaymentDate = paymentDate ?? DateTime.UtcNow;
    }
}

public sealed class Invoice
{
    public Guid Id { get; }
    public decimal GrossTotal { get; }
    public decimal NetTaxableBase { get; }
    public List<Payment> Payments { get; } = new();
    public List<AgentCommission> AgentCommissions { get; } = new();

    public Invoice(decimal grossTotal, decimal netTaxableBase, Guid? id = null)
    {
        if (grossTotal < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(grossTotal));
        }

        if (netTaxableBase < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(netTaxableBase));
        }

        Id = id ?? Guid.NewGuid();
        GrossTotal = grossTotal;
        NetTaxableBase = netTaxableBase;
    }

    public decimal TotalPaid => Payments.Sum(payment => payment.Amount);
    public bool IsFullyPaid => TotalPaid >= GrossTotal;

    public void RegisterPayment(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        Payments.Add(payment);
    }

    public void AddCommission(AgentCommission commission)
    {
        ArgumentNullException.ThrowIfNull(commission);
        AgentCommissions.Add(commission);
    }
}

public sealed class AgentCommission
{
    public Guid Id { get; }
    public Guid AgentId { get; }
    public Guid InvoiceId { get; }
    public decimal TaxableAmountBase { get; }
    public decimal CommissionRate { get; }
    public decimal CommissionAmount { get; }
    public AgentCommissionStatus Status { get; private set; }

    public AgentCommission(
        Guid agentId,
        Guid invoiceId,
        decimal taxableAmountBase,
        decimal commissionRate,
        Guid? id = null)
    {
        if (taxableAmountBase < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxableAmountBase));
        }

        if (commissionRate < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(commissionRate));
        }

        Id = id ?? Guid.NewGuid();
        AgentId = agentId;
        InvoiceId = invoiceId;
        TaxableAmountBase = taxableAmountBase;
        CommissionRate = commissionRate;
        CommissionAmount = taxableAmountBase * (commissionRate / 100m);
        Status = AgentCommissionStatus.Pending;
    }

    public void MarkAccrued()
    {
        Status = AgentCommissionStatus.Accrued;
    }

    public void MarkPaid()
    {
        Status = AgentCommissionStatus.Paid;
    }
}
