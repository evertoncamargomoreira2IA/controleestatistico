from datetime import datetime, timezone

from app.auth.security import hash_password
from app.db import get_db
from app.models.audit import AuditLog
from app.models.user import User


def db_for(client):
    return next(client.app.dependency_overrides[get_db]())


def csrf(client):
    marker = 'name="csrf_token" value="'
    return client.get("/login").text.split(marker)[1].split('"')[0]


def seed(client, role="admin", username="admin"):
    db = db_for(client)
    db.add(
        User(
            username=username,
            full_name=username,
            role=role,
            password_hash=hash_password("StrongPass1234"),
        )
    )
    db.commit()
    db.close()


def seed_must_change(client):
    db = db_for(client)
    db.add(
        User(
            username="admin",
            full_name="admin",
            role="admin",
            password_hash=hash_password("StrongPass1234"),
            must_change_password=True,
        )
    )
    db.commit()
    db.close()


def signin(client, username="admin"):
    return client.post(
        "/login",
        data={
            "username": username,
            "password": "StrongPass1234",
            "csrf_token": csrf(client),
        },
    )


def test_login_ok_and_audit(client):
    seed(client)
    assert signin(client).status_code == 303
    db = db_for(client)
    assert db.query(AuditLog).filter_by(action="login.success").count() == 1
    db.close()


def test_login_failure_generic(client):
    seed(client)
    response = client.post(
        "/login",
        data={"username": "admin", "password": "bad", "csrf_token": csrf(client)},
    )
    assert "Usuário ou senha inválidos" in response.text


def test_lockout_after_n_failures(client):
    seed(client)
    for _ in range(5):
        client.post(
            "/login",
            data={
                "username": "admin",
                "password": "bad",
                "csrf_token": csrf(client),
            },
        )
    assert signin(client).status_code == 401


def test_protected_redirect(client):
    assert client.get("/painel", follow_redirects=False).status_code == 303


def test_must_change_password_redirect_and_change(client):
    seed_must_change(client)
    assert signin(client).headers["location"] == "/senha"
    response = client.get("/painel")
    assert response.status_code == 303
    assert response.headers["location"] == "/senha"
    assert "Troque a senha padrão antes de continuar." in client.get("/senha").text
    response = client.post(
        "/senha",
        data={
            "current_password": "StrongPass1234",
            "new_password": "AnotherStrong123",
            # While the change is pending every page (even /login) redirects to /senha.
            "csrf_token": client.get("/senha")
            .text.split('name="csrf_token" value="')[1]
            .split('"')[0],
        },
    )
    assert response.status_code == 303
    assert client.get("/painel").status_code == 200
    db = db_for(client)
    assert db.query(User).one().must_change_password is False
    db.close()


def test_technician_gets_403_on_users(client):
    seed(client, "tecnico", "tec")
    signin(client, "tec")
    assert client.get("/usuarios").status_code == 403


def test_quality_can_view_audit_but_not_users(client):
    seed(client, "qualidade", "qa")
    signin(client, "qa")
    assert client.get("/auditoria").status_code == 200
    assert client.get("/usuarios").status_code == 403


def test_admin_creates_user_and_audit(client):
    seed(client)
    signin(client)
    response = client.post(
        "/usuarios",
        data={
            "username": "tec",
            "full_name": "Técnico",
            "role": "tecnico",
            "password": "StrongPass1234",
            "csrf_token": csrf(client),
        },
    )
    assert response.status_code == 303
    db = db_for(client)
    assert db.query(AuditLog).filter_by(action="user.create").count() == 1
    db.close()


def test_admin_cannot_deactivate_self(client):
    seed(client)
    signin(client)
    response = client.post(
        "/usuarios/1/editar",
        data={
            "full_name": "admin",
            "role": "admin",
            "csrf_token": csrf(client),
        },
    )
    assert response.status_code == 400
    assert "próprio perfil" in response.text


def test_admin_cannot_demote_self(client):
    seed(client)
    signin(client)
    response = client.post(
        "/usuarios/1/editar",
        data={
            "full_name": "admin",
            "role": "qualidade",
            "is_active": "true",
            "csrf_token": csrf(client),
        },
    )
    assert response.status_code == 400
    assert "próprio perfil" in response.text


def test_admin_can_demote_other_admin_when_one_remains(client):
    seed(client)
    seed(client, "admin", "admin2")
    signin(client)
    response = client.post(
        "/usuarios/2/editar",
        data={
            "full_name": "admin2",
            "role": "qualidade",
            "is_active": "true",
            "csrf_token": csrf(client),
        },
    )
    assert response.status_code == 303
    db = db_for(client)
    assert db.get(User, 2).role == "qualidade"
    db.close()


def test_admin_password_reset_unlocks_user(client):
    seed(client)
    db = db_for(client)
    user = db.query(User).filter_by(username="admin").one()
    user.failed_attempts = 5
    user.locked_until = datetime.now(timezone.utc)
    db.commit()
    db.close()
    signin(client)
    response = client.post(
        "/usuarios/1/editar",
        data={
            "full_name": "admin",
            "role": "admin",
            "is_active": "true",
            "password": "NewStrongPass123",
            "csrf_token": csrf(client),
        },
    )
    assert response.status_code == 303
    db = db_for(client)
    user = db.get(User, 1)
    assert user.failed_attempts == 0
    assert user.locked_until is None
    assert user.must_change_password is True
    assert db.query(AuditLog).filter_by(action="user.password_reset").count() == 1
    db.close()


def test_ensure_default_admin_sqlite_memory():
    from sqlalchemy import create_engine
    from sqlalchemy.orm import Session

    from app.models.base import Base
    from scripts.ensure_default_admin import ensure_default_admin

    engine = create_engine("sqlite://")
    Base.metadata.create_all(engine)
    with Session(engine) as db:
        assert "criado" in ensure_default_admin(db, "development")
        user = db.query(User).one()
        assert user.username == "admin"
        assert user.full_name == "Administrador"
        assert user.role == "admin"
        assert user.must_change_password is True
        assert db.query(AuditLog).filter_by(origin="default_admin").count() == 1
        assert "não criado" in ensure_default_admin(db, "dev")
    with Session(engine) as db:
        db.query(User).delete()
        db.commit()
        assert "fora" in ensure_default_admin(db, "production")
        assert db.query(User).count() == 0
    engine.dispose()


def test_logout_clears_session(client):
    seed(client)
    signin(client)
    response = client.post("/logout", data={"csrf_token": csrf(client)}, follow_redirects=False)
    assert response.status_code == 303
    panel = client.get("/painel", follow_redirects=False)
    assert panel.status_code == 303
    assert panel.headers["location"] == "/login"


def test_missing_csrf_is_rejected(client):
    assert (
        client.post("/login", data={"username": "x", "password": "x", "csrf_token": ""}).status_code
        == 403
    )
