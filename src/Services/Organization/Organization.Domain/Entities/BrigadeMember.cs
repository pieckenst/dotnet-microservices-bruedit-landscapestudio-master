using Organization.Domain.Enums;
using Shared.Common.Common;

namespace Organization.Domain.Entities;

public sealed class BrigadeMember : EntityBase
{
    private BrigadeMember() { }
    public BrigadeMember(Guid id, Guid brigadeId, Guid employeeId, BrigadeMemberRole role, DateOnly joinedDate)
    {
        if (id == Guid.Empty || brigadeId == Guid.Empty || employeeId == Guid.Empty) throw new ArgumentException("Membership ids are required.");
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (joinedDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentOutOfRangeException(nameof(joinedDate));
        Id = id; BrigadeId = brigadeId; EmployeeId = employeeId; Role = role; JoinedDate = joinedDate;
    }
    public Guid BrigadeId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public BrigadeMemberRole Role { get; private set; }
    public DateOnly JoinedDate { get; private set; }
    public DateOnly? LeftDate { get; private set; }
    public Brigade Brigade { get; private set; } = null!;
    public Employee Employee { get; private set; } = null!;
    public void ChangeRole(BrigadeMemberRole role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (LeftDate is not null) throw new InvalidOperationException("Membership has ended.");
        Role = role;
    }
    public void Leave(DateOnly date)
    {
        if (date < JoinedDate) throw new ArgumentOutOfRangeException(nameof(date));
        if (LeftDate is not null) throw new InvalidOperationException("Membership has already ended.");
        LeftDate = date;
    }
}
