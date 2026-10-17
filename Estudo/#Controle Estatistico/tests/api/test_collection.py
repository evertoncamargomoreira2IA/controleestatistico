from datetime import datetime, timedelta, timezone

from app.models.audit import AuditLog
from app.models.collection import Measurement, Subgroup
from app.models.masterdata import Characteristic, Instrument
from app.services.timezone_service import parse_local_datetime
from tests.api.test_auth_users import csrf, db_for, seed, signin
from tests.api.test_masterdata import create_hierarchy, post_cadastro, post_instrument


def setup_characteristic(client, name="Online", data_type="continua"):
    *_, tooling_id = create_hierarchy(client, f"Coleta{name}")
    response = post_cadastro(
        client,
        "characteristics",
        parent_id=tooling_id,
        name=name,
        data_type=data_type,
        unit="mm",
        lsl="9",
        nominal="10",
        usl="11",
        subgroup_size="3",
        chart_type="xbar_r",
    )
    assert response.status_code == 303
    db = db_for(client)
    characteristic = db.query(Characteristic).filter_by(name=name).one()
    ident = characteristic.id
    db.close()
    return ident


def create_instrument(client, code="CAL-ONLINE", due=""):
    response = post_instrument(client, code=code, calibration_due=due)
    assert response.status_code == 303
    db = db_for(client)
    ident = db.query(Instrument).filter_by(code=code).one().id
    db.close()
    return ident


def post_collection(client, characteristic_id, instrument_id, **fields):
    data = {
        "csrf_token": csrf(client),
        "characteristic_id": characteristic_id,
        "instrument_id": instrument_id,
        "collected_at": "2026-10-15T12:00",
        "shift": "T1",
        "lot": "L-1",
        "note": "",
        "values": ["9.8", "10", "10.2"],
    }
    data.update(fields)
    return client.post("/coleta", data=data, follow_redirects=False)


def test_technician_registers_subgroup(client):
    seed(client)
    signin(client)
    characteristic_id = setup_characteristic(client)
    instrument_id = create_instrument(client)
    client.cookies.clear()
    seed(client, "tecnico", "tec")
    signin(client, "tec")
    response = post_collection(client, characteristic_id, instrument_id)
    assert response.status_code == 303
    db = db_for(client)
    subgroup = db.query(Subgroup).one()
    assert subgroup.source == "online"
    assert subgroup.characteristic_id == characteristic_id
    assert db.query(Measurement).filter_by(subgroup_id=subgroup.id).count() == 3
    assert db.query(AuditLog).filter_by(action="subgroup.create").count() == 1
    db.close()


def test_collection_rejects_invalid_values_count_instrument_and_time(client):
    seed(client)
    signin(client)
    characteristic_id = setup_characteristic(client)
    instrument_id = create_instrument(client)
    invalid = post_collection(
        client, characteristic_id, instrument_id, values=["9.8", "bad", "10.2"]
    )
    assert invalid.status_code == 400
    assert "posi\u00e7\u00e3o 2" in invalid.text
    assert (
        post_collection(client, characteristic_id, instrument_id, values=["10"]).status_code == 400
    )
    assert (
        post_collection(
            client,
            characteristic_id,
            instrument_id,
            collected_at=(datetime.now(timezone.utc) + timedelta(hours=1))
            .astimezone(timezone(timedelta(hours=-3)))
            .strftime("%Y-%m-%dT%H:%M"),
        ).status_code
        == 400
    )
    expired_id = create_instrument(client, "CAL-OLD", "2020-01-01")
    assert post_collection(client, characteristic_id, expired_id).status_code == 400


def test_reader_cannot_post_collection(client):
    seed(client)
    signin(client)
    characteristic_id = setup_characteristic(client)
    instrument_id = create_instrument(client)
    client.cookies.clear()
    seed(client, "leitor", "reader")
    signin(client, "reader")
    assert post_collection(client, characteristic_id, instrument_id).status_code == 403


def test_attribute_characteristic_is_rejected(client):
    seed(client)
    signin(client)
    characteristic_id = setup_characteristic(client, "Atributo", "atributo")
    instrument_id = create_instrument(client)
    response = post_collection(client, characteristic_id, instrument_id)
    assert response.status_code == 400
    assert "caracter\u00edsticas cont\u00ednuas" in response.text


def test_exclusion_requires_reason_and_audits_logical_exclusion(client):
    seed(client)
    signin(client)
    characteristic_id = setup_characteristic(client)
    instrument_id = create_instrument(client)
    assert post_collection(client, characteristic_id, instrument_id).status_code == 303
    db = db_for(client)
    subgroup = db.query(Subgroup).one()
    subgroup_id = subgroup.id
    db.close()
    response = client.post(
        f"/coleta/{subgroup_id}/excluir", data={"csrf_token": csrf(client), "reason": ""}
    )
    assert response.status_code == 400
    response = client.post(
        f"/coleta/{subgroup_id}/excluir",
        data={"csrf_token": csrf(client), "reason": "Medição incorreta"},
    )
    assert response.status_code == 303
    db = db_for(client)
    subgroup = db.get(Subgroup, subgroup_id)
    assert subgroup.is_excluded is True
    assert subgroup.exclusion_reason == "Medição incorreta"
    assert db.query(AuditLog).filter_by(action="subgroup.exclude").count() == 1
    db.close()


def test_parse_local_datetime_converts_sao_paulo_to_utc():
    assert parse_local_datetime("2026-10-15T12:00") == datetime(
        2026, 10, 15, 15, 0, tzinfo=timezone.utc
    )
