using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMonotributoCategoryToFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonotributoCategory",
                table: "ArgentinaUserProfiles");

            migrationBuilder.AddColumn<Guid>(
                name: "MonotributoCategoryId",
                table: "ArgentinaUserProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArgentinaUserProfiles_MonotributoCategoryId",
                table: "ArgentinaUserProfiles",
                column: "MonotributoCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArgentinaUserProfiles_InvoiceCategories_MonotributoCategory~",
                table: "ArgentinaUserProfiles",
                column: "MonotributoCategoryId",
                principalTable: "InvoiceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArgentinaUserProfiles_InvoiceCategories_MonotributoCategory~",
                table: "ArgentinaUserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ArgentinaUserProfiles_MonotributoCategoryId",
                table: "ArgentinaUserProfiles");

            migrationBuilder.DropColumn(
                name: "MonotributoCategoryId",
                table: "ArgentinaUserProfiles");

            migrationBuilder.AddColumn<string>(
                name: "MonotributoCategory",
                table: "ArgentinaUserProfiles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }
    }
}
