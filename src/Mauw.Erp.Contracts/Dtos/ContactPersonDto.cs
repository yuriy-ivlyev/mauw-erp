namespace Mauw.Erp.Contracts.Dtos;

public record ContactPersonDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Role,
    string? Email,
    string? Phone,
    bool IsPrimary
);
