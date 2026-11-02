using Shared.Common.Common;

namespace Organization.Domain.Entities;

public sealed class Position : EntityBase
{
    private Position() { }
    public Position(Guid id, string code, string title, string description, int rank)
    {
        if (id == Guid.Empty) throw new ArgumentException("Position id is required.", nameof(id));
        Id = id;
        Update(code, title, description, rank);
        IsActive = true;
    }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Rank { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string code, string title, string description, int rank)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 32) throw new ArgumentException("Position code is required and limited to 32 characters.", nameof(code));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120) throw new ArgumentException("Position title is required and limited to 120 characters.", nameof(title));
        if (description?.Length > 1000) throw new ArgumentOutOfRangeException(nameof(description));
        if (rank < 1 || rank > 1000) throw new ArgumentOutOfRangeException(nameof(rank));
        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Rank = rank;
    }
    public void SetActive(bool active) => IsActive = active;
}
