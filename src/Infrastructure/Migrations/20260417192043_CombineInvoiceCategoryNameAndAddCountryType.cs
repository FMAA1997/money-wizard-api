using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CombineInvoiceCategoryNameAndAddCountryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "InvoiceCategories",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "InvoiceCategories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "InvoiceCategories"
                SET "Name" = "Name" || ' (' || "Category" || ')',
                    "Country" = 'AR',
                    "Type" = 'monotributo'
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "InvoiceCategories",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "InvoiceCategories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "InvoiceCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "InvoiceCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "InvoiceCategories"
                SET "Category" = substring("Name" FROM '\((.*)\)$'),
                    "Name" = substring("Name" FROM '^(.*) \(')
                WHERE "Name" LIKE '% (%)'
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "InvoiceCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Country",
                table: "InvoiceCategories");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "InvoiceCategories");
        }
    }
}
