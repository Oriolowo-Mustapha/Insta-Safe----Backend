using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalizeNigerianPhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align stored phones with PhoneNormalizer: 11-digit 0-prefixed
            // Nigerian mobiles become 234-prefixed, so web-typed numbers match
            // WhatsApp-resolved ones. Guarded against unique-index collisions.
            migrationBuilder.Sql(@"
UPDATE ""Vendors"" v SET ""Phone"" = '234' || SUBSTRING(v.""Phone"" FROM 2)
WHERE v.""Phone"" ~ '^0[0-9]{10}$'
AND NOT EXISTS (SELECT 1 FROM ""Vendors"" x WHERE x.""Phone"" = '234' || SUBSTRING(v.""Phone"" FROM 2));

UPDATE ""Orders"" o SET ""VendorPhone"" = '234' || SUBSTRING(o.""VendorPhone"" FROM 2)
WHERE o.""VendorPhone"" ~ '^0[0-9]{10}$';

UPDATE ""Orders"" o SET ""DriverPhone"" = '234' || SUBSTRING(o.""DriverPhone"" FROM 2)
WHERE o.""DriverPhone"" ~ '^0[0-9]{10}$';

UPDATE ""ConversationStates"" s SET ""Phone"" = '234' || SUBSTRING(s.""Phone"" FROM 2)
WHERE s.""Phone"" ~ '^0[0-9]{10}$'
AND NOT EXISTS (SELECT 1 FROM ""ConversationStates"" x WHERE x.""Phone"" = '234' || SUBSTRING(s.""Phone"" FROM 2));

UPDATE ""ChatMessages"" SET ""Phone"" = '234' || SUBSTRING(""Phone"" FROM 2)
WHERE ""Phone"" ~ '^0[0-9]{10}$';

UPDATE ""Dispatchers"" d SET ""Phone"" = '234' || SUBSTRING(d.""Phone"" FROM 2)
WHERE d.""Phone"" ~ '^0[0-9]{10}$'
AND NOT EXISTS (SELECT 1 FROM ""Dispatchers"" x WHERE x.""Phone"" = '234' || SUBSTRING(d.""Phone"" FROM 2));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
