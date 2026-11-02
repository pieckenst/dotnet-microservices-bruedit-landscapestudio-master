using Shared.Common.Common;

namespace Procurement.Domain.Entities;

public sealed class Supplier : EntityBase
{
    private Supplier() { }
    public Supplier(Guid id, string code, string name, string contactName, string? email, string? phone,
        string? taxIdentifier, string address, int paymentTermDays, string currencyCode)
    {
        if (id == Guid.Empty) throw new ArgumentException("Supplier id is required.", nameof(id));
        Id = id; Update(code, name, contactName, email, phone, taxIdentifier, address, paymentTermDays, currencyCode);
        IsActive = true;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ContactName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? TaxIdentifier { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public int PaymentTermDays { get; private set; }
    public string CurrencyCode { get; private set; } = "BYN";
    public bool IsActive { get; private set; }

    public void Update(string code, string name, string contactName, string? email, string? phone,
        string? taxIdentifier, string address, int paymentTermDays, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 32) throw new ArgumentException("Supplier code is required and limited to 32 characters.", nameof(code));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Supplier name is required and limited to 200 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(contactName) || contactName.Trim().Length > 120) throw new ArgumentException("Contact name is required and limited to 120 characters.", nameof(contactName));
        if (email is not null && email.Trim().Length > 254) throw new ArgumentOutOfRangeException(nameof(email));
        if (phone is not null && phone.Trim().Length > 40) throw new ArgumentOutOfRangeException(nameof(phone));
        if (taxIdentifier is not null && taxIdentifier.Trim().Length > 80) throw new ArgumentOutOfRangeException(nameof(taxIdentifier));
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length > 500) throw new ArgumentException("Supplier address is required and limited to 500 characters.", nameof(address));
        if (paymentTermDays is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(paymentTermDays));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3 || !currencyCode.All(char.IsLetter))
            throw new ArgumentException("Currency code must contain three letters.", nameof(currencyCode));
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); ContactName = contactName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : taxIdentifier.Trim();
        Address = address.Trim(); PaymentTermDays = paymentTermDays; CurrencyCode = currencyCode.Trim().ToUpperInvariant();
    }
    public void SetActive(bool active) => IsActive = active;
}
