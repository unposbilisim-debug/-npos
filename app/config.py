from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from app import PRODUCT_NAME

ROOT_DIR = Path(__file__).resolve().parent.parent
DATA_DIR = ROOT_DIR / "data"
SAMPLES_DIR = ROOT_DIR / "samples"
TEMPLATES_DIR = ROOT_DIR / "templates"
STATIC_DIR = ROOT_DIR / "static"
CONFIG_PATH = DATA_DIR / "config.json"
DB_PATH = DATA_DIR / "vardiya.db"
INBOX_DIR = DATA_DIR / "inbox"
PROCESSED_DIR = DATA_DIR / "processed"
EXPORTS_DIR = DATA_DIR / "exports"

DEFAULT_PAYMENT_MAP = {
    "100": "Nakit",
    "101": "Kredi Kartı",
    "102": "TTS / Filo",
    "103": "Veresiye",
    "104": "Puan / Pont",
    "0": "Nakit",
    "1": "Kredi Kartı",
    "2": "TTS / Filo",
    "3": "Veresiye",
    "NAKIT": "Nakit",
    "NAKIT": "Nakit",
    "CASH": "Nakit",
    "KREDI": "Kredi Kartı",
    "KREDIKARTI": "Kredi Kartı",
    "KART": "Kredi Kartı",
    "CARD": "Kredi Kartı",
    "TTS": "TTS / Filo",
    "FILO": "TTS / Filo",
    "FLEET": "TTS / Filo",
    "VERESIYE": "Veresiye",
    "CARI": "Veresiye",
    "CREDIT": "Veresiye",
    "PUAN": "Puan / Pont",
    "PONT": "Puan / Pont",
}

DEFAULT_CONFIG: dict[str, Any] = {
    "station_name": "İstasyon",
    "watch_path": "",
    "brand": "auto",
    "poll_seconds": 8,
    "volume_divisor": 100,
    "amount_divisor": 100,
    "price_divisor": 100,
    "host": "0.0.0.0",
    "port": 8787,
    "payment_map": DEFAULT_PAYMENT_MAP,
}


def ensure_dirs() -> None:
    for path in (DATA_DIR, INBOX_DIR, PROCESSED_DIR, EXPORTS_DIR, SAMPLES_DIR):
        path.mkdir(parents=True, exist_ok=True)


def load_config() -> dict[str, Any]:
    ensure_dirs()
    if not CONFIG_PATH.exists():
        save_config(DEFAULT_CONFIG)
        return dict(DEFAULT_CONFIG)
    with CONFIG_PATH.open(encoding="utf-8") as handle:
        stored = json.load(handle)
    merged = dict(DEFAULT_CONFIG)
    merged.update(stored)
    payment = dict(DEFAULT_PAYMENT_MAP)
    payment.update({str(k).upper(): v for k, v in (stored.get("payment_map") or {}).items()})
    merged["payment_map"] = payment
    return merged


def save_config(config: dict[str, Any]) -> dict[str, Any]:
    ensure_dirs()
    merged = dict(DEFAULT_CONFIG)
    merged.update(config)
    if "payment_map" in config:
        payment = dict(DEFAULT_PAYMENT_MAP)
        payment.update({str(k).upper(): v for k, v in (config.get("payment_map") or {}).items()})
        merged["payment_map"] = payment
    with CONFIG_PATH.open("w", encoding="utf-8") as handle:
        json.dump(merged, handle, ensure_ascii=False, indent=2)
    return merged


def product_title() -> str:
    return PRODUCT_NAME
