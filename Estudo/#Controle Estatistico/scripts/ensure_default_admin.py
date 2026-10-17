import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from sqlalchemy import inspect
from sqlalchemy.orm import Session

from app.auth.security import hash_password
from app.config import get_settings
from app.db import SessionLocal, engine
from app.models.user import User
from app.services.audit import record


DEVELOPMENT_ENVIRONMENTS = {"dev", "development", "local"}


def ensure_default_admin(db: Session, environment: str) -> str:
    if environment.lower() not in DEVELOPMENT_ENVIRONMENTS:
        return "Aviso: admin padrão não criado fora do ambiente de desenvolvimento."
    if db.query(User).first():
        return "Usuários já existem; admin padrão não criado."
    user = User(
        username="admin",
        full_name="Administrador",
        role="admin",
        password_hash=hash_password("admin"),
        must_change_password=True,
    )
    db.add(user)
    db.flush()
    record(
        db,
        actor_id=user.id,
        entity="user",
        entity_id=user.id,
        action="user.create",
        after={"username": user.username, "role": user.role, "is_active": user.is_active},
        origin="default_admin",
    )
    db.commit()
    return "Admin padrão criado (usuario admin / senha admin); a troca de senha é obrigatória."


def main():
    if not inspect(engine).has_table("users"):
        raise SystemExit("Banco sem a tabela users; rode: alembic upgrade head")
    with SessionLocal() as db:
        print(ensure_default_admin(db, get_settings().environment))


if __name__ == "__main__":
    main()
