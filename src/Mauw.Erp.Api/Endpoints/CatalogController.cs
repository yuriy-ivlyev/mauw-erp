using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>Helper del catalogo per l'app mobile.</summary>
[ApiController]
[Authorize(Policy = AppPolicies.AnyRole)]
[Route("api/catalog")]
public sealed class CatalogController(IImageApplicationService imageService) : ControllerBase
{
    /// <summary>
    /// Risolve uno SKU nel TargetId: SKU di variante (barcode) oppure SKU del prodotto (codice interno).
    /// Lo SKU di un prodotto con varianti e' un errore di inconsistenza (422): servono le varianti.
    /// </summary>
    [HttpGet("resolve-sku/{sku}")]
    [ProducesResponseType<SkuResolutionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResolveSku(string sku, CancellationToken ct)
    {
        var result = await imageService.ResolveSkuAsync(sku, ct);

        if (result.Success)
            return Ok(result.Data);

        return result.Error switch
        {
            ImageErrors.SkuAmbiguous => Conflict(new { error = result.Error }),
            ImageErrors.SkuInconsistent => UnprocessableEntity(new { error = result.Error }),
            _ => NotFound(new { error = result.Error })
        };
    }
}
