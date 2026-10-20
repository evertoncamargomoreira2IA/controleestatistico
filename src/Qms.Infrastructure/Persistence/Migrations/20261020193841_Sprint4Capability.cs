using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4Capability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StudyKind",
                table: "cep_datasets",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "CEP");

            migrationBuilder.CreateTable(
                name: "capability_studies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DatasetId = table.Column<int>(type: "INTEGER", nullable: false),
                    DatasetVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingsVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    InputJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capability_studies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_capability_studies_cep_datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "cep_datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_capability_studies_DatasetId_DatasetVersion_SettingsVersion_Method",
                table: "capability_studies",
                columns: new[] { "DatasetId", "DatasetVersion", "SettingsVersion", "Method" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capability_studies");

            migrationBuilder.DropColumn(
                name: "StudyKind",
                table: "cep_datasets");
        }
    }
}
