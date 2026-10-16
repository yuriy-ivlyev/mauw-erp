namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Referente aziendale (contact person) belonging to a Contact.
/// </summary>
public class ContactPerson : Entity<Guid>
{
    public Guid ContactId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;

    public ContactPerson() : base() { }

    public ContactPerson(
        Guid contactId,
        string firstName,
        string lastName,
        string? role = null,
        string? email = null,
        string? phone = null,
        bool isPrimary = false) : base()
    {
        ContactId = contactId;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        Email = email;
        Phone = phone;
        IsPrimary = isPrimary;
    }
}
