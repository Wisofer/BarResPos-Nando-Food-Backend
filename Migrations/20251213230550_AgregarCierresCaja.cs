using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCierresCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FechaCierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaHoraCierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    MontoInicial = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalEfectivo = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTarjeta = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTransferencia = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCordobas = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalDolares = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalGeneral = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalOrdenes = table.Column<int>(type: "integer", nullable: false),
                    TotalPagos = table.Column<int>(type: "integer", nullable: false),
                    MontoEsperado = table.Column<decimal>(type: "numeric", nullable: false),
                    MontoReal = table.Column<decimal>(type: "numeric", nullable: true),
                    Diferencia = table.Column<decimal>(type: "numeric", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresCaja_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CierresCaja_UsuarioId",
                table: "CierresCaja",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresCaja");
        }
    }
}
