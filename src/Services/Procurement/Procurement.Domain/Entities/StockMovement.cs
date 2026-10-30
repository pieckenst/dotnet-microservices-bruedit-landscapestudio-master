using Procurement.Domain.Enums;
using Shared.Common.Common;

namespace Procurement.Domain.Entities;

public sealed class StockMovement : EntityBase
{
    private StockMovement() { }
    public StockMovement(Guid id, Guid stockItemId, StockMovementType movementType, decimal quantity,
        string sourceDocument, Guid? projectId = null, string? notes = null)
    {
        if (id == Guid.Empty || stockItemId == Guid.Empty) throw new ArgumentException("Movement and stock item ids are required.");
        if (projectId == Guid.Empty) throw new ArgumentException("Optional project id must not be empty.", nameof(projectId));
        if (quantity <= 0 || quantity > 1_000_000_000m) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (string.IsNullOrWhiteSpace(sourceDocument) || sourceDocument.Trim().Length > 100)
            throw new ArgumentException("Source document is required and limited to 100 characters.", nameof(sourceDocument));
        Id = id; StockItemId = stockItemId; MovementType = movementType;
        Quantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        SourceDocument = sourceDocument.Trim(); ProjectId = projectId; Notes = notes?.Trim() ?? string.Empty;
        if (Notes.Length > 2000) throw new ArgumentOutOfRangeException(nameof(notes));
    }
    public Guid StockItemId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; } // Always positive; MovementType determines direction.
    public string SourceDocument { get; private set; } = string.Empty;
    public Guid? ProjectId { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public StockItem StockItem { get; private set; } = null!;
}
