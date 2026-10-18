namespace Mauw.Erp.Domain.Enums;

/// <summary>
/// Distinguishes the origin/channel of an order.
/// Stored as int in DB; add new values without schema changes.
/// </summary>
public enum OrderType
{
    Standard = 0,      // ORD-mmyyyy-nnn — ordine vendita classico
    CassaSession = 1,  // ORD-mmyyyy-nnn — sessione POS (stesso pool ORD-)
    Preventivo = 2,    // PRV-mmyyyy-nnn — offerta / preventivo
    WebOrder = 3,      // WEB-mmyyyy-nnn — ordine e-commerce
}
