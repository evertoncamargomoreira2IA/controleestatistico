from datetime import datetime, timezone
from pathlib import Path
import re
import uuid

from fastapi import APIRouter, Depends, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import RedirectResponse, Response
from fastapi.templating import Jinja2Templates
from sqlalchemy.orm import Session

from app.auth.rbac import ROLE_PERMISSIONS, allows, require_permission
from app.db import get_db
from app.models.collection import Subgroup
from app.models.masterdata import (
    Area,
    Characteristic,
    Instrument,
    Line,
    Product,
    Station,
    Tooling,
)
from app.models.user import User
from app.routes.auth_routes import check_csrf, csrf
from app.services.audit import record
from app.services.collection_service import save_subgroup, validate_subgroup
from app.services.import_service import MAX_FILE_SIZE, commit_import, parse_upload, preview
from app.services.template_service import (
    build_collection_template,
    build_collection_template_csv,
)
from app.services.timezone_service import format_local, parse_local_datetime, to_local

router = APIRouter(prefix="/coleta")
templates = Jinja2Templates(directory="app/templates")
templates.env.filters["local_dt"] = format_local
LEVELS = [
    ("area_id", Area, None),
    ("product_id", Product, "area_id"),
    ("line_id", Line, "product_id"),
    ("station_id", Station, "line_id"),
    ("tooling_id", Tooling, "station_id"),
    ("characteristic_id", Characteristic, "tooling_id"),
]
UPLOAD_DIR = Path("data/uploads")
UUID4_RE = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")


def _template_characteristic(db, characteristic_id):
    characteristic = db.get(Characteristic, characteristic_id) if characteristic_id else None
    if not characteristic or not characteristic.is_active:
        raise HTTPException(404, "Característica não encontrada ou inativa.")
    tooling = db.get(Tooling, characteristic.tooling_id)
    return characteristic, tooling


def _download_filename(tooling_code, characteristic_name, extension):
    stem = re.sub(r"[^A-Za-z0-9_-]+", "_", f"{tooling_code}_{characteristic_name}").strip("_")
    return f"coleta_{stem}.{extension}"


@router.get("/template.xlsx")
def template_xlsx(
    characteristic_id: int,
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    characteristic, tooling = _template_characteristic(db, characteristic_id)
    content = build_collection_template(characteristic, {"tooling_code": tooling.code})
    filename = _download_filename(tooling.code, characteristic.name, "xlsx")
    return Response(
        content,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )


@router.get("/template.csv")
def template_csv(
    characteristic_id: int,
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    characteristic, tooling = _template_characteristic(db, characteristic_id)
    characteristic.tooling_code = tooling.code
    content = build_collection_template_csv(characteristic)
    filename = _download_filename(tooling.code, characteristic.name, "csv")
    return Response(
        content,
        media_type="text/csv; charset=utf-8",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )


@router.get("/importar")
def import_upload_page(
    request: Request,
    user: User = Depends(require_permission("collection.create")),
):
    return templates.TemplateResponse(
        request,
        "import_upload.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "error": None,
        },
    )


@router.post("/importar/previa")
async def import_preview(
    request: Request,
    file: UploadFile = File(...),
    csrf_token: str = Form(...),
    user: User = Depends(require_permission("collection.create")),
    db: Session = Depends(get_db),
):
    check_csrf(request, csrf_token)
    content = await file.read(MAX_FILE_SIZE + 1)
    try:
        rows = parse_upload(file.filename or "", content)
        results = preview(db, rows, user)
    except ValueError as exc:
        return templates.TemplateResponse(
            request,
            "import_upload.html",
            {
                "user": user,
                "permissions": ROLE_PERMISSIONS.get(user.role, set()),
                "csrf_token": csrf(request),
                "error": str(exc),
            },
            status_code=400,
        )
    UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    token = str(uuid.uuid4())
    (UPLOAD_DIR / f"{token}.bin").write_bytes(content)
    return templates.TemplateResponse(
        request,
        "import_preview.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "token": token,
            "filename": Path(file.filename or "upload").name,
            "rows": results,
            "valid_count": sum(not row["errors"] for row in results),
            "rejected_count": sum(bool(row["errors"]) for row in results),
        },
    )


