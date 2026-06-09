using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AddEiksInXlsxFile.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueEiksCountToProcessingStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UniqueEiksCount",
                table: "ProcessingStatistics",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UniqueEiksCount",
                table: "ProcessingStatistics");
        }
    }
}
