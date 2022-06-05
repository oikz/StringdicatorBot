using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stringdicator.Migrations
{
    public partial class AddGorillaMomentstoUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GorillaMoments",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GorillaMoments",
                table: "Users");
        }
    }
}
