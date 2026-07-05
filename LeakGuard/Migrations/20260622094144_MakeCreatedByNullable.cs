using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeakGuard.Migrations
{
    /// <inheritdoc />
    public partial class MakeCreatedByNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rules_Users_CreatedBy",
                table: "Rules");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Rules",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Rules_Users_CreatedBy",
                table: "Rules",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rules_Users_CreatedBy",
                table: "Rules");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Rules",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rules_Users_CreatedBy",
                table: "Rules",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
