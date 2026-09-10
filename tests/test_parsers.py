from pathlib import Path

from app.database import Shift, get_session, init_db
from app.ingest import ingest_file
from app.parsers.asis import parse_asis_text
from app.parsers.turpak import parse_turpak_xml
from app.reports import summarize_shift

ROOT = Path(__file__).resolve().parent.parent
SAMPLES = ROOT / "samples"


def test_turpak_shift_volumes_and_payments():
    parsed = parse_turpak_xml(SAMPLES / "turpak_vardiya.xml")
    assert parsed.shift_date == "2026-09-10"
    assert parsed.shift_no == "2"
    assert parsed.status == "closed"
    assert len(parsed.sales) == 5
    assert round(sum(item.volume for item in parsed.sales), 2) == 189.2
    assert round(sum(item.amount for item in parsed.sales), 2) == 9095.5
    assert parsed.sales[0].payment_code == "100"
    assert parsed.meters[0].nozzle == "1"


def test_live_sales_xml_is_open():
    parsed = parse_turpak_xml(SAMPLES / "Sales.xml")
    assert parsed.status == "open"
    assert parsed.sales[0].volume == 18


def test_asis_text_keeps_decimals():
    parsed = parse_asis_text(SAMPLES / "asis_vardiya.txt")
    assert parsed.brand == "asis"
    assert parsed.shift_no == "3"
    assert parsed.sales[0].volume == 40
    assert parsed.sales[1].amount == 1237.5
    assert parsed.sales[2].attendant == "Can"


def test_ingest_creates_report(tmp_path, monkeypatch):
    monkeypatch.chdir(ROOT)
    init_db()
    shift, result = ingest_file(SAMPLES / "turpak_vardiya.xml", copy_processed=False)
    assert result in {"ok", "skip"}
    assert shift is not None
    db = get_session()
    try:
        stored = db.get(Shift, shift.id)
        summary = summarize_shift(stored)
        assert summary["count"] == 5
        assert "Nakit" in summary["by_payment"]
        assert summary["by_attendant"]["Ali"]["volume"] > 0
    finally:
        db.close()
