from datetime import date, datetime, timezone

from sqlalchemy import (
    Boolean,
    Date,
    DateTime,
    Float,
    ForeignKey,
    Integer,
    JSON,
    String,
    UniqueConstraint,
)
from sqlalchemy.orm import Mapped, mapped_column

from app.models.base import Base, TimestampMixin


class Area(Base, TimestampMixin):
    __tablename__ = "areas"
    __table_args__ = (UniqueConstraint("name", name="uq_areas_name"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class Product(Base, TimestampMixin):
    __tablename__ = "products"
    __table_args__ = (UniqueConstraint("area_id", "name", name="uq_products_area_name"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    area_id: Mapped[int] = mapped_column(ForeignKey("areas.id"), nullable=False)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class Line(Base, TimestampMixin):
    __tablename__ = "lines"
    __table_args__ = (UniqueConstraint("product_id", "name", name="uq_lines_product_name"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    product_id: Mapped[int] = mapped_column(ForeignKey("products.id"), nullable=False)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class Station(Base, TimestampMixin):
    __tablename__ = "stations"
    __table_args__ = (UniqueConstraint("line_id", "name", name="uq_stations_line_name"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    line_id: Mapped[int] = mapped_column(ForeignKey("lines.id"), nullable=False)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class Tooling(Base, TimestampMixin):
    __tablename__ = "toolings"
    __table_args__ = (UniqueConstraint("code", name="uq_toolings_code"),)
    id: Mapped[int] = mapped_column(primary_key=True)
    station_id: Mapped[int] = mapped_column(ForeignKey("stations.id"), nullable=False)
    code: Mapped[str] = mapped_column(String(80), nullable=False)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    description: Mapped[str | None] = mapped_column(String(500))
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class Characteristic(Base, TimestampMixin):
    __tablename__ = "characteristics"
    __table_args__ = (
        UniqueConstraint("tooling_id", "name", name="uq_characteristics_tooling_name"),
    )
    id: Mapped[int] = mapped_column(primary_key=True)
    tooling_id: Mapped[int] = mapped_column(ForeignKey("toolings.id"), nullable=False)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    unit: Mapped[str | None] = mapped_column(String(40))
    lsl: Mapped[float | None] = mapped_column(Float)
    nominal: Mapped[float | None] = mapped_column(Float)
    usl: Mapped[float | None] = mapped_column(Float)
    data_type: Mapped[str] = mapped_column(String(20), nullable=False, default="continua")
    subgroup_size: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    chart_type: Mapped[str] = mapped_column(String(20), nullable=False, default="i_mr")
    revision: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class Instrument(Base, TimestampMixin):
    __tablename__ = "instruments"
    id: Mapped[int] = mapped_column(primary_key=True)
    code: Mapped[str] = mapped_column(String(80), unique=True, nullable=False)
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    description: Mapped[str | None] = mapped_column(String(500))
    type: Mapped[str] = mapped_column(String(80), nullable=False)
    resolution: Mapped[float | None] = mapped_column(Float)
    calibration_due: Mapped[date | None] = mapped_column(Date)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)


class SettingsVersion(Base):
    __tablename__ = "settings_version"
    id: Mapped[int] = mapped_column(primary_key=True)
    version: Mapped[int] = mapped_column(Integer, nullable=False, unique=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        default=lambda: datetime.now(timezone.utc),
        nullable=False,
    )
    created_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"))
    comment: Mapped[str] = mapped_column(String(1000), nullable=False)


class SettingValue(Base):
    __tablename__ = "setting_value"
    id: Mapped[int] = mapped_column(primary_key=True)
    version_id: Mapped[int] = mapped_column(ForeignKey("settings_version.id"), nullable=False)
    key: Mapped[str] = mapped_column(String(160), nullable=False)
    value: Mapped[object] = mapped_column(JSON, nullable=False)
