using System.ComponentModel.DataAnnotations;

namespace LandscapeStudio.Web.Models;

public sealed class OperationFormViewModel
{
    public string Resource { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [StringLength(32)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public Guid PositionId { get; set; }

    public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public Guid? IdentityUserId { get; set; }

    public IReadOnlyList<PositionOption> Positions { get; set; } = Array.Empty<PositionOption>();

    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? LatinName { get; set; }

    [StringLength(64)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal AreaSquareMetres { get; set; }

    [StringLength(100)]
    public string? SoilType { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public Guid MeasurementUnitId { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal UnitPrice { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(32)]
    public string Code { get; set; } = string.Empty;

    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Phone, StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(80)]
    public string? TaxIdentifier { get; set; }

    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Range(0, 365)]
    public int PaymentTermDays { get; set; } = 30;

    [StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "BYN";

    public Guid? CatalogItemId { get; set; }

    [StringLength(24)]
    public string UnitSymbol { get; set; } = string.Empty;

    [StringLength(100)]
    public string StorageLocation { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal ReorderLevel { get; set; }

    public IReadOnlyList<MeasurementUnitOption> MeasurementUnits { get; set; } = Array.Empty<MeasurementUnitOption>();
    public string? LookupError { get; set; }
}

public sealed record MeasurementUnitOption(Guid Id, string Name, string Symbol, bool IsActive);

public sealed record PositionOption(Guid Id, string Code, string Title, string Description, int Rank, bool IsActive, DateTime CreatedDate);
