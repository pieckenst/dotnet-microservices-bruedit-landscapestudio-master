using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Xunit;

namespace Procurement.Tests;

public sealed class ProcurementDomainTests
{
    [Fact]
    public void PurchaseOrderEnforcesLifecycleAndTracksPartialReceipts()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), "po-001", Guid.NewGuid(), null,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        Assert.Throws<InvalidOperationException>(() => order.Submit());

        var plants = new PurchaseOrderLine(Guid.NewGuid(), order.Id, ProcurementItemType.Plant,
            Guid.NewGuid(), "Birch tree", "pcs", 10m, 12.50m);
        var soil = new PurchaseOrderLine(Guid.NewGuid(), order.Id, ProcurementItemType.Material,
            null, "Topsoil", "m3", 4m, 20m);
        order.AddLine(plants);
        order.AddLine(soil);
        order.Submit();
        order.Approve(new DateOnly(2026, 10, 2));

        order.ReceiveLine(plants.Id, 4m, new DateOnly(2026, 10, 8));
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(4m, plants.ReceivedQuantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => order.ReceiveLine(plants.Id, 7m, new DateOnly(2026, 10, 8)));

        order.ReceiveLine(plants.Id, 6m, new DateOnly(2026, 10, 9));
        order.ReceiveLine(soil.Id, 4m, new DateOnly(2026, 10, 9));
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(new DateOnly(2026, 10, 9), order.CompletedDate);
        Assert.Equal(205m, order.OrderTotal);
        Assert.Throws<InvalidOperationException>(order.Cancel);
    }

    [Fact]
    public void StockQuantityCannotBecomeNegativeAndMovementsUsePositiveQuantities()
    {
        var stock = new StockItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-001", "Decorative gravel",
            "kg", "Warehouse A", 10m);
        Assert.Throws<InvalidOperationException>(() => stock.ApplyMovement(-0.001m));
        stock.ApplyMovement(15.250m);
        stock.ApplyMovement(-2.125m);
        Assert.Equal(13.125m, stock.QuantityOnHand);
        Assert.False(stock.IsBelowReorderLevel);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockMovement(
            Guid.NewGuid(), stock.Id, StockMovementType.Receipt, 0m, "PO-001"));
    }

    [Fact]
    public void DeliveryCanArriveEarlyAndTerminalStatesCannotBeReopened()
    {
        var delivery = new SupplierDelivery(Guid.NewGuid(), Guid.NewGuid(), "del-001",
            new DateOnly(2026, 11, 10), "Studio Freight", "TRACK-1");
        delivery.ChangeStatus(DeliveryStatus.Dispatched);
        delivery.ChangeStatus(DeliveryStatus.Delivered, new DateOnly(2026, 11, 8));

        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(new DateOnly(2026, 11, 8), delivery.ActualDate);
        Assert.Throws<InvalidOperationException>(() => delivery.ChangeStatus(DeliveryStatus.Delayed));
        Assert.Throws<ArgumentException>(() => new SupplierDelivery(Guid.NewGuid(), Guid.NewGuid(),
            "del-002", new DateOnly(2026, 11, 10), "Carrier", "").ChangeStatus(DeliveryStatus.Delivered));
    }
}
