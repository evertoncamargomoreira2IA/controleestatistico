"""Base F0: usuários e auditoria."""

from alembic import op
import sqlalchemy as sa

revision = "0001_f0_initial"
down_revision = None
branch_labels = None
depends_on = None


def upgrade():
    op.create_table(
        "users",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("username", sa.String(120), nullable=False),
        sa.Column("full_name", sa.String(200), nullable=False),
        sa.Column("password_hash", sa.String(512), nullable=False),
        sa.Column("role", sa.String(30), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("failed_attempts", sa.Integer(), nullable=False),
        sa.Column("locked_until", sa.DateTime(timezone=True)),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index("ix_users_username", "users", ["username"], unique=True)
    op.create_table(
        "audit_log",
        sa.Column("id", sa.Integer(), primary_key=True),
        sa.Column("actor_id", sa.Integer()),
        sa.Column("occurred_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("entity", sa.String(100), nullable=False),
        sa.Column("entity_id", sa.String(100)),
        sa.Column("action", sa.String(80), nullable=False),
        sa.Column("before_data", sa.JSON()),
        sa.Column("after_data", sa.JSON()),
        sa.Column("ip_address", sa.String(64)),
        sa.Column("origin", sa.String(100)),
    )
    for column in ("actor_id", "occurred_at", "entity"):
        op.create_index(f"ix_audit_log_{column}", "audit_log", [column])


def downgrade():
    for column in ("entity", "occurred_at", "actor_id"):
        op.drop_index(f"ix_audit_log_{column}", table_name="audit_log")
    op.drop_table("audit_log")
    op.drop_index("ix_users_username", table_name="users")
    op.drop_table("users")
