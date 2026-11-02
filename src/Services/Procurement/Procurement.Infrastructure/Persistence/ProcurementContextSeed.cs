using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;

namespace Procurement.Infrastructure.Persistence;

public sealed class ProcurementContextSeed
{
    private static readonly Guid SupplierGreenId    = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid SupplierStoneId    = Guid.Parse("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid StockItemSoilId    = Guid.Parse("c2000000-0000-0000-0000-000000000001");
    private static readonly Guid StockItemStoneId   = Guid.Parse("c2000000-0000-0000-0000-000000000002");
    private static readonly Guid StockItemMulchId   = Guid.Parse("c2000000-0000-0000-0000-000000000003");
    private static readonly Guid PurchaseOrderId    = Guid.Parse("c3000000-0000-0000-0000-000000000001");
    private static readonly Guid LineItemMulchId    = Guid.Parse("c4000000-0000-0000-0000-000000000001");
    private static readonly Guid LineItemStoneId    = Guid.Parse("c4000000-0000-0000-0000-000000000002");
    private static readonly Guid MovementSoilId     = Guid.Parse("c5000000-0000-0000-0000-000000000001");
    private static readonly Guid MovementMulchId    = Guid.Parse("c5000000-0000-0000-0000-000000000002");
    private static readonly Guid MovementStoneId    = Guid.Parse("c5000000-0000-0000-0000-000000000003");

    public static async Task SeedAsync(
        ProcurementContext context,
        ILogger<ProcurementContextSeed> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (await context.Suppliers.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Procurement seed skipped: suppliers already present.");
            return;
        }

        var greenSupplier = new Supplier(SupplierGreenId, "GN", "Green Nursery Ltd.", "Irina Smirnova",
            "orders@greennursery.example", "+375 17 500 10 20", "UNP 100200300",
            "Minsk, ul. Lesnaya 5", 30, "BYN");
        var stoneSupplier = new Supplier(SupplierStoneId, "ST", "Stone & Sand Co.", "Alexei Morozov",
            "sales@stonesand.example", "+375 17 600 20 30", "UNP 400500600",
            "Minsk district, pos. Kolodischi, industrial zone", 45, "BYN");

        await context.Suppliers.AddRangeAsync([greenSupplier, stoneSupplier], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var soilStock = new StockItem(StockItemSoilId, null, "SOIL-001", "Fertile topsoil, screened", "м³", "Warehouse A - Bay 1", 10m);
        var stoneStock = new StockItem(StockItemStoneId, null, "STONE-001", "Granite cobblestone, grey", "м²", "Warehouse A - Bay 2", 50m);
        var mulchStock = new StockItem(StockItemMulchId, null, "MULCH-001", "Bark mulch, medium grade", "м³", "Warehouse B - Bay 1", 5m);

        await context.StockItems.AddRangeAsync([soilStock, stoneStock, mulchStock], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var soilMovement = new StockMovement(MovementSoilId, StockItemSoilId, StockMovementType.Receipt,
            15m, "INITIAL-SEED", null, "Initial stock receipt at warehouse setup.");
        var mulchMovement = new StockMovement(MovementMulchId, StockItemMulchId, StockMovementType.Receipt,
            8m, "INITIAL-SEED", null, "Initial stock receipt at warehouse setup.");
        var stoneMovement = new StockMovement(MovementStoneId, StockItemStoneId, StockMovementType.Receipt,
            60m, "INITIAL-SEED", null, "Initial stock receipt at warehouse setup.");

        soilStock.ApplyMovement(15m);
        stoneStock.ApplyMovement(60m);
        mulchStock.ApplyMovement(8m);

        await context.StockMovements.AddRangeAsync([soilMovement, mulchMovement, stoneMovement], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var purchaseOrder = new PurchaseOrder(PurchaseOrderId, "PO-2026-001", SupplierStoneId, null,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15),
            "Stone and mulch for Maple Garden project hardscape phase.");

        var mulchLine = new PurchaseOrderLine(LineItemMulchId, PurchaseOrderId, ProcurementItemType.Material,
            null, "Bark mulch, medium grade - 5 m³", "м³", 5m, 35.00m);
        var stoneLine = new PurchaseOrderLine(LineItemStoneId, PurchaseOrderId, ProcurementItemType.Material,
            null, "Granite cobblestone - 40 m²", "м²", 40m, 28.50m);

        purchaseOrder.AddLine(mulchLine);
        purchaseOrder.AddLine(stoneLine);
        purchaseOrder.Submit();
        purchaseOrder.Approve(new DateOnly(2026, 3, 2));

        await context.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Procurement seed completed: 2 suppliers, 3 stock items, 3 movements, 1 approved purchase order with 2 lines.");
    }
}
