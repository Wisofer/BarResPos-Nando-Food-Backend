using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddFechaHoraAperturaAndConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaHoraApertura",
                table: "CierresCaja",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaHoraApertura",
                table: "CierresCaja");
        }
    }
}
