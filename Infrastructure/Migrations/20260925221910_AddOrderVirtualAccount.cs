using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderVirtualAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayVirtualAccountBank",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayVirtualAccountName",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayVirtualAccountNumber",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackCustomerCode",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayVirtualAccountBank",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PayVirtualAccountName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PayVirtualAccountNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaystackCustomerCode",
                table: "Orders");
        }
    }
}
