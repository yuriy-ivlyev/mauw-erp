using Mauw.Erp.Api.Authorization;
using Mauw.Erp.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>Variante vetrina: la variante le cui immagini rappresentano il prodotto nelle liste.</summary>
[ApiController]
[Authorize(Policy = AppPolicies.CatalogManagement)]
[Route("api/v1/products/{productId:guid}/variants/{variantId:guid}/vetrina")]
public sealed class ProductVariantsController(IProductService productService) : ControllerBase
{
    /// <summary>Imposta la variante come vetrina del prodotto (le altre varianti perdono il flag).</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> SetVetrina(Guid productId, Guid variantId) => Apply(productId, variantId, true);

    /// <summary>Rimuove il flag vetrina dalla variante.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ClearVetrina(Guid productId, Guid variantId) => Apply(productId, variantId, false);

    private async Task<IActionResult> Apply(Guid productId, Guid variantId, bool value)
    {
        var result = await productService.SetVarianteVetrinaAsync(productId, variantId, value);
        if (result.Success)
            return NoContent();

        return result.Error is "Product not found" or "Variant not found"
            ? NotFound(new { error = result.Error })
            : BadRequest(new { error = result.Error });
    }
}
