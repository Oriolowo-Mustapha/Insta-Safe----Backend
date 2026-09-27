using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows: IS- + 6 hex chars with 0->A, 1->B
            // (hex never contains O/I; avoids the ambiguous digits).
            migrationBuilder.Sql(@"
UPDATE ""Orders""
SET ""OrderNumber"" = 'IS-' || UPPER(TRANSLATE(SUBSTRING(MD5(gen_random_uuid()::text), 1, 6), '01', 'AB'))
WHERE ""OrderNumber"" = '';
");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "Orders");
        }
    }
}
