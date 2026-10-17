from datetime import date, datetime
from decimal import Decimal
from typing import Any

from sqlalchemy.orm import Session

from app.models.audit import AuditLog


def _json_safe(value: Any) -> Any:
    """Convert values the JSON column cannot store (dates, decimals) into plain types."""
    if isinstance(value, dict):
        return {str(key): _json_safe(item) for key, item in value.items()}
    if isinstance(value, (list, tuple, set)):
        return [_json_safe(item) for item in value]
    if isinstance(value, (datetime, date)):
        return value.isoformat()
    if isinstance(value, Decimal):
        return float(value)
    return value


def record(
    db: Session,
    *,
    actor_id: int | None,
    entity: str,
    entity_id: str | int | None,
    action: str,
    before: dict | None = None,
    after: dict | None = None,
    ip: str | None = None,
    origin: str | None = None,
) -> AuditLog:
    row = AuditLog(
        actor_id=actor_id,
        entity=entity,
        entity_id=str(entity_id) if entity_id is not None else None,
        action=action,
        before_data=_json_safe(before),
        after_data=_json_safe(after),
        ip_address=ip,
        origin=origin,
    )
    db.add(row)
    db.flush()
    return row
