using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionInsertions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaycheckExceptions_SeriesId_OriginalDate",
                table: "PaycheckExceptions");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceExceptions_SeriesId_OriginalDate",
                table: "InvoiceExceptions");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseExceptions_SeriesId_OriginalDate",
                table: "ExpenseExceptions");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalDate",
                table: "PaycheckExceptions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalDate",
                table: "InvoiceExceptions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalDate",
                table: "ExpenseExceptions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckExceptions_SeriesId_Date",
                table: "PaycheckExceptions",
                columns: new[] { "SeriesId", "Date" },
                unique: true,
                filter: "\"OriginalDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckExceptions_SeriesId_OriginalDate",
                table: "PaycheckExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true,
                filter: "\"OriginalDate\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_paycheck_exception_isdeleted_only_on_override",
                table: "PaycheckExceptions",
                sql: "\"OriginalDate\" IS NOT NULL OR \"IsDeleted\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "chk_paycheck_exception_overlay_kind",
                table: "PaycheckExceptions",
                sql: "\"OriginalDate\" IS NOT NULL OR (\"Date\" IS NOT NULL AND \"Amount\" IS NOT NULL AND \"Currency\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceExceptions_SeriesId_Date",
                table: "InvoiceExceptions",
                columns: new[] { "SeriesId", "Date" },
                unique: true,
                filter: "\"OriginalDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceExceptions_SeriesId_OriginalDate",
                table: "InvoiceExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true,
                filter: "\"OriginalDate\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_invoice_exception_isdeleted_only_on_override",
                table: "InvoiceExceptions",
                sql: "\"OriginalDate\" IS NOT NULL OR \"IsDeleted\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "chk_invoice_exception_overlay_kind",
                table: "InvoiceExceptions",
                sql: "\"OriginalDate\" IS NOT NULL OR (\"Date\" IS NOT NULL AND \"Amount\" IS NOT NULL AND \"Currency\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseExceptions_SeriesId_Date",
                table: "ExpenseExceptions",
                columns: new[] { "SeriesId", "Date" },
                unique: true,
                filter: "\"OriginalDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseExceptions_SeriesId_OriginalDate",
                table: "ExpenseExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true,
                filter: "\"OriginalDate\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_expense_exception_isdeleted_only_on_override",
                table: "ExpenseExceptions",
                sql: "\"OriginalDate\" IS NOT NULL OR \"IsDeleted\" = FALSE");

            migrationBuilder.AddCheckConstraint(
                name: "chk_expense_exception_overlay_kind",
                table: "ExpenseExceptions",
                sql: "\"OriginalDate\" IS NOT NULL OR (\"Date\" IS NOT NULL AND \"Amount\" IS NOT NULL AND \"Currency\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaycheckExceptions_SeriesId_Date",
                table: "PaycheckExceptions");

            migrationBuilder.DropIndex(
                name: "IX_PaycheckExceptions_SeriesId_OriginalDate",
                table: "PaycheckExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_paycheck_exception_isdeleted_only_on_override",
                table: "PaycheckExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_paycheck_exception_overlay_kind",
                table: "PaycheckExceptions");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceExceptions_SeriesId_Date",
                table: "InvoiceExceptions");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceExceptions_SeriesId_OriginalDate",
                table: "InvoiceExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_invoice_exception_isdeleted_only_on_override",
                table: "InvoiceExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_invoice_exception_overlay_kind",
                table: "InvoiceExceptions");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseExceptions_SeriesId_Date",
                table: "ExpenseExceptions");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseExceptions_SeriesId_OriginalDate",
                table: "ExpenseExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_expense_exception_isdeleted_only_on_override",
                table: "ExpenseExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "chk_expense_exception_overlay_kind",
                table: "ExpenseExceptions");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalDate",
                table: "PaycheckExceptions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalDate",
                table: "InvoiceExceptions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OriginalDate",
                table: "ExpenseExceptions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckExceptions_SeriesId_OriginalDate",
                table: "PaycheckExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceExceptions_SeriesId_OriginalDate",
                table: "InvoiceExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseExceptions_SeriesId_OriginalDate",
                table: "ExpenseExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true);
        }
    }
}
