using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetPaycheckToSeriesModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Paychecks_PaycheckId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Paychecks_Source",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "Paychecks");

            // Clear dangling FK references — data is reset by design.
            migrationBuilder.Sql("UPDATE \"Expenses\" SET \"PaycheckId\" = NULL WHERE \"PaycheckId\" IS NOT NULL;");
            migrationBuilder.Sql("UPDATE \"Invoices\" SET \"Source\" = NULL WHERE \"Source\" IS NOT NULL;");

            migrationBuilder.RenameColumn(
                name: "PaycheckId",
                table: "Expenses",
                newName: "PaycheckSeriesId");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_PaycheckId",
                table: "Expenses",
                newName: "IX_Expenses_PaycheckSeriesId");

            migrationBuilder.CreateTable(
                name: "PaycheckSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckSeries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaycheckExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckExceptions_PaycheckSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "PaycheckSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaycheckSegments",
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
                    RecurrenceTotalInstallments = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaycheckSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaycheckSegments_PaycheckSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "PaycheckSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckExceptions_SeriesId_OriginalDate",
                table: "PaycheckExceptions",
                columns: new[] { "SeriesId", "OriginalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckSegments_SeriesId_EffectiveFrom",
                table: "PaycheckSegments",
                columns: new[] { "SeriesId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckSeries_UserId",
                table: "PaycheckSeries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_PaycheckSeries_PaycheckSeriesId",
                table: "Expenses",
                column: "PaycheckSeriesId",
                principalTable: "PaycheckSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PaycheckSeries_Source",
                table: "Invoices",
                column: "Source",
                principalTable: "PaycheckSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_PaycheckSeries_PaycheckSeriesId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PaycheckSeries_Source",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "PaycheckExceptions");

            migrationBuilder.DropTable(
                name: "PaycheckSegments");

            migrationBuilder.DropTable(
                name: "PaycheckSeries");

            migrationBuilder.RenameColumn(
                name: "PaycheckSeriesId",
                table: "Expenses",
                newName: "PaycheckId");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_PaycheckSeriesId",
                table: "Expenses",
                newName: "IX_Expenses_PaycheckId");

            migrationBuilder.CreateTable(
                name: "Paychecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringPaycheckId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OriginalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecurrenceFrequency = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceTotalInstallments = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paychecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paychecks_Paychecks_RecurringPaycheckId",
                        column: x => x.RecurringPaycheckId,
                        principalTable: "Paychecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Paychecks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_RecurringPaycheckId_OriginalDate",
                table: "Paychecks",
                columns: new[] { "RecurringPaycheckId", "OriginalDate" },
                unique: true,
                filter: "\"RecurringPaycheckId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_UserId",
                table: "Paychecks",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Paychecks_PaycheckId",
                table: "Expenses",
                column: "PaycheckId",
                principalTable: "Paychecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Paychecks_Source",
                table: "Invoices",
                column: "Source",
                principalTable: "Paychecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
