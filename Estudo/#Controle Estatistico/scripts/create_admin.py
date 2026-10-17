import argparse
import sys
from getpass import getpass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from sqlalchemy import inspect

from app.auth.security import hash_password, password_is_valid
from app.config import get_settings
from app.db import SessionLocal, engine
from app.models.user import User
from app.services.audit import record


def main():
    if not inspect(engine).has_table("users"):
        raise SystemExit("Banco sem a tabela users; rode: alembic upgrade head")
    parser = argparse.ArgumentParser()
    parser.add_argument("--username")
    parser.add_argument("--name")
    parser.add_argument("--password")
    args = parser.parse_args()
    username = args.username or input("Usuário: ")
    name = args.name or input("Nome completo: ")
    password = args.password or getpass("Senha: ")
    if not password_is_valid(password, get_settings().password_min_length):
        raise SystemExit("Senha deve atender ao tamanho mínimo, maiúscula, minúscula e número.")
    with SessionLocal() as db:
        if db.query(User).filter_by(username=username).first():
            raise SystemExit("Usuário já existe")
        user = User(
            username=username,
            full_name=name,
            role="admin",
            password_hash=hash_password(password),
        )
        db.add(user)
        db.flush()
        record(
            db,
            actor_id=user.id,
            entity="user",
            entity_id=user.id,
            action="user.create",
            after={"username": username, "role": "admin", "is_active": True},
            origin="create_admin",
        )
        db.commit()
    print("Administrador criado.")


if __name__ == "__main__":
    main()
