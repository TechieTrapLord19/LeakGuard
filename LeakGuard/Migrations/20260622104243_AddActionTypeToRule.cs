using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeakGuard.Migrations
{
    /// <inheritdoc />
    public partial class AddActionTypeToRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActionTypeID",
                table: "Rules",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rules_ActionTypeID",
                table: "Rules",
                column: "ActionTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_Rules_ActionTypes_ActionTypeID",
                table: "Rules",
                column: "ActionTypeID",
                principalTable: "ActionTypes",
                principalColumn: "ActionTypeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rules_ActionTypes_ActionTypeID",
                table: "Rules");

            migrationBuilder.DropIndex(
                name: "IX_Rules_ActionTypeID",
                table: "Rules");

            migrationBuilder.DropColumn(
                name: "ActionTypeID",
                table: "Rules");
        }
    }
}
