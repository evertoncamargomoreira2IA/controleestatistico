from fastapi import FastAPI, Request
from fastapi.responses import RedirectResponse
from fastapi.staticfiles import StaticFiles
from starlette.middleware.sessions import SessionMiddleware
from datetime import datetime, timezone

from app.config import get_settings
from app.db import SessionLocal
from app.models import User
from app.routes import auth_routes, collection, dashboard, masterdata, settings, users

settings_config = get_settings()
app = FastAPI(title=settings_config.app_name)
app.mount("/static", StaticFiles(directory="app/static"), name="static")
app.include_router(auth_routes.router)
app.include_router(dashboard.router)
app.include_router(users.router)
app.include_router(masterdata.router)
app.include_router(collection.router)
app.include_router(settings.router)


@app.middleware("http")
async def attach_user(request: Request, call_next):
    request.state.user = None
    uid = request.session.get("uid")
    stamp = request.session.get("last_activity")
    if uid and stamp:
        try:
            elapsed = datetime.now(timezone.utc).timestamp() - float(stamp)
            if elapsed > settings_config.session_max_age:
                request.session.clear()
            else:
                with SessionLocal() as db:
                    request.state.user = db.get(User, int(uid))
                if request.state.user and request.state.user.is_active:
                    request.session["last_activity"] = datetime.now(timezone.utc).timestamp()
                else:
                    request.session.clear()
        except (ValueError, TypeError):
            request.session.clear()
    user = request.state.user
    path = request.url.path
    allowed = path in {"/senha", "/logout"} or path.startswith("/static/")
    if user and user.must_change_password and not allowed:
        return RedirectResponse("/senha", 303)
    return await call_next(request)


# Registered after attach_user on purpose: Starlette runs the last-added
# middleware first, so the session must wrap attach_user to be available there.
app.add_middleware(
    SessionMiddleware,
    secret_key=settings_config.signing_key(),
    max_age=settings_config.session_max_age,
    same_site="lax",
    https_only=settings_config.cookie_secure,
    session_cookie="qms_session",
)


@app.get("/")
def root(request: Request):
    return RedirectResponse("/painel" if request.state.user else "/login", 303)
