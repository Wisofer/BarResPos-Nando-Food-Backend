using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarRestPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionAndIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Pagos",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Ordenes",
                type: "BLOB",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdempotencyKey",
                table: "Pagos",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdempotencyKey",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Ordenes");
        }
    }
}
