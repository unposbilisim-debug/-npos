from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI, File, Form, Request, UploadFile
from fastapi.responses import FileResponse, HTMLResponse, JSONResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from sqlalchemy import desc, select

from app import PRODUCT_NAME, __version__
from app.config import (
    INBOX_DIR,
    SAMPLES_DIR,
    STATIC_DIR,
    TEMPLATES_DIR,
    load_config,
    save_config,
)
from app.database import IngestLog, Shift, get_session, init_db
from app.ingest import ingest_file
from app.reports import export_shift_excel, liter, money, shift_to_dict, summarize_shift
from app.watcher import scan_folder, start_watcher, status as watcher_status, stop_watcher


@asynccontextmanager
async def lifespan(_app: FastAPI):
    init_db()
    start_watcher()
    yield
    stop_watcher()


app = FastAPI(title=PRODUCT_NAME, version=__version__, lifespan=lifespan)
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")
templates = Jinja2Templates(directory=str(TEMPLATES_DIR))
templates.env.filters["money"] = money
templates.env.filters["liter"] = liter


def _ctx(request: Request, **extra):
    config = load_config()
    payload = {
        "request": request,
        "product": PRODUCT_NAME,
        "version": __version__,
        "config": config,
        "watcher": watcher_status(),
    }
    payload.update(extra)
    return payload


@app.get("/", response_class=HTMLResponse)
def dashboard(request: Request):
    db = get_session()
    try:
        shifts = db.scalars(select(Shift).order_by(desc(Shift.id)).limit(8)).all()
        logs = db.scalars(select(IngestLog).order_by(desc(IngestLog.id)).limit(8)).all()
        cards = [shift_to_dict(item) for item in shifts]
        all_shifts = db.scalars(select(Shift)).all()
        totals = {
            "shifts": len(all_shifts),
            "volume": sum(sum(sale.volume for sale in item.sales) for item in all_shifts),
            "amount": sum(sum(sale.amount for sale in item.sales) for item in all_shifts),
            "open": sum(1 for item in all_shifts if item.status == "open"),
        }
        return templates.TemplateResponse(
            "dashboard.html",
            _ctx(request, shifts=cards, logs=logs, totals=totals),
        )
    finally:
        db.close()


@app.get("/vardiyalar", response_class=HTMLResponse)
def shift_list(request: Request):
    db = get_session()
    try:
        shifts = db.scalars(select(Shift).order_by(desc(Shift.shift_date), desc(Shift.id))).all()
        return templates.TemplateResponse(
            "shifts.html",
            _ctx(request, shifts=[shift_to_dict(item) for item in shifts]),
        )
    finally:
        db.close()


@app.get("/vardiya/{shift_id}", response_class=HTMLResponse)
def shift_detail(request: Request, shift_id: int):
    db = get_session()
    try:
        shift = db.get(Shift, shift_id)
        if not shift:
            return RedirectResponse("/vardiyalar", status_code=303)
        return templates.TemplateResponse(
            "shift_detail.html",
            _ctx(request, summary=summarize_shift(shift)),
        )
    finally:
        db.close()


@app.get("/vardiya/{shift_id}/excel")
def shift_excel(shift_id: int):
    db = get_session()
    try:
        shift = db.get(Shift, shift_id)
        if not shift:
            return RedirectResponse("/vardiyalar", status_code=303)
        path = export_shift_excel(shift)
        return FileResponse(
            path=str(path),
            filename=path.name,
            media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        )
    finally:
        db.close()


@app.get("/ayarlar", response_class=HTMLResponse)
def settings_page(request: Request, saved: int = 0):
    return templates.TemplateResponse("settings.html", _ctx(request, saved=bool(saved)))


@app.post("/ayarlar")
def save_settings(
    station_name: str = Form(...),
    watch_path: str = Form(""),
    brand: str = Form("auto"),
    poll_seconds: int = Form(8),
    volume_divisor: int = Form(100),
    amount_divisor: int = Form(100),
    price_divisor: int = Form(100),
    port: int = Form(8787),
):
    config = load_config()
    config.update(
        {
            "station_name": station_name.strip() or "İstasyon",
            "watch_path": watch_path.strip(),
            "brand": brand,
            "poll_seconds": max(3, int(poll_seconds)),
            "volume_divisor": int(volume_divisor),
            "amount_divisor": int(amount_divisor),
            "price_divisor": int(price_divisor),
            "port": int(port),
        }
    )
    save_config(config)
    return RedirectResponse("/ayarlar?saved=1", status_code=303)


@app.post("/tara")
def manual_scan():
    scan_folder()
    return RedirectResponse("/", status_code=303)


@app.post("/yukle")
async def upload_file(file: UploadFile = File(...)):
    INBOX_DIR.mkdir(parents=True, exist_ok=True)
    target = INBOX_DIR / (file.filename or "yuklenen.xml")
    target.write_bytes(await file.read())
    ingest_file(target)
    return RedirectResponse("/vardiyalar", status_code=303)


@app.post("/ornek")
def load_demo():
    for sample in sorted(SAMPLES_DIR.glob("*")):
        if sample.is_file():
            ingest_file(sample, copy_processed=True)
    return RedirectResponse("/", status_code=303)


@app.get("/api/durum")
def api_status():
    return JSONResponse({"watcher": watcher_status(), "config": load_config()})


@app.get("/api/vardiyalar")
def api_shifts():
    db = get_session()
    try:
        shifts = db.scalars(select(Shift).order_by(desc(Shift.id))).all()
        return [shift_to_dict(item) for item in shifts]
    finally:
        db.close()
