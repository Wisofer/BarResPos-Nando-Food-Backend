using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddMesaOrigenNumeroToOrden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MesaOrigenNumero",
                table: "Ordenes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MesaOrigenNumero",
                table: "Ordenes");
        }
    }
}
