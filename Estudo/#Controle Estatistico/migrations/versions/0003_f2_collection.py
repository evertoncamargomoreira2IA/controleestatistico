from alembic import op
import sqlalchemy as sa


revision = "0003_f2_collection"
down_revision = "0002_f1_masterdata_settings"
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        "import_batches",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("filename", sa.String(255), nullable=False),
        sa.Column("uploaded_by", sa.Integer(), sa.ForeignKey("users.id"), nullable=False),
        sa.Column("uploaded_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("rows_total", sa.Integer(), nullable=False),
        sa.Column("rows_imported", sa.Integer(), nullable=False),
        sa.Column("rows_rejected", sa.Integer(), nullable=False),
        sa.Column("file_sha256", sa.String(64), nullable=False),
        sa.Column("status", sa.String(20), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("file_sha256", name="uq_import_batches_file_sha256"),
    )
    op.create_table(
        "subgroups",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column(
            "characteristic_id",
            sa.Integer(),
            sa.ForeignKey("characteristics.id"),
            nullable=False,
        ),
        sa.Column("characteristic_revision", sa.Integer(), nullable=False),
        sa.Column("instrument_id", sa.Integer(), sa.ForeignKey("instruments.id")),
        sa.Column("operator_id", sa.Integer(), sa.ForeignKey("users.id"), nullable=False),
        sa.Column("collected_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("shift", sa.String(10)),
        sa.Column("lot", sa.String(80)),
        sa.Column("source", sa.String(20), nullable=False),
        sa.Column(
            "import_batch_id",
            sa.Integer(),
            sa.ForeignKey("import_batches.id"),
        ),
        sa.Column("is_excluded", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column("exclusion_reason", sa.String(300)),
        sa.Column("note", sa.String(500)),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index(
        "ix_subgroups_characteristic_collected_at",
        "subgroups",
        ["characteristic_id", "collected_at"],
    )
    op.create_table(
        "measurements",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("subgroup_id", sa.Integer(), sa.ForeignKey("subgroups.id"), nullable=False),
        sa.Column("position", sa.Integer(), nullable=False),
        sa.Column("value", sa.Float(), nullable=False),
        sa.UniqueConstraint(
            "subgroup_id",
            "position",
            name="uq_measurements_subgroup_position",
        ),
    )


def downgrade():
    op.drop_table("measurements")
    op.drop_index("ix_subgroups_characteristic_collected_at", table_name="subgroups")
    op.drop_table("subgroups")
    op.drop_table("import_batches")
