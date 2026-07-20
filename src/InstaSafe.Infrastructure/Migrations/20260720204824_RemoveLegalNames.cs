using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegalNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegalFirstName",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "LegalLastName",
                table: "Merchants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
