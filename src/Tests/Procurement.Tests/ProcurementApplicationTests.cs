using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Procurement.Application.Contracts;
using Procurement.Application.Features.PurchaseOrders;
using Procurement.Application.Features.Stock;
using Procurement.Application.Features.Suppliers;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Procurement.Infrastructure.Persistence;
using Procurement.Infrastructure.Repositories;
using Shared.Common.Persistence;
using Xunit;

namespace Procurement.Tests;

public sealed class ProcurementApplicationTests
{
    [Fact]
    public void SupplierValidatorRejectsMissingRequiredBusinessFields()
    {
        var validator = new CreateSupplierCommandValidator();
        var result = validator.Validate(new CreateSupplierCommand(
            "", "", "", "invalid-address", null, null, "", -1, "R"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSupplierCommand.Code));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSupplierCommand.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSupplierCommand.CurrencyCode));
    }

    [Fact]
    public void PurchaseOrderValidatorRequiresAtLeastOneValidLine()
    {
        var validator = new CreatePurchaseOrderCommandValidator();
        var result = validator.Validate(new CreatePurchaseOrderCommand(
            "PO-INVALID", Guid.NewGuid(), null, new DateOnly(2026, 10, 1), null, null, []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreatePurchaseOrderCommand.Lines));
    }

    [Fact]
    public async Task StockItemUpdateChangesMetadataWithoutChangingCatalogLinkOrOnHandQuantity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProcurementContext>().UseSqlite(connection).Options;
        await using var context = new ProcurementContext(options);
        await context.Database.EnsureCreatedAsync();

        var catalogItemId = Guid.NewGuid();
        var stock = new StockItem(Guid.NewGuid(), catalogItemId, "SKU-001", "Birch tree",
            "pcs", "Warehouse A", 2m);
        context.StockItems.Add(stock);
        await context.SaveChangesAsync();

        var stockRepository = new RepositoryBase<StockItem>(context);
        var movementRepository = new RepositoryBase<StockMovement>(context);
        var unitOfWork = new EfCoreProcurementUnitOfWork(context);
        var movementHandler = new RecordStockMovementCommandHandler(stockRepository, movementRepository, unitOfWork);
        await movementHandler.Handle(new RecordStockMovementCommand(
            stock.Id, StockMovementType.Receipt, 15m, "PO-001"), CancellationToken.None);

        var updateHandler = new UpdateStockItemCommandHandler(stockRepository);
        var updated = await updateHandler.Handle(new UpdateStockItemCommand(
            stock.Id, "sku-002", "Silver birch", "pcs", "Warehouse B / Rack 04", 5m), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("SKU-002", updated.Sku);
        Assert.Equal("Silver birch", updated.Description);
        Assert.Equal("Warehouse B / Rack 04", updated.StorageLocation);
        Assert.Equal(catalogItemId, updated.CatalogItemId);
        Assert.Equal(15m, updated.QuantityOnHand);
        Assert.Equal(5m, updated.ReorderLevel);
    }

    [Fact]
    public async Task StockItemUpdateRejectsDuplicateSku()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProcurementContext>().UseSqlite(connection).Options;
        await using var context = new ProcurementContext(options);
        await context.Database.EnsureCreatedAsync();
        var first = new StockItem(Guid.NewGuid(), null, "SKU-001", "First item", "pcs", "A", 0m);
        var second = new StockItem(Guid.NewGuid(), null, "SKU-002", "Second item", "pcs", "B", 0m);
        context.StockItems.AddRange(first, second);
        await context.SaveChangesAsync();

        var handler = new UpdateStockItemCommandHandler(new RepositoryBase<StockItem>(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateStockItemCommand(first.Id, "sku-002", "Renamed item", "pcs", "A", 0m),
            CancellationToken.None));
    }

    [Fact]
    public async Task SupplierUpdateCanChangeActiveStateWithoutLosingCommercialDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProcurementContext>().UseSqlite(connection).Options;
        await using var context = new ProcurementContext(options);
        await context.Database.EnsureCreatedAsync();

        var supplier = new Supplier(Guid.NewGuid(), "SUP-001", "Green Supply", "Alex",
            "alex@example.test", null, null, "Main street 1", 30, "BYN");
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        var handler = new UpdateSupplierCommandHandler(new RepositoryBase<Supplier>(context));
        var updated = await handler.Handle(new UpdateSupplierCommand(
            supplier.Id, "SUP-001", "Green Supply Ltd", "Alex", "orders@example.test",
            null, null, "Main street 2", 45, "EUR", IsActive: false), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Green Supply Ltd", updated.Name);
        Assert.Equal("orders@example.test", updated.Email);
        Assert.Equal(45, updated.PaymentTermDays);
        Assert.Equal("EUR", updated.CurrencyCode);
        Assert.False(updated.IsActive);

        // The original supplier instance is still tracked by this context. The update
        // must merge into that instance and persist without an EF duplicate-key tracker error.
        var persisted = await context.Suppliers.AsNoTracking().SingleAsync(x => x.Id == supplier.Id);
        Assert.Equal("Green Supply Ltd", persisted.Name);
        Assert.Equal("orders@example.test", persisted.Email);
        Assert.Equal(45, persisted.PaymentTermDays);
        Assert.Equal("EUR", persisted.CurrencyCode);
        Assert.False(persisted.IsActive);
        Assert.Single(context.ChangeTracker.Entries<Supplier>());
    }

    [Fact]
    public async Task StockMovementAndQuantityAreCommittedTogetherAndInvalidConsumptionRollsBack()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProcurementContext>().UseSqlite(connection).Options;
        await using var context = new ProcurementContext(options);
        await context.Database.EnsureCreatedAsync();

        var stock = new StockItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-001", "Birch tree",
            "pcs", "Warehouse A", 2m);
        context.StockItems.Add(stock);
        await context.SaveChangesAsync();

        var stockRepository = new RepositoryBase<StockItem>(context);
        var movementRepository = new RepositoryBase<StockMovement>(context);
        IProcurementUnitOfWork unitOfWork = new EfCoreProcurementUnitOfWork(context);
        var handler = new RecordStockMovementCommandHandler(stockRepository, movementRepository, unitOfWork);

        var receipt = await handler.Handle(new RecordStockMovementCommand(stock.Id,
            StockMovementType.Receipt, 15m, "PO-001"), CancellationToken.None);
        Assert.Equal(15m, (await stockRepository.GetByIdAsync(stock.Id))!.QuantityOnHand);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RecordStockMovementCommand(stock.Id, StockMovementType.Consumption, 20m, "WORK-001"),
            CancellationToken.None));

        await using var verification = new ProcurementContext(options);
        var persistedStock = await verification.StockItems.AsNoTracking().SingleAsync(x => x.Id == stock.Id);
        var movements = await verification.StockMovements.AsNoTracking()
            .Where(x => x.StockItemId == stock.Id).OrderBy(x => x.CreatedDate).ToListAsync();

        Assert.Equal(15m, persistedStock.QuantityOnHand);
        Assert.Single(movements);
        Assert.Equal(receipt.Id, movements[0].Id);
        Assert.Equal(StockMovementType.Receipt, movements[0].MovementType);
    }
}