using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToMoneyEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add as nullable first, backfill from User.Country, then enforce NOT NULL
            // with no lingering column default.

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Paychecks",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Invoices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Expenses",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Paychecks" p
                SET "Currency" = CASE WHEN u."Country" = 'ar' THEN 'ARS' ELSE 'USD' END
                FROM "Users" u
                WHERE p."UserId" = u."Id";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Invoices" i
                SET "Currency" = CASE WHEN u."Country" = 'ar' THEN 'ARS' ELSE 'USD' END
                FROM "Users" u
                WHERE i."UserId" = u."Id";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Expenses" e
                SET "Currency" = CASE WHEN u."Country" = 'ar' THEN 'ARS' ELSE 'USD' END
                FROM "Users" u
                WHERE e."UserId" = u."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Paychecks",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Invoices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Expenses",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Expenses");
        }
    }
}
