namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a DDT (transport document).
/// </summary>
public class CreateDdtRequest
{
    public Guid ContactId { get; set; }
    public string DdtNumber { get; set; } = "";
    public Guid WarehouseId { get; set; }
}