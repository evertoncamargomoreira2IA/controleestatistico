import secrets
from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, Form, HTTPException, Request
from fastapi.responses import RedirectResponse
from fastapi.templating import Jinja2Templates
from sqlalchemy.orm import Session

from app.auth.provider import LocalAuthProvider
from app.auth.security import (
    hash_password,
    is_locked,
    password_is_valid,
    verify_password,
)
from app.config import get_settings
from app.db import get_db
from app.models.user import User
from app.services.audit import record

router = APIRouter()
templates = Jinja2Templates(directory="app/templates")
provider = LocalAuthProvider()


def csrf(request: Request) -> str:
    token = request.session.get("csrf")
    if not token:
        token = secrets.token_urlsafe(32)
        request.session["csrf"] = token
    return token


def check_csrf(request: Request, token: str) -> None:
    expected = request.session.get("csrf")
    if not expected or not token or not secrets.compare_digest(expected, token):
        raise HTTPException(403, "Token CSRF inválido")


@router.get("/login")
def login_page(request: Request):
    return templates.TemplateResponse(
        request, "login.html", {"csrf_token": csrf(request), "error": None}
    )


@router.post("/login")
def login(
    request: Request,
    username: str = Form(...),
    password: str = Form(...),
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
):
    check_csrf(request, csrf_token)
    config = get_settings()
    user = db.query(User).filter(User.username == username).first()
    success = bool(
        user and not is_locked(user.locked_until) and provider.authenticate(db, username, password)
    )
    record(
        db,
        actor_id=user.id if user else None,
        entity="auth",
        entity_id=username,
        action="login.success" if success else "login.failure",
        ip=request.client.host if request.client else None,
        origin="web",
    )
    if user:
        if success:
            user.failed_attempts = 0
            user.locked_until = None
            request.session.clear()
            request.session.update(
                uid=user.id,
                last_activity=datetime.now(timezone.utc).timestamp(),
                csrf=secrets.token_urlsafe(32),
            )
        elif not is_locked(user.locked_until):
            user.failed_attempts += 1
            if user.failed_attempts >= config.login_max_attempts:
                user.locked_until = datetime.now(timezone.utc) + timedelta(
                    minutes=config.login_lock_minutes
                )
    db.commit()
    if success:
        return RedirectResponse("/senha" if user.must_change_password else "/painel", 303)
    return templates.TemplateResponse(
        request,
        "login.html",
        {"csrf_token": csrf(request), "error": "Usuário ou senha inválidos."},
        status_code=401,
    )


@router.post("/logout")
def logout(
    request: Request,
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
):
    check_csrf(request, csrf_token)
    user = request.state.user
    record(
        db,
        actor_id=user.id if user else None,
        entity="auth",
        entity_id=user.id if user else None,
        action="logout",
        ip=request.client.host if request.client else None,
        origin="web",
    )
    db.commit()
    request.session.clear()
    return RedirectResponse("/login", 303)


@router.get("/senha")
def change_password_page(request: Request):
    user = request.state.user
    if not user:
        return RedirectResponse("/login", 303)
    from app.auth.rbac import ROLE_PERMISSIONS

    return templates.TemplateResponse(
        request,
        "user_form.html",
        {
            "csrf_token": csrf(request),
            "change_own": True,
            "user": user,
            "roles": [],
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "must_change_password": user.must_change_password,
        },
    )


@router.post("/senha")
def change_password(
    request: Request,
    current_password: str = Form(...),
    new_password: str = Form(...),
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
):
    check_csrf(request, csrf_token)
    config = get_settings()
    if not request.state.user:
        return RedirectResponse("/login", 303)
    # request.state.user comes from the middleware's (closed) session; reload it here so
    # the changes below are actually persisted by this request's session.
    user = db.get(User, request.state.user.id)
    valid = verify_password(current_password, user.password_hash) and password_is_valid(
        new_password, config.password_min_length
    )
    if not valid:
        return templates.TemplateResponse(
            request,
            "user_form.html",
            {
                "csrf_token": csrf(request),
                "change_own": True,
                "user": user,
                "roles": [],
                "must_change_password": user.must_change_password,
                "error": "Senha atual incorreta ou nova senha fora da política.",
            },
            status_code=400,
        )
    user.password_hash = hash_password(new_password)
    user.must_change_password = False
    record(
        db,
        actor_id=user.id,
        entity="user",
        entity_id=user.id,
        action="password.change",
        ip=request.client.host if request.client else None,
        origin="web",
    )
    db.commit()
    return RedirectResponse("/painel", 303)
