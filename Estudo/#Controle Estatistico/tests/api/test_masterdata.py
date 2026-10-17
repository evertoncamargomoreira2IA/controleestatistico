from app.models.audit import AuditLog
from app.models.masterdata import Area, Characteristic, Instrument, Line, Product, Station, Tooling
from tests.api.test_auth_users import csrf, db_for, seed, signin


def post_cadastro(client, entity, **fields):
    data = {
        "entity": entity,
        "parent_id": 0,
        "name": "",
        "code": "",
        "description": "",
        "unit": "",
        "lsl": "",
        "nominal": "",
        "usl": "",
        "data_type": "continua",
        "subgroup_size": 1,
        "chart_type": "i_mr",
        "csrf_token": csrf(client),
    }
    data.update(fields)
    return client.post("/cadastros", data=data)


def create_hierarchy(client, prefix="Teste"):
    area = post_cadastro(client, "areas", name=f"{prefix} Área")
    assert area.status_code == 303
    db = db_for(client)
    area_id = db.query(Area).filter_by(name=f"{prefix} Área").one().id
    db.close()

    product = post_cadastro(client, "products", parent_id=area_id, name=f"{prefix} Produto")
    assert product.status_code == 303
    db = db_for(client)
    product_id = db.query(Product).filter_by(name=f"{prefix} Produto").one().id
    db.close()

    line = post_cadastro(client, "lines", parent_id=product_id, name=f"{prefix} Linha")
    assert line.status_code == 303
    db = db_for(client)
    line_id = db.query(Line).filter_by(name=f"{prefix} Linha").one().id
    db.close()

    station = post_cadastro(client, "stations", parent_id=line_id, name=f"{prefix} Posto")
    assert station.status_code == 303
    db = db_for(client)
    station_id = db.query(Station).filter_by(name=f"{prefix} Posto").one().id
    db.close()

    tooling = post_cadastro(
        client, "toolings", parent_id=station_id, name=f"{prefix} Tooling", code=f"{prefix}-TOOL"
    )
    assert tooling.status_code == 303
    db = db_for(client)
    tooling_id = db.query(Tooling).filter_by(code=f"{prefix}-TOOL").one().id
    db.close()
    return area_id, product_id, line_id, station_id, tooling_id


def test_admin_creates_complete_hierarchy_and_characteristic(client):
    seed(client)
    signin(client)
    area_id, product_id, line_id, station_id, tooling_id = create_hierarchy(client)
    response = post_cadastro(
        client,
        "characteristics",
        parent_id=tooling_id,
        name="Diâmetro",
        unit="mm",
        lsl="9.5",
        nominal="10",
        usl="10.5",
        data_type="continua",
        subgroup_size=5,
        chart_type="xbar_r",
    )
    assert response.status_code == 303

    db = db_for(client)
    area = db.get(Area, area_id)
    product = db.get(Product, product_id)
    line = db.get(Line, line_id)
    station = db.get(Station, station_id)
    tooling = db.get(Tooling, tooling_id)
    characteristic = db.query(Characteristic).filter_by(name="Diâmetro").one()
    assert product.area_id == area.id
    assert line.product_id == product.id
    assert station.line_id == line.id
    assert tooling.station_id == station.id
    assert characteristic.tooling_id == tooling.id
    assert (characteristic.data_type, characteristic.chart_type, characteristic.subgroup_size) == (
        "continua",
        "xbar_r",
        5,
    )
    assert (characteristic.lsl, characteristic.nominal, characteristic.usl) == (9.5, 10, 10.5)
    db.close()


def test_characteristic_rejects_lsl_greater_than_or_equal_to_usl(client):
    seed(client)
    signin(client)
    *_, tooling_id = create_hierarchy(client)
    for lsl, usl in ((10, 10), (11, 10)):
        response = post_cadastro(
            client,
            "characteristics",
            parent_id=tooling_id,
            name=f"Limite {lsl}",
            lsl=lsl,
            nominal=10,
            usl=usl,
            subgroup_size=5,
            chart_type="xbar_r",
        )
        assert response.status_code == 400
        assert "LSL deve ser menor que USL" in response.text


def test_characteristic_rejects_nominal_outside_limits(client):
    seed(client)
    signin(client)
    *_, tooling_id = create_hierarchy(client)
    response = post_cadastro(
        client,
        "characteristics",
        parent_id=tooling_id,
        name="Nominal inválido",
        lsl=9,
        nominal=11,
        usl=10,
        subgroup_size=5,
        chart_type="xbar_r",
    )
    assert response.status_code == 400
    assert "Nominal deve estar entre LSL e USL" in response.text


def test_characteristic_rejects_incompatible_subgroup_size(client):
    seed(client)
    signin(client)
    *_, tooling_id = create_hierarchy(client)
    for name, chart_type, subgroup_size in (("IMR", "i_mr", 5), ("Xbar", "xbar_r", 1)):
        response = post_cadastro(
            client,
            "characteristics",
            parent_id=tooling_id,
            name=name,
            chart_type=chart_type,
            subgroup_size=subgroup_size,
        )
        assert response.status_code == 400
    assert "Tamanho do subgrupo incompatível com o tipo de carta" in response.text


def test_characteristic_revision_changes_only_for_specification_edits(client):
    seed(client)
    signin(client)
    *_, tooling_id = create_hierarchy(client)
    assert (
        post_cadastro(
            client,
            "characteristics",
            parent_id=tooling_id,
            name="Medida",
            lsl=9,
            nominal=10,
            usl=11,
        ).status_code
        == 303
    )
    db = db_for(client)
    characteristic = db.query(Characteristic).filter_by(name="Medida").one()
    assert characteristic.revision == 1
    db.close()


