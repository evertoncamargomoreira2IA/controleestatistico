using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class F2Collection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UploadedBy = table.Column<string>(
                        type: "TEXT",
                        maxLength: 450,
                        nullable: false
                    ),
                    UploadedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RowsTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsImported = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsRejected = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "subgroups",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacteristicId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    InstrumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    OperatorId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 450,
                        nullable: false
                    ),
                    CollectedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Shift = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Lot = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsExcluded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExclusionReason = table.Column<string>(
                        type: "TEXT",
                        maxLength: 300,
                        nullable: true
                    ),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subgroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subgroups_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "measurements",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubgroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_measurements_subgroups_SubgroupId",
                        column: x => x.SubgroupId,
                        principalTable: "subgroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_FileSha256",
                table: "import_batches",
                column: "FileSha256",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "uq_measurements_subgroup_position",
                table: "measurements",
                columns: new[] { "SubgroupId", "Position" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_subgroups_characteristic_collected_at",
                table: "subgroups",
                columns: new[] { "CharacteristicId", "CollectedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_subgroups_ImportBatchId",
                table: "subgroups",
                column: "ImportBatchId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "measurements");

            migrationBuilder.DropTable(name: "subgroups");

            migrationBuilder.DropTable(name: "import_batches");
        }
    }
}
