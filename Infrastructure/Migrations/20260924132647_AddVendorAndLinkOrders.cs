using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorAndLinkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BankCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PaystackRecipientCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VendorId",
                table: "Orders",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Phone",
                table: "Vendors",
                column: "Phone",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Vendors_VendorId",
                table: "Orders",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
INSERT INTO ""Vendors"" (""Id"", ""CreatedAt"", ""UpdatedAt"", ""Phone"", ""DisplayName"", ""IsActive"")
SELECT gen_random_uuid(), NOW(), NULL, vp.""VendorPhone"", vp.""VendorPhone"", true
FROM (SELECT DISTINCT ""VendorPhone"" FROM ""Orders"" WHERE ""VendorId"" IS NULL) vp
WHERE NOT EXISTS (SELECT 1 FROM ""Vendors"" v WHERE v.""Phone"" = vp.""VendorPhone"");

UPDATE ""Orders"" o SET ""VendorId"" = v.""Id""
FROM ""Vendors"" v WHERE v.""Phone"" = o.""VendorPhone"" AND o.""VendorId"" IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Vendors_VendorId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VendorId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "Orders");
        }
    }
}
