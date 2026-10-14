namespace Mauw.Erp.Domain;

public class ProductVariant
{
    public Guid Id { get; private set; }
    public string Barcode { get; private set; }
    public string Name { get; private set; }
    public int OnHandQuantity { get; private set; }
    public decimal UnitCost { get; private set; }

    public ProductVariant(Guid? id = null, string barcode = "", string name = "", int onHandQuantity = 0, decimal unitCost = 0m)
    {
        Id = id ?? Guid.NewGuid();
        Barcode = barcode;
        Name = name;
        OnHandQuantity = onHandQuantity;
        UnitCost = unitCost;
    }

    public void ApplyStockMovement(int quantity)
    {
        OnHandQuantity += quantity;
    }
}
