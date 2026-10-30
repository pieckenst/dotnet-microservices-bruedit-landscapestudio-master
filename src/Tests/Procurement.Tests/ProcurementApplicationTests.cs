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