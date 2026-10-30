using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Account.Infrastructure.Migrations;

[DbContext(typeof(AccountContext))]
[Migration("20261030000000_AddAccountProfiles")]
public sealed class AddAccountProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                LastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                CompanyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AccountProfiles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AccountPreferences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Theme = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "system"),
                Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "en"),
                TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "UTC"),
                EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                SmsNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountPreferences", x => x.Id);
                table.ForeignKey("FK_AccountPreferences_AccountProfiles_Id", x => x.Id, "AccountProfiles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AccountAddresses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Line1 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Line2 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PostalCode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountAddresses", x => x.Id);
                table.ForeignKey("FK_AccountAddresses_AccountProfiles_UserId", x => x.UserId, "AccountProfiles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_AccountProfiles_Email", "AccountProfiles", "Email");
        migrationBuilder.CreateIndex("IX_AccountAddresses_UserId_IsDefault", "AccountAddresses", new[] { "UserId", "IsDefault" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccountAddresses");
        migrationBuilder.DropTable(name: "AccountPreferences");
        migrationBuilder.DropTable(name: "AccountProfiles");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.8");
    }
}
