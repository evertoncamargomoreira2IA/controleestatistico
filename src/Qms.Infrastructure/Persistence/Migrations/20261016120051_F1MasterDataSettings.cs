using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class F1MasterDataSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "characteristics",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ToolingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Lsl = table.Column<double>(type: "REAL", nullable: true),
                    Nominal = table.Column<double>(type: "REAL", nullable: true),
                    Usl = table.Column<double>(type: "REAL", nullable: true),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubgroupSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ChartType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characteristics", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    Type = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Resolution = table.Column<double>(type: "REAL", nullable: true),
                    CalibrationDue = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "lines",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lines", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AreaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "settings_version",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings_version", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LineId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "toolings",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolings", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "setting_value",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettingsVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_setting_value", x => x.Id);
                    table.ForeignKey(
                        name: "FK_setting_value_settings_version_SettingsVersionId",
                        column: x => x.SettingsVersionId,
                        principalTable: "settings_version",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_areas_Name",
                table: "areas",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_characteristics_ToolingId_Name",
                table: "characteristics",
                columns: new[] { "ToolingId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_instruments_Code",
                table: "instruments",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_lines_ProductId_Name",
                table: "lines",
                columns: new[] { "ProductId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_products_AreaId_Name",
                table: "products",
                columns: new[] { "AreaId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_setting_value_SettingsVersionId_Key",
                table: "setting_value",
                columns: new[] { "SettingsVersionId", "Key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_settings_version_Version",
                table: "settings_version",
                column: "Version",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_stations_LineId_Name",
                table: "stations",
                columns: new[] { "LineId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_toolings_Code",
                table: "toolings",
                column: "Code",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "areas");

            migrationBuilder.DropTable(name: "characteristics");

            migrationBuilder.DropTable(name: "instruments");

            migrationBuilder.DropTable(name: "lines");

            migrationBuilder.DropTable(name: "products");

            migrationBuilder.DropTable(name: "setting_value");

            migrationBuilder.DropTable(name: "stations");

            migrationBuilder.DropTable(name: "toolings");

            migrationBuilder.DropTable(name: "settings_version");
        }
    }
}
