from datetime import datetime

from sqlalchemy import (
    Boolean,
    DateTime,
    Float,
    ForeignKey,
    Index,
    Integer,
    String,
    UniqueConstraint,
)
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.models.base import Base, TimestampMixin


class Subgroup(Base, TimestampMixin):
    __tablename__ = "subgroups"
    __table_args__ = (
        Index("ix_subgroups_characteristic_collected_at", "characteristic_id", "collected_at"),
    )

    id: Mapped[int] = mapped_column(primary_key=True)
    characteristic_id: Mapped[int] = mapped_column(ForeignKey("characteristics.id"), nullable=False)
    characteristic_revision: Mapped[int] = mapped_column(Integer, nullable=False)
    instrument_id: Mapped[int | None] = mapped_column(ForeignKey("instruments.id"))
    operator_id: Mapped[int] = mapped_column(ForeignKey("users.id"), nullable=False)
    collected_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    shift: Mapped[str | None] = mapped_column(String(10))
    lot: Mapped[str | None] = mapped_column(String(80))
    source: Mapped[str] = mapped_column(String(20), nullable=False)
    import_batch_id: Mapped[int | None] = mapped_column(ForeignKey("import_batches.id"))
    is_excluded: Mapped[bool] = mapped_column(Boolean, default=False, nullable=False)
    exclusion_reason: Mapped[str | None] = mapped_column(String(300))
    note: Mapped[str | None] = mapped_column(String(500))

    characteristic = relationship("Characteristic")
    instrument = relationship("Instrument")
    operator = relationship("User")
    import_batch = relationship("ImportBatch", back_populates="subgroups")
    measurements = relationship(
        "Measurement",
        back_populates="subgroup",
        order_by="Measurement.position",
        cascade="all, delete-orphan",
    )


class Measurement(Base):
    __tablename__ = "measurements"
    __table_args__ = (
        UniqueConstraint("subgroup_id", "position", name="uq_measurements_subgroup_position"),
    )

    id: Mapped[int] = mapped_column(primary_key=True)
    subgroup_id: Mapped[int] = mapped_column(ForeignKey("subgroups.id"), nullable=False)
    position: Mapped[int] = mapped_column(Integer, nullable=False)
    value: Mapped[float] = mapped_column(Float, nullable=False)

    subgroup = relationship("Subgroup", back_populates="measurements")


class ImportBatch(Base, TimestampMixin):
    __tablename__ = "import_batches"

    id: Mapped[int] = mapped_column(primary_key=True)
    filename: Mapped[str] = mapped_column(String(255), nullable=False)
    uploaded_by: Mapped[int] = mapped_column(ForeignKey("users.id"), nullable=False)
    uploaded_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    rows_total: Mapped[int] = mapped_column(Integer, nullable=False)
    rows_imported: Mapped[int] = mapped_column(Integer, nullable=False)
    rows_rejected: Mapped[int] = mapped_column(Integer, nullable=False)
    file_sha256: Mapped[str] = mapped_column(String(64), unique=True, nullable=False)
    status: Mapped[str] = mapped_column(String(20), nullable=False)

    uploader = relationship("User")
    subgroups = relationship("Subgroup", back_populates="import_batch")
