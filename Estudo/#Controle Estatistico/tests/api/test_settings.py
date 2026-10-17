from app.models.audit import AuditLog
from app.models.masterdata import SettingValue, SettingsVersion
from tests.api.test_auth_users import csrf, db_for, seed, signin


def settings_form(client, comment="Ajuste validado"):
    data = {
        "csrf_token": csrf(client),
        "comment": comment,
        "chart__outside_3sigma": "1",
    }
    for metric in ("cp", "cpk", "pp", "ppk"):
        data[f"capability__{metric}__bad_below"] = "1"
        data[f"capability__{metric}__good_from"] = "1.33"
    for metric in ("study_percent", "tolerance_percent"):
        data[f"gage_rr__{metric}__good_max"] = "10"
        data[f"gage_rr__{metric}__bad_above"] = "30"
    data.update(
        {
            "gage_rr__ndc_min": "5",
            "kappa__kappa__bad_below": "0.75",
            "kappa__kappa__good_from": "0.9",
            "kappa__effectiveness_min": "90",
            "kappa__miss_max": "2",
            "kappa__false_alarm_max": "5",
            "chart__same_side__enabled": "1",
            "chart__same_side__n": "9",
            "chart__trend__enabled": "1",
            "chart__trend__n": "6",
            "chart__spec_proximity__percent": "95",
            "frequency__control_limits_days": "180",
            "frequency__msa_validity_days": "365",
        }
    )
    return data


def test_admin_saves_version_and_audit(client):
    seed(client)
    db = db_for(client)
    previous = SettingsVersion(version=1, created_by=1, comment="Inicial")
    db.add(previous)
    db.flush()
    db.add(
        SettingValue(
            version_id=previous.id,
            key="capability.cpk",
            value={"bad_below": 1.0, "good_from": 1.3},
        )
    )
    db.commit()
    db.close()
    signin(client)
    response = client.post("/definicoes", data=settings_form(client))
    assert response.status_code == 303
    db = db_for(client)
    assert db.query(SettingsVersion).order_by(SettingsVersion.version.desc()).first().version == 2
    audit = db.query(AuditLog).filter_by(action="settings.update").one()
    assert audit.before_data == {"capability.cpk": {"bad_below": 1.0, "good_from": 1.3}}
    assert audit.after_data == {"capability.cpk": {"bad_below": 1.0, "good_from": 1.33}}
    db.close()


def test_engineer_cannot_save_settings(client):
    seed(client, "engenheiro", "eng")
    signin(client, "eng")
    assert client.post("/definicoes", data=settings_form(client)).status_code == 403


def test_reader_can_view_settings(client):
    seed(client, "leitor", "reader")
    signin(client, "reader")
    assert client.get("/definicoes").status_code == 200


def test_bad_limit_order_is_rejected_and_input_preserved(client):
    seed(client)
    signin(client)
    data = settings_form(client)
    data["capability__cpk__bad_below"] = "1.5"
    response = client.post("/definicoes", data=data)
    assert response.status_code == 400
    assert "limite ruim deve ser menor que o bom" in response.text
    assert 'value="1.5"' in response.text


def test_comment_is_required(client):
    seed(client)
    signin(client)
    response = client.post("/definicoes", data=settings_form(client, "   "))
    assert response.status_code == 400
    assert "Informe um comentário" in response.text


def test_unchecked_rule_is_saved_disabled(client):
    seed(client)
    signin(client)
    data = settings_form(client)
    data.pop("chart__same_side__enabled")
    client.post("/definicoes", data=data)
    db = db_for(client)
    version = db.query(SettingsVersion).one()
    saved = db.query(SettingValue).filter_by(version_id=version.id, key="chart.same_side").one()
    assert saved.value["enabled"] is False
    db.close()
