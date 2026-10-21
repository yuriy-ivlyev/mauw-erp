using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Contracts.Dtos;

public record ImageDto(Guid Id, Guid TargetId, string Url, ImageCategory Category, int SortOrder);

public record SkuResolutionDto(Guid TargetId, ImageTargetType TargetType);
