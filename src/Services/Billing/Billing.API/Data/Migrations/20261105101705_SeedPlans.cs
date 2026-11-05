using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.API.Data.Migrations
{
    /// <summary>
    /// Plan catalog approved by Huy on 2026-11-05: FREE (3 FullTest/month, 5 Practice/day, 100 saved words),
    /// PRO_MONTH 79,000 VND / 30 days and PRO_QUARTER 199,000 VND / 90 days, Pro unlimited (explicit null rows).
    /// Idempotent by Code/Feature: existing rows are never duplicated or overwritten.
    /// </summary>
    public partial class SeedPlans : Migration
    {
        internal const string SeedSql = """
            INSERT INTO [Plans] ([Id], [Code], [Name], [Description], [Price], [DurationDays], [IsActive], [SortOrder])
            SELECT v.[Id], v.[Code], v.[Name], NULL, v.[Price], v.[DurationDays], 1, v.[SortOrder]
            FROM (VALUES
                (CAST('5f1c0d2e-0000-4000-8000-000000000001' AS uniqueidentifier), N'FREE', N'Miễn phí', 0, 0, 0),
                (CAST('5f1c0d2e-0000-4000-8000-000000000002' AS uniqueidentifier), N'PRO_MONTH', N'Pro 1 tháng', 79000, 30, 1),
                (CAST('5f1c0d2e-0000-4000-8000-000000000003' AS uniqueidentifier), N'PRO_QUARTER', N'Pro 3 tháng', 199000, 90, 2)
            ) AS v([Id], [Code], [Name], [Price], [DurationDays], [SortOrder])
            WHERE NOT EXISTS (SELECT 1 FROM [Plans] p WHERE p.[Code] = v.[Code]);

            INSERT INTO [PlanLimits] ([PlanId], [Feature], [LimitValue], [Period])
            SELECT p.[Id], v.[Feature], v.[LimitValue], v.[Period]
            FROM (VALUES
                (N'FREE', N'FullTest', 3, N'Monthly'),
                (N'FREE', N'Practice', 5, N'Daily'),
                (N'FREE', N'Vocab', 100, N'Lifetime'),
                (N'PRO_MONTH', N'FullTest', NULL, N'Monthly'),
                (N'PRO_MONTH', N'Practice', NULL, N'Daily'),
                (N'PRO_MONTH', N'Vocab', NULL, N'Lifetime'),
                (N'PRO_QUARTER', N'FullTest', NULL, N'Monthly'),
                (N'PRO_QUARTER', N'Practice', NULL, N'Daily'),
                (N'PRO_QUARTER', N'Vocab', NULL, N'Lifetime')
            ) AS v([Code], [Feature], [LimitValue], [Period])
            JOIN [Plans] p ON p.[Code] = v.[Code]
            WHERE NOT EXISTS (SELECT 1 FROM [PlanLimits] l WHERE l.[PlanId] = p.[Id] AND l.[Feature] = v.[Feature]);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(SeedSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove only seeded plans that nothing references (never delete purchase/grant history).
            migrationBuilder.Sql("""
                DELETE l FROM [PlanLimits] l JOIN [Plans] p ON p.[Id] = l.[PlanId]
                WHERE p.[Code] IN (N'FREE', N'PRO_MONTH', N'PRO_QUARTER')
                  AND NOT EXISTS (SELECT 1 FROM [Subscriptions] s WHERE s.[PlanId] = p.[Id])
                  AND NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[PlanId] = p.[Id])
                  AND NOT EXISTS (SELECT 1 FROM [SubscriptionGrants] g WHERE g.[PlanId] = p.[Id]);
                DELETE p FROM [Plans] p
                WHERE p.[Code] IN (N'FREE', N'PRO_MONTH', N'PRO_QUARTER')
                  AND NOT EXISTS (SELECT 1 FROM [PlanLimits] l WHERE l.[PlanId] = p.[Id]);
                """);
        }
    }
}
