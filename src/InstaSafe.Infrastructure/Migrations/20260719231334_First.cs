using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class First : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AlatPayTransactionId",
                table: "WebhookEventLogs",
                newName: "TransactionReference");

            migrationBuilder.RenameIndex(
                name: "IX_WebhookEventLogs_AlatPayTransactionId",
                table: "WebhookEventLogs",
                newName: "IX_WebhookEventLogs_TransactionReference");

            migrationBuilder.RenameColumn(
                name: "AlatPayPayoutReference",
                table: "PayoutSplits",
                newName: "MonnifyTransferReference");

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Bvn",
                table: "Merchants",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Merchants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalFirstName",
                table: "Merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalLastName",
                table: "Merchants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nin",
                table: "Merchants",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatbotSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TemporaryData = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsRead",
                table: "Notifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_PhoneNumber",
                table: "ChatbotSessions",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatbotSessions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Bvn",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "LegalFirstName",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "LegalLastName",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "Nin",
                table: "Merchants");

            migrationBuilder.RenameColumn(
                name: "TransactionReference",
                table: "WebhookEventLogs",
                newName: "AlatPayTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_WebhookEventLogs_TransactionReference",
                table: "WebhookEventLogs",
                newName: "IX_WebhookEventLogs_AlatPayTransactionId");

            migrationBuilder.RenameColumn(
                name: "MonnifyTransferReference",
                table: "PayoutSplits",
                newName: "AlatPayPayoutReference");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);
        }
    }
}
