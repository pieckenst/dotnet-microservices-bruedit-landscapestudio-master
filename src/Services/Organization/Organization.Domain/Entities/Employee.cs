using Organization.Domain.Enums;
using Shared.Common.Common;

namespace Organization.Domain.Entities;

/// <summary>Employment record; login credentials remain solely in Identity.</summary>
public sealed class Employee : EntityBase
{
    private Employee() { }
    public Employee(Guid id, string employeeNumber, string firstName, string lastName,
        Guid positionId, DateOnly hireDate, Guid? identityUserId = null, string? email = null, string? phone = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Employee id is required.", nameof(id));
        Id = id;
        Update(employeeNumber, firstName, lastName, positionId, hireDate, identityUserId, email, phone);
        Status = EmployeeStatus.Active;
    }
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public Guid PositionId { get; private set; }
    public Guid? IdentityUserId { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public DateOnly HireDate { get; private set; }
    public DateOnly? TerminationDate { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public Position Position { get; private set; } = null!;

    public void Update(string employeeNumber, string firstName, string lastName, Guid positionId, DateOnly hireDate,
        Guid? identityUserId, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber) || employeeNumber.Trim().Length > 32)
            throw new ArgumentException("Employee number is required and limited to 32 characters.", nameof(employeeNumber));
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100)
            throw new ArgumentException("First name is required and limited to 100 characters.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100)
            throw new ArgumentException("Last name is required and limited to 100 characters.", nameof(lastName));
        if (positionId == Guid.Empty) throw new ArgumentException("Position id is required.", nameof(positionId));
        if (hireDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentOutOfRangeException(nameof(hireDate));
        if (identityUserId == Guid.Empty) throw new ArgumentException("Optional Identity user id must not be empty.", nameof(identityUserId));
        if (email is not null && email.Trim().Length > 254) throw new ArgumentOutOfRangeException(nameof(email));
        if (phone is not null && phone.Trim().Length > 40) throw new ArgumentOutOfRangeException(nameof(phone));
        EmployeeNumber = employeeNumber.Trim().ToUpperInvariant();
        FirstName = firstName.Trim(); LastName = lastName.Trim();
        PositionId = positionId; HireDate = hireDate; IdentityUserId = identityUserId;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }
    public void ChangeStatus(EmployeeStatus status, DateOnly today)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (Status == EmployeeStatus.Terminated && status != EmployeeStatus.Terminated)
            throw new InvalidOperationException("A terminated employee cannot be reactivated through a status change.");
        if (status == EmployeeStatus.Terminated) TerminationDate ??= today;
        else if (status != EmployeeStatus.Terminated && TerminationDate is not null)
            throw new InvalidOperationException("A terminated employee cannot be reactivated.");
        Status = status;
    }
}
