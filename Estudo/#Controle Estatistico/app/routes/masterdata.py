from datetime import date

from fastapi import APIRouter, Depends, Form, HTTPException, Request
from fastapi.responses import RedirectResponse
from urllib.parse import parse_qs, urlsplit
from fastapi.templating import Jinja2Templates
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.auth.rbac import ROLE_PERMISSIONS, require_permission
from app.db import get_db
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

router = APIRouter(prefix="/cadastros")
templates = Jinja2Templates(directory="app/templates")
LEVELS = [
    ("areas", Area, None, "Área"),
    ("products", Product, "area_id", "Produto"),
    ("lines", Line, "product_id", "Linha"),
    ("stations", Station, "line_id", "Posto"),
    ("toolings", Tooling, "station_id", "Tooling"),
    ("characteristics", Characteristic, "tooling_id", "Característica"),
]
NEW_LABELS = {
    "areas": "Nova área",
    "products": "Novo produto",
    "lines": "Nova linha",
    "stations": "Novo posto",
    "toolings": "Novo tooling",
    "characteristics": "Nova característica",
}
EDIT_LABELS = {
    "areas": "Editar área",
    "products": "Editar produto",
    "lines": "Editar linha",
    "stations": "Editar posto",
    "toolings": "Editar tooling",
    "characteristics": "Editar característica",
}


