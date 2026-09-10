from __future__ import annotations

import webbrowser
from threading import Timer

import uvicorn

from app.config import ensure_dirs, load_config
from app.database import init_db


def main() -> None:
    ensure_dirs()
    init_db()
    config = load_config()
    host = config.get("host") or "0.0.0.0"
    port = int(config.get("port") or 8787)
    url = f"http://127.0.0.1:{port}"
    Timer(1.2, lambda: webbrowser.open(url)).start()
    uvicorn.run("app.main:app", host=host, port=port, reload=False)


if __name__ == "__main__":
    main()
