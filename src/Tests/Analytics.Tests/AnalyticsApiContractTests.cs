using Microsoft.AspNetCore.Mvc;
using Transaction.API.Controllers;
using Transaction.API.Data.DTOs.Requests;
using Transaction.API.Data.DTOs.Responses;
using Transaction.API.Entities;
using Transaction.API.Repositories.Interfaces;
using Xunit;

namespace Analytics.Tests;

public sealed class AnalyticsApiContractTests
{
    [Fact]
    public async Task Summary_Returns_The_Requested_Window_And_Aggregates()
    {
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
        var repository = new StubAuditEventRepository
        {
            TotalCount = 12,
            ByService = [("Project", 7), ("Procurement", 5)],
            ByAction = [("Created", 8), ("Approved", 4)]
        };

        var result = await new AnalyticsController(repository).GetSummary(from, to, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<AnalyticsSummaryDto>(response.Value);
        Assert.Equal(from, summary.FromUtc);
        Assert.Equal(to, summary.ToUtc);
        Assert.Equal(12, summary.TotalEvents);
        Assert.Equal(2, summary.ByService.Count);
        Assert.Equal(2, summary.ByAction.Count);
        Assert.Equal(1, repository.CountCalls);
    }

    [Fact]
    public async Task Summary_Rejects_Reversed_Range_Without_Querying_Repository()
    {
        var repository = new StubAuditEventRepository();
        var result = await new AnalyticsController(repository).GetSummary(
            new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.CountCalls);
        Assert.Equal(0, repository.QueryCalls);
    }

    [Fact]
    public async Task Audit_Search_Returns_Paged_Contract_And_Uses_Query()
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            SourceService = "Project",
            Action = "ProjectCreated",
            EntityType = "Project",
            EntityId = Guid.NewGuid().ToString("D")
        };
        var repository = new StubAuditEventRepository
        {
            QueryResult = ([record], 1)
        };
        var query = new AuditEventQuery { Page = 1, PageSize = 25, SourceService = "Project" };

        var result = await new AuditEventsController(repository).Search(query, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedAuditEventsResponse>(response.Value);
        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, repository.QueryCalls);
        Assert.Same(query, repository.LastQuery);
    }

    [Fact]
    public async Task Audit_Search_Rejects_Invalid_Page_Size_Before_Repository_Access()
    {
        var repository = new StubAuditEventRepository();
        var result = await new AuditEventsController(repository).Search(
            new AuditEventQuery { PageSize = 500 },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.QueryCalls);
    }

    private sealed class StubAuditEventRepository : IAuditEventRepository
    {
        public long TotalCount { get; init; }
        public IReadOnlyList<(string Name, long Count)> ByService { get; init; } = [];
        public IReadOnlyList<(string Name, long Count)> ByAction { get; init; } = [];
        public (IReadOnlyList<AuditRecord> Items, long TotalCount) QueryResult { get; init; } = ([], 0);
        public int CountCalls { get; private set; }
        public int QueryCalls { get; private set; }
        public AuditEventQuery? LastQuery { get; private set; }

        public Task AddAsync(AuditRecord record, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(IReadOnlyList<AuditRecord> Items, long TotalCount)> QueryAsync(
            AuditEventQuery query, CancellationToken cancellationToken = default)
        {
            QueryCalls++;
            LastQuery = query;
            return Task.FromResult(QueryResult);
        }

        public Task<IReadOnlyList<AuditRecord>> GetForExportAsync(
            AuditEventQuery query, int maximumRows, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditRecord>>([]);

        public Task<IReadOnlyList<AuditRecord>> GetSnapshotAsync(
            int maximumRows, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditRecord>>([]);

        public Task<int> ImportMissingAsync(
            IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.Count);

        public Task<IReadOnlyList<(string Name, long Count)>> CountByServiceAsync(
            DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByService);

        public Task<IReadOnlyList<(string Name, long Count)>> CountByActionAsync(
            DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByAction);

        public Task<long> CountAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            CountCalls++;
            return Task.FromResult(TotalCount);
        }
    }
}
