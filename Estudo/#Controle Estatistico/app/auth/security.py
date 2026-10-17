from datetime import datetime, timezone

from argon2 import PasswordHasher
from argon2.exceptions import VerificationError, VerifyMismatchError

_password_hasher = PasswordHasher()


def hash_password(password: str) -> str:
    return _password_hasher.hash(password)


def verify_password(password: str, hashed: str) -> bool:
    try:
        return _password_hasher.verify(hashed, password)
    except (VerifyMismatchError, VerificationError):
        return False


def password_is_valid(password: str, minimum: int) -> bool:
    return (
        len(password) >= minimum
        and any(character.islower() for character in password)
        and any(character.isupper() for character in password)
        and any(character.isdigit() for character in password)
    )


def is_locked(until: datetime | None) -> bool:
    return bool(until and until.replace(tzinfo=timezone.utc) > datetime.now(timezone.utc))
