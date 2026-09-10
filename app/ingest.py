from __future__ import annotations

import hashlib
import shutil
from datetime import datetime
from pathlib import Path

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import PROCESSED_DIR, load_config
from app.database import IngestLog, Meter, Sale, Shift, get_session
from app.parsers.base import ParsedShift
from app.parsers.detect import parse_automation_file


WATCH_SUFFIXES = {".xml", ".txt", ".csv", ".zip", ".gz"}


def file_hash(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 64), b""):
            digest.update(chunk)
    return digest.hexdigest()


def payment_name(code: str, mapping: dict[str, str]) -> str:
    key = str(code or "").strip()
    return mapping.get(key.upper(), mapping.get(key, key or "Diğer"))


def _apply_shift_rows(db: Session, shift: Shift, parsed: ParsedShift, mapping: dict[str, str]) -> None:
    shift.sales.clear()
    shift.meters.clear()
    db.flush()
    for sale in parsed.sales:
        shift.sales.append(
            Sale(
                seq=sale.seq,
                sold_at=sale.sold_at,
                pump=sale.pump,
                nozzle=sale.nozzle,
                fuel=sale.fuel,
                volume=sale.volume,
                amount=sale.amount,
                unit_price=sale.unit_price,
                attendant=sale.attendant,
                payment_code=sale.payment_code,
                payment_name=payment_name(sale.payment_code, mapping),
                plate=sale.plate,
                customer=sale.customer,
            )
        )
    sales_by_nozzle: dict[tuple[str, str, str], float] = {}
    for sale in parsed.sales:
        key = (sale.pump, sale.nozzle, sale.fuel)
        sales_by_nozzle[key] = sales_by_nozzle.get(key, 0) + sale.volume
    if parsed.meters:
        for meter in parsed.meters:
            sold = sales_by_nozzle.get((meter.pump, meter.nozzle, meter.fuel), 0)
            if not sold:
                sold = sales_by_nozzle.get((meter.pump, meter.nozzle, ""), 0)
            delta = meter.closing - meter.opening
            shift.meters.append(
                Meter(
                    pump=meter.pump,
                    nozzle=meter.nozzle,
                    fuel=meter.fuel,
                    opening=meter.opening,
                    closing=meter.closing,
                    sales_volume=sold,
                    variance=round(delta - sold, 3),
                )
            )
    else:
        for (pump, nozzle, fuel), volume in sorted(sales_by_nozzle.items()):
            shift.meters.append(
                Meter(
                    pump=pump,
                    nozzle=nozzle,
                    fuel=fuel,
                    opening=0,
                    closing=volume,
                    sales_volume=volume,
                    variance=0,
                )
            )


def ingest_file(path: Path, db: Session | None = None, copy_processed: bool = True) -> tuple[Shift | None, str]:
    config = load_config()
    own_session = db is None
    db = db or get_session()
    path = Path(path)
    try:
        digest = file_hash(path)
        existing = db.scalar(select(Shift).where(Shift.source_hash == digest))
        is_live = path.name.lower().startswith("sales")
        if existing and not is_live:
            db.add(IngestLog(path=str(path), status="skip", message="Aynı dosya daha önce işlendi."))
            db.commit()
            return existing, "skip"

        divisors = {
            "volume": float(config.get("volume_divisor") or 100),
            "amount": float(config.get("amount_divisor") or 100),
            "price": float(config.get("price_divisor") or 100),
        }
        parsed = parse_automation_file(path, brand=config.get("brand") or "auto", divisors=divisors)
        parsed.station_name = parsed.station_name or config.get("station_name") or "İstasyon"
        mapping = {str(k).upper(): v for k, v in (config.get("payment_map") or {}).items()}

        shift = existing
        if is_live:
            already_closed = db.scalar(
                select(Shift).where(
                    Shift.status == "closed",
                    Shift.shift_date == parsed.shift_date,
                    Shift.shift_no == parsed.shift_no,
                    Shift.station_name == parsed.station_name,
                )
            )
            if already_closed:
                db.add(
                    IngestLog(
                        path=str(path),
                        status="skip",
                        message="Bu vardiya kapanmış arşivden alındı; canlı dosya yok sayıldı.",
                    )
                )
                db.commit()
                return already_closed, "skip"
            shift = db.scalar(
                select(Shift).where(
                    Shift.status == "open",
                    Shift.shift_date == parsed.shift_date,
                    Shift.station_name == parsed.station_name,
                )
            )
        else:
            open_match = db.scalar(
                select(Shift).where(
                    Shift.status == "open",
                    Shift.shift_date == parsed.shift_date,
                    Shift.shift_no == parsed.shift_no,
                    Shift.station_name == parsed.station_name,
                )
            )
            if open_match:
                shift = open_match
                parsed.status = "closed"
        if shift is None:
            shift = Shift(
                station_name=parsed.station_name,
                shift_date=parsed.shift_date,
                shift_no=parsed.shift_no,
                brand=parsed.brand,
                status=parsed.status,
                source_file=str(path),
                source_hash=digest,
                opened_at=parsed.opened_at,
                closed_at=parsed.closed_at,
            )
            db.add(shift)
        else:
            shift.source_file = str(path)
            shift.source_hash = digest
            shift.shift_no = parsed.shift_no or shift.shift_no
            shift.brand = parsed.brand
            shift.status = parsed.status
            shift.opened_at = parsed.opened_at
            shift.closed_at = parsed.closed_at

        _apply_shift_rows(db, shift, parsed, mapping)
        db.flush()

        if copy_processed:
            PROCESSED_DIR.mkdir(parents=True, exist_ok=True)
            target = PROCESSED_DIR / f"{shift.shift_date}_v{shift.shift_no}_{path.name}"
            if path.resolve() != target.resolve():
                try:
                    shutil.copy2(path, target)
                except OSError:
                    pass

        db.add(IngestLog(path=str(path), status="ok", message=f"{len(parsed.sales)} satış aktarıldı."))
        db.commit()
        db.refresh(shift)
        return shift, "ok"
    except Exception as exc:  # noqa: BLE001 — log and continue watcher
        db.rollback()
        db.add(IngestLog(path=str(path), status="error", message=str(exc)))
        db.commit()
        return None, str(exc)
    finally:
        if own_session:
            db.close()


def is_watch_file(path: Path) -> bool:
    if not path.is_file():
        return False
    name = path.name.lower()
    if name.startswith("."):
        return False
    return path.suffix.lower() in WATCH_SUFFIXES or name.endswith(".xml.gz")
