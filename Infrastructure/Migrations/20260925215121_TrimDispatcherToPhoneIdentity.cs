using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrimDispatcherToPhoneIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Dispatchers");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "Dispatchers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Dispatchers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Dispatchers");

            migrationBuilder.DropColumn(
                name: "PaystackRecipientCode",
                table: "Dispatchers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Dispatchers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "Dispatchers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Dispatchers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Dispatchers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaystackRecipientCode",
                table: "Dispatchers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
