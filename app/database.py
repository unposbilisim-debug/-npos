from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import DateTime, Float, ForeignKey, Integer, String, Text, create_engine
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship, sessionmaker

from app.config import DB_PATH, ensure_dirs


class Base(DeclarativeBase):
    pass


class Shift(Base):
    __tablename__ = "shifts"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    station_name: Mapped[str] = mapped_column(String(120), default="İstasyon")
    shift_date: Mapped[str] = mapped_column(String(10), index=True)
    shift_no: Mapped[str] = mapped_column(String(20), default="1")
    brand: Mapped[str] = mapped_column(String(20), default="turpak")
    status: Mapped[str] = mapped_column(String(20), default="closed")
    source_file: Mapped[str] = mapped_column(String(500), default="")
    source_hash: Mapped[str] = mapped_column(String(64), unique=True)
    opened_at: Mapped[str | None] = mapped_column(String(32), nullable=True)
    closed_at: Mapped[str | None] = mapped_column(String(32), nullable=True)
    note: Mapped[str] = mapped_column(Text, default="")
    created_at: Mapped[datetime] = mapped_column(DateTime, default=lambda: datetime.now(timezone.utc).replace(tzinfo=None))

    sales: Mapped[list["Sale"]] = relationship(back_populates="shift", cascade="all, delete-orphan")
    meters: Mapped[list["Meter"]] = relationship(back_populates="shift", cascade="all, delete-orphan")


class Sale(Base):
    __tablename__ = "sales"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    shift_id: Mapped[int] = mapped_column(ForeignKey("shifts.id"), index=True)
    seq: Mapped[int] = mapped_column(Integer, default=0)
    sold_at: Mapped[str] = mapped_column(String(32), default="")
    pump: Mapped[str] = mapped_column(String(20), default="")
    nozzle: Mapped[str] = mapped_column(String(20), default="")
    fuel: Mapped[str] = mapped_column(String(40), default="")
    volume: Mapped[float] = mapped_column(Float, default=0)
    amount: Mapped[float] = mapped_column(Float, default=0)
    unit_price: Mapped[float] = mapped_column(Float, default=0)
    attendant: Mapped[str] = mapped_column(String(80), default="")
    payment_code: Mapped[str] = mapped_column(String(40), default="")
    payment_name: Mapped[str] = mapped_column(String(80), default="")
    plate: Mapped[str] = mapped_column(String(20), default="")
    customer: Mapped[str] = mapped_column(String(120), default="")

    shift: Mapped[Shift] = relationship(back_populates="sales")


class Meter(Base):
    __tablename__ = "meters"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    shift_id: Mapped[int] = mapped_column(ForeignKey("shifts.id"), index=True)
    pump: Mapped[str] = mapped_column(String(20), default="")
    nozzle: Mapped[str] = mapped_column(String(20), default="")
    fuel: Mapped[str] = mapped_column(String(40), default="")
    opening: Mapped[float] = mapped_column(Float, default=0)
    closing: Mapped[float] = mapped_column(Float, default=0)
    sales_volume: Mapped[float] = mapped_column(Float, default=0)
    variance: Mapped[float] = mapped_column(Float, default=0)

    shift: Mapped[Shift] = relationship(back_populates="meters")


class IngestLog(Base):
    __tablename__ = "ingest_logs"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    path: Mapped[str] = mapped_column(String(500))
    status: Mapped[str] = mapped_column(String(20))
    message: Mapped[str] = mapped_column(Text, default="")
    created_at: Mapped[datetime] = mapped_column(DateTime, default=lambda: datetime.now(timezone.utc).replace(tzinfo=None))


engine = None
SessionLocal = None


def init_db() -> None:
    global engine, SessionLocal
    ensure_dirs()
    engine = create_engine(f"sqlite:///{DB_PATH}", echo=False, future=True)
    SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True, expire_on_commit=False)
    Base.metadata.create_all(engine)


def get_session():
    if SessionLocal is None:
        init_db()
    return SessionLocal()
