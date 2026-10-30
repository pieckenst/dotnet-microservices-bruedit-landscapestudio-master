using FluentValidation;
using Project.Application.Contracts;
using Project.Application.Features.Estimates;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Persistence;
using Xunit;

namespace Project.Tests;

public sealed class ProjectCatalogIntegrationTests
{
    [Fact]
    public async Task CatalogLinkedEstimateLineItemRequiresActiveMatchingCatalogUnit()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "V1");
        var catalogId = Guid.NewGuid();
        var repository = new FakeEstimateRepository(estimate);
        var catalog = new FakeCatalogItemLookup(new CatalogItemReference(
            catalogId, "Granite paving", "Cut granite paving", Guid.NewGuid(), "m²",
            48.50m, IsActive: true, MeasurementUnitIsActive: true));

        var handler = new AddEstimateLineItemCommandHandler(repository, catalog);
        var result = await handler.Handle(new AddEstimateLineItemCommand(
            estimate.Id, EstimateLineItemType.Material, "Granite paving",
            12m, "m²", 52m, 31m, catalogId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(repository.LineItemWasAdded);
        Assert.Equal(catalogId, estimate.LineItems.Single().CatalogItemId);
        Assert.Equal("m²", estimate.LineItems.Single().UnitSymbol);
    }

    [Fact]
    public async Task CatalogLinkedEstimateLineItemRejectsMismatchedMeasurementUnit()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "V1");
        var catalogId = Guid.NewGuid();
        var repository = new FakeEstimateRepository(estimate);
        var catalog = new FakeCatalogItemLookup(new CatalogItemReference(
            catalogId, "Paving", "Stone paving", Guid.NewGuid(), "m²",
            48.50m, IsActive: true, MeasurementUnitIsActive: true));

        var handler = new AddEstimateLineItemCommandHandler(repository, catalog);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new AddEstimateLineItemCommand(
            estimate.Id, EstimateLineItemType.Material, "Paving",
            12m, "pcs", 52m, null, catalogId), CancellationToken.None));

        Assert.False(repository.LineItemWasAdded);
    }

    [Fact]
    public async Task CatalogLinkedEstimateLineItemRejectsInactiveItemsOrUnits()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "V1");
        var catalogId = Guid.NewGuid();
        var repository = new FakeEstimateRepository(estimate);
        var inactiveItemLookup = new FakeCatalogItemLookup(new CatalogItemReference(
            catalogId, "Paving", "Stone paving", Guid.NewGuid(), "m²",
            48.50m, IsActive: false, MeasurementUnitIsActive: true));
        var handler = new AddEstimateLineItemCommandHandler(repository, inactiveItemLookup);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new AddEstimateLineItemCommand(
            estimate.Id, EstimateLineItemType.Material, "Paving",
            12m, "m²", 52m, null, catalogId), CancellationToken.None));

        Assert.False(repository.LineItemWasAdded);
    }

    private sealed class FakeCatalogItemLookup(CatalogItemReference? item) : ICatalogItemLookup
    {
        public Task<CatalogItemReference?> FindAsync(
            Guid catalogItemId,
            EstimateLineItemType itemType,
            CancellationToken cancellationToken) =>
            Task.FromResult(item?.Id == catalogItemId ? item : null);
    }

    private sealed class FakeEstimateRepository(Estimate estimate) : IEstimateRepository
    {
        public bool LineItemWasAdded { get; private set; }

        public Task<Estimate?> GetByIdWithItemsAsync(Guid id, bool tracking, CancellationToken cancellationToken) =>
            Task.FromResult<Estimate?>(id == estimate.Id ? estimate : null);

        public Task<PagedResult<Estimate>> GetByProjectAsync(
            Guid projectId, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Estimate>(Array.Empty<Estimate>(), page, pageSize, 0));

        public Task AddAsync(Estimate value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveAsync(Estimate value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddLineItemAsync(Estimate value, EstimateLineItem item, CancellationToken cancellationToken)
        {
            estimate.AddLineItem(item);
            LineItemWasAdded = true;
            return Task.CompletedTask;
        }

        public Task RemoveLineItemAsync(Estimate value, Guid lineItemId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
