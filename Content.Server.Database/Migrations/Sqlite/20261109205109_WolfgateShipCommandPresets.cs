using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class WolfgateShipCommandPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wolfgate_ship_command_preset",
                columns: table => new
                {
                    wolfgate_ship_command_preset_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    vessel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: false),
                    saved = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wolfgate_ship_command_preset", x => x.wolfgate_ship_command_preset_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wolfgate_ship_command_preset_player_user_id_vessel_name",
                table: "wolfgate_ship_command_preset",
                columns: new[] { "player_user_id", "vessel", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wolfgate_ship_command_preset");
        }
    }
}
