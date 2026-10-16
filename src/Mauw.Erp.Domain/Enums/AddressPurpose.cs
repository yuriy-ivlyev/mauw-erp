namespace Mauw.Erp.Domain.Enums;

/// <summary>
/// Scopo di un indirizzo/sede associato a un contatto.
/// Sostituisce il vecchio AddressType.
/// </summary>
public enum AddressPurpose
{
    LegalHeadquarters = 1,
    OperationalSite   = 2,
    WarehouseSite     = 3,
    Administrative    = 4
}
