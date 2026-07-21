using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaSafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDisputeAugmentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisSummary",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiConfidenceScore",
                table: "Disputes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAnalysisSummary",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "AiConfidenceScore",
                table: "Disputes");
        }
    }
}
