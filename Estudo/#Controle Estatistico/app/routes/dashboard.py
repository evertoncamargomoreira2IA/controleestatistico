from fastapi import APIRouter, Depends, Request
from fastapi.templating import Jinja2Templates

from app.auth.rbac import ROLE_PERMISSIONS, require_permission
from app.models.user import User
from app.routes.auth_routes import csrf

router = APIRouter()
templates = Jinja2Templates(directory="app/templates")


@router.get("/painel")
def dashboard(
    request: Request,
    user: User = Depends(require_permission("view")),
):
    return templates.TemplateResponse(
        request,
        "dashboard.html",
        {
            "user": user,
            "csrf_token": csrf(request),
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
        },
    )
