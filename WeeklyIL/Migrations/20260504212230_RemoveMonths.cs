using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeeklyIL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Months");

            migrationBuilder.DropColumn(
                name: "MonthlyWins",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "MonthlyWins",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "Months",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RoleId = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Months", x => x.Id);
                });
        }
    }
}
