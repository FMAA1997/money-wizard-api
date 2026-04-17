using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDistributionsWithInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Paychecks_Source",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "PaycheckDistributions");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Expenses",
                newName: "PaycheckId");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_Source",
                table: "Expenses",
                newName: "IX_Expenses_PaycheckId");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<Guid>(type: "uuid", nullable: true),
                    RecurrenceFrequency = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceTotalInstallments = table.Column<int>(type: "integer", nullable: true),
                    RecurringInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_RecurringInvoiceId",
                        column: x => x.RecurringInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_Paychecks_Source",
                        column: x => x.Source,
                        principalTable: "Paychecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_InvoiceId",
                table: "Expenses",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_RecurringInvoiceId_OriginalDate",
                table: "Invoices",
                columns: new[] { "RecurringInvoiceId", "OriginalDate" },
                unique: true,
                filter: "\"RecurringInvoiceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Source",
                table: "Invoices",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UserId",
                table: "Invoices",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Invoices_InvoiceId",
                table: "Expenses",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Paychecks_PaycheckId",
                table: "Expenses",
                column: "PaycheckId",
                principalTable: "Paychecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
                """ALTER TABLE "Expenses" ADD CONSTRAINT "CK_Expenses_SingleSource" CHECK (NOT ("PaycheckId" IS NOT NULL AND "InvoiceId" IS NOT NULL))""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "Expenses" DROP CONSTRAINT IF EXISTS "CK_Expenses_SingleSource" """);

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Invoices_InvoiceId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Paychecks_PaycheckId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_InvoiceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "Expenses");

            migrationBuilder.RenameColumn(
                name: "PaycheckId",
                table: "Expenses",
                newName: "Source");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_PaycheckId",
                table: "Expenses",
                newName: "IX_Expenses_Source");

            migrationBuilder.CreateTable(
                name: "PaycheckDistributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckDistributions_Paychecks_Source",
                        column: x => x.Source,
                        principalTable: "Paychecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckDistributions_Source",
                table: "PaycheckDistributions",
                column: "Source");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Paychecks_Source",
                table: "Expenses",
                column: "Source",
                principalTable: "Paychecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
