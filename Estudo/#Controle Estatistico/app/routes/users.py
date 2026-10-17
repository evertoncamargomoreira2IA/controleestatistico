from fastapi import APIRouter, Depends, Form, HTTPException, Request
from fastapi.responses import RedirectResponse
from fastapi.templating import Jinja2Templates
from sqlalchemy.orm import Session

from app.auth.rbac import ROLE_PERMISSIONS, require_permission
from app.auth.security import hash_password, password_is_valid
from app.config import get_settings
from app.db import get_db
from app.models.audit import AuditLog
from app.models.user import User
from app.routes.auth_routes import check_csrf, csrf
from app.services.audit import record

router = APIRouter()
templates = Jinja2Templates(directory="app/templates")
ROLES = ["admin", "engenheiro", "tecnico", "qualidade", "leitor"]


def snapshot(user: User) -> dict:
    return {
        "username": user.username,
        "full_name": user.full_name,
        "role": user.role,
        "is_active": user.is_active,
    }


def form_error(request, admin, message, target_user=None, status=400):
    return templates.TemplateResponse(
        request,
        "user_form.html",
        {
            "user": admin,
            "target_user": target_user,
            "permissions": ROLE_PERMISSIONS.get(admin.role, set()),
            "roles": ROLES,
            "csrf_token": csrf(request),
            "change_own": False,
            "error": message,
        },
        status_code=status,
    )


@router.get("/usuarios")
def listing(
    request: Request,
    page: int = 1,
    q: str = "",
    db: Session = Depends(get_db),
    admin: User = Depends(require_permission("users.manage")),
):
    query = db.query(User)
    if q:
        query = query.filter(User.username.contains(q) | User.full_name.contains(q))
    return templates.TemplateResponse(
        request,
        "users_list.html",
        {
            "user": admin,
            "permissions": ROLE_PERMISSIONS.get(admin.role, set()),
            "users": query.order_by(User.id).offset((max(page, 1) - 1) * 25).limit(25).all(),
            "page": max(page, 1),
            "q": q,
            "csrf_token": csrf(request),
        },
    )


@router.get("/auditoria")
def audit_page(
    request: Request,
    page: int = 1,
    entity: str = "",
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("audit.view")),
):
    query = db.query(AuditLog)
    if entity:
        query = query.filter(AuditLog.entity == entity)
    rows = query.order_by(AuditLog.id.desc()).offset((max(page, 1) - 1) * 50).limit(50).all()
    return templates.TemplateResponse(
        request,
        "audit.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "rows": rows,
            "page": max(page, 1),
            "entity": entity,
        },
    )


@router.get("/usuarios/novo")
def new_user(
    request: Request,
    admin: User = Depends(require_permission("users.manage")),
):
    return form_error(request, admin, None, status=200)


@router.post("/usuarios")
def create_user(
    request: Request,
    username: str = Form(...),
    full_name: str = Form(...),
    role: str = Form(...),
    password: str = Form(...),
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
    admin: User = Depends(require_permission("users.manage")),
):
    check_csrf(request, csrf_token)
    if role not in ROLES:
        return form_error(request, admin, "Perfil inválido")
    if not password_is_valid(password, get_settings().password_min_length):
        return form_error(request, admin, "Senha fora da política")
    if db.query(User).filter_by(username=username).first():
        return form_error(request, admin, "Usuário já existente")
    user = User(
        username=username,
        full_name=full_name,
        role=role,
        password_hash=hash_password(password),
    )
    db.add(user)
    db.flush()
    record(
        db,
        actor_id=admin.id,
        entity="user",
        entity_id=user.id,
        action="user.create",
        after=snapshot(user),
        ip=request.client.host if request.client else None,
        origin="web",
    )
    db.commit()
    return RedirectResponse("/usuarios", 303)


@router.get("/usuarios/{user_id}/editar")
def edit_user(
    request: Request,
    user_id: int,
    db: Session = Depends(get_db),
    admin: User = Depends(require_permission("users.manage")),
):
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(404)
    return templates.TemplateResponse(
        request,
        "user_form.html",
        {
            "user": admin,
            "target_user": user,
            "roles": ROLES,
            "csrf_token": csrf(request),
            "change_own": False,
            "permissions": ROLE_PERMISSIONS.get(admin.role, set()),
        },
    )


@router.post("/usuarios/{user_id}/editar")
def update_user(
    request: Request,
    user_id: int,
    full_name: str = Form(...),
    role: str = Form(...),
    is_active: bool = Form(False),
    password: str = Form(""),
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
    admin: User = Depends(require_permission("users.manage")),
):
    check_csrf(request, csrf_token)
    user = db.get(User, user_id)
    if not user:
        raise HTTPException(404)
    if role not in ROLES:
        return form_error(request, admin, "Perfil inválido", user)
    if password and not password_is_valid(password, get_settings().password_min_length):
        return form_error(request, admin, "Senha fora da política", user)
    if user.id == admin.id and (role != "admin" or not is_active):
        return form_error(
            request, admin, "Não é permitido remover ou desativar seu próprio perfil admin.", user
        )
    loses_admin = user.role == "admin" and user.is_active and (role != "admin" or not is_active)
    if loses_admin:
        active_admins = db.query(User).filter_by(role="admin", is_active=True).count()
        if active_admins <= 1:
            return form_error(request, admin, "Não é possível remover o último admin ativo.", user)

    before = snapshot(user)
    user.full_name = full_name
    user.role = role
    user.is_active = is_active
    record_args = {
        "actor_id": admin.id,
        "entity": "user",
        "entity_id": user.id,
        "ip": request.client.host if request.client else None,
        "origin": "web",
    }
    if password:
        user.password_hash = hash_password(password)
        user.must_change_password = True
        user.failed_attempts = 0
        user.locked_until = None
        record(db, action="user.password_reset", **record_args)
    record(
        db,
        action="user.update",
        before=before,
        after=snapshot(user),
        **record_args,
    )
    db.commit()
    return RedirectResponse("/usuarios", 303)
