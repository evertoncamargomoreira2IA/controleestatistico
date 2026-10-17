from typing import Protocol

from sqlalchemy.orm import Session

from app.auth.security import verify_password
from app.models.user import User


class AuthProvider(Protocol):
    def authenticate(self, db: Session, username: str, password: str) -> User | None: ...


class LocalAuthProvider:
    def authenticate(self, db: Session, username: str, password: str) -> User | None:
        user = db.query(User).filter(User.username == username).first()
        if user and user.is_active and verify_password(password, user.password_hash):
            return user
        return None
