using Mauw.Erp.Contracts.Dtos;

namespace Mauw.Erp.Application.Services;

public interface IVatValidationService
{
    /// <summary>
    /// Valida P.IVA / Tax ID:
    ///   IT → algoritmo checksum a 11 cifre.
    ///   UE → regex formato + chiamata VIES REST API.
    ///   Extra-UE → accetta qualsiasi valore alfanumerico.
    /// </summary>
    Task<VatValidationResultDto> ValidateAsync(string countryCode, string vatNumber);
}
