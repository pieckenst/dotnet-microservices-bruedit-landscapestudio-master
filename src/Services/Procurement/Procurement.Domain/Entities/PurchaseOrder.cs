using Procurement.Domain.Enums;
using Shared.Common.Common;

namespace Procurement.Domain.Entities;

public sealed class PurchaseOrder : EntityBase
{
    private PurchaseOrder() { }
    public PurchaseOrder(Guid id, string orderNumber, Guid supplierId, Guid? projectId, DateOnly orderDate,
        DateOnly? expectedDeliveryDate, string notes = "")
    {
        if (id == Guid.Empty || supplierId == Guid.Empty) throw new ArgumentException("Order and supplier ids are required.");
        if (projectId == Guid.Empty) throw new ArgumentException("Optional project id must not be empty.", nameof(projectId));
        Id = id; SupplierId = supplierId; ProjectId = projectId;
        Update(orderNumber, orderDate, expectedDeliveryDate, notes);
        Status = PurchaseOrderStatus.Draft;
    }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid? ProjectId { get; private set; } // Project service owns this record.
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDeliveryDate { get; private set; }
    public DateOnly? ApprovedDate { get; private set; }
    public DateOnly? CompletedDate { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public ICollection<PurchaseOrderLine> Lines { get; private set; } = new List<PurchaseOrderLine>();
    public decimal OrderTotal => decimal.Round(Lines.Sum(line => line.LineTotal), 2, MidpointRounding.AwayFromZero);
    public Supplier Supplier { get; private set; } = null!;

    public void Update(string orderNumber, DateOnly orderDate, DateOnly? expectedDeliveryDate, string notes)
    {
        if (Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("Only a draft purchase order can be edited.");
        if (string.IsNullOrWhiteSpace(orderNumber) || orderNumber.Trim().Length > 40) throw new ArgumentException("Order number is required and limited to 40 characters.", nameof(orderNumber));
        if (expectedDeliveryDate is not null && expectedDeliveryDate < orderDate) throw new ArgumentException("Expected delivery cannot precede the order date.");
        if (notes?.Length > 4000) throw new ArgumentOutOfRangeException(nameof(notes));
        OrderNumber = orderNumber.Trim().ToUpperInvariant(); OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate; Notes = notes?.Trim() ?? string.Empty;
    }
    public void AddLine(PurchaseOrderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("Only a draft purchase order can be edited.");
        if (line.PurchaseOrderId != Id) throw new ArgumentException("Line belongs to another purchase order.", nameof(line));
        if (Lines.Any(item => item.Id == line.Id)) throw new InvalidOperationException("Purchase-order line already exists.");
        Lines.Add(line);
    }
    public void RemoveLine(Guid lineId)
    {
        if (Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("Only a draft purchase order can be edited.");
        var line = Lines.SingleOrDefault(item => item.Id == lineId) ?? throw new KeyNotFoundException("Purchase-order line was not found.");
        Lines.Remove(line);
    }
    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("Only a draft purchase order can be submitted.");
        if (Lines.Count == 0) throw new InvalidOperationException("A purchase order must contain at least one line.");
        Status = PurchaseOrderStatus.Submitted;
    }
    public void Approve(DateOnly approvalDate)
    {
        if (Status != PurchaseOrderStatus.Submitted) throw new InvalidOperationException("Only a submitted purchase order can be approved.");
        if (approvalDate < OrderDate) throw new ArgumentOutOfRangeException(nameof(approvalDate));
        Status = PurchaseOrderStatus.Approved; ApprovedDate = approvalDate;
    }
    public PurchaseOrderLine ReceiveLine(Guid lineId, decimal quantity, DateOnly receivedDate)
    {
        if (Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            throw new InvalidOperationException("Only an approved purchase order can receive goods.");
        var line = Lines.SingleOrDefault(item => item.Id == lineId) ?? throw new KeyNotFoundException("Purchase-order line was not found.");
        line.Receive(quantity);
        var allReceived = Lines.All(item => item.ReceivedQuantity >= item.Quantity);
        Status = allReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        if (allReceived) CompletedDate = receivedDate;
        return line;
    }
    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException("Received or partially received purchase orders cannot be cancelled.");
        Status = PurchaseOrderStatus.Cancelled;
    }
}
