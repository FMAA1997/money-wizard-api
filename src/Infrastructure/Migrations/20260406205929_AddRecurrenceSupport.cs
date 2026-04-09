using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Paychecks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OriginalDate",
                table: "Paychecks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecurrenceEndDate",
                table: "Paychecks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceFrequency",
                table: "Paychecks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "Paychecks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceTotalInstallments",
                table: "Paychecks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringPaycheckId",
                table: "Paychecks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Expenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OriginalDate",
                table: "Expenses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecurrenceEndDate",
                table: "Expenses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceFrequency",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceTotalInstallments",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringExpenseId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_RecurringPaycheckId_OriginalDate",
                table: "Paychecks",
                columns: new[] { "RecurringPaycheckId", "OriginalDate" },
                unique: true,
                filter: "\"RecurringPaycheckId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RecurringExpenseId_OriginalDate",
                table: "Expenses",
                columns: new[] { "RecurringExpenseId", "OriginalDate" },
                unique: true,
                filter: "\"RecurringExpenseId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Expenses_RecurringExpenseId",
                table: "Expenses",
                column: "RecurringExpenseId",
                principalTable: "Expenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Paychecks_Paychecks_RecurringPaycheckId",
                table: "Paychecks",
                column: "RecurringPaycheckId",
                principalTable: "Paychecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Expenses_RecurringExpenseId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Paychecks_Paychecks_RecurringPaycheckId",
                table: "Paychecks");

            migrationBuilder.DropIndex(
                name: "IX_Paychecks_RecurringPaycheckId_OriginalDate",
                table: "Paychecks");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_RecurringExpenseId_OriginalDate",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "OriginalDate",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndDate",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "RecurrenceFrequency",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "RecurrenceTotalInstallments",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "RecurringPaycheckId",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "OriginalDate",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndDate",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceFrequency",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceTotalInstallments",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurringExpenseId",
                table: "Expenses");
        }
    }
}
