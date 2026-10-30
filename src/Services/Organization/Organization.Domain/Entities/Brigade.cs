using Shared.Common.Common;

namespace Organization.Domain.Entities;

public sealed class Brigade : EntityBase
{
    private Brigade() { }
    public Brigade(Guid id, string code, string name, string specialization, string description, Guid? forepersonEmployeeId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Brigade id is required.", nameof(id));
        Id = id;
        Update(code, name, specialization, description, forepersonEmployeeId);
        IsActive = true;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Specialization { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? ForepersonEmployeeId { get; private set; }
    public bool IsActive { get; private set; }
    public ICollection<BrigadeMember> Members { get; private set; } = new List<BrigadeMember>();

    public void Update(string code, string name, string specialization, string description, Guid? forepersonEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 32) throw new ArgumentException("Brigade code is required and limited to 32 characters.", nameof(code));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new ArgumentException("Brigade name is required and limited to 120 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(specialization) || specialization.Trim().Length > 100) throw new ArgumentException("Specialization is required and limited to 100 characters.", nameof(specialization));
        if (description?.Length > 2000) throw new ArgumentOutOfRangeException(nameof(description));
        if (forepersonEmployeeId == Guid.Empty) throw new ArgumentException("Foreperson id must not be empty.", nameof(forepersonEmployeeId));
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim();
        Specialization = specialization.Trim(); Description = description?.Trim() ?? string.Empty;
        ForepersonEmployeeId = forepersonEmployeeId;
    }
    public void AddMember(BrigadeMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.BrigadeId != Id) throw new ArgumentException("Member belongs to another brigade.", nameof(member));
        if (Members.Any(item => item.EmployeeId == member.EmployeeId && item.LeftDate is null))
            throw new InvalidOperationException("Employee is already an active member of this brigade.");
        Members.Add(member);
    }
    public void RemoveMember(Guid employeeId, DateOnly leaveDate)
    {
        var membership = Members.SingleOrDefault(item => item.EmployeeId == employeeId && item.LeftDate is null)
            ?? throw new KeyNotFoundException("Employee is not an active member of this brigade.");
        membership.Leave(leaveDate);
        if (ForepersonEmployeeId == employeeId) ForepersonEmployeeId = null;
    }
    public void SetActive(bool active) => IsActive = active;
}