@router.post("/importar/confirmar")
def import_confirm(
    request: Request,
    token: str = Form(...),
    filename: str = Form(...),
    csrf_token: str = Form(...),
    user: User = Depends(require_permission("collection.create")),
    db: Session = Depends(get_db),
):
    check_csrf(request, csrf_token)
    if not UUID4_RE.fullmatch(token):
        raise HTTPException(400, "Token de importação inválido.")
    path = UPLOAD_DIR / f"{token}.bin"
    if not path.is_file():
        raise HTTPException(400, "Arquivo temporário expirado ou inexistente.")
    content = path.read_bytes()
    try:
        rows = parse_upload(filename, content)
        results = preview(db, rows, user)
        batch = commit_import(db, filename, content, results, user)
    except ValueError as exc:
        db.rollback()
        return templates.TemplateResponse(
            request,
            "import_upload.html",
            {
                "user": user,
                "permissions": ROLE_PERMISSIONS.get(user.role, set()),
                "csrf_token": csrf(request),
                "error": str(exc),
            },
            status_code=400,
        )
    finally:
        path.unlink(missing_ok=True)
    return templates.TemplateResponse(
        request,
        "import_result.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "batch": batch,
        },
    )


def render(request, db, user, ids=None, error=None, status=200, submitted=None, ok=False):
    ids = list(ids or [None] * 6)
    ids += [None] * (6 - len(ids))
    selected = [
        db.get(model, ident) if ident else None for (_, model, _), ident in zip(LEVELS, ids)
    ]
    rows = []
    characteristic = selected[5]
    instruments = (
        db.query(Instrument).filter(Instrument.is_active.is_(True)).order_by(Instrument.code).all()
    )
    if characteristic and characteristic.is_active and characteristic.data_type == "continua":
        rows = (
            db.query(Subgroup)
            .filter(Subgroup.characteristic_id == characteristic.id)
            .order_by(Subgroup.collected_at.desc(), Subgroup.id.desc())
            .limit(20)
            .all()
        )
    levels = []
    for index, (key, model, fk) in enumerate(LEVELS):
        parent = selected[index - 1] if index else None
        query = db.query(model).filter(model.is_active.is_(True))
        if fk and parent:
            query = query.filter(getattr(model, fk) == parent.id)
        elif fk:
            query = query.filter(False)
        levels.append(
            {"key": key, "rows": query.order_by(model.name).all(), "selected": selected[index]}
        )
    now_local = to_local(datetime.now(timezone.utc))
    local_now = now_local.strftime("%Y-%m-%dT%H:%M")
    return templates.TemplateResponse(
        request,
        "collection.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "levels": levels,
            "characteristic": characteristic,
            "instruments": instruments,
            "rows": rows,
            "format_local": format_local,
            "error": error,
            "submitted": submitted or {},
            "local_now": local_now,
            "today_local": now_local.date(),
            "ok": ok,
        },
        status_code=status,
    )


