using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                    table.CheckConstraint("CK_Plans_Code", "[Code] IN ('FREE', 'PRO_MONTH', 'PRO_QUARTER')");
                    table.CheckConstraint("CK_Plans_Price_Duration", "([Code] = 'FREE' AND [Price] = 0 AND [DurationDays] = 0) OR ([Code] <> 'FREE' AND [Price] > 0 AND [DurationDays] > 0)");
                });

            migrationBuilder.CreateTable(
                name: "PlanLimits",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LimitValue = table.Column<int>(type: "int", nullable: true),
                    Period = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanLimits", x => new { x.PlanId, x.Feature });
                    table.CheckConstraint("CK_PlanLimits_Feature", "[Feature] IN ('FullTest', 'Practice', 'Vocab')");
                    table.CheckConstraint("CK_PlanLimits_Period", "[Period] IN ('Daily', 'Monthly', 'Lifetime')");
                    table.CheckConstraint("CK_PlanLimits_Value", "[LimitValue] IS NULL OR [LimitValue] >= 0");
                    table.ForeignKey(
                        name: "FK_PlanLimits_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanLimits");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
