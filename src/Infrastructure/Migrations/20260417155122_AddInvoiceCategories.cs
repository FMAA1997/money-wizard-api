using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceCategories : Migration
    {
        private static readonly Guid[] SeedIds =
        [
            new Guid("11111111-1111-1111-1111-000000000001"),
            new Guid("11111111-1111-1111-1111-000000000002"),
            new Guid("11111111-1111-1111-1111-000000000003"),
            new Guid("11111111-1111-1111-1111-000000000004"),
            new Guid("11111111-1111-1111-1111-000000000005"),
            new Guid("11111111-1111-1111-1111-000000000006"),
            new Guid("11111111-1111-1111-1111-000000000007"),
            new Guid("11111111-1111-1111-1111-000000000008"),
            new Guid("11111111-1111-1111-1111-000000000009"),
            new Guid("11111111-1111-1111-1111-000000000010"),
            new Guid("11111111-1111-1111-1111-000000000011"),
            new Guid("11111111-1111-1111-1111-000000000012"),
            new Guid("11111111-1111-1111-1111-000000000013"),
            new Guid("11111111-1111-1111-1111-000000000014"),
            new Guid("11111111-1111-1111-1111-000000000015"),
            new Guid("11111111-1111-1111-1111-000000000016"),
            new Guid("11111111-1111-1111-1111-000000000017"),
            new Guid("11111111-1111-1111-1111-000000000018"),
            new Guid("11111111-1111-1111-1111-000000000019"),
            new Guid("11111111-1111-1111-1111-000000000020"),
            new Guid("11111111-1111-1111-1111-000000000021"),
            new Guid("11111111-1111-1111-1111-000000000022")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Bottom = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Top = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceCategories", x => x.Id);
                });

            var cutDate = new DateOnly(2026, 7, 1);

            migrationBuilder.InsertData(
                table: "InvoiceCategories",
                columns: new[] { "Id", "Name", "Category", "CutDate", "Bottom", "Top", "Tax" },
                values: new object[,]
                {
                    { SeedIds[0],  "A", "Servicios", cutDate, 0m,            10277988.13m,  42386.74m },
                    { SeedIds[1],  "A", "Bienes",    cutDate, 0m,            10277988.13m,  42386.74m },
                    { SeedIds[2],  "B", "Servicios", cutDate, 10277988.14m,  15058447.71m,  48250.78m },
                    { SeedIds[3],  "B", "Bienes",    cutDate, 10277988.14m,  15058447.71m,  48250.78m },
                    { SeedIds[4],  "C", "Servicios", cutDate, 15058447.72m,  21113696.52m,  56501.85m },
                    { SeedIds[5],  "C", "Bienes",    cutDate, 15058447.72m,  21113696.52m,  55227.06m },
                    { SeedIds[6],  "D", "Servicios", cutDate, 21113696.53m,  26212853.42m,  72414.10m },
                    { SeedIds[7],  "D", "Bienes",    cutDate, 21113696.53m,  26212853.42m,  70661.26m },
                    { SeedIds[8],  "E", "Servicios", cutDate, 26212853.43m,  30833964.37m,  102537.97m },
                    { SeedIds[9],  "E", "Bienes",    cutDate, 26212853.43m,  30833964.37m,  92658.35m },
                    { SeedIds[10], "F", "Servicios", cutDate, 30833964.38m,  38642048.36m,  129045.32m },
                    { SeedIds[11], "F", "Bienes",    cutDate, 30833964.38m,  38642048.36m,  111198.27m },
                    { SeedIds[12], "G", "Servicios", cutDate, 38642048.37m,  46211109.37m,  197108.23m },
                    { SeedIds[13], "G", "Bienes",    cutDate, 38642048.37m,  46211109.37m,  135918.34m },
                    { SeedIds[14], "H", "Servicios", cutDate, 46211109.38m,  70113407.33m,  447346.93m },
                    { SeedIds[15], "H", "Bienes",    cutDate, 46211109.38m,  70113407.33m,  272063.40m },
                    { SeedIds[16], "I", "Servicios", cutDate, 70113407.34m,  78479211.62m,  824802.26m },
                    { SeedIds[17], "I", "Bienes",    cutDate, 70113407.34m,  78479211.62m,  406512.05m },
                    { SeedIds[18], "J", "Servicios", cutDate, 78479211.63m,  89872640.30m,  999007.65m },
                    { SeedIds[19], "J", "Bienes",    cutDate, 78479211.63m,  89872640.30m,  497059.41m },
                    { SeedIds[20], "K", "Servicios", cutDate, 89872640.31m,  108357084.05m, 1381687.90m },
                    { SeedIds[21], "K", "Bienes",    cutDate, 89872640.31m,  108357084.05m, 600879.51m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceCategories");
        }
    }
}
