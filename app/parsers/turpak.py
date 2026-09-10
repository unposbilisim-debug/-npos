from __future__ import annotations

import gzip
import io
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from app.parsers.base import (
    AMOUNT_KEYS,
    ATTENDANT_KEYS,
    CLOSE_KEYS,
    CUSTOMER_KEYS,
    FUEL_KEYS,
    NOZZLE_KEYS,
    OPEN_KEYS,
    PAYMENT_KEYS,
    PLATE_KEYS,
    PRICE_KEYS,
    PUMP_KEYS,
    SEQ_KEYS,
    SHIFT_NO_KEYS,
    STATION_KEYS,
    TIME_KEYS,
    VOLUME_KEYS,
    ParsedMeter,
    ParsedSale,
    ParsedShift,
    fold,
    normalize_date,
    pick,
    to_number,
    today_iso,
)

ENCODINGS = ("utf-8-sig", "cp1254", "iso-8859-9", "latin-1")


def _read_bytes(path: Path) -> bytes:
    data = path.read_bytes()
    name = path.name.lower()
    if name.endswith(".gz") or data[:2] == b"\x1f\x8b":
        data = gzip.decompress(data)
    if name.endswith(".zip") or data[:2] == b"PK":
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            xml_names = [item for item in archive.namelist() if item.lower().endswith((".xml", ".txt"))]
            chosen = xml_names[0] if xml_names else archive.namelist()[0]
            data = archive.read(chosen)
    return data


def _parse_xml_root(raw: bytes) -> ET.Element:
    last_error: Exception | None = None
    for encoding in ENCODINGS:
        try:
            text = raw.decode(encoding)
            return ET.fromstring(text)
        except Exception as exc:  # noqa: BLE001 — try next encoding
            last_error = exc
    try:
        return ET.fromstring(raw)
    except Exception as exc:  # noqa: BLE001
        raise ValueError(f"XML okunamadı: {last_error or exc}") from exc


def _local(tag: str) -> str:
    return tag.split("}")[-1]


def _element_map(element: ET.Element) -> dict[str, str]:
    data: dict[str, str] = {}
    for key, value in element.attrib.items():
        data[_local(key)] = value
    if element.text and element.text.strip() and list(element):
        data[_local(element.tag)] = element.text.strip()
    elif element.text and element.text.strip() and not list(element):
        data[_local(element.tag)] = element.text.strip()
    for child in list(element):
        name = _local(child.tag)
        if list(child) or child.attrib:
            nested = _element_map(child)
            for nested_key, nested_value in nested.items():
                data.setdefault(nested_key, nested_value)
        if child.text and child.text.strip():
            data[name] = child.text.strip()
        for key, value in child.attrib.items():
            data.setdefault(_local(key), value)
            data.setdefault(f"{name}_{_local(key)}", value)
    return data


def _looks_like_sale(data: dict[str, str]) -> bool:
    has_volume = any(fold(key) in VOLUME_KEYS for key in data)
    has_amount = any(fold(key) in AMOUNT_KEYS for key in data)
    has_pump = any(fold(key) in PUMP_KEYS | NOZZLE_KEYS for key in data)
    return has_volume and (has_amount or has_pump)


def _looks_like_meter(data: dict[str, str]) -> bool:
    has_open = any(fold(key) in OPEN_KEYS for key in data)
    has_close = any(fold(key) in CLOSE_KEYS for key in data)
    return has_open and has_close


def _walk(element: ET.Element) -> tuple[list[dict[str, str]], list[dict[str, str]], dict[str, str]]:
    sales: list[dict[str, str]] = []
    meters: list[dict[str, str]] = []
    header = _element_map(element)
    tag = fold(_local(element.tag))
    data = _element_map(element)

    if tag in {"satis", "sale", "transaction", "hareket", "fis", "kayit"} or _looks_like_sale(data):
        if _looks_like_sale(data) and not list(element):
            sales.append(data)
        elif _looks_like_sale(data) and not any(_looks_like_sale(_element_map(child)) for child in list(element)):
            sales.append(data)

    if tag in {"endeks", "meter", "sayac", "tabancaendeks", "totalizer"} or _looks_like_meter(data):
        if _looks_like_meter(data):
            meters.append(data)

    for child in list(element):
        child_sales, child_meters, child_header = _walk(child)
        sales.extend(child_sales)
        meters.extend(child_meters)
        for key, value in child_header.items():
            header.setdefault(key, value)
    return sales, meters, header


