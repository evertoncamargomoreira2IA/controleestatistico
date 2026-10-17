from app.services.settings_service import DEFAULTS, classify


def test_classify_capability_boundaries():
    assert classify("cpk", 1.33) == "bom"
    assert classify("cpk", 1.0) == "atencao"
    assert classify("cpk", 0.9999) == "ruim"


def test_classify_gage_rr_boundaries():
    assert classify("gage_rr_study", 10) == "bom"
    assert classify("gage_rr_study", 30) == "atencao"
    assert classify("gage_rr_study", 30.01) == "ruim"


def test_classify_kappa_boundaries():
    assert classify("kappa", 0.90) == "bom"
    assert classify("kappa", 0.75) == "atencao"
    assert classify("kappa", 0.7499) == "ruim"


def test_classify_accepts_active_custom_values():
    values = dict(DEFAULTS)
    values["capability.cpk"] = {"bad_below": 1.2, "good_from": 1.5}
    assert classify("cpk", 1.33, values) == "atencao"
