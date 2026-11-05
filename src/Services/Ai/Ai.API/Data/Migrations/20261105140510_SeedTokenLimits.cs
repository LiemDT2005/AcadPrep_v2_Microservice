using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ai.API.Data.Migrations
{
    /// <summary>
    /// Token limits per rolling 24h window approved by Huy on 2026-11-05: FREE 15,000; PRO_MONTH 60,000;
    /// PRO_QUARTER 60,000. Admin can change them later (UC-22.2). Idempotent: existing rows are never overwritten.
    /// </summary>
    public partial class SeedTokenLimits : Migration
    {
        internal const string SeedSql = """
            INSERT INTO [AiTokenLimits] ([PlanCode], [TokenLimit], [UpdatedBy], [UpdatedAt])
            SELECT v.[PlanCode], v.[TokenLimit], NULL, SYSUTCDATETIME()
            FROM (VALUES (N'FREE', CAST(15000 AS bigint)), (N'PRO_MONTH', CAST(60000 AS bigint)), (N'PRO_QUARTER', CAST(60000 AS bigint)))
                AS v([PlanCode], [TokenLimit])
            WHERE NOT EXISTS (SELECT 1 FROM [AiTokenLimits] l WHERE l.[PlanCode] = v.[PlanCode]);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(SeedSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("DELETE FROM [AiTokenLimits] WHERE [UpdatedBy] IS NULL AND [PlanCode] IN (N'FREE', N'PRO_MONTH', N'PRO_QUARTER');");
    }
}
