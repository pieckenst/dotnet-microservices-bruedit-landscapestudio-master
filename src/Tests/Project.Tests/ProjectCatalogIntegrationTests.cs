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
    public async Task CatalogLinkedEstimateLineItemUsesAuthoritativeCatalogSnapshot()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "V1");
        var catalogId = Guid.NewGuid();
        var repository = new FakeEstimateRepository(estimate);
        var catalog = new FakeCatalogItemLookup(new CatalogItemReference(
            catalogId, "Granite paving", "Cut granite paving", Guid.NewGuid(), "m²",
            48.50m, IsActive: true, MeasurementUnitIsActive: true));

        var handler = new AddEstimateLineItemCommandHandler(repository, catalog);
        var result = await handler.Handle(new AddEstimateLineItemCommand(
            estimate.Id, EstimateLineItemType.Material, "Client supplied description",
            12m, "pcs", 999m, 31m, catalogId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(repository.LineItemWasAdded);
        var line = Assert.Single(estimate.LineItems);
        Assert.Equal(catalogId, line.CatalogItemId);
        Assert.Equal("Granite paving", line.Description);
        Assert.Equal("m²", line.UnitSymbol);
        Assert.Equal(48.50m, line.UnitPrice);
        Assert.Equal(31m, line.UnitCost);
        Assert.Equal(582m, line.LineTotal);
    }

    [Fact]
    public async Task CatalogLinkedEstimateLineItemRejectsInactiveItems()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "V1");
        var catalogId = Guid.NewGuid();
        var repository = new FakeEstimateRepository(estimate);
        var catalog = new FakeCatalogItemLookup(new CatalogItemReference(
            catalogId, "Paving", "Stone paving", Guid.NewGuid(), "m²",
            48.50m, IsActive: false, MeasurementUnitIsActive: true));

        var handler = new AddEstimateLineItemCommandHandler(repository, catalog);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new AddEstimateLineItemCommand(
            estimate.Id, EstimateLineItemType.Material, "Paving",
            12m, "m²", 52m, null, catalogId), CancellationToken.None));

        Assert.False(repository.LineItemWasAdded);
    }

    [Fact]
    public async Task CatalogLinkedEstimateLineItemRejectsInactiveMeasurementUnits()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "V1");
        var catalogId = Guid.NewGuid();
        var repository = new FakeEstimateRepository(estimate);
        var catalog = new FakeCatalogItemLookup(new CatalogItemReference(
            catalogId, "Paving", "Stone paving", Guid.NewGuid(), "m²",
            48.50m, IsActive: true, MeasurementUnitIsActive: false));

        var handler = new AddEstimateLineItemCommandHandler(repository, catalog);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new AddEstimateLineItemCommand(
            estimate.Id, EstimateLineItemType.Material, "Paving",
            12m, "m²", 52m, null, catalogId), CancellationToken.None));

        Assert.False(repository.LineItemWasAdded);
    }

    [Fact]
    public async Task CatalogLinkedLineDoesNotRequireClientOwnedDescriptionUnitOrPrice()
    {
        var validator = new AddEstimateLineItemCommandValidator();
        var command = new AddEstimateLineItemCommand(
            Guid.NewGuid(), EstimateLineItemType.Material, string.Empty,
            2m, string.Empty, -1m, null, Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CustomLineStillRequiresDescriptionUnitAndNonNegativePrice()
    {
        var validator = new AddEstimateLineItemCommandValidator();
        var command = new AddEstimateLineItemCommand(
            Guid.NewGuid(), EstimateLineItemType.Other, string.Empty,
            2m, string.Empty, -1m, null, null);

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddEstimateLineItemCommand.Description));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddEstimateLineItemCommand.UnitSymbol));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddEstimateLineItemCommand.UnitPrice));
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
