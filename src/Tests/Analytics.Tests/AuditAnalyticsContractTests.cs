using Transaction.API.Data.DTOs.Requests;
using Transaction.API.Entities;
using Transaction.API.Services;
using Xunit;

namespace Analytics.Tests;

public sealed class AuditAnalyticsContractTests
{
    [Fact]
    public void Query_Rejects_Reversed_Utc_Range()
    {
        var query = new AuditEventQuery
        {
            FromUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)
        };

        Assert.Equal("FromUtc cannot be later than ToUtc.", query.Validate());
    }

    [Fact]
    public void Query_Rejects_Unbounded_Page_Size()
    {
        var query = new AuditEventQuery { Page = 1, PageSize = 201 };

        Assert.Equal("PageSize must be between 1 and 200.", query.Validate());
    }

    [Fact]
    public void Csv_Quotes_Fields_And_Neutralizes_Spreadsheet_Formulas()
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
            SourceService = "=HYPERLINK(\"https://example.invalid\")",
            Action = "project,created",
            EntityType = "Projects",
            EntityId = "id,\"quoted\"",
            ActorId = "+SUM(1,1)",
            CorrelationId = "trace-1",
            Metadata = new Dictionary<string, string> { ["should_not_export"] = "private" }
        };

        var csv = AuditCsvExporter.Export([record]);

        Assert.Contains("\"'=HYPERLINK", csv);
        Assert.Contains("\"project,created\"", csv);
        Assert.Contains("id,\"\"quoted\"\"", csv);
        Assert.Contains("\"'+SUM(1,1)\"", csv);
        Assert.DoesNotContain("private", csv);
    }

    [Fact]
    public void Query_Allows_Empty_Filters_With_Default_Paging()
    {
        var query = new AuditEventQuery();

        Assert.Null(query.Validate());
        Assert.Equal(1, query.Page);
        Assert.Equal(50, query.PageSize);
    }
}
