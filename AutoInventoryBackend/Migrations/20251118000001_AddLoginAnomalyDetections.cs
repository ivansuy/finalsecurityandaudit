using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoInventoryBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAnomalyDetections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginAnomalyDetections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    IsAnomaly = table.Column<bool>(type: "bit", nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    ErrorRate = table.Column<double>(type: "float", nullable: false),
                    AvgSecondsBetweenRequests = table.Column<double>(type: "float", nullable: true),
                    AvgElapsedMs = table.Column<double>(type: "float", nullable: true),
                    P95ElapsedMs = table.Column<double>(type: "float", nullable: true),
                    UniqueUserCount = table.Column<int>(type: "int", nullable: false),
                    LastStatusCode = table.Column<int>(type: "int", nullable: false),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    UnauthorizedCount = table.Column<int>(type: "int", nullable: false),
                    ServerErrorCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAnomalyDetections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAnomalyDetections_WindowStartUtc_IpAddress",
                table: "LoginAnomalyDetections",
                columns: new[] { "WindowStartUtc", "IpAddress" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAnomalyDetections");
        }
    }
}
