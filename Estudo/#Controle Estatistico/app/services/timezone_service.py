from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

UTC = timezone.utc

try:
    LOCAL_TZ = ZoneInfo("America/Sao_Paulo")
except ZoneInfoNotFoundError:
    LOCAL_TZ = timezone(timedelta(hours=-3))


def parse_local_datetime(text: str) -> datetime:
    """Parse datetime-local input and return an aware UTC datetime."""
    try:
        value = datetime.strptime(text, "%Y-%m-%dT%H:%M")
    except (TypeError, ValueError) as exc:
        raise ValueError("Informe uma data e hora válidas.") from exc
    return value.replace(tzinfo=LOCAL_TZ).astimezone(UTC)


def to_local(value: datetime) -> datetime:
    """Convert a datetime to local time; naive database values are UTC."""
    if value.tzinfo is None:
        value = value.replace(tzinfo=UTC)
    return value.astimezone(LOCAL_TZ)


def format_local(value: datetime) -> str:
    return to_local(value).strftime("%d/%m/%Y %H:%M")