@router.get("")
def listing(
    request: Request,
    area_id: int | None = None,
    product_id: int | None = None,
    line_id: int | None = None,
    station_id: int | None = None,
    tooling_id: int | None = None,
    characteristic_id: int | None = None,
    ok: int = 0,
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    return render(
        request,
        db,
        user,
        [area_id, product_id, line_id, station_id, tooling_id, characteristic_id],
        ok=bool(ok),
    )


@router.post("")
async def create(
    request: Request,
    csrf_token: str = Form(...),
    characteristic_id: int | None = Form(None),
    instrument_id: int | None = Form(None),
    collected_at: str = Form(""),
    shift: str = Form(""),
    lot: str = Form(""),
    note: str = Form(""),
    values: list[str] = Form([]),
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("collection.create")),
):
    check_csrf(request, csrf_token)
    form = await request.form()
    ids = [form.get(key) for key, _, _ in LEVELS]
    submitted = dict(form)
    submitted["values"] = form.getlist("values")
    try:
        characteristic = db.get(Characteristic, characteristic_id) if characteristic_id else None
        instrument = db.get(Instrument, instrument_id)
        if not characteristic:
            raise ValueError("Selecione uma característica válida.")
        if not instrument:
            raise ValueError("Selecione um instrumento.")
        timestamp = parse_local_datetime(collected_at)
        parsed, errors = validate_subgroup(characteristic, values, instrument, timestamp)
        if errors:
            raise ValueError(" ".join(errors))
        if shift and shift not in {"T1", "T2", "T3"}:
            raise ValueError("Selecione um turno válido.")
        save_subgroup(
            db,
            characteristic,
            parsed,
            user,
            instrument,
            timestamp,
            shift or None,
            lot.strip() or None,
            "online",
            note.strip() or None,
        )
        db.commit()
    except (ValueError, TypeError) as exc:
        db.rollback()
        try:
            safe_ids = [int(value) if value else None for value in ids]
        except (TypeError, ValueError):
            safe_ids = [None] * 6
        return render(request, db, user, safe_ids, str(exc), 400, submitted)
    tooling = db.get(Tooling, characteristic.tooling_id)
    station = db.get(Station, tooling.station_id)
    line = db.get(Line, station.line_id)
    product = db.get(Product, line.product_id)
    area = db.get(Area, product.area_id)
    query = "&".join(
        f"{key}={item.id}"
        for (key, _, _), item in zip(
            LEVELS, [area, product, line, station, tooling, characteristic]
        )
    )
    return RedirectResponse(f"/coleta?{query}&ok=1", 303)


@router.post("/{subgroup_id}/excluir")
def exclude(
    request: Request,
    subgroup_id: int,
    reason: str = Form(""),
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    check_csrf(request, csrf_token)
    if not allows(user, "masterdata.manage"):
        raise HTTPException(403, "Acesso negado")
    subgroup = db.get(Subgroup, subgroup_id)
    if not subgroup:
        raise HTTPException(404, "Subgrupo não encontrado")
    if not reason.strip():
        characteristic = db.get(Characteristic, subgroup.characteristic_id)
        tooling = db.get(Tooling, characteristic.tooling_id) if characteristic else None
        station = db.get(Station, tooling.station_id) if tooling else None
        line = db.get(Line, station.line_id) if station else None
        product = db.get(Product, line.product_id) if line else None
        area = db.get(Area, product.area_id) if product else None
        ids = [area, product, line, station, tooling, characteristic]
        return render(
            request,
            db,
            user,
            [item.id if item else None for item in ids],
            "Informe o motivo da exclusão.",
            400,
        )
    before = {"is_excluded": subgroup.is_excluded, "exclusion_reason": subgroup.exclusion_reason}
    subgroup.is_excluded = True
    subgroup.exclusion_reason = reason.strip()[:300]
    record(
        db,
        actor_id=user.id,
        entity="subgroup",
        entity_id=subgroup.id,
        action="subgroup.exclude",
        before=before,
        after={"is_excluded": True, "exclusion_reason": subgroup.exclusion_reason},
        origin="web",
        ip=request.client.host if request.client else None,
    )
    db.commit()
    characteristic = db.get(Characteristic, subgroup.characteristic_id)
    tooling = db.get(Tooling, characteristic.tooling_id)
    station = db.get(Station, tooling.station_id)
    line = db.get(Line, station.line_id)
    product = db.get(Product, line.product_id)
    area = db.get(Area, product.area_id)
    query = "&".join(
        f"{key}={item.id}"
        for (key, _, _), item in zip(
            LEVELS, [area, product, line, station, tooling, characteristic]
        )
    )
    return RedirectResponse(f"/coleta?{query}", 303)
