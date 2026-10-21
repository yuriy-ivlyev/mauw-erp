using System.Globalization;

namespace Mauw.Erp.DataGenerator;

/// <summary>Synthetic EAN-13 codes in the GS1 restricted-circulation range (prefix 29), never real products.</summary>
public static class BarcodeFactory
{
    private const long MaxSequence = 9_999_999_999;

    public static string Ean13(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, MaxSequence);
        var body = "29" + sequence.ToString("D10", CultureInfo.InvariantCulture);
        return body + CheckDigit(body).ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsValidEan13(string code) =>
        code.Length == 13 && code.All(char.IsAsciiDigit) && CheckDigit(code[..12]) == code[12] - '0';

    private static int CheckDigit(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
            sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }
}
