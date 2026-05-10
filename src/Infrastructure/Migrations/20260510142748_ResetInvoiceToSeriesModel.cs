using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetInvoiceToSeriesModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseSegments_Invoices_InvoiceId",
                table: "ExpenseSegments");

            migrationBuilder.DropTable(
                name: "Invoices");

            // Clear dangling FK references — data is reset by design.
            migrationBuilder.Sql("UPDATE \"ExpenseSegments\" SET \"InvoiceId\" = NULL WHERE \"InvoiceId\" IS NOT NULL;");

            migrationBuilder.RenameColumn(
                name: "InvoiceId",
                table: "ExpenseSegments",
                newName: "InvoiceSeriesId");

            migrationBuilder.RenameIndex(
                name: "IX_ExpenseSegments_InvoiceId",
                table: "ExpenseSegments",
                newName: "IX_ExpenseSegments_InvoiceSeriesId");

            migrationBuilder.CreateTable(
                name: "InvoiceExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceExceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "Invoice"),
                    Class = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    PointOfSale = table.Column<int>(type: "integer", nullable: true),
                    BaseNumber = table.Column<long>(type: "bigint", nullable: true),
                    ParentExceptionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSeries_InvoiceExceptions_ParentExceptionId",
                        column: x => x.ParentExceptionId,
                        principalTable: "InvoiceExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceSeries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecurrenceFrequency = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceTotalInstallments = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSegments_InvoiceSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "InvoiceSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceSegments_PaycheckSeries_Source",
                        column: x => x.Source,
                        principalTable: "PaycheckSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceExceptions_SeriesId_OriginalDate",
                table: "InvoiceExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSegments_SeriesId_EffectiveFrom",
                table: "InvoiceSegments",
                columns: new[] { "SeriesId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSegments_Source",
                table: "InvoiceSegments",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSeries_ParentExceptionId",
                table: "InvoiceSeries",
                column: "ParentExceptionId",
                filter: "\"ParentExceptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSeries_UserId",
                table: "InvoiceSeries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseSegments_InvoiceSeries_InvoiceSeriesId",
                table: "ExpenseSegments",
                column: "InvoiceSeriesId",
                principalTable: "InvoiceSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceExceptions_InvoiceSeries_SeriesId",
                table: "InvoiceExceptions",
                column: "SeriesId",
                principalTable: "InvoiceSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseSegments_InvoiceSeries_InvoiceSeriesId",
                table: "ExpenseSegments");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceExceptions_InvoiceSeries_SeriesId",
                table: "InvoiceExceptions");

            migrationBuilder.DropTable(
                name: "InvoiceSegments");

            migrationBuilder.DropTable(
                name: "InvoiceSeries");

            migrationBuilder.DropTable(
                name: "InvoiceExceptions");

            migrationBuilder.RenameColumn(
                name: "InvoiceSeriesId",
                table: "ExpenseSegments",
                newName: "InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_ExpenseSegments_InvoiceSeriesId",
                table: "ExpenseSegments",
                newName: "IX_ExpenseSegments_InvoiceId");

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecurringInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Class = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    OriginalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PointOfSale = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "Invoice"),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceFrequency = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceTotalInstallments = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_ParentInvoiceId",
                        column: x => x.ParentInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_RecurringInvoiceId",
                        column: x => x.RecurringInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_PaycheckSeries_Source",
                        column: x => x.Source,
                        principalTable: "PaycheckSeries",
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
                name: "IX_Invoices_ParentInvoiceId",
                table: "Invoices",
                column: "ParentInvoiceId",
                filter: "\"ParentInvoiceId\" IS NOT NULL");

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
                name: "FK_ExpenseSegments_Invoices_InvoiceId",
                table: "ExpenseSegments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
