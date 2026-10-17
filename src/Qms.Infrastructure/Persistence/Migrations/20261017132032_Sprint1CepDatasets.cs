using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1CepDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cep_datasets",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacteristicId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 120,
                        nullable: false
                    ),
                    ProcessPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: false
                    ),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Lsl = table.Column<double>(type: "REAL", nullable: true),
                    Nominal = table.Column<double>(type: "REAL", nullable: true),
                    Usl = table.Column<double>(type: "REAL", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_datasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_datasets_characteristics_CharacteristicId",
                        column: x => x.CharacteristicId,
                        principalTable: "characteristics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "cep_samples",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CepDatasetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Value1 = table.Column<double>(type: "REAL", nullable: true),
                    Value2 = table.Column<double>(type: "REAL", nullable: true),
                    Value3 = table.Column<double>(type: "REAL", nullable: true),
                    InstrumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    CollectedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Shift = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Lot = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    OperatorId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 450,
                        nullable: false
                    ),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SourceSubgroupId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_samples_cep_datasets_CepDatasetId",
                        column: x => x.CepDatasetId,
                        principalTable: "cep_datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_cep_samples_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_cep_samples_subgroups_SourceSubgroupId",
                        column: x => x.SourceSubgroupId,
                        principalTable: "subgroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_cep_datasets_CharacteristicId",
                table: "cep_datasets",
                column: "CharacteristicId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_cep_datasets_CreatedAt",
                table: "cep_datasets",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_cep_samples_CepDatasetId_Position",
                table: "cep_samples",
                columns: new[] { "CepDatasetId", "Position" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_cep_samples_InstrumentId",
                table: "cep_samples",
                column: "InstrumentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_cep_samples_SourceSubgroupId",
                table: "cep_samples",
                column: "SourceSubgroupId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cep_samples");

            migrationBuilder.DropTable(name: "cep_datasets");
        }
    }
}