def page(request, user, db, selected=None, error=None, status=200):
    # One slot per level; the last level (characteristics) is never "selected".
    selected = list(selected or [])
    selected += [None] * (len(LEVELS) - len(selected))
    levels = []
    for index, (key, model, fk, label) in enumerate(LEVELS):
        parent = selected[index - 1] if index else None
        if fk and parent is None:
            # A child level only lists rows once its parent has been chosen.
            rows = []
        else:
            query = db.query(model)
            if fk:
                query = query.filter(getattr(model, fk) == parent.id)
            rows = query.order_by(model.id).all()
        levels.append(
            {
                "key": key,
                "label": label,
                "rows": rows,
                "selected": selected[index],
                "parent": parent,
            }
        )
    path = [x for x in selected[:5] if x]
    breadcrumbs = []
    query = []
    for index, item in enumerate(path):
        query.append(
            f"{['area_id', 'product_id', 'line_id', 'station_id', 'tooling_id'][index]}={item.id}"
        )
        breadcrumbs.append((item, "/cadastros?" + "&".join(query)))
    return templates.TemplateResponse(
        request,
        "masterdata.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "levels": levels,
            "new_labels": NEW_LABELS,
            "edit_labels": EDIT_LABELS,
            "path": path,
            "breadcrumbs": breadcrumbs,
            "return_to": request.url.path + ("?" + request.url.query if request.url.query else ""),
            "error": error,
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
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    ids = [area_id, product_id, line_id, station_id, tooling_id, characteristic_id]
    selected = []
    for (_, model, _, _), ident in zip(LEVELS, ids):
        selected.append(db.get(model, ident) if ident else None)
    return page(request, user, db, selected)


@router.post("")
def mutate(
    request: Request,
    entity: str = Form(...),
    parent_id: int = Form(0),
    name: str = Form(""),
    code: str = Form(""),
    description: str = Form(""),
    unit: str = Form(""),
    lsl: str = Form(""),
    nominal: str = Form(""),
    usl: str = Form(""),
    data_type: str = Form("continua"),
    subgroup_size: int = Form(1),
    chart_type: str = Form("i_mr"),
    is_active: bool = Form(False),
    item_id: int = Form(0),
    csrf_token: str = Form(...),
    return_to: str = Form("/cadastros"),
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("masterdata.manage")),
):
    check_csrf(request, csrf_token)
    spec = next((entry for entry in LEVELS if entry[0] == entity), None)
    if not spec:
        raise HTTPException(400, "Cadastro inválido")
    key, model, fk, label = spec
    query = parse_qs(urlsplit(return_to).query)
    selected = [
        db.get(model, int(query[name][0])) if query.get(name) else None
        for (_, model, _, _), name in zip(
            LEVELS,
            ["area_id", "product_id", "line_id", "station_id", "tooling_id", "characteristic_id"],
        )
    ]
    try:
        if model is Tooling and code:
            existing = db.query(Tooling).filter(Tooling.code == code)
            if item_id:
                existing = existing.filter(Tooling.id != item_id)
            if existing.first():
                return page(
                    request,
                    user,
                    db,
                    selected,
                    "Código de tooling já cadastrado.",
                    400,
                )
        if item_id:
            row = db.get(model, item_id)
            if not row:
                raise ValueError("Registro não encontrado")
            before = {
                c.name: getattr(row, c.name)
                for c in model.__table__.columns
                if c.name not in {"id", "created_at"}
            }
            if not is_active:
                children = [Product, Line, Station, Tooling, Characteristic]
                idx = [x[0] for x in LEVELS].index(key)
                if idx < 5:
                    child = children[idx]
                    child_fk = LEVELS[idx + 1][2]
                    if (
                        db.query(child)
                        .filter(getattr(child, child_fk) == row.id, child.is_active.is_(True))
                        .first()
                    ):
                        return page(
                            request,
                            user,
                            db,
                            selected,
                            f"Não é possível desativar {label.lower()} com filhos ativos.",
                            400,
                        )
            row.is_active = is_active
            if hasattr(row, "name"):
                row.name = name
            if hasattr(row, "code"):
                row.code = code
            if isinstance(row, Characteristic):
                row.unit = unit or None
                row.data_type = data_type
                row.subgroup_size = subgroup_size
                row.chart_type = chart_type
                if data_type == "continua":
                    row.lsl = float(lsl) if lsl else None
                    row.nominal = float(nominal) if nominal else None
                    row.usl = float(usl) if usl else None
                    if row.lsl is not None and row.usl is not None and row.lsl >= row.usl:
                        raise ValueError("LSL deve ser menor que USL")
                    if row.nominal is not None and (
                        (row.lsl is not None and row.nominal < row.lsl)
                        or (row.usl is not None and row.nominal > row.usl)
                    ):
                        raise ValueError("Nominal deve estar entre LSL e USL")
                else:
                    row.lsl = row.nominal = row.usl = None
                if (
                    chart_type == "i_mr"
                    and subgroup_size != 1
                    or chart_type == "xbar_r"
                    and not 2 <= subgroup_size <= 10
                    or chart_type == "xbar_s"
                    and not 2 <= subgroup_size <= 25
                ):
                    raise ValueError("Tamanho do subgrupo incompatível com o tipo de carta")
                if any(
                    getattr(row, field) != before[field]
                    for field in ("lsl", "nominal", "usl", "subgroup_size", "chart_type")
                ):
                    row.revision += 1
            if hasattr(row, "description"):
                row.description = description or None
            after = {
                c.name: getattr(row, c.name)
                for c in model.__table__.columns
                if c.name not in {"id", "created_at"}
            }
            record(
                db,
                actor_id=user.id,
                entity=key,
                entity_id=row.id,
                action=f"{key}.update",
                before=before,
                after=after,
                origin="web",
                ip=request.client.host if request.client else None,
            )
        else:
            values = {"name": name, "is_active": True}
            if fk:
                values[fk] = parent_id
            if model is Tooling:
                values.update(code=code, description=description or None)
            if model is Characteristic:
                values.update(
                    unit=unit or None,
                    data_type=data_type,
                    subgroup_size=subgroup_size,
                    chart_type=chart_type,
                )
                values.update(
                    lsl=float(lsl) if lsl else None,
                    nominal=float(nominal) if nominal else None,
                    usl=float(usl) if usl else None,
                )
                if data_type == "atributo":
                    values.update(lsl=None, nominal=None, usl=None)
                if (
                    values["lsl"] is not None
                    and values["usl"] is not None
                    and values["lsl"] >= values["usl"]
                ):
                    raise ValueError("LSL deve ser menor que USL")
                if values["nominal"] is not None and (
                    (values["lsl"] is not None and values["nominal"] < values["lsl"])
                    or (values["usl"] is not None and values["nominal"] > values["usl"])
                ):
                    raise ValueError("Nominal deve estar entre LSL e USL")
                if (
                    chart_type == "i_mr"
                    and subgroup_size != 1
                    or chart_type == "xbar_r"
                    and not 2 <= subgroup_size <= 10
                    or chart_type == "xbar_s"
                    and not 2 <= subgroup_size <= 25
                ):
                    raise ValueError("Tamanho do subgrupo incompatível com o tipo de carta")
            row = model(**values)
            db.add(row)
            db.flush()
            record(
                db,
                actor_id=user.id,
                entity=key,
                entity_id=row.id,
                action=f"{key}.create",
                after={
                    c.name: getattr(row, c.name)
                    for c in model.__table__.columns
                    if c.name not in {"id", "created_at"}
                },
                origin="web",
                ip=request.client.host if request.client else None,
            )
        db.commit()
    except (ValueError, TypeError) as exc:
        db.rollback()
        return page(request, user, db, selected, str(exc), 400)
    except IntegrityError:
        db.rollback()
        return page(
            request, user, db, selected, "Já existe um cadastro com esse nome ou código.", 400
        )
    return RedirectResponse(return_to if return_to.startswith("/cadastros") else "/cadastros", 303)


