using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class RellenarStockProductosConControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Productos con control de inventario que quedaron en 0 (p. ej. tras TempDbCleaner o datos viejos)
            migrationBuilder.Sql("""
                UPDATE "Productos"
                SET "Stock" = 100
                WHERE "ControlarStock" = TRUE
                  AND "Activo" = TRUE
                  AND "Stock" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reversible sin saber el stock anterior
        }
    }
}
