import math
from datetime import datetime, timedelta, timezone

from sqlalchemy.orm import Session

from app.models.collection import Measurement, Subgroup
from app.services.audit import record


def validate_subgroup(
    characteristic,
    values: list[str | float],
    instrument,
    collected_at,
):
    parsed_values = []
    errors = []

    if len(values) != characteristic.subgroup_size:
        errors.append(f"O subgrupo deve conter exatamente {characteristic.subgroup_size} valores.")

    for position, raw_value in enumerate(values, start=1):
        try:
            if isinstance(raw_value, str):
                normalized = raw_value.strip().replace(",", ".")
                if not normalized:
                    raise ValueError
                value = float(normalized)
            else:
                value = float(raw_value)
            if not math.isfinite(value):
                raise ValueError
            parsed_values.append(value)
        except (TypeError, ValueError, OverflowError):
            errors.append(f"Valor na posição {position} está vazio ou não é um número finito.")

    if not characteristic.is_active:
        errors.append("A característica está inativa e não aceita coleta.")
    if characteristic.data_type != "continua":
        errors.append("Esta fase aceita coleta apenas para características contínuas.")

    if instrument is not None:
        if not instrument.is_active:
            errors.append("O instrumento está inativo e não pode ser usado na coleta.")
        if (
            instrument.calibration_due is not None
            and collected_at.date() > instrument.calibration_due
        ):
            errors.append("A calibração do instrumento venceu na data informada para a coleta.")

    now = datetime.now(timezone.utc)
    comparison_time = collected_at
    if comparison_time.tzinfo is None:
        comparison_time = comparison_time.replace(tzinfo=timezone.utc)
    if comparison_time > now + timedelta(minutes=5):
        errors.append("A data e hora da coleta não pode estar mais de 5 minutos no futuro.")

    return parsed_values, errors


def save_subgroup(
    db: Session,
    characteristic,
    values: list[float],
    user,
    instrument,
    collected_at: datetime,
    shift: str | None,
    lot: str | None,
    source: str,
    note: str | None,
    import_batch_id: int | None = None,
) -> Subgroup:
    subgroup = Subgroup(
        characteristic_id=characteristic.id,
        characteristic_revision=characteristic.revision,
        instrument_id=instrument.id if instrument is not None else None,
        operator_id=user.id,
        collected_at=collected_at,
        shift=shift,
        lot=lot,
        source=source,
        note=note,
        import_batch_id=import_batch_id,
        measurements=[
            Measurement(position=position, value=float(value))
            for position, value in enumerate(values, start=1)
        ],
    )
    db.add(subgroup)
    db.flush()
    record(
        db,
        actor_id=user.id,
        entity="subgroup",
        entity_id=subgroup.id,
        action="subgroup.create",
        after={
            "characteristic_id": characteristic.id,
            "characteristic_revision": characteristic.revision,
            "instrument_id": instrument.id if instrument is not None else None,
            "operator_id": user.id,
            "collected_at": collected_at.isoformat(),
            "shift": shift,
            "lot": lot,
            "source": source,
            "import_batch_id": import_batch_id,
            "note": note,
            "values": [float(value) for value in values],
        },
    )
    return subgroup
