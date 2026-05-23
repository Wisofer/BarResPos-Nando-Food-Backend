using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryClienteDireccion",
                table: "Ordenes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryClienteNombre",
                table: "Ordenes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryClienteTelefono",
                table: "Ordenes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacion",
                table: "Ordenes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigenPedido",
                table: "Ordenes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Salon");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryClienteDireccion",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "DeliveryClienteNombre",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "DeliveryClienteTelefono",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "FechaActualizacion",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "OrigenPedido",
                table: "Ordenes");
        }
    }
}
