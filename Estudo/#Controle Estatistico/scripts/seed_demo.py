import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from pathlib import Path
from sqlalchemy.orm import Session

from app.db import SessionLocal
from app.models.masterdata import (
    Area,
    Characteristic,
    Instrument,
    Line,
    Product,
    SettingValue,
    SettingsVersion,
    Station,
    Tooling,
)
from app.services.settings_service import DEFAULTS


def seed(db: Session):
    area = db.query(Area).filter_by(name="Montagem").first()
    if not area:
        area = Area(name="Montagem")
        db.add(area)
        db.flush()
    product = db.query(Product).filter_by(area_id=area.id, name="Produto A").first()
    if not product:
        product = Product(area_id=area.id, name="Produto A")
        db.add(product)
        db.flush()
    line = db.query(Line).filter_by(product_id=product.id, name="Linha 1").first()
    if not line:
        line = Line(product_id=product.id, name="Linha 1")
        db.add(line)
        db.flush()
    station = db.query(Station).filter_by(line_id=line.id, name="Posto 10").first()
    if not station:
        station = Station(line_id=line.id, name="Posto 10")
        db.add(station)
        db.flush()
    tooling = db.query(Tooling).filter_by(station_id=station.id, code="TL-001").first()
    if not tooling:
        tooling = Tooling(station_id=station.id, code="TL-001", name="TL-001")
        db.add(tooling)
        db.flush()
    if not db.query(Characteristic).filter_by(tooling_id=tooling.id, name="Altura").first():
        db.add(
            Characteristic(
                tooling_id=tooling.id,
                name="Altura",
                unit="mm",
                lsl=9.5,
                nominal=10,
                usl=10.5,
                data_type="continua",
                subgroup_size=5,
                chart_type="xbar_r",
            )
        )
    if (
        not db.query(Characteristic)
        .filter_by(tooling_id=tooling.id, name="Inspeção visual")
        .first()
    ):
        db.add(
            Characteristic(
                tooling_id=tooling.id,
                name="Inspeção visual",
                data_type="atributo",
                subgroup_size=1,
                chart_type="i_mr",
            )
        )
    if not db.query(Instrument).filter_by(code="INS-001").first():
        db.add(
            Instrument(code="INS-001", name="Instrumento 001", type="Paquímetro", resolution=0.01)
        )
    if not db.query(SettingsVersion).first():
        version = SettingsVersion(
            version=1, created_by=None, comment="Versão inicial de demonstração"
        )
        db.add(version)
        db.flush()
        for key, value in DEFAULTS.items():
            db.add(SettingValue(version_id=version.id, key=key, value=value))
    db.commit()


if __name__ == "__main__":
    with SessionLocal() as session:
        seed(session)
