from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass, field
from datetime import datetime


def fold(value: str | None) -> str:
    text = (value or "").strip().lower()
    replacements = str.maketrans(
        {
            "ı": "i",
            "İ": "i",
            "ş": "s",
            "ğ": "g",
            "ü": "u",
            "ö": "o",
            "ç": "c",
        }
    )
    text = text.translate(replacements)
    text = unicodedata.normalize("NFKD", text)
    text = "".join(ch for ch in text if not unicodedata.combining(ch))
    return re.sub(r"[^a-z0-9]+", "", text)


VOLUME_KEYS = {
    "miktar",
    "miktari",
    "volume",
    "litre",
    "liter",
    "liters",
    "qty",
    "quantity",
    "satismiktari",
    "fuelvolume",
    "lt",
}
AMOUNT_KEYS = {
    "tutar",
    "tutari",
    "amount",
    "total",
    "satistutari",
    "priceamount",
    "bedel",
}
PRICE_KEYS = {"birimfiyat", "unitprice", "price", "fiyat", "unit", "birim"}
PUMP_KEYS = {"pompa", "pump", "pumpno", "pompano", "pompakodu"}
NOZZLE_KEYS = {"tabanca", "nozzle", "hose", "tabancano", "tabancakodu"}
FUEL_KEYS = {"yakit", "yakitadi", "fuel", "product", "stok", "stokkodu", "grade", "urun"}
ATTENDANT_KEYS = {
    "pompaci",
    "attendant",
    "personel",
    "personelkodu",
    "perskodu",
    "cashier",
    "operator",
    "pn",
}
PAYMENT_KEYS = {"odeme", "odemeturu", "odemekodu", "payment", "paytype", "satistipi", "tip"}
TIME_KEYS = {"tarih", "datetime", "time", "saat", "satistarihi", "zaman", "date"}
PLATE_KEYS = {"plaka", "plate", "vehicle", "arac"}
CUSTOMER_KEYS = {"cari", "customer", "musteri", "kart", "cardno", "filo"}
SEQ_KEYS = {"sira", "seq", "no", "id", "satissirano", "fisno"}
SHIFT_NO_KEYS = {"vardiyano", "vardiyanumarasi", "shiftno", "vardiya", "numarasi", "no"}
STATION_KEYS = {"istasyon", "station", "istasyonadi", "bayi"}
OPEN_KEYS = {"acilis", "opening", "start", "startindex", "acilisendeks"}
CLOSE_KEYS = {"kapanis", "closing", "end", "endindex", "kapanisendeks"}


@dataclass
class ParsedSale:
    seq: int = 0
    sold_at: str = ""
    pump: str = ""
    nozzle: str = ""
    fuel: str = ""
    volume: float = 0
    amount: float = 0
    unit_price: float = 0
    attendant: str = ""
    payment_code: str = ""
    plate: str = ""
    customer: str = ""
    raw: dict[str, str] = field(default_factory=dict)


@dataclass
class ParsedMeter:
    pump: str = ""
    nozzle: str = ""
    fuel: str = ""
    opening: float = 0
    closing: float = 0


@dataclass
class ParsedShift:
    station_name: str = ""
    shift_date: str = ""
    shift_no: str = "1"
    brand: str = "turpak"
    status: str = "closed"
    opened_at: str | None = None
    closed_at: str | None = None
    sales: list[ParsedSale] = field(default_factory=list)
    meters: list[ParsedMeter] = field(default_factory=list)
    source_name: str = ""
    warnings: list[str] = field(default_factory=list)


def pick(data: dict[str, str], keys: set[str]) -> str:
    for key, value in data.items():
        if fold(key) in keys and str(value).strip():
            return str(value).strip()
    return ""


def to_number(value: str | None, divisor: float = 1) -> float:
    if value is None:
        return 0.0
    text = str(value).strip()
    if not text:
        return 0.0
    text = text.replace(" ", "")
    if "," in text and "." in text:
        if text.rfind(",") > text.rfind("."):
            text = text.replace(".", "").replace(",", ".")
        else:
            text = text.replace(",", "")
    elif "," in text:
        text = text.replace(".", "").replace(",", ".")
    try:
        number = float(text)
    except ValueError:
        digits = re.sub(r"[^0-9,.-]", "", text).replace(",", ".")
        try:
            number = float(digits)
        except ValueError:
            return 0.0
    if divisor and divisor != 1 and "." not in str(value) and "," not in str(value):
        number = number / divisor
    return number


def today_iso() -> str:
    return datetime.now().strftime("%Y-%m-%d")


def normalize_date(value: str | None) -> str:
    text = (value or "").strip()
    if not text:
        return today_iso()
    for fmt in (
        "%Y-%m-%d",
        "%Y-%m-%dT%H:%M:%S",
        "%d.%m.%Y",
        "%d/%m/%Y",
        "%Y%m%d",
        "%d-%m-%Y",
        "%Y.%m.%d",
    ):
        try:
            return datetime.strptime(text[:19], fmt).strftime("%Y-%m-%d")
        except ValueError:
            continue
    digits = re.sub(r"\D", "", text)
    if len(digits) >= 8:
        try:
            return datetime.strptime(digits[:8], "%Y%m%d").strftime("%Y-%m-%d")
        except ValueError:
            try:
                return datetime.strptime(digits[:8], "%d%m%Y").strftime("%Y-%m-%d")
            except ValueError:
                pass
    return today_iso()
