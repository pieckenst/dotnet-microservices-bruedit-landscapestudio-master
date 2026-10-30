using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Account.Infrastructure.Migrations;

[DbContext(typeof(AccountContext))]
[Migration("20261030160000_AddIntegrationOutbox")]
public sealed class AddIntegrationOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "integration_outbox",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                message_type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                payload = table.Column<string>(type: "text", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                dead_lettered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_integration_outbox", x => x.id));

        migrationBuilder.CreateIndex(
            name: "IX_integration_outbox_dispatch",
            table: "integration_outbox",
            columns: new[] { "published_at_utc", "dead_lettered_at_utc", "next_attempt_at_utc", "lease_expires_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "integration_outbox");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder) =>
        modelBuilder.HasAnnotation("ProductVersion", "8.0.8");
}
