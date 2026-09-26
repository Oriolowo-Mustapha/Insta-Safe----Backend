using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentDraftId",
                table: "ConversationStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SavedOrderDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DraftJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedOrderDrafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedOrderDrafts_Status",
                table: "SavedOrderDrafts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SavedOrderDrafts_VendorPhone",
                table: "SavedOrderDrafts",
                column: "VendorPhone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedOrderDrafts");

            migrationBuilder.DropColumn(
                name: "CurrentDraftId",
                table: "ConversationStates");
        }
    }
}
