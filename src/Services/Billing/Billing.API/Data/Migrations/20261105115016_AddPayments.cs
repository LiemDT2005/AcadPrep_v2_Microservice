using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentUrl",
                table: "Orders",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RawQuery = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSignatureValid = table.Column<bool>(type: "bit", nullable: false),
                    RspCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLogs", x => x.Id);
                    table.CheckConstraint("CK_PaymentLogs_Type", "[Type] IN ('Create', 'Return', 'Ipn')");
                });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_UserId_PlanId_Pending",
                table: "Orders",
                columns: new[] { "UserId", "PlanId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_OrderCode_CreatedAt",
                table: "PaymentLogs",
                columns: new[] { "OrderCode", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentLogs");

            migrationBuilder.DropIndex(
                name: "UX_Orders_UserId_PlanId_Pending",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentUrl",
                table: "Orders");
        }
    }
}
