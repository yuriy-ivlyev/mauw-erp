namespace Mauw.Erp.Domain.Exceptions;

/// <summary>
/// Thrown when an optimistic concurrency exception occurs (entity was modified or deleted).
/// </summary>
public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message = "The entity was modified or deleted by another user. Please refresh and try again.")
        : base(message)
    {
    }
}
