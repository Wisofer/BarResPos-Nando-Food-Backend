using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEsPreparadoColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsPreparado",
                table: "Productos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPreparado",
                table: "Productos",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }
    }
}
