namespace Khala.FakeDomain.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Migrations;

    public partial class InitializeProcessManagerDbContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FakeProcessManagers",
                columns: table => new
                {
                    SequenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FakeProcessManagers", x => x.SequenceId);
                });

            migrationBuilder.CreateTable(
                name: "PendingCommands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    CommandJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessManagerType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingScheduledCommands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    CommandJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessManagerType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduledTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingScheduledCommands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FakeProcessManagers_Id",
                table: "FakeProcessManagers",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingCommands_MessageId",
                table: "PendingCommands",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingCommands_ProcessManagerId",
                table: "PendingCommands",
                column: "ProcessManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledCommands_MessageId",
                table: "PendingScheduledCommands",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledCommands_ProcessManagerId",
                table: "PendingScheduledCommands",
                column: "ProcessManagerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FakeProcessManagers");

            migrationBuilder.DropTable(
                name: "PendingCommands");

            migrationBuilder.DropTable(
                name: "PendingScheduledCommands");
        }
    }
}
