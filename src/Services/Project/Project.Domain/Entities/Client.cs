using Shared.Common.Common;
namespace Project.Domain.Entities;
/// <summary>CRM client record, independent of Identity credentials and Account profiles.</summary>
public sealed class Client : EntityBase
{
    private Client() { }
    public Client(Guid id, string displayName, string? email, string? phone, string? billingAddress)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id; Update(displayName, email, phone, billingAddress);
    }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? BillingAddress { get; private set; }
    public bool IsArchived { get; private set; }
    public void Update(string displayName, string? email, string? phone, string? billingAddress)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Client name is required.", nameof(displayName));
        if (displayName.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(displayName));
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@')) throw new ArgumentException("Email address is invalid.", nameof(email));
        DisplayName = displayName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        BillingAddress = string.IsNullOrWhiteSpace(billingAddress) ? null : billingAddress.Trim();
    }
    public void Archive() => IsArchived = true;
}
