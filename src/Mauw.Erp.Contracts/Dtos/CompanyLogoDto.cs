namespace Mauw.Erp.Contracts.Dtos;

/// <summary>Company logo normalized to PNG for both UI and PDF rendering.</summary>
public sealed record CompanyLogoDto(byte[] PngData);