def _sale_from_map(data: dict[str, str], index: int, divisors: dict[str, float]) -> ParsedSale:
    volume = to_number(pick(data, VOLUME_KEYS), divisors["volume"])
    amount = to_number(pick(data, AMOUNT_KEYS), divisors["amount"])
    unit_price = to_number(pick(data, PRICE_KEYS), divisors["price"])
    if not unit_price and volume:
        unit_price = round(amount / volume, 4)
    seq_raw = pick(data, SEQ_KEYS)
    try:
        seq = int(float(seq_raw)) if seq_raw else index
    except ValueError:
        seq = index
    return ParsedSale(
        seq=seq,
        sold_at=pick(data, TIME_KEYS),
        pump=pick(data, PUMP_KEYS),
        nozzle=pick(data, NOZZLE_KEYS),
        fuel=pick(data, FUEL_KEYS) or "Yakıt",
        volume=volume,
        amount=amount,
        unit_price=unit_price,
        attendant=pick(data, ATTENDANT_KEYS) or "Pompacı",
        payment_code=pick(data, PAYMENT_KEYS) or "NAKIT",
        plate=pick(data, PLATE_KEYS),
        customer=pick(data, CUSTOMER_KEYS),
        raw={k: str(v) for k, v in data.items()},
    )


def _meter_from_map(data: dict[str, str], divisors: dict[str, float]) -> ParsedMeter:
    nozzle = pick(data, NOZZLE_KEYS)
    if not nozzle:
        for key, value in data.items():
            if fold(key) in {"no", "numara", "tabancano"} and str(value).strip():
                nozzle = str(value).strip()
                break
    return ParsedMeter(
        pump=pick(data, PUMP_KEYS),
        nozzle=nozzle,
        fuel=pick(data, FUEL_KEYS),
        opening=to_number(pick(data, OPEN_KEYS), divisors["volume"]),
        closing=to_number(pick(data, CLOSE_KEYS), divisors["volume"]),
    )


def parse_turpak_xml(path: Path, divisors: dict[str, float] | None = None) -> ParsedShift:
    divisors = {
        "volume": float((divisors or {}).get("volume", 100)),
        "amount": float((divisors or {}).get("amount", 100)),
        "price": float((divisors or {}).get("price", 100)),
    }
    raw = _read_bytes(path)
    root = _parse_xml_root(raw)
    sale_maps, meter_maps, header = _walk(root)

    unique_sales: list[dict[str, str]] = []
    seen: set[str] = set()
    for item in sale_maps:
        fingerprint = "|".join(
            [
                pick(item, SEQ_KEYS),
                pick(item, TIME_KEYS),
                pick(item, PUMP_KEYS),
                pick(item, NOZZLE_KEYS),
                pick(item, VOLUME_KEYS),
                pick(item, AMOUNT_KEYS),
            ]
        )
        if fingerprint in seen:
            continue
        seen.add(fingerprint)
        unique_sales.append(item)

    shift = ParsedShift(
        brand="turpak",
        source_name=path.name,
        station_name=pick(header, STATION_KEYS),
        shift_date=normalize_date(pick(header, TIME_KEYS) or path.stem),
        shift_no=pick(header, SHIFT_NO_KEYS) or "1",
        status="open" if path.name.lower().startswith("sales") else "closed",
    )
    if not unique_sales:
        raise ValueError("XML içinde satış kaydı bulunamadı.")
    shift.sales = [_sale_from_map(item, index + 1, divisors) for index, item in enumerate(unique_sales)]
    if shift.sales and not pick(header, TIME_KEYS):
        shift.shift_date = normalize_date(shift.sales[0].sold_at)
    shift.opened_at = shift.sales[0].sold_at or None
    shift.closed_at = shift.sales[-1].sold_at or None
    shift.meters = [_meter_from_map(item, divisors) for item in meter_maps]
    if not shift.shift_date:
        shift.shift_date = today_iso()
    return shift
