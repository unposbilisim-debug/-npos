from __future__ import annotations

import csv
import io
from pathlib import Path

from app.parsers.base import (
    AMOUNT_KEYS,
    ATTENDANT_KEYS,
    CUSTOMER_KEYS,
    FUEL_KEYS,
    NOZZLE_KEYS,
    PAYMENT_KEYS,
    PLATE_KEYS,
    PRICE_KEYS,
    PUMP_KEYS,
    SEQ_KEYS,
    SHIFT_NO_KEYS,
    STATION_KEYS,
    TIME_KEYS,
    VOLUME_KEYS,
    ParsedSale,
    ParsedShift,
    fold,
    normalize_date,
    to_number,
)

ENCODINGS = ("utf-8-sig", "cp1254", "iso-8859-9", "latin-1")

HEADER_ALIASES = {
    **{key: "volume" for key in VOLUME_KEYS},
    **{key: "amount" for key in AMOUNT_KEYS},
    **{key: "price" for key in PRICE_KEYS},
    **{key: "pump" for key in PUMP_KEYS},
    **{key: "nozzle" for key in NOZZLE_KEYS},
    **{key: "fuel" for key in FUEL_KEYS},
    **{key: "attendant" for key in ATTENDANT_KEYS},
    **{key: "payment" for key in PAYMENT_KEYS},
    **{key: "time" for key in TIME_KEYS},
    **{key: "plate" for key in PLATE_KEYS},
    **{key: "customer" for key in CUSTOMER_KEYS},
    **{key: "seq" for key in SEQ_KEYS},
    **{key: "shift_no" for key in SHIFT_NO_KEYS},
    **{key: "station" for key in STATION_KEYS},
}


def _decode(raw: bytes) -> str:
    for encoding in ENCODINGS:
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    return raw.decode("latin-1", errors="replace")


def _sniff_dialect(sample: str) -> csv.Dialect:
    try:
        return csv.Sniffer().sniff(sample, delimiters=";,\t|")
    except csv.Error:
        class Fallback(csv.Dialect):
            delimiter = ";"
            quotechar = '"'
            doublequote = True
            skipinitialspace = True
            lineterminator = "\n"
            quoting = csv.QUOTE_MINIMAL

        return Fallback()


def parse_asis_text(path: Path, divisors: dict[str, float] | None = None) -> ParsedShift:
    divisors = {
        "volume": float((divisors or {}).get("volume", 1)),
        "amount": float((divisors or {}).get("amount", 1)),
        "price": float((divisors or {}).get("price", 1)),
    }
    text = _decode(path.read_bytes())
    lines = [line for line in text.splitlines() if line.strip() and not line.strip().startswith("#")]
    if not lines:
        raise ValueError("Asis dosyası boş.")
    dialect = _sniff_dialect("\n".join(lines[:10]))
    reader = csv.reader(io.StringIO("\n".join(lines)), dialect)
    rows = list(reader)
    if not rows:
        raise ValueError("Asis dosyasında satır yok.")

    header_row = [fold(cell) for cell in rows[0]]
    has_header = any(cell in HEADER_ALIASES for cell in header_row)
    mapping: dict[int, str] = {}
    data_rows = rows
    if has_header:
        for index, cell in enumerate(header_row):
            if cell in HEADER_ALIASES:
                mapping[index] = HEADER_ALIASES[cell]
        data_rows = rows[1:]
    else:
        default = ["time", "shift_no", "pump", "nozzle", "fuel", "volume", "amount", "price", "attendant", "payment"]
        mapping = {index: name for index, name in enumerate(default)}

    sales: list[ParsedSale] = []
    station = ""
    shift_no = "1"
    shift_date = ""
    for index, row in enumerate(data_rows, start=1):
        if not any(cell.strip() for cell in row):
            continue
        data = {mapping[i]: row[i].strip() for i in range(min(len(row), max(mapping) + 1)) if i in mapping}
        # also keep original header names if present
        raw = {f"col{i}": cell for i, cell in enumerate(row)}
        if has_header:
            for i, name in enumerate(rows[0]):
                if i < len(row):
                    raw[name] = row[i]
        volume = to_number(data.get("volume", ""), divisors["volume"] if "." not in data.get("volume", "") and "," not in data.get("volume", "") else 1)
        amount = to_number(data.get("amount", ""), divisors["amount"] if "." not in data.get("amount", "") and "," not in data.get("amount", "") else 1)
        unit_price = to_number(data.get("price", ""), divisors["price"] if "." not in data.get("price", "") and "," not in data.get("price", "") else 1)
        if not unit_price and volume:
            unit_price = round(amount / volume, 4)
        if not volume and not amount:
            continue
        station = station or data.get("station", "")
        shift_no = data.get("shift_no") or shift_no
        shift_date = shift_date or normalize_date(data.get("time"))
        seq_raw = data.get("seq") or str(index)
        try:
            seq = int(float(seq_raw))
        except ValueError:
            seq = index
        sales.append(
            ParsedSale(
                seq=seq,
                sold_at=data.get("time", ""),
                pump=data.get("pump", ""),
                nozzle=data.get("nozzle", ""),
                fuel=data.get("fuel", "") or "Yakıt",
                volume=volume,
                amount=amount,
                unit_price=unit_price,
                attendant=data.get("attendant", "") or "Pompacı",
                payment_code=data.get("payment", "") or "NAKIT",
                plate=data.get("plate", ""),
                customer=data.get("customer", ""),
                raw=raw,
            )
        )

    if not sales:
        raise ValueError("Asis dosyasında satış satırı bulunamadı.")
    return ParsedShift(
        brand="asis",
        source_name=path.name,
        station_name=station,
        shift_date=shift_date or normalize_date(path.stem),
        shift_no=str(shift_no or "1"),
        status="closed",
        opened_at=sales[0].sold_at or None,
        closed_at=sales[-1].sold_at or None,
        sales=sales,
    )
