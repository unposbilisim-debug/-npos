from __future__ import annotations

from pathlib import Path

from app.parsers.asis import parse_asis_text
from app.parsers.base import ParsedShift
from app.parsers.turpak import parse_turpak_xml


def detect_brand(path: Path, preferred: str = "auto") -> str:
    if preferred in {"turpak", "asis"}:
        return preferred
    name = path.name.lower()
    suffix = path.suffix.lower()
    if "asis" in name or suffix in {".txt", ".csv"}:
        return "asis"
    return "turpak"


def parse_automation_file(
    path: Path,
    brand: str = "auto",
    divisors: dict[str, float] | None = None,
) -> ParsedShift:
    chosen = detect_brand(path, brand)
    if chosen == "asis":
        try:
            return parse_asis_text(path, divisors)
        except Exception:
            if brand == "asis":
                raise
            return parse_turpak_xml(path, divisors)
    try:
        return parse_turpak_xml(path, divisors)
    except Exception:
        if brand == "turpak":
            raise
        return parse_asis_text(path, divisors)
