from __future__ import annotations

import math
from datetime import datetime
from typing import Any
from sqlalchemy.orm import Session

from app.models.masterdata import SettingValue, SettingsVersion
from app.services.timezone_service import format_local

DEFAULTS = {
    "capability.cp": {"bad_below": 1.0, "good_from": 1.33},
    "capability.cpk": {"bad_below": 1.0, "good_from": 1.33},
    "capability.pp": {"bad_below": 1.0, "good_from": 1.33},
    "capability.ppk": {"bad_below": 1.0, "good_from": 1.33},
    "gage_rr.study_percent": {"good_max": 10, "bad_above": 30},
    "gage_rr.tolerance_percent": {"good_max": 10, "bad_above": 30},
    "gage_rr.ndc_min": 5,
    "kappa.kappa": {"good_from": 0.9, "bad_below": 0.75},
    "kappa.effectiveness_min": 90,
    "kappa.miss_max": 2,
    "kappa.false_alarm_max": 5,
    "chart.outside_3sigma": True,
    "chart.same_side": {"enabled": True, "n": 9},
    "chart.trend": {"enabled": True, "n": 6},
    "chart.spec_proximity": {"enabled": False, "percent": 95},
    "frequency.control_limits_days": 180,
    "frequency.msa_validity_days": 365,
}

LABELS = {
    "capability.cp": "Cp",
    "capability.cpk": "Cpk",
    "capability.pp": "Pp",
    "capability.ppk": "Ppk",
    "gage_rr.study_percent": "%R&R do estudo",
    "gage_rr.tolerance_percent": "%R&R da tolerância",
    "gage_rr.ndc_min": "Ndc mínimo",
    "kappa.kappa": "Kappa",
    "kappa.effectiveness_min": "Efetividade mínima (%)",
    "kappa.miss_max": "Taxa máxima de deixar passar (%)",
    "kappa.false_alarm_max": "Taxa máxima de alarme falso (%)",
    "chart.outside_3sigma": "Fora de 3σ",
    "chart.same_side": "Regra: N pontos do mesmo lado",
    "chart.trend": "Regra: tendência",
    "chart.spec_proximity": "Proximidade da especificação",
    "frequency.control_limits_days": "Dias de recálculo dos limites",
    "frequency.msa_validity_days": "Validade do MSA (dias)",
}


def describe_value(key: str, value: Any) -> str:
    """Formata uma definição para exibição, usando convenções pt-BR."""
    if value is None:
        return "—"

    def number_pt(number: Any) -> str:
        return f"{float(number):.2f}".replace(".", ",")

    if key.startswith("capability.") or key == "kappa.kappa":
        return (
            f"ruim abaixo de {number_pt(value['bad_below'])}; "
            f"bom a partir de {number_pt(value['good_from'])}"
        )
    if key.startswith("gage_rr.") and isinstance(value, dict):
        return (
            f"bom até {number_pt(value['good_max'])}%; "
            f"ruim acima de {number_pt(value['bad_above'])}%"
        )
    if key in {"chart.same_side", "chart.trend"}:
        state = "ativa" if value.get("enabled") else "inativa"
        return f"{state}, N = {value['n']}"
    if key == "chart.spec_proximity":
        state = "ativa" if value.get("enabled") else "inativa"
        return f"{state}, {number_pt(value['percent'])}%"
    if isinstance(value, bool):
        return "sim" if value else "não"
    if key in {"kappa.effectiveness_min", "kappa.miss_max", "kappa.false_alarm_max"}:
        return f"{number_pt(value)}%"
    if isinstance(value, (int, float)):
        return str(value).replace(".", ",")
    return str(value)


def format_datetime(value: datetime) -> str:
    """Format a datetime in local time."""
    return format_local(value)


def active_values(db: Session) -> dict:
    version = db.query(SettingsVersion).order_by(SettingsVersion.version.desc()).first()
    values = dict(DEFAULTS)
    if version:
        values.update(
            {row.key: row.value for row in db.query(SettingValue).filter_by(version_id=version.id)}
        )
    return values


