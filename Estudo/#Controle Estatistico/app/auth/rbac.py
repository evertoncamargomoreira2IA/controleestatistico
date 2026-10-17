from fastapi import HTTPException, Request

from app.models.user import User

ROLE_PERMISSIONS = {
    "admin": {"*"},
    "engenheiro": {"view", "collection.create", "studies.run", "masterdata.manage"},
    "tecnico": {"view", "collection.create"},
    "qualidade": {"view", "studies.run", "msa.approve", "audit.view"},
    "leitor": {"view"},
}


def allows(user: User, permission: str) -> bool:
    grants = ROLE_PERMISSIONS.get(user.role, set())
    return "*" in grants or permission in grants


def require_permission(permission: str):
    async def dependency(request: Request) -> User:
        user = request.state.user
        if not user:
            raise HTTPException(status_code=303, headers={"Location": "/login"})
        if not allows(user, permission):
            raise HTTPException(status_code=403, detail="Acesso negado")
        return user

    return dependency
