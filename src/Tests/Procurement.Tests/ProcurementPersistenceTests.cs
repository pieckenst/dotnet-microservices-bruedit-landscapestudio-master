using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Procurement.Infrastructure.Persistence;
using Xunit;

namespace Procurement.Tests;

public sealed class ProcurementPersistenceTests
{
    [Fact]
    public async Task PurchaseOrderLinesStockLedgerAndDeliveryPersistTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProcurementContext>().UseSqlite(connection).Options;
        await using var context = new ProcurementContext(options);
        await context.Database.EnsureCreatedAsync();

        var supplier = new Supplier(Guid.NewGuid(), "SUP-001", "Green Supply", "Alex Green",
            "orders@example.test", "+375290000000", null, "Minsk, Belarus", 14, "BYN");
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        var catalogItemId = Guid.NewGuid();
        var stock = new StockItem(Guid.NewGuid(), catalogItemId, "PLANT-BIRCH", "Birch tree",
            "pcs", "Warehouse A", 2m);
        var order = new PurchaseOrder(Guid.NewGuid(), "PO-001", supplier.Id, Guid.NewGuid(),
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        order.AddLine(new PurchaseOrderLine(Guid.NewGuid(), order.Id, ProcurementItemType.Plant,
            catalogItemId, "Birch tree", "pcs", 5m, 12.50m));
        order.Submit();
        order.Approve(new DateOnly(2026, 10, 2));
        order.ReceiveLine(order.Lines.Single().Id, 3m, new DateOnly(2026, 10, 8));
        stock.ApplyMovement(3m);
        var movement = new StockMovement(Guid.NewGuid(), stock.Id, StockMovementType.Receipt,
            3m, order.OrderNumber, order.ProjectId, "Partial receipt");
        var delivery = new SupplierDelivery(Guid.NewGuid(), order.Id, "DEL-001",
            new DateOnly(2026, 10, 15), "Studio Freight", "TRACK-001");
        delivery.ChangeStatus(DeliveryStatus.Dispatched);

        context.PurchaseOrders.Add(order);
        context.StockItems.Add(stock);
        context.StockMovements.Add(movement);
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync();

        await using var verification = new ProcurementContext(options);
        var savedOrder = await verification.PurchaseOrders.AsNoTracking()
            .Include(x => x.Lines).SingleAsync(x => x.Id == order.Id);
        var savedStock = await verification.StockItems.AsNoTracking().SingleAsync(x => x.Id == stock.Id);
        var savedMovement = await verification.StockMovements.AsNoTracking().SingleAsync(x => x.Id == movement.Id);
        var savedDelivery = await verification.Deliveries.AsNoTracking().SingleAsync(x => x.Id == delivery.Id);

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, savedOrder.Status);
        Assert.Equal(62.50m, savedOrder.OrderTotal);
        Assert.Equal(3m, savedOrder.Lines.Single().ReceivedQuantity);
        Assert.Equal(3m, savedStock.QuantityOnHand);
        Assert.Equal(StockMovementType.Receipt, savedMovement.MovementType);
        Assert.Equal(DeliveryStatus.Dispatched, savedDelivery.Status);
        Assert.NotEqual(default, savedOrder.CreatedDate);
    }
}