using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceEngine.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusSoldSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SoldSeats",
                table: "Buses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoldSeats",
                table: "Buses");
        }
    }
}
