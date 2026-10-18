namespace Mauw.Erp.Contracts.Requests;

/// <summary>PNG bytes encoded as base64 in JSON, at most 2 MiB and 4096 pixels per side.</summary>
public sealed record UpdateCompanyLogoRequest(byte[] PngData);