def test_characteristic_edit_form_and_save_keep_selection(client):
    seed(client)
    signin(client)
    area_id, product_id, line_id, station_id, tooling_id = create_hierarchy(client)
    assert (
        post_cadastro(
            client,
            "characteristics",
            parent_id=tooling_id,
            name="Editável",
            lsl=9,
            nominal=10,
            usl=11,
        ).status_code
        == 303
    )
    db = db_for(client)
    characteristic = db.query(Characteristic).filter_by(name="Editável").one()
    characteristic_id = characteristic.id
    assert characteristic.revision == 1
    db.close()

    query = (
        f"area_id={area_id}&product_id={product_id}&line_id={line_id}"
        f"&station_id={station_id}&tooling_id={tooling_id}"
        f"&characteristic_id={characteristic_id}"
    )
    response = client.get(f"/cadastros?{query}")
    assert response.status_code == 200
    assert "Editar característica" in response.text
    assert 'name="usl"' in response.text

    response = post_cadastro(
        client,
        "characteristics",
        item_id=characteristic_id,
        name="Editável",
        is_active="true",
        unit="",
        data_type="continua",
        lsl="9",
        nominal="10",
        usl="12",
        subgroup_size="1",
        chart_type="i_mr",
        return_to=f"/cadastros?{query}",
    )
    assert response.status_code == 303
    assert f"characteristic_id={characteristic_id}" in response.headers["location"]
    db = db_for(client)
    characteristic = db.get(Characteristic, characteristic_id)
    assert characteristic.usl == 12
    assert characteristic.revision == 2
    db.close()

    common = {
        "item_id": characteristic_id,
        "is_active": "true",
        "unit": "",
        "data_type": "continua",
        "subgroup_size": 1,
        "chart_type": "i_mr",
        "nominal": 10,
    }
    response = post_cadastro(client, "characteristics", name="Medida", lsl=9, usl=12, **common)
    assert response.status_code == 303
    db = db_for(client)
    characteristic = db.get(Characteristic, characteristic_id)
    assert characteristic.revision == 2
    db.close()

    response = post_cadastro(
        client, "characteristics", name="Medida renomeada", lsl=9, usl=12, **common
    )
    assert response.status_code == 303
    db = db_for(client)
    characteristic = db.get(Characteristic, characteristic_id)
    assert characteristic.name == "Medida renomeada"
    assert characteristic.revision == 2
    db.close()


def test_cannot_deactivate_area_with_active_child(client):
    seed(client)
    signin(client)
    area_id, *_ = create_hierarchy(client)
    response = post_cadastro(client, "areas", item_id=area_id, name="Teste Área", is_active="false")
    assert response.status_code == 400
    assert "Não é possível desativar área com filhos ativos." in response.text


def test_tooling_code_must_be_unique_across_stations(client):
    seed(client)
    signin(client)
    *_, first_station_id, _ = create_hierarchy(client, "Primeiro")
    *_, second_station_id, _ = create_hierarchy(client, "Segundo")
    assert (
        post_cadastro(
            client, "toolings", parent_id=first_station_id, name="Ferramenta A", code="DUPLICADO"
        ).status_code
        == 303
    )
    response = post_cadastro(
        client, "toolings", parent_id=second_station_id, name="Ferramenta B", code="DUPLICADO"
    )
    assert response.status_code == 400
    assert "Código de tooling já cadastrado." in response.text


def test_technician_cannot_create_area_and_reader_can_view_cadastros(client):
    seed(client, "tecnico", "tec")
    signin(client, "tec")
    response = post_cadastro(client, "areas", name="Área proibida")
    assert response.status_code == 403

    seed(client, "leitor", "leitor")
    client.cookies.clear()
    signin(client, "leitor")
    assert client.get("/cadastros").status_code == 200


def test_characteristic_creation_is_audited(client):
    seed(client)
    signin(client)
    *_, tooling_id = create_hierarchy(client)
    response = post_cadastro(
        client, "characteristics", parent_id=tooling_id, name="Auditada", lsl=1, nominal=2, usl=3
    )
    assert response.status_code == 303
    db = db_for(client)
    characteristic = db.query(Characteristic).filter_by(name="Auditada").one()
    entry = (
        db.query(AuditLog)
        .filter_by(entity="characteristics", action="characteristics.create")
        .one()
    )
    assert entry.entity_id == str(characteristic.id)
    db.close()


def post_instrument(client, **fields):
    data = {
        "code": "CAL-001",
        "name": "Paquímetro",
        "description": "Medição dimensional",
        "type": "Paquímetro",
        "resolution": "0.01",
        "calibration_due": "",
        "is_active": "true",
        "item_id": 0,
        "csrf_token": csrf(client),
    }
    data.update(fields)
    return client.post("/cadastros/instrumentos", data=data)


def test_create_instrument(client):
    seed(client)
    signin(client)
    response = post_instrument(client)
    assert response.status_code == 303
    db = db_for(client)
    instrument = db.query(Instrument).filter_by(code="CAL-001").one()
    assert instrument.name == "Paquímetro"
    db.close()


def test_instrument_code_must_be_unique(client):
    seed(client)
    signin(client)
    assert post_instrument(client).status_code == 303
    response = post_instrument(client, name="Outro")
    assert response.status_code == 400
    assert "Código de instrumento já cadastrado." in response.text


def test_technician_cannot_create_instrument(client):
    seed(client, "tecnico", "tec")
    signin(client, "tec")
    assert post_instrument(client).status_code == 403


def test_admin_sees_instrument_form(client):
    seed(client)
    signin(client)
    response = client.get("/cadastros/instrumentos")
    assert response.status_code == 200
    assert "Novo instrumento" in response.text
