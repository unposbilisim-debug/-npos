from __future__ import annotations

import threading
import time
from datetime import datetime
from pathlib import Path

from app.config import load_config
from app.ingest import ingest_file, is_watch_file

_state = {
    "running": False,
    "watch_path": "",
    "last_scan": None,
    "last_file": "",
    "last_result": "Beklemede",
    "files_ok": 0,
    "files_error": 0,
    "message": "Klasör izleme henüz başlamadı.",
}

_stop = threading.Event()
_thread: threading.Thread | None = None
_seen_mtime: dict[str, float] = {}


def status() -> dict:
    return dict(_state)


def _stable(path: Path) -> bool:
    try:
        first = path.stat()
        time.sleep(1.2)
        second = path.stat()
    except OSError:
        return False
    return first.st_size == second.st_size and first.st_size > 0


def scan_folder(watch_path: str | None = None) -> dict:
    config = load_config()
    raw = (watch_path if watch_path is not None else config.get("watch_path") or "").strip()
    _state["watch_path"] = raw
    if not raw:
        _state["message"] = "İzleme klasörü ayarlanmadı."
        _state["last_scan"] = datetime.now().strftime("%H:%M:%S")
        return status()
    folder = Path(raw)
    if not folder.exists() or not folder.is_dir():
        _state["message"] = "İzleme klasörü yok veya erişilemiyor."
        _state["last_scan"] = datetime.now().strftime("%H:%M:%S")
        return status()

    found = sorted(p for p in folder.iterdir() if is_watch_file(p))
    processed = 0
    for path in found:
        try:
            mtime = path.stat().st_mtime
        except OSError:
            continue
        key = str(path)
        is_live = path.name.lower().startswith("sales")
        if not is_live and _seen_mtime.get(key) == mtime:
            continue
        if not _stable(path):
            continue
        shift, result = ingest_file(path)
        _seen_mtime[key] = path.stat().st_mtime if path.exists() else mtime
        _state["last_file"] = path.name
        if result == "ok":
            _state["files_ok"] += 1
            _state["last_result"] = f"Aktarıldı: {path.name}"
            processed += 1
        elif result == "skip":
            _state["last_result"] = f"Atlandı (daha önce işlendi): {path.name}"
        else:
            _state["files_error"] += 1
            _state["last_result"] = f"Hata: {path.name} — {result}"
        if shift:
            processed += 0
    _state["last_scan"] = datetime.now().strftime("%H:%M:%S")
    _state["message"] = f"{len(found)} dosya tarandı, son durum: {_state['last_result']}"
    return status()


def _loop() -> None:
    _state["running"] = True
    _state["message"] = "Klasör izleme çalışıyor."
    while not _stop.is_set():
        config = load_config()
        try:
            scan_folder(config.get("watch_path"))
        except Exception as exc:  # noqa: BLE001
            _state["message"] = f"İzleme hatası: {exc}"
        seconds = max(3, int(config.get("poll_seconds") or 8))
        _stop.wait(seconds)
    _state["running"] = False
    _state["message"] = "Klasör izleme durdu."


def start_watcher() -> None:
    global _thread
    if _thread and _thread.is_alive():
        return
    _stop.clear()
    _thread = threading.Thread(target=_loop, name="vardiya-watcher", daemon=True)
    _thread.start()


def stop_watcher() -> None:
    _stop.set()
