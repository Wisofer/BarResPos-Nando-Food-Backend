using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoOpcionesYLineaSelecciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdenLineaOpciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacturaServicioId = table.Column<int>(type: "integer", nullable: false),
                    ProductoOpcionGrupoId = table.Column<int>(type: "integer", nullable: false),
                    ProductoOpcionItemId = table.Column<int>(type: "integer", nullable: false),
                    NombreGrupo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NombreOpcion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PrecioAdicional = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenLineaOpciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdenLineaOpciones_OrdenProductos_FacturaServicioId",
                        column: x => x.FacturaServicioId,
                        principalTable: "OrdenProductos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoOpcionGrupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServicioId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    MinSeleccion = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxSeleccion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoOpcionGrupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoOpcionGrupos_Productos_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoOpcionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    PrecioAdicional = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoOpcionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoOpcionItems_ProductoOpcionGrupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "ProductoOpcionGrupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdenLineaOpciones_FacturaServicioId",
                table: "OrdenLineaOpciones",
                column: "FacturaServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenLineaOpciones_ProductoOpcionItemId",
                table: "OrdenLineaOpciones",
                column: "ProductoOpcionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoOpcionGrupos_ServicioId",
                table: "ProductoOpcionGrupos",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoOpcionItems_GrupoId",
                table: "ProductoOpcionItems",
                column: "GrupoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdenLineaOpciones");

            migrationBuilder.DropTable(
                name: "ProductoOpcionItems");

            migrationBuilder.DropTable(
                name: "ProductoOpcionGrupos");
        }
    }
}
