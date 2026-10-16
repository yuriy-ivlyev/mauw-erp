namespace Mauw.Erp.Contracts.Dtos;

public record CountryDto(
    string IsoCode,
    string Name,
    bool IsEuMember,
    int DisplayOrder
);
