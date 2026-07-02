using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistroAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RolUsuario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Accion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MesaNumero = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: true),
                    DetallesJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosAuditoria_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_Accion",
                table: "RegistrosAuditoria",
                column: "Accion");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_Fecha",
                table: "RegistrosAuditoria",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_Fecha_Accion",
                table: "RegistrosAuditoria",
                columns: new[] { "Fecha", "Accion" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_MesaNumero",
                table: "RegistrosAuditoria",
                column: "MesaNumero");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_UsuarioId",
                table: "RegistrosAuditoria",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosAuditoria");
        }
    }
}
