from fastapi import APIRouter, Depends, Form, Request
from fastapi.responses import RedirectResponse
from fastapi.templating import Jinja2Templates
from sqlalchemy.orm import Session

from app.auth.rbac import ROLE_PERMISSIONS, require_permission
from app.config import get_settings
from app.db import get_db
from app.models.masterdata import SettingValue, SettingsVersion
from app.models.user import User
from app.routes.auth_routes import check_csrf, csrf
from app.services.audit import record
from app.services.settings_service import (
    LABELS,
    active_values,
    describe_value,
    format_datetime,
    values_from_form,
)

router = APIRouter()
templates = Jinja2Templates(directory="app/templates")


def render(request, db, user, error=None, status=200, submitted=None):
    versions = db.query(SettingsVersion).order_by(SettingsVersion.version.desc()).all()
    values = active_values(db)
    previous = versions[1] if len(versions) > 1 else None
    old = (
        {row.key: row.value for row in db.query(SettingValue).filter_by(version_id=previous.id)}
        if previous
        else {}
    )
    current = versions[0] if versions else None
    diff = [
        (LABELS.get(key, key), describe_value(key, old.get(key)), describe_value(key, value))
        for key, value in values.items()
        if previous and old.get(key) != value
    ]
    author_ids = [version.created_by for version in versions if version.created_by]
    authors = (
        {item.id: item.full_name for item in db.query(User).filter(User.id.in_(author_ids)).all()}
        if author_ids
        else {}
    )
    config = get_settings()
    return templates.TemplateResponse(
        request,
        "settings.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "values": values,
            "versions": versions,
            "authors": authors,
            "current": current,
            "diff": diff,
            "format_datetime": format_datetime,
            "error": error,
            "submitted": submitted or {},
            "login_max_attempts": config.login_max_attempts,
            "login_lock_minutes": config.login_lock_minutes,
        },
        status_code=status,
    )


@router.get("/definicoes")
def listing(
    request: Request,
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    return render(request, db, user)


@router.post("/definicoes")
async def save(
    request: Request,
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("settings.manage")),
):
    check_csrf(request, csrf_token)
    form = await request.form()
    try:
        values, comment = values_from_form(form)
    except ValueError as exc:
        return render(request, db, user, str(exc), 400, dict(form))

    previous = db.query(SettingsVersion).order_by(SettingsVersion.version.desc()).first()
    before = active_values(db)
    changed_before = {key: before[key] for key in values if before.get(key) != values[key]}
    changed_after = {key: values[key] for key in values if before.get(key) != values[key]}
    version = SettingsVersion(
        version=(previous.version + 1) if previous else 1,
        created_by=user.id,
        comment=comment,
    )
    db.add(version)
    db.flush()
    for key, value in values.items():
        db.add(SettingValue(version_id=version.id, key=key, value=value))
    record(
        db,
        actor_id=user.id,
        entity="settings",
        entity_id=version.version,
        action="settings.update",
        before=changed_before,
        after=changed_after,
        origin="web",
        ip=request.client.host if request.client else None,
    )
    db.commit()
    return RedirectResponse("/definicoes", 303)
