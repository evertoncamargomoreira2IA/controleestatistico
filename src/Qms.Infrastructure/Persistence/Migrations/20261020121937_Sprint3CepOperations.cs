using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3CepOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cep_limit_approvals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceAnalysisId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponsibleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ApprovedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceResultJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_limit_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_limit_approvals_cep_analyses_SourceAnalysisId",
                        column: x => x.SourceAnalysisId,
                        principalTable: "cep_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cep_occurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnalysisId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ResponsibleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DueAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Cause = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Verification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_occurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_occurrences_cep_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "cep_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cep_plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacteristicId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacteristicRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CharacteristicName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Day = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Shift = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ResponsibleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    TargetSets = table.Column<int>(type: "INTEGER", nullable: false),
                    DueAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Cancelled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_plans_characteristics_CharacteristicId",
                        column: x => x.CharacteristicId,
                        principalTable: "characteristics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cep_limit_revocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApprovalId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_limit_revocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_limit_revocations_cep_limit_approvals_ApprovalId",
                        column: x => x.ApprovalId,
                        principalTable: "cep_limit_approvals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cep_plan_datasets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    DatasetId = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LinkedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_plan_datasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cep_plan_datasets_cep_datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "cep_datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cep_plan_datasets_cep_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "cep_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cep_limit_approvals_SourceAnalysisId",
                table: "cep_limit_approvals",
                column: "SourceAnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cep_limit_revocations_ApprovalId",
                table: "cep_limit_revocations",
                column: "ApprovalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cep_occurrences_AnalysisId",
                table: "cep_occurrences",
                column: "AnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cep_plan_datasets_DatasetId",
                table: "cep_plan_datasets",
                column: "DatasetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cep_plan_datasets_PlanId",
                table: "cep_plan_datasets",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_cep_plans_CharacteristicId",
                table: "cep_plans",
                column: "CharacteristicId");

            migrationBuilder.CreateIndex(
                name: "IX_cep_plans_Day",
                table: "cep_plans",
                column: "Day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cep_limit_revocations");

            migrationBuilder.DropTable(
                name: "cep_occurrences");

            migrationBuilder.DropTable(
                name: "cep_plan_datasets");

            migrationBuilder.DropTable(
                name: "cep_limit_approvals");

            migrationBuilder.DropTable(
                name: "cep_plans");
        }
    }
}
