namespace Mauw.Erp.Domain.Exceptions;

/// <summary>
/// Base class for domain exceptions.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
