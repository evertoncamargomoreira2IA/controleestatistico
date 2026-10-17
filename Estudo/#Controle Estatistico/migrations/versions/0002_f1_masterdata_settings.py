from alembic import op
import sqlalchemy as sa

revision = "0002_f1_masterdata_settings"
down_revision = "0001_f0_initial"
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        "areas",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("name", name="uq_areas_name"),
    )
    op.create_table(
        "products",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("area_id", sa.Integer(), sa.ForeignKey("areas.id"), nullable=False),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("area_id", "name", name="uq_products_area_name"),
    )
    op.create_table(
        "lines",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("product_id", sa.Integer(), sa.ForeignKey("products.id"), nullable=False),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("product_id", "name", name="uq_lines_product_name"),
    )
    op.create_table(
        "stations",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("line_id", sa.Integer(), sa.ForeignKey("lines.id"), nullable=False),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("line_id", "name", name="uq_stations_line_name"),
    )
    op.create_table(
        "toolings",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("station_id", sa.Integer(), sa.ForeignKey("stations.id"), nullable=False),
        sa.Column("code", sa.String(80), nullable=False),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("description", sa.String(500)),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("code", name="uq_toolings_code"),
    )
    op.create_table(
        "characteristics",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("tooling_id", sa.Integer(), sa.ForeignKey("toolings.id"), nullable=False),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("unit", sa.String(40)),
        sa.Column("lsl", sa.Float()),
        sa.Column("nominal", sa.Float()),
        sa.Column("usl", sa.Float()),
        sa.Column("data_type", sa.String(20), nullable=False),
        sa.Column("subgroup_size", sa.Integer(), nullable=False),
        sa.Column("chart_type", sa.String(20), nullable=False),
        sa.Column("revision", sa.Integer(), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("tooling_id", "name", name="uq_characteristics_tooling_name"),
    )
    op.create_table(
        "instruments",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("code", sa.String(80), unique=True, nullable=False),
        sa.Column("name", sa.String(120), nullable=False),
        sa.Column("description", sa.String(500)),
        sa.Column("type", sa.String(80), nullable=False),
        sa.Column("resolution", sa.Float()),
        sa.Column("calibration_due", sa.Date()),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
    )
    op.create_table(
        "settings_version",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("version", sa.Integer(), unique=True, nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("created_by", sa.Integer(), sa.ForeignKey("users.id")),
        sa.Column("comment", sa.String(1000), nullable=False),
    )
    op.create_table(
        "setting_value",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("version_id", sa.Integer(), sa.ForeignKey("settings_version.id"), nullable=False),
        sa.Column("key", sa.String(160), nullable=False),
        sa.Column("value", sa.JSON(), nullable=False),
    )


def downgrade():
    for table in (
        "setting_value",
        "settings_version",
        "instruments",
        "characteristics",
        "toolings",
        "stations",
        "lines",
        "products",
        "areas",
    ):
        op.drop_table(table)
