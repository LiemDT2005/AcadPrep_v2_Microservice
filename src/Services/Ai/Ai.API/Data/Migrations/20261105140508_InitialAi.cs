using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ai.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiChatRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    ReplyMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatRequests", x => x.Id);
                    table.CheckConstraint("CK_AiChatRequests_Status", "[Status] IN ('Processing', 'Completed', 'Rejected', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "AiChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiTokenLimits",
                columns: table => new
                {
                    PlanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenLimit = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTokenLimits", x => x.PlanCode);
                    table.CheckConstraint("CK_AiTokenLimits_PlanCode", "[PlanCode] IN ('FREE', 'PRO_MONTH', 'PRO_QUARTER')");
                    table.CheckConstraint("CK_AiTokenLimits_Range", "[TokenLimit] BETWEEN 0 AND 100000000");
                });

            migrationBuilder.CreateTable(
                name: "AiUsageAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OldValue = table.Column<long>(type: "bigint", nullable: true),
                    NewValue = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageAudits", x => x.Id);
                    table.CheckConstraint("CK_AiUsageAudits_Action", "[Action] IN ('ResetUsage', 'ChangeLimit')");
                });

            migrationBuilder.CreateTable(
                name: "AiUsageWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TokensUsed = table.Column<long>(type: "bigint", nullable: false),
                    Locked = table.Column<bool>(type: "bit", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageWindows", x => x.Id);
                    table.CheckConstraint("CK_AiUsageWindows_Range", "[WindowEnd] >= [WindowStart]");
                });

            migrationBuilder.CreateTable(
                name: "AiChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    IsOutOfScope = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatMessages", x => x.Id);
                    table.CheckConstraint("CK_AiChatMessages_Role", "[Role] IN ('User', 'Model')");
                    table.ForeignKey(
                        name: "FK_AiChatMessages_AiChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatMessages_SessionId_CreatedAt",
                table: "AiChatMessages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatRequests_UserId_RequestId",
                table: "AiChatRequests",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiChatRequests_UserId_Status_CompletedAt",
                table: "AiChatRequests",
                columns: new[] { "UserId", "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_AiChatRequests_UserId_Processing",
                table: "AiChatRequests",
                column: "UserId",
                unique: true,
                filter: "[Status] = 'Processing'");

            migrationBuilder.CreateIndex(
                name: "IX_AiChatSessions_UserId_IsDeleted_UpdatedAt",
                table: "AiChatSessions",
                columns: new[] { "UserId", "IsDeleted", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageAudits_UserId_CreatedAt",
                table: "AiUsageAudits",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageWindows_UserId_WindowEnd",
                table: "AiUsageWindows",
                columns: new[] { "UserId", "WindowEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageWindows_WindowEnd_TokensUsed",
                table: "AiUsageWindows",
                columns: new[] { "WindowEnd", "TokensUsed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiChatMessages");

            migrationBuilder.DropTable(
                name: "AiChatRequests");

            migrationBuilder.DropTable(
                name: "AiTokenLimits");

            migrationBuilder.DropTable(
                name: "AiUsageAudits");

            migrationBuilder.DropTable(
                name: "AiUsageWindows");

            migrationBuilder.DropTable(
                name: "AiChatSessions");
        }
    }
}
