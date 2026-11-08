using System.Globalization;
using System.Text;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierDocumentOcrHeaderAlias : Entity<Guid>
{
    public SupplierDocumentOcrColumn Column { get; private set; }
    public string Alias { get; private set; } = "";
    public string NormalizedAlias { get; private set; } = "";
    public int Position { get; private set; }

    public SupplierDocumentOcrHeaderAlias() : base(Guid.NewGuid()) { }

    public SupplierDocumentOcrHeaderAlias(
        SupplierDocumentOcrColumn column,
        string alias,
        int position) : base(Guid.NewGuid())
    {
        SetAlias(alias);
        Column = column;
        Position = position;
    }

    public void SetAlias(string alias)
    {
        Alias = alias.Trim();
        NormalizedAlias = NormalizeAlias(Alias);
        UpdateTimestamp();
    }

    public void SetPosition(int position)
    {
        Position = position;
        UpdateTimestamp();
    }

    public static string NormalizeAlias(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
                normalized.Append(char.ToLowerInvariant(character));
        }

        return normalized.ToString();
    }
}
