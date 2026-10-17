from io import BytesIO, StringIO
import csv

from openpyxl import Workbook
from openpyxl.comments import Comment
from openpyxl.styles import Font


FIXED_HEADERS = [
    "tooling_code",
    "characteristic_name",
    "characteristic_id",
    "data_hora",
    "instrumento_codigo",
    "turno",
    "lote",
    "observacao",
]


def _headers(characteristic):
    return FIXED_HEADERS + [
        f"v{position}" for position in range(1, characteristic.subgroup_size + 1)
    ]


def _tooling_code(characteristic, path_labels):
    if getattr(characteristic, "tooling_code", None):
        return characteristic.tooling_code
    if isinstance(path_labels, dict):
        return path_labels.get("tooling_code", path_labels.get("code", ""))
    if path_labels:
        for item in path_labels:
            if hasattr(item, "code"):
                return item.code
    return ""


def _example(characteristic, path_labels):
    return [
        _tooling_code(characteristic, path_labels),
        characteristic.name,
        characteristic.id,
        "15/10/2026 12:00",
        "CAL-001",
        "T1",
        "LOTE-EXEMPLO",
        "Exemplo",
    ] + [10 for _ in range(characteristic.subgroup_size)]


def _specification(characteristic):
    return (
        f"Unidade: {characteristic.unit or '—'}; "
        f"LSL: {characteristic.lsl if characteristic.lsl is not None else '—'}; "
        f"Nominal: {characteristic.nominal if characteristic.nominal is not None else '—'}; "
        f"USL: {characteristic.usl if characteristic.usl is not None else '—'}; "
        f"Revisão: {characteristic.revision}"
    )


def build_collection_template(characteristic, path_labels) -> bytes:
    workbook = Workbook()
    sheet = workbook.active
    sheet.title = "Coleta"
    sheet.append(_headers(characteristic))
    sheet.append(_example(characteristic, path_labels))
    for cell in sheet[2]:
        cell.font = Font(italic=True, color="808080")
        cell.comment = Comment("apague esta linha", "Sistema")
    sheet.freeze_panes = "A2"
    sheet.auto_filter.ref = sheet.dimensions

    instructions = workbook.create_sheet("Instrucoes")
    instructions.append(["Instruções para preenchimento"])
    instructions.append(["Use vírgula ou ponto como separador decimal."])
    instructions.append(["Informe um subgrupo por linha."])
    instructions.append([f"Preencha os {characteristic.subgroup_size} valores obrigatórios."])
    instructions.append(["Use um instrumento ativo e calibrado."])
    instructions.append(["Turno permitido: T1, T2 ou T3."])
    instructions.append([_specification(characteristic)])
    output = BytesIO()
    workbook.save(output)
    return output.getvalue()


def build_collection_template_csv(characteristic) -> bytes:
    output = StringIO(newline="")
    writer = csv.writer(output, delimiter=";", lineterminator="\r\n")
    writer.writerow(_headers(characteristic))
    writer.writerow(_example(characteristic, None))
    return output.getvalue().encode("utf-8-sig")
