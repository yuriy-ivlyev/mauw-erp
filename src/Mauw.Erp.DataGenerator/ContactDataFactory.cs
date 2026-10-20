using System.Globalization;
using System.Text;
using Bogus;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.DataGenerator;

/// <summary>Synthetic, deterministic registry data (VAT, tax code, addresses, contact persons).</summary>
public static class ContactDataFactory
{
    private static readonly string[] Roles =
        ["Amministrazione", "Ufficio acquisti", "Ufficio commerciale", "Responsabile magazzino", "Titolare"];

    private static readonly string[] BirthPlaceCodes = ["H501", "F205", "L219", "G273", "D612", "A944", "F839"];
    private static readonly int[] OddValues =
        [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];

    /// <summary>11-digit partita IVA with valid Luhn check digit, unique for each sequence number.</summary>
    public static string VatNumber(int sequence)
    {
        var body = (1_000_000 + sequence).ToString(CultureInfo.InvariantCulture) + "001";
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var digit = body[i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        return body + ((10 - sum % 10) % 10).ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsValidVatNumber(string vatNumber)
    {
        if (vatNumber.Length != 11 || !vatNumber.All(char.IsAsciiDigit))
            return false;
        var sum = 0;
        for (var i = 0; i < 11; i++)
        {
            var digit = vatNumber[i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        return sum % 10 == 0;
    }

    /// <summary>16-character codice fiscale with valid check character for a synthetic person.</summary>
    public static string PersonTaxCode(Faker faker, string firstName, string lastName)
    {
        var birth = faker.Date.Past(50, new DateTime(2000, 1, 1));
        var female = faker.Random.Bool();
        var code = new StringBuilder()
            .Append(SurnameLetters(lastName))
            .Append(NameLetters(firstName))
            .Append((birth.Year % 100).ToString("D2", CultureInfo.InvariantCulture))
            .Append("ABCDEHLMPRST"[birth.Month - 1])
            .Append((birth.Day + (female ? 40 : 0)).ToString("D2", CultureInfo.InvariantCulture))
            .Append(faker.PickRandom(BirthPlaceCodes))
            .ToString();
        return code + CheckCharacter(code);
    }

    public static void Fill(Contact contact, Faker faker, int sequence, bool person, string? firstName = null,
        string? lastName = null)
    {
        contact.VatNumber = VatNumber(sequence);
        contact.TaxCode = person ? PersonTaxCode(faker, firstName!, lastName!) : contact.VatNumber;
        if (!contact.IsAgent)
            contact.PecEmail = $"amministrazione{sequence}@pec.example.com";
        if (contact.IsCustomer)
            contact.SdiCode = faker.Random.String2(7, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
    }

    public static ContactAddress LegalHeadquarters(Contact contact, Faker faker) => new(
        contact.Id, AddressPurpose.LegalHeadquarters, faker.Address.StreetAddress(),
        faker.Random.Replace("#####"), faker.Address.City(), faker.Address.StateAbbr(),
        phone: faker.Phone.PhoneNumber("+39 0## ######"), email: $"info@{Slug(contact)}.example.com");

    public static IEnumerable<ContactPerson> Persons(Contact contact, Faker faker, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var first = faker.Name.FirstName();
            var last = faker.Name.LastName();
            yield return new ContactPerson(contact.Id, first, last, faker.PickRandom(Roles),
                faker.Internet.Email(first, last, "example.com"),
                faker.Phone.PhoneNumber("+39 3## ### ####"), i == 0);
        }
    }

    private static string Slug(Contact contact) => "sim" + contact.Id.ToString("N")[..8];

    private static string Letters(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Normalize(NormalizationForm.FormD))
            if (char.IsAsciiLetter(c))
                builder.Append(char.ToUpperInvariant(c));
        return builder.ToString();
    }

    private static string Consonants(string letters) => new(letters.Where(c => "AEIOU".IndexOf(c) < 0).ToArray());

    private static string Pad(string consonants, string letters)
    {
        var vowels = new string(letters.Where(c => "AEIOU".IndexOf(c) >= 0).ToArray());
        return (consonants + vowels + "XXX")[..3];
    }

    private static string SurnameLetters(string surname)
    {
        var letters = Letters(surname);
        return Pad(Consonants(letters), letters);
    }

    private static string NameLetters(string name)
    {
        var letters = Letters(name);
        var consonants = Consonants(letters);
        return consonants.Length >= 4 ? $"{consonants[0]}{consonants[2]}{consonants[3]}" : Pad(consonants, letters);
    }

    public static char TaxCodeCheckCharacter(string code) => CheckCharacter(code);

    private static char CheckCharacter(string code)
    {
        var sum = 0;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            var index = char.IsAsciiDigit(c) ? c - '0' : c - 'A';
            sum += i % 2 == 0 ? OddValues[index] : index;
        }
        return (char)('A' + sum % 26);
    }
}
