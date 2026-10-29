using Project.Domain.Enums;
using Shared.Common.Common;
namespace Project.Domain.Entities;
public sealed class Estimate : EntityBase
{
    private Estimate() { }
    public Estimate(Guid id, Guid projectId, string version, string notes = "")
    {
        if (id == Guid.Empty || projectId == Guid.Empty) throw new ArgumentException("Estimate and project ids are required.");
        Id = id; ProjectId = projectId; Version = ValidateVersion(version); Notes = notes?.Trim() ?? string.Empty;
        Status = EstimateStatus.Draft;
    }
    public Guid ProjectId { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public EstimateStatus Status { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public decimal EstimatedTotal => LineItems.Sum(item => item.LineTotal);
    public decimal? EstimatedCost => LineItems.All(item => item.LineCost is not null)
        ? LineItems.Sum(item => item.LineCost!.Value) : null;
    public ICollection<EstimateLineItem> LineItems { get; private set; } = new List<EstimateLineItem>();
    public void Update(string version, string notes)
    {
        if (Status != EstimateStatus.Draft)
            throw new InvalidOperationException("Only a draft estimate can be edited.");
        Version = ValidateVersion(version); Notes = notes?.Trim() ?? string.Empty;
    }
    public void AddLineItem(EstimateLineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Status != EstimateStatus.Draft) throw new InvalidOperationException("Only a draft estimate can be edited.");
        if (item.EstimateId != Id) throw new ArgumentException("Line item belongs to a different estimate.", nameof(item));
        if (LineItems.Any(existing => existing.Id == item.Id)) throw new InvalidOperationException("Estimate line item already exists.");
        LineItems.Add(item);
    }
    public void RemoveLineItem(Guid id)
    {
        if (Status != EstimateStatus.Draft) throw new InvalidOperationException("Only a draft estimate can be edited.");
        var item = LineItems.SingleOrDefault(line => line.Id == id) ?? throw new KeyNotFoundException("Estimate line item was not found.");
        LineItems.Remove(item);
    }
    public void Submit(DateTime nowUtc)
    {
        if (Status != EstimateStatus.Draft) throw new InvalidOperationException("Only a draft estimate can be submitted.");
        if (LineItems.Count == 0) throw new InvalidOperationException("An estimate must contain at least one line item.");
        Status = EstimateStatus.Submitted; SubmittedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }
    public void Approve(DateTime nowUtc)
    {
        if (Status != EstimateStatus.Submitted) throw new InvalidOperationException("Only a submitted estimate can be approved.");
        Status = EstimateStatus.Approved; ApprovedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }
    public void Reject()
    {
        if (Status != EstimateStatus.Submitted) throw new InvalidOperationException("Only a submitted estimate can be rejected.");
        Status = EstimateStatus.Rejected;
    }
    public void Supersede()
    {
        if (Status != EstimateStatus.Approved) throw new InvalidOperationException("Only an approved estimate can be superseded.");
        Status = EstimateStatus.Superseded;
    }
    private static string ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Estimate version is required.", nameof(version));
        if (version.Trim().Length > 40) throw new ArgumentOutOfRangeException(nameof(version));
        return version.Trim().ToUpperInvariant();
    }
}
