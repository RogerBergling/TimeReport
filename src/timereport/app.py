"""Application bootstrap and command-line entry point."""

from __future__ import annotations

import argparse
import logging
import sys
import tkinter as tk
from logging.handlers import RotatingFileHandler
from pathlib import Path
from tkinter import messagebox

from . import __version__
from .db import Database
from .paths import database_path, default_data_dir, ensure_data_dir
from .service import TimeReportService
from .ui.main_window import MainWindow

LOG_FILENAME = "timereport.log"
logger = logging.getLogger(__name__)


def _setup_logging(data_dir: Path) -> None:
    log_path = data_dir / LOG_FILENAME
    root_logger = logging.getLogger()
    root_logger.setLevel(logging.INFO)

    if not any(isinstance(handler, RotatingFileHandler) for handler in root_logger.handlers):
        file_handler = RotatingFileHandler(
            log_path,
            maxBytes=1_000_000,
            backupCount=3,
            encoding="utf-8",
        )
        file_handler.setFormatter(
            logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")
        )
        root_logger.addHandler(file_handler)
        try:
            log_path.chmod(0o600)
        except OSError:
            pass

    if sys.stderr is not None and not any(
        type(handler) is logging.StreamHandler for handler in root_logger.handlers
    ):
        stream_handler = logging.StreamHandler()
        stream_handler.setFormatter(
            logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")
        )
        root_logger.addHandler(stream_handler)


def _enable_high_dpi() -> None:
    if sys.platform.startswith("win"):
        try:
            import ctypes

            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except Exception:
            pass


def _show_fatal(message: str, log_path: Path) -> None:
    try:
        root = tk.Tk()
        root.withdraw()
        messagebox.showerror(
            "TimeReport",
            f"{message}\n\nDetails are written to:\n{log_path}",
            parent=root,
        )
        root.destroy()
    except Exception:
        if sys.stderr is not None:
            print(f"{message}\nLog: {log_path}", file=sys.stderr)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="TimeReport",
        description="Local-first desktop time reporting.",
    )
    parser.add_argument(
        "--version",
        action="version",
        version=f"TimeReport {__version__}",
    )
    parser.add_argument(
        "--data-dir",
        type=Path,
        help="Override the folder used for the database, backups and log.",
    )
    parser.add_argument(
        "--smoke-test",
        action="store_true",
        help=argparse.SUPPRESS,
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    data_dir = (
        args.data_dir.expanduser() if args.data_dir is not None else default_data_dir()
    ).resolve()
    data_dir = ensure_data_dir(data_dir)
    _setup_logging(data_dir)
    _enable_high_dpi()

    database = Database(database_path(data_dir))
    try:
        database.connect()
    except Exception as exc:
        logger.exception("Could not open the database")
        if not args.smoke_test:
            _show_fatal(f"Could not open the database:\n{exc}", data_dir / LOG_FILENAME)
        return 1

    root: tk.Tk | None = None
    try:
        root = tk.Tk()

        def report_callback_exception(
            exc_type: type[BaseException],
            exc_value: BaseException,
            exc_traceback: object,
        ) -> None:
            logger.exception("Unhandled UI error", exc_info=(exc_type, exc_value, exc_traceback))
            if args.smoke_test:
                return
            messagebox.showerror(
                "TimeReport",
                f"An unexpected error occurred:\n{exc_value}\n\n"
                f"Details are written to:\n{data_dir / LOG_FILENAME}",
                parent=root,
            )

        root.report_callback_exception = report_callback_exception
        MainWindow(root, TimeReportService(database), data_dir)
        if args.smoke_test:
            root.after(500, root.destroy)
        root.mainloop()
        return 0
    except Exception as exc:
        logger.exception("Fatal startup error")
        if not args.smoke_test:
            _show_fatal(f"TimeReport could not start:\n{exc}", data_dir / LOG_FILENAME)
        return 1
    finally:
        if root is not None:
            try:
                root.destroy()
            except Exception:
                pass
        database.close()