@router.get("/instrumentos")
def instruments(
    request: Request,
    item_id: int | None = None,
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("view")),
):
    return render_instruments(
        request, db, user, selected=db.get(Instrument, item_id) if item_id else None
    )


def render_instruments(request, db, user, selected=None, error=None, status=200, submitted=None):
    return templates.TemplateResponse(
        request,
        "instruments.html",
        {
            "user": user,
            "permissions": ROLE_PERMISSIONS.get(user.role, set()),
            "csrf_token": csrf(request),
            "rows": db.query(Instrument).order_by(Instrument.id).all(),
            "selected": selected,
            "today": date.today(),
            "error": error,
            "submitted": submitted or {},
        },
        status_code=status,
    )


@router.post("/instrumentos")
def instrument_mutate(
    request: Request,
    code: str = Form(""),
    name: str = Form(""),
    description: str = Form(""),
    instrument_type: str = Form("", alias="type"),
    resolution: str = Form(""),
    calibration_due: str = Form(""),
    is_active: bool = Form(False),
    item_id: int = Form(0),
    csrf_token: str = Form(...),
    db: Session = Depends(get_db),
    user: User = Depends(require_permission("masterdata.manage")),
):
    check_csrf(request, csrf_token)
    submitted = {
        "code": code,
        "name": name,
        "description": description,
        "type": instrument_type,
        "resolution": resolution,
        "calibration_due": calibration_due,
        "is_active": is_active,
    }
    try:
        code, name, instrument_type = code.strip(), name.strip(), instrument_type.strip()
        if not code or not name or not instrument_type:
            raise ValueError("Informe código, nome e tipo.")
        duplicate = db.query(Instrument).filter(Instrument.code == code)
        if item_id:
            duplicate = duplicate.filter(Instrument.id != item_id)
        if duplicate.first():
            raise ValueError("Código de instrumento já cadastrado.")
        try:
            parsed_resolution = float(resolution.replace(",", ".")) if resolution.strip() else None
        except ValueError as exc:
            raise ValueError("Informe uma resolução válida.") from exc
        if parsed_resolution is not None and parsed_resolution <= 0:
            raise ValueError("A resolução deve ser maior que zero.")
        try:
            due = date.fromisoformat(calibration_due) if calibration_due.strip() else None
        except ValueError as exc:
            raise ValueError("Informe uma data de vencimento válida.") from exc
        row = db.get(Instrument, item_id) if item_id else Instrument()
        if item_id and row is None:
            raise ValueError("Instrumento não encontrado.")
        fields = (
            "code",
            "name",
            "description",
            "type",
            "resolution",
            "calibration_due",
            "is_active",
        )
        before = {field: getattr(row, field) for field in fields} if item_id else None
        row.code, row.name = code, name
        row.description, row.type = description.strip() or None, instrument_type
        row.resolution, row.calibration_due, row.is_active = parsed_resolution, due, is_active
        db.add(row)
        db.flush()
        record(
            db,
            actor_id=user.id,
            entity="instruments",
            entity_id=row.id,
            action="instrument.update" if before is not None else "instrument.create",
            before=before,
            after={field: getattr(row, field) for field in fields},
            origin="web",
            ip=request.client.host if request.client else None,
        )
        db.commit()
    except (ValueError, TypeError) as exc:
        db.rollback()
        return render_instruments(
            request,
            db,
            user,
            selected=db.get(Instrument, item_id) if item_id else None,
            error=str(exc),
            status=400,
            submitted=submitted,
        )
    except IntegrityError:
        db.rollback()
        return render_instruments(
            request,
            db,
            user,
            selected=db.get(Instrument, item_id) if item_id else None,
            error="Já existe um instrumento com esse código.",
            status=400,
            submitted=submitted,
        )
    return RedirectResponse("/cadastros/instrumentos", 303)