def classify(
    metric: str,
    value: float | None,
    values: dict | None = None,
) -> str | None:
    if value is None:
        return None
    key = metric.lower().replace("%", "").replace(" ", "_")
    aliases = {
        "cp": "capability.cp",
        "cpk": "capability.cpk",
        "pp": "capability.pp",
        "ppk": "capability.ppk",
        "gage_rr_study": "gage_rr.study_percent",
        "gage_rr_tolerance": "gage_rr.tolerance_percent",
        "kappa": "kappa.kappa",
    }
    config = (DEFAULTS if values is None else values).get(aliases.get(key, metric))
    if not isinstance(config, dict):
        return None
    if "good_from" in config and "bad_below" in config:
        if value >= config["good_from"]:
            return "bom"
        if value < config["bad_below"]:
            return "ruim"
        return "atencao"
    if "good_max" in config and "bad_above" in config:
        if value <= config["good_max"]:
            return "bom"
        if value > config["bad_above"]:
            return "ruim"
        return "atencao"
    return None


def values_from_form(form: Any) -> tuple[dict, str]:
    """Converte os campos do formulário em valores completos, validando cada limite."""
    comment = str(form.get("comment", "")).strip()
    if not comment:
        raise ValueError("Informe um comentário para salvar a versão.")
    values = dict(DEFAULTS)

    def number(name: str) -> float:
        try:
            value = float(form[name])
        except (KeyError, TypeError, ValueError) as exc:
            field = name.replace("__", " / ")
            raise ValueError(f"Informe um n\u00famero v\u00e1lido para {field}.") from exc
        if not math.isfinite(value):
            raise ValueError(f"Informe um número válido para {name.replace('__', ' / ')}.")
        return value

    def integer(name: str, minimum: int) -> int:
        value = number(name)
        if not value.is_integer() or value < minimum:
            raise ValueError(
                f"{name.replace('__', ' / ')} deve ser inteiro maior ou igual a {minimum}."
            )
        return int(value)

    for metric in ("cp", "cpk", "pp", "ppk"):
        bad = number(f"capability__{metric}__bad_below")
        good = number(f"capability__{metric}__good_from")
        if bad >= good:
            raise ValueError(f"Em {metric.upper()}, o limite ruim deve ser menor que o bom.")
        values[f"capability.{metric}"] = {"bad_below": bad, "good_from": good}

    for field in ("study_percent", "tolerance_percent"):
        good = number(f"gage_rr__{field}__good_max")
        bad = number(f"gage_rr__{field}__bad_above")
        if not 0 <= good < bad <= 100:
            raise ValueError(
                "Percentuais Gage R&R devem estar entre 0 e 100, com bom menor que ruim."
            )
        values[f"gage_rr.{field}"] = {"good_max": good, "bad_above": bad}
    values["gage_rr.ndc_min"] = integer("gage_rr__ndc_min", 1)

    bad = number("kappa__kappa__bad_below")
    good = number("kappa__kappa__good_from")
    if bad >= good:
        raise ValueError("Em Kappa, o limite ruim deve ser menor que o bom.")
    values["kappa.kappa"] = {"bad_below": bad, "good_from": good}
    for field in ("effectiveness_min", "miss_max", "false_alarm_max"):
        value = number(f"kappa__{field}")
        if not 0 <= value <= 100:
            raise ValueError("Percentuais Kappa devem estar entre 0 e 100.")
        values[f"kappa.{field}"] = value

    values["chart.outside_3sigma"] = "chart__outside_3sigma" in form
    for field in ("same_side", "trend"):
        values[f"chart.{field}"] = {
            "enabled": f"chart__{field}__enabled" in form,
            "n": integer(f"chart__{field}__n", 2),
        }
    spec = number("chart__spec_proximity__percent")
    if not 0 <= spec <= 100:
        raise ValueError("O percentual de proximidade deve estar entre 0 e 100.")
    values["chart.spec_proximity"] = {
        "enabled": "chart__spec_proximity__enabled" in form,
        "percent": spec,
    }
    values["frequency.control_limits_days"] = integer("frequency__control_limits_days", 1)
    values["frequency.msa_validity_days"] = integer("frequency__msa_validity_days", 1)
    return values, comment
