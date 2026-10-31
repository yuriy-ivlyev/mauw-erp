namespace Mauw.Erp.Domain.Enums;

/// <summary>Transmission state of an invoice towards the Sistema di Interscambio.</summary>
public enum SdiStatus
{
    NotTransmitted = 0,
    Pending = 1,
    Transmitted = 2,
    Accepted = 3
}
