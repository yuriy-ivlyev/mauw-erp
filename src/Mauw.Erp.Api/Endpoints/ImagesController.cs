using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>Proxy autenticato e upload delle immagini di prodotti e varianti (storage S3).</summary>
[ApiController]
[Authorize]
[Route("api/images")]
public sealed class ImagesController(
    IImageStorageService storage,
    IImageApplicationService imageService) : ControllerBase
{
    /// <summary>Restituisce l'immagine tramite proxy (nessun presigned URL).</summary>
    [HttpGet("{targetId:guid}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid targetId, string fileName, CancellationToken ct)
    {
        if (!ImageStorageKeys.IsValidFileName(fileName))
            return NotFound();

        try
        {
            var (stream, contentType) = await storage.GetImageStreamAsync(
                ImageStorageKeys.Build(targetId, fileName), ct);

            Response.Headers.CacheControl = "public, max-age=86400";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Carica un'immagine. Senza categoria: la prima foto diventa Main (SortOrder 0),
    /// le successive Zoom1 con SortOrder = massimo + 1.
    /// </summary>
    [HttpPost("{targetId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageApplicationService.MaxFileBytes + 1024 * 1024)]
    [ProducesResponseType<UploadedImageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadImage(
        Guid targetId, IFormFile file, [FromForm] ImageCategory? category, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        var result = await imageService.UploadAsync(
            targetId, new ImageUpload(content, file.Length, file.ContentType), category, ct);

        return result.Success
            ? Created(result.Data!.Url, result.Data)
            : result.Error switch
            {
                ImageErrors.TargetNotFound => NotFound(new { error = result.Error }),
                ImageErrors.TargetInconsistent => UnprocessableEntity(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
    }
}
