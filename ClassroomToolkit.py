# -*- coding: utf-8 -*-
from __future__ import annotations

import base64
import configparser
import contextlib
import ctypes
from ctypes import wintypes
import filecmp
import importlib
import io
import json
import logging
from logging.handlers import RotatingFileHandler
import math
import os
import hashlib
import random
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import functools
import uuid
from collections import OrderedDict, deque
from functools import singledispatch
from queue import Empty, Queue
from dataclasses import dataclass, field
from enum import Enum
from typing import (
    TYPE_CHECKING,
    Any,
    Callable,
    Dict,
    Iterable,
    List,
    Mapping,
    Optional,
    Set,
    Tuple,
    Union,
    Literal,
    FrozenSet,
    cast,
    TypeVar,
)

logger = logging.getLogger(__name__)

RectTuple = Tuple[int, int, int, int]
SHAPE_TYPES = {"line", "dashed_line", "rect", "rect_fill", "circle"}

# ---------- 应用生命周期 ----------
# 单文件项目也建议统一维护"退出闸门"，避免窗口销毁后后台回调继续触发造成偶发崩溃。
_APP_CLOSING_LOCK = threading.Lock()
_APP_CLOSING = False
LOG_FILENAME = "classroom_toolkit.log"
QT_PLATFORM_PLUGIN_NAME = "qwindows.dll"
LOG_MAX_BYTES = 2 * 1024 * 1024
LOG_BACKUP_COUNT = 3
ROLL_CALL_IMMEDIATE_SAVE_COOLDOWN_SECONDS = 2.0
SETTINGS_AUTOSAVE_INTERVAL_SECONDS = 15
WRITABLE_DIR_CACHE_TTL_SECONDS = 3.0
PHOTO_INDEX_CACHE_TTL_SECONDS = 8.0
PHOTO_INDEX_SCAN_LIMIT = 3000
_LOG_PATH: Optional[str] = None
_WRITABLE_DIR_CACHE: Dict[str, float] = {}


# ---------- UI 常量 ----------
class UIConstants:
    """UI 相关常量集中管理"""

    # 尺寸常量
    ICON_SIZE = 28
    BUTTON_MIN_WIDTH = 80
    BUTTON_MIN_HEIGHT = 30
    CONTROL_PADDING = 6

    # 字体大小范围
    MIN_FONT_SIZE = 5
    MAX_FONT_SIZE = 220
    DEFAULT_ID_FONT_SIZE = 48
    DEFAULT_NAME_FONT_SIZE = 60
    DEFAULT_TIMER_FONT_SIZE = 56

    # 计时器相关
    TIMER_UPDATE_INTERVAL_MS = 100
    TIMER_DEFAULT_MINUTES = 5
    TIMER_MIN_DURATION = 1
    TIMER_MAX_DURATION = 9999

    # 照片显示
    PHOTO_DEFAULT_DURATION_SECONDS = 0

    # 画笔相关
    BRUSH_MIN_SIZE = 1.0
    BRUSH_MAX_SIZE = 50.0
    BRUSH_DEFAULT_SIZE = 12.0
    ERASER_DEFAULT_SIZE = 24.0
    ERASER_MAX_SIZE = 50.0

    # 不透明度范围
    OPACITY_MIN = 0
    OPACITY_MAX = 255

    # 文件操作重试
    FILE_WRITE_MAX_RETRIES = 3
    FILE_WRITE_RETRY_BASE_DELAY_MS = 100


# ---------- 边界验证常量 ----------
class ValidationConstants:
    """输入验证相关常量"""

    # 字符串长度限制
    MAX_CLASS_NAME_LENGTH = 100
    MAX_STUDENT_NAME_LENGTH = 100
    MAX_GROUP_NAME_LENGTH = 50

    # 数值范围
    MIN_VOICE_VOLUME = 0.0
    MAX_VOICE_VOLUME = 1.0
    MIN_VOICE_RATE = 100
    MAX_VOICE_RATE = 300

    # 列表大小限制
    MAX_QUICK_COLORS = 10
    MAX_RECENT_FILES = 20


def begin_app_shutdown(reason: str = "") -> bool:
    """标记应用进入退出流程；返回 True 表示本次调用首次触发。"""

    global _APP_CLOSING
    with _APP_CLOSING_LOCK:
        if _APP_CLOSING:
            return False
        _APP_CLOSING = True
    if reason:
        logger.info("Application shutdown started: %s", reason)
    return True


def is_app_closing() -> bool:
    with _APP_CLOSING_LOCK:
        return bool(_APP_CLOSING)


def ensure_rollcall_dependencies(parent: Optional["QWidget"]) -> bool:
    """检查点名功能依赖是否可用，并在缺失时提示。"""

    if PANDAS_AVAILABLE and OPENPYXL_AVAILABLE:
        return True
    QMessageBox.warning(parent, "提示", "未安装 pandas/openpyxl，点名功能不可用。")
    return False


def _find_system_dll(dll_name: str) -> Optional[str]:
    system_root = os.environ.get("SystemRoot", "")
    if system_root:
        for folder in ("System32", "SysWOW64"):
            path = os.path.join(system_root, folder, dll_name)
            if os.path.isfile(path):
                return path
    return None


def _windows_major_version() -> Optional[int]:
    if sys.platform != "win32":
        return None
    try:
        return sys.getwindowsversion().major
    except Exception:
        return None


def collect_system_diagnostics() -> Tuple[bool, str, str, str]:
    """收集运行环境诊断信息，供用户排查兼容性问题。"""

    import platform

    lines: List[str] = []
    issues: List[str] = []
    fixes: List[str] = []

    lines.append(f"平台：{sys.platform}")
    lines.append(f"系统：{platform.platform()}")
    lines.append(f"架构：{platform.machine()} Python={platform.architecture()[0]}")
    lines.append(f"Python：{sys.version.splitlines()[0]}")
    lines.append(f"可执行文件：{sys.executable}")
    lines.append(f"工作目录：{os.getcwd()}")
    lines.append(f"打包模式：{'frozen' if getattr(sys, 'frozen', False) else 'source'}")
    try:
        import PyQt6  # type: ignore

        lines.append(f"PyQt6：{getattr(PyQt6, '__version__', 'unknown')}")
    except Exception:
        lines.append("PyQt6：unknown")

    def _flag(name: str, ok: bool) -> None:
        lines.append(f"{name}：{'OK' if ok else '缺失/不可用'}")

    _flag("pandas", bool(PANDAS_AVAILABLE))
    _flag("openpyxl", bool(OPENPYXL_AVAILABLE))
    _flag("sounddevice", bool(SOUNDDEVICE_AVAILABLE))
    _flag("numpy", bool(np is not None))
    _flag("pyttsx3", bool(pyttsx3 is not None))
    _flag("comtypes", bool(comtypes is not None))
    _flag("pywin32(win32gui)", bool(win32gui is not None))

    if not (PANDAS_AVAILABLE and OPENPYXL_AVAILABLE):
        issues.append("点名功能不可用：缺少 pandas/openpyxl。")
        fixes.append("安装：python -m pip install pandas openpyxl")
    if not SOUNDDEVICE_AVAILABLE:
        issues.append("倒计时提示音可能不可用：缺少 sounddevice/numpy 或设备不可用。")
        fixes.append("安装：python -m pip install numpy sounddevice")

    # 配置与数据文件路径可写性
    try:
        settings_path = _probe_settings_path()
        settings_dir = os.path.dirname(settings_path) or os.getcwd()
        lines.append(f"设置文件：{settings_path}")
        if os.path.isdir(settings_dir):
            writable = _is_writable_directory(settings_dir)
            lines.append(f"设置目录可写：{'OK' if writable else '否'}")
        else:
            writable = False
            lines.append("设置目录：不存在")
        if not writable:
            issues.append("设置目录不可写：配置无法保存。")
            fixes.append("请将程序放到可写目录（如用户目录/桌面），或以管理员身份运行。")
    except Exception as exc:
        lines.append(f"设置路径检测失败：{exc}")

    try:
        students_path = _STUDENT_RESOURCES.plain
        lines.append(f"学生数据文件（默认）：{students_path}")
        if os.path.exists(students_path):
            lines.append("学生数据文件：存在")
        else:
            lines.append("学生数据文件：不存在（首次会自动生成模板）")
    except Exception as exc:
        lines.append(f"学生数据路径检测失败：{exc}")

    try:
        photos_root, _roots = _probe_student_photo_roots()
        lines.append(f"照片根目录：{photos_root}")
    except Exception as exc:
        lines.append(f"照片目录检测失败：{exc}")

    if sys.platform == "win32":
        qt_platform_dir, qt_candidates = _find_qt_platform_plugin_dir()
        if qt_platform_dir:
            lines.append(f"Qt平台插件目录：{qt_platform_dir}")
        else:
            lines.append("Qt平台插件目录：未找到 qwindows.dll")
            if qt_candidates:
                lines.append(f"Qt平台插件搜索路径：{'; '.join(qt_candidates)}")
            issues.append("Qt 平台插件未找到：qwindows.dll。")
            fixes.append("请确保打包输出中存在 platforms/qwindows.dll 且与 exe 同级。")

        # 检查常用运行库
        for dll_name in ("vcruntime140.dll", "msvcp140.dll"):
            try:
                ctypes.CDLL(dll_name)
            except OSError:
                lines.append(f"{dll_name}：缺失")
                issues.append(f"系统缺失运行库：{dll_name}。")
                fixes.append("请安装 Visual C++ Redistributable for Visual Studio 2015-2022 (x86/x64)。")
            except Exception:
                pass
        win_major = _windows_major_version()
        if win_major is not None and win_major < 10:
            if not _find_system_dll("api-ms-win-core-path-l1-1-0.dll"):
                issues.append("系统缺失通用运行库：api-ms-win-core-path-l1-1-0.dll。")
                fixes.append("请安装 KB2999226 或 Visual C++ Redistributable (2015-2022)。")
        if not _find_system_dll("d3dcompiler_47.dll"):
            issues.append("系统缺失图形运行库：d3dcompiler_47.dll。")
            fixes.append("请安装系统补丁 KB4019990 或更新显卡/系统组件。")

        hook_ok = bool(_USER32 and hasattr(_USER32, "SetWindowsHookExW"))
        _flag("Win32键盘钩子", hook_ok)
        if not hook_ok:
            issues.append("翻页笔遥控点名不可用：Win32 Hook 不可用。")
            fixes.append("检查系统组件是否完整，或在安全软件中放行；必要时以管理员运行。")

        voice_count, voice_err = _count_windows_voice_tokens()
        if voice_count >= 0:
            lines.append(f"Windows语音包（注册表Tokens）：{voice_count}")
        if voice_err:
            lines.append(f"Windows语音包检测错误：{voice_err}")
        driver_issue = _detect_pyttsx3_driver_issue()
        if driver_issue:
            lines.append(f"pyttsx3驱动：{driver_issue}")
            issues.append("语音播报可能不可用：pyttsx3 SAPI5 驱动异常或缺失。")
            fixes.append("安装：python -m pip install pyttsx3 comtypes pywin32")
            fixes.append("在 Windows“语音/讲述人/语言包”中安装中文语音（SAPI5）后重启。")

        ps = _find_powershell_executable()
        lines.append(f"PowerShell：{ps or '未找到'}")
        ok, err = _probe_powershell_speech_runtime(ps)
        lines.append(f"PowerShell语音可用：{'OK' if ok else '不可用'}")
        if err:
            lines.append(f"PowerShell语音错误：{err}")
            issues.append("PowerShell 语音回退不可用。")
            fixes.append("检查系统是否能加载 System.Speech（部分精简/策略系统会禁用）。")

        cache_dir = os.environ.get("COMTYPES_CACHE_DIR", "").strip()
        if cache_dir:
            lines.append(f"COMTYPES_CACHE_DIR：{cache_dir}")
            if os.path.isdir(cache_dir):
                can_write = _ensure_writable_directory(cache_dir)
                lines.append(f"COMTYPES缓存目录可写：{'OK' if can_write else '否'}")
                if not can_write:
                    issues.append("comtypes 缓存目录不可写：语音初始化可能失败。")
                    fixes.append("将 COMTYPES_CACHE_DIR 指向可写目录，或以管理员运行。")

    has_issues = bool(issues)
    
    title = "系统兼容性诊断"
    detail = "\n".join(lines)
    
    suggest_lines: List[str] = []
    if issues:
        suggest_lines.extend(f"问题：{item}" for item in dedupe_strings(issues))
        if fixes:
            suggest_lines.append("")
            suggest_lines.append("解决建议：")
            suggest_lines.extend(f"· {item}" for item in dedupe_strings(fixes))
    else:
        suggest_lines.append("✅ 当前系统环境良好，所有关键依赖检测通过。")

    suggest = "\n".join(suggest_lines).strip()
    return has_issues, title, detail, suggest


def collect_quick_diagnostics() -> Tuple[bool, str, str, str]:
    """执行启动阶段的轻量兼容性检查，避免耗时探测阻塞 UI。"""

    lines: List[str] = []
    issues: List[str] = []
    fixes: List[str] = []

    lines.append(f"平台：{sys.platform}")
    lines.append(f"打包模式：{'frozen' if getattr(sys, 'frozen', False) else 'source'}")

    if sys.platform == "win32":
        qt_platform_dir, qt_candidates = _find_qt_platform_plugin_dir()
        if qt_platform_dir:
            lines.append(f"Qt平台插件目录：{qt_platform_dir}")
        else:
            lines.append("Qt平台插件目录：未找到 qwindows.dll")
            if qt_candidates:
                lines.append(f"Qt平台插件搜索路径：{'; '.join(qt_candidates)}")
            issues.append("Qt 平台插件未找到：qwindows.dll。")
            fixes.append("请确保打包输出中存在 platforms/qwindows.dll 且与 exe 同级。")

        runtime_missing = []
        for dll_name in ("vcruntime140.dll", "msvcp140.dll", "ucrtbase.dll"):
            try:
                ctypes.CDLL(dll_name)
            except OSError:
                runtime_missing.append(dll_name)
        if runtime_missing:
            issues.append("系统缺少 VC 运行库/通用运行时。")
            fixes.append("请安装 Visual C++ Redistributable (2015-2022)。")
            lines.extend(f"{name}：缺失" for name in runtime_missing)

        win_major = _windows_major_version()
        if win_major is not None and win_major < 10:
            if not _find_system_dll("api-ms-win-core-path-l1-1-0.dll"):
                issues.append("系统缺失通用运行库：api-ms-win-core-path-l1-1-0.dll。")
                fixes.append("请安装 KB2999226 或 Visual C++ Redistributable (2015-2022)。")

        if not _find_system_dll("d3dcompiler_47.dll"):
            issues.append("系统缺失图形运行库：d3dcompiler_47.dll。")
            fixes.append("请安装系统补丁 KB4019990 或更新显卡/系统组件。")

    has_issues = bool(issues)
    title = "启动快速检查"
    detail = "\n".join(lines)

    suggest_lines: List[str] = []
    if issues:
        suggest_lines.extend(f"问题：{item}" for item in dedupe_strings(issues))
        if fixes:
            suggest_lines.append("")
            suggest_lines.append("解决建议：")
            suggest_lines.extend(f"· {item}" for item in dedupe_strings(fixes))
    else:
        suggest_lines.append("✅ 启动快速检查通过。")
    suggest = "\n".join(suggest_lines).strip()
    return has_issues, title, detail, suggest


def show_diagnostic_result(parent: Optional[QWidget], result: Tuple[bool, str, str, str]) -> None:
    """显示系统诊断结果对话框，使用自定义 QDialog 以完全控制交互行为。"""
    has_issues, title, details, suggestions = result
    from PyQt6.QtWidgets import QStyle  # 局部导入以避免循环依赖或顶层修改

    # 使用 QDialog 而非 QMessageBox，以解决按钮自动关闭和布局限制问题
    dlg = QDialog(parent)
    dlg.setWindowTitle(title)
    # 启用最小化/最大化和关闭按钮，允许调整大小
    dlg.setWindowFlags(dlg.windowFlags() | Qt.WindowType.WindowMinMaxButtonsHint | Qt.WindowType.WindowCloseButtonHint)
    dlg.setSizeGripEnabled(True)
    dlg.resize(520, 180)  # 初始尺寸

    main_layout = QVBoxLayout(dlg)
    main_layout.setContentsMargins(20, 20, 20, 20)
    main_layout.setSpacing(15)

    # 顶部区域：图标 + 简要提示
    top_layout = QHBoxLayout()
    top_layout.setSpacing(15)
    
    icon_label = QLabel(dlg)
    icon_pixmap = QApplication.style().standardIcon(
        QStyle.StandardPixmap.SP_MessageBoxWarning if has_issues else QStyle.StandardPixmap.SP_MessageBoxInformation
    ).pixmap(48, 48)
    icon_label.setPixmap(icon_pixmap)
    icon_label.setAlignment(Qt.AlignmentFlag.AlignTop)
    
    msg_label = QLabel(dlg)
    msg_label.setWordWrap(True)
    msg_label.setText("检测到系统环境存在潜在兼容性问题。" if has_issues else "系统环境检测正常。")
    # 使用相对通用的字体设置，增加字号
    msg_label.setStyleSheet("font-weight: bold; font-size: 14px; color: #303133;")
    msg_label.setAlignment(Qt.AlignmentFlag.AlignVCenter | Qt.AlignmentFlag.AlignLeft)

    top_layout.addWidget(icon_label)
    top_layout.addWidget(msg_label, 1)
    main_layout.addLayout(top_layout)

    # 详细信息区域 (默认隐藏)
    full_message = details
    if suggestions:
        full_message = f"{full_message}\n\n建议：\n{suggestions}"

    text_edit = QPlainTextEdit(dlg)
    text_edit.setPlainText(full_message)
    text_edit.setReadOnly(True)
    text_edit.setVisible(False)
    text_edit.setMinimumHeight(250)
    # 样式优化
    text_edit.setStyleSheet(
        """
        QPlainTextEdit {
            background-color: #fafbfc;
            color: #606266;
            border: 1px solid #dcdfe6;
            border-radius: 6px;
            font-family: "Consolas", "Microsoft YaHei UI", monospace;
            font-size: 12px;
        }
        """
    )
    main_layout.addWidget(text_edit, 1) # 1 的拉伸因子允许其占据多余空间

    # 底部按钮区域
    btn_layout = QHBoxLayout()
    
    # 详细信息 (左侧)
    detail_btn = QPushButton("详细信息", dlg)
    detail_btn.setCheckable(True)
    detail_btn.setCursor(Qt.CursorShape.PointingHandCursor)
    
    # 复制信息 (右侧)
    copy_btn = QPushButton("复制信息", dlg)
    copy_btn.setCursor(Qt.CursorShape.PointingHandCursor)

    btn_layout.addWidget(detail_btn)
    btn_layout.addStretch()
    btn_layout.addWidget(copy_btn)
    
    main_layout.addLayout(btn_layout)

    # 交互逻辑
    def on_toggle_details(checked: bool) -> None:
        text_edit.setVisible(checked)
        if checked:
            # 展开时若高度不够则自动增高
            if dlg.height() < 400:
                dlg.resize(dlg.width(), 450)
        else:
            # 收起时恢复紧凑高度，但保留宽度
            dlg.resize(dlg.width(), 180)

    detail_btn.toggled.connect(on_toggle_details)

    def on_copy() -> None:
        copy_text = f"{title}\n\n{full_message}"
        QApplication.clipboard().setText(copy_text)
        # 简单的视觉反馈
        orig_text = copy_btn.text()
        copy_btn.setText("已复制 ✓")
        copy_btn.setEnabled(False)
        def _restore_copy_button() -> None:
            try:
                copy_btn.setText(orig_text)
                copy_btn.setEnabled(True)
            except RuntimeError:
                # 对话框已销毁时忽略回调
                return

        QTimer.singleShot(1500, _restore_copy_button)

    copy_btn.clicked.connect(on_copy)

    # 全局样式微调 (针对该对话框)
    dlg.setStyleSheet(StyleConfig.DIAGNOSTIC_DIALOG_STYLE)

    dlg.exec()


if sys.platform == "win32":
    try:  # pragma: no cover - Windows 专用依赖
        import win32api
        import win32con
        import win32gui
    except ImportError:  # pragma: no cover - 部分环境未安装 pywin32
        win32api = None  # type: ignore[assignment]
        win32con = None  # type: ignore[assignment]
        win32gui = None  # type: ignore[assignment]
    try:
        _USER32 = ctypes.windll.user32  # type: ignore[attr-defined]
    except Exception:  # pragma: no cover - 某些环境可能限制 Win32 API
        _USER32 = None  # type: ignore[assignment]
    try:
        _KERNEL32 = ctypes.windll.kernel32  # type: ignore[attr-defined]
    except Exception:  # pragma: no cover - 某些环境可能限制 Win32 API
        _KERNEL32 = None  # type: ignore[assignment]
    try:
        _PSAPI = ctypes.windll.psapi  # type: ignore[attr-defined]
    except Exception:  # pragma: no cover - 某些环境可能限制 Win32 API
        _PSAPI = None  # type: ignore[assignment]
else:
    win32api = None  # type: ignore[assignment]
    win32con = None  # type: ignore[assignment]
    win32gui = None  # type: ignore[assignment]
    _USER32 = None  # type: ignore[assignment]
    _KERNEL32 = None  # type: ignore[assignment]
    _PSAPI = None  # type: ignore[assignment]


def _safe_set_prototype(func: Any, *, argtypes: Optional[list] = None, restype: Any = None) -> None:
    """在可用时为 ctypes 函数设置签名，失败时静默跳过。"""

    if func is None:
        return
    try:
        if argtypes is not None:
            func.argtypes = argtypes
        if restype is not None:
            func.restype = restype
    except Exception:
        pass


def _ensure_winapi_types() -> None:
    """为缺失的 WinAPI 类型提供兼容定义。"""

    if not hasattr(wintypes, "LRESULT"):
        wintypes.LRESULT = ctypes.c_long  # type: ignore[attr-defined]
    if not hasattr(wintypes, "HHOOK"):
        wintypes.HHOOK = wintypes.HANDLE  # type: ignore[attr-defined]
    if not hasattr(wintypes, "HMODULE"):
        wintypes.HMODULE = wintypes.HANDLE  # type: ignore[attr-defined]


_ensure_winapi_types()
HHOOK = HINSTANCE = HMODULE = wintypes.HANDLE
_HOOKPROC_TYPE = ctypes.WINFUNCTYPE(wintypes.LRESULT, ctypes.c_int, wintypes.WPARAM, wintypes.LPARAM)
_WNDENUMPROC: Optional[ctypes.WINFUNCTYPE] = None


def _configure_winapi_prototypes() -> None:
    """集中设置 Win32 API 函数签名，避免重复代码与句柄截断。"""

    global _WNDENUMPROC
    _ensure_winapi_types()

    _WNDENUMPROC = (
        ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        if _USER32 is not None
        else None
    )

    _safe_set_prototype(
        getattr(_KERNEL32, "GetModuleHandleW", None),
        argtypes=[wintypes.LPCWSTR],
        restype=wintypes.HMODULE,
    )
    _safe_set_prototype(
        getattr(_KERNEL32, "GetCurrentThreadId", None),
        restype=wintypes.DWORD,
    )
    _safe_set_prototype(
        getattr(_KERNEL32, "OpenProcess", None),
        argtypes=[wintypes.DWORD, wintypes.BOOL, wintypes.DWORD],
        restype=wintypes.HANDLE,
    )
    _safe_set_prototype(
        getattr(_KERNEL32, "CloseHandle", None),
        argtypes=[wintypes.HANDLE],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_KERNEL32, "QueryFullProcessImageNameW", None),
        argtypes=[wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)],
        restype=wintypes.BOOL,
    )

    _safe_set_prototype(
        getattr(_USER32, "MapVirtualKeyW", None),
        argtypes=[wintypes.UINT, wintypes.UINT],
        restype=wintypes.UINT,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetForegroundWindow", None),
        restype=wintypes.HWND,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetWindowRect", None),
        argtypes=[wintypes.HWND, ctypes.POINTER(wintypes.RECT)],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "IsWindow", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "IsWindowVisible", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "IsIconic", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetClassNameW", None),
        argtypes=[wintypes.HWND, wintypes.LPWSTR, ctypes.c_int],
        restype=ctypes.c_int,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetParent", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.HWND,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetAncestor", None),
        argtypes=[wintypes.HWND, wintypes.UINT],
        restype=wintypes.HWND,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetWindowThreadProcessId", None),
        argtypes=[wintypes.HWND, ctypes.POINTER(wintypes.DWORD)],
        restype=wintypes.DWORD,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetGUIThreadInfo", None),
        argtypes=[wintypes.DWORD, wintypes.LPVOID],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "AttachThreadInput", None),
        argtypes=[wintypes.DWORD, wintypes.DWORD, wintypes.BOOL],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "SetForegroundWindow", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "SetActiveWindow", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.HWND,
    )
    _safe_set_prototype(
        getattr(_USER32, "SetFocus", None),
        argtypes=[wintypes.HWND],
        restype=wintypes.HWND,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetWindowLongW", None),
        argtypes=[wintypes.HWND, ctypes.c_int],
        restype=wintypes.LONG,
    )
    _safe_set_prototype(
        getattr(_USER32, "WindowFromPoint", None),
        argtypes=[wintypes.POINT],
        restype=wintypes.HWND,
    )
    _safe_set_prototype(
        getattr(_USER32, "GetAsyncKeyState", None),
        argtypes=[wintypes.INT],
        restype=wintypes.SHORT,
    )
    _ulong_ptr = getattr(wintypes, "ULONG_PTR", ctypes.c_void_p)
    _safe_set_prototype(
        getattr(_USER32, "keybd_event", None),
        argtypes=[wintypes.BYTE, wintypes.BYTE, wintypes.DWORD, _ulong_ptr],
        restype=None,
    )
    _safe_set_prototype(
        getattr(_USER32, "UnhookWindowsHookEx", None),
        argtypes=[wintypes.HHOOK],
        restype=wintypes.BOOL,
    )
    _safe_set_prototype(
        getattr(_USER32, "CallNextHookEx", None),
        argtypes=[wintypes.HHOOK, ctypes.c_int, wintypes.WPARAM, wintypes.LPARAM],
        restype=wintypes.LRESULT,
    )
    _safe_set_prototype(
        getattr(_USER32, "SetWindowsHookExW", None),
        argtypes=[ctypes.c_int, _HOOKPROC_TYPE, wintypes.HINSTANCE, wintypes.DWORD],
        restype=wintypes.HHOOK,
    )

    if _WNDENUMPROC is not None:
        _safe_set_prototype(
            getattr(_USER32, "EnumWindows", None),
            argtypes=[_WNDENUMPROC, wintypes.LPARAM],
            restype=wintypes.BOOL,
        )
        _safe_set_prototype(
            getattr(_USER32, "EnumChildWindows", None),
            argtypes=[wintypes.HWND, _WNDENUMPROC, wintypes.LPARAM],
            restype=wintypes.BOOL,
        )
    _safe_set_prototype(
        getattr(_PSAPI, "GetModuleFileNameExW", None),
        argtypes=[wintypes.HANDLE, wintypes.HMODULE, wintypes.LPWSTR, wintypes.DWORD],
        restype=wintypes.DWORD,
    )


_configure_winapi_prototypes()

VK_UP = getattr(win32con, "VK_UP", 0x26)
VK_DOWN = getattr(win32con, "VK_DOWN", 0x28)
VK_LEFT = getattr(win32con, "VK_LEFT", 0x25)
VK_RIGHT = getattr(win32con, "VK_RIGHT", 0x27)
VK_PRIOR = getattr(win32con, "VK_PRIOR", 0x21)
VK_NEXT = getattr(win32con, "VK_NEXT", 0x22)
KEYEVENTF_EXTENDEDKEY = getattr(win32con, "KEYEVENTF_EXTENDEDKEY", 0x0001)
KEYEVENTF_KEYUP = getattr(win32con, "KEYEVENTF_KEYUP", 0x0002)
_NAVIGATION_EXTENDED_KEYS = {VK_UP, VK_DOWN, VK_LEFT, VK_RIGHT}
MOUSEEVENTF_WHEEL = getattr(win32con, "MOUSEEVENTF_WHEEL", 0x0800)
_PROCESS_QUERY_INFORMATION = getattr(win32con, "PROCESS_QUERY_INFORMATION", 0x0400)
_PROCESS_VM_READ = getattr(win32con, "PROCESS_VM_READ", 0x0010)
_PROCESS_QUERY_LIMITED_INFORMATION = getattr(
    win32con, "PROCESS_QUERY_LIMITED_INFORMATION", 0x1000
)

def clamp(value: float, minimum: float, maximum: float) -> float:
    """Clamp *value* into the inclusive range [minimum, maximum]."""

    if minimum > maximum:
        minimum, maximum = maximum, minimum
    return max(minimum, min(maximum, value))


_TRUE_STRINGS = frozenset({"1", "true", "yes", "on", "y", "t"})
_FALSE_STRINGS = frozenset({"0", "false", "no", "off", "n", "f"})
_INVALID_SHEET_CHARS = frozenset("\\/:?*[]")
_PHOTO_SEGMENT_SANITIZER = re.compile(r"[^\w\u4e00-\u9fff-]+")
_WHITESPACE_RE = re.compile(r"\s+")


class _BooleanParseResult:
    """Namespace for sentinel objects used during boolean coercion."""

    UNRESOLVED = object()

NumberT = TypeVar("NumberT", int, float)


@singledispatch
def _coerce_bool(value: Any) -> object:
    """Return a parsed boolean or :data:`_BooleanParseResult.UNRESOLVED`."""

    return _BooleanParseResult.UNRESOLVED


@_coerce_bool.register(bool)
def _coerce_bool_from_bool(value: bool) -> bool:
    return value


@_coerce_bool.register(int)
def _coerce_bool_from_int(value: int) -> bool:
    return bool(value)


@_coerce_bool.register(float)
def _coerce_bool_from_float(value: float) -> object:
    if math.isnan(value):
        return _BooleanParseResult.UNRESOLVED
    return bool(value)


@_coerce_bool.register(Enum)
def _coerce_bool_from_enum(value: Enum) -> object:
    return _coerce_bool(value.value)


@_coerce_bool.register(bytes)
def _coerce_bool_from_bytes(value: bytes) -> object:
    try:
        decoded = value.decode("utf-8")
    except Exception:
        return _BooleanParseResult.UNRESOLVED
    return _coerce_bool(decoded)


@_coerce_bool.register(bytearray)
@_coerce_bool.register(memoryview)
def _coerce_bool_from_byteslike(value: Union[bytearray, memoryview]) -> object:
    """Handle common bytes-like inputs when converting to booleans."""

    try:
        return _coerce_bool(bytes(value))
    except Exception:
        return _BooleanParseResult.UNRESOLVED


@_coerce_bool.register(str)
def _coerce_bool_from_str(value: str) -> object:
    normalized = value.strip()
    if not normalized:
        return _BooleanParseResult.UNRESOLVED
    lowered = normalized.casefold()
    if lowered in _TRUE_STRINGS:
        return True
    if lowered in _FALSE_STRINGS:
        return False
    signless = lowered[1:] if lowered[0] in "+-" and len(lowered) > 1 else lowered
    if signless.isdigit():
        try:
            return bool(int(lowered, 10))
        except Exception:
            return _BooleanParseResult.UNRESOLVED
    try:
        number = float(lowered)
    except ValueError:
        return _BooleanParseResult.UNRESOLVED
    if math.isnan(number):
        return _BooleanParseResult.UNRESOLVED
    return bool(number)


def _coerce_to_text(value: Any) -> str:
    """Safely coerce *value* into text, returning an empty string on failure."""

    if value is None:
        return ""
    if isinstance(value, str):
        return value
    if isinstance(value, (bytes, bytearray, memoryview)):
        try:
            return bytes(value).decode("utf-8", "ignore")
        except Exception:
            return ""
    if isinstance(value, os.PathLike):
        try:
            return os.fspath(value)
        except Exception:
            return ""
    try:
        return str(value)
    except Exception:
        return ""


@functools.lru_cache(maxsize=512)
def _casefold_cached(value: str) -> str:
    """Return a stripped, case-folded representation of *value* with caching."""

    if not value:
        return ""
    sanitized = value.replace("\x00", "") if "\x00" in value else value
    return sanitized.strip().casefold()


def _normalize_text_token(value: Any, *, empty_on_falsy: bool) -> str:
    """Normalize arbitrary *value* inputs into case-folded strings."""

    text = _coerce_to_text(value)
    if (empty_on_falsy and not value) or not text:
        return ""
    return _casefold_cached(text)


def _normalize_class_token(value: Any) -> str:
    """Normalize potential class-name hints to lowercase tokens."""

    return _normalize_text_token(value, empty_on_falsy=True)


def parse_bool(value: Any, default: bool = False) -> bool:
    """Attempt to coerce *value* into a boolean, returning *default* on failure."""

    result = _coerce_bool(value)
    if result is _BooleanParseResult.UNRESOLVED:
        return default
    return bool(result)


def str_to_bool(value: Any, default: bool = False) -> bool:
    """Alias for :func:`parse_bool` kept for backwards compatibility."""

    return parse_bool(value, default)


@functools.lru_cache(maxsize=1)
def _preferred_app_directory() -> str:
    """Return the user-specific data directory without creating it."""

    home = os.path.expanduser("~")
    if sys.platform.startswith("win"):
        base = os.environ.get("APPDATA") or os.path.join(home, "AppData", "Roaming")
    elif sys.platform == "darwin":
        base = os.path.join(home, "Library", "Application Support")
    else:
        base = os.environ.get("XDG_CONFIG_HOME") or os.path.join(home, ".config")
    return os.path.abspath(os.path.join(base, "ClassroomTools"))


def _ensure_directory(path: str) -> bool:
    if not path:
        return False
    try:
        os.makedirs(path, exist_ok=True)
    except Exception:
        return False
    return os.path.isdir(path)


def _ensure_writable_directory(path: str) -> bool:
    if not path:
        return False
    normalized = os.path.normcase(os.path.abspath(path))
    now = time.monotonic()
    cached_at = _WRITABLE_DIR_CACHE.get(normalized)
    if cached_at and now - cached_at < WRITABLE_DIR_CACHE_TTL_SECONDS:
        if os.path.isdir(path) and os.access(path, os.W_OK):
            return True
    if not _ensure_directory(path):
        return False
    test_path: Optional[str] = None
    fd: Optional[int] = None
    try:
        fd, test_path = tempfile.mkstemp(prefix="ctools_", dir=path)
    except OSError:
        return os.access(path, os.W_OK)
    except Exception:
        return False
    finally:
        if fd is not None:
            with contextlib.suppress(Exception):
                os.close(fd)
        if test_path:
            with contextlib.suppress(Exception):
                os.remove(test_path)
    _WRITABLE_DIR_CACHE[normalized] = now
    return True


def _is_writable_directory(path: str) -> bool:
    if not path or not os.path.isdir(path):
        return False
    try:
        return os.access(path, os.W_OK)
    except Exception:
        return False


def _resolve_log_path() -> Optional[str]:
    global _LOG_PATH
    if _LOG_PATH:
        return _LOG_PATH
    base_dir = _preferred_app_directory()
    if not _ensure_writable_directory(base_dir):
        base_dir = os.getcwd()
        if not _ensure_writable_directory(base_dir):
            return None
    _LOG_PATH = os.path.join(base_dir, LOG_FILENAME)
    return _LOG_PATH


def _configure_logging() -> Optional[str]:
    log_path = _resolve_log_path()
    if not log_path:
        return None
    root_logger = logging.getLogger()
    for handler in root_logger.handlers:
        if isinstance(handler, logging.FileHandler):
            return getattr(handler, "baseFilename", log_path)
    formatter = logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")
    try:
        file_handler = RotatingFileHandler(
            log_path,
            maxBytes=int(LOG_MAX_BYTES),
            backupCount=int(LOG_BACKUP_COUNT),
            encoding="utf-8",
        )
    except Exception:
        return None
    file_handler.setLevel(logging.DEBUG)  # [调试] 设置为 DEBUG 级别
    file_handler.setFormatter(formatter)
    root_logger.setLevel(logging.DEBUG)  # [调试] 设置为 DEBUG 级别
    root_logger.addHandler(file_handler)
    return log_path


def _install_exception_hook() -> None:
    def _handle_exception(exc_type, exc_value, exc_traceback) -> None:
        logger.critical("Unhandled exception", exc_info=(exc_type, exc_value, exc_traceback))
        app = QApplication.instance()
        if app is not None:
            log_path = _resolve_log_path()
            message = "程序发生未处理异常，已记录日志。"
            if log_path:
                message += f"\n日志路径：{log_path}"
            show_quiet_information(None, message, "错误")

    sys.excepthook = _handle_exception


def _run_startup_self_check(settings_manager: "SettingsManager") -> None:
    """Run a minimal startup self-check and report issues via logs."""

    try:
        settings_manager.load_settings()
    except Exception:
        logger.warning("Startup self-check: failed to load settings.ini", exc_info=True)
    settings_path = getattr(settings_manager, "filename", "")
    if settings_path and not os.path.exists(settings_path):
        logger.warning("Startup self-check: settings.ini missing at %s", settings_path)
    icon_path = _any_existing_path(_get_resource_locator().candidates("icon.ico"))
    if not icon_path:
        logger.warning("Startup self-check: icon.ico not found in resource roots")
    student_path = _any_existing_path(_STUDENT_RESOURCES.plain_candidates)
    if not student_path:
        logger.warning("Startup self-check: students.xlsx not found in resource roots")


def _collect_resource_roots() -> List[str]:
    """Return an ordered list of candidate directories containing bundled resources."""

    roots: List[str] = []
    seen: Set[str] = set()

    def _append(path: Optional[str]) -> None:
        if not path:
            return
        normalized = os.path.normpath(os.path.abspath(path))
        if normalized in seen:
            return
        seen.add(normalized)
        roots.append(normalized)

    app_dir = _preferred_app_directory()
    if _ensure_directory(app_dir):
        _append(app_dir)

    exe_dir: Optional[str] = None
    with contextlib.suppress(Exception):
        exe_dir = os.path.dirname(os.path.abspath(getattr(sys, "executable", "")))
    if getattr(sys, "frozen", False) and exe_dir:
        _append(exe_dir)

    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        _append(meipass)

    script_dir: Optional[str] = None
    with contextlib.suppress(Exception):
        script_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
    if script_dir:
        _append(script_dir)

    module_dir = os.path.dirname(os.path.abspath(__file__))
    _append(module_dir)

    with contextlib.suppress(Exception):
        cwd = os.getcwd()
        _append(cwd)

    return roots


class _ResourceLocator:
    """Centralised helper for resolving bundled and user data paths."""

    __slots__ = ("_roots", "_cache")

    def __init__(self) -> None:
        self._roots: Tuple[str, ...] = tuple(_collect_resource_roots())
        self._cache: Dict[str, Tuple[str, ...]] = {}

    def candidates(self, relative_path: str) -> Tuple[str, ...]:
        normalized_key = os.path.normpath(str(relative_path).strip().replace("\\", "/"))
        cached = self._cache.get(normalized_key)
        if cached is not None:
            return cached
        norm_rel = normalized_key.lstrip("./")
        if not norm_rel:
            result = self._roots
            self._cache[normalized_key] = result
            return result
        paths: List[str] = []
        seen: Set[str] = set()
        for root in self._roots:
            candidate = os.path.join(root, norm_rel)
            normalized = os.path.normpath(candidate)
            if normalized in seen:
                continue
            seen.add(normalized)
            paths.append(normalized)
        result = tuple(paths)
        self._cache[normalized_key] = result
        return result


@functools.lru_cache(maxsize=1)
def _get_resource_locator() -> _ResourceLocator:
    return _ResourceLocator()


def _any_existing_path(paths: Iterable[str]) -> Optional[str]:
    for path in paths:
        if path and os.path.exists(path):
            return path
    return None


def _probe_settings_path(filename: str = "settings.ini") -> str:
    """Resolve the settings path without creating directories or files."""

    base_name = os.path.basename(filename) or "settings.ini"
    candidates: List[str] = []
    seen: Set[str] = set()

    def _append(path: Optional[str]) -> None:
        if not path:
            return
        normalized = os.path.normpath(os.path.abspath(path))
        marker = os.path.normcase(normalized)
        if marker in seen:
            return
        seen.add(marker)
        candidates.append(normalized)

    _append(os.path.abspath(filename))
    preferred_dir = _preferred_app_directory()
    if preferred_dir:
        _append(os.path.join(preferred_dir, base_name))

    exe_dir: Optional[str] = None
    with contextlib.suppress(Exception):
        exe_dir = os.path.dirname(os.path.abspath(getattr(sys, "executable", "")))
    if getattr(sys, "frozen", False) and exe_dir:
        _append(os.path.join(exe_dir, base_name))

    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        _append(os.path.join(meipass, base_name))

    script_dir: Optional[str] = None
    with contextlib.suppress(Exception):
        script_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
    if script_dir:
        _append(os.path.join(script_dir, base_name))

    module_dir = os.path.dirname(os.path.abspath(__file__))
    _append(os.path.join(module_dir, base_name))

    with contextlib.suppress(Exception):
        cwd = os.getcwd()
        _append(os.path.join(cwd, base_name))

    existing = _any_existing_path(candidates)
    if existing:
        return existing
    if preferred_dir:
        return os.path.join(preferred_dir, base_name)
    return os.path.abspath(base_name)


def _choose_writable_target(
    candidates: Tuple[str, ...],
    *,
    is_dir: bool,
    fallback_name: str,
) -> str:
    checked_dirs: Set[str] = set()
    for raw_target in candidates:
        if not raw_target:
            continue
        target = os.path.normpath(raw_target)
        directory = target if is_dir else os.path.dirname(target)
        normalized_dir = os.path.normcase(os.path.abspath(directory or os.getcwd()))
        if normalized_dir in checked_dirs:
            continue
        checked_dirs.add(normalized_dir)
        if _ensure_writable_directory(directory or os.getcwd()):
            return target

    sanitized_fallback = os.path.basename(fallback_name.strip()) if fallback_name else ""
    if os.sep in sanitized_fallback:
        sanitized_fallback = sanitized_fallback.replace(os.sep, "_")
    if os.altsep and os.altsep in sanitized_fallback:
        sanitized_fallback = sanitized_fallback.replace(os.altsep, "_")
    if "\\" in sanitized_fallback and os.sep != "\\":
        sanitized_fallback = sanitized_fallback.replace("\\", "_")
    sanitized_fallback = sanitized_fallback or "ClassroomTools"
    app_dir = _preferred_app_directory()
    base_dir = app_dir if _ensure_writable_directory(app_dir) else os.getcwd()
    fallback = os.path.join(base_dir, sanitized_fallback)
    directory = fallback if is_dir else os.path.dirname(fallback)
    if directory and not _ensure_writable_directory(directory):
        fallback = os.path.abspath(sanitized_fallback)
        directory = fallback if is_dir else os.path.dirname(fallback)
        _ensure_writable_directory(directory or os.getcwd())
    return fallback


def _mirror_resource_to_primary(primary: str, candidates: Tuple[str, ...]) -> None:
    if os.path.exists(primary):
        return
    source = None
    for candidate in candidates:
        if not candidate:
            continue
        if os.path.normcase(os.path.abspath(candidate)) == os.path.normcase(os.path.abspath(primary)):
            continue
        if os.path.exists(candidate):
            source = candidate
            break
    if source is None:
        return
    directory = os.path.dirname(primary)
    if directory and not _ensure_directory(directory):
        return
    try:
        shutil.copy2(source, primary)
    except Exception:
        logger.debug("Failed to mirror %s to %s", source, primary, exc_info=True)


@dataclass(frozen=True)
class _ResolvedPathGroup:
    primary: str
    candidates: Tuple[str, ...]


@functools.lru_cache(maxsize=None)
def _resolve_writable_resource(
    relative_path: str,
    *,
    fallback_name: Optional[str] = None,
    is_dir: bool = False,
    extra_candidates: Tuple[str, ...] = (),
    ensure_primary_exists: bool = False,
    copy_from_candidates: bool = True,
    prefer_extra_candidates: bool = False,
) -> _ResolvedPathGroup:
    normalized_rel = str(relative_path).strip().replace("\\", "/")
    locator = _get_resource_locator()
    candidate_list: List[str] = []
    seen: Set[str] = set()

    def _append(path: Optional[str]) -> None:
        if not path:
            return
        normalized = os.path.normpath(os.path.abspath(path))
        marker = os.path.normcase(normalized)
        if marker in seen:
            return
        seen.add(marker)
        candidate_list.append(normalized)

    locator_candidates = locator.candidates(normalized_rel)
    if prefer_extra_candidates:
        for extra in extra_candidates:
            _append(extra)
        for candidate in locator_candidates:
            _append(candidate)
    else:
        for candidate in locator_candidates:
            _append(candidate)
        for extra in extra_candidates:
            _append(extra)

    if not candidate_list:
        module_dir = os.path.dirname(os.path.abspath(__file__))
        _append(os.path.join(module_dir, normalized_rel))

    fallback = fallback_name or os.path.basename(normalized_rel) or normalized_rel.replace("/", "_")
    primary = _choose_writable_target(tuple(candidate_list), is_dir=is_dir, fallback_name=fallback)
    unique_candidates = (primary,) + tuple(
        candidate for candidate in candidate_list if os.path.normcase(candidate) != os.path.normcase(primary)
    )

    if is_dir:
        if ensure_primary_exists:
            _ensure_directory(primary)
    elif copy_from_candidates:
        _mirror_resource_to_primary(primary, unique_candidates)

    return _ResolvedPathGroup(primary=primary, candidates=unique_candidates)


@dataclass(frozen=True)
class _StudentResourcePaths:
    plain: str
    plain_candidates: Tuple[str, ...]


@functools.lru_cache(maxsize=1)
def _resolve_student_resource_paths() -> _StudentResourcePaths:
    module_plain = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "students.xlsx"))
    legacy_plain = os.path.abspath("students.xlsx")
    plain_group = _resolve_writable_resource(
        "students.xlsx",
        fallback_name="students.xlsx",
        extra_candidates=(module_plain, legacy_plain),
        is_dir=False,
        copy_from_candidates=True,
        prefer_extra_candidates=True,
    )

    return _StudentResourcePaths(
        plain=plain_group.primary,
        plain_candidates=plain_group.candidates,
    )


_STUDENT_RESOURCES = _resolve_student_resource_paths()


def _cleanup_student_candidates(keep_plain: Optional[str]) -> None:
    """Remove duplicated student files in alternative candidate locations."""

    def _is_duplicate(path: str, keep: Optional[str]) -> bool:
        if not path or not keep:
            return False
        if not (os.path.exists(path) and os.path.exists(keep)):
            return False
        try:
            if os.path.samefile(path, keep):
                return False
        except Exception:
            pass
        with contextlib.suppress(Exception):
            if filecmp.cmp(path, keep, shallow=False):
                return True
        return False

    def _is_safe_cleanup_target(path: str) -> bool:
        app_dir = _preferred_app_directory()
        if not app_dir:
            return False
        try:
            common = os.path.commonpath([os.path.abspath(path), os.path.abspath(app_dir)])
        except Exception:
            return False
        return os.path.normcase(common) == os.path.normcase(os.path.abspath(app_dir))

    for path in _STUDENT_RESOURCES.plain_candidates:
        if _is_duplicate(path, keep_plain) and _is_safe_cleanup_target(path):
            with contextlib.suppress(Exception):
                os.remove(path)


def _probe_student_photo_roots() -> Tuple[str, List[str]]:
    """Resolve student photo roots without creating directories."""

    module_dir = os.path.dirname(os.path.abspath(__file__))
    app_dir = _preferred_app_directory()
    candidates = [
        os.path.join(module_dir, "student_photos"),
        os.path.abspath("student_photos"),
    ]
    if app_dir:
        candidates.append(os.path.join(app_dir, "student_photos"))
    cleaned: List[str] = []
    for path in candidates:
        if path and path not in cleaned:
            cleaned.append(path)
    for path in cleaned:
        if os.path.isdir(path):
            return path, cleaned
    if cleaned:
        return cleaned[0], cleaned
    return os.path.abspath("student_photos"), [os.path.abspath("student_photos")]


def _determine_student_photo_roots() -> Tuple[str, List[str]]:
    """Select the most appropriate student photo root and provide the fallback list."""

    module_dir = os.path.dirname(os.path.abspath(__file__))
    group = _resolve_writable_resource(
        "student_photos",
        fallback_name="student_photos",
        is_dir=True,
        extra_candidates=(os.path.join(module_dir, "student_photos"), os.path.abspath("student_photos")),
        ensure_primary_exists=True,
        copy_from_candidates=False,
        prefer_extra_candidates=True,
    )
    return group.primary, list(group.candidates)


def _user32_window_rect(hwnd: int) -> Optional[Tuple[int, int, int, int]]:
    if _USER32 is None or hwnd == 0:
        return None
    rect = wintypes.RECT()
    try:
        ok = bool(_USER32.GetWindowRect(wintypes.HWND(hwnd), ctypes.byref(rect)))
    except Exception:
        return None
    if not ok:
        return None
    return rect.left, rect.top, rect.right, rect.bottom


def _user32_is_window(hwnd: int) -> bool:
    if _USER32 is None or hwnd == 0:
        return False
    try:
        return bool(_USER32.IsWindow(wintypes.HWND(hwnd)))
    except Exception:
        return False


def _user32_is_window_visible(hwnd: int) -> bool:
    if _USER32 is None or hwnd == 0:
        return False
    try:
        return bool(_USER32.IsWindowVisible(wintypes.HWND(hwnd)))
    except Exception:
        return False


def _user32_is_window_iconic(hwnd: int) -> bool:
    if _USER32 is None or hwnd == 0:
        return False
    try:
        return bool(_USER32.IsIconic(wintypes.HWND(hwnd)))
    except Exception:
        return False


def _user32_window_class_name(hwnd: int) -> str:
    if _USER32 is None or hwnd == 0:
        return ""
    buffer = ctypes.create_unicode_buffer(256)
    try:
        length = int(_USER32.GetClassNameW(wintypes.HWND(hwnd), buffer, len(buffer)))
    except Exception:
        return ""
    if length <= 0:
        return ""
    return buffer.value.strip().lower()


def _user32_get_foreground_window() -> int:
    if _USER32 is None:
        return 0
    try:
        return int(_USER32.GetForegroundWindow())
    except Exception:
        return 0


def _user32_get_parent(hwnd: int) -> int:
    if _USER32 is None or hwnd == 0:
        return 0
    try:
        return int(_USER32.GetParent(wintypes.HWND(hwnd)))
    except Exception:
        return 0


def _user32_top_level_hwnd(hwnd: int) -> int:
    if _USER32 is None or hwnd == 0:
        return hwnd
    try:
        ga_root = getattr(win32con, "GA_ROOT", 2) if win32con is not None else 2
    except Exception:
        ga_root = 2
    try:
        ancestor = int(_USER32.GetAncestor(wintypes.HWND(hwnd), ga_root))
    except Exception:
        ancestor = 0
    if ancestor:
        return ancestor
    parent = _user32_get_parent(hwnd)
    return parent or hwnd


@functools.lru_cache(maxsize=256)
def _process_image_path(pid: int) -> str:
    if pid <= 0 or _KERNEL32 is None:
        return ""
    access = int(_PROCESS_QUERY_INFORMATION | _PROCESS_VM_READ)
    if _PROCESS_QUERY_LIMITED_INFORMATION:
        access |= int(_PROCESS_QUERY_LIMITED_INFORMATION)
    handle = None
    try:
        handle = _KERNEL32.OpenProcess(access, False, pid)
    except Exception:
        handle = None
    if not handle and _PROCESS_QUERY_LIMITED_INFORMATION:
        try:
            handle = _KERNEL32.OpenProcess(int(_PROCESS_QUERY_LIMITED_INFORMATION), False, pid)
        except Exception:
            handle = None
    if not handle:
        return ""
    try:
        if _PSAPI is not None:
            buffer = ctypes.create_unicode_buffer(512)
            try:
                length = int(_PSAPI.GetModuleFileNameExW(handle, None, buffer, len(buffer)))
            except Exception:
                length = 0
            if length:
                return buffer.value.strip()
        if hasattr(_KERNEL32, "QueryFullProcessImageNameW"):
            buffer = ctypes.create_unicode_buffer(512)
            size = wintypes.DWORD(len(buffer))
            try:
                ok = bool(_KERNEL32.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(size)))
            except Exception:
                ok = False
            if ok:
                return buffer.value.strip()
    finally:
        try:
            _KERNEL32.CloseHandle(handle)
        except Exception:
            pass
    return ""


def _user32_focus_window(hwnd: int) -> bool:
    if _USER32 is None or hwnd == 0:
        return False
    focused = False
    try:
        focused = bool(_USER32.SetForegroundWindow(wintypes.HWND(hwnd)))
    except Exception:
        focused = False
    if not focused:
        try:
            focused = bool(_USER32.SetActiveWindow(wintypes.HWND(hwnd)))
        except Exception:
            focused = False
    focus_ok = False
    try:
        focus_ok = bool(_USER32.SetFocus(wintypes.HWND(hwnd)))
    except Exception:
        focus_ok = False
    return focused or focus_ok

from PyQt6.QtCore import (
    QByteArray,
    QCoreApplication,
    QMutex,
    QMutexLocker,
    QPoint,
    QPointF,
    QRect,
    QRectF,
    QSize,
    Qt,
    QTimer,
    QEvent,
    pyqtSignal,
    QObject,
    QUrl,
    QRunnable,
    QThreadPool,
)
from PyQt6.QtGui import (
    QBrush,
    QColor,
    QCursor,
    QDesktopServices,
    QFont,
    QFontDatabase,
    QFontMetrics,
    QIcon,
    QImage,
    QImageReader,
    QPainter,
    QPainterPath,
    QPainterPathStroker,
    QPen,
    QPixmap,
    QKeyEvent,
    QMouseEvent,
    QResizeEvent,
    QScreen,
    QWheelEvent,
    QAction,
    QGuiApplication,
)
from PyQt6.QtWidgets import (
    QApplication,
    QButtonGroup,
    QGraphicsDropShadowEffect,
    QComboBox,
    QCheckBox,
    QDialog,
    QDialogButtonBox,
    QFrame,
    QFormLayout,
    QGridLayout,
    QGroupBox,
    QHBoxLayout,
    QLabel,
    QMenu,
    QInputDialog,
    QMessageBox,
    QPlainTextEdit,
    QPushButton,
    QSpacerItem,
    QSizePolicy,
    QSlider,
    QSpinBox,
    QStackedWidget,
    QTabWidget,
    QToolButton,
    QVBoxLayout,
    QWidget,
    QToolTip,
)

_QT_NAVIGATION_FORWARD_KEYS = {
    Qt.Key.Key_Down,
    Qt.Key.Key_Right,
    Qt.Key.Key_PageDown,
    Qt.Key.Key_Space,
    Qt.Key.Key_Return,
    Qt.Key.Key_Enter,
}
_QT_NAVIGATION_BACK_KEYS = {
    Qt.Key.Key_Up,
    Qt.Key.Key_Left,
    Qt.Key.Key_PageUp,
}
_QT_NAVIGATION_KEYS = _QT_NAVIGATION_FORWARD_KEYS | _QT_NAVIGATION_BACK_KEYS

# WPS 放映常用翻页键：用于规避 WPS 对 KeyUp/滚轮的二次触发。
_QT_WPS_SLIDESHOW_KEYS = {
    Qt.Key.Key_Up,
    Qt.Key.Key_Down,
    Qt.Key.Key_Left,
    Qt.Key.Key_Right,
    Qt.Key.Key_PageUp,
    Qt.Key.Key_PageDown,
    Qt.Key.Key_Space,
    Qt.Key.Key_Return,
    Qt.Key.Key_Enter,
}

# ---------- 运行环境准备 ----------

def _prepare_windows_tts_environment() -> None:
    """确保 Windows 打包环境下的语音依赖可以写入缓存。"""

    if sys.platform != "win32":
        return
    cache_dir = os.environ.get("COMTYPES_CACHE_DIR", "").strip()
    if cache_dir and os.path.isdir(cache_dir):
        return
    try:
        base = os.environ.get("LOCALAPPDATA")
        if not base:
            base = os.path.join(os.path.expanduser("~"), "AppData", "Local")
        cache_dir = os.path.join(base, "ClassroomTools", "comtypes_cache")
        os.makedirs(cache_dir, exist_ok=True)
        os.environ["COMTYPES_CACHE_DIR"] = cache_dir
    except Exception:
        # 打包环境下若目录创建失败，也不要阻塞主程序。
        pass


def _collect_qt_platform_candidates() -> List[str]:
    base_dir = (
        os.path.dirname(sys.executable)
        if getattr(sys, "frozen", False)
        else os.path.dirname(os.path.abspath(__file__))
    )
    candidates = []
    try:
        import PyQt6  # type: ignore

        pyqt_root = os.path.dirname(os.path.abspath(PyQt6.__file__))
        candidates.extend(
            [
                os.path.join(pyqt_root, "Qt6", "plugins", "platforms"),
                os.path.join(pyqt_root, "Qt", "plugins", "platforms"),
            ]
        )
    except Exception:
        pass
    env_path = os.environ.get("QT_QPA_PLATFORM_PLUGIN_PATH", "").strip()
    if env_path:
        candidates.append(env_path)
    candidates.extend(
        [
            os.path.join(base_dir, "platforms"),
            os.path.join(base_dir, "PyQt6", "Qt6", "plugins", "platforms"),
            os.path.join(base_dir, "PyQt6", "Qt", "plugins", "platforms"),
            os.path.join(base_dir, "Qt6", "plugins", "platforms"),
            os.path.join(base_dir, "plugins", "platforms"),
        ]
    )
    plugin_dirs: List[str] = []
    for path in candidates:
        if path and path not in plugin_dirs:
            plugin_dirs.append(path)
    return plugin_dirs


def _find_qt_platform_plugin_dir() -> Tuple[Optional[str], List[str]]:
    plugin_dirs = _collect_qt_platform_candidates()
    for path in plugin_dirs:
        if os.path.isdir(path) and os.path.isfile(os.path.join(path, QT_PLATFORM_PLUGIN_NAME)):
            return path, plugin_dirs
    return None, plugin_dirs


def _setup_qt_plugin_paths() -> None:
    """在打包场景下补充 Qt 平台插件的搜索路径，避免找不到 windows 插件。"""

    try:
        plugin_dirs: List[str] = []
        for path in _collect_qt_platform_candidates():
            if os.path.isdir(path) and path not in plugin_dirs:
                plugin_dirs.append(path)
        if not plugin_dirs:
            return

        for path in QCoreApplication.libraryPaths():
            if path not in plugin_dirs:
                plugin_dirs.append(path)

        QCoreApplication.setLibraryPaths(plugin_dirs)
        os.environ.setdefault("QT_QPA_PLATFORM_PLUGIN_PATH", plugin_dirs[0])
        plugins_root = os.path.dirname(plugin_dirs[0])
        if plugins_root:
            os.environ.setdefault("QT_PLUGIN_PATH", plugins_root)
    except Exception:
        # 插件路径检查失败不应阻塞主程序。
        pass


# 兼容早期 Python 版本缺失的 ULONG_PTR 定义，供 Win32 输入结构体使用。
if not hasattr(wintypes, "ULONG_PTR"):
    if ctypes.sizeof(ctypes.c_void_p) == ctypes.sizeof(ctypes.c_ulonglong):
        wintypes.ULONG_PTR = ctypes.c_ulonglong  # type: ignore[attr-defined]
    else:
        wintypes.ULONG_PTR = ctypes.c_ulong  # type: ignore[attr-defined]

_prepare_windows_tts_environment()

# ---------- 图标 ----------
class IconManager:
    """集中管理浮动工具条的 SVG 图标，方便后续统一换肤。"""
    _icons: Dict[str, str] = {
        "cursor": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHZpZXdCb3g9JzAgMCAyNCAyNCc+CiAgICA8cGF0aCBmaWxsPScjZjFmM2Y0JyBkPSdNNCAzLjMgMTEuNCAyMWwxLjgtNS44IDYuMy0yLjF6Jy8+CiAgICA8cGF0aCBmaWxsPScjOGFiNGY4JyBkPSdtMTIuNiAxNC40IDQuOCA0LjgtMi4xIDIuMS00LjItNC4yeicvPgo8L3N2Zz4=",
        "shape": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHZpZXdCb3g9JzAgMCAyNCAyNCc+CiAgICA8cmVjdCB4PSczLjUnIHk9JzMuNScgd2lkdGg9JzknIGhlaWdodD0nOScgcng9JzInIGZpbGw9JyNmMWYzZjQnLz4KICAgIDxjaXJjbGUgY3g9JzE2LjUnIGN5PScxNi41JyByPSc1LjUnIGZpbGw9J25vbmUnIHN0cm9rZT0nI2YxZjNmNCcgc3Ryb2tlLXdpZHRoPScxLjgnLz4KICAgIDxjaXJjbGUgY3g9JzE2LjUnIGN5PScxNi41JyByPSczLjUnIGZpbGw9JyM4YWI0ZjgnIGZpbGwtb3BhY2l0eT0nMC4zNScvPgo8L3N2Zz4=",
        "eraser": "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+CiAgPHBhdGggZD0iTTQuNiAxNC4yIDExLjMgNy40YTIgMiAwIDAgMSAyLjggMGwzLjUgMy41YTIgMiAwIDAgMSAwIDIuOGwtNC44IDQuOEg5LjRhMiAyIDAgMCAxLTEuNC0uNmwtMy0zYTIgMiAwIDAgMSAwLTIuOHoiIGZpbGw9IiNmNGE5YjciLz4KICA8cGF0aCBkPSJNOS4yIDE5LjZoNi4xYy42IDAgMS4xLS4yIDEuNS0uNmwxLjctMS43IiBmaWxsPSJub25lIiBzdHJva2U9IiM1ZjYzNjgiIHN0cm9rZS13aWR0aD0iMS42IiBzdHJva2UtbGluZWNhcD0icm91bmQiLz4KICA8cGF0aCBkPSJtNy4yIDEyLjMgNC41IDQuNSIgZmlsbD0ibm9uZSIgc3Ryb2tlPSIjZmZmZmZmIiBzdHJva2Utd2lkdGg9IjEuNiIgc3Rya2UtbGluZWNhcD0icm91bmQiLz4KICA8cGF0aCBkPSJNMy42IDE4LjZoNiIgc3Ryb2tlPSIjNWY2MzY4IiBzdHJva2Utd2lkdGg9IjEuNiIgc3Rya2UtbGluZWNhcD0icm91bmQiLz4KPC9zdmc+",
        "clear_all": "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+CiAgPGRlZnM+CiAgICA8bGluZWFyR3JhZGllbnQgaWQ9ImciIHgxPSIwIiB4Mj0iMCIgeTE9IjAiIHkyPSIxIj4KICAgICAgPHN0b3Agb2Zmc2V0PSIwIiBzdG9wLWNvbG9yPSIjOGFiNGY4Ii8+CiAgICAgIDxzdG9wIG9mZnNldD0iMSIgc3RvcC1jb2xvcj0iIzFhNzNlOCIvPgogICAgPC9saW5lYXJHcmFkaWVudD4KICA8L2RlZnM+CiAgPHBhdGggZD0iTTUuNSA4aDEzbC0uOSAxMS4yQTIgMiAwIDAgMSAxNS42IDIxSDguNGEyIDIgMCAwIDEtMS45LTEuOEw1LjUgOHoiIGZpbGw9InVybCgjZykiIHN0cm9rZT0iIzFhNzNlOCIgc3Rya2Utd2lkdGg9IjEuMiIvPgogIDxwYXRoIGQ9Ik05LjUgNS41IDEwLjMgNGgzLjRsLjggMS41aDQuNSIgZmlsbD0ibm9uZSIgc3Rya2U9IiM1ZjYzNjgiIHN0cm9rZS13aWR0aD0iMS42IiBzdHJva2UtbGluZWNhcD0icm91bmQiIHN0cm9rZS1saW5lam9pbj0icm91bmQiLz4KICA8cGF0aCBkPSJNNSA1LjVoNCIgc3Ryb2tlPSIjNWY2MzY4IiBzdHJva2Utd2lkdGg9IjEuNiIgc3Rya2UtbGluZWNhcD0icm91bmQiLz4KICA8cGF0aCBkPSJNMTAgMTEuMnY2LjFNMTQgMTEuMnY2LjEiIHN0cm9rZT0iI2ZmZmZmZiIgc3Rya2Utd2lkdGg9IjEuNCIgc3Rya2UtbGluZWNhcD0icm91bmQiLz4KICA8cGF0aCBkPSJNOC4yIDExLjJ2Ni4xIiBzdHJva2U9IiMzYjc4ZTciIHN0cm9rZS13aWR0aD0iMS40IiBzdHJva2UtbGluZWNhcD0icm91bmQiIG9wYWNpdHk9Ii43Ii8+CiAgPHBhdGggZD0iTTE1LjggMTEuMnY2LjEiIHN0cm9rZT0iIzNiNzhlNyIgc3Rya2Utd2lkdGg9IjEuNCIgc3Rya2UtbGluZWNhcD0icm91bmQiIG9wYWNpdHk9Ii43Ii8+CiAgPHBhdGggZD0iTTYuMiAzLjYgNy40IDIuNCIgc3Ryb2tlPSIjZmJiYzA0IiBzdHJva2Utd2lkdGg9IjEuNCIgc3Rya2UtbGluZWNhcD0icm91bmQiLz4KICA8cGF0aCBkPSJtMTguNCAzLjQgMS40LTEuNCIgc3Rya2U9IiMzNGE4NTMiIHN0cm9rZS13aWR0aD0iMS40IiBzdHJva2UtbGluZWNhcD0icm91bmQiLz4KPC9zdmc+",
        "settings": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHZpZXdCb3g9JzAgMCAyNCAyNCc+CiAgICA8Y2lyY2xlIGN4PScxMicgY3k9JzEyJyByPSczLjUnIGZpbGw9JyM4YWI0ZjgnLz4KICAgIDxwYXRoIGZpbGw9J25vbmUnIHN0cm9rZT0nI2YxZjNmNCcgc3Ryb2tlLXdpZHRoPScxLjYnIHN0cm9rZS1saW5lY2FwPSdyb3VuZCcgc3Ryb2tlLWxpbmVqb2luPSdyb3VuZCcKICAgICAgICBkPSdNMTIgNC41VjIuOG0wIDE4LjR2LTEuN203LjEtNy41SDIwbS0xOCAwaDEuNk0xNy42IDZsMS4yLTEuMk01LjIgMTguNCA2LjQgMTcuMk02LjQgNiA1LjIgNC44bTEzLjYgMTMuNi0xLjItMS4yJy8+Cjwvc3ZnPg==",
        "whiteboard": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHZpZXdCb3g9JzAgMCAyNCAyNCc+CiAgICA8cmVjdCB4PSczJyB5PSc0JyB3aWR0aD0nMTgnIGhlaWdodD0nMTInIHJ4PScyJyByeT0nMicgZmlsbD0nI2YxZjNmNCcgZmlsbC1vcGFjaXR5PScwLjEyJyBzdHJva2U9JyNmMWYzZjQnIHN0cm9rZS13aWR0aD0nMS42Jy8+CiAgICA8cGF0aCBkPSdtNyAxOCA1LTUgNSA1JyBmaWxsPSdub25lJyBzdHJva2U9JyM4YWI0ZjgnIHN0cm9rZS13aWR0aD0nMS44JyBzdHJva2UtbGluZWNhcD0ncm91bmQnIHN0cm9rZS1saW5lam9pbj0ncm91bmQnLz4KICAgIDxwYXRoIGQ9J004IDloOG0tOCAzaDUnIHN0cm9rZT0nI2YxZjNmNCcgc3Ryb2tlLXdpZHRoPScxLjYnIHN0cm9rZS1saW5lY2FwPSdyb3VuZCcvPgo8L3N2Zz4=",
        "undo": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHZpZXdCb3g9JzAgMCAyNCAyNCc+CiAgPHBhdGggZmlsbD0nI2YxZjNmNCcgZD0nTTguNCA1LjJMMyAxMC42bDUuNCA1LjQgMS40LTEuNC0yLjMtMi4zaDUuNWMzLjIgMCA1LjggMi42IDUuOCA1LjggMCAuNS0uMSAxLS4yIDEuNWwyLjEuNmMuMi0uNy4zLTEuNC4zLTIuMSAwLTQuNC0zLjYtOC04LThINy41bDIuMy0yLjMtMS40LTEuNHonLz4KPC9zdmc+",
    }
    _cache: Dict[str, QIcon] = {}
    _icons.update(
        {
            "selection_delete": "PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHZpZXdCb3g9JzAgMCAyNCAyNCc+PHJlY3QgeD0nNCcgeT0nNC41JyB3aWR0aD0nMTYnIGhlaWdodD0nMTInIGZpbGw9J25vbmUnIHN0cm9rZT0nI2YxZjNmNCcgc3Rya2Utd2lkdGg9JzEuNScgc3Rya2UtZGFzaGFycmF5PSczIDInIHN0cm9rZS1saW5lam9pbj0ncm91bmQnLz48cmVjdCB4PSc1LjQnIHk9JzUuOScgd2lkdGg9JzEzLjInIGhlaWdodD0nOS42JyBmaWxsPScjOGFiNGY4JyBvcGFjaXR5PScwLjE4Jy8+PHBhdGggZD0nTTkuMyAxNi4zIDE0IDExLjYgMTcgMTQuNiAxMi4zIDE5LjNjLS40LjQtMSAuNC0xLjQgMEw5LjMgMTcuN2ExIDEgMCAwIDEgMC0xLjRaJyBmaWxsPScjZjRhOWI3Jy8+PHBhdGggZD0nTTEzLjYgMTcgMTUuOSAxNC43JyBmaWxsPSdub25lJyBzdHJva2U9JyM1ZjYzNjgnIHN0cm9rZS13aWR0aD0nMS40JyBzdHJva2UtbGluZWNhcD0ncm91bmQnLz48L3N2Zz4=",
        }
    )

    @classmethod
    def get_brush_icon(cls, color_hex: str) -> QIcon:
        key = f"brush_{color_hex.lower()}"
        if key in cls._cache:
            return cls._cache[key]
        pixmap = QPixmap(28, 28)
        pixmap.fill(Qt.GlobalColor.transparent)
        painter = QPainter(pixmap)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        brush_color = QColor(color_hex)
        if not brush_color.isValid():
            brush_color = QColor("#999999")
        painter.setBrush(QBrush(brush_color))
        painter.setPen(QPen(QColor(0, 0, 0, 140), 1.4))
        painter.drawEllipse(5, 6, 18, 18)
        painter.setPen(QPen(QColor(255, 255, 255, 230), 3, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        painter.drawLine(9, 10, 18, 19)
        painter.setPen(QPen(QColor(0, 0, 0, 90), 2, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        painter.drawLine(10, 9, 19, 18)
        painter.end()
        icon = QIcon(pixmap)
        cls._cache[key] = icon
        return icon

    @classmethod
    def get_icon(cls, name: str) -> QIcon:
        """返回缓存的图标，如果未缓存则即时加载。"""
        if name == "clear":
            name = "clear_all"  # 兼容旧配置
        if name in cls._cache:
            return cls._cache[name]
        data = cls._icons.get(name)
        if not data:
            return QIcon()
        try:
            pixmap = QPixmap()
            pixmap.loadFromData(QByteArray.fromBase64(data.encode("ascii")), "SVG")
            icon = QIcon(pixmap)
            cls._cache[name] = icon
            return icon
        except Exception:
            return QIcon()


# ---------- 可选依赖 ----------
try:
    import pandas as pd
    PANDAS_AVAILABLE = True
except ImportError:
    pd = None
    PANDAS_AVAILABLE = False

PANDAS_READY = PANDAS_AVAILABLE and pd is not None

try:
    OPENPYXL_AVAILABLE = importlib.util.find_spec("openpyxl") is not None
except Exception:
    OPENPYXL_AVAILABLE = False

try:
    import pyttsx3
except ImportError:
    pyttsx3 = None

try:
    import comtypes  # type: ignore[import-not-found]
    import comtypes.client  # type: ignore[import-not-found]
    COMTYPES_AVAILABLE = True
except ImportError:
    comtypes = None  # type: ignore[assignment]
    COMTYPES_AVAILABLE = False

try:
    import win32com.client  # type: ignore[import-not-found]
    import pythoncom  # type: ignore[import-not-found]
    WIN32COM_AVAILABLE = True
except ImportError:
    win32com = None  # type: ignore[assignment]
    pythoncom = None  # type: ignore[assignment]
    WIN32COM_AVAILABLE = False

try:
    import sounddevice as sd
    import numpy as np
    SOUNDDEVICE_AVAILABLE = True
except ImportError:
    sd = None
    np = None
    SOUNDDEVICE_AVAILABLE = False

if TYPE_CHECKING:
    from pandas import DataFrame as PandasDataFrame
else:  # pragma: no cover - runtime fallback for typing
    PandasDataFrame = Any  # type: ignore[misc, assignment]

# 统一学生名单使用的列顺序，避免各处硬编码。
DEFAULT_STUDENT_COLUMNS: list[str] = ["学号", "姓名", "班级", "分组"]
STUDENT_ROW_ID_COLUMN = "__row_id__"
# 规范化后的空表模板，避免重复构造；每次取用时复制以保证不可变。
_EMPTY_STUDENT_TEMPLATE: Optional["PandasDataFrame"] = None

if sys.platform == "win32":
    try:
        import winreg
        WINREG_AVAILABLE = True
    except ImportError:
        WINREG_AVAILABLE = False
else:
    WINREG_AVAILABLE = False


_EMBEDDED_ROLL_STATE: Optional[str] = None
def _set_embedded_roll_state(state: Optional[str]) -> None:
    global _EMBEDDED_ROLL_STATE
    _EMBEDDED_ROLL_STATE = state if state else None


def _consume_embedded_roll_state() -> Optional[str]:
    global _EMBEDDED_ROLL_STATE
    state = _EMBEDDED_ROLL_STATE
    _EMBEDDED_ROLL_STATE = None
    return state

# ---------- 缓存 ----------
_SPEECH_ENV_CACHE: tuple[float, str, List[str]] = (0.0, "", [])


# ---------- DPI ----------
def ensure_high_dpi_awareness() -> None:
    if sys.platform != "win32":
        return
    os.environ.setdefault("QT_ENABLE_HIGHDPI_SCALING", "1")
    os.environ.setdefault("QT_SCALE_FACTOR_ROUNDING_POLICY", "PassThrough")
    try:
        QGuiApplication.setHighDpiScaleFactorRoundingPolicy(
            Qt.HighDpiScaleFactorRoundingPolicy.PassThrough
        )
    except Exception:
        pass
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


# ---------- 字体 ----------
@functools.lru_cache(maxsize=1)
def _get_available_font_families() -> Set[str]:
    """缓存字体列表，避免重复扫描导致卡顿。"""

    families_list: List[str] = []
    get_families = getattr(QFontDatabase, "families", None)
    if callable(get_families):
        try:
            families_list = list(get_families())
        except TypeError:
            try:
                families_list = list(get_families(QFontDatabase.WritingSystem.Any))  # type: ignore[arg-type]
            except Exception:
                families_list = []
        except Exception:
            families_list = []
    return set(families_list)


# ---------- 颜色处理 ----------
@functools.lru_cache(maxsize=256)
def normalize_color_hex(color_hex: str, fallback: str = "#000000") -> str:
    """
    标准化颜色十六进制字符串，带缓存。

    Args:
        color_hex: 输入的颜色字符串
        fallback: 无效时的默认颜色

    Returns:
        标准化后的颜色字符串（#rrggbb 格式）
    """
    if not color_hex:
        return fallback
    try:
        color = QColor(str(color_hex))
        if not color.isValid():
            return fallback
        return color.name().lower()
    except Exception:
        return fallback


def validate_color_value(value: Any) -> bool:
    """
    验证颜色值是否有效。

    Args:
        value: 待验证的值

    Returns:
        是否为有效的颜色值
    """
    if isinstance(value, str):
        return bool(value.strip())
    if isinstance(value, QColor):
        return value.isValid()
    return False


def clamp_opacity(value: int) -> int:
    """
    限制不透明度值在有效范围内。

    Args:
        value: 不透明度值

    Returns:
        限制在 [OPACITY_MIN, OPACITY_MAX] 范围内的值
    """
    return int(clamp(float(value), float(UIConstants.OPACITY_MIN), float(UIConstants.OPACITY_MAX)))


def clamp_brush_size(value: float) -> float:
    """
    限制画笔大小在有效范围内。

    Args:
        value: 画笔大小值

    Returns:
        限制在有效范围内的值
    """
    return clamp(value, UIConstants.BRUSH_MIN_SIZE, UIConstants.BRUSH_MAX_SIZE)


def clamp_eraser_size(value: float) -> float:
    """
    限制橡皮擦大小在有效范围内。

    Args:
        value: 橡皮擦大小值

    Returns:
        限制在有效范围内的值
    """
    return clamp(value, UIConstants.BRUSH_MIN_SIZE, UIConstants.ERASER_MAX_SIZE)


def clamp_font_size(value: int) -> int:
    """
    限制字体大小在有效范围内。

    Args:
        value: 字体大小值

    Returns:
        限制在有效范围内的值
    """
    return int(clamp(float(value), float(UIConstants.MIN_FONT_SIZE), float(UIConstants.MAX_FONT_SIZE)))


def clamp_voice_volume(value: float) -> float:
    """
    限制语音音量在有效范围内。

    Args:
        value: 音量值

    Returns:
        限制在 [MIN_VOICE_VOLUME, MAX_VOICE_VOLUME] 范围内的值
    """
    return clamp(value, ValidationConstants.MIN_VOICE_VOLUME, ValidationConstants.MAX_VOICE_VOLUME)


def clamp_voice_rate(value: int) -> int:
    """
    限制语音速率在有效范围内。

    Args:
        value: 语音速率值

    Returns:
        限制在有效范围内的值
    """
    return int(clamp(float(value), float(ValidationConstants.MIN_VOICE_RATE), float(ValidationConstants.MAX_VOICE_RATE)))


# ---------- 工具 ----------
def geometry_to_text(widget: QWidget) -> str:
    """以不含边框的尺寸记录窗口几何信息，避免重复放大。"""

    frame = widget.frameGeometry()
    inner = widget.geometry()
    width = inner.width() or frame.width()
    height = inner.height() or frame.height()
    return f"{width}x{height}+{frame.x()}+{frame.y()}"


def apply_geometry_from_text(widget: QWidget, geometry: str) -> None:
    if not geometry:
        return
    parts = geometry.split("+")
    if len(parts) != 3:
        return
    size_part, x_str, y_str = parts
    if "x" not in size_part:
        return
    width_str, height_str = size_part.split("x", 1)
    try:
        width = int(width_str)
        height = int(height_str)
        x = int(x_str)
        y = int(y_str)
    except ValueError:
        return

    base_min_width = getattr(widget, "_base_minimum_width", widget.minimumWidth())
    base_min_height = getattr(widget, "_base_minimum_height", widget.minimumHeight())

    custom_min_width = getattr(widget, "_ensure_min_width", 160)
    custom_min_height = getattr(widget, "_ensure_min_height", 120)

    min_width = max(base_min_width, custom_min_width)
    min_height = max(base_min_height, custom_min_height)

    screen = QApplication.screenAt(QPoint(x, y))
    if screen is None:
        try:
            screen = widget.screen() or QApplication.primaryScreen()
        except Exception:
            screen = QApplication.primaryScreen()
    if screen is not None:
        available = screen.availableGeometry()
        max_width = max(min_width, 320, int(available.width() * 0.9))
        max_height = max(min_height, 240, int(available.height() * 0.9))
        width = max(min_width, min(width, max_width))
        height = max(min_height, min(height, max_height))
        x = max(available.left(), min(x, available.right() - width))
        y = max(available.top(), min(y, available.bottom() - height))
    target_width = max(min_width, width)
    target_height = max(min_height, height)
    widget.resize(target_width, target_height)
    widget.move(x, y)


def ensure_widget_within_screen(widget: QWidget) -> None:
    screen = None
    try:
        screen = widget.screen()
    except Exception:
        screen = None
    if screen is None:
        screen = QApplication.primaryScreen()
    if screen is None:
        return
    base_min_width = getattr(widget, "_base_minimum_width", widget.minimumWidth())
    base_min_height = getattr(widget, "_base_minimum_height", widget.minimumHeight())

    custom_min_width = getattr(widget, "_ensure_min_width", 160)
    custom_min_height = getattr(widget, "_ensure_min_height", 120)

    min_width = max(base_min_width, custom_min_width)
    min_height = max(base_min_height, custom_min_height)

    available = screen.availableGeometry()
    geom = widget.frameGeometry()
    width = widget.width() or geom.width() or widget.sizeHint().width()
    height = widget.height() or geom.height() or widget.sizeHint().height()
    max_width = min(available.width(), max(min_width, int(available.width() * 0.9)))
    max_height = min(available.height(), max(min_height, int(available.height() * 0.9)))
    width = max(min_width, min(width, max_width))
    height = max(min_height, min(height, max_height))
    left_limit = available.x()
    top_limit = available.y()
    right_limit = max(left_limit, available.x() + available.width() - width)
    bottom_limit = max(top_limit, available.y() + available.height() - height)
    x = geom.x() if geom.width() else widget.x()
    y = geom.y() if geom.height() else widget.y()
    x = max(left_limit, min(x, right_limit))
    y = max(top_limit, min(y, bottom_limit))
    widget.resize(width, height)
    widget.move(x, y)


def bool_to_str(value: bool) -> str:
    return "True" if value else "False"


def dedupe_strings(values: List[str]) -> List[str]:
    seen: set[str] = set()
    unique: List[str] = []
    for value in values:
        normalized = value.strip() if isinstance(value, str) else ""
        if not normalized or normalized in seen:
            continue
        seen.add(normalized)
        unique.append(normalized)
    return unique


def _new_student_dataframe() -> Optional["PandasDataFrame"]:
    """生成一份空白的学生名单 DataFrame，保持列顺序一致。"""

    if not (PANDAS_READY and pd is not None):
        return None
    try:
        return _empty_student_dataframe().copy()
    except Exception:
        try:
            return pd.DataFrame(columns=DEFAULT_STUDENT_COLUMNS)
        except Exception:
            return None


def recommended_control_height(font: QFont, *, extra: int = 0, minimum: int = 0) -> int:
    """根据字体高度返回推荐控件高度，避免不同平台字体溢出。
    注意：extra 参数应包含 padding + border 的预留空间。
    对于带边框的按钮，建议 extra >= 14 (padding 10px + border 4px)
    """

    try:
        fm = QFontMetrics(font)
        # 增加 4px 的边框预留 (上下各 2px)，防止边框被裁剪
        height = fm.height() + int(extra) + 4
    except Exception:
        height = minimum
    return max(int(minimum), int(height))


def ask_quiet_confirmation(parent: QWidget, message: str, title: str) -> bool:
    """弹出简洁的确认对话框，返回用户是否选择“确定”。"""

    box = QMessageBox(parent)
    box.setIcon(QMessageBox.Icon.Question)
    box.setWindowTitle(title or "确认")
    box.setText(message)
    box.setStyleSheet(StyleConfig.CONFIRM_DIALOG_STYLE)
    yes_button = box.addButton("确定", QMessageBox.ButtonRole.YesRole)
    no_button = box.addButton("取消", QMessageBox.ButtonRole.NoRole)
    box.setDefaultButton(cast(QPushButton, yes_button))
    box.setEscapeButton(cast(QPushButton, no_button))
    box.exec()
    return box.clickedButton() is yes_button


def _normalize_text(value: object) -> str:
    """标准化文本：去空白、过滤 None/nan，返回字符串。"""

    if PANDAS_READY:
        if pd.isna(value):
            return ""
    else:
        if value is None:
            return ""
        if isinstance(value, float) and math.isnan(value):
            return ""
    text = str(value).strip()
    lowered = text.lower()
    if lowered in {"nan", "none", "nat"}:
        return ""
    return text


def _compact_text(value: object) -> str:
    """去除所有空白字符后的文本，用于学号/姓名等字段展示与匹配。"""

    return _WHITESPACE_RE.sub("", _normalize_text(value))


def _normalize_group_name(value: object) -> str:
    """统一分组名称规范，避免大小写/空白差异导致匹配失败。"""

    text = _normalize_text(value)
    if not text:
        return ""
    return text.upper()


def _student_sort_key(student_id: str, student_name: str) -> Tuple[int, Union[int, str], str, str]:
    """为学生列表排序生成稳定的 key：数字学号优先，其次字典序。"""

    sid = str(student_id or "")
    if sid.isdigit():
        with contextlib.suppress(ValueError):
            return (0, int(sid), _casefold_cached(student_name), sid)
    if sid:
        return (1, _casefold_cached(sid), _casefold_cached(student_name), sid)
    return (2, "", _casefold_cached(student_name), "")


def _student_identity_key(
    student_id: object,
    student_name: object,
    class_name: object,
    group_name: object,
) -> str:
    """基于学生关键字段生成稳定的行标识，用于跨导入/导出恢复点名状态。"""

    parts = [
        _compact_text(student_id),
        _normalize_text(student_name),
        _normalize_text(class_name),
        _normalize_group_name(group_name),
    ]
    joined = "|".join(parts)
    if not joined.strip("|"):
        return ""
    digest = hashlib.sha1(joined.encode("utf-8")).hexdigest()
    return f"rk:{digest}"


def _normalize_student_dataframe(
    df: "PandasDataFrame",
    *,
    drop_incomplete: bool = True,
) -> "PandasDataFrame":
    if not PANDAS_READY:
        return df.copy()

    normalized = df.copy()

    alias_map = {
        "学号": ["学号", "学号 ", "学生学号", "学生编号", "编号"],
        "姓名": ["姓名", "姓名 ", "名字", "学生姓名"],
        "班级": ["班级", "班级 ", "班别", "班级名称"],
        "分组": ["分组", "分组 ", "小组", "组别", "分组名称"],
    }
    alias_to_canonical = {
        alias: canonical for canonical, aliases in alias_map.items() for alias in aliases
    }
    rename_map: Dict[str, str] = {}
    for col in list(normalized.columns):
        trimmed = str(col).strip()
        canonical = alias_to_canonical.get(trimmed, trimmed)
        if canonical != col:
            rename_map[col] = canonical
    if rename_map:
        normalized = normalized.rename(columns=rename_map)
    for canonical in DEFAULT_STUDENT_COLUMNS:
        dup_cols = [col for col in normalized.columns if col == canonical]
        if len(dup_cols) > 1:
            merged = normalized[dup_cols].bfill(axis=1).iloc[:, 0]
            normalized = normalized.drop(columns=dup_cols)
            normalized[canonical] = merged

    original_order = list(normalized.columns)
    for column in ("学号", "姓名", "班级", "分组"):
        if column not in normalized.columns:
            normalized[column] = pd.NA

    def _new_row_id() -> str:
        return uuid.uuid4().hex

    if STUDENT_ROW_ID_COLUMN not in normalized.columns:
        if len(normalized) > 0:
            normalized[STUDENT_ROW_ID_COLUMN] = [_new_row_id() for _ in range(len(normalized))]
        else:
            normalized[STUDENT_ROW_ID_COLUMN] = []
    else:
        seen: Set[str] = set()
        new_values: List[str] = []
        for value in normalized[STUDENT_ROW_ID_COLUMN].tolist():
            text = _compact_text(value)
            if not text or text in seen:
                text = _new_row_id()
            seen.add(text)
            new_values.append(text)
        normalized[STUDENT_ROW_ID_COLUMN] = new_values
    # 清理已废弃的成绩列，避免旧数据影响界面
    normalized = normalized.drop(columns=["成绩"], errors="ignore")

    normalized["姓名"] = normalized["姓名"].apply(_normalize_text)
    normalized["分组"] = normalized["分组"].apply(_normalize_group_name)

    id_series = normalized["学号"].apply(_compact_text)
    normalized["学号"] = id_series

    for column in normalized.select_dtypes(include=["object"]).columns:
        if column in {"姓名", "分组", "学号"}:
            continue
        normalized[column] = normalized[column].apply(_normalize_text)

    ordered_columns = [col for col in original_order if col in normalized.columns]
    for column in DEFAULT_STUDENT_COLUMNS:
        if column in normalized.columns and column not in ordered_columns:
            ordered_columns.append(column)
    extra_columns = [col for col in normalized.columns if col not in ordered_columns]
    normalized = normalized[ordered_columns + extra_columns]

    if drop_incomplete:
        normalized = normalized[(normalized["学号"] != "") & (normalized["姓名"] != "")].copy()
        normalized.reset_index(drop=True, inplace=True)

    return normalized


def _empty_student_dataframe() -> "PandasDataFrame":
    if not PANDAS_READY:
        raise RuntimeError("Pandas support is required to create student data tables.")
    global _EMPTY_STUDENT_TEMPLATE
    if _EMPTY_STUDENT_TEMPLATE is not None:
        return _EMPTY_STUDENT_TEMPLATE.copy(deep=True)
    template = pd.DataFrame({column: [] for column in DEFAULT_STUDENT_COLUMNS})
    normalized = _normalize_student_dataframe(template, drop_incomplete=False)
    _EMPTY_STUDENT_TEMPLATE = normalized.copy(deep=True)
    return normalized


def _missing_student_columns(df: "PandasDataFrame") -> List[str]:
    if df is None or not hasattr(df, "columns"):
        return list(DEFAULT_STUDENT_COLUMNS)
    existing = {str(col).strip() for col in df.columns}
    return [col for col in DEFAULT_STUDENT_COLUMNS if col not in existing]


def _sanitize_sheet_name(name: str, fallback: str) -> str:
    cleaned = "".join(ch for ch in str(name) if ch not in _INVALID_SHEET_CHARS).strip()
    if not cleaned:
        cleaned = fallback
    if len(cleaned) > 31:
        cleaned = cleaned[:31]
    return cleaned


def _build_row_key_map(df: "PandasDataFrame") -> Dict[str, int]:
    if df is None or not isinstance(df, pd.DataFrame) or df.empty:
        return {}
    columns = list(df.columns)
    def _col_index(name: str) -> Optional[int]:
        try:
            return columns.index(name)
        except ValueError:
            return None

    id_idx = _col_index("学号")
    name_idx = _col_index("姓名")
    class_idx = _col_index("班级")
    group_idx = _col_index("分组")

    mapping: Dict[str, int] = {}
    for row in df.itertuples(index=True, name=None):
        idx = row[0]
        student_id = row[id_idx + 1] if id_idx is not None else ""
        student_name = row[name_idx + 1] if name_idx is not None else ""
        class_name = row[class_idx + 1] if class_idx is not None else ""
        group_name = row[group_idx + 1] if group_idx is not None else ""
        key = _student_identity_key(student_id, student_name, class_name, group_name)
        if not key:
            continue
        try:
            mapping[key] = int(idx)
        except (TypeError, ValueError):
            continue
    return mapping


def _evaluate_roll_state_mapping(
    snapshot: ClassRollState,
    df: Optional["PandasDataFrame"],
) -> Tuple[int, int, int]:
    if snapshot is None or df is None or not isinstance(df, pd.DataFrame):
        return 0, 0, 0
    row_key_map = _build_row_key_map(df)
    if not row_key_map:
        return 0, 0, 0
    saved_keys = set()
    for values in snapshot.group_remaining.values():
        for value in values:
            if isinstance(value, str) and value.startswith("rk:"):
                saved_keys.add(value)
    for value in snapshot.global_drawn:
        if isinstance(value, str) and value.startswith("rk:"):
            saved_keys.add(value)
    if not saved_keys:
        return 0, 0, 0
    matched = sum(1 for key in saved_keys if key in row_key_map)
    total = len(saved_keys)
    missing = total - matched
    return matched, total, missing


def _unique_sheet_name(name: str, fallback: str, used: Set[str]) -> str:
    base = _sanitize_sheet_name(name, fallback)
    candidate = base
    if candidate not in used:
        used.add(candidate)
        return candidate
    suffix = 2
    while True:
        tail = f"_{suffix}"
        trimmed = base[: max(1, 31 - len(tail))]
        candidate = f"{trimmed}{tail}"
        if candidate not in used:
            used.add(candidate)
            return candidate
        suffix += 1


@dataclass
class StudentWorkbook:
    """封装多班级学生数据的简单容器，提供读取、更新与切换。"""

    sheets: "OrderedDict[str, PandasDataFrame]"
    active_class: str = ""

    def __post_init__(self) -> None:
        ordered: "OrderedDict[str, PandasDataFrame]" = OrderedDict()
        used_names: Set[str] = set()
        if self.sheets:
            for idx, (name, df) in enumerate(self.sheets.items(), start=1):
                fallback = f"班级{idx}" if idx > 1 else "班级1"
                safe_name = _unique_sheet_name(name, fallback, used_names)
                try:
                    normalized = _normalize_student_dataframe(df, drop_incomplete=False)
                except Exception:
                    normalized = pd.DataFrame(df)
                ordered[safe_name] = normalized
        if not ordered:
            used_names.add("班级1")
            ordered["班级1"] = _empty_student_dataframe().copy()
        self.sheets = ordered
        if not self.active_class or self.active_class not in self.sheets:
            self.active_class = next(iter(self.sheets))

    def class_names(self) -> List[str]:
        return list(self.sheets.keys())

    def is_empty(self) -> bool:
        if not self.sheets:
            return True
        for df in self.sheets.values():
            try:
                if not df.empty:
                    return False
            except AttributeError:
                return False
        return True

    def get_active_dataframe(self) -> "PandasDataFrame":
        df = self.sheets.get(self.active_class)
        if df is None:
            raise KeyError("Active class not found in workbook")
        return df

    def set_active_class(self, class_name: str) -> None:
        name = str(class_name).strip()
        if not name:
            return
        if name not in self.sheets:
            name = self.class_names()[0]
        self.active_class = name

    def update_class(self, class_name: str, df: "PandasDataFrame") -> None:
        name = str(class_name or "").strip() or "班级1"
        safe_name = _sanitize_sheet_name(name, "班级1")
        try:
            normalized = _normalize_student_dataframe(df, drop_incomplete=False)
        except Exception:
            normalized = pd.DataFrame(df)
        self.sheets[safe_name] = normalized

    def as_dict(self) -> "OrderedDict[str, PandasDataFrame]":
        return OrderedDict(self.sheets)


def _atomic_write_payload(file_path: str, payload: bytes, *, suffix: str, description: str) -> None:
    """原子写入二进制内容，避免中途失败留下损坏文件。"""

    directory = os.path.dirname(file_path) or os.getcwd()
    if not _ensure_writable_directory(directory):
        raise OSError(f"目标目录不可写：{directory}")
    fd: Optional[int] = None
    tmp_path = ""
    try:
        fd, tmp_path = tempfile.mkstemp(prefix="ctools_", suffix=suffix, dir=directory)
        with os.fdopen(fd, "wb") as tmp_file:
            tmp_file.write(payload)
        os.replace(tmp_path, file_path)
    except OSError as exc:
        raise OSError(f"Failed to write {description} to {file_path!r}: {exc}") from exc
    finally:
        if fd is not None:
            with contextlib.suppress(Exception):
                os.close(fd)
        if tmp_path:
            with contextlib.suppress(FileNotFoundError):
                os.remove(tmp_path)


def _atomic_write_bytes(file_path: str, payload: bytes, *, suffix: str, description: str) -> None:
    """原子写入二进制文件，避免中途失败留下损坏文件。"""

    _atomic_write_payload(file_path, payload, suffix=suffix, description=description)


def _atomic_write_text(file_path: str, data: str, *, suffix: str, description: str) -> None:
    payload = data.encode("utf-8")
    _atomic_write_payload(file_path, payload, suffix=suffix, description=description)


@dataclass(frozen=True)
class MappingReader:
    """Lightweight helper to read typed values from a mapping with defaults."""

    data: Mapping[str, Any]
    defaults: Mapping[str, Any]

    def _raw(self, key: str, fallback: Any) -> Any:
        if key in self.data:
            return self.data[key]
        if key in self.defaults:
            return self.defaults[key]
        return fallback

    def _raw_default(self, key: str, fallback: Any) -> Any:
        if key in self.defaults:
            return self.defaults[key]
        return fallback

    @staticmethod
    def _coerce_number(
        raw: Any,
        fallback: NumberT,
        converter: Callable[[str], NumberT],
        min_value: Optional[NumberT],
        max_value: Optional[NumberT],
    ) -> NumberT:
        try:
            if isinstance(raw, bool):
                value = converter(str(raw))
            elif isinstance(raw, (int, float)):
                value = converter(raw)  # type: ignore[arg-type]
            else:
                value = converter(str(raw))
        except (TypeError, ValueError):
            value = fallback
        if isinstance(value, float) and not math.isfinite(value):
            value = fallback
        if min_value is not None:
            value = max(min_value, value)
        if max_value is not None:
            value = min(max_value, value)
        return value

    def get_str(self, key: str, fallback: str) -> str:
        raw = self._raw(key, fallback)
        try:
            return str(raw)
        except Exception:
            return fallback

    def get_bool(self, key: str, fallback: bool) -> bool:
        raw = self._raw(key, fallback)
        return parse_bool(raw, fallback)

    def get_int(
        self,
        key: str,
        fallback: int,
        *,
        min_value: Optional[int] = None,
        max_value: Optional[int] = None,
    ) -> int:
        raw = self._raw(key, fallback)
        return self._coerce_number(raw, fallback, int, min_value, max_value)

    def get_float(
        self,
        key: str,
        fallback: float,
        *,
        min_value: Optional[float] = None,
        max_value: Optional[float] = None,
    ) -> float:
        raw = self._raw(key, fallback)
        return self._coerce_number(raw, fallback, float, min_value, max_value)

    def get_int_from_defaults(
        self,
        key: str,
        fallback: int,
        *,
        min_value: Optional[int] = None,
        max_value: Optional[int] = None,
    ) -> int:
        raw = self._raw_default(key, fallback)
        return self._coerce_number(raw, fallback, int, min_value, max_value)

def _compute_presentation_category(
    class_name: str,
    top_class: str,
    process_name: str,
    *,
    has_wps_presentation_signature: Callable[[str], bool],
    is_wps_slideshow_class: Callable[[str], bool],
    has_ms_presentation_signature: Callable[[str], bool],
    is_wps_presentation_process: Callable[..., bool],
) -> str:
    """Classify a presentation window based on class and process hints."""

    def _normalize(value: Any) -> str:
        return _normalize_class_token(value)

    def _normalize_process(value: Any) -> Tuple[str, str]:
        text = _coerce_to_text(value).strip()
        if not text:
            return "", ""
        return text, _casefold_cached(text)

    classes = tuple(
        dict.fromkeys(
            filter(
                None,
                (
                    _normalize(class_name),
                    _normalize(top_class),
                ),
            )
        )
    )

    process, process_lower = _normalize_process(process_name)

    if not classes and not process_lower:
        return "other"

    predicate_cache: Dict[int, Dict[str, bool]] = {}
    process_cache: Dict[int, bool] = {}
    _missing = object()

    def _class_check(predicate: Optional[Callable[[str], bool]]) -> bool:
        if predicate is None:
            return False
        key = id(predicate)
        cache = predicate_cache.setdefault(key, {})
        for candidate in classes:
            cached = cache.get(candidate, _missing)
            if cached is _missing:
                try:
                    cached = bool(predicate(candidate))
                except Exception:
                    if logger.isEnabledFor(logging.DEBUG):
                        logger.debug(
                            "presentation: predicate %s failed for %s",  # pragma: no cover - log only
                            getattr(predicate, "__name__", repr(predicate)),
                            candidate,
                            exc_info=True,
                        )
                    cached = False
                cache[candidate] = bool(cached)
            if cached:
                return True
        return False

    def _process_check(predicate: Optional[Callable[..., bool]]) -> bool:
        if not process:
            return False
        if predicate is None:
            return False
        key = id(predicate)
        cached = process_cache.get(key, _missing)
        if cached is _missing:
            try:
                cached = bool(predicate(process, *classes))
            except Exception:
                if logger.isEnabledFor(logging.DEBUG):
                    logger.debug(
                        "presentation: process predicate %s failed",  # pragma: no cover - log only
                        getattr(predicate, "__name__", repr(predicate)),
                        exc_info=True,
                    )
                cached = False
            process_cache[key] = bool(cached)
        return bool(cached)

    if _class_check(has_wps_presentation_signature):
        return "wps_ppt"

    if _class_check(is_wps_slideshow_class):
        return "wps_ppt"

    if _process_check(is_wps_presentation_process):
        return "wps_ppt"

    if process_lower:
        if process_lower.startswith(("wpp", "wppt")):
            return "wps_ppt"
        if "wpspresentation" in process_lower:
            return "wps_ppt"
        if "powerpnt" in process_lower or process_lower.startswith("pptview"):
            return "ms_ppt"

    if _class_check(has_ms_presentation_signature):
        return "ms_ppt"

    return "other"


def _try_import_module(module: str) -> bool:
    try:
        importlib.import_module(module)
        return True
    except Exception:
        return False


def _count_windows_voice_tokens() -> tuple[int, Optional[str]]:
    if not WINREG_AVAILABLE:
        return -1, "无法访问 Windows 注册表"
    token_names: set[str] = set()
    path = r"SOFTWARE\\Microsoft\\Speech\\Voices\\Tokens"
    flags = {0}
    for attr in ("KEY_WOW64_32KEY", "KEY_WOW64_64KEY"):
        flag = getattr(winreg, attr, 0) if WINREG_AVAILABLE else 0  # type: ignore[name-defined]
        if flag:
            flags.add(flag)
    try:
        for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):  # type: ignore[name-defined]
            for flag in flags:
                access = getattr(winreg, "KEY_READ", 0) | flag  # type: ignore[name-defined]
                try:
                    handle = winreg.OpenKey(hive, path, 0, access)  # type: ignore[name-defined]
                except FileNotFoundError:
                    continue
                except OSError as exc:
                    return 0, str(exc)
                try:
                    index = 0
                    while True:
                        try:
                            name = winreg.EnumKey(handle, index)  # type: ignore[name-defined]
                        except OSError:
                            break
                        token_names.add(str(name))
                        index += 1
                finally:
                    with contextlib.suppress(Exception):
                        winreg.CloseKey(handle)  # type: ignore[name-defined]
    except Exception as exc:
        return 0, str(exc)
    return len(token_names), None


def _find_powershell_executable() -> Optional[str]:
    if sys.platform != "win32":
        return None
    path = shutil.which("pwsh") or shutil.which("powershell")
    if path:
        return path
    system_root = os.environ.get("SystemRoot") or os.environ.get("WINDIR")
    candidate_paths: List[str] = []
    if system_root:
        candidate_paths.extend(
            [
                os.path.join(system_root, "System32", "WindowsPowerShell", "v1.0", "pwsh.exe"),
                os.path.join(system_root, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
                os.path.join(system_root, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe"),
            ]
        )
    candidate_paths.extend(
        [
            os.path.join("C:\\Program Files\\PowerShell\\7", "pwsh.exe"),
            os.path.join("C:\\Program Files\\PowerShell\\6", "pwsh.exe"),
        ]
    )
    for candidate in candidate_paths:
        if candidate and os.path.exists(candidate):
            return candidate
    return None


def _probe_powershell_speech_runtime(executable: Optional[str]) -> tuple[bool, Optional[str]]:
    if sys.platform != "win32" or not executable:
        return True, None
    script = (
        "try { "
        "Add-Type -AssemblyName System.Speech; "
        "[void][System.Speech.Synthesis.SpeechSynthesizer]::new().GetInstalledVoices().Count; "
        "\"OK\" } catch { $_.Exception.Message }"
    )
    startupinfo = None
    if os.name == "nt":
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    try:
        result = subprocess.run(
            [executable, "-NoLogo", "-NonInteractive", "-NoProfile", "-Command", script],
            capture_output=True,
            text=True,
            timeout=8,
            startupinfo=startupinfo,
        )
    except Exception as exc:
        return False, str(exc)
    output = (result.stdout or "").strip()
    if result.returncode != 0:
        message = output or (result.stderr or "").strip()
        return False, message or f"PowerShell exited with code {result.returncode}"
    if "OK" in output:
        return True, None
    if output:
        return False, output
    return True, None


def _detect_pyttsx3_driver_issue() -> Optional[str]:
    if pyttsx3 is None or sys.platform != "win32":
        return None
    try:
        drivers_spec = importlib.util.find_spec("pyttsx3.drivers")
        sapi_spec = importlib.util.find_spec("pyttsx3.drivers.sapi5")
    except Exception:
        return None
    if drivers_spec is None or sapi_spec is None:
        return "pyttsx3 的 SAPI5 驱动未找到，当前系统无法使用 pyttsx3 进行语音播报"
    return None


def _collect_sapi_outputs() -> Tuple[List[str], Dict[str, str]]:
    if sys.platform != "win32" or not WIN32COM_AVAILABLE:
        return [], {}
    try:
        speaker = win32com.client.Dispatch("SAPI.SpVoice")  # type: ignore[attr-defined]
        outputs = list(getattr(speaker, "GetAudioOutputs", lambda: [])())  # type: ignore[call-arg]
    except Exception:
        return [], {}
    output_ids: List[str] = []
    output_descriptions: Dict[str, str] = {}
    for out in outputs:
        oid = getattr(out, "Id", None)
        if not oid:
            continue
        oid_str = str(oid)
        output_ids.append(oid_str)
        try:
            desc = str(out.GetDescription())
        except Exception:
            desc = ""
        output_descriptions[oid_str] = desc
    return output_ids, output_descriptions


def detect_speech_environment_issues(
    force_refresh: bool = False,
    cache_seconds: float = 30.0,
) -> tuple[str, List[str]]:
    global _SPEECH_ENV_CACHE
    now = time.time()
    cached_at, cached_reason, cached_suggestions = _SPEECH_ENV_CACHE
    if not force_refresh and cached_at and now - cached_at < cache_seconds:
        return cached_reason, list(cached_suggestions)

    issues: List[str] = []
    suggestions: List[str] = []
    if sys.platform == "win32":
        missing: List[str] = []
        module_hints = (
            ("pyttsx3", "请安装 pyttsx3（命令：pip install pyttsx3）"),
            ("comtypes.client", "请安装 comtypes（命令：pip install comtypes）"),
            ("win32com.client", "请安装 pywin32（命令：pip install pywin32）"),
        )
        for module_name, hint in module_hints:
            if not _try_import_module(module_name):
                base_name = module_name.split(".")[0]
                if base_name not in missing:
                    missing.append(base_name)
                if hint not in suggestions:
                    suggestions.append(hint)
        if not WIN32COM_AVAILABLE:
            suggestions.append("未检测到 pywin32，推荐安装以启用本地 SAPI 播报（速度快、可选发音人）。")
        if missing:
            issues.append(f"缺少依赖包{'、'.join(sorted(missing))}")
        token_count, token_error = _count_windows_voice_tokens()
        if token_error:
            issues.append(f"无法读取语音库信息：{token_error}")
        elif token_count == 0:
            issues.append("系统未检测到任何语音包")
            suggestions.append("请在 Windows 设置 -> 时间和语言 -> 语音 中下载并启用语音包")
        driver_issue = _detect_pyttsx3_driver_issue()
        if driver_issue:
            issues.append(driver_issue)
            suggestions.append("请确认 pyttsx3 的 SAPI5 驱动已随程序打包，或在命令提示窗运行：python -m pip install pyttsx3 comtypes pywin32")
        powershell_path = _find_powershell_executable()
        if not powershell_path:
            issues.append("未检测到 PowerShell，可用语音回退不可用")
            suggestions.append("请确保系统安装了 PowerShell 5+ 或 PowerShell 7，并能在 PATH 中访问")
        else:
            ps_ok, ps_reason = _probe_powershell_speech_runtime(powershell_path)
            if not ps_ok:
                detail = (ps_reason or "").strip()
                if detail:
                    issues.append(f"PowerShell 初始化语音失败：{detail}")
                else:
                    issues.append("PowerShell 初始化语音失败")
                suggestions.append("请在 PowerShell 中执行 Add-Type -AssemblyName System.Speech 检查错误，必要时启用 RemoteSigned 策略并安装最新 .NET 组件")
        if getattr(sys, "frozen", False):
            suggestions.append("如使用打包版，请确保 pyttsx3、comtypes、pywin32 被包含或在目标机器单独安装")
        suggestions.append("建议在命令提示窗执行：python -m pip install pyttsx3 comtypes pywin32（需管理员权限）")
        suggestions.append("若依旧失败，可尝试以管理员身份首次启动并重启系统")
    else:
        suggestions.append("请确认系统已安装可用的语音引擎（如 espeak 或系统自带语音）。")
    reason = "；".join(issues)
    deduped = dedupe_strings(suggestions)
    _SPEECH_ENV_CACHE = (now, reason, list(deduped))
    return reason, list(deduped)


class QuietInfoPopup(QWidget):
    """提供一个静音的小型提示窗口，避免系统提示音干扰课堂。"""

    _active_popups: List["QuietInfoPopup"] = []

    def __init__(self, parent: Optional[QWidget], text: str, title: str) -> None:
        flags = (
            Qt.WindowType.Tool
            | Qt.WindowType.WindowTitleHint
            | Qt.WindowType.WindowCloseButtonHint
            | Qt.WindowType.CustomizeWindowHint
        )
        super().__init__(parent, flags)
        self.setWindowTitle(title)
        self.setAttribute(Qt.WidgetAttribute.WA_DeleteOnClose, True)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        self.setMinimumWidth(240)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(18, 18, 18, 12)
        layout.setSpacing(12)

        self.message_label = QLabel(text, self)
        self.message_label.setWordWrap(True)
        self.message_label.setAlignment(Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignTop)
        layout.addWidget(self.message_label)

        button_row = QHBoxLayout()
        button_row.addStretch(1)
        self.ok_button = QPushButton("确定", self)
        self.ok_button.setDefault(True)
        apply_button_style(
            self.ok_button,
            ButtonStyles.PRIMARY,
            height=recommended_control_height(self.ok_button.font(), extra=10, minimum=32),
        )
        self.ok_button.clicked.connect(self.close)
        button_row.addWidget(self.ok_button)
        layout.addLayout(button_row)

        QuietInfoPopup._active_popups.append(self)
        self.destroyed.connect(self._cleanup)

    def showEvent(self, event) -> None:  # type: ignore[override]
        super().showEvent(event)
        self.adjustSize()
        self._relocate()
        self.activateWindow()
        self.ok_button.setFocus(Qt.FocusReason.ActiveWindowFocusReason)

    def _relocate(self) -> None:
        target_rect: Optional[QRect] = None
        parent = self.parentWidget()
        if parent and parent.isVisible():
            target_rect = parent.frameGeometry()
        else:
            screen = QApplication.primaryScreen()
            if screen:
                target_rect = screen.availableGeometry()
        if not target_rect:
            return
        geo = self.frameGeometry()
        geo.moveCenter(target_rect.center())
        self.move(geo.topLeft())

    def _cleanup(self, *_args) -> None:
        try:
            QuietInfoPopup._active_popups.remove(self)
        except ValueError:
            pass


def show_quiet_information(parent: Optional[QWidget], text: str, title: str = "提示") -> None:
    popup = QuietInfoPopup(parent, text, title)
    popup.show()


class StyleConfig:
    """统一管理颜色、字体与 QSS 片段，避免散落的硬编码。"""

    # ========== 现代化配色方案 - Material Design 3.0 风格 ==========
    # 主色调
    PRIMARY_COLOR = "#1976D2"
    PRIMARY_CONTAINER = "#E3F2FD"
    ON_PRIMARY = "#FFFFFF"
    ON_PRIMARY_CONTAINER = "#0D47A1"

    # 次要色调
    SECONDARY_COLOR = "#42A5F5"
    SECONDARY_CONTAINER = "#E3F2FD"

    # 中性色
    DESCRIPTION_COLOR = "#546E7A"
    TEXT_PRIMARY = "#263238"
    TEXT_SECONDARY = "#546E7A"
    TEXT_HINT = "#90A4AE"
    BORDER_COLOR = "#CFD8DC"
    BORDER_LIGHT = "#ECEFF1"
    DIVIDER_COLOR = "#E0E0E0"

    # 背景色
    SURFACE_COLOR = "#FAFAFA"
    BACKGROUND_COLOR = "#FFFFFF"
    CARD_BACKGROUND = "#FFFFFF"
    HOVER_OVERLAY = "rgba(25, 118, 210, 0.08)"
    PRESSED_OVERLAY = "rgba(25, 118, 210, 0.12)"

    # 功能色
    SUCCESS_COLOR = "#4CAF50"
    SUCCESS_BG = "#E8F5E9"
    WARNING_COLOR = "#FF9800"
    WARNING_BG = "#FFF3E0"
    ERROR_COLOR = "#F44336"
    ERROR_BG = "#FFEBEE"
    INFO_COLOR = "#2196F3"
    INFO_BG = "#E3F2FD"

    # ========== 工具栏配色 ==========
    # 工具栏背景 - 现代深色主题
    TOOLBAR_BG = "rgba(30, 35, 45, 250)"
    TOOLBAR_BORDER = "rgba(120, 130, 150, 35)"
    TOOLBAR_TEXT = "#E8EAED"
    # 按钮背景
    TOOLBAR_BUTTON_BG = "rgba(255, 255, 255, 12)"
    TOOLBAR_BUTTON_BORDER = "rgba(255, 255, 255, 20)"
    # 悬停效果
    TOOLBAR_HOVER_BG = "rgba(66, 133, 244, 220)"
    TOOLBAR_HOVER_BORDER = "rgba(66, 133, 244, 255)"
    TOOLBAR_HOVER_TEXT = "#FFFFFF"
    # 激活状态
    TOOLBAR_CHECKED_BG = "rgba(66, 133, 244, 255)"
    TOOLBAR_CHECKED_BORDER = "rgba(66, 133, 244, 255)"
    TOOLBAR_CHECKED_TEXT = "#FFFFFF"

    # ========== 特殊按钮配色 ==========
    # 橡皮擦按钮 - 温暖橙色
    ERASER_BG = "rgba(255, 255, 255, 12)"
    ERASER_TEXT = "#E8EAED"
    ERASER_BORDER = "rgba(255, 255, 255, 20)"
    ERASER_HOVER_BG = "rgba(251, 146, 60, 220)"
    ERASER_HOVER_BORDER = "rgba(251, 146, 60, 255)"
    ERASER_ACTIVE_BG = "rgba(251, 146, 60, 255)"
    ERASER_ACTIVE_BORDER = "rgba(251, 146, 60, 255)"
    ERASER_ACTIVE_TEXT = "#FFFFFF"

    # 区域删除按钮 - 警告红色
    REGION_DELETE_BG = "rgba(255, 255, 255, 12)"
    REGION_DELETE_TEXT = "#E8EAED"
    REGION_DELETE_BORDER = "rgba(255, 255, 255, 20)"
    REGION_DELETE_HOVER_BG = "rgba(239, 83, 80, 220)"
    REGION_DELETE_HOVER_BORDER = "rgba(239, 83, 80, 255)"
    REGION_DELETE_ACTIVE_BG = "rgba(239, 83, 80, 255)"
    REGION_DELETE_ACTIVE_BORDER = "rgba(239, 83, 80, 255)"
    REGION_DELETE_ACTIVE_TEXT = "#FFFFFF"

    # 清除按钮 - 成功绿色
    CLEAR_BG = "rgba(255, 255, 255, 12)"
    CLEAR_BORDER = "rgba(255, 255, 255, 20)"
    CLEAR_TEXT = "#E8EAED"
    CLEAR_HOVER_BG = "rgba(76, 175, 80, 220)"
    CLEAR_HOVER_BORDER = "rgba(76, 175, 80, 255)"
    CLEAR_ACTIVE_BG = "rgba(76, 175, 80, 255)"
    CLEAR_ACTIVE_BORDER = "rgba(76, 175, 80, 255)"
    CLEAR_ACTIVE_TEXT = "#FFFFFF"

    # 白板按钮 - 活力琥珀色
    WHITEBOARD_ACTIVE_BG = "rgba(255, 213, 79, 255)"
    WHITEBOARD_ACTIVE_BORDER = "rgba(255, 235, 59, 255)"
    WHITEBOARD_ACTIVE_TEXT = "#1A1A1A"

    # ========== 样式片段 ==========
    DESCRIPTION_LABEL_STYLE = f"color: {DESCRIPTION_COLOR}; font-size: 12px;"
    MENU_BUTTON_STYLE = "padding-bottom: 6px;"

    # 现代化对话框样式
    CONFIRM_DIALOG_STYLE = f"""
        QMessageBox {{
            background-color: {BACKGROUND_COLOR};
        }}
        QMessageBox QLabel {{
            color: {TEXT_PRIMARY};
            font-size: 14px;
            padding: 4px;
        }}
        QMessageBox QPushButton {{
            min-width: 70px;
            padding: 7px 18px;
            color: {TEXT_PRIMARY};
            background: {SURFACE_COLOR};
            border: 1px solid {BORDER_COLOR};
            border-radius: 6px;
            font-size: 13px;
            font-weight: 500;
        }}
        QMessageBox QPushButton:hover {{
            color: {PRIMARY_COLOR};
            border-color: {SECONDARY_COLOR};
            background-color: {PRIMARY_CONTAINER};
        }}
        QMessageBox QPushButton:pressed {{
            color: {ON_PRIMARY_CONTAINER};
            border-color: {ON_PRIMARY_CONTAINER};
            background-color: {PRIMARY_CONTAINER};
        }}
        QMessageBox QPushButton:focus {{
            border-color: {PRIMARY_COLOR};
            outline: none;
        }}
        """

    DIAGNOSTIC_DIALOG_STYLE = f"""
        QDialog {{
            background-color: {BACKGROUND_COLOR};
        }}
        QPlainTextEdit {{
            background-color: {SURFACE_COLOR};
            color: {TEXT_PRIMARY};
            border: 1px solid {BORDER_COLOR};
            border-radius: 6px;
            selection-background-color: {PRIMARY_CONTAINER};
            selection-color: {ON_PRIMARY_CONTAINER};
        }}
        QPushButton {{
            min-width: 80px;
            padding: 7px 18px;
            color: {TEXT_PRIMARY};
            background-color: {SURFACE_COLOR};
            border: 1px solid {BORDER_COLOR};
            border-radius: 6px;
            font-size: 13px;
            font-weight: 500;
        }}
        QPushButton:hover {{
            color: {PRIMARY_COLOR};
            border-color: {SECONDARY_COLOR};
            background-color: {PRIMARY_CONTAINER};
        }}
        QPushButton:pressed {{
            color: {ON_PRIMARY_CONTAINER};
            border-color: {ON_PRIMARY_CONTAINER};
            background-color: {PRIMARY_CONTAINER};
        }}
        QPushButton:checked {{
            background-color: {PRIMARY_COLOR};
            color: {ON_PRIMARY};
            border-color: {PRIMARY_COLOR};
        }}
        QPushButton:focus {{
            border-color: {PRIMARY_COLOR};
            outline: none;
        }}
        """

    # 菜单样式
    MENU_STYLE = f"""
        QMenu {{
            background-color: {BACKGROUND_COLOR};
            border: 1px solid {BORDER_COLOR};
            border-radius: 8px;
            padding: 6px;
            font-size: 13px;
        }}
        QMenu::item {{
            padding: 8px 32px 8px 16px;
            border-radius: 5px;
            color: {TEXT_PRIMARY};
            background-color: transparent;
        }}
        QMenu::item:selected {{
            background-color: {PRIMARY_CONTAINER};
            color: {PRIMARY_COLOR};
        }}
        QMenu::item:checked {{
            background-color: {PRIMARY_COLOR};
            color: {ON_PRIMARY};
        }}
        QMenu::item:disabled {{
            color: {TEXT_HINT};
            background-color: transparent;
        }}
        QMenu::separator {{
            height: 1px;
            background-color: {DIVIDER_COLOR};
            margin: 6px 12px;
        }}
        QMenu::indicator {{
            width: 16px;
            height: 16px;
            left: 8px;
            border-radius: 3px;
            border: 1px solid {BORDER_COLOR};
            background-color: {SURFACE_COLOR};
        }}
        QMenu::indicator:checked {{
            background-color: {PRIMARY_COLOR};
            border-color: {PRIMARY_COLOR};
            image: url(none);
        }}
        QMenu::indicator:checked:disabled {{
            background-color: {BORDER_COLOR};
            border-color: {BORDER_COLOR};
        }}
    """

    @staticmethod
    def floating_toolbar_style(scale: float = 1.0) -> str:
        def _scaled(value: float) -> int:
            return max(1, int(round(value * scale)))

        return (
            """
            #container {{
                background-color: {bg};
                border-radius: {radius}px;
                border: 1px solid {border};
            }}
            QPushButton {{
                color: {text};
                background: {button_bg};
                border: 1px solid {border};
                border-radius: {button_radius}px;
                padding: {padding}px;
                min-width: {button_size}px;
                min-height: {button_size}px;
            }}
            QPushButton:hover {{
                background: {hover_bg};
                border-color: {hover_border};
                color: {hover_text};
            }}
            QPushButton:checked {{
                background: {checked_bg};
                color: {checked_text};
            }}
            QPushButton#eraserButton {{
                background: {eraser_bg};
                color: {eraser_text};
                border-color: {eraser_border};
            }}
            QPushButton#eraserButton:hover {{
                background: {eraser_hover_bg};
                border-color: {eraser_hover_border};
            }}
            QPushButton#eraserButton:checked,
            QPushButton#eraserButton:pressed {{
                background: {eraser_active_bg};
                border-color: {eraser_active_border};
                color: {eraser_active_text};
            }}
            QPushButton#regionDeleteButton {{
                background: {region_delete_bg};
                color: {region_delete_text};
                border-color: {region_delete_border};
            }}
            QPushButton#regionDeleteButton:hover {{
                background: {region_delete_hover_bg};
                border-color: {region_delete_hover_border};
            }}
            QPushButton#regionDeleteButton:checked,
            QPushButton#regionDeleteButton:pressed {{
                background: {region_delete_active_bg};
                border-color: {region_delete_active_border};
                color: {region_delete_active_text};
            }}
            QPushButton#clearButton {{
                background: {clear_bg};
                color: {clear_text};
                border-color: {clear_border};
            }}
            QPushButton#clearButton:hover {{
                background: {clear_hover_bg};
                border-color: {clear_hover_border};
            }}
            QPushButton#clearButton:checked,
            QPushButton#clearButton:pressed {{
                background: {clear_active_bg};
                border-color: {clear_active_border};
                color: {clear_active_text};
            }}
            #whiteboardButtonActive {{
                background: {whiteboard_bg};
                border-color: {whiteboard_border};
                color: {whiteboard_text};
            }}
            """
        ).format(
            bg=StyleConfig.TOOLBAR_BG,
            border=StyleConfig.TOOLBAR_BORDER,
            text=StyleConfig.TOOLBAR_TEXT,
            button_bg=StyleConfig.TOOLBAR_BUTTON_BG,
            radius=_scaled(10),
            button_radius=_scaled(6),
            padding=_scaled(3),
            button_size=_scaled(28),
            hover_bg=StyleConfig.TOOLBAR_HOVER_BG,
            hover_border=StyleConfig.TOOLBAR_HOVER_BORDER,
            hover_text=StyleConfig.TOOLBAR_HOVER_TEXT,
            checked_bg=StyleConfig.TOOLBAR_CHECKED_BG,
            checked_text=StyleConfig.TOOLBAR_CHECKED_TEXT,
            eraser_bg=StyleConfig.ERASER_BG,
            eraser_text=StyleConfig.ERASER_TEXT,
            eraser_border=StyleConfig.ERASER_BORDER,
            eraser_hover_bg=StyleConfig.ERASER_HOVER_BG,
            eraser_hover_border=StyleConfig.ERASER_HOVER_BORDER,
            eraser_active_bg=StyleConfig.ERASER_ACTIVE_BG,
            eraser_active_border=StyleConfig.ERASER_ACTIVE_BORDER,
            eraser_active_text=StyleConfig.ERASER_ACTIVE_TEXT,
            region_delete_bg=StyleConfig.REGION_DELETE_BG,
            region_delete_text=StyleConfig.REGION_DELETE_TEXT,
            region_delete_border=StyleConfig.REGION_DELETE_BORDER,
            region_delete_hover_bg=StyleConfig.REGION_DELETE_HOVER_BG,
            region_delete_hover_border=StyleConfig.REGION_DELETE_HOVER_BORDER,
            region_delete_active_bg=StyleConfig.REGION_DELETE_ACTIVE_BG,
            region_delete_active_border=StyleConfig.REGION_DELETE_ACTIVE_BORDER,
            region_delete_active_text=StyleConfig.REGION_DELETE_ACTIVE_TEXT,
            clear_bg=StyleConfig.CLEAR_BG,
            clear_text=StyleConfig.CLEAR_TEXT,
            clear_border=StyleConfig.CLEAR_BORDER,
            clear_hover_bg=StyleConfig.CLEAR_HOVER_BG,
            clear_hover_border=StyleConfig.CLEAR_HOVER_BORDER,
            clear_active_bg=StyleConfig.CLEAR_ACTIVE_BG,
            clear_active_border=StyleConfig.CLEAR_ACTIVE_BORDER,
            clear_active_text=StyleConfig.CLEAR_ACTIVE_TEXT,
            whiteboard_bg=StyleConfig.WHITEBOARD_ACTIVE_BG,
            whiteboard_border=StyleConfig.WHITEBOARD_ACTIVE_BORDER,
            whiteboard_text=StyleConfig.WHITEBOARD_ACTIVE_TEXT,
        )


@dataclass(frozen=True)
class StyleManager:
    """集中管理通用样式，避免分散的硬编码。"""

    description_label: str = StyleConfig.DESCRIPTION_LABEL_STYLE

    def apply_description_style(self, widget: QWidget) -> None:
        try:
            widget.setStyleSheet(self.description_label)
        except Exception:
            logger.debug("apply_description_style failed", exc_info=True)


STYLE_MANAGER = StyleManager()


class ButtonStyles:
    """Centralised QPushButton样式，避免各窗口重复定义造成视觉不一致。
    使用 Material Design 3.0 风格的现代化配色方案。
    """

    # 工具栏按钮样式 - 紧凑型，适配小窗口
    TOOLBAR = f"""
        QPushButton {{
            padding: 4px 12px;
            border-radius: 5px;
            border: 1px solid {StyleConfig.BORDER_COLOR};
            background-color: {StyleConfig.SURFACE_COLOR};
            color: {StyleConfig.TEXT_PRIMARY};
            font-weight: 500;
            text-align: center;
        }}
        QPushButton:disabled {{
            color: {StyleConfig.TEXT_HINT};
            background-color: {StyleConfig.BORDER_LIGHT};
            border-color: {StyleConfig.BORDER_LIGHT};
        }}
        QPushButton:hover {{
            color: {StyleConfig.PRIMARY_COLOR};
            border-color: {StyleConfig.SECONDARY_COLOR};
            background-color: {StyleConfig.PRIMARY_CONTAINER};
        }}
        QPushButton:pressed {{
            color: {StyleConfig.ON_PRIMARY_CONTAINER};
            border-color: {StyleConfig.ON_PRIMARY_CONTAINER};
            background-color: {StyleConfig.PRIMARY_CONTAINER};
        }}
        QPushButton:checked {{
            background-color: {StyleConfig.PRIMARY_COLOR};
            border-color: {StyleConfig.PRIMARY_COLOR};
            color: {StyleConfig.ON_PRIMARY};
        }}
        QPushButton:focus {{
            border-color: {StyleConfig.PRIMARY_COLOR};
            outline: none;
        }}
    """

    # 网格按钮样式 - 略大内边距
    GRID = f"""
        QPushButton {{
            padding: 8px 18px;
            border-radius: 6px;
            border: 1px solid {StyleConfig.BORDER_COLOR};
            background-color: {StyleConfig.BACKGROUND_COLOR};
            color: {StyleConfig.TEXT_PRIMARY};
            font-weight: 500;
        }}
        QPushButton:disabled {{
            color: {StyleConfig.TEXT_HINT};
            background-color: {StyleConfig.BORDER_LIGHT};
            border-color: {StyleConfig.BORDER_COLOR};
        }}
        QPushButton:hover {{
            color: {StyleConfig.PRIMARY_COLOR};
            border-color: {StyleConfig.SECONDARY_COLOR};
            background-color: {StyleConfig.PRIMARY_CONTAINER};
        }}
        QPushButton:pressed {{
            color: {StyleConfig.ON_PRIMARY_CONTAINER};
            border-color: {StyleConfig.ON_PRIMARY_CONTAINER};
            background-color: {StyleConfig.PRIMARY_CONTAINER};
        }}
        QPushButton:checked {{
            background-color: {StyleConfig.PRIMARY_COLOR};
            border-color: {StyleConfig.PRIMARY_COLOR};
            color: {StyleConfig.ON_PRIMARY};
        }}
        QPushButton:focus {{
            border-color: {StyleConfig.PRIMARY_COLOR};
            outline: none;
        }}
    """

    # 主要按钮样式 - 品牌色填充
    PRIMARY = f"""
        QPushButton {{
            padding: 8px 22px;
            border-radius: 6px;
            background-color: {StyleConfig.PRIMARY_COLOR};
            color: {StyleConfig.ON_PRIMARY};
            border: none;
            font-weight: 500;
        }}
        QPushButton:hover {{
            background-color: {StyleConfig.ON_PRIMARY_CONTAINER};
        }}
        QPushButton:pressed {{
            background-color: #0D47A1;
        }}
        QPushButton:disabled {{
            background-color: {StyleConfig.BORDER_COLOR};
            color: {StyleConfig.TEXT_HINT};
        }}
        QPushButton:focus {{
            outline: 2px solid {StyleConfig.PRIMARY_CONTAINER};
            outline-offset: 2px;
        }}
    """

    # 次要按钮样式 - 边框样式
    SECONDARY = f"""
        QPushButton {{
            padding: 8px 22px;
            border-radius: 6px;
            background-color: transparent;
            color: {StyleConfig.PRIMARY_COLOR};
            border: 1px solid {StyleConfig.PRIMARY_COLOR};
            font-weight: 500;
        }}
        QPushButton:hover {{
            background-color: {StyleConfig.PRIMARY_CONTAINER};
        }}
        QPushButton:pressed {{
            background-color: {StyleConfig.PRIMARY_CONTAINER};
            border-color: {StyleConfig.ON_PRIMARY_CONTAINER};
        }}
        QPushButton:disabled {{
            color: {StyleConfig.TEXT_HINT};
            border-color: {StyleConfig.BORDER_COLOR};
        }}
        QPushButton:focus {{
            outline: 2px solid {StyleConfig.PRIMARY_CONTAINER};
            outline-offset: 2px;
        }}
    """

    # 成功按钮样式
    SUCCESS = f"""
        QPushButton {{
            padding: 8px 22px;
            border-radius: 6px;
            background-color: {StyleConfig.SUCCESS_COLOR};
            color: #FFFFFF;
            border: none;
            font-weight: 500;
        }}
        QPushButton:hover {{
            background-color: #43A047;
        }}
        QPushButton:pressed {{
            background-color: #388E3C;
        }}
        QPushButton:disabled {{
            background-color: {StyleConfig.BORDER_COLOR};
            color: {StyleConfig.TEXT_HINT};
        }}
    """

    # 警告按钮样式
    WARNING = f"""
        QPushButton {{
            padding: 8px 22px;
            border-radius: 6px;
            background-color: {StyleConfig.WARNING_COLOR};
            color: #FFFFFF;
            border: none;
            font-weight: 500;
        }}
        QPushButton:hover {{
            background-color: #FB8C00;
        }}
        QPushButton:pressed {{
            background-color: #F57C00;
        }}
        QPushButton:disabled {{
            background-color: {StyleConfig.BORDER_COLOR};
            color: {StyleConfig.TEXT_HINT};
        }}
    """

    # 危险/错误按钮样式
    DANGER = f"""
        QPushButton {{
            padding: 8px 22px;
            border-radius: 6px;
            background-color: {StyleConfig.ERROR_COLOR};
            color: #FFFFFF;
            border: none;
            font-weight: 500;
        }}
        QPushButton:hover {{
            background-color: #E53935;
        }}
        QPushButton:pressed {{
            background-color: #C62828;
        }}
        QPushButton:disabled {{
            background-color: {StyleConfig.BORDER_COLOR};
            color: {StyleConfig.TEXT_HINT};
        }}
    """


def apply_button_style(button: QPushButton, style: str, *, height: Optional[int] = None) -> None:
    """Apply a reusable QPushButton stylesheet and pointer cursor."""

    button.setCursor(Qt.CursorShape.PointingHandCursor)
    if height is not None:
        button.setMinimumHeight(height)
        button.setSizePolicy(QSizePolicy.Policy.Minimum, QSizePolicy.Policy.Fixed)
    button.setStyleSheet(style)


def style_dialog_buttons(
    button_box: QDialogButtonBox,
    styles: Mapping[QDialogButtonBox.StandardButton, str],
    *,
    extra_padding: int = 6,
    minimum_height: int = 30,
    uniform_width: bool = False,
) -> None:
    """Apply shared styling to all buttons contained in a QDialogButtonBox."""

    styled_buttons: list[QPushButton] = []
    for standard_button, style in styles.items():
        button = button_box.button(standard_button)
        if button is None:
            continue
        control_height = recommended_control_height(
            button.font(), extra=extra_padding, minimum=minimum_height
        )
        apply_button_style(button, style, height=control_height)
        styled_buttons.append(button)
    if uniform_width and styled_buttons:
        target_width = max(button.sizeHint().width() for button in styled_buttons)
        for button in styled_buttons:
            button.setFixedWidth(target_width)


def _set_action_checked_safely(action: QAction, checked: bool) -> None:
    """在不触发信号的情况下更新 QAction 的勾选状态。"""

    block = action.blockSignals(True)
    action.setChecked(bool(checked))
    action.blockSignals(block)


def _menu_add_check_action(
    menu: QMenu,
    text: str,
    *,
    checked: bool = False,
    data: Any = None,
    on_toggled: Optional[Callable[[bool], None]] = None,
    on_triggered: Optional[Callable[[bool], None]] = None,
) -> QAction:
    action = menu.addAction(text)
    action.setCheckable(True)
    action.setChecked(bool(checked))
    if data is not None:
        action.setData(data)
    if on_toggled is not None:
        action.toggled.connect(on_toggled)
    if on_triggered is not None:
        action.triggered.connect(on_triggered)
    return action


def _menu_add_choice_actions(
    menu: QMenu,
    choices: Iterable[Tuple[Any, str]],
    *,
    current_key: Any,
    on_select: Callable[[Any], None],
) -> List[QAction]:
    actions: List[QAction] = []
    for key, label in choices:
        action = menu.addAction(label)
        action.setCheckable(True)
        action.setData(key)
        action.setChecked(key == current_key)
        action.triggered.connect(lambda _checked=False, k=key: on_select(k))
        actions.append(action)
    return actions


def _wrap_checkbox_row(*checkboxes: QCheckBox) -> QWidget:
    container = QWidget()
    layout = QHBoxLayout(container)
    layout.setContentsMargins(0, 0, 0, 0)
    layout.setSpacing(10)
    for checkbox in checkboxes:
        layout.addWidget(checkbox)
    layout.addStretch(1)
    return container


def _set_combo_value(combo: QComboBox, value: Any) -> None:
    for idx in range(combo.count()):
        data = combo.itemData(idx)
        if data == value:
            combo.setCurrentIndex(idx)
            return


def _set_combo_text(combo: QComboBox, text: str) -> None:
    if not text:
        return
    for idx in range(combo.count()):
        if str(combo.itemText(idx)) == text:
            combo.setCurrentIndex(idx)
            return
    if combo.isEditable():
        combo.setCurrentText(text)


def _normalize_wps_input_mode(value: Any) -> str:
    try:
        raw = str(value).strip().lower()
    except Exception:
        raw = ""
    if raw in {"auto", "raw", "message", "manual"}:
        return raw
    if raw in {"sendinput", "force_raw", "raw_input"}:
        return "raw"
    if raw in {"postmessage", "msg", "message_only"}:
        return "message"
    return "auto"


# ---------- 配置 ----------
# 配置文件版本号，用于未来配置迁移
SETTINGS_VERSION = "2.0"
SETTINGS_VERSION_KEY = "_settings_version"


class SettingsManager:
    """负责读取/写入配置文件的轻量封装，支持版本管理和迁移。"""

    def __init__(self, filename: str = "settings.ini") -> None:
        # 统一维护配置文件的存放路径，优先使用用户配置目录，保证跨次启动仍能读取到历史点名状态。
        self._mirror_targets: set[str] = set()
        self.filename = self._prepare_storage_path(filename)
        self._mirror_targets.add(self.filename)
        self.config = configparser.ConfigParser(strict=False)
        self.config.optionxform = str
        self._settings_cache: Optional[Dict[str, Dict[str, str]]] = None
        self._settings_cache_token: Optional[Tuple[int, int, int]] = None
        # 线程安全：保护缓存和文件操作的锁
        self._lock = threading.RLock()
        self.defaults: Dict[str, Dict[str, str]] = {
            "_meta": {
                SETTINGS_VERSION_KEY: SETTINGS_VERSION,
            },
            "Launcher": {
                "x": "120",
                "y": "120",
                "minimized": "False",
                "bubble_x": "120",
                "bubble_y": "120",
                "auto_exit_seconds": "2400",
            },
            "RollCallTimer": {
                "geometry": "480x280+180+180",
                "show_id": "True",
                "show_name": "True",
                "show_photo": "False",
                "photo_duration_seconds": "0",
                "photo_shared_class": "",
                "speech_enabled": "False",
                "speech_voice_id": "",
                "speech_output_id": "",
                "speech_engine": "pyttsx3",
                "current_group": "全部",
                "timer_countdown_minutes": "5",
                "timer_countdown_seconds": "0",
                "timer_sound_enabled": "True",
                "timer_sound_variant": "gentle",
                "timer_reminder_enabled": "False",
                "timer_reminder_interval_minutes": "0",
                "timer_reminder_sound_variant": "soft_beep",
                "mode": "roll_call",
                "timer_mode": "countdown",
                "timer_seconds_left": "300",
                "timer_stopwatch_seconds": "0",
                "timer_running": "False",
                "id_font_size": "48",
                "name_font_size": "60",
                "timer_font_size": "56",
                "remote_roll_enabled": "False",
                "remote_roll_key": "tab",
            },
            "Paint": {
                "x": "260",
                "y": "260",
                "brush_base_size": "12",
                "brush_color": "#ff0000",
                "brush_style": "whiteboard",
                "brush_opacity": "255",
                "quick_color_1": "#000000",
                "quick_color_2": "#ff0000",
                "quick_color_3": "#1e90ff",
                "board_color": "#ffffff",
                "eraser_size": "24",
                "ui_scale": "1.0",
                "shape_type": "line",
                "control_ms_ppt": "True",
                "control_wps_ppt": "True",
                "nav_debug": "False",
                "wps_input_mode": "auto",
                "wps_raw_input": "False",
                "wps_wheel_forward": "True",
            },
        }

    def _prepare_storage_path(self, filename: str) -> str:
        """根据平台选择合适的设置文件路径，并在需要时迁移旧文件。"""

        base_name = os.path.basename(filename) or "settings.ini"
        legacy_path = os.path.abspath(filename)
        resolved = _resolve_writable_resource(
            base_name,
            fallback_name=base_name,
            extra_candidates=(legacy_path,),
            is_dir=False,
            copy_from_candidates=True,
        )

        for candidate in resolved.candidates[1:]:
            if not os.path.exists(candidate):
                continue
            directory = os.path.dirname(candidate) or os.getcwd()
            if os.access(directory, os.W_OK):
                self._mirror_targets.add(candidate)

        try:
            with open(resolved.primary, "a", encoding="utf-8"):
                pass
        except OSError:
            return legacy_path

        return resolved.primary

    def _settings_file_token(self) -> Optional[Tuple[int, int, int]]:
        try:
            stat = os.stat(self.filename)
        except FileNotFoundError:
            return None
        except OSError:
            return None
        mtime_ns = int(getattr(stat, "st_mtime_ns", int(stat.st_mtime * 1_000_000_000)))
        ctime_ns = int(getattr(stat, "st_ctime_ns", int(stat.st_ctime * 1_000_000_000)))
        size = int(stat.st_size)
        return (mtime_ns, size, ctime_ns)

    def get_defaults(self) -> Dict[str, Dict[str, str]]:
        return {section: values.copy() for section, values in self.defaults.items()}

    def get_paint_settings(self) -> PaintConfig:
        settings = self.load_settings()
        section = settings.get("Paint", {})
        defaults = self.defaults.get("Paint", {})
        return PaintConfig.from_mapping(section, defaults)

    def update_paint_settings(self, config: PaintConfig) -> None:
        settings = self.load_settings()
        settings["Paint"] = config.to_mapping()
        self.save_settings(settings)

    def get_roll_call_settings(self) -> RollCallTimerConfig:
        settings = self.load_settings()
        section = settings.get("RollCallTimer", {})
        defaults = self.defaults.get("RollCallTimer", {})
        return RollCallTimerConfig.from_mapping(section, defaults)

    def load_settings(self) -> Dict[str, Dict[str, str]]:
        with self._lock:
            token = self._settings_file_token()
            if self._settings_cache is not None and token == self._settings_cache_token:
                return {section: values.copy() for section, values in self._settings_cache.items()}

            settings = self.get_defaults()
            if os.path.exists(self.filename):
                try:
                    self.config.clear()
                    self.config.read(self.filename, encoding="utf-8")
                    for section in self.config.sections():
                        if section not in settings:
                            settings[section] = {}
                        for key, value in self.config.items(section):
                            settings[section][key] = value
                except (configparser.Error, OSError, UnicodeError) as exc:
                    logger.warning("Failed to read settings from %s: %s", self.filename, exc)
                    settings = self._load_settings_loose() or self.get_defaults()

            self._settings_cache = {section: values.copy() for section, values in settings.items()}
            self._settings_cache_token = token
            # 配置迁移：检查并迁移旧版本配置
            self._migrate_settings_if_needed(settings)
            return {section: values.copy() for section, values in self._settings_cache.items()}

    def _migrate_settings_if_needed(self, settings: Dict[str, Dict[str, str]]) -> None:
        """
        检查配置版本，必要时执行迁移。

        Args:
            settings: 当前加载的配置字典（会被就地修改）
        """
        meta = settings.get("_meta", {})
        current_version = meta.get(SETTINGS_VERSION_KEY, "")

        if current_version == SETTINGS_VERSION:
            return

        logger.info(
            "配置版本迁移: %s -> %s",
            current_version or "(未版本化)",
            SETTINGS_VERSION,
        )

        # 未来版本迁移逻辑在这里添加
        # 例如：
        # if current_version == "1.0":
        #     self._migrate_from_v1_to_v2(settings)

        # 更新版本号
        if "_meta" not in settings:
            settings["_meta"] = {}
        settings["_meta"][SETTINGS_VERSION_KEY] = SETTINGS_VERSION

    def _load_settings_loose(self) -> Optional[Dict[str, Dict[str, str]]]:
        """Try a tolerant parser when configparser rejects duplicate keys."""

        if not os.path.exists(self.filename):
            return None
        try:
            with open(self.filename, "r", encoding="utf-8") as handle:
                raw = handle.read()
        except OSError:
            return None
        settings = self.get_defaults()
        current_section: Optional[str] = None
        for line in raw.splitlines():
            stripped = line.strip()
            if not stripped or stripped.startswith(("#", ";")):
                continue
            if stripped.startswith("[") and stripped.endswith("]"):
                section = stripped[1:-1].strip()
                if not section:
                    current_section = None
                    continue
                current_section = section
                settings.setdefault(section, {})
                continue
            if current_section is None:
                continue
            if "=" in stripped:
                key, value = stripped.split("=", 1)
            elif ":" in stripped:
                key, value = stripped.split(":", 1)
            else:
                continue
            key = key.strip()
            value = value.strip()
            if key:
                settings[current_section][key] = value
        return settings

    def _write_atomic(self, path: str, data: str) -> None:
        """原子写入配置文件，添加重试机制增强健壮性。"""
        directory = os.path.dirname(path)
        if directory and not os.path.exists(directory):
            try:
                os.makedirs(directory, exist_ok=True)
            except Exception as exc:
                logger.warning("Failed to prepare directory for settings %s: %s", directory, exc)
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write(data)
                return

        # 重试机制：使用常量定义的次数和延迟
        max_retries = UIConstants.FILE_WRITE_MAX_RETRIES
        base_delay_ms = UIConstants.FILE_WRITE_RETRY_BASE_DELAY_MS
        last_error = None

        for attempt in range(max_retries):
            try:
                _atomic_write_text(path, data, suffix=".ini", description="settings")
                return
            except (OSError, IOError) as exc:
                last_error = exc
                if attempt < max_retries - 1:
                    # 指数退避策略
                    wait_time = (base_delay_ms / 1000.0) * (2 ** attempt)
                    logger.debug(
                        "Write attempt %d/%d failed for %s: %s, retrying in %.3fs",
                        attempt + 1, max_retries, path, exc, wait_time
                    )
                    time.sleep(wait_time)
                    continue
                # 最后一次尝试失败，使用降级策略
                logger.warning("Atomic write failed for %s after %d attempts: %s", path, max_retries, exc)
                break
            except Exception as exc:
                # 非IO异常直接抛出
                logger.warning("Atomic write failed for %s with unexpected error: %s", path, exc)
                raise

        # 降级策略：使用直接写入
        if last_error is not None:
            logger.warning("Atomic write failed for %s, falling back to direct write: %s", path, last_error)
            try:
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write(data)
            except Exception as exc:
                raise OSError(f"Failed to write settings to {path} after retries: {exc}") from exc

    def save_settings(self, settings: Dict[str, Dict[str, str]]) -> None:
        with self._lock:
            config = configparser.ConfigParser()
            config.optionxform = str
            snapshot: Dict[str, Dict[str, str]] = {}
            for section, options in settings.items():
                snapshot[section] = {key: str(value) for key, value in options.items()}
                config[section] = snapshot[section]

            if self._settings_cache is not None and snapshot == self._settings_cache:
                missing_targets = [path for path in self._mirror_targets if not os.path.exists(path)]
                if not missing_targets:
                    self._settings_cache_token = self._settings_file_token()
                    return

            buffer = io.StringIO()
            config.write(buffer)
            data = buffer.getvalue()
            buffer.close()

            failed: List[str] = []
            for path in sorted(self._mirror_targets):
                try:
                    self._write_atomic(path, data)
                except Exception:
                    failed.append(path)
            if failed and self.filename not in failed:
                logger.warning("Failed to write mirrored settings to %s", failed)

            self._settings_cache = {section: values.copy() for section, values in snapshot.items()}
            self._settings_cache_token = self._settings_file_token()

    def get_launcher_state(self) -> "LauncherSettings":
        """Return launcher geometry and timing flags in a single pass."""

        settings = self.load_settings()
        launcher_defaults = self.defaults.get("Launcher", {})
        launcher_section = settings.get("Launcher", {})
        launcher_settings = LauncherSettings.from_mapping(launcher_section, launcher_defaults)
        return launcher_settings

    def update_launcher_settings(self, launcher_settings: "LauncherSettings") -> None:
        """Persist the launcher configuration."""

        settings = self.load_settings()
        launcher_section = dict(settings.get("Launcher", {}))
        launcher_section.update(launcher_settings.to_mapping())
        settings["Launcher"] = launcher_section

        self.save_settings(settings)

    def clear_roll_call_history(self) -> None:
        """清除点名历史信息，仅在用户主动重置时调用。"""

        settings = self.load_settings()
        section = settings.get("RollCallTimer", {})
        removed = False
        for key in ("group_remaining", "group_last", "global_drawn"):
            if key in section:
                section.pop(key, None)
                removed = True
        if removed:
            settings["RollCallTimer"] = section
            self.save_settings(settings)


class _IOWorkerSignals(QObject):
    """通用的线程任务信号定义。"""

    finished = pyqtSignal(object)
    error = pyqtSignal(str, object)


class _IOWorker(QRunnable):
    """在后台线程中执行 I/O 任务，避免阻塞 UI。"""

    def __init__(self, fn: Callable[..., Any], *args: Any, **kwargs: Any) -> None:
        super().__init__()
        self.fn = fn
        self.args = args
        self.kwargs = kwargs
        self.signals = _IOWorkerSignals()

    def run(self) -> None:  # pragma: no cover - 线程任务
        try:
            result = self.fn(*self.args, **self.kwargs)
        except Exception as exc:  # noqa: BLE001 - 顶层捕获以防线程崩溃
            try:
                fn_name = getattr(self.fn, "__name__", "worker")
            except Exception:
                fn_name = "worker"
            logger.exception("Background task failed (%s)", fn_name)
            try:
                if is_app_closing():
                    return
                self.signals.error.emit(str(exc), exc)
            except Exception:
                pass
            return
        try:
            if is_app_closing():
                return
            self.signals.finished.emit(result)
        except Exception:
            pass


@dataclass(frozen=True)
class LauncherSettings:
    position: QPoint
    bubble_position: QPoint
    minimized: bool
    auto_exit_seconds: int

    @staticmethod
    def from_mapping(
        mapping: Mapping[str, str],
        defaults: Mapping[str, str],
    ) -> "LauncherSettings":
        reader = MappingReader(mapping, defaults)

        default_x = reader.get_int_from_defaults("x", 120)
        default_y = reader.get_int_from_defaults("y", 120)
        x = reader.get_int("x", default_x)
        y = reader.get_int("y", default_y)

        bubble_default_x = reader.get_int_from_defaults("bubble_x", x)
        bubble_default_y = reader.get_int_from_defaults("bubble_y", y)
        bubble_x = reader.get_int("bubble_x", bubble_default_x)
        bubble_y = reader.get_int("bubble_y", bubble_default_y)

        minimized = reader.get_bool("minimized", False)
        auto_exit_seconds = reader.get_int("auto_exit_seconds", 0, min_value=0)
        return LauncherSettings(QPoint(x, y), QPoint(bubble_x, bubble_y), minimized, auto_exit_seconds)

    def to_mapping(self) -> Dict[str, str]:
        return {
            "x": str(self.position.x()),
            "y": str(self.position.y()),
            "bubble_x": str(self.bubble_position.x()),
            "bubble_y": str(self.bubble_position.y()),
            "minimized": bool_to_str(self.minimized),
            "auto_exit_seconds": str(self.auto_exit_seconds),
        }


# ---------- 笔刷方案 ----------
class PenStyle(Enum):
    WHITEBOARD = "whiteboard"
    CHALK = "chalk"
    SIGNATURE = "signature"
    HIGHLIGHTER = "highlighter"
    FOUNTAIN = "fountain"
    BRUSH = "brush"


@dataclass(frozen=True)
class PenStyleConfig:
    key: str
    display_name: str
    description: str
    slider_range: tuple[int, int]
    default_base: int
    width_multiplier: float
    smoothing: float
    speed_base_multiplier: float
    speed_base_offset: float
    target_min_factor: float
    target_speed_factor: float
    target_curve_factor: float
    target_blend: float
    curve_sensitivity: float
    pressure_factor: float
    width_memory: float
    pressure_time_weight: float
    travel_weight: float
    fade_min_alpha: int
    fade_max_alpha: int
    fade_speed_weight: float
    fade_curve_weight: float
    base_alpha: int
    shadow_alpha: int
    shadow_alpha_scale: float
    shadow_width_scale: float
    texture: Optional[Qt.BrushStyle]
    composition_mode: QPainter.CompositionMode
    color_lighten: float = 1.0
    target_max_factor: float = 2.0
    width_change_limit: float = 0.35
    noise_strength: float = 0.0
    fill_alpha_boost: int = 0
    feather_strength: float = 0.0
    edge_highlight_alpha: int = 0
    solid_fill: bool = False
    opacity_range: Optional[tuple[int, int]] = None
    default_opacity: Optional[int] = None
    target_responsiveness: float = 0.35
    width_accel: float = 0.18
    width_velocity_limit: float = 0.22
    width_velocity_damping: float = 0.7
    width_gamma: float = 1.0
    entry_taper_distance: float = 0.0
    entry_taper_strength: float = 0.0
    entry_taper_curve: float = 1.0
    exit_taper_speed: float = 0.0
    exit_taper_strength: float = 0.0
    exit_taper_curve: float = 1.0
    tail_alpha_fade: float = 0.0
    jitter_strength: float = 0.0


_DEFAULT_PEN_STYLE = PenStyle.WHITEBOARD


def _parse_pen_style(value: Any, fallback: PenStyle) -> PenStyle:
    """统一画笔风格：无论配置中是什么，都回落到默认风格。"""
    return fallback


@dataclass
class PaintConfig:
    """Typed configuration for overlay and paint tools."""

    x: int = 260
    y: int = 260
    brush_base_size: float = 12.0
    brush_color: str = "#ff0000"
    brush_style: PenStyle = _DEFAULT_PEN_STYLE
    brush_opacity: int = 255
    quick_color_1: str = "#000000"
    quick_color_2: str = "#ff0000"
    quick_color_3: str = "#1e90ff"
    board_color: str = "#ffffff"
    eraser_size: float = 24.0
    ui_scale: float = 1.0
    shape_type: str = "line"
    control_ms_ppt: bool = True
    control_wps_ppt: bool = True
    nav_debug: bool = False
    wps_input_mode: str = "auto"
    wps_raw_input: bool = False
    wps_wheel_forward: bool = False

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, str], defaults: Mapping[str, str]) -> "PaintConfig":
        reader = MappingReader(mapping, defaults)

        raw_shape = reader.get_str("shape_type", "line")
        shape_type = "line"
        if isinstance(raw_shape, str):
            candidate = raw_shape.strip()
            if candidate in SHAPE_TYPES:
                shape_type = candidate
        style_raw = reader.get_str("brush_style", _DEFAULT_PEN_STYLE.value)
        style = _parse_pen_style(style_raw, _DEFAULT_PEN_STYLE)
        opacity_raw = reader.get_int("brush_opacity", -1)
        if opacity_raw < 0:
            fallback_key = f"{_DEFAULT_PEN_STYLE.value}_opacity"
            if fallback_key in mapping:
                opacity_raw = int(reader.get_float(fallback_key, 255.0))
            else:
                for key in mapping.keys():
                    if key.endswith("_opacity"):
                        opacity_raw = int(reader.get_float(key, 255.0))
                        break
        brush_opacity = int(clamp(opacity_raw if opacity_raw >= 0 else 255, 0, 255))
        raw_input_mode = reader.get_str("wps_input_mode", "auto")
        normalized_input_mode = _normalize_wps_input_mode(raw_input_mode)
        return cls(
            x=reader.get_int("x", 260),
            y=reader.get_int("y", 260),
            brush_base_size=reader.get_float("brush_base_size", 12.0),
            brush_color=reader.get_str("brush_color", "#ff0000"),
            brush_style=style,
            brush_opacity=brush_opacity,
            quick_color_1=reader.get_str("quick_color_1", "#000000"),
            quick_color_2=reader.get_str("quick_color_2", "#ff0000"),
            quick_color_3=reader.get_str("quick_color_3", "#1e90ff"),
            board_color=reader.get_str("board_color", "#ffffff"),
            eraser_size=reader.get_float("eraser_size", 24.0),
            ui_scale=reader.get_float("ui_scale", 1.0),
            shape_type=shape_type,
            control_ms_ppt=reader.get_bool("control_ms_ppt", True),
            control_wps_ppt=reader.get_bool("control_wps_ppt", True),
            nav_debug=reader.get_bool("nav_debug", False),
            wps_input_mode=normalized_input_mode,
            wps_raw_input=reader.get_bool("wps_raw_input", False),
            wps_wheel_forward=reader.get_bool("wps_wheel_forward", False),
        )

    def to_mapping(self) -> Dict[str, str]:
        return {
            "x": str(int(self.x)),
            "y": str(int(self.y)),
            "brush_base_size": f"{float(self.brush_base_size):.2f}",
            "brush_color": str(self.brush_color),
            "brush_style": self.brush_style.value,
            "brush_opacity": str(int(clamp(self.brush_opacity, 0, 255))),
            "quick_color_1": str(self.quick_color_1),
            "quick_color_2": str(self.quick_color_2),
            "quick_color_3": str(self.quick_color_3),
            "board_color": str(self.board_color),
            "eraser_size": f"{float(self.eraser_size):.2f}",
            "ui_scale": f"{float(self.ui_scale):.2f}",
            "shape_type": str(self.shape_type or "line"),
            "control_ms_ppt": bool_to_str(self.control_ms_ppt),
            "control_wps_ppt": bool_to_str(self.control_wps_ppt),
            "nav_debug": bool_to_str(self.nav_debug),
            "wps_input_mode": str(_normalize_wps_input_mode(self.wps_input_mode or "auto")),
            "wps_raw_input": bool_to_str(self.wps_raw_input),
            "wps_wheel_forward": bool_to_str(self.wps_wheel_forward),
        }


@dataclass
class RollCallTimerConfig:
    """Typed configuration for roll call and timer settings."""

    geometry: str = "480x280+180+180"
    show_id: bool = True
    show_name: bool = True
    show_photo: bool = False
    photo_duration_seconds: int = 0
    photo_shared_class: str = ""
    speech_enabled: bool = False
    speech_voice_id: str = ""
    speech_output_id: str = ""
    speech_engine: str = "pyttsx3"
    current_group: str = "全部"
    timer_countdown_minutes: int = 5
    timer_countdown_seconds: int = 0
    timer_sound_enabled: bool = True
    timer_sound_variant: str = "gentle"
    timer_reminder_enabled: bool = False
    timer_reminder_interval_minutes: int = 0
    timer_reminder_sound_variant: str = "soft_beep"
    mode: str = "roll_call"
    timer_mode: str = "countdown"
    timer_seconds_left: int = 300
    timer_stopwatch_seconds: int = 0
    timer_running: bool = False
    id_font_size: int = 48
    name_font_size: int = 60
    timer_font_size: int = 56
    current_class: str = ""
    class_states: str = ""
    group_remaining: str = ""
    group_last: str = ""
    global_drawn: str = ""
    remote_roll_enabled: bool = False
    remote_roll_key: str = "tab"

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, str], defaults: Mapping[str, str]) -> "RollCallTimerConfig":
        reader = MappingReader(mapping, defaults)

        return cls(
            geometry=reader.get_str("geometry", "480x280+180+180"),
            show_id=reader.get_bool("show_id", True),
            show_name=reader.get_bool("show_name", True),
            show_photo=reader.get_bool("show_photo", False),
            photo_duration_seconds=reader.get_int("photo_duration_seconds", 0, min_value=0),
            photo_shared_class=reader.get_str("photo_shared_class", ""),
            speech_enabled=reader.get_bool("speech_enabled", False),
            speech_voice_id=reader.get_str("speech_voice_id", ""),
            speech_output_id=reader.get_str("speech_output_id", ""),
            speech_engine=reader.get_str("speech_engine", "pyttsx3"),
            current_group=reader.get_str("current_group", "全部"),
            timer_countdown_minutes=reader.get_int("timer_countdown_minutes", 5),
            timer_countdown_seconds=reader.get_int("timer_countdown_seconds", 0),
            timer_sound_enabled=reader.get_bool("timer_sound_enabled", True),
            timer_sound_variant=reader.get_str("timer_sound_variant", "gentle"),
            timer_reminder_enabled=reader.get_bool("timer_reminder_enabled", False),
            timer_reminder_interval_minutes=reader.get_int("timer_reminder_interval_minutes", 0, min_value=0),
            timer_reminder_sound_variant=reader.get_str("timer_reminder_sound_variant", "soft_beep"),
            mode=reader.get_str("mode", "roll_call"),
            timer_mode=reader.get_str("timer_mode", "countdown"),
            timer_seconds_left=reader.get_int("timer_seconds_left", 300, min_value=0),
            timer_stopwatch_seconds=reader.get_int("timer_stopwatch_seconds", 0, min_value=0),
            timer_running=reader.get_bool("timer_running", False),
            id_font_size=reader.get_int("id_font_size", 48),
            name_font_size=reader.get_int("name_font_size", 60),
            timer_font_size=reader.get_int("timer_font_size", 56),
            current_class=reader.get_str("current_class", ""),
            class_states=reader.get_str("class_states", ""),
            group_remaining=reader.get_str("group_remaining", ""),
            group_last=reader.get_str("group_last", ""),
            global_drawn=reader.get_str("global_drawn", ""),
            remote_roll_enabled=reader.get_bool("remote_roll_enabled", False),
            remote_roll_key=reader.get_str("remote_roll_key", "tab"),
        )

    def to_mapping(self) -> Dict[str, str]:
        return {
            "geometry": self.geometry,
            "show_id": bool_to_str(self.show_id),
            "show_name": bool_to_str(self.show_name),
            "show_photo": bool_to_str(self.show_photo),
            "photo_duration_seconds": str(int(self.photo_duration_seconds)),
            "photo_shared_class": str(self.photo_shared_class or ""),
            "speech_enabled": bool_to_str(self.speech_enabled),
            "speech_voice_id": self.speech_voice_id,
            "speech_output_id": self.speech_output_id,
            "speech_engine": self.speech_engine,
            "current_group": self.current_group,
            "timer_countdown_minutes": str(int(self.timer_countdown_minutes)),
            "timer_countdown_seconds": str(int(self.timer_countdown_seconds)),
            "timer_sound_enabled": bool_to_str(self.timer_sound_enabled),
            "timer_sound_variant": self.timer_sound_variant,
            "timer_reminder_enabled": bool_to_str(self.timer_reminder_enabled),
            "timer_reminder_interval_minutes": str(int(self.timer_reminder_interval_minutes)),
            "timer_reminder_sound_variant": self.timer_reminder_sound_variant,
            "mode": self.mode,
            "timer_mode": self.timer_mode,
            "timer_seconds_left": str(int(self.timer_seconds_left)),
            "timer_stopwatch_seconds": str(int(self.timer_stopwatch_seconds)),
            "timer_running": bool_to_str(self.timer_running),
            "id_font_size": str(int(self.id_font_size)),
            "name_font_size": str(int(self.name_font_size)),
            "timer_font_size": str(int(self.timer_font_size)),
            "current_class": self.current_class,
            "class_states": self.class_states,
            "group_remaining": self.group_remaining,
            "group_last": self.group_last,
            "global_drawn": self.global_drawn,
            "remote_roll_enabled": bool_to_str(self.remote_roll_enabled),
            "remote_roll_key": self.remote_roll_key,
        }


_UNIFIED_PEN_CONFIG = PenStyleConfig(
    key="pen",
    display_name="画笔",
    description="统一笔型，线条均匀顺滑，收笔快速收尖。",
    slider_range=(1, 50),
    default_base=12,
    width_multiplier=1.0,
    smoothing=0.9,
    speed_base_multiplier=22.0,
    speed_base_offset=30.0,
    target_min_factor=0.92,
    target_speed_factor=0.05,
    target_curve_factor=0.05,
    target_blend=0.16,
    curve_sensitivity=0.35,
    pressure_factor=0.1,
    width_memory=0.97,
    pressure_time_weight=1.8,
    travel_weight=0.22,
    fade_min_alpha=255,
    fade_max_alpha=255,
    fade_speed_weight=0.0,
    fade_curve_weight=0.0,
    base_alpha=255,
    shadow_alpha=0,
    shadow_alpha_scale=0.0,
    shadow_width_scale=1.0,
    texture=None,
    composition_mode=QPainter.CompositionMode.CompositionMode_SourceOver,
    color_lighten=1.0,
    target_max_factor=1.06,
    width_change_limit=0.004,
    noise_strength=0.0,
    fill_alpha_boost=0,
    feather_strength=0.0,
    edge_highlight_alpha=0,
    solid_fill=False,
    opacity_range=(0, 255),
    default_opacity=255,
    target_responsiveness=0.2,
    width_accel=0.06,
    width_velocity_limit=0.08,
    width_velocity_damping=0.9,
    width_gamma=1.0,
    entry_taper_distance=10.0,
    entry_taper_strength=0.18,
    entry_taper_curve=1.1,
    exit_taper_speed=220.0,
    exit_taper_strength=0.6,
    exit_taper_curve=1.2,
    tail_alpha_fade=0.0,
    jitter_strength=0.0,
)

PEN_STYLE_CONFIGS: Dict[PenStyle, PenStyleConfig] = {
    PenStyle.WHITEBOARD: _UNIFIED_PEN_CONFIG,
}

def get_pen_style_config(style: PenStyle) -> PenStyleConfig:
    return PEN_STYLE_CONFIGS.get(style, PEN_STYLE_CONFIGS[_DEFAULT_PEN_STYLE])


def clamp_base_size_for_style(style: PenStyle, base_size: float) -> float:
    config = get_pen_style_config(style)
    minimum, maximum = config.slider_range
    return float(clamp(base_size, minimum, maximum))


def configure_pen_for_style(
    pen: QPen,
    shadow_pen: QPen,
    color: QColor,
    width: float,
    fade_alpha: int,
    style: PenStyle,
    *,
    base_alpha_override: Optional[int] = None,
    shadow_alpha_override: Optional[int] = None,
    alpha_scale: float = 1.0,
) -> QColor:
    config = get_pen_style_config(style)
    effective_width = max(0.35, float(width))
    base_color = QColor(color)
    if config.color_lighten and abs(config.color_lighten - 1.0) > 1e-3:
        light_factor = max(25, min(400, int(config.color_lighten * 100)))
        base_color = base_color.lighter(light_factor)
    target_alpha = base_alpha_override if base_alpha_override is not None else config.base_alpha
    target_alpha = int(clamp(target_alpha, 0, 255))
    if target_alpha < 255:
        base_color.setAlpha(target_alpha)
    pen_color = QColor(base_color)
    pen.setColor(pen_color)
    pen.setWidthF(effective_width)
    pen.setStyle(Qt.PenStyle.SolidLine)
    pen.setCapStyle(Qt.PenCapStyle.RoundCap)
    pen.setJoinStyle(Qt.PenJoinStyle.RoundJoin)
    if config.texture is not None:
        pen.setBrush(QBrush(base_color, config.texture))
    else:
        pen.setBrush(QBrush(base_color))
    pen.setCosmetic(False)

    shadow_color = QColor(base_color)
    shadow_alpha = shadow_alpha_override if shadow_alpha_override is not None else config.shadow_alpha
    if shadow_alpha <= 0 and config.shadow_alpha_scale <= 0:
        shadow_color.setAlpha(0)
    else:
        composite_alpha = int(
            clamp(
                shadow_alpha + fade_alpha * config.shadow_alpha_scale * max(0.0, alpha_scale),
                0,
                255,
            )
        )
        shadow_color.setAlpha(composite_alpha)
    shadow_pen.setColor(shadow_color)
    shadow_pen.setWidthF(effective_width * config.shadow_width_scale)
    shadow_pen.setStyle(Qt.PenStyle.SolidLine)
    shadow_pen.setCapStyle(Qt.PenCapStyle.RoundCap)
    shadow_pen.setJoinStyle(Qt.PenJoinStyle.RoundJoin)
    if config.texture is not None:
        shadow_pen.setBrush(QBrush(shadow_color, config.texture))
    else:
        shadow_pen.setBrush(QBrush(shadow_color))
    shadow_pen.setCosmetic(False)
    return base_color


def resolve_pen_opacity(
    config: PenStyleConfig,
    override_alpha: Optional[int],
) -> tuple[int, int, int, int, float]:
    """Return (target_alpha, fade_min, fade_max, shadow_alpha, scale)."""

    base_alpha = int(config.base_alpha)
    if config.opacity_range is not None:
        min_alpha, max_alpha = config.opacity_range
    else:
        min_alpha = base_alpha
        max_alpha = base_alpha
    if config.default_opacity is not None:
        default_alpha = int(config.default_opacity)
    else:
        default_alpha = base_alpha
    target_alpha = override_alpha if override_alpha is not None else default_alpha
    target_alpha = int(clamp(target_alpha, min_alpha, max_alpha))
    scale = 1.0
    if base_alpha > 0:
        scale = max(0.0, target_alpha / float(base_alpha))
    fade_min = int(clamp(config.fade_min_alpha * scale, 0, 255))
    fade_max = int(clamp(config.fade_max_alpha * scale, fade_min, 255))
    shadow_alpha = int(clamp(config.shadow_alpha * scale, 0, 255))
    return target_alpha, fade_min, fade_max, shadow_alpha, scale


def render_pen_preview_pixmap(
    color: QColor,
    style: PenStyle,
    base_size: float,
    *,
    size: Optional[QSize] = None,
    opacity_override: Optional[int] = None,
) -> QPixmap:
    if size is None:
        size = QSize(200, 64)
    width = max(60, size.width())
    height = max(36, size.height())
    pixmap = QPixmap(width, height)
    pixmap.fill(QColor(255, 255, 255, 0))

    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    painter.fillRect(pixmap.rect(), QColor(255, 255, 255, 230))
    painter.setPen(QPen(QColor(0, 0, 0, 28), 1))
    painter.drawRoundedRect(pixmap.rect().adjusted(0, 0, -1, -1), 8, 8)

    config = get_pen_style_config(style)
    base_width = clamp_base_size_for_style(style, base_size)
    effective_width = max(1.0, float(base_width) * config.width_multiplier)

    target_alpha, _fade_min, fade_max, shadow_alpha, alpha_scale = resolve_pen_opacity(
        config, opacity_override
    )

    pen = QPen()
    shadow_pen = QPen()
    base_color = configure_pen_for_style(
        pen,
        shadow_pen,
        color,
        effective_width,
        fade_max,
        style,
        base_alpha_override=target_alpha,
        shadow_alpha_override=shadow_alpha,
        alpha_scale=alpha_scale,
    )

    path = QPainterPath(QPointF(14, height * 0.7))
    path.cubicTo(
        QPointF(width * 0.35, height * 0.15),
        QPointF(width * 0.55, height * 0.95),
        QPointF(width - 16, height * 0.38),
    )

    painter.setPen(shadow_pen)
    painter.drawPath(path)
    painter.setPen(pen)
    painter.drawPath(path)
    painter.end()
    return pixmap


# ---------- 自绘置顶 ToolTip ----------
class TipWindow(QWidget):
    """一个轻量的自绘 ToolTip，确保位于所有置顶窗之上。"""
    def __init__(self) -> None:
        super().__init__(None, Qt.WindowType.Tool | Qt.WindowType.FramelessWindowHint | Qt.WindowType.WindowStaysOnTopHint)
        self.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, True)
        self.setStyleSheet(
            f"""
            QLabel {{
                color: #E8EAED;
                background: qlineargradient(x1:0, y1:0, x2:0, y2:1,
                    stop:0 rgba(32, 35, 42, 250),
                    stop:1 rgba(25, 28, 35, 250));
                border: 1px solid rgba(120, 130, 150, 60);
                border-radius: 10px;
                padding: 7px 14px;
                font-size: 12px;
            }}
            """
        )
        self._label = QLabel("", self)
        self._label.setAlignment(Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter)
        self._hide_timer = QTimer(self)
        self._hide_timer.setSingleShot(True)
        self._hide_timer.timeout.connect(self.hide)

    def show_tip(self, text: str, pos: QPoint, duration_ms: int = 2500) -> None:
        self._label.setText(text or "")
        self._label.adjustSize()
        self.resize(self._label.size())
        self.move(pos + QPoint(12, 16))
        self.show()
        self.raise_()
        self._hide_timer.start(duration_ms)

    def hide_tip(self) -> None:
        self._hide_timer.stop()
        self.hide()


# ---------- 对话框 ----------
class _EnsureOnScreenMixin:
    def showEvent(self, event) -> None:  # type: ignore[override]
        super().showEvent(event)
        ensure_widget_within_screen(self)  # type: ignore[arg-type]


class PenSettingsDialog(_EnsureOnScreenMixin, QDialog):
    """画笔粗细与颜色选择对话框。"""

    COLOR_CHOICES: List[Tuple[str, str]] = [
        ("#000000", "黑"),
        ("#FF0000", "红"),
        ("#1E90FF", "蓝"),
        ("#24B47E", "绿"),
        ("#FFFF00", "黄"),
        ("#FFA500", "橙"),
        ("#800080", "紫"),
        ("#FFFFFF", "白"),
    ]
    SIZE_RANGE = (1, 50)

    def __init__(
        self,
        parent: Optional[QWidget] = None,
        initial_base_size: float = 12,
        initial_color: str = "#FF0000",
        initial_opacity: int = 255,
        initial_eraser_size: float = 24.0,
        initial_ui_scale: float = 1.0,
        initial_mode: str = "brush",
        initial_shape_type: Optional[str] = None,
        initial_wps_input_mode: str = "auto",
        initial_wps_raw_input: bool = False,
        initial_wps_wheel_forward: bool = False,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle("画笔设置")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)

        self.pen_color = QColor(initial_color)
        if not self.pen_color.isValid():
            self.pen_color = QColor("#FF0000")

        self._preview_size = QSize(220, 76)
        self._initial_base_size = float(clamp(initial_base_size, *self.SIZE_RANGE))
        self._opacity_alpha = int(clamp(initial_opacity, 0, 255))
        self._initial_eraser_size = float(clamp(initial_eraser_size, *self.SIZE_RANGE))
        self._eraser_size = float(self._initial_eraser_size)
        self._ui_scale = float(clamp(initial_ui_scale, 0.8, 2.0))
        self._initial_mode = str(initial_mode or "brush")
        raw_shape = str(initial_shape_type or "").strip()
        self._initial_shape_type = raw_shape if raw_shape in SHAPE_TYPES else ""
        self._initial_wps_input_mode = _normalize_wps_input_mode(initial_wps_input_mode)
        self._initial_wps_raw_input = bool(initial_wps_raw_input)
        self._initial_wps_wheel_forward = bool(initial_wps_wheel_forward)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(8)

        # ========== 基本画笔设置组 ==========
        basic_group = QGroupBox("基本画笔设置")
        basic_layout = QVBoxLayout(basic_group)
        basic_layout.setSpacing(6)

        size_layout = QHBoxLayout()
        size_layout.setContentsMargins(0, 0, 0, 0)
        size_layout.setSpacing(6)
        size_label = QLabel("笔画粗细:")
        self.size_slider = QSlider(Qt.Orientation.Horizontal)
        self.size_slider.setMinimumWidth(140)
        self.size_value = QLabel("")
        size_layout.addWidget(size_label)
        size_layout.addWidget(self.size_slider, 1)
        size_layout.addWidget(self.size_value)
        basic_layout.addLayout(size_layout)

        self.opacity_container = QWidget(self)
        opacity_layout = QHBoxLayout(self.opacity_container)
        opacity_layout.setContentsMargins(0, 0, 0, 0)
        opacity_layout.setSpacing(6)
        self.opacity_label = QLabel("透明度:")
        self.opacity_slider = QSlider(Qt.Orientation.Horizontal)
        self.opacity_slider.setRange(0, 100)
        self.opacity_slider.setMinimumWidth(140)
        self.opacity_value = QLabel("")
        opacity_layout.addWidget(self.opacity_label)
        opacity_layout.addWidget(self.opacity_slider, 1)
        opacity_layout.addWidget(self.opacity_value)
        basic_layout.addWidget(self.opacity_container)

        self.preview_label = QLabel(self)
        self.preview_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.preview_label.setMinimumSize(self._preview_size)
        basic_layout.addWidget(self.preview_label, 0, Qt.AlignmentFlag.AlignCenter)

        basic_layout.addWidget(QLabel("临时更换画笔的颜色:"))
        color_layout = QGridLayout()
        color_layout.setContentsMargins(0, 0, 0, 0)
        color_layout.setSpacing(6)
        for index, (color_hex, name) in enumerate(self.COLOR_CHOICES):
            button = QPushButton()
            button.setFixedSize(26, 26)
            button.setCursor(Qt.CursorShape.PointingHandCursor)
            button.setStyleSheet(
                f"background-color: {color_hex}; border: 1px solid rgba(0, 0, 0, 60); border-radius: 13px;"
            )
            button.setToolTip(name)
            button.clicked.connect(lambda _checked=False, c=color_hex: self._select_color(c))
            color_layout.addWidget(button, index // 4, index % 4)
        basic_layout.addLayout(color_layout)

        eraser_layout = QHBoxLayout()
        eraser_layout.setContentsMargins(0, 0, 0, 0)
        eraser_layout.setSpacing(6)
        eraser_icon = QLabel()
        eraser_pix = IconManager.get_icon("eraser").pixmap(18, 18)
        eraser_icon.setPixmap(eraser_pix)
        eraser_icon.setFixedSize(20, 20)
        eraser_label = QLabel("橡皮擦粗细:")
        self.eraser_slider = QSlider(Qt.Orientation.Horizontal)
        self.eraser_slider.setMinimumWidth(140)
        self.eraser_value = QLabel("")
        eraser_layout.addWidget(eraser_icon)
        eraser_layout.addWidget(eraser_label)
        eraser_layout.addWidget(self.eraser_slider, 1)
        eraser_layout.addWidget(self.eraser_value)
        basic_layout.addLayout(eraser_layout)

        shape_layout = QHBoxLayout()
        shape_layout.setContentsMargins(0, 0, 0, 0)
        shape_layout.setSpacing(6)
        shape_label = QLabel("图形工具:")
        self.shape_combo = QComboBox(self)
        self.shape_combo.setSizeAdjustPolicy(QComboBox.SizeAdjustPolicy.AdjustToContents)
        self.shape_combo.addItem("无", "")
        self.shape_combo.addItem("直线", "line")
        self.shape_combo.addItem("虚线", "dashed_line")
        self.shape_combo.addItem("矩形", "rect")
        self.shape_combo.addItem("矩形（实心）", "rect_fill")
        self.shape_combo.addItem("圆形", "circle")
        self.shape_combo.setToolTip("选择后，点「确定」会切换到图形工具；取消则不改变当前工具。")
        shape_layout.addWidget(shape_label)
        shape_layout.addWidget(self.shape_combo, 1)
        basic_layout.addLayout(shape_layout)

        scale_layout = QHBoxLayout()
        scale_layout.setContentsMargins(0, 0, 0, 0)
        scale_layout.setSpacing(6)
        scale_label = QLabel("画笔工具条的界面大小（缩放比例）:")
        self.scale_combo = QComboBox(self)
        self.scale_combo.setSizeAdjustPolicy(QComboBox.SizeAdjustPolicy.AdjustToContents)
        self._scale_choices = [0.8, 1.0, 1.25, 1.5, 1.75, 2.0]
        for value in self._scale_choices:
            percent = int(round(value * 100))
            self.scale_combo.addItem(f"{percent}%", value)
        nearest = min(self._scale_choices, key=lambda v: abs(v - self._ui_scale))
        self._ui_scale = nearest
        self.scale_combo.setCurrentIndex(self._scale_choices.index(nearest))
        self.scale_combo.currentIndexChanged.connect(self._on_scale_changed)
        scale_layout.addWidget(scale_label)
        scale_layout.addWidget(self.scale_combo, 1)
        basic_layout.addLayout(scale_layout)

        layout.addWidget(basic_group)

        # ========== WPS 兼容性设置组 ==========
        wps_group = QGroupBox("WPS 放映兼容性设置")
        wps_layout = QVBoxLayout(wps_group)
        wps_layout.setSpacing(6)

        wps_mode_layout = QHBoxLayout()
        wps_mode_layout.setContentsMargins(0, 0, 0, 0)
        wps_mode_layout.setSpacing(6)
        wps_mode_label = QLabel("兼容策略:")
        self.wps_mode_combo = QComboBox(self)
        self.wps_mode_combo.setSizeAdjustPolicy(QComboBox.SizeAdjustPolicy.AdjustToContents)
        self.wps_mode_combo.addItem("自动判断（推荐）", "auto")
        self.wps_mode_combo.addItem("强制原始输入（SendInput）", "raw")
        self.wps_mode_combo.addItem("强制消息投递（PostMessage）", "message")
        wps_mode_layout.addWidget(wps_mode_label)
        wps_mode_layout.addWidget(self.wps_mode_combo, 1)
        wps_layout.addLayout(wps_mode_layout)

        self.wps_wheel_forward_check = QCheckBox("滚轮映射为翻页键（解决双动画/无动画）")
        self.wps_wheel_forward_check.setToolTip("用于将滚轮映射为翻页键，减少双动画或无动画问题")
        wps_layout.addWidget(self.wps_wheel_forward_check)

        layout.addWidget(wps_group)

        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        style_dialog_buttons(
            buttons,
            {
                QDialogButtonBox.StandardButton.Ok: ButtonStyles.TOOLBAR,
                QDialogButtonBox.StandardButton.Cancel: ButtonStyles.TOOLBAR,
            },
            extra_padding=10,
            minimum_height=32,
            uniform_width=True,  # 确保确定和取消按钮宽度相同
        )
        ok_button = buttons.button(QDialogButtonBox.StandardButton.Ok)
        if ok_button is not None:
            ok_button.setText("确定")
        cancel_button = buttons.button(QDialogButtonBox.StandardButton.Cancel)
        if cancel_button is not None:
            cancel_button.setText("取消")
        layout.addWidget(buttons)

        # 初始化数据与事件
        self.size_slider.setRange(*self.SIZE_RANGE)
        self.size_slider.setValue(int(round(self._initial_base_size)))
        self.size_slider.valueChanged.connect(self._on_size_changed)
        self.opacity_slider.setValue(self._alpha_to_percent(self._opacity_alpha))
        self.opacity_slider.valueChanged.connect(self._on_opacity_changed)
        self.eraser_slider.setRange(*self.SIZE_RANGE)
        self.eraser_slider.setValue(int(round(self._initial_eraser_size)))
        self.eraser_slider.valueChanged.connect(self._on_eraser_size_changed)
        self._update_eraser_label()
        self.wps_wheel_forward_check.setChecked(self._initial_wps_wheel_forward)
        self._init_wps_mode_selection()
        self.wps_mode_combo.currentIndexChanged.connect(self._on_wps_mode_changed)

        desired_shape = self._initial_shape_type if self._initial_mode == "shape" else ""
        for idx in range(self.shape_combo.count()):
            if str(self.shape_combo.itemData(idx) or "") == desired_shape:
                self.shape_combo.setCurrentIndex(idx)
                break

        self._update_size_label()
        self._update_opacity_label()
        self._update_preview()
        self.setFixedSize(self.sizeHint())

    def _update_size_label(self) -> None:
        base_value = int(self.size_slider.value())
        self.size_value.setText(f"{base_value}px")

    def _update_eraser_label(self) -> None:
        value = int(clamp(self.eraser_slider.value(), *self.SIZE_RANGE))
        if self.eraser_slider.value() != value:
            prev = self.eraser_slider.blockSignals(True)
            self.eraser_slider.setValue(value)
            self.eraser_slider.blockSignals(prev)
        self._eraser_size = float(value)
        self.eraser_value.setText(f"{value}px")

    def _update_opacity_label(self) -> None:
        percent = self._alpha_to_percent(self._opacity_alpha)
        self.opacity_value.setText(f"{percent}%")

    def _alpha_to_percent(self, alpha: int) -> int:
        percent = int(round(clamp(alpha, 0, 255) * 100 / 255))
        return int(clamp(percent, 0, 100))

    def _percent_to_alpha(self, percent: int) -> int:
        percent = int(clamp(percent, 0, 100))
        return int(round(255 * (percent / 100.0)))

    def _update_preview(self) -> None:
        pixmap = render_pen_preview_pixmap(
            self.pen_color,
            _DEFAULT_PEN_STYLE,
            float(self.size_slider.value()),
            size=self._preview_size,
            opacity_override=self._opacity_alpha,
        )
        self.preview_label.setPixmap(pixmap)
        self.preview_label.setFixedSize(pixmap.size())

    def _on_size_changed(self) -> None:
        value = float(clamp(self.size_slider.value(), *self.SIZE_RANGE))
        if int(self.size_slider.value()) != int(round(value)):
            prev = self.size_slider.blockSignals(True)
            self.size_slider.setValue(int(round(value)))
            self.size_slider.blockSignals(prev)
        self._update_size_label()
        self._update_preview()

    def _on_eraser_size_changed(self) -> None:
        self._update_eraser_label()

    def _on_opacity_changed(self) -> None:
        percent = int(self.opacity_slider.value())
        self._opacity_alpha = self._percent_to_alpha(percent)
        self._update_opacity_label()
        self._update_preview()

    def _on_scale_changed(self) -> None:
        idx = self.scale_combo.currentIndex()
        try:
            value = float(self.scale_combo.itemData(idx))
        except Exception:
            return
        self._ui_scale = float(clamp(value, 0.8, 2.0))

    def _select_color(self, color_hex: str) -> None:
        color = QColor(color_hex)
        if not color.isValid():
            return
        self.pen_color = color
        self._update_preview()

    def _init_wps_mode_selection(self) -> None:
        mode = self._initial_wps_input_mode
        if mode == "manual":
            mode = "raw" if self._initial_wps_raw_input else "message"
        _set_combo_value(self.wps_mode_combo, mode)
        self._update_wps_controls()

    def _on_wps_mode_changed(self) -> None:
        self._update_wps_controls()

    def _update_wps_controls(self) -> None:
        try:
            mode = str(self.wps_mode_combo.currentData())
        except Exception:
            mode = "auto"
        allow_wheel = mode in {"auto", "raw", "message"}
        self.wps_wheel_forward_check.setEnabled(allow_wheel)

    def get_settings(
        self,
    ) -> tuple[
        float,
        QColor,
        int,
        float,
        float,
        Optional[str],
        str,
        bool,
    ]:
        return (
            float(clamp(self.size_slider.value(), *self.SIZE_RANGE)),
            QColor(self.pen_color),
            int(clamp(self._opacity_alpha, 0, 255)),
            float(clamp(self._eraser_size, *self.SIZE_RANGE)),
            float(self._ui_scale),
            self._selected_shape_choice(),
            str(self.wps_mode_combo.currentData()),
            bool(self.wps_wheel_forward_check.isChecked()),
        )

    def _selected_shape_choice(self) -> Optional[str]:
        idx = self.shape_combo.currentIndex()
        try:
            value = str(self.shape_combo.itemData(idx) or "").strip()
        except Exception:
            value = ""
        if not value:
            return None
        if value not in SHAPE_TYPES:
            return None
        return value

class BoardColorDialog(_EnsureOnScreenMixin, QDialog):
    """白板背景颜色选择对话框。"""
    COLORS = {"#FFFFFF": "白板", "#000000": "黑板", "#0E4020": "绿板"}

    def __init__(self, parent: Optional[QWidget] = None) -> None:
        super().__init__(parent)
        self.setWindowTitle("选择颜色")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.selected_color: Optional[QColor] = None

        layout = QHBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(6)

        for color_hex, name in self.COLORS.items():
            text_color = self._contrast_text_color(color_hex)
            button = QPushButton(name)
            button.setCursor(Qt.CursorShape.PointingHandCursor)
            button.setFixedSize(72, 28)
            button.setStyleSheet(
                f"background-color: {color_hex}; color: {text_color};"
                "border: 1px solid rgba(0, 0, 0, 120); border-radius: 4px;"
            )
            button.clicked.connect(lambda _checked, c=color_hex: self._select_color(c))
            layout.addWidget(button)

        self.setFixedSize(self.sizeHint())

    @staticmethod
    def _contrast_text_color(color_hex: str) -> str:
        color = QColor(color_hex)
        if not color.isValid():
            return "#000000"
        r, g, b, _ = color.getRgb()
        luminance = (0.299 * r + 0.587 * g + 0.114 * b)
        return "#000000" if luminance >= 160 else "#ffffff"

    def _select_color(self, color_hex: str) -> None:
        self.selected_color = QColor(color_hex)
        self.accept()

    def get_color(self) -> Optional[QColor]:
        return self.selected_color

    

# ---------- 标题栏 ----------
class TitleBar(QWidget):
    """浮动工具条的标题栏，负责拖拽移动。"""

    def __init__(self, toolbar: "FloatingToolbar", *, scale: float = 1.0) -> None:
        super().__init__(toolbar)
        self.toolbar = toolbar
        self._dragging = False
        self._drag_offset = QPoint()
        self._scale = float(clamp(scale, 0.8, 2.0))
        self.setCursor(Qt.CursorShape.OpenHandCursor)

        self.setAutoFillBackground(True)
        palette = self.palette()
        palette.setColor(self.backgroundRole(), QColor(36, 37, 41, 235))
        self.setPalette(palette)

        self._layout = QHBoxLayout(self)
        self._title = QLabel("屏幕画笔")
        self._layout.addWidget(self._title)
        self._layout.addStretch()
        self.apply_scale(self._scale)

    def apply_scale(self, scale: float) -> None:
        self._scale = float(clamp(scale, 0.8, 2.0))
        height = max(14, int(round(22 * self._scale)))
        self.setFixedHeight(height)
        margin = max(2, int(round(6 * self._scale)))
        self._layout.setContentsMargins(margin, 0, margin, 0)
        font = self._title.font()
        font.setBold(True)
        font.setPointSizeF(max(8.0, 10.5 * self._scale))
        self._title.setFont(font)
        self._title.setStyleSheet("color: #e8eaed;")

    def mousePressEvent(self, event) -> None:
        if event.button() == Qt.MouseButton.LeftButton:
            self._dragging = True
            self._drag_offset = event.globalPosition().toPoint() - self.toolbar.pos()
            self.setCursor(Qt.CursorShape.ClosedHandCursor)
            event.accept()
        super().mousePressEvent(event)

    def mouseMoveEvent(self, event) -> None:
        if self._dragging:
            self.toolbar.move(event.globalPosition().toPoint() - self._drag_offset)
            event.accept()
        super().mouseMoveEvent(event)

    def mouseReleaseEvent(self, event) -> None:
        if self._dragging:
            self.toolbar.overlay.save_window_position()
        self._dragging = False
        self.setCursor(Qt.CursorShape.OpenHandCursor)
        try:
            self.toolbar.overlay.raise_toolbar()
        except Exception:
            pass
        event.accept()
        super().mouseReleaseEvent(event)


# ---------- 浮动工具条（画笔/白板） ----------
class FloatingToolbar(_EnsureOnScreenMixin, QWidget):
    """悬浮工具条：提供画笔、白板等常用按钮。"""

    def __init__(
        self,
        overlay: "OverlayWindow",
        settings_manager: SettingsManager,
        *,
        ui_scale: float = 1.0,
    ) -> None:
        super().__init__(
            None,
            Qt.WindowType.Tool | Qt.WindowType.FramelessWindowHint | Qt.WindowType.WindowStaysOnTopHint,
        )
        self.overlay = overlay
        self.settings_manager = settings_manager
        self.ui_scale = float(clamp(ui_scale, 0.8, 2.0))
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setAttribute(Qt.WidgetAttribute.WA_AlwaysShowToolTips, True)
        self.setWindowFlag(Qt.WindowType.WindowDoesNotAcceptFocus, True)
        self._tip = TipWindow()
        self._build_ui()
        self._whiteboard_locked = False
        # 启用鼠标跟踪，以便在绘图时能接收鼠标移动事件并转发到 overlay
        self.setMouseTracking(True)

        settings = self.settings_manager.load_settings().get("Paint", {})
        self.move(int(settings.get("x", "260")), int(settings.get("y", "260")))
        self.adjustSize()
        self.setFixedSize(self.sizeHint())
        self._base_minimum_width = self.width()
        self._base_minimum_height = self.height()
        self._ensure_min_width = self.width()
        self._ensure_min_height = self.height()

    def _scaled(self, value: float) -> int:
        return max(1, int(round(value * self.ui_scale)))

    def _apply_toolbar_stylesheet(self) -> None:
        """Apply floating toolbar stylesheet only to the toolbar container,避免波及设置对话框。"""
        target = getattr(self, "_style_container", None) or self
        target.setStyleSheet(StyleConfig.floating_toolbar_style(self.ui_scale))

    def apply_ui_scale(self, scale: float) -> None:
        self.ui_scale = float(clamp(scale, 0.8, 2.0))
        self._apply_toolbar_stylesheet()
        spacing_small = self._scaled(3)
        spacing_large = self._scaled(4)
        margin_h = self._scaled(6)
        margin_v_top = self._scaled(4)
        margin_v_bottom = self._scaled(5)
        if isinstance(self.layout(), QVBoxLayout):
            self.layout().setContentsMargins(0, 0, 0, 0)
        container = self.findChild(QWidget, "container")
        if container is not None and isinstance(container.layout(), QVBoxLayout):
            layout = container.layout()
            layout.setContentsMargins(margin_h, margin_v_top, margin_h, margin_v_bottom)
            layout.setSpacing(spacing_large)
        if hasattr(self, "title_bar") and isinstance(self.title_bar, TitleBar):
            self.title_bar.apply_scale(self.ui_scale)
        for row in (
            getattr(self, "_row_top", None),
            getattr(self, "_row_bottom", None),
        ):
            if isinstance(row, QHBoxLayout):
                row.setSpacing(spacing_small)
                row.setContentsMargins(0, 0, 0, 0)
        icon_size = self._scaled(16)
        min_size = self._scaled(28)
        for btn in getattr(self, "_all_buttons", []):
            btn.setIconSize(QSize(icon_size, icon_size))
            btn.setMinimumSize(min_size, min_size)
        self.adjustSize()
        self.setFixedSize(self.sizeHint())
        self._base_minimum_width = self.width()
        self._base_minimum_height = self.height()
        self._ensure_min_width = self.width()
        self._ensure_min_height = self.height()

    def _build_ui(self) -> None:
        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        container = QWidget(self)
        container.setObjectName("container")
        self._style_container = container
        self._apply_toolbar_stylesheet()
        root.addWidget(container)

        layout = QVBoxLayout(container)
        layout.setContentsMargins(self._scaled(6), self._scaled(4), self._scaled(6), self._scaled(5))
        layout.setSpacing(self._scaled(4))
        self.title_bar = TitleBar(self, scale=self.ui_scale)
        layout.addWidget(self.title_bar)

        self.btn_cursor = QPushButton(IconManager.get_icon("cursor"), "")
        self.quick_colors: List[str] = [c.lower() for c in self.overlay.get_quick_colors()]
        self._palette_colors: List[Tuple[str, str]] = list(PenSettingsDialog.COLOR_CHOICES)
        self.brush_color_buttons: List[QPushButton] = []
        brush_buttons: List[QPushButton] = []
        for idx, color_hex in enumerate(self.quick_colors):
            button = QPushButton(IconManager.get_brush_icon(color_hex), "")
            # 常用画笔统一提示：长按可换色
            button.setToolTip("长按更换颜色")
            self.brush_color_buttons.append(button)
            brush_buttons.append(button)
        self.btn_settings = QPushButton(IconManager.get_icon("settings"), "")
        self.btn_undo = QPushButton(IconManager.get_icon("undo"), "")
        self.btn_eraser = QPushButton(IconManager.get_icon("eraser"), "")
        self.btn_eraser.setObjectName("eraserButton")
        self.btn_region_delete = QPushButton(IconManager.get_icon("selection_delete"), "")
        self.btn_region_delete.setObjectName("regionDeleteButton")
        self.btn_clear_all = QPushButton(IconManager.get_icon("clear_all"), "")
        self.btn_clear_all.setObjectName("clearButton")
        self.btn_whiteboard = QPushButton(IconManager.get_icon("whiteboard"), "")

        row_top = QHBoxLayout()
        row_top.setContentsMargins(0, 0, 0, 0)
        row_top.setSpacing(self._scaled(3))
        row_bottom = QHBoxLayout()
        row_bottom.setContentsMargins(0, 0, 0, 0)
        row_bottom.setSpacing(self._scaled(3))
        self._row_top = row_top
        self._row_bottom = row_bottom

        top_buttons = [
            self.btn_cursor,
            *brush_buttons,
            self.btn_settings,
        ]
        bottom_buttons = [
            self.btn_undo,
            self.btn_eraser,
            self.btn_region_delete,
            self.btn_clear_all,
            self.btn_whiteboard,
        ]

        def _configure_toolbar_button(btn: QPushButton) -> None:
            btn.setIconSize(QSize(self._scaled(16), self._scaled(16)))
            btn.setCursor(Qt.CursorShape.PointingHandCursor)
            btn.setFocusPolicy(Qt.FocusPolicy.NoFocus)
            btn.setMinimumSize(self._scaled(28), self._scaled(28))

        self._all_buttons: List[QPushButton] = []
        for btn in top_buttons + bottom_buttons:
            _configure_toolbar_button(btn)
            self._all_buttons.append(btn)
        for btn in top_buttons:
            row_top.addWidget(btn)
        for btn in bottom_buttons:
            row_bottom.addWidget(btn)
        layout.addLayout(row_top)
        layout.addLayout(row_bottom)

        tooltip_text = {
            self.btn_cursor: "光标",
            self.btn_undo: "撤销",
            self.btn_eraser: "橡皮擦",
            self.btn_region_delete: "框选删除",
            self.btn_clear_all: "一键清屏",
            self.btn_whiteboard: "白板（单击开关 / 长按换色）",
            self.btn_settings: "画笔设置",
        }
        for button in brush_buttons:
            tooltip_text[button] = button.toolTip() or "画笔"
        for btn, tip_text in tooltip_text.items():
            btn.setToolTip(tip_text)
            btn.installEventFilter(self)

        self.tool_buttons = QButtonGroup(self)
        for btn in (
            self.btn_cursor,
            self.btn_eraser,
            self.btn_region_delete,
        ):
            btn.setCheckable(True)
            self.tool_buttons.addButton(btn)
        self.tool_buttons.setExclusive(True)

        self.color_buttons = QButtonGroup(self)
        for btn in brush_buttons:
            btn.setCheckable(True)
            self.color_buttons.addButton(btn)
        self.color_buttons.setExclusive(True)

        self.btn_cursor.clicked.connect(self.overlay.toggle_cursor_mode)
        self.btn_undo.clicked.connect(self.overlay.undo_last_action)
        self.btn_eraser.clicked.connect(self.overlay.toggle_eraser_mode)
        self.btn_region_delete.clicked.connect(self.overlay.toggle_region_erase_mode)
        self.btn_clear_all.clicked.connect(self.overlay.clear_all)
        self.btn_settings.clicked.connect(self.overlay.open_pen_settings)
        self.btn_whiteboard.pressed.connect(self._handle_whiteboard_pressed)
        self.btn_whiteboard.released.connect(self._handle_whiteboard_released)

        self._BRUSH_LONG_PRESS_MS = 500
        self._brush_hold_timer = QTimer(self)
        self._brush_hold_timer.setSingleShot(True)
        self._brush_hold_timer.timeout.connect(self._on_brush_long_press)
        self._brush_hold_index: Optional[int] = None
        self._brush_hold_triggered = False
        for idx, button in enumerate(self.brush_color_buttons):
            button.pressed.connect(lambda _checked=False, i=idx: self._on_brush_pressed(i))
            button.released.connect(lambda _checked=False, i=idx: self._on_brush_released(i))

        self._WHITEBOARD_LONG_PRESS_MS = 650
        self._wb_long_press_timer = QTimer(self)
        self._wb_long_press_timer.setSingleShot(True)
        self._wb_long_press_timer.timeout.connect(self._handle_whiteboard_long_press)
        self._wb_hold_feedback_timer = QTimer(self)
        self._wb_hold_feedback_timer.setInterval(30)
        self._wb_hold_feedback_timer.timeout.connect(self._update_whiteboard_hold_feedback)
        self._wb_press_started_at: Optional[float] = None
        self._wb_long_press_triggered = False
        self._whiteboard_hold_active = False

        self.btn_undo.setEnabled(False)

        for widget in (self, container, self.title_bar):
            widget.installEventFilter(self)
        self.setCursor(Qt.CursorShape.ArrowCursor)

    def update_tool_states(self, mode: str, pen_color: QColor) -> None:
        color_key = pen_color.name().lower()
        if mode == "brush":
            for idx, button in enumerate(self.brush_color_buttons):
                hex_key = self.quick_colors[idx] if idx < len(self.quick_colors) else ""
                prev = button.blockSignals(True)
                is_active = hex_key.lower() == color_key
                button.setChecked(is_active)
                button.blockSignals(prev)
        else:
            group = getattr(self, "color_buttons", None)
            prev_exclusive = False
            if isinstance(group, QButtonGroup):
                prev_exclusive = group.exclusive()
                if prev_exclusive:
                    group.setExclusive(False)
            for button in self.brush_color_buttons:
                prev = button.blockSignals(True)
                button.setChecked(False)
                button.blockSignals(prev)
            if isinstance(group, QButtonGroup) and prev_exclusive:
                group.setExclusive(True)
        tool_entries = (
            ("cursor", self.btn_cursor),
            ("eraser", self.btn_eraser),
            ("region_erase", self.btn_region_delete),
        )
        if mode in {"cursor", "eraser", "region_erase"}:
            for tool, button in tool_entries:
                prev = button.blockSignals(True)
                button.setChecked(mode == tool)
                button.blockSignals(prev)
        else:
            tool_group = getattr(self, "tool_buttons", None)
            prev_exclusive = False
            if isinstance(tool_group, QButtonGroup):
                prev_exclusive = tool_group.exclusive()
                if prev_exclusive:
                    tool_group.setExclusive(False)
            for _tool, button in tool_entries:
                prev = button.blockSignals(True)
                button.setChecked(False)
                button.blockSignals(prev)
            if isinstance(tool_group, QButtonGroup) and prev_exclusive:
                tool_group.setExclusive(True)
        if mode == "brush":
            self.update_pen_tooltip(
                self.overlay.pen_style,
                self.overlay.pen_base_size,
                self.overlay.pen_size,
                opacity_percent=self.overlay._get_active_opacity_percent(),
            )

    def update_undo_state(self, enabled: bool) -> None:
        self.btn_undo.setEnabled(enabled)

    def update_pen_tooltip(
        self,
        style: PenStyle,
        base_size: float,
        effective_size: int,
        *,
        opacity_percent: Optional[int] = None,
    ) -> None:
        self.btn_settings.setToolTip("画笔设置")

    def eventFilter(self, obj, event):
        event_type = event.type()
        if isinstance(obj, QPushButton) and event_type == QEvent.Type.ToolTip:
            try:
                self.overlay.raise_toolbar()
            except Exception:
                pass
            self._tip.show_tip(obj.toolTip(), QCursor.pos())
            return True
        if event_type in (
            QEvent.Type.Leave,
            QEvent.Type.MouseButtonPress,
            QEvent.Type.MouseButtonDblClick,
        ):
            self._tip.hide_tip()
        return super().eventFilter(obj, event)

    def _on_brush_pressed(self, index: int) -> None:
        self._brush_hold_index = index
        self._brush_hold_triggered = False
        self._brush_hold_timer.start(self._BRUSH_LONG_PRESS_MS)

    def _on_brush_released(self, index: int) -> None:
        if self._brush_hold_index != index:
            return
        triggered = self._brush_hold_triggered
        self._brush_hold_timer.stop()
        self._brush_hold_index = None
        if triggered:
            return
        color = self.quick_colors[index] if index < len(self.quick_colors) else "#000000"
        self.overlay.use_brush_color(color)

    def _on_brush_long_press(self) -> None:
        idx = self._brush_hold_index
        self._brush_hold_triggered = True
        if idx is None:
            return
        self._show_brush_palette(idx)
        self._brush_hold_index = None

    def _show_brush_palette(self, index: int) -> None:
        if index < 0 or index >= len(self.brush_color_buttons):
            return
        button = self.brush_color_buttons[index]
        menu = QMenu(self)
        for color_hex, label in self._palette_colors:
            act = menu.addAction(label)
            act.setData(color_hex)
            act.setIcon(IconManager.get_brush_icon(color_hex))
        action = menu.exec(button.mapToGlobal(button.rect().bottomLeft()))
        if action is None:
            return
        data = action.data()
        try:
            selected = str(data)
        except Exception:
            selected = ""
        normalized = self.overlay.set_quick_color_slot(index, selected)
        self.update_quick_color_slot(index, normalized)
        self.overlay.use_brush_color(normalized)

    def update_quick_color_slot(self, index: int, color_hex: str) -> None:
        if index < 0:
            return
        while len(self.quick_colors) <= index:
            self.quick_colors.append("#000000")
        color = QColor(color_hex)
        if not color.isValid():
            return
        normalized = color.name().lower()
        self.quick_colors[index] = normalized
        try:
            button = self.brush_color_buttons[index]
        except IndexError:
            return
        button.setIcon(IconManager.get_brush_icon(normalized))
        button.setToolTip("长按更换颜色")
        button.update()

    def _handle_whiteboard_pressed(self) -> None:
        self._wb_long_press_triggered = False
        self._wb_press_started_at = time.monotonic()
        self._whiteboard_hold_active = True
        self._apply_whiteboard_hold_progress(0.0)
        self._wb_long_press_timer.start(self._WHITEBOARD_LONG_PRESS_MS)
        self._wb_hold_feedback_timer.start()

    def _handle_whiteboard_released(self) -> None:
        self._wb_long_press_timer.stop()
        triggered = self._wb_long_press_triggered
        self._stop_whiteboard_hold_feedback()
        if triggered:
            return
        self.overlay.toggle_whiteboard()

    def _handle_whiteboard_long_press(self) -> None:
        self._wb_long_press_triggered = True
        self._stop_whiteboard_hold_feedback(reset_style=False)
        self._apply_whiteboard_hold_progress(1.0)
        self.overlay.open_board_color_dialog()
        self._stop_whiteboard_hold_feedback()

    def _update_whiteboard_hold_feedback(self) -> None:
        if not self._whiteboard_hold_active or self._wb_press_started_at is None:
            return
        elapsed_ms = (time.monotonic() - self._wb_press_started_at) * 1000.0
        progress = min(1.0, elapsed_ms / float(self._WHITEBOARD_LONG_PRESS_MS))
        self._apply_whiteboard_hold_progress(progress)
        if progress >= 1.0:
            self._wb_hold_feedback_timer.stop()

    def _stop_whiteboard_hold_feedback(self, *, reset_style: bool = True) -> None:
        self._whiteboard_hold_active = False
        self._wb_hold_feedback_timer.stop()
        if reset_style:
            self.btn_whiteboard.setStyleSheet("")
            self.btn_whiteboard.update()

    @staticmethod
    def _mix_color(start: QColor, end: QColor, ratio: float) -> QColor:
        ratio = max(0.0, min(1.0, ratio))
        return QColor(
            int(start.red() + (end.red() - start.red()) * ratio),
            int(start.green() + (end.green() - start.green()) * ratio),
            int(start.blue() + (end.blue() - start.blue()) * ratio),
            int(start.alpha() + (end.alpha() - start.alpha()) * ratio),
        )

    def _apply_whiteboard_hold_progress(self, progress: float) -> None:
        try:
            base_bg = QColor(StyleConfig.TOOLBAR_BUTTON_BG)
            target_bg = QColor(StyleConfig.WHITEBOARD_ACTIVE_BG)
            base_border = QColor(StyleConfig.TOOLBAR_BORDER)
            target_border = QColor(StyleConfig.WHITEBOARD_ACTIVE_BORDER)
            bg_color = self._mix_color(base_bg, target_bg, progress)
            border_color = self._mix_color(base_border, target_border, progress)
            self.btn_whiteboard.setStyleSheet(
                "background: rgba({r},{g},{b},{a}); border-color: rgba({br},{bg},{bb},{ba});".format(
                    r=bg_color.red(),
                    g=bg_color.green(),
                    b=bg_color.blue(),
                    a=bg_color.alpha(),
                    br=border_color.red(),
                    bg=border_color.green(),
                    bb=border_color.blue(),
                    ba=border_color.alpha(),
                )
            )
            self.btn_whiteboard.update()
        except Exception:
            # 视觉反馈失败时不影响功能。
            pass

    def set_whiteboard_locked(self, locked: bool) -> None:
        self._whiteboard_locked = locked
        # 白板模式时禁用光标按钮
        if hasattr(self, "btn_cursor"):
            self.btn_cursor.setEnabled(not locked)

    def update_whiteboard_button_state(self, active: bool) -> None:
        self.btn_whiteboard.setObjectName("whiteboardButtonActive" if active else "")
        self.style().polish(self.btn_whiteboard)

    def showEvent(self, event) -> None:  # type: ignore[override]
        super().showEvent(event)
        try:
            cursor_pos = QCursor.pos()
            local = self.mapFromGlobal(cursor_pos)
            if self.rect().contains(local):
                self.setCursor(Qt.CursorShape.ArrowCursor)
                try:
                    self.overlay.setCursor(Qt.CursorShape.ArrowCursor)
                except Exception:
                    pass
                try:
                    self.overlay.handle_toolbar_enter()
                except Exception:
                    pass
        except Exception:
            pass

    def enterEvent(self, event) -> None:
        self.setCursor(Qt.CursorShape.ArrowCursor)
        try:
            self.overlay.setCursor(Qt.CursorShape.ArrowCursor)
        except Exception:
            pass
        self.overlay.handle_toolbar_enter()
        self.overlay.raise_toolbar()
        super().enterEvent(event)

    def leaveEvent(self, event) -> None:
        super().leaveEvent(event)
        self.overlay.handle_toolbar_leave()
        try:
            self.overlay.update_cursor()
        except Exception:
            pass
        QTimer.singleShot(0, self.overlay.on_toolbar_mouse_leave)

    def wheelEvent(self, event) -> None:
        handled = False
        forwarder = getattr(self.overlay, "_forwarder", None)
        if forwarder is not None and (
            getattr(self.overlay, "mode", "") == "cursor"
            or getattr(self.overlay, "navigation_active", False)
        ) and not getattr(self.overlay, "whiteboard_active", False):
            try:
                handled = forwarder.forward_wheel(
                    event,
                    allow_cursor=True,
                )
            except Exception:
                handled = False
        if handled:
            event.accept()
            return
        super().wheelEvent(event)

    def mouseReleaseEvent(self, event) -> None:
        """处理鼠标在工具栏上松开时完成正在进行的绘图。"""
        # 如果 overlay 正在绘图且鼠标左键松开，则完成绘图
        if hasattr(self.overlay, "drawing") and self.overlay.drawing:
            if event.button() == Qt.MouseButton.LeftButton:
                # 获取鼠标在 overlay 上的位置
                overlay_pos = self.overlay.mapFromGlobal(event.globalPosition().toPoint())
                # 完成绘图会话
                dirty_region = self.overlay._finalize_paint_session(overlay_pos)
                if dirty_region is not None:
                    self.overlay._apply_dirty_region(dirty_region)
                self.overlay.raise_toolbar()
                event.accept()
                return
        super().mouseReleaseEvent(event)

    def mouseMoveEvent(self, event) -> None:
        """当正在绘图时，直接调用 overlay 的绘画方法继续绘画。

        这解决了画笔划过工具栏区域时笔画中断的问题：
        当鼠标在工具栏上移动时，工具栏会捕获鼠标事件，导致 overlay
        不再接收 mouseMoveEvent，从而停止绘画。此方法直接调用绘画方法，
        避免通过事件循环的延迟，确保绘画立即执行。
        """
        # [调试日志] 追踪工具栏 mouseMoveEvent 调用
        drawing = getattr(self.overlay, "drawing", False)
        mouse_tracking = self.hasMouseTracking()
        logger.debug(f"[Toolbar] mouseMoveEvent: drawing={drawing}, mouseTracking={mouse_tracking}")

        if drawing:
            # 确保 mouseTracking 处于启用状态
            if not mouse_tracking:
                logger.warning("[Toolbar] mouseTracking 未启用，正在启用...")
                self.setMouseTracking(True)
            # 直接调用 overlay 的绘画逻辑，避免事件循环延迟
            if self.overlay.mode != "cursor":
                overlay_pos = self.overlay.mapFromGlobal(event.globalPosition().toPoint())
                overlay_pf = QPointF(overlay_pos)
                dirty_region = None
                if self.overlay.mode == "brush":
                    logger.debug(f"[Toolbar] 调用 _draw_brush_line, pos=({overlay_pos.x()}, {overlay_pos.y()})")
                    dirty_region = self.overlay._draw_brush_line(overlay_pf)
                elif self.overlay.mode == "eraser":
                    dirty_region = self.overlay._erase_at(overlay_pos)
                elif self.overlay.mode == "shape" and self.overlay.current_shape:
                    dirty_region = self.overlay._draw_shape_preview(overlay_pos)
                self.overlay._apply_dirty_region(dirty_region)
        super().mouseMoveEvent(event)

    def showEvent(self, event) -> None:
        """窗口显示时确保 mouseTracking 处于启用状态。

        由于 raise_toolbar() 会频繁调用 show()，而 PyQt6 在某些情况下
        可能会重置窗口状态，这里在每次 show 时重新设置 mouseTracking
        以确保绘画事件转发功能始终有效。
        """
        self.setMouseTracking(True)
        # [调试日志] 确认 showEvent 被调用
        logger.debug(f"[Toolbar] showEvent called, mouseTracking={self.hasMouseTracking()}")
        super().showEvent(event)

# ---------- 叠加层（画笔/白板） ----------


class _PresentationWindowMixin:
    @dataclass(frozen=True, slots=True)
    class _WPSProcessHints:
        classes: Tuple[str, ...]
        has_slideshow: bool
        has_wps_presentation_signature: bool
        has_ms_presentation_signature: bool

    @dataclass(slots=True)
    class _WPSHintFlagState:
        """Mutable container that tracks which WPS hint flags were satisfied."""

        flags: Dict[str, bool]
        remaining: int

        @classmethod
        def from_delegates(
            cls,
            delegates: Tuple[Tuple[str, Callable[[str], bool]], ...],
        ) -> "_PresentationWindowMixin._WPSHintFlagState":
            flags = {flag_name: False for flag_name, _ in delegates}
            return cls(flags, len(flags))

        def mark_flag(self, flag_name: str) -> None:
            if flag_name not in self.flags or self.flags[flag_name]:
                return
            self.flags[flag_name] = True
            self.remaining = max(0, self.remaining - 1)

        def evaluate_class(
            self,
            class_name: str,
            delegates: Tuple[Tuple[str, Callable[[str], bool]], ...],
        ) -> bool:
            for flag_name, predicate in delegates:
                if self.flags.get(flag_name):
                    continue
                if predicate(class_name):
                    self.mark_flag(flag_name)
                    if self.remaining == 0:
                        return True
            return self.remaining == 0

        def to_process_hints(
            self,
            owner: "_PresentationWindowMixin",
            classes: Tuple[str, ...],
        ) -> "_PresentationWindowMixin._WPSProcessHints":
            return owner._WPSProcessHints(
                classes=classes,
                has_slideshow=self.flags.get("has_slideshow", False),
                has_wps_presentation_signature=self.flags.get(
                    "has_wps_presentation_signature", False
                ),
                has_ms_presentation_signature=self.flags.get(
                    "has_ms_presentation_signature", False
                ),
            )

    @dataclass(slots=True)
    class _WPSPredicateManager:
        """Helper that prepares and evaluates cached WPS hint predicates."""

        owner: "_PresentationWindowMixin"
        debug: Optional[Callable[..., None]]
        specs: Tuple["_PresentationWindowMixin._PredicateSpec", ...]
        _delegates: Optional[Tuple[Tuple[str, Callable[[str], bool]], ...]] = None

        def _resolve_delegates(
            self,
        ) -> Tuple[Tuple[str, Callable[[str], bool]], ...]:
            if not self.specs:
                return tuple()

            cache = self.owner._resolve_wps_predicate_cache()
            cache_key = self.owner._build_wps_delegate_cache_key(self.debug, self.specs)
            cached = cache.get(cache_key)
            if cached is not None:
                return cached

            seen_flags: Set[str] = set()
            duplicate_flags: Set[str] = set()
            missing_flags = 0
            delegates: List[Tuple[str, Callable[[str], bool]]] = []

            for spec in self.specs:
                flag_name = spec.flag_name
                if not flag_name:
                    missing_flags += 1
                    continue
                if flag_name in seen_flags:
                    duplicate_flags.add(flag_name)
                    continue

                seen_flags.add(flag_name)
                delegates.append(
                    (
                        flag_name,
                        self.owner._memoize_wps_spec(spec, self.debug),
                    )
                )

            if duplicate_flags:
                self.owner._emit_wps_debug(
                    self.debug,
                    "Duplicate WPS predicate flags ignored: %s",  # pragma: no cover - debug logging
                    ", ".join(sorted(duplicate_flags)),
                )
            if missing_flags:
                self.owner._emit_wps_debug(
                    self.debug,
                    "WPS predicate specs missing flag names: %d",  # pragma: no cover - debug logging
                    missing_flags,
                )

            prepared = tuple(delegates)
            cache[cache_key] = prepared
            return prepared

        def delegates(self) -> Tuple[Tuple[str, Callable[[str], bool]], ...]:
            if self._delegates is None:
                self._delegates = self._resolve_delegates()
            return self._delegates

        def summarize(
            self, classes: Tuple[str, ...]
        ) -> "_PresentationWindowMixin._WPSProcessHints":
            if not classes:
                return self.owner._empty_wps_process_hints(classes)

            delegates = self.delegates()
            if not delegates:
                return self.owner._empty_wps_process_hints(classes)

            state = self.owner._WPSHintFlagState.from_delegates(delegates)
            if not state.flags:
                return self.owner._empty_wps_process_hints(classes)

            unique_classes = tuple(dict.fromkeys(classes))
            for class_name in unique_classes:
                if state.evaluate_class(class_name, delegates):
                    break

            return state.to_process_hints(self.owner, classes)

    class _PrefixKeywordClassifier:
        __slots__ = ("prefixes", "keywords", "excludes", "canonical")

        def __init__(
            self,
            *,
            prefixes: Iterable[str],
            keywords: Iterable[str],
            excludes: Iterable[str] = (),
            canonical: Iterable[str] = (),
        ) -> None:
            normalizer = self._normalize

            def _normalize_sequence(values: Iterable[str]) -> Tuple[str, ...]:
                seen: Set[str] = set()
                normalized: List[str] = []
                for value in values:
                    candidate = normalizer(value)
                    if not candidate or candidate in seen:
                        continue
                    seen.add(candidate)
                    normalized.append(candidate)
                return tuple(normalized)

            self.prefixes = _normalize_sequence(prefixes)
            self.keywords = _normalize_sequence(keywords)
            self.excludes = _normalize_sequence(excludes)
            self.canonical = frozenset(_normalize_sequence(canonical))

        @staticmethod
        def _normalize(value: Any) -> str:
            return _normalize_class_token(value)

        def _matches(self, normalized: str) -> bool:
            if not normalized:
                return False
            if normalized in self.canonical:
                return True
            if not self.prefixes:
                return False
            has_prefix = any(normalized.startswith(prefix) for prefix in self.prefixes)
            if not has_prefix:
                return False
            if self.excludes and any(token in normalized for token in self.excludes):
                return False
            if self.keywords:
                return any(keyword in normalized for keyword in self.keywords)
            return False

        def has_signature(self, class_name: Any) -> bool:
            normalized = self._normalize(class_name)
            return self._matches(normalized)

        def has_normalized_signature(self, class_name: Any) -> bool:
            if not isinstance(class_name, str):
                normalized = self._normalize(class_name)
            else:
                normalized = class_name.strip().casefold()
            return self._matches(normalized)

    class _ClassTokens:
        __slots__ = ()

        @staticmethod
        def freeze(*groups: Any) -> FrozenSet[str]:
            tokens: Set[str] = set()
            normalizer = _normalize_class_token
            for group in groups:
                if isinstance(group, (str, bytes)) or not hasattr(group, "__iter__"):
                    normalized = normalizer(group)
                    if normalized:
                        tokens.add(normalized)
                    continue
                for value in group:
                    normalized = normalizer(value)
                    if normalized:
                        tokens.add(normalized)
            return frozenset(tokens)

    @staticmethod
    def _unwrap_predicate_callable(value: Callable[..., Any]) -> Callable[..., Any]:
        """Return the underlying function for bound methods without unwrapping decorators."""

        func: Any = getattr(value, "__func__", value)
        return cast(Callable[..., Any], func)

    @dataclass(frozen=True, slots=True)
    class _PredicateSpec:
        flag_name: str
        predicate: Callable[[Any], bool]
        normalized_predicate: Optional[Callable[[str], bool]]
        base_impl: Callable[[Any], bool]

        def normalized_delegate(self) -> Optional[Callable[[str], bool]]:
            """Return the normalized predicate when the base implementation is used."""

            if self.normalized_predicate is None:
                return None

            predicate_impl = _PresentationWindowMixin._unwrap_predicate_callable(
                self.predicate
            )
            base_impl = _PresentationWindowMixin._unwrap_predicate_callable(
                self.base_impl
            )
            if predicate_impl is base_impl:
                return self.normalized_predicate
            return None

    _WPS_FRAME_CLASSES: FrozenSet[str] = _ClassTokens.freeze(
        "kwpsframeclass", "kwpsmainframe", "wpsframeclass", "wpsmainframe"
    )
    _KNOWN_PRESENTATION_CLASSES: FrozenSet[str] = _ClassTokens.freeze(
        (
            "screenclass",
            "pptframeclass",
            "pptviewwndclass",
            "powerpntframeclass",
            "powerpointframeclass",
            "acrobatsdiwindow",
            "kwppframeclass",
            "kwppmainframe",
            "nuidocumentwindow",
            "netuihwnd",
            "mdiclient",
            "documentwindow",
        ),
    )
    _KNOWN_PRESENTATION_PREFIXES: Tuple[str, ...] = ("kwpp", "wpsframe", "wpsmain")
    _SLIDESHOW_PRIORITY_CLASSES: FrozenSet[str] = _ClassTokens.freeze("screenclass")
    _SLIDESHOW_SECONDARY_CLASSES: FrozenSet[str] = _ClassTokens.freeze(
        "pptviewwndclass",
        "kwppshowframeclass",
        "kwppshowframe",
        "kwppshowwndclass",
        "kwpsshowframe",
        "kwpsshowframeclass",
        "wpsshowwndclass",
        "wpsshowframeclass",
    )
    _WPS_SLIDESHOW_CLASSES: FrozenSet[str] = _ClassTokens.freeze(
        "kwppshowframeclass",
        "kwppshowframe",
        "kwppshowwndclass",
        "kwpsshowframe",
        "kwpsshowframeclass",
        "wpsshowframe",
        "wpsshowframeclass",
        "wpsshowwndclass",
    )
    _PRESENTATION_EDITOR_CLASSES: FrozenSet[str] = _ClassTokens.freeze(
        (
            "pptframeclass",
            "powerpntframeclass",
            "powerpointframeclass",
            "kwppframeclass",
            "kwppmainframe",
        ),
        _WPS_FRAME_CLASSES,
    )

    @classmethod
    def _normalize_class_hint(cls, value: Any) -> str:
        return cls._PrefixKeywordClassifier._normalize(value)

    @staticmethod
    def _resolve_debug_logger() -> Optional[Callable[..., None]]:
        """Return a callable debug logger suitable for predicate diagnostics."""

        logger_ref = globals().get("logger")
        debug = getattr(logger_ref, "debug", None)
        if callable(debug):
            return debug

        logging_module = globals().get("logging")
        if logging_module is None:  # pragma: no cover - helper extraction fallback
            import logging as logging_module  # type: ignore[import-not-found]

        fallback_logger = logging_module.getLogger(__name__)
        debug = getattr(fallback_logger, "debug", None)
        if callable(debug):
            return debug
        return None

    def _wps_hint_predicate_specs(self) -> Tuple["_PredicateSpec", ...]:
        cls = _PresentationWindowMixin
        return (
            self._PredicateSpec(
                "has_slideshow",
                self._is_wps_slideshow_class,
                self._normalized_is_wps_slideshow_class,
                cls._is_wps_slideshow_class,
            ),
            self._PredicateSpec(
                "has_wps_presentation_signature",
                self._class_has_wps_presentation_signature,
                self._normalized_has_wps_presentation_signature,
                cls._class_has_wps_presentation_signature,
            ),
            self._PredicateSpec(
                "has_ms_presentation_signature",
                self._class_has_ms_presentation_signature,
                self._normalized_has_ms_presentation_signature,
                cls._class_has_ms_presentation_signature,
            ),
        )

    def _normalized_is_wps_slideshow_class(self, normalized: str) -> bool:
        if not normalized:
            return False
        if normalized in self._WPS_SLIDESHOW_CLASSES:
            return True
        return normalized.startswith("kwppshow")

    def _evaluate_normalized_class(
        self,
        class_name: Any,
        normalized_predicate: Callable[[str], bool],
    ) -> bool:
        normalized = self._normalize_class_hint(class_name)
        if not normalized:
            return False
        return bool(normalized_predicate(normalized))

    def _is_wps_slideshow_class(self, class_name: str) -> bool:
        return self._evaluate_normalized_class(
            class_name, self._normalized_is_wps_slideshow_class
        )

    def _is_slideshow_class(self, class_name: str) -> bool:
        if not class_name:
            return False
        if class_name in self._SLIDESHOW_PRIORITY_CLASSES:
            return True
        if class_name in self._SLIDESHOW_SECONDARY_CLASSES:
            return True
        return False

    def _is_preferred_presentation_class(self, class_name: str) -> bool:
        return self._is_slideshow_class(class_name)

    def _normalized_has_wps_presentation_signature(self, normalized: str) -> bool:
        if not normalized:
            return False
        if self._normalized_is_wps_slideshow_class(normalized):
            return True
        if normalized.startswith("kwpp") or "kwpp" in normalized:
            return True
        if normalized.startswith("wpp") and "wps" not in normalized:
            return True
        if normalized.startswith("wpsshow") or "wpsshow" in normalized:
            return True
        return False

    def _class_has_wps_presentation_signature(self, class_name: str) -> bool:
        return self._evaluate_normalized_class(
            class_name, self._normalized_has_wps_presentation_signature
        )

    def _normalized_has_ms_presentation_signature(self, normalized: str) -> bool:
        if not normalized:
            return False
        if self._normalized_has_wps_presentation_signature(normalized):
            return False
        if normalized in self._SLIDESHOW_PRIORITY_CLASSES:
            return True
        if normalized in self._SLIDESHOW_SECONDARY_CLASSES:
            return True
        if normalized in self._PRESENTATION_EDITOR_CLASSES:
            if normalized.startswith("kwpp") or normalized.startswith("kwps"):
                return False
            if normalized.startswith("wps"):
                return False
            return True
        keywords = ("ppt", "powerpnt", "powerpoint", "screenclass")
        return any(keyword in normalized for keyword in keywords)

    def _class_has_ms_presentation_signature(self, class_name: str) -> bool:
        return self._evaluate_normalized_class(
            class_name, self._normalized_has_ms_presentation_signature
        )

    def _is_ambiguous_screenclass(self, class_name: str, process_name: str) -> bool:
        if class_name != "screenclass":
            return False
        if not process_name:
            return True
        if self._is_ms_presentation_process_name(process_name):
            return False
        return True

    def _should_treat_wps_slideshow(self, class_name: str, process_name: str) -> bool:
        if not class_name:
            return False
        if class_name in self._SLIDESHOW_PRIORITY_CLASSES or class_name in self._SLIDESHOW_SECONDARY_CLASSES:
            return not self._is_ms_presentation_process_name(process_name)
        return False

    def _is_probable_wps_slideshow_window(self, hwnd: int, class_name: str, process_name: str) -> bool:
        if not hwnd:
            return False
        normalized_class = self._normalize_class_hint(class_name)
        if normalized_class and normalized_class in self._PRESENTATION_EDITOR_CLASSES:
            return False
        if self._is_ms_presentation_process_name(process_name):
            return False
        rect = self._get_window_rect_generic(hwnd)
        if rect is None or not self._matches_any_screen_geometry(rect):
            return False
        if process_name:
            normalized_proc = self._normalize_process_name(process_name)
            if normalized_proc.startswith(("wpp", "wppt", "wpspresentation", "wps")):
                return True
        if normalized_class:
            if "wps" in normalized_class or "kwpp" in normalized_class or "wpp" in normalized_class:
                return True
        return False

    def _normalized_class_hints(self, *classes: Any) -> Tuple[str, ...]:
        normalized: List[str] = []
        for value in classes:
            hint = self._normalize_class_hint(value)
            if hint:
                normalized.append(hint)
        return tuple(normalized)

    def _normalize_process_name(self, value: Any) -> str:
        if value is None:
            return ""
        try:
            if isinstance(value, (bytes, bytearray, memoryview)):
                text = bytes(value).decode("utf-8", "ignore")
            else:
                text = str(value)
        except Exception:
            return ""
        if not text:
            return ""
        return _casefold_cached(text)

    def _normalized_process_context(
        self, process_name: Any, classes: Iterable[Any]
    ) -> Tuple[str, Tuple[str, ...]]:
        normalized_name = self._normalize_process_name(process_name)
        normalized_classes = self._normalized_class_hints(*classes)
        return normalized_name, normalized_classes

    def _is_wps_presentation_process_name(self, process_name: Any) -> bool:
        """Return True if the process name looks like WPS 演示进程（含新版 wppt/wpspresentation）。"""

        name = self._normalize_process_name(process_name)
        if not name:
            return False
        if name.startswith(("wpp", "wppt")):
            return True
        # 仅在明确包含 presentation 关键词时判定为演示，避免将 wps.exe（文字/表格等）误判为放映。
        return "wpspresentation" in name or name.startswith("wpspresentation")

    def _is_ms_presentation_process_name(self, process_name: Any) -> bool:
        name = self._normalize_process_name(process_name)
        if not name:
            return False
        return "powerpnt" in name or name.startswith("pptview")

    def _empty_wps_process_hints(
        self, classes: Tuple[str, ...]
    ) -> "_PresentationWindowMixin._WPSProcessHints":
        return self._WPSProcessHints(
            classes=classes,
            has_slideshow=False,
            has_wps_presentation_signature=False,
            has_ms_presentation_signature=False,
        )

    @staticmethod
    def _emit_wps_debug(
        debug: Optional[Callable[..., None]],
        message: str,
        *args: Any,
        **kwargs: Any,
    ) -> None:
        if callable(debug):
            debug(message, *args, **kwargs)

    def _log_wps_predicate_failure(
        self, debug: Optional[Callable[..., None]]
    ) -> None:
        self._emit_wps_debug(
            debug,
            "WPS process hint predicate failed",  # pragma: no cover - debug logging
            exc_info=True,
        )

    def _memoize_wps_spec(
        self,
        spec: "_PresentationWindowMixin._PredicateSpec",
        debug: Optional[Callable[..., None]],
    ) -> Callable[[str], bool]:
        normalized_impl = spec.normalized_delegate()
        delegate: Callable[[Any], bool]
        if normalized_impl is None:
            delegate = spec.predicate
        else:
            delegate = normalized_impl

        @functools.lru_cache(maxsize=None)
        def _call(class_name: str) -> bool:
            try:
                return bool(delegate(class_name))
            except Exception:
                self._log_wps_predicate_failure(debug)
                return False

        return _call

    def _resolve_wps_predicate_cache(self) -> Dict[
        Tuple[Any, ...], Tuple[Tuple[str, Callable[[str], bool]], ...]
    ]:
        cache = getattr(self, "_cached_wps_predicate_delegates", None)
        if cache is None:
            cache = {}
            self._cached_wps_predicate_delegates = cache
        return cast(
            Dict[Tuple[Any, ...], Tuple[Tuple[str, Callable[[str], bool]], ...]],
            cache,
        )

    def _debug_delegate_cache_key(
        self, debug: Optional[Callable[..., None]]
    ) -> Tuple[Any, Any]:
        if not callable(debug):
            return (None, None)
        owner = getattr(debug, "__self__", None)
        return (
            owner,
            self._unwrap_predicate_callable(debug),
        )

    def _build_wps_delegate_cache_key(
        self,
        debug: Optional[Callable[..., None]],
        predicate_specs: Tuple["_PresentationWindowMixin._PredicateSpec", ...],
    ) -> Tuple[Any, ...]:
        debug_key = self._debug_delegate_cache_key(debug)
        spec_keys: List[Tuple[Any, Any, Any, Any]] = []
        for spec in predicate_specs:
            predicate_impl = self._unwrap_predicate_callable(spec.predicate)
            if spec.normalized_predicate is None:
                normalized_impl: Optional[Callable[..., Any]] = None
            else:
                normalized_impl = self._unwrap_predicate_callable(
                    spec.normalized_predicate
                )
            base_impl = self._unwrap_predicate_callable(spec.base_impl)
            spec_keys.append(
                (
                    spec.flag_name,
                    predicate_impl,
                    normalized_impl,
                    base_impl,
                )
            )
        return (debug_key, tuple(spec_keys))

    def _summarize_wps_process_hints(
        self, normalized_classes: Iterable[str]
    ) -> "_PresentationWindowMixin._WPSProcessHints":
        classes = tuple(normalized_classes)
        empty_hints = self._empty_wps_process_hints(classes)
        if not classes:
            return empty_hints

        manager = self._WPSPredicateManager(
            owner=self,
            debug=self._resolve_debug_logger(),
            specs=self._wps_hint_predicate_specs(),
        )
        return manager.summarize(classes)

    def _classify_wps_process(
        self, process_name: Any, *classes: Any
    ) -> Literal["presentation", "other"]:
        name, normalized_classes = self._normalized_process_context(process_name, classes)
        if not name:
            return "other"
        if name.startswith(("wpp", "wppt")) or "wpspresentation" in name:
            return "presentation"
        if not name.startswith("wps"):
            return "other"

        hints = self._summarize_wps_process_hints(normalized_classes)

        presentation_detected = any(
            (
                hints.has_slideshow,
                hints.has_wps_presentation_signature,
                hints.has_ms_presentation_signature,
            )
        )
        # 兼容：WPS 文字与 WPS 演示可能复用 wps.exe 进程名，不能默认当作演示。
        # 规则：只有明确检测到放映/演示特征时才视为 presentation，否则视为 other。
        if "wpswriter" in name:
            return "other"
        return "presentation" if presentation_detected else "other"

    def _is_wps_presentation_process(self, process_name: str, *classes: str) -> bool:
        return self._classify_wps_process(process_name, *classes) == "presentation"

    def _window_thread_id(self, hwnd: int) -> int:
        if _USER32 is None or hwnd == 0:
            return 0
        pid = wintypes.DWORD()
        try:
            thread_id = int(_USER32.GetWindowThreadProcessId(wintypes.HWND(hwnd), ctypes.byref(pid)))
        except Exception:
            thread_id = 0
        return thread_id

    def _attach_to_target_thread(self, hwnd: int) -> Optional[Tuple[int, int]]:
        if _USER32 is None or hwnd == 0:
            return None
        target_thread = self._window_thread_id(hwnd)
        if not target_thread:
            return None
        try:
            current_thread = int(_USER32.GetCurrentThreadId())
        except Exception:
            current_thread = 0
        if not current_thread or current_thread == target_thread:
            return None
        try:
            attached = bool(_USER32.AttachThreadInput(current_thread, target_thread, True))
        except Exception:
            attached = False
        return (current_thread, target_thread) if attached else None

    def _detach_from_target_thread(self, pair: Optional[Tuple[int, int]]) -> None:
        if _USER32 is None or not pair:
            return
        src, dst = pair
        if not src or not dst or src == dst:
            return
        try:
            _USER32.AttachThreadInput(src, dst, False)
        except Exception:
            pass

    def _enumerate_overlay_candidate_windows(self, overlay_hwnd: int) -> Optional[List[int]]:
        if _USER32 is None or _WNDENUMPROC is None:
            return None
        candidates: List[int] = []

        def _enum_callback(hwnd: int, _l_param: int) -> int:
            if hwnd == overlay_hwnd:
                return True
            if self._should_ignore_window(hwnd):
                return True
            if not _user32_is_window_visible(hwnd) or _user32_is_window_iconic(hwnd):
                return True
            rect = _user32_window_rect(hwnd)
            if not rect or not self._rect_intersects_overlay(rect):
                return True
            candidates.append(int(hwnd))
            return True

        enum_proc = _WNDENUMPROC(_enum_callback)
        try:
            _USER32.EnumWindows(enum_proc, 0)
        except Exception:
            return None
        return candidates

    def _enumerate_overlay_candidate_windows_win32(self, overlay_hwnd: int) -> Optional[List[int]]:
        """Collect visible, intersecting windows via win32gui."""

        if win32gui is None:
            return None

        candidates: List[int] = []

        def _enum_callback(hwnd: int, acc: List[int]) -> bool:
            if hwnd == overlay_hwnd:
                return True
            if self._should_ignore_window(hwnd):
                return True
            try:
                if not win32gui.IsWindowVisible(hwnd) or win32gui.IsIconic(hwnd):
                    return True
                rect = win32gui.GetWindowRect(hwnd)
            except Exception:
                return True
            if not rect or not self._rect_intersects_overlay(rect):
                return True
            acc.append(hwnd)
            return True

        try:
            win32gui.EnumWindows(_enum_callback, candidates)
        except Exception:
            return None
        return candidates

    def _overlay_child_widget(self, attribute: str) -> Optional[QWidget]:
        overlay = self._overlay_widget()
        return getattr(overlay, attribute, None) if overlay is not None else None

    def _widget_hwnd(self, widget: Optional[QWidget]) -> int:
        if widget is None:
            return 0
        try:
            wid = widget.winId()
        except Exception:
            return 0
        return int(wid) if wid else 0

    def _toolbar_widget(self) -> Optional[QWidget]:
        return self._overlay_child_widget("toolbar")

    def _photo_overlay_widget(self) -> Optional[QWidget]:
        return self._overlay_child_widget("_photo_overlay")

    def _overlay_hwnd(self) -> int:
        return self._widget_hwnd(self._overlay_widget())

    def _toolbar_hwnd(self) -> int:
        return self._widget_hwnd(self._toolbar_widget())

    def _photo_overlay_hwnd(self) -> int:
        return self._widget_hwnd(self._photo_overlay_widget())

    def _overlay_rect_tuple(self) -> Optional[RectTuple]:
        widget = self._overlay_widget()
        if widget is None:
            return None
        rect = widget.geometry()
        if rect.isNull():
            return None
        left = rect.left()
        top = rect.top()
        right = left + rect.width()
        bottom = top + rect.height()
        return left, top, right, bottom

    def _rect_intersects_overlay(self, rect: RectTuple) -> bool:
        overlay_rect = self._overlay_rect_tuple()
        if overlay_rect is None:
            return False
        left, top, right, bottom = rect
        o_left, o_top, o_right, o_bottom = overlay_rect
        return not (right <= o_left or left >= o_right or bottom <= o_top or top >= o_bottom)

    def _window_process_id(self, hwnd: int) -> Optional[int]:
        if _USER32 is None or hwnd == 0:
            return None
        pid = wintypes.DWORD()
        try:
            _USER32.GetWindowThreadProcessId(wintypes.HWND(hwnd), ctypes.byref(pid))
        except Exception:
            return None
        value = int(pid.value)
        return value or None

    def _window_process_name(self, hwnd: int) -> str:
        pid = self._window_process_id(hwnd)
        if not pid:
            return ""
        path = _process_image_path(int(pid))
        if not path:
            return ""
        return os.path.basename(path).strip().lower()

    def _window_class_name(self, hwnd: int) -> str:
        if hwnd == 0:
            return ""
        if win32gui is not None:
            try:
                return win32gui.GetClassName(hwnd).strip().lower()
            except Exception:
                return ""
        return _user32_window_class_name(hwnd)

    def _presentation_window_class(self, hwnd: int) -> str:
        return self._window_class_name(hwnd)

    def _is_own_process_window(self, hwnd: int) -> bool:
        try:
            pid = self._window_process_id(hwnd)
            return pid == os.getpid() if pid is not None else False
        except Exception:
            return False

    def _should_ignore_window(self, hwnd: int) -> bool:
        if hwnd == 0:
            return True
        overlay_hwnd = self._overlay_hwnd()
        if hwnd == overlay_hwnd:
            return True
        toolbar_hwnd = self._toolbar_hwnd()
        if toolbar_hwnd and hwnd == toolbar_hwnd:
            return True
        photo_hwnd = self._photo_overlay_hwnd()
        if photo_hwnd and hwnd == photo_hwnd:
            return True
        return self._is_own_process_window(hwnd)

    def _matches_overlay_geometry(self, rect: RectTuple) -> bool:
        overlay_rect = self._overlay_rect_tuple()
        if overlay_rect is None:
            return False
        left, top, right, bottom = rect
        width = max(0, right - left)
        height = max(0, bottom - top)
        if width < 1 or height < 1:
            return False
        o_left, o_top, o_right, o_bottom = overlay_rect
        o_width = o_right - o_left
        o_height = o_bottom - o_top
        if o_width <= 0 or o_height <= 0:
            return False
        width_diff = abs(width - o_width)
        height_diff = abs(height - o_height)
        cx = (o_left + o_right) // 2
        cy = (o_top + o_bottom) // 2
        contains_center = left <= cx <= right and top <= cy <= bottom
        size_match = width >= 400 and height >= 300 and width_diff <= 64 and height_diff <= 64
        if contains_center and width >= 400 and height >= 300:
            return True
        return size_match

    def _matches_any_screen_geometry(self, rect: RectTuple) -> bool:
        left, top, right, bottom = rect
        width = max(0, right - left)
        height = max(0, bottom - top)
        if width < 1 or height < 1:
            return False
        try:
            screens = QGuiApplication.screens()
        except Exception:
            screens = []
        if not screens:
            return False
        cx = (left + right) // 2
        cy = (top + bottom) // 2
        for screen in screens:
            try:
                geom = screen.geometry()
            except Exception:
                continue
            if not geom.isValid():
                continue
            if geom.contains(QPoint(cx, cy)):
                if abs(width - geom.width()) <= 120 and abs(height - geom.height()) <= 120:
                    return True
                if abs(width - geom.width()) <= 80 and abs(height - geom.height()) <= 80:
                    return True
        return False

    def _get_window_rect_generic(self, hwnd: int) -> Optional[Tuple[int, int, int, int]]:
        if win32gui is not None:
            try:
                rect = win32gui.GetWindowRect(hwnd)
                if rect:
                    return int(rect[0]), int(rect[1]), int(rect[2]), int(rect[3])
            except Exception:
                pass
        return _user32_window_rect(hwnd)

    def _fallback_is_candidate_window(self, hwnd: int) -> bool:
        if _USER32 is None or hwnd == 0:
            return False
        if self._should_ignore_window(hwnd):
            return False
        class_name = _user32_window_class_name(hwnd)
        if not class_name:
            return False
        if class_name in self._KNOWN_PRESENTATION_CLASSES:
            return True
        if any(class_name.startswith(prefix) for prefix in self._KNOWN_PRESENTATION_PREFIXES):
            return True
        process_name = self._window_process_name(hwnd)
        if self._is_wps_presentation_process(process_name, class_name, ""):
            return True
        rect = _user32_window_rect(hwnd)
        if not rect:
            return False
        return self._matches_overlay_geometry(rect)

    def _fallback_is_target_window_valid(self, hwnd: int) -> bool:
        if _USER32 is None or hwnd == 0:
            return False
        overlay_hwnd = self._overlay_hwnd()
        if hwnd == overlay_hwnd:
            return False
        if not _user32_is_window(hwnd):
            return False
        if not _user32_is_window_visible(hwnd) or _user32_is_window_iconic(hwnd):
            return False
        rect = _user32_window_rect(hwnd)
        if not rect:
            return False
        if self._rect_intersects_overlay(rect):
            return True
        if self._fallback_is_candidate_window(hwnd) and self._matches_any_screen_geometry(rect):
            return True
        return False

    def _is_target_window_valid(self, hwnd: int) -> bool:
        """Validate a target window using win32gui, falling back to ctypes when needed."""

        if win32gui is None:
            return self._fallback_is_target_window_valid(hwnd)
        overlay_hwnd = self._overlay_hwnd()
        try:
            if hwnd == 0 or hwnd == overlay_hwnd:
                return False
            if not win32gui.IsWindow(hwnd) or not win32gui.IsWindowVisible(hwnd):
                return False
            if win32gui.IsIconic(hwnd):
                return False
            rect = win32gui.GetWindowRect(hwnd)
        except Exception:
            return False
        if not rect:
            return False
        if self._rect_intersects_overlay(rect):
            return True
        if self._is_candidate_presentation_window(hwnd) and self._matches_any_screen_geometry(rect):
            return True
        return False

    def _presentation_target_category(self, hwnd: Optional[int]) -> str:
        if not hwnd:
            return "other"
        class_name = self._presentation_window_class(hwnd)
        top_hwnd = _user32_top_level_hwnd(hwnd)
        top_class = self._presentation_window_class(top_hwnd) if top_hwnd else ""
        process_name = self._window_process_name(top_hwnd or hwnd)
        return _compute_presentation_category(
            class_name,
            top_class,
            process_name,
            has_wps_presentation_signature=self._class_has_wps_presentation_signature,
            is_wps_slideshow_class=self._is_wps_slideshow_class,
            has_ms_presentation_signature=self._class_has_ms_presentation_signature,
            is_wps_presentation_process=self._is_wps_presentation_process,
        )


class _PresentationForwarder(_PresentationWindowMixin):
    """在绘图模式下将特定输入事件转发给下层演示窗口。"""

    __slots__ = (
        "overlay",
        "_last_target_hwnd",
        "_child_buffer",
        "_probe_failure_count",
        "_probe_cooldown_until",
    )

    def _overlay_widget(self) -> Optional[QWidget]:
        return self.overlay

    def _is_hwnd_valid(self, hwnd: int) -> bool:
        """Return True if *hwnd* looks like a usable window handle."""

        if hwnd == 0 or _USER32 is None:
            return False
        try:
            return bool(_USER32.IsWindow(wintypes.HWND(hwnd)))
        except Exception:
            return False

    _SMTO_ABORTIFHUNG = 0x0002
    _MAX_CHILD_FORWARDS = 32
    _INPUT_KEYBOARD = 1
    _KEYEVENTF_EXTENDEDKEY = 0x0001
    _KEYEVENTF_KEYUP = 0x0002

    if _USER32 is not None:

        class _GuiThreadInfo(ctypes.Structure):
            _fields_ = [
                ("cbSize", wintypes.DWORD),
                ("flags", wintypes.DWORD),
                ("hwndActive", wintypes.HWND),
                ("hwndFocus", wintypes.HWND),
                ("hwndCapture", wintypes.HWND),
                ("hwndMenuOwner", wintypes.HWND),
                ("hwndMoveSize", wintypes.HWND),
                ("hwndCaret", wintypes.HWND),
                ("rcCaret", wintypes.RECT),
            ]

        _KeyboardInput = None  # type: ignore[assignment]
        _InputUnion = None  # type: ignore[assignment]
        _Input = None  # type: ignore[assignment]
        try:

            _KeyboardInput = type(
                "_KeyboardInput",
                (ctypes.Structure,),
                {
                    "_fields_": [
                        ("wVk", wintypes.WORD),
                        ("wScan", wintypes.WORD),
                        ("dwFlags", wintypes.DWORD),
                        ("time", wintypes.DWORD),
                        ("dwExtraInfo", wintypes.ULONG_PTR),
                    ]
                },
            )
            _InputUnion = type(
                "_InputUnion",
                (ctypes.Union,),
                {"_fields_": [("ki", _KeyboardInput)]},
            )
            _Input = type(
                "_Input",
                (ctypes.Structure,),
                {
                    "_fields_": [
                        ("type", wintypes.DWORD),
                        ("data", _InputUnion),
                    ]
                },
            )
            try:
                _USER32.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(_Input), ctypes.c_int]
                _USER32.SendInput.restype = wintypes.UINT
            except Exception:
                pass
        except Exception:
            _KeyboardInput = None  # type: ignore[assignment]
            _InputUnion = None  # type: ignore[assignment]
            _Input = None  # type: ignore[assignment]

        if _USER32 is not None and _Input is not None:
            try:
                _USER32.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(_Input), ctypes.c_int]
                _USER32.SendInput.restype = wintypes.UINT
            except Exception:
                pass

    else:

        class _GuiThreadInfo(ctypes.Structure):  # type: ignore[misc,override]
            _fields_: List[Tuple[str, Any]] = []

        _KeyboardInput = None  # type: ignore[assignment]
        _InputUnion = None  # type: ignore[assignment]
        _Input = None  # type: ignore[assignment]

    _KEY_FORWARD_MAP: Dict[int, int] = {
        int(Qt.Key.Key_PageUp): VK_PRIOR,
        int(Qt.Key.Key_PageDown): VK_NEXT,
        int(Qt.Key.Key_Up): VK_UP,
        int(Qt.Key.Key_Down): VK_DOWN,
        int(Qt.Key.Key_Left): VK_LEFT,
        int(Qt.Key.Key_Right): VK_RIGHT,
        int(Qt.Key.Key_Space): getattr(win32con, "VK_SPACE", 0x20),
        int(Qt.Key.Key_Return): getattr(win32con, "VK_RETURN", 0x0D),
        int(Qt.Key.Key_Enter): getattr(win32con, "VK_RETURN", 0x0D),
    }
    _EXTENDED_KEY_CODES: Set[int] = (
        {
            win32con.VK_UP,
            win32con.VK_DOWN,
            win32con.VK_LEFT,
            win32con.VK_RIGHT,
        }
        if win32con is not None
        else set()
    )

    @staticmethod
    def is_supported() -> bool:
        return bool(win32api and win32con and win32gui)

    def __init__(self, overlay: "OverlayWindow") -> None:
        self.overlay = overlay
        self._last_target_hwnd: Optional[int] = None
        self._child_buffer: List[int] = []
        self._probe_failure_count = 0
        self._probe_cooldown_until = 0.0

    def _log_debug(self, message: str, *args: Any) -> None:
        if logger.isEnabledFor(logging.DEBUG):
            logger.debug(message, *args)

    def clear_cached_target(self) -> None:
        self._last_target_hwnd = None
        self._probe_failure_count = 0
        self._probe_cooldown_until = 0.0

    def _register_input_activity(self) -> None:
        self._probe_failure_count = 0
        self._probe_cooldown_until = 0.0

    def _update_probe_backoff(self, found: bool) -> None:
        now = time.monotonic()
        if found:
            self._probe_failure_count = 0
            self._probe_cooldown_until = 0.0
            return
        self._probe_failure_count = min(self._probe_failure_count + 1, 8)
        delay = min(1.0, 0.1 * (2 ** self._probe_failure_count))
        self._probe_cooldown_until = now + delay

    def _is_wps_slideshow_window(self, hwnd: int) -> bool:
        if not self._is_hwnd_valid(hwnd):
            return False
        class_name = self._window_class_name(hwnd)
        if self._is_wps_slideshow_class(class_name):
            return True
        if class_name in self._SLIDESHOW_PRIORITY_CLASSES or class_name in self._SLIDESHOW_SECONDARY_CLASSES:
            top_hwnd = self._top_level_hwnd(hwnd)
            process_name = self._window_process_name(top_hwnd)
            top_class = self._window_class_name(top_hwnd) if top_hwnd else ""
            if self._is_wps_presentation_process(process_name, class_name, top_class):
                return True
            if self._is_ambiguous_screenclass(class_name, process_name):
                return True
            if self._should_treat_wps_slideshow(class_name, process_name):
                return True
            if self._is_probable_wps_slideshow_window(hwnd, class_name, process_name):
                return True
        return False

    def _is_ms_slideshow_window(self, hwnd: int) -> bool:
        if not self._is_hwnd_valid(hwnd):
            return False
        if self._is_wps_slideshow_window(hwnd):
            return False
        class_name = self._window_class_name(hwnd)
        if class_name not in self._SLIDESHOW_PRIORITY_CLASSES and class_name not in self._SLIDESHOW_SECONDARY_CLASSES:
            return False
        process_name = self._window_process_name(self._top_level_hwnd(hwnd))
        if not process_name:
            if self._class_has_ms_presentation_signature(class_name):
                return True
            if not self._class_has_wps_presentation_signature(class_name):
                return True
            return False
        return "powerpnt" in process_name

    def _should_refresh_cached_target(self, hwnd: int) -> bool:
        class_name = self._window_class_name(hwnd)
        return not self._is_slideshow_class(class_name)

    def get_presentation_target(self) -> Optional[int]:
        hwnd = self._resolve_presentation_target()
        if hwnd:
            return hwnd
        return self._detect_presentation_window()

    def bring_target_to_foreground(self, hwnd: int) -> bool:
        if hwnd == 0:
            return False
        if not self._is_control_allowed(hwnd, log=False):
            self.clear_cached_target()
            return False
        if self._is_wps_slideshow_window(hwnd):
            self._last_target_hwnd = hwnd
            return True
        activated = False
        attach_pair = self._attach_to_target_thread(hwnd)
        try:
            activated = self._activate_window_for_input(hwnd)
            if win32gui is not None:
                try:
                    win32gui.SetForegroundWindow(hwnd)
                    activated = True
                except Exception:
                    pass
        finally:
            self._detach_from_target_thread(attach_pair)
        if activated:
            self._last_target_hwnd = hwnd
        return activated

    # ---- 公共接口 ----
    def focus_presentation_window(self) -> bool:
        if not self.is_supported():
            return False
        self._register_input_activity()
        target = self._resolve_presentation_target()
        if not target:
            target = self._detect_presentation_window()
        if not target:
            return False
        if not self.overlay._presentation_control_allowed(target):
            self._log_debug(
                "focus_presentation_window: control disabled target=%s",
                hex(target) if target else "0x0",
            )
            self.clear_cached_target()
            return False
        attach_pair = self._attach_to_target_thread(target)
        try:
            activated = self._activate_window_for_input(target)
        finally:
            self._detach_from_target_thread(attach_pair)
        if activated:
            self._last_target_hwnd = target
        return activated

    def forward_wheel(self, event: QWheelEvent, *, allow_cursor: bool = False) -> bool:
        if not self._can_forward(allow_cursor=allow_cursor):
            self.clear_cached_target()
            return False
        self._register_input_activity()
        delta_vec = event.angleDelta()
        delta = int(delta_vec.y() or delta_vec.x())
        if delta == 0:
            pixel_vec = event.pixelDelta()
            delta = int(pixel_vec.y() or pixel_vec.x())
        if delta == 0:
            return False
        target = self._resolve_presentation_target()
        if not target:
            target = self._detect_presentation_window()
        if not target:
            self.clear_cached_target()
            return False
        if not self.overlay._presentation_control_allowed(target):
            self._log_debug(
                "forward_wheel: control disabled target=%s",
                hex(target) if target else "0x0",
            )
            self.clear_cached_target()
            return False
        is_wps_target = self._is_wps_slideshow_window(target)
        keys = self._translate_mouse_modifiers(event)
        delta_word = ctypes.c_short(delta).value & 0xFFFF
        w_param = (ctypes.c_ushort(keys).value & 0xFFFF) | (delta_word << 16)
        global_pos = event.globalPosition().toPoint()
        x_word = ctypes.c_short(global_pos.x()).value & 0xFFFF
        y_word = ctypes.c_short(global_pos.y()).value & 0xFFFF
        l_param = x_word | (y_word << 16)
        delivered = False
        guard = (
            contextlib.nullcontext()
            if is_wps_target
            else self._keyboard_capture_guard()
        )
        with guard:
            if is_wps_target:
                focus_ok = True
            else:
                focus_ok = self.bring_target_to_foreground(target)
                if not focus_ok:
                    focus_ok = self._activate_window_for_input(target)
            for hwnd, update_cache in self._iter_wheel_targets(target):
                if self._deliver_mouse_wheel(hwnd, w_param, l_param):
                    delivered = True
                    if update_cache:
                        self._last_target_hwnd = target
                    if is_wps_target:
                        return True
                    break
            if not delivered and focus_ok:
                delivered = self._deliver_mouse_wheel(target, w_param, l_param)
                if delivered and is_wps_target:
                    return True
        if not delivered:
            self.clear_cached_target()
        if logger.isEnabledFor(logging.DEBUG):
            self._log_debug(
                "forward_wheel: target=%s class=%s delivered=%s",
                hex(target) if target else "0x0",
                self._window_class_name(target) if target else "",
                delivered,
            )
        return delivered

    def forward_key(
        self,
        event: QKeyEvent,
        *,
        is_press: bool,
        allow_cursor: bool = False,
    ) -> bool:
        if not self._can_forward(allow_cursor=allow_cursor):
            self.clear_cached_target()
            return False
        self._register_input_activity()
        vk_code = self._resolve_vk_code(event)
        if vk_code is None:
            return False
        target = self._resolve_presentation_target()
        if not target:
            target = self._detect_presentation_window()
        if not target:
            self._log_debug("forward_key: target window not found for key=%s", event.key())
            self.clear_cached_target()
            return False
        if not self.overlay._presentation_control_allowed(target):
            self._log_debug(
                "forward_key: control disabled target=%s key=%s",
                hex(target) if target else "0x0",
                event.key(),
            )
            self.clear_cached_target()
            return False
        for hwnd, update_cache in self._iter_key_targets(target):
            if self._send_key_to_window(
                hwnd, vk_code, event, is_press=is_press, update_cache=update_cache
            ):
                self._log_debug(
                    "forward_key: delivered to hwnd=%s key=%s is_press=%s",
                    hwnd,
                    vk_code,
                    is_press,
                )
                return True
        self._log_debug("forward_key: delivery failed for key=%s", vk_code)
        self.clear_cached_target()
        return False

    def send_virtual_key(self, vk_code: int) -> bool:
        if not self.is_supported() or vk_code == 0:
            return False
        target = self._resolve_presentation_target()
        if not target:
            target = self._detect_presentation_window()
        if not target:
            self._log_debug("send_virtual_key: target not found vk=%s", vk_code)
            return False
        if not self.overlay._presentation_control_allowed(target):
            self._log_debug(
                "send_virtual_key: control disabled target=%s vk=%s",
                hex(target) if target else "0x0",
                vk_code,
            )
            self.clear_cached_target()
            return False
        if self._is_wps_slideshow_window(target):
            down_param = self._build_basic_key_lparam(vk_code, is_press=True)
            success = self._deliver_key_message(target, win32con.WM_KEYDOWN, vk_code, down_param)
            if success:
                self._last_target_hwnd = target
            else:
                self._log_debug("send_virtual_key: wps slideshow delivery failed vk=%s", vk_code)
            return success
        if self._is_ms_slideshow_window(target):
            success = False
            for hwnd, update_cache in self._iter_key_targets(target):
                if self._send_key_message_sequence(hwnd, vk_code):
                    success = True
                    if update_cache:
                        self._last_target_hwnd = target
                    break
            if not success:
                success = self._send_key_message_sequence(target, vk_code)
                if success:
                    self._last_target_hwnd = target
            if not success:
                self._log_debug(
                    "send_virtual_key: message delivery failed vk=%s target=%s",
                    vk_code,
                    hex(target),
                )
                self.clear_cached_target()
            return success
        press = release = False
        with self._keyboard_capture_guard():
            attach_pair = self._attach_to_target_thread(target)
            try:
                if not self._activate_window_for_input(target):
                    self._log_debug("send_virtual_key: activate failed hwnd=%s", target)
                    return False
                press = self._send_input_event(vk_code, is_press=True)
                release = self._send_input_event(vk_code, is_press=False)
            finally:
                self._detach_from_target_thread(attach_pair)
        success = press and release
        if success:
            self._last_target_hwnd = target
        else:
            self._log_debug(
                "send_virtual_key: send input failed vk=%s press=%s release=%s",
                vk_code,
                press,
                release,
            )
        return success

    # ---- 内部工具方法 ----
    def _can_forward(self, *, allow_cursor: bool = False) -> bool:
        if not self.is_supported():
            return False
        if getattr(self.overlay, "whiteboard_active", False):
            return False
        mode = getattr(self.overlay, "mode", "cursor")
        if mode == "cursor" and not allow_cursor:
            return False
        return True

    def _is_control_allowed(self, hwnd: Optional[int], *, log: bool = False) -> bool:
        overlay = getattr(self, "overlay", None)
        if overlay is None:
            return True
        checker = getattr(overlay, "_presentation_control_allowed", None)
        if callable(checker):
            try:
                return checker(hwnd, log=log)
            except TypeError:
                return checker(hwnd)
        return True

    def _translate_mouse_modifiers(self, event: QWheelEvent) -> int:
        keys = 0
        modifiers = event.modifiers()
        if modifiers & Qt.KeyboardModifier.ShiftModifier:
            keys |= win32con.MK_SHIFT
        if modifiers & Qt.KeyboardModifier.ControlModifier:
            keys |= win32con.MK_CONTROL
        buttons = event.buttons()
        if buttons & Qt.MouseButton.LeftButton:
            keys |= win32con.MK_LBUTTON
        if buttons & Qt.MouseButton.RightButton:
            keys |= win32con.MK_RBUTTON
        if buttons & Qt.MouseButton.MiddleButton:
            keys |= win32con.MK_MBUTTON
        return keys

    def _resolve_vk_code(self, event: QKeyEvent) -> Optional[int]:
        native_getter = getattr(event, "nativeVirtualKey", None)
        vk_code = 0
        if callable(native_getter):
            try:
                vk_code = int(native_getter())
            except Exception:
                vk_code = 0
        if vk_code:
            return vk_code
        vk_code = self._KEY_FORWARD_MAP.get(event.key(), 0)
        return vk_code or None

    def _send_key_to_window(
        self,
        hwnd: int,
        vk_code: int,
        event: QKeyEvent,
        *,
        is_press: bool,
        update_cache: bool,
    ) -> bool:
        delivered = False
        if self._inject_key_event(hwnd, vk_code, event, is_press=is_press):
            delivered = True
        elif win32con is not None:
            message = win32con.WM_KEYDOWN if is_press else win32con.WM_KEYUP
            l_param = self._build_key_lparam(vk_code, event, is_press)
            delivered = self._deliver_key_message(hwnd, message, vk_code, l_param)
        if delivered and update_cache:
            self._last_target_hwnd = hwnd
        if not delivered:
            self._log_debug(
                "_send_key_to_window: failed hwnd=%s vk=%s is_press=%s",
                hwnd,
                vk_code,
                is_press,
            )
        return delivered

    def _inject_key_event(
        self,
        hwnd: int,
        vk_code: int,
        event: QKeyEvent,
        *,
        is_press: bool,
    ) -> bool:
        if (
            _USER32 is None
            or self._Input is None
            or self._KeyboardInput is None
            or hwnd == 0
            or vk_code == 0
        ):
            return False
        if self._is_wps_slideshow_window(hwnd):
            return False
        if self._is_ms_slideshow_window(hwnd):
            return False
        success = False
        with self._keyboard_capture_guard():
            attach_pair = self._attach_to_target_thread(hwnd)
            try:
                if not self._activate_window_for_input(hwnd):
                    self._log_debug("_inject_key_event: activate failed hwnd=%s", hwnd)
                    return False
                success = self._send_input_event(vk_code, is_press=is_press)
            finally:
                self._detach_from_target_thread(attach_pair)
        return success

    @contextlib.contextmanager
    def _keyboard_capture_guard(self) -> Iterable[None]:
        release = getattr(self.overlay, "_release_keyboard_capture", None)
        capture = getattr(self.overlay, "_ensure_keyboard_capture", None)
        try:
            if callable(release):
                release()
        except Exception:
            pass
        try:
            yield
        finally:
            if callable(capture):
                def _restore_focus() -> None:
                    try:
                        if getattr(self.overlay, "mode", "") != "cursor":
                            capture()
                        else:
                            if getattr(self.overlay, "_keyboard_grabbed", False):
                                self.overlay._release_keyboard_capture()
                    except Exception:
                        return
                    try:
                        self.overlay.raise_toolbar()
                    except Exception:
                        pass
                    try:
                        QApplication.processEvents()
                    except Exception:
                        pass

                try:
                    QTimer.singleShot(10, _restore_focus)
                except Exception:
                    try:
                        _restore_focus()
                    except Exception:
                        pass

    def _activate_window_for_input(self, hwnd: int, *, force: bool = False) -> bool:
        if not self._is_hwnd_valid(hwnd):
            return False
        if self._is_wps_slideshow_window(hwnd) and not force:
            return True
        root_hwnd = self._top_level_hwnd(hwnd)
        use_root = (
            root_hwnd
            and root_hwnd != hwnd
            and self._has_window_caption(root_hwnd) is not True
        )
        handles_for_activation: List[int] = [hwnd]
        if use_root and root_hwnd:
            handles_for_activation.append(root_hwnd)
        activated = False
        for handle in handles_for_activation:
            if handle == 0:
                continue
            try:
                if _USER32.SetActiveWindow(wintypes.HWND(handle)):
                    activated = True
            except Exception:
                pass
            if activated:
                break
            try:
                if _USER32.SetForegroundWindow(wintypes.HWND(handle)):
                    activated = True
            except Exception:
                pass
            if activated:
                break
        focus_ok = False
        try:
            focus_ok = bool(_USER32.SetFocus(wintypes.HWND(hwnd)))
        except Exception:
            focus_ok = False
        if not focus_ok and use_root and root_hwnd and root_hwnd != hwnd:
            try:
                focus_ok = bool(_USER32.SetFocus(wintypes.HWND(root_hwnd)))
            except Exception:
                focus_ok = False
        return activated or focus_ok

    def force_focus_presentation_window(self) -> bool:
        """强制将目标窗口置前并尝试聚焦，主要用于光标模式下让物理输入回到放映窗口。"""

        if not self.is_supported():
            return False
        self._register_input_activity()
        target = self._resolve_presentation_target()
        if not target:
            target = self._detect_presentation_window()
        if not target:
            return False
        if not self.overlay._presentation_control_allowed(target):
            self.clear_cached_target()
            return False
        attach_pair = self._attach_to_target_thread(target)
        try:
            activated = self._activate_window_for_input(target, force=True)
            if win32gui is not None:
                try:
                    win32gui.SetForegroundWindow(target)
                    activated = True
                except Exception:
                    pass
        finally:
            self._detach_from_target_thread(attach_pair)
        if activated:
            self._last_target_hwnd = target
        return activated

    def _top_level_hwnd(self, hwnd: int) -> int:
        if win32gui is None or hwnd == 0:
            return hwnd
        try:
            ga_root = getattr(win32con, "GA_ROOT", 2) if win32con is not None else 2
        except Exception:
            ga_root = 2
        try:
            root = win32gui.GetAncestor(hwnd, ga_root)
            if root:
                return int(root)
        except Exception:
            pass
        try:
            parent = win32gui.GetParent(hwnd)
            if parent:
                return int(parent)
        except Exception:
            pass
        return hwnd

    def _send_input_event(self, vk_code: int, *, is_press: bool) -> bool:
        if _USER32 is None or self._Input is None or self._KeyboardInput is None:
            return False
        keyboard_input = self._KeyboardInput()
        keyboard_input.wVk = vk_code & 0xFFFF
        keyboard_input.wScan = self._map_virtual_key(vk_code)
        flags = 0
        if vk_code in self._EXTENDED_KEY_CODES:
            flags |= self._KEYEVENTF_EXTENDEDKEY
        if not is_press:
            flags |= self._KEYEVENTF_KEYUP
        keyboard_input.dwFlags = flags
        keyboard_input.time = 0
        try:
            keyboard_input.dwExtraInfo = 0
        except Exception:
            pass
        input_record = self._Input()
        input_record.type = self._INPUT_KEYBOARD
        input_record.data.ki = keyboard_input
        try:
            sent = int(_USER32.SendInput(1, ctypes.byref(input_record), ctypes.sizeof(self._Input)))
        except Exception:
            sent = 0
        return bool(sent)

    def _send_key_message_sequence(self, hwnd: int, vk_code: int) -> bool:
        if win32con is None or hwnd == 0 or vk_code == 0:
            return False
        down_param = self._build_basic_key_lparam(vk_code, is_press=True)
        up_param = self._build_basic_key_lparam(vk_code, is_press=False)
        press = self._deliver_key_message(hwnd, win32con.WM_KEYDOWN, vk_code, down_param)
        release = self._deliver_key_message(hwnd, win32con.WM_KEYUP, vk_code, up_param)
        return press and release

    def _map_virtual_key(self, vk_code: int) -> int:
        map_vk = getattr(win32api, "MapVirtualKey", None) if win32api is not None else None
        if callable(map_vk):
            try:
                return int(map_vk(vk_code, 0)) & 0xFFFF
            except Exception:
                return 0
        return 0

    def _normalize_presentation_target(self, hwnd: int) -> Optional[int]:
        if not self._is_hwnd_valid(hwnd):
            return None
        return hwnd

    def _target_priority(self, hwnd: int, *, base: int) -> int:
        score = base
        class_name = self._window_class_name(hwnd)
        if self._is_wps_slideshow_window(hwnd) or self._is_ms_slideshow_window(hwnd):
            score += 10000
        elif self._is_slideshow_class(class_name):
            score += 520
        elif class_name in self._KNOWN_PRESENTATION_CLASSES:
            score += 300
        if class_name in self._PRESENTATION_EDITOR_CLASSES:
            score -= 340
        if "document" in class_name or "viewer" in class_name:
            score += 220
        has_caption = self._has_window_caption(hwnd)
        if has_caption is False:
            score += 160
        elif has_caption is True:
            score -= 180
        rect = self._get_window_rect_generic(hwnd)
        if rect is not None:
            left, top, right, bottom = rect
            width = max(0, right - left)
            height = max(0, bottom - top)
            if width > 0 and height > 0:
                area = width * height
                score += min(area // 24000, 160)
                if width >= 600 and height >= 400:
                    score += 80
        is_topmost = self._is_topmost_window(hwnd)
        if is_topmost:
            score += 40
        return score

    def _iter_targets_with_priority(
        self,
        target: int,
        *,
        focus_base: int,
        target_base: int,
        child_base: int,
    ) -> Iterable[Tuple[int, bool]]:
        if target == 0 or not self._is_hwnd_valid(target):
            return ()
        seen: Set[int] = set()
        ranked: List[Tuple[int, int, bool]] = []

        def _append(
            hwnd: int,
            *,
            cache: bool,
            require_visible: bool,
            base: int,
        ) -> None:
            if hwnd in seen:
                return
            if not self._is_keyboard_target(hwnd, require_visible=require_visible):
                return
            seen.add(hwnd)
            priority = self._target_priority(hwnd, base=base)
            ranked.append((priority, hwnd, cache))

        for focus_hwnd in self._gather_thread_focus_handles(target):
            _append(focus_hwnd, cache=False, require_visible=False, base=focus_base)
        _append(target, cache=True, require_visible=True, base=target_base)
        for child_hwnd in self._collect_descendant_windows(target):
            _append(child_hwnd, cache=False, require_visible=False, base=child_base)

        ranked.sort(key=lambda item: item[0], reverse=True)
        for _priority, hwnd, cache in ranked:
            yield hwnd, cache

    def _iter_key_targets(self, target: int) -> Iterable[Tuple[int, bool]]:
        yield from self._iter_targets_with_priority(
            target,
            focus_base=900,
            target_base=820,
            child_base=780,
        )

    def _iter_wheel_targets(self, target: int) -> Iterable[Tuple[int, bool]]:
        yield from self._iter_targets_with_priority(
            target,
            focus_base=880,
            target_base=800,
            child_base=760,
        )

    def _build_key_lparam(self, vk_code: int, event: QKeyEvent, is_press: bool) -> int:
        repeat_getter = getattr(event, "count", None)
        repeat_count = 1
        if callable(repeat_getter):
            try:
                repeat_count = max(1, int(repeat_getter()))
            except Exception:
                repeat_count = 1
        l_param = repeat_count & 0xFFFF
        scan_code = self._map_virtual_key(vk_code)
        l_param |= (scan_code & 0xFF) << 16
        if vk_code in self._EXTENDED_KEY_CODES:
            l_param |= 1 << 24
        auto_repeat_getter = getattr(event, "isAutoRepeat", None)
        is_auto_repeat = False
        if callable(auto_repeat_getter):
            try:
                is_auto_repeat = bool(auto_repeat_getter())
            except Exception:
                is_auto_repeat = False
        if is_press:
            if is_auto_repeat:
                l_param |= 1 << 30
        else:
            l_param |= 1 << 30
            l_param |= 1 << 31
        return l_param & 0xFFFFFFFF

    def _build_basic_key_lparam(self, vk_code: int, *, is_press: bool) -> int:
        l_param = 1
        scan_code = self._map_virtual_key(vk_code)
        l_param |= (scan_code & 0xFF) << 16
        if vk_code in self._EXTENDED_KEY_CODES:
            l_param |= 1 << 24
        if not is_press:
            l_param |= 1 << 30
            l_param |= 1 << 31
        return l_param & 0xFFFFFFFF

    def _deliver_key_message(self, hwnd: int, message: int, vk_code: int, l_param: int) -> bool:
        if not self._is_hwnd_valid(hwnd):
            return False
        delivered = False
        if win32api is not None:
            try:
                delivered = bool(win32api.PostMessage(hwnd, message, vk_code, l_param))
            except Exception:
                delivered = False
        if delivered:
            return True
        if _USER32 is None:
            return False
        result = ctypes.c_size_t()
        try:
            sent = _USER32.SendMessageTimeoutW(
                hwnd,
                message,
                wintypes.WPARAM(vk_code),
                wintypes.LPARAM(l_param),
                self._SMTO_ABORTIFHUNG,
                30,
                ctypes.byref(result),
            )
        except Exception:
            sent = 0
        return bool(sent)

    def _deliver_mouse_wheel(self, hwnd: int, w_param: int, l_param: int) -> bool:
        if not self._is_hwnd_valid(hwnd):
            return False
        delivered = False
        if win32api is not None and win32con is not None:
            try:
                delivered = bool(win32api.PostMessage(hwnd, win32con.WM_MOUSEWHEEL, w_param, l_param))
            except Exception:
                delivered = False
        if delivered:
            return True
        if _USER32 is None:
            return False
        result = ctypes.c_size_t()
        try:
            sent = _USER32.SendMessageTimeoutW(
                hwnd,
                win32con.WM_MOUSEWHEEL if win32con is not None else 0x020A,
                wintypes.WPARAM(w_param),
                wintypes.LPARAM(l_param),
                self._SMTO_ABORTIFHUNG,
                30,
                ctypes.byref(result),
            )
        except Exception:
            sent = 0
        return bool(sent)

    def _is_overlay_window(self, hwnd: int) -> bool:
        try:
            overlay_hwnd = int(self.overlay.winId()) if self.overlay.winId() else 0
        except Exception:
            overlay_hwnd = 0
        return hwnd != 0 and hwnd == overlay_hwnd

    def _is_keyboard_target(self, hwnd: int, *, require_visible: bool) -> bool:
        if hwnd == 0 or self._is_overlay_window(hwnd):
            return False
        if self._should_ignore_window(hwnd):
            return False
        if win32gui is not None:
            try:
                if not win32gui.IsWindow(hwnd):
                    return False
                if require_visible and not win32gui.IsWindowVisible(hwnd):
                    return False
            except Exception:
                return False
            return True
        if _USER32 is None:
            return False
        try:
            if not _USER32.IsWindow(wintypes.HWND(hwnd)):
                return False
            if require_visible and not _USER32.IsWindowVisible(wintypes.HWND(hwnd)):
                return False
        except Exception:
            return False
        return True

    def _gather_thread_focus_handles(self, target: int) -> Iterable[int]:
        if _USER32 is None:
            return ()
        info = self._GuiThreadInfo()
        pid = wintypes.DWORD()
        try:
            thread_id = _USER32.GetWindowThreadProcessId(
                wintypes.HWND(target), ctypes.byref(pid)
            )
        except Exception:
            return ()
        if not thread_id:
            return ()
        info.cbSize = ctypes.sizeof(info)
        try:
            ok = bool(_USER32.GetGUIThreadInfo(thread_id, ctypes.byref(info)))
        except Exception:
            ok = False
        if not ok:
            return ()
        handles = (
            info.hwndFocus,
            info.hwndActive,
            info.hwndCapture,
            info.hwndMenuOwner,
            info.hwndCaret,
        )
        return tuple(int(h) for h in handles if h)

    def _collect_descendant_windows(self, root: int) -> Iterable[int]:
        if not self._is_hwnd_valid(root):
            return ()
        queue: deque[int] = deque([root])
        discovered: Set[int] = {root}
        results: List[int] = []
        buffer = self._child_buffer
        while queue and len(results) < self._MAX_CHILD_FORWARDS:
            parent = queue.popleft()
            snapshot: List[int] = []
            if win32gui is not None:
                buffer.clear()

                def _collector(child_hwnd: int, acc: List[int]) -> bool:
                    if child_hwnd not in discovered:
                        acc.append(child_hwnd)
                    return len(acc) < self._MAX_CHILD_FORWARDS

                try:
                    win32gui.EnumChildWindows(parent, _collector, buffer)
                except Exception:
                    continue
                snapshot = list(buffer)
                buffer.clear()
            elif _USER32 is not None and _WNDENUMPROC is not None:
                def _enum_child(child_hwnd: int, _lparam: int) -> int:
                    if child_hwnd not in discovered:
                        snapshot.append(int(child_hwnd))
                    return int(len(snapshot) < self._MAX_CHILD_FORWARDS)

                enum_proc = _WNDENUMPROC(_enum_child)
                try:
                    _USER32.EnumChildWindows(wintypes.HWND(parent), enum_proc, 0)
                except Exception:
                    continue
            else:
                break

            for child in snapshot:
                if child in discovered:
                    continue
                discovered.add(child)
                results.append(child)
                if len(results) >= self._MAX_CHILD_FORWARDS:
                    break
                queue.append(child)
        return tuple(results)

    def _get_window_styles(self, hwnd: int) -> Tuple[Optional[int], Optional[int]]:
        if hwnd == 0:
            return None, None
        if _USER32 is not None and not self._is_hwnd_valid(hwnd):
            return None, None
        style: Optional[int] = None
        ex_style: Optional[int] = None
        try:
            if win32gui is not None:
                style = int(win32gui.GetWindowLong(hwnd, getattr(win32con, "GWL_STYLE", -16)))
                ex_style = int(win32gui.GetWindowLong(hwnd, getattr(win32con, "GWL_EXSTYLE", -20)))
            elif _USER32 is not None:
                gwl_style = getattr(win32con, "GWL_STYLE", -16)
                gwl_exstyle = getattr(win32con, "GWL_EXSTYLE", -20)
                style = int(_USER32.GetWindowLongW(wintypes.HWND(hwnd), gwl_style))
                ex_style = int(_USER32.GetWindowLongW(wintypes.HWND(hwnd), gwl_exstyle))
        except Exception:
            style = style if isinstance(style, int) else None
            ex_style = ex_style if isinstance(ex_style, int) else None
        return style, ex_style

    def _has_window_caption(self, hwnd: int) -> Optional[bool]:
        style, _ = self._get_window_styles(hwnd)
        if style is None:
            return None
        caption_flag = getattr(win32con, "WS_CAPTION", 0x00C00000)
        return bool(style & caption_flag)

    def _is_topmost_window(self, hwnd: int) -> Optional[bool]:
        _, ex_style = self._get_window_styles(hwnd)
        if ex_style is None:
            return None
        topmost_flag = getattr(win32con, "WS_EX_TOPMOST", 0x00000008)
        return bool(ex_style & topmost_flag)

    def _get_window_rect_generic(self, hwnd: int) -> Optional[Tuple[int, int, int, int]]:
        if win32gui is not None:
            try:
                rect = win32gui.GetWindowRect(hwnd)
                if rect:
                    return int(rect[0]), int(rect[1]), int(rect[2]), int(rect[3])
            except Exception:
                pass
        return _user32_window_rect(hwnd)

    def _candidate_score(self, hwnd: int) -> int:
        rect = self._get_window_rect_generic(hwnd)
        if rect is None:
            return -1
        left, top, right, bottom = rect
        width = max(0, right - left)
        height = max(0, bottom - top)
        if width == 0 or height == 0:
            return -1
        class_name = ""
        if win32gui is not None:
            try:
                class_name = win32gui.GetClassName(hwnd)
            except Exception:
                class_name = ""
        if not class_name:
            class_name = _user32_window_class_name(hwnd)
        class_name = class_name.strip().lower()

        score = 0
        if class_name in self._SLIDESHOW_PRIORITY_CLASSES:
            score += 2000
        elif class_name in self._SLIDESHOW_SECONDARY_CLASSES:
            score += 1200
        elif "screen" in class_name or "slide" in class_name or "show" in class_name:
            score += 900
        elif class_name in self._KNOWN_PRESENTATION_CLASSES:
            score += 400

        has_caption = self._has_window_caption(hwnd)
        if has_caption is False:
            score += 220
        elif has_caption is True:
            score -= 180

        if class_name in self._PRESENTATION_EDITOR_CLASSES:
            score -= 600

        is_topmost = self._is_topmost_window(hwnd)
        if is_topmost:
            score += 80

        overlay_rect = self._overlay_rect_tuple()
        if overlay_rect is not None:
            o_width = max(0, overlay_rect[2] - overlay_rect[0])
            o_height = max(0, overlay_rect[3] - overlay_rect[1])
            if o_width > 0 and o_height > 0:
                width_diff = abs(width - o_width)
                height_diff = abs(height - o_height)
                size_penalty = min(width_diff + height_diff, 1600)
                score += max(0, 320 - size_penalty // 3)
                area = width * height
                overlay_area = o_width * o_height
                if overlay_area > 0:
                    ratio = min(area, overlay_area) / max(area, overlay_area)
                    score += int(ratio * 160)
                overlap_x = max(0, min(right, overlay_rect[2]) - max(left, overlay_rect[0]))
                overlap_y = max(0, min(bottom, overlay_rect[3]) - max(top, overlay_rect[1]))
                overlap_area = overlap_x * overlap_y
                if overlap_area > 0 and area > 0:
                    score += int((overlap_area / area) * 180)

        return score

    def _fallback_detect_presentation_window_user32(self) -> Optional[int]:
        if _USER32 is None:
            return None
        now = time.monotonic()
        if self._probe_cooldown_until and now < self._probe_cooldown_until:
            return None
        overlay_hwnd = int(self.overlay.winId()) if self.overlay.winId() else 0
        best_hwnd: Optional[int] = None
        best_score = -1
        foreground = _user32_get_foreground_window()
        if (
            foreground
            and foreground != overlay_hwnd
            and not self._should_ignore_window(foreground)
            and self._fallback_is_candidate_window(foreground)
        ):
            score = self._candidate_score(foreground)
            if score > best_score and self._is_control_allowed(foreground, log=False):
                best_score = score
                best_hwnd = foreground
        candidates = self._enumerate_overlay_candidate_windows(overlay_hwnd)
        if candidates is None:
            return best_hwnd
        for hwnd in candidates:
            if not self._fallback_is_candidate_window(hwnd):
                continue
            score = self._candidate_score(hwnd)
            if score > best_score and self._is_control_allowed(hwnd, log=False):
                best_score = score
                best_hwnd = hwnd
        self._update_probe_backoff(bool(best_hwnd))
        return best_hwnd

    def _is_candidate_window(self, hwnd: int) -> bool:
        if win32gui is None:
            return self._fallback_is_candidate_window(hwnd)
        if self._should_ignore_window(hwnd):
            return False
        try:
            class_name = win32gui.GetClassName(hwnd)
        except Exception:
            class_name = ""
        class_name = class_name.strip().lower()
        if not class_name:
            return False
        if class_name in self._KNOWN_PRESENTATION_CLASSES:
            return True
        if any(class_name.startswith(prefix) for prefix in self._KNOWN_PRESENTATION_PREFIXES):
            return True
        try:
            rect = win32gui.GetWindowRect(hwnd)
        except Exception:
            return False
        if not rect:
            return False
        return self._matches_overlay_geometry(rect)

    def _detect_presentation_window(self) -> Optional[int]:
        if win32gui is None:
            return self._fallback_detect_presentation_window_user32()
        now = time.monotonic()
        if self._probe_cooldown_until and now < self._probe_cooldown_until:
            return None
        overlay_hwnd = int(self.overlay.winId()) if self.overlay.winId() else 0
        try:
            foreground = win32gui.GetForegroundWindow()
        except Exception:
            foreground = 0
        best_hwnd: Optional[int] = None
        best_score = -1
        if (
            foreground
            and foreground != overlay_hwnd
            and not self._should_ignore_window(foreground)
            and self._is_candidate_window(foreground)
        ):
            normalized = self._normalize_presentation_target(foreground)
            if (
                normalized
                and self._is_target_window_valid(normalized)
                and self._is_control_allowed(normalized, log=False)
            ):
                score = self._candidate_score(normalized)
                if score > best_score:
                    best_score = score
                    best_hwnd = normalized

        candidates = self._enumerate_overlay_candidate_windows_win32(overlay_hwnd)
        if candidates is None:
            return best_hwnd
        for hwnd in candidates:
            if not self._is_candidate_window(hwnd):
                continue
            normalized = self._normalize_presentation_target(hwnd)
            if not normalized or not self._is_target_window_valid(normalized):
                continue
            if not self._is_control_allowed(normalized, log=False):
                continue
            score = self._candidate_score(normalized)
            if score > best_score:
                best_score = score
                best_hwnd = normalized
        self._update_probe_backoff(bool(best_hwnd))
        return best_hwnd

    def _resolve_presentation_target(self) -> Optional[int]:
        if win32gui is None:
            hwnd = self._last_target_hwnd
            if hwnd and not self._is_control_allowed(hwnd, log=False):
                self.clear_cached_target()
                hwnd = None
            if hwnd and self._fallback_is_target_window_valid(hwnd):
                normalized = self._normalize_presentation_target(hwnd)
                if normalized and normalized != hwnd and self._fallback_is_target_window_valid(normalized):
                    self._last_target_hwnd = normalized
                    return normalized
                if self._should_refresh_cached_target(hwnd):
                    refreshed = self._fallback_detect_presentation_window_user32()
                    if (
                        refreshed
                        and refreshed != hwnd
                        and self._fallback_is_target_window_valid(refreshed)
                    ):
                        normalized = self._normalize_presentation_target(refreshed)
                        if normalized and self._fallback_is_target_window_valid(normalized):
                            if self._is_control_allowed(normalized, log=False):
                                self._last_target_hwnd = normalized
                                return normalized
                            self.clear_cached_target()
                            return None
                        if self._is_control_allowed(refreshed, log=False):
                            self._last_target_hwnd = refreshed
                            return refreshed
                        self.clear_cached_target()
                        return None
                return hwnd
            hwnd = self._fallback_detect_presentation_window_user32()
            normalized = self._normalize_presentation_target(hwnd) if hwnd else None
            target = normalized or hwnd
            if target and self._fallback_is_target_window_valid(target):
                if self._is_control_allowed(target, log=False):
                    self._last_target_hwnd = target
                    return target
                self.clear_cached_target()
                return None
            self._last_target_hwnd = None
            return None
        hwnd = self._last_target_hwnd
        if hwnd and not self._is_control_allowed(hwnd, log=False):
            self.clear_cached_target()
            hwnd = None
        if hwnd and self._is_target_window_valid(hwnd):
            normalized = self._normalize_presentation_target(hwnd)
            if normalized and normalized != hwnd and self._is_target_window_valid(normalized):
                self._last_target_hwnd = normalized
                hwnd = normalized
            if self._should_refresh_cached_target(hwnd):
                refreshed = self._detect_presentation_window()
                normalized = self._normalize_presentation_target(refreshed) if refreshed else None
                target = normalized or refreshed
                if target and target != hwnd and self._is_target_window_valid(target):
                    if self._is_control_allowed(target, log=False):
                        self._last_target_hwnd = target
                        return target
                    self.clear_cached_target()
                    return None
            return hwnd
        hwnd = self._detect_presentation_window()
        normalized = self._normalize_presentation_target(hwnd) if hwnd else None
        target = normalized or hwnd
        if target and self._is_target_window_valid(target):
            if self._is_control_allowed(target, log=False):
                self._last_target_hwnd = target
                return target
            self.clear_cached_target()
            return None
        self._last_target_hwnd = None
        return None


class WpsSlideshowNavigationHook(QObject):
    """在画笔模式下拦截物理滚轮/翻页键，必要时仅阻断输入避免双触发。"""

    nav_requested = pyqtSignal(int, str)  # direction (+1/-1), source ("wheel"/"keyboard")

    _WH_KEYBOARD_LL = 13
    _WH_MOUSE_LL = 14
    _HC_ACTION = 0

    _WM_KEYDOWN = 0x0100
    _WM_KEYUP = 0x0101
    _WM_SYSKEYDOWN = 0x0104
    _WM_SYSKEYUP = 0x0105
    _WM_MOUSEWHEEL = 0x020A

    class _KBDLLHOOKSTRUCT(ctypes.Structure):
        _fields_ = [
            ("vkCode", wintypes.DWORD),
            ("scanCode", wintypes.DWORD),
            ("flags", wintypes.DWORD),
            ("time", wintypes.DWORD),
            ("dwExtraInfo", wintypes.ULONG_PTR),
        ]

    class _MSLLHOOKSTRUCT(ctypes.Structure):
        _fields_ = [
            ("pt", wintypes.POINT),
            ("mouseData", wintypes.DWORD),
            ("flags", wintypes.DWORD),
            ("time", wintypes.DWORD),
            ("dwExtraInfo", wintypes.ULONG_PTR),
        ]

    def __init__(self, parent: Optional[QObject] = None) -> None:
        super().__init__(parent)
        self._keyboard_hook: Optional[int] = None
        self._mouse_hook: Optional[int] = None
        self._c_keyboard_proc = None
        self._c_mouse_proc = None
        self._intercept_enabled = False
        self._block_only = False
        self._intercept_keyboard = True
        self._intercept_wheel = True
        self._emit_wheel_on_block = True
        vk_space = getattr(win32con, "VK_SPACE", 0x20) if win32con is not None else 0x20
        vk_return = getattr(win32con, "VK_RETURN", 0x0D) if win32con is not None else 0x0D
        self._vk_allow: Set[int] = {
            VK_UP,
            VK_DOWN,
            VK_LEFT,
            VK_RIGHT,
            VK_PRIOR,
            VK_NEXT,
            vk_space,
            vk_return,
        }

    @property
    def available(self) -> bool:
        return bool(_USER32 and _KERNEL32 and hasattr(_USER32, "SetWindowsHookExW"))

    def set_intercept_enabled(self, enabled: bool) -> None:
        self._intercept_enabled = bool(enabled)

    def set_block_only(self, enabled: bool) -> None:
        self._block_only = bool(enabled)

    def set_intercept_keyboard(self, enabled: bool) -> None:
        self._intercept_keyboard = bool(enabled)

    def set_intercept_wheel(self, enabled: bool) -> None:
        self._intercept_wheel = bool(enabled)

    def set_emit_wheel_on_block(self, enabled: bool) -> None:
        self._emit_wheel_on_block = bool(enabled)

    def start(self) -> bool:
        if not self.available:
            return False
        if self._keyboard_hook and self._mouse_hook:
            return True

        def _call_next(hhook: Optional[int], nCode: int, wParam: int, lParam: int) -> int:
            try:
                if _USER32 is not None and hasattr(_USER32, "CallNextHookEx"):
                    return _USER32.CallNextHookEx(hhook, nCode, wParam, lParam)
            except Exception:
                pass
            return 0

        def keyboard_proc(nCode: int, wParam: int, lParam: int) -> int:
            if nCode != self._HC_ACTION or not self._intercept_enabled:
                return _call_next(self._keyboard_hook, nCode, wParam, lParam)
            if not self._intercept_keyboard:
                return _call_next(self._keyboard_hook, nCode, wParam, lParam)
            try:
                if wParam in (self._WM_KEYDOWN, self._WM_SYSKEYDOWN, self._WM_KEYUP, self._WM_SYSKEYUP):
                    kb = ctypes.cast(lParam, ctypes.POINTER(self._KBDLLHOOKSTRUCT)).contents
                    vk = int(kb.vkCode)
                    if vk in self._vk_allow:
                        if self._block_only:
                            if wParam in (self._WM_KEYDOWN, self._WM_SYSKEYDOWN):
                                prev_vk = {VK_UP, VK_LEFT, VK_PRIOR}
                                direction = -1 if vk in prev_vk else 1
                                owner = self.parent()
                                if owner is not None and hasattr(owner, "_mark_wps_hook_input"):
                                    try:
                                        owner._mark_wps_hook_input()
                                    except Exception:
                                        pass
                                QTimer.singleShot(
                                    0, lambda d=direction: self.nav_requested.emit(d, "keyboard")
                                )
                            return 1
                        if wParam in (self._WM_KEYDOWN, self._WM_SYSKEYDOWN):
                            prev_vk = {VK_UP, VK_LEFT, VK_PRIOR}
                            direction = -1 if vk in prev_vk else 1
                            owner = self.parent()
                            if owner is not None and hasattr(owner, "_mark_wps_hook_input"):
                                try:
                                    owner._mark_wps_hook_input()
                                except Exception:
                                    pass
                            QTimer.singleShot(0, lambda d=direction: self.nav_requested.emit(d, "keyboard"))
                        return 1
            except Exception:
                return _call_next(self._keyboard_hook, nCode, wParam, lParam)
            return _call_next(self._keyboard_hook, nCode, wParam, lParam)

        def mouse_proc(nCode: int, wParam: int, lParam: int) -> int:
            if nCode != self._HC_ACTION or not self._intercept_enabled:
                return _call_next(self._mouse_hook, nCode, wParam, lParam)
            if not self._intercept_wheel:
                return _call_next(self._mouse_hook, nCode, wParam, lParam)
            try:
                if wParam == self._WM_MOUSEWHEEL:
                    ms = ctypes.cast(lParam, ctypes.POINTER(self._MSLLHOOKSTRUCT)).contents
                    delta = ctypes.c_short((int(ms.mouseData) >> 16) & 0xFFFF).value
                    if delta:
                        if self._block_only:
                            if not self._emit_wheel_on_block:
                                return 1
                            direction = 1 if delta < 0 else -1
                            owner = self.parent()
                            if owner is not None and hasattr(owner, "_mark_wps_hook_input"):
                                try:
                                    owner._mark_wps_hook_input()
                                except Exception:
                                    pass
                            QTimer.singleShot(0, lambda d=direction: self.nav_requested.emit(d, "wheel"))
                            return 1
                        direction = 1 if delta < 0 else -1
                        owner = self.parent()
                        if owner is not None and hasattr(owner, "_mark_wps_hook_input"):
                            try:
                                owner._mark_wps_hook_input()
                            except Exception:
                                pass
                        QTimer.singleShot(0, lambda d=direction: self.nav_requested.emit(d, "wheel"))
                        return 1
            except Exception:
                return _call_next(self._mouse_hook, nCode, wParam, lParam)
            return _call_next(self._mouse_hook, nCode, wParam, lParam)

        try:
            module = _KERNEL32.GetModuleHandleW(None) if _KERNEL32 is not None else None
        except Exception:
            module = None

        self._c_keyboard_proc = _HOOKPROC_TYPE(keyboard_proc)
        self._c_mouse_proc = _HOOKPROC_TYPE(mouse_proc)
        try:
            self._keyboard_hook = _USER32.SetWindowsHookExW(
                self._WH_KEYBOARD_LL,
                self._c_keyboard_proc,
                module,
                0,
            )
            self._mouse_hook = _USER32.SetWindowsHookExW(
                self._WH_MOUSE_LL,
                self._c_mouse_proc,
                module,
                0,
            )
        except Exception:
            self._keyboard_hook = None
            self._mouse_hook = None
            return False
        ok = bool(self._keyboard_hook and self._mouse_hook)
        if not ok:
            self.stop()
        return ok

    def stop(self) -> None:
        if not self.available:
            self._keyboard_hook = None
            self._mouse_hook = None
            self._c_keyboard_proc = None
            self._c_mouse_proc = None
            self._intercept_enabled = False
            self._block_only = False
            self._intercept_keyboard = True
            self._intercept_wheel = True
            return
        try:
            if self._keyboard_hook:
                _USER32.UnhookWindowsHookEx(self._keyboard_hook)
        except Exception:
            pass
        try:
            if self._mouse_hook:
                _USER32.UnhookWindowsHookEx(self._mouse_hook)
        except Exception:
            pass
        self._keyboard_hook = None
        self._mouse_hook = None
        self._c_keyboard_proc = None
        self._c_mouse_proc = None
        self._intercept_enabled = False
        self._block_only = False
        self._intercept_keyboard = True
        self._intercept_wheel = True
        self._emit_wheel_on_block = True


class OverlayWindow(QWidget, _PresentationWindowMixin):
    _NAVIGATION_RESTORE_DELAY_MS = 600
    _NAVIGATION_HOLD_DURATION_MS = 2400
    _WPS_NAV_DEBOUNCE_MS = 200  # suppress identical WPS导航事件的最小间隔
    _CURSOR_CACHE_LIMIT = 32
    _SLIDESHOW_TARGET_CACHE_MS = 800
    _RELEASE_TAIL_STEPS = 4
    _RELEASE_TAIL_MIN_LEN = 3.0
    _RELEASE_TAIL_MAX_LEN = 10.0
    _RELEASE_TAIL_LEN_FACTOR = 1.1
    _RELEASE_TAIL_LEN_MAX_SCALE = 2.2
    _RELEASE_TAIL_STEP_FACTOR = 2.0
    _RELEASE_TAIL_TIP_EXP = 1.6
    _RELEASE_TAIL_MIN_RATIO = 0.02
    _RELEASE_TAIL_MIN_ABS = 0.12
    _RELEASE_TAIL_REDRAW_MULT = 3.0
    _RELEASE_TAIL_SPEED_MIN = 140.0
    _RELEASE_TAIL_SPEED_SCALE = 10.0
    _RELEASE_TAIL_SPEED_RATIO = 0.65
    _RELEASE_TAIL_MIN_MOVE_RATIO = 0.08
    _RELEASE_TAIL_MIN_MOVE_ABS = 0.4
    _RELEASE_TAIL_PAUSE_SECONDS = 0.12
    _RELEASE_TAIL_ALPHA_FALLOFF = 0.55

    def _overlay_widget(self) -> Optional[QWidget]:
        return self

    @staticmethod
    def _normalize_color_hex(color_hex: str, fallback: str) -> str:
        color = QColor(color_hex)
        if not color.isValid():
            color = QColor(fallback)
        return color.name().lower()

    def _normalize_quick_colors(self, colors: Iterable[str]) -> List[str]:
        defaults = ["#000000", "#ff0000", "#1e90ff"]
        normalized: List[str] = []
        for idx, color in enumerate(colors):
            fallback = defaults[idx] if idx < len(defaults) else defaults[-1]
            normalized.append(self._normalize_color_hex(color, fallback))
        while len(normalized) < 3:
            normalized.append(defaults[len(normalized)])
        return normalized[:3]

    def get_quick_colors(self) -> List[str]:
        return list(self.quick_colors)

    def set_quick_color_slot(self, index: int, color_hex: str) -> str:
        if index < 0 or index >= len(self.quick_colors):
            return color_hex
        normalized = self._normalize_color_hex(color_hex, self.quick_colors[index])
        self.quick_colors[index] = normalized
        if getattr(self, "toolbar", None):
            try:
                self.toolbar.update_quick_color_slot(index, normalized)
            except Exception:
                pass
        self.save_settings()
        return normalized

    def __init__(self, settings_manager: SettingsManager) -> None:
        super().__init__(None, Qt.WindowType.FramelessWindowHint | Qt.WindowType.WindowStaysOnTopHint)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.settings_manager = settings_manager
        paint_config = self.settings_manager.get_paint_settings()
        self.paint_config = paint_config
        self.ui_scale = float(clamp(getattr(paint_config, "ui_scale", 1.0), 0.8, 2.0))
        self.paint_config.ui_scale = float(self.ui_scale)
        self._nav_debug_enabled = (
            parse_bool(os.environ.get("CTOOL_NAV_DEBUG"), False)
            or bool(getattr(paint_config, "nav_debug", False))
        )
        stored_wps_mode = _normalize_wps_input_mode(
            getattr(paint_config, "wps_input_mode", "auto")
        )
        if stored_wps_mode == "manual":
            stored_wps_mode = "raw" if bool(getattr(paint_config, "wps_raw_input", False)) else "message"
        if stored_wps_mode not in {"auto", "raw", "message"}:
            stored_wps_mode = "auto"
        self._wps_input_mode = stored_wps_mode
        self._wps_raw_input_mode = bool(getattr(paint_config, "wps_raw_input", False))
        self._wps_wheel_forward = bool(getattr(paint_config, "wps_wheel_forward", False))
        self._wps_auto_force_message = False
        self._focus_accept_blocked = False
        self._presentation_control_flags: Dict[str, bool] = {}
        self._update_presentation_control_flags(
            {
                "ms_ppt": paint_config.control_ms_ppt,
                "wps_ppt": paint_config.control_wps_ppt,
            }
        )
        self.pen_style = _parse_pen_style(paint_config.brush_style, _DEFAULT_PEN_STYLE)
        parsed_base = clamp_base_size_for_style(self.pen_style, float(paint_config.brush_base_size))
        self.pen_base_size = parsed_base
        color_hex = paint_config.brush_color
        self.pen_color = QColor(color_hex)
        if not self.pen_color.isValid():
            self.pen_color = QColor("#ff0000")
        self._brush_opacity = int(clamp(getattr(paint_config, "brush_opacity", 255), 0, 255))
        self.quick_colors: List[str] = self._normalize_quick_colors(
            [
                getattr(paint_config, "quick_color_1", "#000000"),
                getattr(paint_config, "quick_color_2", "#ff0000"),
                getattr(paint_config, "quick_color_3", "#1e90ff"),
            ]
        )
        stored_board_color = getattr(paint_config, "board_color", "#ffffff")
        board_color = QColor(stored_board_color)
        self.last_board_color = board_color if board_color.isValid() else QColor("#ffffff")
        self.eraser_size = float(clamp(getattr(paint_config, "eraser_size", 24.0), 1.0, 50.0))
        config = get_pen_style_config(self.pen_style)
        self.pen_size = max(1, int(round(self.pen_base_size * config.width_multiplier)))
        self.mode = "brush"
        stored_shape = getattr(paint_config, "shape_type", None)
        self.current_shape = str(stored_shape) if stored_shape else None
        if self.current_shape not in SHAPE_TYPES:
            self.current_shape = None
        self.shape_start_point: Optional[QPoint] = None
        self.drawing = False
        self.last_point = QPointF()
        self.last_width = max(1.0, self.pen_base_size * config.target_min_factor)
        self._stroke_target_width = float(self.last_width)
        self.last_time = time.time()
        self._last_brush_color = QColor(self.pen_color)
        self._last_pen_style: PenStyle = self.pen_style
        self._last_pen_base_size: float = float(self.pen_base_size)
        self._last_draw_mode = "brush"
        self._last_shape_type: Optional[str] = None
        self._restoring_tool = False
        self._eraser_last_point: Optional[QPoint] = None
        self._stroke_points: deque[QPointF] = deque(maxlen=8)
        self._stroke_timestamps: deque[float] = deque(maxlen=8)
        self._stroke_speed: float = 0.0
        self._stroke_last_midpoint: Optional[QPointF] = None
        self._stroke_filter_point: Optional[QPointF] = None
        self._stroke_width_velocity: float = 0.0
        self._stroke_smoothed_target: float = max(1.0, self.pen_size)
        self._stroke_uniform_canvas: Optional[QPixmap] = None
        self._stroke_uniform_painter: Optional[QPainter] = None
        self._stroke_uniform_active = False
        self._stroke_uniform_bounds: Optional[QRectF] = None
        self._stroke_segments: List[Tuple[QPointF, QPointF, QPointF, float]] = []
        self._stroke_release_taper = False
        self._stroke_total_length: float = 0.0
        self._stroke_tail_state: float = 0.0
        self._stroke_jitter_offset = QPointF()
        self._stroke_rng = random.Random()
        self.navigation_active = False
        self._navigation_reasons: Dict[str, int] = {}
        self._active_navigation_keys: Set[int] = set()
        self._nav_pointer_button = Qt.MouseButton.NoButton
        self._nav_pointer_press_pos = QPointF()
        self._nav_pointer_press_global = QPointF()
        self._nav_pointer_press_modifiers = Qt.KeyboardModifier.NoModifier
        self._nav_pointer_started_draw = False
        self._brush_painter: Optional[QPainter] = None
        self._eraser_painter: Optional[QPainter] = None
        self._last_target_hwnd: Optional[int] = None
        self._pending_tool_restore: Optional[Tuple[str, Optional[str]]] = None
        self._nav_restore_mode: Optional[Tuple[str, Optional[str]]] = None
        self._nav_hold_persistent = False
        self._nav_restore_timer = QTimer(self)
        self._nav_restore_timer.setSingleShot(True)
        self._nav_restore_timer.timeout.connect(self._restore_navigation_tool)
        self._nav_hold_active = False
        self._nav_hold_timer = QTimer(self)
        self._nav_hold_timer.setSingleShot(True)
        self._nav_hold_timer.timeout.connect(self._release_navigation_hold)
        self._skip_focus_reactivation = False
        self._wps_binding_retry_timer: Optional[QTimer] = None
        self._wps_binding_retry_attempts = 0
        self._pending_wps_cursor_pulse = False
        self._wps_cursor_reset_timer: Optional[QTimer] = None
        self._last_wps_nav_event: Optional[Tuple[int, int, float]] = None
        self._wps_nav_block_until: float = 0.0
        self._last_wps_hook_input_ts: float = 0.0
        self._wps_nav_hook: Optional[WpsSlideshowNavigationHook] = None
        self._wps_nav_hook_active = False
        self._wps_hook_intercept_keyboard = True
        self._wps_hook_intercept_wheel = True
        if sys.platform == "win32":
            hook = WpsSlideshowNavigationHook(self)
            if hook.available:
                hook.nav_requested.connect(self._handle_wps_nav_hook_request)
                self._wps_nav_hook = hook

        self._cursor_cache: "OrderedDict[tuple, QCursor]" = OrderedDict()
        base_width = self._effective_brush_width()
        self._brush_pen = QPen(
            self.pen_color,
            base_width,
            Qt.PenStyle.SolidLine,
            Qt.PenCapStyle.RoundCap,
            Qt.PenJoinStyle.RoundJoin,
        )
        fade_color = QColor(self.pen_color)
        fade_color.setAlpha(config.fade_max_alpha)
        self._brush_shadow_pen = QPen(
            fade_color,
            max(0.6, base_width * config.shadow_width_scale),
            Qt.PenStyle.SolidLine,
            Qt.PenCapStyle.RoundCap,
            Qt.PenJoinStyle.RoundJoin,
        )
        self._brush_composition_mode = config.composition_mode
        self._active_base_alpha = int(config.base_alpha)
        self._active_fade_min = int(config.fade_min_alpha)
        self._active_fade_max = int(config.fade_max_alpha)
        self._active_shadow_alpha = int(config.shadow_alpha)
        self._active_alpha_scale = 1.0
        self._refresh_pen_alpha_state()
        self._stroke_smoothed_target = max(1.0, base_width * config.target_min_factor)
        self._update_brush_pen_appearance(base_width, self._active_fade_max)
        self._last_preview_bounds: Optional[QRect] = None
        self.whiteboard_active = False
        self._mode_before_whiteboard: Optional[str] = None
        self.whiteboard_color = QColor(0, 0, 0, 0)
        if not isinstance(self.last_board_color, QColor) or not self.last_board_color.isValid():
            self.last_board_color = QColor("#ffffff")
        self.cursor_pixmap = QPixmap()
        self._eraser_stroker = QPainterPathStroker()
        self._eraser_stroker.setCapStyle(Qt.PenCapStyle.RoundCap)
        self._eraser_stroker.setJoinStyle(Qt.PenJoinStyle.RoundJoin)
        self._eraser_stroker_width = 0.0
        self._forwarder: Optional[_PresentationForwarder] = (
            _PresentationForwarder(self) if _PresentationForwarder.is_supported() else None
        )
        self.setFocusPolicy(Qt.FocusPolicy.StrongFocus)
        self.setMouseTracking(True)
        self._keyboard_grabbed = False
        self._dispatch_suppress_override = False
        self._region_select_start: Optional[QPoint] = None
        self._region_preview_bounds: Optional[QRect] = None
        self._region_previewing = False
        self._toolbar_hovering = False

        self._build_scene()
        self.history: List[QPixmap] = []
        self._history_limit = 30
        self.toolbar = FloatingToolbar(self, self.settings_manager, ui_scale=self.ui_scale)
        self._update_pen_tooltip()
        self.set_mode("brush", initial=True)
        self.toolbar.update_undo_state(False)
        self._apply_whiteboard_lock()

    def raise_toolbar(self) -> None:
        if getattr(self, "toolbar", None) is not None:
            self.toolbar.show()
            self.toolbar.raise_()
        # 确保点名窗口不被画笔层遮挡
        if not self.whiteboard_active:
            self._raise_roll_call_window(activate=False)

    def _build_scene(self) -> None:
        virtual = QRect()
        for screen in QApplication.screens():
            virtual = virtual.united(screen.geometry())
        self.setGeometry(virtual)
        self.canvas = QPixmap(self.size())
        self.canvas.fill(Qt.GlobalColor.transparent)
        self.temp_canvas = QPixmap(self.size())
        self.temp_canvas.fill(Qt.GlobalColor.transparent)

    # ---- 画布绘制 ----
    def _ensure_brush_painter(self) -> QPainter:
        painter = self._brush_painter
        if painter is None:
            painter = QPainter(self.canvas)
            painter.setRenderHint(QPainter.RenderHint.Antialiasing)
            painter.setCompositionMode(self._brush_composition_mode)
            self._brush_painter = painter
        else:
            painter.setCompositionMode(self._brush_composition_mode)
        return painter

    def _release_brush_painter(self) -> None:
        if self._brush_painter is not None:
            self._brush_painter.end()
            self._brush_painter = None

    def _ensure_uniform_painter(self) -> Optional[QPainter]:
        if self._stroke_uniform_canvas is None:
            return None
        painter = self._stroke_uniform_painter
        if painter is None:
            painter = QPainter(self._stroke_uniform_canvas)
            painter.setRenderHint(QPainter.RenderHint.Antialiasing)
            painter.setCompositionMode(QPainter.CompositionMode.CompositionMode_Source)
            self._stroke_uniform_painter = painter
        else:
            painter.setCompositionMode(QPainter.CompositionMode.CompositionMode_Source)
        return painter

    def _release_uniform_painter(self) -> None:
        if self._stroke_uniform_painter is not None:
            self._stroke_uniform_painter.end()
            self._stroke_uniform_painter = None

    def _ensure_eraser_painter(self) -> QPainter:
        painter = self._eraser_painter
        if painter is None:
            painter = QPainter(self.canvas)
            painter.setRenderHint(QPainter.RenderHint.Antialiasing)
            painter.setCompositionMode(QPainter.CompositionMode.CompositionMode_Clear)
            self._eraser_painter = painter
        return painter

    def _release_eraser_painter(self) -> None:
        if self._eraser_painter is not None:
            self._eraser_painter.end()
            self._eraser_painter = None

    def _release_canvas_painters(self) -> None:
        self._release_brush_painter()
        self._release_uniform_painter()
        self._release_eraser_painter()

    def _effective_brush_width(self) -> float:
        config = get_pen_style_config(self.pen_style)
        return max(1.0, float(self.pen_base_size) * config.width_multiplier)

    def _update_brush_pen_appearance(self, width: float, fade_alpha: int) -> None:
        target_fade = int(clamp(fade_alpha, self._active_fade_min, self._active_fade_max))
        base_color = configure_pen_for_style(
            self._brush_pen,
            self._brush_shadow_pen,
            self.pen_color,
            width,
            target_fade,
            self.pen_style,
            base_alpha_override=self._active_base_alpha,
            shadow_alpha_override=self._active_shadow_alpha,
            alpha_scale=self._active_alpha_scale,
        )
        self._active_pen_color = QColor(base_color)

    def _refresh_pen_alpha_state(self) -> None:
        config = get_pen_style_config(self.pen_style)
        override = int(self._brush_opacity)
        target_alpha, fade_min, fade_max, shadow_alpha, scale = resolve_pen_opacity(
            config, override
        )
        self._active_base_alpha = target_alpha
        self._active_fade_min = fade_min
        self._active_fade_max = fade_max
        self._active_shadow_alpha = shadow_alpha
        self._active_alpha_scale = scale if scale > 0 else 1.0

    def _apply_pen_style_change(self, *, update_cursor: bool = True) -> None:
        self.pen_base_size = clamp_base_size_for_style(self.pen_style, float(self.pen_base_size))
        config = get_pen_style_config(self.pen_style)
        self.pen_size = max(1, int(round(self._effective_brush_width())))
        self._brush_composition_mode = config.composition_mode
        base_width = self._effective_brush_width()
        self._refresh_pen_alpha_state()
        self._update_brush_pen_appearance(base_width, self._active_fade_max)
        if self._brush_painter is not None:
            self._brush_painter.setCompositionMode(self._brush_composition_mode)
        self.last_width = max(1.0, base_width * config.target_min_factor)
        self._stroke_smoothed_target = float(self.last_width)
        self._stroke_width_velocity = 0.0
        self._stroke_target_width = float(self.last_width)
        if update_cursor:
            self.update_cursor()
        self._update_pen_tooltip()

    def _update_pen_tooltip(self) -> None:
        toolbar = getattr(self, "toolbar", None)
        if toolbar is None:
            return
        effective = int(round(self._effective_brush_width()))
        toolbar.update_pen_tooltip(
            self.pen_style,
            float(self.pen_base_size),
            effective,
            opacity_percent=self._get_active_opacity_percent(),
        )

    def _get_active_opacity_percent(self) -> Optional[int]:
        percent = int(round(clamp(self._brush_opacity, 0, 255) * 100 / 255))
        return int(clamp(percent, 0, 100))

    def _apply_whiteboard_lock(self) -> None:
        toolbar = getattr(self, "toolbar", None)
        if toolbar is not None:
            toolbar.set_whiteboard_locked(self.whiteboard_active)
        if self.whiteboard_active:
            if self._mode_before_whiteboard is None:
                self._mode_before_whiteboard = getattr(self, "mode", "brush")
            self._cancel_region_selection()
            self._navigation_reasons.clear()
            self._active_navigation_keys.clear()
            self.navigation_active = False
            self.update_cursor()
            self._raise_roll_call_window()
            return
        restore_mode = self._mode_before_whiteboard
        self._mode_before_whiteboard = None
        if restore_mode and restore_mode != getattr(self, "mode", restore_mode):
            self.set_mode(restore_mode)
        self.update_cursor()

    def _raise_roll_call_window(self, *, activate: bool = True) -> None:
        """Keep the roll-call/timer window visible when the whiteboard is active."""

        try:
            window_cls = globals().get("RollCallTimerWindow")
        except Exception:
            window_cls = None
        if window_cls is None:
            return
        try:
            for widget in QApplication.topLevelWidgets():
                if isinstance(widget, window_cls) and widget.isVisible():
                    try:
                        if widget.windowState() & Qt.WindowState.WindowMinimized:
                            widget.showNormal()
                    except Exception:
                        pass
                    try:
                        widget.raise_()
                        if activate:
                            widget.activateWindow()
                    except Exception:
                        try:
                            widget.show()
                            widget.raise_()
                        except Exception:
                            pass
        except Exception:
            pass

    def show_overlay(self) -> None:
        allow_focus = self._should_capture_keyboard()
        self._apply_focus_acceptance(allow_focus)
        self.show()
        self.raise_()
        if allow_focus:
            self.activateWindow()
        else:
            self._force_wps_foreground_for_raw_input()
        self.toolbar.show()
        self.raise_toolbar()
        self._toolbar_hovering = bool(self.toolbar.underMouse())
        if self._toolbar_hovering:
            self.handle_toolbar_enter()
        self.set_mode(self.mode, self.current_shape)

    def hide_overlay(self) -> None:
        self._release_keyboard_capture()
        hook = getattr(self, "_wps_nav_hook", None)
        if hook is not None:
            try:
                hook.stop()
            except Exception:
                pass
        self._wps_nav_hook_active = False
        self.hide()
        self.toolbar.hide()
        self._toolbar_hovering = False
        self.save_settings()
        self.save_window_position()

    def open_pen_settings(self) -> None:
        pm, ps = self.mode, self.current_shape
        dialog = PenSettingsDialog(
            self.toolbar,
            self.pen_base_size,
            self.pen_color.name(),
            initial_opacity=self._brush_opacity,
            initial_eraser_size=self.eraser_size,
            initial_ui_scale=self.ui_scale,
            initial_mode=self.mode,
            initial_shape_type=self.current_shape or getattr(self.paint_config, "shape_type", None),
            initial_wps_input_mode=getattr(self, "_wps_input_mode", "auto"),
            initial_wps_raw_input=bool(getattr(self, "_wps_raw_input_mode", False)),
            initial_wps_wheel_forward=bool(getattr(self, "_wps_wheel_forward", False)),
        )
        accepted = bool(dialog.exec())
        desired_mode, desired_shape = pm, ps
        if accepted:
            (
                base_size,
                color,
                opacity_alpha,
                eraser_size,
                ui_scale,
                shape_choice,
                wps_input_mode,
                wps_wheel_forward,
            ) = dialog.get_settings()
            self.pen_base_size = float(base_size)
            self.pen_style = _DEFAULT_PEN_STYLE
            self._brush_opacity = int(clamp(opacity_alpha, 0, 255))
            self.pen_color = QColor(color)
            self.eraser_size = float(clamp(eraser_size, 1.0, 50.0))
            self.ui_scale = float(clamp(ui_scale, 0.8, 2.0))
            self._apply_pen_style_change()
            try:
                self.toolbar.apply_ui_scale(self.ui_scale)
            except Exception:
                pass
            normalized_wps_mode = _normalize_wps_input_mode(wps_input_mode)
            self._wps_input_mode = normalized_wps_mode
            self._wps_wheel_forward = bool(wps_wheel_forward)
            self._wps_raw_input_mode = normalized_wps_mode == "raw"
            self._wps_auto_force_message = False
            self.paint_config.wps_input_mode = normalized_wps_mode
            self.paint_config.wps_raw_input = bool(self._wps_raw_input_mode)
            self.paint_config.wps_wheel_forward = bool(self._wps_wheel_forward)
            if shape_choice:
                self.current_shape = shape_choice
                self.paint_config.shape_type = shape_choice
                desired_mode, desired_shape = "shape", shape_choice
            else:
                if pm in {"shape", "eraser", "region_erase"}:
                    desired_mode = "brush"
                desired_shape = None
            try:
                self._ensure_keyboard_capture()
            except Exception:
                pass
            try:
                self._update_wps_nav_hook_state()
            except Exception:
                pass
            self.save_settings()
        self.set_mode(desired_mode, desired_shape)
        self.raise_toolbar()

    def open_board_color_dialog(self) -> None:
        d = BoardColorDialog(self.toolbar)
        if d.exec():
            c = d.get_color()
            if c:
                self.last_board_color = c
                self.whiteboard_color = c
                self.whiteboard_active = True
                self._apply_whiteboard_lock()
                if self._forwarder:
                    self._forwarder.clear_cached_target()
                self.toolbar.update_whiteboard_button_state(True)
                self._update_visibility_for_mode(initial=False)
                self.raise_toolbar()
                self.update()
                self._raise_roll_call_window()
                self.save_settings()

    def toggle_whiteboard(self) -> None:
        was_active = self.whiteboard_active
        self.whiteboard_active = not self.whiteboard_active
        self.whiteboard_color = self.last_board_color if self.whiteboard_active else QColor(0, 0, 0, 0)
        self._apply_whiteboard_lock()
        if self._forwarder and self.whiteboard_active:
            self._forwarder.clear_cached_target()
        self._update_visibility_for_mode(initial=False)
        self.raise_toolbar()
        self.toolbar.update_whiteboard_button_state(self.whiteboard_active)
        self.update()
        if self.whiteboard_active and not was_active:
            self._raise_roll_call_window()

    def set_mode(self, mode: str, shape_type: Optional[str] = None, *, initial: bool = False) -> None:
        prev_mode = getattr(self, "mode", None)
        if prev_mode != mode:
            self._release_canvas_painters()
            if prev_mode == "region_erase":
                self._cancel_region_selection()
        if mode != "cursor":
            self._cancel_navigation_cursor_hold()
            self._set_navigation_reason("cursor-button", False)
        else:
            self._set_navigation_reason("cursor-button", False)
        focus_on_cursor = bool(self._forwarder) and mode == "cursor" and not initial
        pending_focus_target: Optional[int] = None
        self.mode = mode
        if not self._restoring_tool:
            self._pending_tool_restore = None
        if shape_type is not None or mode != "shape":
            self.current_shape = shape_type
        if mode != "shape":
            self.shape_start_point = None
            self._last_preview_bounds = None
        if mode != "region_erase":
            self._region_previewing = False
            self._region_select_start = None
            self._region_preview_bounds = None
        else:
            self._navigation_reasons.clear()
            self.navigation_active = False
            self._nav_pointer_button = Qt.MouseButton.NoButton
        if self.mode != "eraser":
            self._eraser_last_point = None
        if self._forwarder and mode == "cursor":
            self._forwarder.clear_cached_target()
            if focus_on_cursor:
                try:
                    pending_focus_target = self._forwarder.get_presentation_target()
                except Exception:
                    pending_focus_target = None
                if pending_focus_target and not self._presentation_control_allowed(pending_focus_target):
                    focus_on_cursor = False
                    self._forwarder.clear_cached_target()
        if self.mode in {"brush", "shape"} and not self._restoring_tool:
            self._update_last_tool_snapshot()
        self._update_visibility_for_mode(initial=initial)
        if focus_on_cursor and self._forwarder:
            try:
                if pending_focus_target and self._is_wps_slideshow_target(pending_focus_target):
                    self._forwarder.force_focus_presentation_window()
                else:
                    self._forwarder.focus_presentation_window()
            except Exception:
                pass
        if not initial:
            self.raise_toolbar()
        self.update_toolbar_state()
        self.update_cursor()

    def update_toolbar_state(self) -> None:
        if not getattr(self, 'toolbar', None):
            return
        self.toolbar.update_tool_states(self.mode, self.pen_color)

    def _update_last_tool_snapshot(self) -> None:
        if self.pen_color.isValid():
            self._last_brush_color = QColor(self.pen_color)
        self._last_pen_style = self.pen_style
        if self.pen_base_size > 0:
            self._last_pen_base_size = float(self.pen_base_size)
        if self.mode in {"brush", "shape"}:
            self._last_draw_mode = self.mode
            if self.mode == "shape":
                self._last_shape_type = self.current_shape

    def _restore_last_tool(self, preferred_mode: Optional[str] = None, *, shape_type: Optional[str] = None) -> None:
        self._pending_tool_restore = None
        if isinstance(self._last_brush_color, QColor) and self._last_brush_color.isValid():
            self.pen_color = QColor(self._last_brush_color)
        if isinstance(self._last_pen_style, PenStyle):
            self.pen_style = self._last_pen_style
        if isinstance(self._last_pen_base_size, (int, float)) and self._last_pen_base_size > 0:
            self.pen_base_size = float(self._last_pen_base_size)
        else:
            self.pen_base_size = clamp_base_size_for_style(self.pen_style, self.pen_base_size)
        self._apply_pen_style_change()
        target_mode = preferred_mode
        target_shape: Optional[str] = None
        if target_mode == "shape":
            target_shape = shape_type or self._last_shape_type or self.current_shape
        if target_mode not in {"brush", "shape", "eraser"}:
            if self._last_draw_mode == "shape":
                target_mode = "shape"
                target_shape = shape_type or self._last_shape_type or self.current_shape
            else:
                target_mode = "brush"
        self._restoring_tool = True
        try:
            self.set_mode(target_mode, shape_type=target_shape)
        finally:
            self._restoring_tool = False
        if target_mode in {"brush", "shape"}:
            self._update_last_tool_snapshot()

    def toggle_eraser_mode(self) -> None:
        """切换橡皮模式；再次点击会恢复上一次的画笔配置。"""
        if self.mode == "eraser":
            self._restore_last_tool()
        else:
            self._update_last_tool_snapshot()
            self.set_mode("eraser")

    def toggle_cursor_mode(self) -> None:
        """切换光标模式；再次点击恢复最近的画笔或图形设置。"""
        if self.mode == "cursor":
            self._restore_last_tool()
            self._set_navigation_reason("cursor-button", True)
            return
        self._set_navigation_reason("cursor-button", False)
        self._update_last_tool_snapshot()
        self.set_mode("cursor")

    def toggle_region_erase_mode(self) -> None:
        """框选删除模式；再次点击恢复最近的画笔或图形设置。"""
        if self.mode == "region_erase":
            self._restore_last_tool()
            return
        self._update_last_tool_snapshot()
        self.set_mode("region_erase")

    def go_to_next_slide(
        self,
        *,
        via_toolbar: bool = False,
        originating_key: Optional[int] = None,
        from_keyboard: bool = False,
    ) -> None:
        if self.whiteboard_active:
            return
        self._send_slide_virtual_key(
            VK_DOWN,
            via_toolbar=via_toolbar,
            originating_key=originating_key,
            from_keyboard=from_keyboard,
        )

    def go_to_previous_slide(
        self,
        *,
        via_toolbar: bool = False,
        originating_key: Optional[int] = None,
        from_keyboard: bool = False,
    ) -> None:
        if self.whiteboard_active:
            return
        self._send_slide_virtual_key(
            VK_UP,
            via_toolbar=via_toolbar,
            originating_key=originating_key,
            from_keyboard=from_keyboard,
        )

    def _wheel_delta_for_vk(self, vk_code: int) -> int:
        if vk_code in (VK_DOWN, VK_RIGHT):
            return -120
        if vk_code in (VK_UP, VK_LEFT):
            return 120
        return 0

    def _normalize_wps_nav_code(self, code: int) -> int:
        """将不同输入来源统一成方向编码，便于对同向的重复事件去重。"""

        forward_codes = {VK_RIGHT, VK_DOWN, VK_NEXT, 1}
        backward_codes = {VK_LEFT, VK_UP, VK_PRIOR, -1}
        if code in forward_codes:
            return 1
        if code in backward_codes:
            return -1
        return code


    def _should_suppress_wps_nav(self, code: int, target: Optional[int]) -> bool:
        if not target or not self._is_wps_slideshow_target(target):
            return False
        if self._wps_nav_block_until and time.monotonic() < self._wps_nav_block_until:
            return True
        normalized = self._normalize_wps_nav_code(code)
        prev = self._last_wps_nav_event
        if not prev:
            return False
        prev_code, prev_target, prev_ts = prev
        if prev_code != normalized or prev_target != target:
            return False
        return (time.monotonic() - prev_ts) * 1000 < self._WPS_NAV_DEBOUNCE_MS


    def _remember_wps_nav(self, code: int, target: Optional[int]) -> None:
        if target and self._is_wps_slideshow_target(target):
            normalized = self._normalize_wps_nav_code(code)
            self._last_wps_nav_event = (normalized, target, time.monotonic())
            self._wps_nav_block_until = time.monotonic() + (self._WPS_NAV_DEBOUNCE_MS / 1000.0)

    def _mark_wps_hook_input(self) -> None:
        self._last_wps_hook_input_ts = time.monotonic()

    def _wps_hook_recently_fired(self) -> bool:
        ts = float(getattr(self, "_last_wps_hook_input_ts", 0.0) or 0.0)
        if ts <= 0:
            return False
        return (time.monotonic() - ts) * 1000 < self._WPS_NAV_DEBOUNCE_MS


    def _handle_wps_nav_hook_request(self, direction: int, source: str) -> None:
        if not getattr(self, "control_wps_ppt", True):
            return
        if getattr(self, "whiteboard_active", False):
            return
        if getattr(self, "mode", None) == "cursor":
            return
        if not direction:
            return
        try:
            target = self._resolve_wps_nav_target()
        except Exception:
            target = None
        if not target:
            return
        send_mode = self._effective_wps_send_mode(target)
        effective_wheel_forward = self._effective_wps_wheel_forward(target)
        passthrough = self._is_wps_raw_input_passthrough(target)
        intercept_source = (
            bool(getattr(self, "_wps_hook_intercept_wheel", True))
            if source == "wheel"
            else bool(getattr(self, "_wps_hook_intercept_keyboard", True))
        )
        if passthrough and not intercept_source:
            self._log_nav_trace(
                "wps_hook_skip_send",
                reason="foreground_passthrough",
                dir=direction,
                source=source,
                target=hex(target),
            )
            return
        if passthrough and intercept_source and source == "wheel" and effective_wheel_forward:
            self._log_nav_trace(
                "wps_hook_passthrough_override",
                reason="foreground_passthrough",
                dir=direction,
                source=source,
                target=hex(target),
            )
        if self._should_suppress_wps_nav(direction, target):
            self._log_nav_trace(
                "wps_hook_suppressed",
                dir=direction,
                source=source,
                target=hex(target),
            )
            return
        vk_code = VK_NEXT if direction > 0 else VK_PRIOR
        self._set_navigation_reason(source or "hook", True)
        try:
            if send_mode == "raw":
                self._log_nav_trace(
                    "wps_hook_send",
                    dir=direction,
                    source=f"{source}_input_key",
                    vk=vk_code,
                    target=hex(target),
                )
                sent = self._send_wps_slideshow_input_key(target, vk_code)
                if not sent:
                    if self._wps_auto_input_enabled() and not getattr(self, "_wps_auto_force_message", False):
                        self._wps_auto_force_message = True
                        self._log_nav_trace(
                            "wps_auto_fallback",
                            reason="send_input_failed",
                            target=hex(target),
                        )
                    self._log_nav_trace(
                        "wps_hook_send",
                        dir=direction,
                        source=f"{source}_vk_fallback",
                        vk=vk_code,
                        target=hex(target),
                    )
                    self._send_wps_slideshow_virtual_key(target, vk_code)
            else:
                self._log_nav_trace(
                    "wps_hook_send",
                    dir=direction,
                    source=source,
                    vk=vk_code,
                    target=hex(target),
                )
                self._send_wps_slideshow_virtual_key(target, vk_code)
        finally:
            self._set_navigation_reason(source or "hook", False)

    def _update_wps_nav_hook_state(self) -> None:
        # WPS 放映：仅在画笔等非穿透模式启用全局拦截钩子，避免与前台输入叠加。
        hook = getattr(self, "_wps_nav_hook", None)
        if hook is None:
            self._wps_nav_hook_active = False
            return
        should_enable = (
            getattr(self, "control_wps_ppt", True)
            and not getattr(self, "whiteboard_active", False)
            and getattr(self, "mode", None) != "cursor"
            and self.isVisible()
        )
        block_only = False
        intercept_keyboard = True
        intercept_wheel = True
        wps_target: Optional[int] = None
        if should_enable:
            try:
                wps_target = self._resolve_wps_nav_target()
                should_enable = bool(wps_target)
            except Exception:
                should_enable = False
        emit_wheel_on_block = True
        send_mode = "message"
        effective_wheel_forward = False
        if should_enable:
            send_mode = self._effective_wps_send_mode(wps_target)
            effective_wheel_forward = self._effective_wps_wheel_forward(wps_target)
            intercept_wheel = bool(effective_wheel_forward)
            emit_wheel_on_block = bool(effective_wheel_forward)
        if should_enable and send_mode == "raw":
            block_only = True
            if wps_target and self._wps_target_has_foreground_focus(wps_target):
                intercept_keyboard = False
                if not effective_wheel_forward:
                    intercept_wheel = False
                    block_only = False
                    emit_wheel_on_block = False
        if should_enable:
            try:
                hook.set_intercept_enabled(True)
                hook.set_block_only(block_only)
                hook.set_intercept_keyboard(intercept_keyboard)
                hook.set_intercept_wheel(intercept_wheel)
                hook.set_emit_wheel_on_block(emit_wheel_on_block)
            except Exception:
                pass
            self._wps_hook_intercept_keyboard = bool(intercept_keyboard)
            self._wps_hook_intercept_wheel = bool(intercept_wheel)
            started = self._wps_nav_hook_active
            if not started:
                try:
                    started = hook.start()
                except Exception:
                    started = False
            if not started:
                try:
                    hook.stop()
                except Exception:
                    pass
            self._wps_nav_hook_active = bool(started)
            self._log_nav_trace(
                "wps_hook_state",
                active=self._wps_nav_hook_active,
                mode=getattr(self, "mode", ""),
                block_only=block_only,
                intercept_keyboard=intercept_keyboard,
                intercept_wheel=intercept_wheel,
                emit_wheel=emit_wheel_on_block,
            )
            return
        try:
            hook.set_intercept_enabled(False)
            hook.set_block_only(False)
            hook.set_intercept_keyboard(True)
            hook.set_intercept_wheel(True)
            hook.set_emit_wheel_on_block(True)
        except Exception:
            pass
        try:
            hook.stop()
        except Exception:
            pass
        self._wps_nav_hook_active = False
        self._wps_hook_intercept_keyboard = True
        self._wps_hook_intercept_wheel = True
        self._log_nav_trace(
            "wps_hook_state",
            active=False,
            mode=getattr(self, "mode", ""),
        )

    def _is_presentation_category_allowed(self, category: str) -> bool:
        if not category or category == "other":
            return True
        flags = getattr(self, "_presentation_control_flags", None)
        if isinstance(flags, Mapping) and category in flags:
            return bool(flags[category])
        attr_map = {
            "ms_ppt": "control_ms_ppt",
            "wps_ppt": "control_wps_ppt",
        }
        attr = attr_map.get(category)
        if attr is not None and hasattr(self, attr):
            return bool(getattr(self, attr))
        return True

    def _process_control_disallowed(self, hwnd: Optional[int]) -> bool:
        if not hwnd:
            return False
        process_name = self._window_process_name(_user32_top_level_hwnd(hwnd) or hwnd)
        if not process_name:
            category = self._presentation_target_category(hwnd)
            if category == "wps_ppt":
                return not getattr(self, "control_wps_ppt", True)
            if category == "ms_ppt":
                return not getattr(self, "control_ms_ppt", True)
            return False
        name = process_name.lower()
        if self._is_wps_presentation_process_name(name):
            return not getattr(self, "control_wps_ppt", True)
        if name.startswith("wps"):
            # 禁用 WPS 文档滚动控制（仅保留演示/放映相关控制）。
            try:
                class_name = self._presentation_window_class(hwnd)
                top_hwnd = _user32_top_level_hwnd(hwnd)
                top_class = self._presentation_window_class(top_hwnd) if top_hwnd else ""
            except Exception:
                class_name = ""
                top_class = ""
            if self._is_wps_presentation_process(name, class_name, top_class):
                return not getattr(self, "control_wps_ppt", True)
            return True
        if "powerpnt" in name:
            return not getattr(self, "control_ms_ppt", True)
        if "winword" in name:
            # 禁用 Word 滚动控制（仅保留演示/放映相关控制）。
            return True
        return False

    def _presentation_control_allowed(self, hwnd: Optional[int], *, log: bool = True) -> bool:
        category = self._presentation_target_category(hwnd)
        allowed = self._is_presentation_category_allowed(category)
        if allowed and self._process_control_disallowed(hwnd):
            allowed = False
        if not allowed and log:
            self._log_navigation_debug(
                "control_disabled",
                target=hex(hwnd) if hwnd else "0x0",
                category=category,
            )
        return allowed

    def _get_cached_slideshow_target(
        self,
        kind: str,
        *,
        require_allowed: bool,
        is_target: Callable[[int], bool],
    ) -> Optional[int]:
        """短时间缓存放映窗口句柄，减少频繁枚举导致的抖动/误判。"""

        now = time.monotonic()
        cached_target = getattr(self, f"_cached_{kind}_slideshow_target", None)
        cached_ts = float(getattr(self, f"_cached_{kind}_slideshow_ts", 0.0) or 0.0)
        if not cached_target:
            return None
        if (now - cached_ts) * 1000 >= self._SLIDESHOW_TARGET_CACHE_MS:
            return None
        hwnd = int(cached_target)
        if not self._is_target_window_valid(hwnd):
            return None
        if not is_target(hwnd):
            return None
        if require_allowed and not self._presentation_control_allowed(hwnd, log=False):
            return None
        return hwnd

    def _set_cached_slideshow_target(self, kind: str, hwnd: int, ts: float) -> None:
        setattr(self, f"_cached_{kind}_slideshow_target", int(hwnd))
        setattr(self, f"_cached_{kind}_slideshow_ts", float(ts))

    def _find_wps_slideshow_target(self, *, require_allowed: bool = True) -> Optional[int]:
        cached = self._get_cached_slideshow_target(
            "wps",
            require_allowed=require_allowed,
            is_target=self._is_wps_slideshow_target,
        )
        if cached:
            return cached

        if require_allowed and not getattr(self, "control_wps_ppt", True):
            return None
        candidates: List[int] = []
        sources: List[Callable[[], Optional[int]]] = []
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            sources.append(forwarder.get_presentation_target)
            detector = getattr(forwarder, "_detect_presentation_window", None)
            if callable(detector):
                sources.append(detector)  # type: ignore[arg-type]
        sources.append(self._resolve_presentation_target)
        sources.append(self._fallback_detect_presentation_window_user32)
        if _USER32 is not None:
            sources.append(lambda: _user32_get_foreground_window())
        for getter in sources:
            if not callable(getter):
                continue
            try:
                hwnd = getter()
            except Exception:
                hwnd = None
            if not hwnd:
                continue
            normalized = self._normalize_presentation_target(hwnd)
            for candidate in (normalized, hwnd):
                if not candidate or candidate in candidates:
                    continue
                candidates.append(candidate)
                allowed = self._presentation_control_allowed(candidate, log=False)
                if not allowed and require_allowed:
                    continue
                if self._is_wps_slideshow_target(candidate):
                    if allowed:
                        if forwarder is not None:
                            try:
                                forwarder._last_target_hwnd = candidate  # type: ignore[attr-defined]
                            except Exception:
                                pass
                        try:
                            self._last_target_hwnd = candidate
                        except Exception:
                            pass
                        self._set_cached_slideshow_target("wps", candidate, time.monotonic())
                        return candidate
                    if not require_allowed:
                        self._set_cached_slideshow_target("wps", candidate, time.monotonic())
                        return candidate
        return None

    def _resolve_wps_nav_target(self, *, require_allowed: bool = True) -> Optional[int]:
        """在强制WPS模式下，允许使用当前控制目标作为WPS导航目标。"""

        target = self._find_wps_slideshow_target(require_allowed=require_allowed)
        if target:
            return target
        mode = _normalize_wps_input_mode(getattr(self, "_wps_input_mode", "auto"))
        if mode == "manual":
            mode = "raw" if bool(getattr(self, "_wps_raw_input_mode", False)) else "message"
        if mode not in {"raw", "message"}:
            return None
        fallback = self._resolve_control_target()
        if not fallback:
            return None
        if require_allowed and not self._presentation_control_allowed(fallback, log=False):
            return None
        if self._presentation_target_category(fallback) == "ms_ppt":
            return None
        return fallback

    def _find_ms_slideshow_target(self, *, require_allowed: bool = True) -> Optional[int]:
        cached = self._get_cached_slideshow_target(
            "ms",
            require_allowed=require_allowed,
            is_target=self._is_ms_slideshow_target,
        )
        if cached:
            return cached

        if require_allowed and not getattr(self, "control_ms_ppt", True):
            return None
        candidates: List[int] = []
        sources: List[Callable[[], Optional[int]]] = []
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            sources.append(forwarder.get_presentation_target)
            detector = getattr(forwarder, "_detect_presentation_window", None)
            if callable(detector):
                sources.append(detector)  # type: ignore[arg-type]
        sources.append(self._resolve_presentation_target)
        sources.append(self._fallback_detect_presentation_window_user32)
        if _USER32 is not None:
            sources.append(lambda: _user32_get_foreground_window())
        for getter in sources:
            if not callable(getter):
                continue
            try:
                hwnd = getter()
            except Exception:
                hwnd = None
            if not hwnd:
                continue
            normalized = self._normalize_presentation_target(hwnd)
            for candidate in (normalized, hwnd):
                if not candidate or candidate in candidates:
                    continue
                candidates.append(candidate)
                allowed = self._presentation_control_allowed(candidate, log=False)
                if not allowed and require_allowed:
                    continue
                if self._is_ms_slideshow_target(candidate):
                    if allowed:
                        if forwarder is not None:
                            try:
                                forwarder._last_target_hwnd = candidate  # type: ignore[attr-defined]
                            except Exception:
                                pass
                        try:
                            self._last_target_hwnd = candidate
                        except Exception:
                            pass
                        self._set_cached_slideshow_target("ms", candidate, time.monotonic())
                        return candidate
                    if not require_allowed:
                        self._set_cached_slideshow_target("ms", candidate, time.monotonic())
                        return candidate
        return None

    def _cancel_wps_slideshow_binding_retry(self) -> None:
        self._wps_binding_retry_attempts = 0
        timer = getattr(self, "_wps_binding_retry_timer", None)
        if timer is not None and timer.isActive():
            timer.stop()

    def _schedule_wps_slideshow_binding_retry(self, delay_ms: int = 200) -> None:
        if not getattr(self, "control_wps_ppt", True):
            self._cancel_wps_slideshow_binding_retry()
            return
        attempts = getattr(self, "_wps_binding_retry_attempts", 0)
        if attempts >= 3:
            return
        self._wps_binding_retry_attempts = attempts + 1
        timer = getattr(self, "_wps_binding_retry_timer", None)
        if timer is None:
            timer = QTimer(self)
            timer.setSingleShot(True)
            timer.timeout.connect(self._refresh_wps_slideshow_binding)
            self._wps_binding_retry_timer = timer
        timer.start(max(0, int(delay_ms)))

    def _refresh_wps_slideshow_binding(self) -> None:
        if not getattr(self, "control_wps_ppt", True):
            self._cancel_wps_slideshow_binding_retry()
            return
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None and not getattr(self, "_wps_binding_retry_attempts", 0):
            try:
                forwarder.clear_cached_target()
            except Exception:
                pass
        try:
            self._last_target_hwnd = None
        except Exception:
            pass
        candidate: Optional[int] = None
        try:
            candidate = self._find_wps_slideshow_target()
        except Exception:
            candidate = None
        if not candidate:
            try:
                fallback = self._find_wps_slideshow_target(require_allowed=False)
            except Exception:
                fallback = None
            if fallback and self._presentation_control_allowed(fallback, log=False):
                candidate = fallback
        if not candidate:
            self._schedule_wps_slideshow_binding_retry()
            return
        self._cancel_wps_slideshow_binding_retry()
        try:
            self._last_target_hwnd = candidate
        except Exception:
            pass
        if forwarder is not None:
            try:
                forwarder._last_target_hwnd = candidate  # type: ignore[attr-defined]
            except Exception:
                pass
            try:
                forwarder.focus_presentation_window()
            except Exception:
                pass
        try:
            self._update_wps_nav_hook_state()
        except Exception:
            pass

    def _maybe_pulse_cursor_for_wps_control(self) -> None:
        if self.whiteboard_active:
            return
        if self.mode == "cursor":
            self._ensure_keyboard_capture()
            return
        if self._pending_wps_cursor_pulse:
            return
        restore_mode = self.mode
        restore_shape = self.current_shape if self.mode == "shape" else None
        if restore_mode not in {"brush", "shape", "eraser"}:
            return
        self._pending_wps_cursor_pulse = True

        def _apply_pulse() -> None:
            self._pending_wps_cursor_pulse = False
            self._apply_navigation_cursor_hold(restore_mode, restore_shape)

        QTimer.singleShot(50, _apply_pulse)

    def _reset_wps_presentation_state(self, *, trigger_cursor: bool = True) -> None:
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            try:
                forwarder.clear_cached_target()
            except Exception:
                pass
        try:
            self._last_target_hwnd = None
        except Exception:
            pass
        self._wps_auto_force_message = False
        self._pending_wps_cursor_pulse = False
        if not trigger_cursor:
            return
        mode = getattr(self, "mode", None)
        if not mode:
            return
        if mode == "cursor":
            try:
                self.set_mode("cursor")
            except Exception:
                pass
            return
        if mode in {"brush", "shape", "eraser"}:
            self._maybe_pulse_cursor_for_wps_control()

    def _schedule_wps_cursor_reactivation(self) -> None:
        timer = getattr(self, "_wps_cursor_reset_timer", None)
        if timer is None:
            timer = QTimer(self)
            timer.setSingleShot(True)
            timer.timeout.connect(self._apply_wps_cursor_reactivation)
            self._wps_cursor_reset_timer = timer
        else:
            if timer.isActive():
                timer.stop()
        timer.start(0)

    def _apply_wps_cursor_reactivation(self) -> None:
        self._reset_wps_presentation_state()

    def _apply_wps_control_enabled_refresh(self) -> None:
        """在启用“控制WPS演示放映”后刷新绑定与输入状态。

        注意：该方法可能会在设置对话框关闭后被延迟调用，避免被 set_mode(pm, ps) 覆盖。
        """

        if not getattr(self, "control_wps_ppt", True):
            return
        try:
            self._refresh_wps_slideshow_binding()
        except Exception:
            pass
        try:
            self._reset_wps_presentation_state(trigger_cursor=False)
        except Exception:
            pass
        try:
            self._update_wps_nav_hook_state()
        except Exception:
            pass
        if getattr(self, "whiteboard_active", False):
            return
        if getattr(self, "mode", None) == "cursor":
            forwarder = getattr(self, "_forwarder", None)
            if forwarder is not None:
                try:
                    forwarder.force_focus_presentation_window()
                except Exception:
                    pass
            return
        try:
            self._ensure_keyboard_capture()
        except Exception:
            pass

    def _auto_activate_cursor_for_wps(self) -> None:
        if getattr(self, "whiteboard_active", False):
            return
        if getattr(self, "mode", None) == "cursor":
            return
        try:
            self.toggle_cursor_mode()
        except Exception:
            try:
                self.set_mode("cursor")
            except Exception:
                pass

    def _resolve_control_target(self) -> Optional[int]:
        # 若当前存在 WPS 放映窗口，优先锁定到放映窗口，避免文档窗口抢占目标导致退出放映或失效。
        if getattr(self, "control_wps_ppt", True):
            try:
                wps_slideshow = self._find_wps_slideshow_target()
            except Exception:
                wps_slideshow = None
            if wps_slideshow:
                return wps_slideshow
        if getattr(self, "control_ms_ppt", True):
            try:
                ms_slideshow = self._find_ms_slideshow_target()
            except Exception:
                ms_slideshow = None
            if ms_slideshow:
                return ms_slideshow
        target = self._current_navigation_target()
        if target and not self._presentation_control_allowed(target, log=False):
            target = None
        if target:
            return target
        if self._forwarder is not None:
            try:
                candidate = self._forwarder.get_presentation_target()
            except Exception:
                candidate = None
            if candidate and not self._presentation_control_allowed(candidate, log=False):
                return None
            return candidate
        fallback = self._fallback_detect_presentation_window_user32()
        if fallback and self._presentation_control_allowed(fallback, log=False):
            return fallback
        return None

    def _is_wps_slideshow_target(self, hwnd: Optional[int] = None) -> bool:
        if hwnd is None:
            hwnd = self._current_navigation_target()
        if not hwnd:
            return False
        class_name = self._presentation_window_class(hwnd)
        if self._is_wps_slideshow_class(class_name):
            return True
        # 新版WPS有时会复用PowerPoint的全屏窗口类名
        top_hwnd = _user32_top_level_hwnd(hwnd)
        top_class = self._presentation_window_class(top_hwnd) if top_hwnd else ""
        process_name = self._window_process_name(top_hwnd or hwnd)
        if class_name == "screenclass":
            # 优先使用类名签名判断，避免某些 WPS 集成版复用 wps.exe 导致进程名无法区分。
            if top_class and self._class_has_wps_presentation_signature(top_class):
                return True
            if process_name and self._is_wps_presentation_process(process_name, class_name, top_class):
                return True
            if self._is_ambiguous_screenclass(class_name, process_name):
                return True
        if class_name in self._SLIDESHOW_PRIORITY_CLASSES or class_name in self._SLIDESHOW_SECONDARY_CLASSES:
            if self._is_wps_presentation_process(process_name, class_name, top_class):
                return True
        if self._should_treat_wps_slideshow(class_name, process_name):
            return True
        if self._is_probable_wps_slideshow_window(hwnd, class_name, process_name):
            return True
        return False

    def _is_ms_slideshow_target(self, hwnd: Optional[int] = None) -> bool:
        if hwnd is None:
            hwnd = self._current_navigation_target()
        if not hwnd:
            return False
        if self._is_wps_slideshow_target(hwnd):
            return False
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            checker = getattr(forwarder, "_is_ms_slideshow_window", None)
            if callable(checker):
                try:
                    if checker(hwnd):
                        return True
                except Exception:
                    pass
        class_name = self._presentation_window_class(hwnd)
        if self._class_has_wps_presentation_signature(class_name):
            return False
        if self._class_has_ms_presentation_signature(class_name):
            return True
        top_hwnd = _user32_top_level_hwnd(hwnd)
        process_name = self._window_process_name(top_hwnd or hwnd)
        if process_name and ("powerpnt" in process_name or process_name.startswith("pptview")):
            return True
        return False

    def _wps_target_has_foreground_focus(self, hwnd: Optional[int]) -> bool:
        """Return True if the WPS放映窗口已经位于前台，可直接接收用户输入。"""

        if not hwnd or _USER32 is None:
            return False
        if not self._is_wps_slideshow_target(hwnd):
            return False
        return self._target_has_foreground_focus(hwnd)

    def _target_has_foreground_focus(self, hwnd: Optional[int]) -> bool:
        if not hwnd or _USER32 is None:
            return False
        try:
            target_top = _user32_top_level_hwnd(hwnd) or hwnd
        except Exception:
            target_top = hwnd
        try:
            foreground = _user32_get_foreground_window()
            foreground_top = _user32_top_level_hwnd(foreground) if foreground else 0
        except Exception:
            return False
        return bool(target_top and foreground_top and target_top == foreground_top)

    def _release_keyboard_navigation_state(self, key: Optional[int] = None) -> None:
        if key is not None:
            self._active_navigation_keys.discard(key)
        if not self._active_navigation_keys:
            self._set_navigation_reason("keyboard", False)

    def _update_navigation_target_cache(self, hwnd: int) -> None:
        try:
            self._last_target_hwnd = hwnd
        except Exception:
            pass
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            try:
                forwarder._last_target_hwnd = hwnd  # type: ignore[attr-defined]
            except Exception:
                pass

    @staticmethod
    def _map_wps_slideshow_vk(vk_code: int) -> int:
        if vk_code in (VK_DOWN, VK_RIGHT, VK_NEXT):
            return VK_NEXT
        if vk_code in (VK_UP, VK_LEFT, VK_PRIOR):
            return VK_PRIOR
        return vk_code

    def _finalize_navigation_input(self, *, originating_key: Optional[int], via_toolbar: bool) -> None:
        if originating_key is not None:
            self._release_keyboard_navigation_state(originating_key)
        if via_toolbar:
            self._cancel_navigation_cursor_hold()

    def _try_send_wps_slideshow_key(
        self,
        wps_target: int,
        vk_code: int,
        *,
        via_toolbar: bool,
        originating_key: Optional[int],
    ) -> bool:
        vk_code = self._map_wps_slideshow_vk(vk_code)
        if self._should_suppress_wps_nav(vk_code, wps_target):
            self._finalize_navigation_input(originating_key=originating_key, via_toolbar=via_toolbar)
            return True
        sent = self._send_wps_slideshow_virtual_key(wps_target, vk_code)
        if not sent:
            return False
        self._remember_wps_nav(vk_code, wps_target)
        self._update_navigation_target_cache(wps_target)
        self._finalize_navigation_input(originating_key=originating_key, via_toolbar=via_toolbar)
        return True

    def _try_send_ms_slideshow_key(
        self,
        ms_target: int,
        vk_code: int,
        *,
        via_toolbar: bool,
        originating_key: Optional[int],
    ) -> bool:
        if not self._send_ms_slideshow_virtual_key(ms_target, vk_code):
            return False
        self._finalize_navigation_input(originating_key=originating_key, via_toolbar=via_toolbar)
        return True

    def _send_slide_virtual_key(
        self,
        vk_code: int,
        *,
        via_toolbar: bool = False,
        originating_key: Optional[int] = None,
        from_keyboard: bool = False,
    ) -> None:
        if vk_code == 0 or self.whiteboard_active:
            return
        wheel_delta = self._wheel_delta_for_vk(vk_code)
        original_mode = getattr(self, "mode", None)
        target_hwnd = self._current_navigation_target()
        effective_target = target_hwnd or self._resolve_control_target()
        if not target_hwnd and effective_target:
            target_hwnd = effective_target
        try:
            wps_override = self._resolve_wps_nav_target()
        except Exception:
            wps_override = None
        ms_override: Optional[int] = None
        if not wps_override:
            try:
                ms_override = self._find_ms_slideshow_target()
            except Exception:
                ms_override = None
        if wps_override:
            target_hwnd = wps_override
            effective_target = wps_override
        elif ms_override:
            target_hwnd = ms_override
            effective_target = ms_override
        target_class = self._presentation_window_class(target_hwnd) if target_hwnd else ""
        wps_slideshow_target = wps_override or (
            effective_target if effective_target and self._is_wps_slideshow_target(effective_target) else None
        )
        wps_foreground = False
        if wps_slideshow_target:
            if self._is_wps_slideshow_target(wps_slideshow_target):
                wps_foreground = self._wps_target_has_foreground_focus(wps_slideshow_target)
            else:
                wps_foreground = self._target_has_foreground_focus(wps_slideshow_target)
        if effective_target and not self._presentation_control_allowed(effective_target):
            self._finalize_navigation_input(originating_key=originating_key, via_toolbar=via_toolbar)
            return
        # WPS 放映：统一只发送一次按下（内部去重），避免按下/抬起各触发一次动画或误判退出
        if wps_slideshow_target:
            hook_blocks = bool(self._wps_nav_hook_active and self._wps_hook_intercept_keyboard)
            if wps_foreground and not via_toolbar and not hook_blocks:
                if originating_key is not None:
                    self._release_keyboard_navigation_state(originating_key)
                return
            if self._try_send_wps_slideshow_key(
                wps_slideshow_target,
                vk_code,
                via_toolbar=via_toolbar,
                originating_key=originating_key,
            ):
                return
        ms_slideshow_target = (
            effective_target if effective_target and self._is_ms_slideshow_target(effective_target) else None
        )
        if ms_slideshow_target and self._try_send_ms_slideshow_key(
            ms_slideshow_target,
            vk_code,
            via_toolbar=via_toolbar,
            originating_key=originating_key,
        ):
            return
        category = (
            self._presentation_target_category(effective_target)
            if effective_target
            else "other"
        )
        top_for_process = _user32_top_level_hwnd(effective_target) if effective_target else 0
        process_name = ""
        if effective_target:
            try:
                process_name = self._window_process_name(top_for_process or effective_target)
            except Exception:
                process_name = ""
        is_ms_ppt_target = (
            category == "ms_ppt"
            or (target_class and self._class_has_ms_presentation_signature(target_class))
            or (process_name and "powerpnt" in process_name)
        )
        persist_hold = is_ms_ppt_target
        if (
            is_ms_ppt_target
            and not self.whiteboard_active
            and original_mode not in {None, "cursor"}
        ):
            self._apply_navigation_cursor_hold(
                original_mode,
                self.current_shape,
                suppress_focus_restore=True,
                persist=persist_hold,
            )
            self._focus_presentation_window_fallback()
            if effective_target:
                self._update_navigation_target_cache(effective_target)
        override_focus_restore = (
            is_ms_ppt_target
            and not self.whiteboard_active
            and original_mode not in {None, "cursor"}
        )
        previous_dispatch_override = getattr(self, "_dispatch_suppress_override", False)
        if override_focus_restore:
            self._dispatch_suppress_override = True
        else:
            self._dispatch_suppress_override = previous_dispatch_override
        try:
            prefer_wheel = (
                (via_toolbar or self.navigation_active or original_mode == "cursor")
                and not is_ms_ppt_target
            )
            base_suppress_focus_restore = bool(wps_slideshow_target) or self._is_wps_slideshow_class(target_class)
            focus_restore_suppressed = base_suppress_focus_restore or override_focus_restore
            release_keyboard = not base_suppress_focus_restore
            success = False
            wheel_used = False
            if wheel_delta and prefer_wheel:
                success = self._send_navigation_wheel(wheel_delta)
                wheel_used = success
                self._log_navigation_debug(
                    "wheel_forward",
                    vk=vk_code,
                    delta=wheel_delta,
                    target=hex(target_hwnd) if target_hwnd else "0x0",
                    cls=target_class or "",
                    category=category,
                    ppt=is_ms_ppt_target,
                    success=success,
                )
            prev_mode = original_mode
            if prev_mode in {"brush", "shape"}:
                self._update_last_tool_snapshot()
            had_keyboard_grab = False
            if not success:
                candidates = (vk_code,)
                with self._temporarily_release_keyboard(
                    release=release_keyboard,
                    restore=not focus_restore_suppressed,
                ) as had_keyboard_grab:
                    for candidate in candidates:
                        if not candidate:
                            continue
                        success = self._dispatch_virtual_key(candidate)
                        if success:
                            current_target = self._current_navigation_target()
                            current_class = (
                                self._presentation_window_class(current_target)
                                if current_target
                                else ""
                            )
                            if self._is_wps_slideshow_class(current_class):
                                base_suppress_focus_restore = True
                                focus_restore_suppressed = True
                            self._log_navigation_debug(
                                "virtual_key_forward",
                                vk=candidate,
                                target=hex(current_target) if current_target else "0x0",
                                cls=current_class or "",
                            )
                            break
            self._pending_tool_restore = None
            if not success:
                if originating_key is not None:
                    self._release_keyboard_navigation_state(originating_key)
                self._log_navigation_debug(
                    "virtual_key_failed",
                    vk=vk_code,
                    target=hex(target_hwnd) if target_hwnd else "0x0",
                    cls=target_class or "",
                )
                if via_toolbar:
                    self._cancel_navigation_cursor_hold()
                return
            if originating_key is not None:
                self._release_keyboard_navigation_state(originating_key)
            if focus_restore_suppressed:
                return
            if (
                not wheel_used
                and not had_keyboard_grab
                and original_mode != "cursor"
                and not is_ms_ppt_target
            ):
                self._ensure_keyboard_capture()
            self.raise_toolbar()
        finally:
            self._dispatch_suppress_override = previous_dispatch_override

    def _send_navigation_wheel(self, delta: int) -> bool:
        if delta == 0 or self.whiteboard_active:
            return False
        self._set_navigation_reason("wheel", True)
        try:
            handled = False
            try:
                wps_target = self._resolve_wps_nav_target()
            except Exception:
                wps_target = None
            target_hwnd = wps_target or self._resolve_control_target()
            if wps_target:
                direction = 1 if delta < 0 else -1
                if self._should_suppress_wps_nav(direction, wps_target):
                    return True
                vk_code = VK_NEXT if direction > 0 else VK_PRIOR
                return self._send_wps_slideshow_virtual_key(wps_target, vk_code)
            suppress_code = -1 if delta < 0 else 1 if delta > 0 else 0
            if wps_target:
                if self._should_suppress_wps_nav(suppress_code or delta, wps_target):
                    return True
                self._wps_nav_block_until = time.monotonic() + (self._WPS_NAV_DEBOUNCE_MS / 1000.0)
            if target_hwnd and not self._presentation_control_allowed(target_hwnd):
                self._log_navigation_debug(
                    "wheel_blocked",
                    delta=delta,
                    target=hex(target_hwnd),
                    category=self._presentation_target_category(target_hwnd),
                )
                return False
            suppress_target = wps_target or target_hwnd
            if suppress_code and self._should_suppress_wps_nav(suppress_code, suppress_target):
                handled = True
                return True
            if self._forwarder is not None:
                try:
                    global_pos = QCursor.pos()
                    local_pos = self.mapFromGlobal(global_pos)
                    wheel_event = QWheelEvent(
                        QPointF(local_pos),
                        QPointF(global_pos),
                        QPoint(),
                        QPoint(0, delta),
                        Qt.MouseButton.NoButton,
                        Qt.KeyboardModifier.NoModifier,
                        Qt.ScrollPhase.ScrollUpdate,
                        False,
                    )
                    handled = self._forwarder.forward_wheel(
                        wheel_event,
                        allow_cursor=(self.mode == "cursor" or self.navigation_active),
                    )
                except Exception:
                    handled = False
            if handled:
                if suppress_code and suppress_target:
                    self._remember_wps_nav(suppress_code, suppress_target)
                return True
            focused = self._focus_presentation_window_fallback()
            fallback = self._fallback_send_wheel(delta)
            if not fallback and not focused:
                return False
            if fallback and suppress_code and suppress_target:
                self._remember_wps_nav(suppress_code, suppress_target)
            return fallback
        finally:
            self._set_navigation_reason("wheel", False)

    def _send_wps_slideshow_wheel(
        self,
        hwnd: int,
        delta: int,
        *,
        global_pos: Optional[QPoint] = None,
    ) -> bool:
        if not hwnd or delta == 0:
            return False
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is None or win32con is None:
            return False
        try:
            keys = 0
            if win32api is not None:
                try:
                    if win32api.GetAsyncKeyState(win32con.VK_SHIFT) & 0x8000:
                        keys |= win32con.MK_SHIFT
                    if win32api.GetAsyncKeyState(win32con.VK_CONTROL) & 0x8000:
                        keys |= win32con.MK_CONTROL
                    if win32api.GetAsyncKeyState(win32con.VK_LBUTTON) & 0x8000:
                        keys |= win32con.MK_LBUTTON
                    if win32api.GetAsyncKeyState(win32con.VK_RBUTTON) & 0x8000:
                        keys |= win32con.MK_RBUTTON
                    if win32api.GetAsyncKeyState(win32con.VK_MBUTTON) & 0x8000:
                        keys |= win32con.MK_MBUTTON
                except Exception:
                    pass
            if global_pos is None:
                global_pos = QCursor.pos()
            delta_word = ctypes.c_short(delta).value & 0xFFFF
            w_param = (ctypes.c_ushort(keys).value & 0xFFFF) | (delta_word << 16)
            x_word = ctypes.c_short(global_pos.x()).value & 0xFFFF
            y_word = ctypes.c_short(global_pos.y()).value & 0xFFFF
            l_param = x_word | (y_word << 16)
            delivered = False
            for child_hwnd, update_cache in forwarder._iter_wheel_targets(hwnd):
                if forwarder._deliver_mouse_wheel(child_hwnd, w_param, l_param):
                    delivered = True
                    if update_cache:
                        self._update_navigation_target_cache(hwnd)
                    break
            if not delivered:
                delivered = forwarder._deliver_mouse_wheel(hwnd, w_param, l_param)
                if delivered:
                    self._update_navigation_target_cache(hwnd)
            return delivered
        except Exception:
            return False

    def _send_wps_slideshow_input_key(self, hwnd: int, vk_code: int) -> bool:
        if not hwnd or vk_code == 0:
            return False
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is None:
            return False
        send_input = getattr(forwarder, "_send_input_event", None)
        if not callable(send_input):
            return False
        try:
            self._force_wps_foreground_for_raw_input()
        except Exception:
            pass
        try:
            press_ok = bool(send_input(vk_code, is_press=True))
        except Exception:
            return False
        if not press_ok:
            self._log_nav_trace("wps_input_send_zero", vk=vk_code, target=hex(hwnd))
            fallback_ok = self._fallback_send_virtual_key(vk_code)
            if not fallback_ok:
                return False
            self._log_nav_trace("wps_input_keybd_event", vk=vk_code, target=hex(hwnd))
        self._remember_wps_nav(vk_code, hwnd)
        self._update_navigation_target_cache(hwnd)
        return True

    def _send_wps_slideshow_virtual_key(self, hwnd: int, vk_code: int) -> bool:
        if not hwnd or vk_code == 0:
            return False
        self._log_nav_trace("wps_send_key", vk=vk_code, target=hex(hwnd))
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is None or win32con is None:
            return False
        try:
            down_param = forwarder._build_basic_key_lparam(vk_code, is_press=True)
        except Exception:
            return False
        try:
            press = forwarder._deliver_key_message(hwnd, win32con.WM_KEYDOWN, vk_code, down_param)
        except Exception:
            return False
        if not press:
            return False
        # 仅发送按下，避免 WPS 对抬起再次触发动画或误判为退出
        self._remember_wps_nav(vk_code, hwnd)
        try:
            forwarder._last_target_hwnd = hwnd
        except Exception:
            pass
        return True

    def _send_ms_slideshow_virtual_key(self, hwnd: int, vk_code: int) -> bool:
        if not hwnd or vk_code == 0:
            return False
        if self._is_wps_slideshow_target(hwnd):
            return self._send_wps_slideshow_virtual_key(hwnd, vk_code)
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is None or win32con is None:
            return False
        self._focus_presentation_window_fallback()
        try:
            down_param = forwarder._build_basic_key_lparam(vk_code, is_press=True)
            up_param = forwarder._build_basic_key_lparam(vk_code, is_press=False)
        except Exception:
            return False
        try:
            press = forwarder._deliver_key_message(hwnd, win32con.WM_KEYDOWN, vk_code, down_param)
            release = forwarder._deliver_key_message(hwnd, win32con.WM_KEYUP, vk_code, up_param)
        except Exception:
            return False
        if press and release:
            try:
                self._last_target_hwnd = hwnd
            except Exception:
                pass
            if forwarder is not None:
                try:
                    forwarder._last_target_hwnd = hwnd  # type: ignore[attr-defined]
                except Exception:
                    pass
            return True
        return False

    def _fallback_send_wheel(self, delta: int) -> bool:
        if delta == 0 or _USER32 is None:
            return False
        try:
            _USER32.mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, 0)
            return True
        except Exception:
            return False

    def _apply_navigation_cursor_hold(
        self,
        restore_mode: str,
        restore_shape: Optional[str],
        *,
        suppress_focus_restore: bool = False,
        persist: bool = False,
    ) -> None:
        if restore_mode not in {"brush", "shape", "eraser"}:
            return
        if persist:
            self._activate_navigation_hold()
        else:
            self._deactivate_navigation_hold(restore=False)
        if suppress_focus_restore:
            self._skip_focus_reactivation = True
        self._nav_restore_mode = (restore_mode, restore_shape)
        if self.mode != "cursor":
            self.set_mode("cursor")
        if self._nav_restore_timer.isActive():
            self._nav_restore_timer.stop()
        if not persist:
            self._nav_restore_timer.start(self._NAVIGATION_RESTORE_DELAY_MS)

    def _cancel_navigation_cursor_hold(self) -> None:
        if self._nav_restore_timer.isActive():
            self._nav_restore_timer.stop()
        self._nav_restore_mode = None
        self._deactivate_navigation_hold(restore=False)
        self._skip_focus_reactivation = False

    def _restore_navigation_tool(self) -> None:
        pending = self._nav_restore_mode
        self._nav_restore_mode = None
        self._nav_hold_persistent = False
        if not pending:
            return
        if self.mode != "cursor":
            return
        mode, shape = pending
        if mode == "eraser":
            self.set_mode("eraser")
        else:
            self._restore_last_tool(mode, shape_type=shape)

    def _activate_navigation_hold(self) -> None:
        self._nav_hold_persistent = True
        if self._nav_hold_timer.isActive():
            self._nav_hold_timer.stop()
        if not self._nav_hold_active:
            self._nav_hold_active = True
            self._set_navigation_reason("auto-hold", True)
        self._nav_hold_timer.start(self._NAVIGATION_HOLD_DURATION_MS)

    def _deactivate_navigation_hold(self, *, restore: bool) -> None:
        if self._nav_hold_timer.isActive():
            self._nav_hold_timer.stop()
        if self._nav_hold_active:
            self._nav_hold_active = False
            self._set_navigation_reason("auto-hold", False)
        self._nav_hold_persistent = False
        if restore and not self.navigation_active and self.mode == "cursor" and self._nav_restore_mode:
            self._restore_navigation_tool()

    def _release_navigation_hold(self) -> None:
        self._deactivate_navigation_hold(restore=True)

    def _dispatch_virtual_key(self, vk_code: int) -> bool:
        if vk_code == 0 or self.whiteboard_active:
            return False
        success = False
        suppress_focus_restore = False
        override_dispatch_suppress = bool(getattr(self, "_dispatch_suppress_override", False))
        wps_override = self._find_wps_slideshow_target()
        if wps_override:
            suppress_focus_restore = True
            forwarder = getattr(self, "_forwarder", None)
            if forwarder is not None:
                try:
                    forwarder._last_target_hwnd = wps_override  # type: ignore[attr-defined]
                except Exception:
                    pass
            if self._should_suppress_wps_nav(vk_code, wps_override):
                return True
        if self._forwarder is not None:
            qt_key_map = {
                VK_UP: Qt.Key.Key_Up,
                VK_DOWN: Qt.Key.Key_Down,
                VK_LEFT: Qt.Key.Key_Left,
                VK_RIGHT: Qt.Key.Key_Right,
            }
            qt_key = qt_key_map.get(vk_code)
            if qt_key is not None:
                press_event = QKeyEvent(QEvent.Type.KeyPress, qt_key, Qt.KeyboardModifier.NoModifier)
                release_event = QKeyEvent(QEvent.Type.KeyRelease, qt_key, Qt.KeyboardModifier.NoModifier)
                press_ok = self._forwarder.forward_key(
                    press_event,
                    is_press=True,
                    allow_cursor=True,
                )
                release_ok = (
                    self._forwarder.forward_key(
                        release_event,
                        is_press=False,
                        allow_cursor=True,
                    )
                    if press_ok
                    else False
                )
                if press_ok and release_ok:
                    success = True
                    current_target = self._forwarder.get_presentation_target()
                    if self._is_wps_slideshow_target(current_target):
                        suppress_focus_restore = True
            if not success:
                target_hwnd = self._forwarder.get_presentation_target()
                focus_ok = False
                if target_hwnd:
                    suppress_focus_restore = self._is_wps_slideshow_target(target_hwnd)
                    if not suppress_focus_restore:
                        try:
                            focus_ok = self._forwarder.focus_presentation_window()
                        except Exception:
                            focus_ok = False
                        if not focus_ok:
                            try:
                                if self._forwarder.bring_target_to_foreground(target_hwnd):
                                    QApplication.processEvents()
                                    time.sleep(0.05)
                                    focus_ok = True
                            except Exception:
                                focus_ok = False
                else:
                    self._forwarder.clear_cached_target()
                success = self._forwarder.send_virtual_key(vk_code)
                if not success:
                    self._forwarder.clear_cached_target()
        if not success:
            # 对于WPS，避免使用keybd_event fallback，只使用PostMessage方式
            current_target = self._current_navigation_target() or self._resolve_control_target()
            if current_target and self._is_wps_slideshow_target(current_target):
                # WPS目标不使用fallback，因为已经通过前面的PostMessage处理了
                success = False
            else:
                self._focus_presentation_window_fallback()
                success = self._fallback_send_virtual_key(vk_code)
        if success:
            resolved_target = self._current_navigation_target() or self._resolve_control_target()
            if resolved_target:
                try:
                    self._last_target_hwnd = resolved_target
                except Exception:
                    pass
                forwarder = getattr(self, "_forwarder", None)
                if forwarder is not None:
                    try:
                        forwarder._last_target_hwnd = resolved_target  # type: ignore[attr-defined]
                    except Exception:
                        pass
        if override_dispatch_suppress:
            suppress_focus_restore = True
        if success and self.mode != "cursor" and not suppress_focus_restore:
            QTimer.singleShot(100, self._ensure_keyboard_capture)
        return success

    def cancel_pending_tool_restore(self) -> None:
        self._pending_tool_restore = None
        self._cancel_navigation_cursor_hold()

    def _set_navigation_reason(self, reason: str, active: bool) -> None:
        if not reason:
            return
        if self.whiteboard_active and active:
            return
        if active:
            self._navigation_reasons[reason] = self._navigation_reasons.get(reason, 0) + 1
        else:
            count = self._navigation_reasons.get(reason)
            if count is None:
                return
            if count <= 1:
                self._navigation_reasons.pop(reason, None)
            else:
                self._navigation_reasons[reason] = count - 1
        self._update_navigation_state()

    def _update_navigation_state(self) -> None:
        active = bool(self._navigation_reasons)
        if active == self.navigation_active:
            return
        self.navigation_active = active
        if active:
            if not self._nav_hold_persistent:
                self._cancel_navigation_cursor_hold()
            else:
                if self._nav_restore_timer.isActive():
                    self._nav_restore_timer.stop()
            self._pending_tool_restore = None
            # [修复] 不要在导航激活时中断正在进行的绘画
            # 这会导致画笔划过工具栏时笔画中断
            # if self.drawing:
            #     self.drawing = False
            toolbar = getattr(self, "toolbar", None)
            if toolbar is not None:
                try:
                    self.raise_toolbar()
                except Exception:
                    pass
        else:
            if not self._nav_hold_persistent:
                if self._nav_restore_mode and not self._nav_restore_timer.isActive():
                    self._nav_restore_timer.start(self._NAVIGATION_RESTORE_DELAY_MS)
        self.update_cursor()

    def handle_toolbar_enter(self) -> None:
        self._set_navigation_reason("toolbar", True)
        # 不再中断正在进行的笔画，避免划过工具栏时笔画丢失
        # 即使鼠标悬停在工具栏上，绘图操作仍应继续
        # if self.drawing:
        #     self.drawing = False
        self.cancel_pending_tool_restore()

    def handle_toolbar_leave(self) -> None:
        toolbar = getattr(self, "toolbar", None)
        if toolbar is not None and toolbar.underMouse():
            return
        self._set_navigation_reason("toolbar", False)
        self._set_navigation_reason("cursor-button", False)
        if not self.navigation_active and self.mode != "cursor":
            self.update_cursor()

    def _toolbar_contains_global(self, global_pos: QPoint) -> bool:
        toolbar = getattr(self, "toolbar", None)
        if toolbar is None or not toolbar.isVisible():
            return False
        local = toolbar.mapFromGlobal(global_pos)
        result = toolbar.rect().contains(local)
        # [调试日志] 追踪工具栏区域检测
        if hasattr(self, 'drawing') and self.drawing:
            logger.debug(f"[Overlay] _toolbar_contains_global: global=({global_pos.x()}, {global_pos.y()}), local=({local.x()}, {local.y()}), toolbar_rect={toolbar.rect()}, result={result}")
        return result

    def on_toolbar_mouse_leave(self) -> None:
        if not self._pending_tool_restore:
            return
        if getattr(self, "toolbar", None) is not None and self.toolbar.underMouse():
            return
        mode, shape = self._pending_tool_restore
        self._pending_tool_restore = None
        self._restore_last_tool(mode, shape_type=shape)

    def _fallback_send_virtual_key(self, vk_code: int) -> bool:
        if vk_code == 0 or _USER32 is None or self.whiteboard_active:
            return False
        try:
            scan_code = _USER32.MapVirtualKeyW(vk_code, 0) if hasattr(_USER32, "MapVirtualKeyW") else 0
        except Exception:
            scan_code = 0
        flags = KEYEVENTF_EXTENDEDKEY if vk_code in _NAVIGATION_EXTENDED_KEYS else 0

        # 检测当前前台窗口是否为WPS进程，如果是则只发送按下事件避免双重动画
        is_wps_foreground = False
        try:
            foreground_hwnd = _user32_get_foreground_window()
            if foreground_hwnd:
                top_hwnd = _user32_top_level_hwnd(foreground_hwnd)
                class_name = self._presentation_window_class(foreground_hwnd)
                top_class = self._presentation_window_class(top_hwnd) if top_hwnd else ""
                process_name = self._window_process_name(top_hwnd or foreground_hwnd)
                is_wps_foreground = self._is_wps_presentation_process(
                    process_name,
                    class_name,
                    top_class,
                )
        except Exception:
            pass

        try:
            _USER32.keybd_event(vk_code, scan_code, flags, 0)
            if not is_wps_foreground:
                _USER32.keybd_event(vk_code, scan_code, flags | KEYEVENTF_KEYUP, 0)
            return True
        except Exception:
            return False

    def update_cursor(self) -> None:
        if self.mode == "cursor":
            self.setCursor(Qt.CursorShape.ArrowCursor)
            return
        if self.navigation_active:
            self.setCursor(Qt.CursorShape.ArrowCursor)
            return
        if self.mode == "region_erase":
            self.setCursor(Qt.CursorShape.CrossCursor)
            return
        if self.mode == "shape":
            self.setCursor(Qt.CursorShape.CrossCursor)
            return

        mode = self.mode
        if mode == "eraser":
            base = float(clamp(self.eraser_size, 1.0, 50.0))
            d = max(10, int(base * 2.0))
        else:
            d = max(10, int(self.pen_size * 2.2))

        cache = getattr(self, "_cursor_cache", None)
        if isinstance(cache, OrderedDict):
            if mode == "eraser":
                color_key = "eraser"
            else:
                try:
                    color_key = QColor(self.pen_color).name().lower()
                except Exception:
                    color_key = "pen"
            cache_key = (mode, int(d), color_key)
            cached_cursor = cache.get(cache_key)
            if cached_cursor is not None:
                cache.move_to_end(cache_key)
                self.setCursor(cached_cursor)
                return

        self.cursor_pixmap = QPixmap(d, d)
        self.cursor_pixmap.fill(Qt.GlobalColor.transparent)
        painter = QPainter(self.cursor_pixmap)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        if mode == "eraser":
            painter.setBrush(QBrush(Qt.GlobalColor.white))
            painter.setPen(QPen(QColor("#555"), 2))
        else:
            painter.setBrush(QBrush(self.pen_color))
            painter.setPen(QPen(Qt.GlobalColor.black, 2))
        painter.drawEllipse(1, 1, d - 2, d - 2)
        painter.end()
        cursor = QCursor(self.cursor_pixmap, d // 2, d // 2)

        if isinstance(cache, OrderedDict):
            cache[cache_key] = cursor
            cache.move_to_end(cache_key)
            limit = int(getattr(self, "_CURSOR_CACHE_LIMIT", 32))
            while len(cache) > limit:
                cache.popitem(last=False)
        self.setCursor(cursor)

    def _apply_dirty_region(self, region: Optional[Union[QRect, QRectF]]) -> None:
        if not region:
            return
        if isinstance(region, QRectF):
            rect = region.toAlignedRect()
        else:
            rect = QRect(region)
        if rect.isNull() or rect.width() <= 0 or rect.height() <= 0:
            return
        inflated = rect.adjusted(-4, -4, 4, 4)
        target = inflated.intersected(self.rect())
        if target.isValid() and not target.isNull():
            self.update(target)
        else:
            self.update()

    def _shape_dirty_bounds(
        self,
        start_point: Optional[QPoint],
        end_point: Optional[Union[QPoint, QPointF]],
        pen_width: int,
    ) -> Optional[QRect]:
        if start_point is None or end_point is None:
            return None
        if isinstance(end_point, QPointF):
            end = end_point.toPoint()
        else:
            end = end_point
        rect = QRect(start_point, end).normalized()
        if rect.isNull():
            rect = QRect(end, end)
        margin = max(4, int(max(1, pen_width) * 2))
        return rect.adjusted(-margin, -margin, margin, margin)

    def _wps_auto_input_enabled(self) -> bool:
        mode = _normalize_wps_input_mode(getattr(self, "_wps_input_mode", "auto"))
        return mode == "auto"

    def _effective_wps_send_mode(self, wps_target: Optional[int] = None) -> str:
        mode = _normalize_wps_input_mode(getattr(self, "_wps_input_mode", "auto"))
        if mode == "manual":
            return "raw" if bool(getattr(self, "_wps_raw_input_mode", False)) else "message"
        if mode == "auto":
            if bool(getattr(self, "_wps_auto_force_message", False)):
                return "message"
            if wps_target is None:
                try:
                    wps_target = self._find_wps_slideshow_target()
                except Exception:
                    wps_target = None
            if wps_target and self._is_wps_slideshow_target(wps_target):
                return "raw"
            return "message"
        if mode in {"raw", "message"}:
            return mode
        return "message"

    def _effective_wps_raw_input_mode(self, wps_target: Optional[int] = None) -> bool:
        send_mode = self._effective_wps_send_mode(wps_target)
        return send_mode == "raw"

    def _effective_wps_wheel_forward(self, wps_target: Optional[int] = None) -> bool:
        return bool(getattr(self, "_wps_wheel_forward", False))

    def _should_capture_keyboard(self) -> bool:
        try:
            target = self._find_wps_slideshow_target()
        except Exception:
            target = None
        if not self._effective_wps_raw_input_mode(target):
            return True
        if target and self._is_wps_slideshow_target(target):
            return False
        return True

    def _is_wps_raw_input_passthrough(self, wps_target: Optional[int] = None) -> bool:
        if not self._effective_wps_raw_input_mode(wps_target):
            return False
        target = wps_target
        if target is None:
            try:
                target = self._find_wps_slideshow_target()
            except Exception:
                target = None
        if not target or not self._is_wps_slideshow_target(target):
            return False
        return self._wps_target_has_foreground_focus(target)

    def _force_wps_foreground_for_raw_input(self) -> None:
        if not self._effective_wps_raw_input_mode():
            return
        try:
            target = self._find_wps_slideshow_target()
        except Exception:
            target = None
        if not target or not self._is_wps_slideshow_target(target):
            return
        if self._wps_target_has_foreground_focus(target):
            return
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            try:
                forwarder.force_focus_presentation_window()
                return
            except Exception:
                pass
        try:
            self._focus_presentation_window_fallback()
        except Exception:
            pass

    def _apply_focus_acceptance(self, allow_focus: bool) -> None:
        block_focus = not bool(allow_focus)
        if bool(getattr(self, "_focus_accept_blocked", False)) == block_focus:
            return
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, block_focus)
        try:
            self.setWindowFlag(Qt.WindowType.WindowDoesNotAcceptFocus, block_focus)
        except Exception:
            pass
        self._focus_accept_blocked = block_focus
        if self.isVisible():
            super().show()  # Ensure window flags are applied immediately.

    def _apply_input_passthrough(self, enabled: bool) -> None:
        # Toggle input passthrough flags and force a refresh
        self.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, enabled)
        self.setWindowFlag(Qt.WindowType.WindowTransparentForInput, enabled)
        if enabled:
            self._release_keyboard_capture()
        if self.isVisible():
            super().show()  # Force Qt to apply the new flags

    def _ensure_keyboard_capture(self) -> None:
        if not self._should_capture_keyboard():
            self._apply_focus_acceptance(False)
            self._release_keyboard_capture()
            try:
                self.clearFocus()
            except Exception:
                pass
            try:
                self.raise_()
            except Exception:
                pass
            if self.whiteboard_active:
                try:
                    self._raise_roll_call_window(activate=False)
                except Exception:
                    pass
            self._log_nav_trace("kbd_capture_skip", reason="wps_raw_input")
            self._force_wps_foreground_for_raw_input()
            try:
                self._update_wps_nav_hook_state()
            except Exception:
                pass
            return
        self._apply_focus_acceptance(True)
        if not self._keyboard_grabbed:
            try:
                self.grabKeyboard()
                self._keyboard_grabbed = True
            except Exception:
                self._keyboard_grabbed = False
        try:
            self.raise_()
            self.activateWindow()
        except Exception:
            pass
        if self.whiteboard_active:
            try:
                self._raise_roll_call_window(activate=False)
            except Exception:
                pass
        self.setFocus(Qt.FocusReason.ActiveWindowFocusReason)

    def _release_keyboard_capture(self) -> None:
        if not self._keyboard_grabbed:
            return
        try:
            self.releaseKeyboard()
        except Exception:
            pass
        self._keyboard_grabbed = False

    @contextlib.contextmanager
    def _temporarily_release_keyboard(
        self, *, release: bool = True, restore: bool = True
    ) -> Iterable[bool]:
        had_keyboard_grab = bool(self._keyboard_grabbed and release)
        if release and self._keyboard_grabbed:
            self._release_keyboard_capture()
        try:
            yield had_keyboard_grab
        finally:
            if restore and had_keyboard_grab:
                self._ensure_keyboard_capture()

    def _fallback_detect_presentation_window_user32(self) -> Optional[int]:
        if _USER32 is None:
            return None
        overlay_hwnd = int(self.winId()) if self.winId() else 0
        foreground = _user32_get_foreground_window()
        if (
            foreground
            and foreground != overlay_hwnd
            and not self._should_ignore_window(foreground)
            and self._fallback_is_candidate_window(foreground)
        ):
            normalized = self._normalize_presentation_target(foreground)
            ordered: Tuple[int, ...] = tuple(
                hwnd
                for hwnd in (
                    normalized if normalized and self._fallback_is_target_window_valid(normalized) else None,
                    foreground if self._fallback_is_target_window_valid(foreground) else None,
                )
                if hwnd
            )
            for candidate in ordered:
                if self._presentation_control_allowed(candidate, log=False):
                    return candidate
        candidates_list = self._enumerate_overlay_candidate_windows(overlay_hwnd)
        if candidates_list is None:
            return None
        for hwnd in candidates_list:
            if not self._fallback_is_candidate_window(hwnd):
                continue
            normalized = self._normalize_presentation_target(hwnd)
            ordered: Tuple[int, ...] = tuple(
                handle
                for handle in (
                    normalized if normalized and self._fallback_is_target_window_valid(normalized) else None,
                    hwnd if self._fallback_is_target_window_valid(hwnd) else None,
                )
                if handle
            )
            for candidate in ordered:
                if self._presentation_control_allowed(candidate, log=False):
                    return candidate
        return None

    def _normalize_presentation_target(self, hwnd: Optional[int]) -> Optional[int]:
        if not hwnd:
            return None
        forwarder = getattr(self, "_forwarder", None)
        if forwarder is not None:
            try:
                normalized = forwarder._normalize_presentation_target(hwnd)
            except Exception:
                normalized = None
            else:
                if normalized and normalized != hwnd and logger.isEnabledFor(logging.DEBUG):
                    logger.debug(
                        "navigation: overlay normalized hwnd=%s -> %s",
                        hex(hwnd),
                        hex(normalized),
                    )
                if normalized:
                    return normalized
        return hwnd

    def _log_navigation_debug(self, message: str, **extra: Any) -> None:
        if not logger.isEnabledFor(logging.DEBUG):
            return
        if extra:
            formatted = " ".join(f"{key}={value}" for key, value in extra.items())
            logger.debug("navigation: %s %s", message, formatted)
        else:
            logger.debug("navigation: %s", message)

    def _log_nav_trace(self, message: str, **extra: Any) -> None:
        if not getattr(self, "_nav_debug_enabled", False):
            return
        if extra:
            formatted = " ".join(f"{key}={value}" for key, value in extra.items())
            logger.info("nav-trace: %s %s", message, formatted)
        else:
            logger.info("nav-trace: %s", message)

    def _current_navigation_target(self) -> Optional[int]:
        target: Optional[int] = None
        if self._forwarder is not None:
            try:
                target = self._forwarder.get_presentation_target()
            except Exception:
                target = None
        if target and not self._presentation_control_allowed(target, log=False):
            if self._forwarder is not None:
                try:
                    self._forwarder.clear_cached_target()
                except Exception:
                    pass
            target = None
        if not target:
            target = self._resolve_presentation_target()
            if target and not self._presentation_control_allowed(target, log=False):
                target = None
        return target

    def _should_refresh_cached_presentation_target(self, hwnd: int) -> bool:
        class_name = self._presentation_window_class(hwnd)
        return not self._is_preferred_presentation_class(class_name)

    def _focus_presentation_window_fallback(self) -> bool:
        if _USER32 is None:
            return False
        hwnd = self._resolve_presentation_target()
        if not hwnd:
            candidate = self._fallback_detect_presentation_window_user32()
            if candidate and self._fallback_is_target_window_valid(candidate):
                hwnd = candidate
        if not hwnd or not self._fallback_is_target_window_valid(hwnd):
            return False
        if not self._presentation_control_allowed(hwnd, log=False):
            return False
        class_name = self._presentation_window_class(hwnd)
        top_level = _user32_top_level_hwnd(hwnd)
        attach_pair = self._attach_to_target_thread(top_level or hwnd)
        if attach_pair is None and top_level and top_level != hwnd:
            attach_pair = self._attach_to_target_thread(hwnd)
        try:
            if (
                self._is_wps_slideshow_class(class_name)
                or self._is_wps_slideshow_class(self._presentation_window_class(top_level))
            ):
                self._last_target_hwnd = hwnd
                return True
            focused = False
            if top_level and top_level != hwnd:
                focused = _user32_focus_window(top_level)
                if focused:
                    _user32_focus_window(hwnd)
            if not focused:
                focused = _user32_focus_window(hwnd)
                if not focused and top_level and top_level != hwnd:
                    focused = _user32_focus_window(top_level)
            if focused:
                self._last_target_hwnd = hwnd
            return focused
        finally:
            self._detach_from_target_thread(attach_pair)

    def _detect_presentation_window(self) -> Optional[int]:
        if win32gui is None:
            return self._fallback_detect_presentation_window_user32()
        overlay_hwnd = int(self.winId()) if self.winId() else 0
        try:
            foreground = win32gui.GetForegroundWindow()
        except Exception:
            foreground = 0
        if (
            foreground
            and foreground != overlay_hwnd
            and not self._should_ignore_window(foreground)
            and self._is_candidate_presentation_window(foreground)
        ):
            normalized = self._normalize_presentation_target(foreground)
            candidates: Tuple[int, ...] = tuple(
                hwnd
                for hwnd in (
                    normalized if normalized and self._is_target_window_valid(normalized) else None,
                    foreground if self._is_target_window_valid(foreground) else None,
                )
                if hwnd
            )
            for candidate in candidates:
                if self._presentation_control_allowed(candidate, log=False):
                    return candidate
        candidates_list = self._enumerate_overlay_candidate_windows_win32(overlay_hwnd)
        if candidates_list is None:
            return None
        for hwnd in candidates_list:
            if not self._is_candidate_presentation_window(hwnd):
                continue
            normalized = self._normalize_presentation_target(hwnd)
            ordered: Tuple[int, ...] = tuple(
                handle
                for handle in (
                    normalized if normalized and self._is_target_window_valid(normalized) else None,
                    hwnd if self._is_target_window_valid(hwnd) else None,
                )
                if handle
            )
            for candidate in ordered:
                if self._presentation_control_allowed(candidate, log=False):
                    return candidate
        return None

    def _resolve_presentation_target(self) -> Optional[int]:
        if win32gui is None:
            hwnd = self._last_target_hwnd
            if hwnd and not self._presentation_control_allowed(hwnd, log=False):
                self._last_target_hwnd = None
                hwnd = None
            if hwnd and self._fallback_is_target_window_valid(hwnd):
                normalized = self._normalize_presentation_target(hwnd)
                if normalized and normalized != hwnd and self._fallback_is_target_window_valid(normalized):
                    self._last_target_hwnd = normalized
                    return normalized
                if self._should_refresh_cached_presentation_target(hwnd):
                    refreshed = self._fallback_detect_presentation_window_user32()
                    if (
                        refreshed
                        and refreshed != hwnd
                        and self._fallback_is_target_window_valid(refreshed)
                    ):
                        normalized = self._normalize_presentation_target(refreshed)
                        if normalized and self._fallback_is_target_window_valid(normalized):
                            if self._presentation_control_allowed(normalized, log=False):
                                self._last_target_hwnd = normalized
                                return normalized
                            self._last_target_hwnd = None
                            return None
                        if self._presentation_control_allowed(refreshed, log=False):
                            self._last_target_hwnd = refreshed
                            return refreshed
                        self._last_target_hwnd = None
                        return None
                return hwnd
            hwnd = self._fallback_detect_presentation_window_user32()
            normalized = self._normalize_presentation_target(hwnd) if hwnd else None
            target = normalized or hwnd
            if target and self._fallback_is_target_window_valid(target):
                if self._presentation_control_allowed(target, log=False):
                    self._last_target_hwnd = target
                    return target
                self._last_target_hwnd = None
                return None
            self._last_target_hwnd = None
            return None
        hwnd = self._last_target_hwnd
        if hwnd and not self._presentation_control_allowed(hwnd, log=False):
            self._last_target_hwnd = None
            hwnd = None
        if hwnd and self._is_target_window_valid(hwnd):
            normalized = self._normalize_presentation_target(hwnd)
            if normalized and normalized != hwnd and self._is_target_window_valid(normalized):
                self._last_target_hwnd = normalized
                hwnd = normalized
            if self._should_refresh_cached_presentation_target(hwnd):
                refreshed = self._detect_presentation_window()
                normalized = self._normalize_presentation_target(refreshed) if refreshed else None
                target = normalized or refreshed
                if target and target != hwnd and self._is_target_window_valid(target):
                    if self._presentation_control_allowed(target, log=False):
                        self._last_target_hwnd = target
                        return target
                    self._last_target_hwnd = None
                    return None
            return hwnd
        hwnd = self._detect_presentation_window()
        normalized = self._normalize_presentation_target(hwnd) if hwnd else None
        target = normalized or hwnd
        if target and self._is_target_window_valid(target):
            if self._presentation_control_allowed(target, log=False):
                self._last_target_hwnd = target
                return target
            self._last_target_hwnd = None
            return None
        self._last_target_hwnd = None
        return None

    def _is_candidate_presentation_window(self, hwnd: int) -> bool:
        if win32gui is None:
            return self._fallback_is_candidate_window(hwnd)
        if self._should_ignore_window(hwnd):
            return False
        try:
            class_name = win32gui.GetClassName(hwnd).lower()
        except Exception:
            class_name = ""
        if class_name in self._KNOWN_PRESENTATION_CLASSES:
            return True
        process_name = self._window_process_name(hwnd)
        if self._is_wps_presentation_process(process_name, class_name, ""):
            return True
        try:
            rect = win32gui.GetWindowRect(hwnd)
        except Exception:
            return False
        if not rect:
            return False
        return self._matches_overlay_geometry(rect)

    def _update_visibility_for_mode(self, *, initial: bool = False) -> None:
        passthrough = self.mode == "cursor" and (not self.whiteboard_active)
        self._apply_input_passthrough(passthrough)
        if passthrough:
            if not self.isVisible():
                self.show()
            if not initial:
                self._release_keyboard_capture()
            try:
                self._update_wps_nav_hook_state()
            except Exception:
                pass
            return
        if not self.isVisible():
            self.show()
        suppress_focus = False
        if self._skip_focus_reactivation:
            suppress_focus = True
            self._skip_focus_reactivation = False
        if suppress_focus:
            if self._keyboard_grabbed:
                self._release_keyboard_capture()
        else:
            self._ensure_keyboard_capture()
        if self.whiteboard_active:
            self._raise_roll_call_window(activate=False)
        if initial:
            try:
                self._update_wps_nav_hook_state()
            except Exception:
                pass
            return
        try:
            self._update_wps_nav_hook_state()
        except Exception:
            pass

    def _push_history(self) -> None:
        if not isinstance(self.canvas, QPixmap):
            return
        self.history.append(self.canvas.copy())
        if len(self.history) > self._history_limit:
            self.history.pop(0)
        self._update_undo_button()

    def _update_undo_button(self) -> None:
        if getattr(self, "toolbar", None):
            self.toolbar.update_undo_state(bool(self.history))

    def _cancel_region_selection(self) -> None:
        self._region_select_start = None
        self._region_preview_bounds = None
        self._region_previewing = False
        try:
            self.temp_canvas.fill(Qt.GlobalColor.transparent)
        except Exception:
            pass

    def _begin_region_selection(self, event: QMouseEvent) -> None:
        self._region_select_start = event.pos()
        self._region_preview_bounds = None
        self._region_previewing = True
        try:
            self.temp_canvas.fill(Qt.GlobalColor.transparent)
        except Exception:
            pass
        self._update_region_selection_preview(event.pos())

    def _update_region_selection_preview(self, current) -> None:
        if not self._region_previewing or self._region_select_start is None:
            return
        self.temp_canvas.fill(Qt.GlobalColor.transparent)
        if isinstance(current, QPointF):
            end_point = current.toPoint()
        else:
            end_point = QPoint(current)
        rect = QRect(self._region_select_start, end_point).normalized()
        self._region_preview_bounds = None
        if rect.isNull() or rect.width() < 2 or rect.height() < 2:
            self.update()
            return
        painter = QPainter(self.temp_canvas)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        fill_color = QColor(self.pen_color)
        fill_color.setAlpha(40)
        painter.fillRect(rect, fill_color)
        pen = QPen(QColor("#f1f3f4"))
        pen.setCosmetic(True)
        pen.setWidth(2)
        pen.setStyle(Qt.PenStyle.DashLine)
        painter.setPen(pen)
        painter.drawRect(rect)
        painter.end()
        margin = 6
        self._region_preview_bounds = rect.adjusted(-margin, -margin, margin, margin)
        self._apply_dirty_region(self._region_preview_bounds or rect)

    def _finalize_region_selection(self, release_pos: QPoint) -> None:
        start = self._region_select_start
        preview_bounds = self._region_preview_bounds
        self._cancel_region_selection()
        if start is None:
            if preview_bounds:
                self._apply_dirty_region(preview_bounds)
            return
        rect = QRect(start, release_pos).normalized()
        if rect.isNull() or rect.width() < 2 or rect.height() < 2:
            if preview_bounds:
                self._apply_dirty_region(preview_bounds)
            return
        target = rect.intersected(self.rect())
        if target.isNull():
            return
        self._push_history()
        self._release_canvas_painters()
        painter = QPainter(self.canvas)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setCompositionMode(QPainter.CompositionMode.CompositionMode_Clear)
        painter.fillRect(target, QColor(0, 0, 0, 0))
        painter.end()
        self._apply_dirty_region(target)
        self._update_undo_button()

    def clear_all(self) -> None:
        """清除整块画布，同时根据需要恢复画笔模式。"""
        restore_needed = self.mode not in {"brush", "shape"}
        self._push_history()
        self._release_canvas_painters()
        self.canvas.fill(Qt.GlobalColor.transparent)
        self.temp_canvas.fill(Qt.GlobalColor.transparent)
        self._last_preview_bounds = None
        self._cancel_region_selection()
        self.update()
        self._eraser_last_point = None
        if restore_needed:
            self._restore_last_tool()
        else:
            if self.mode in {"brush", "shape"}:
                self._update_last_tool_snapshot()
            self.raise_toolbar()
        self._update_undo_button()

    def use_brush_color(self, color_hex: str) -> None:
        """根据传入的十六进制颜色值启用画笔模式。"""
        color = QColor(color_hex)
        if not color.isValid():
            return
        self.pen_color = color
        base_width = self._effective_brush_width()
        self._refresh_pen_alpha_state()
        self._update_brush_pen_appearance(base_width, self._active_fade_max)
        self._update_pen_tooltip()
        self.set_mode("brush")
        self.save_settings()

    def undo_last_action(self) -> None:
        if not self.history:
            return
        last = self.history.pop()
        if isinstance(last, QPixmap):
            self._release_canvas_painters()
            self.canvas = last
        else:
            self._update_undo_button()
            return
        self._cancel_region_selection()
        self.temp_canvas.fill(Qt.GlobalColor.transparent)
        self.drawing = False
        self._last_preview_bounds = None
        self.update()
        self.raise_toolbar()
        self._update_undo_button()

    def _update_presentation_control_flags(self, flags: Optional[Mapping[str, Any]]) -> None:
        defaults = {
            "ms_ppt": True,
            "wps_ppt": True,
        }
        resolved: Dict[str, bool] = {}
        source = flags or {}
        for key, default in defaults.items():
            raw = None
            if isinstance(source, Mapping):
                raw = source.get(key)
                if raw is None:
                    raw = source.get(f"control_{key}")
            resolved[key] = parse_bool(raw, default)
        previous = getattr(self, "_presentation_control_flags", None)
        previous_flags: Mapping[str, Any] = previous or {}
        changed = previous != resolved
        self._presentation_control_flags = resolved
        self.control_ms_ppt = resolved["ms_ppt"]
        self.control_wps_ppt = resolved["wps_ppt"]
        if changed:
            forwarder = getattr(self, "_forwarder", None)
            if forwarder is not None:
                try:
                    forwarder.clear_cached_target()
                except Exception:
                    pass
            if hasattr(self, "_last_target_hwnd"):
                self._last_target_hwnd = None
            try:
                QTimer.singleShot(50, lambda: self._resolve_control_target())
            except Exception:
                try:
                    self._resolve_control_target()
                except Exception:
                    pass
            else:
                if forwarder is not None:
                    try:
                        QTimer.singleShot(50, lambda: forwarder.get_presentation_target())
                    except Exception:
                        pass
        if not resolved.get("wps_ppt"):
            self._cancel_wps_slideshow_binding_retry()
        if resolved.get("wps_ppt") and not parse_bool(previous_flags.get("wps_ppt"), True):
            try:
                QTimer.singleShot(0, self._apply_wps_control_enabled_refresh)
            except Exception:
                try:
                    self._apply_wps_control_enabled_refresh()
                except Exception:
                    pass
        try:
            QTimer.singleShot(0, self._update_wps_nav_hook_state)
        except Exception:
            try:
                self._update_wps_nav_hook_state()
            except Exception:
                pass

    def save_settings(self) -> None:
        self.paint_config.brush_base_size = float(self.pen_base_size)
        self.paint_config.brush_color = self.pen_color.name()
        self.paint_config.brush_style = _DEFAULT_PEN_STYLE
        self.paint_config.brush_opacity = int(clamp(self._brush_opacity, 0, 255))
        self.paint_config.ui_scale = float(self.ui_scale)
        quicks = self._normalize_quick_colors(self.quick_colors)
        self.quick_colors = quicks
        self.paint_config.quick_color_1 = quicks[0]
        self.paint_config.quick_color_2 = quicks[1]
        self.paint_config.quick_color_3 = quicks[2]
        if isinstance(self.last_board_color, QColor) and self.last_board_color.isValid():
            self.paint_config.board_color = self.last_board_color.name()
        self.paint_config.eraser_size = float(clamp(self.eraser_size, 1.0, 50.0))
        if self.current_shape in SHAPE_TYPES:
            self.paint_config.shape_type = str(self.current_shape)
        self.paint_config.control_ms_ppt = bool(self.control_ms_ppt)
        self.paint_config.control_wps_ppt = bool(self.control_wps_ppt)
        self.paint_config.wps_input_mode = _normalize_wps_input_mode(
            getattr(self, "_wps_input_mode", "auto")
        )
        self.paint_config.wps_raw_input = bool(getattr(self, "_wps_raw_input_mode", False))
        self.paint_config.wps_wheel_forward = bool(getattr(self, "_wps_wheel_forward", False))
        try:
            self.settings_manager.update_paint_settings(self.paint_config)
        except Exception:
            logger.error("Failed to persist paint settings", exc_info=True)

    def save_window_position(self) -> None:
        pos = self.toolbar.pos()
        self.paint_config.x = int(pos.x())
        self.paint_config.y = int(pos.y())
        try:
            self.settings_manager.update_paint_settings(self.paint_config)
        except Exception:
            logger.error("Failed to persist paint window position", exc_info=True)

    # ---- 画图事件 ----
    def wheelEvent(self, e) -> None:
        delta_vec = e.angleDelta()
        wheel_delta = int(delta_vec.y() or delta_vec.x())
        target = self._resolve_control_target()
        if target and not self._presentation_control_allowed(target):
            super().wheelEvent(e)
            return
        try:
            wps_target = self._resolve_wps_nav_target()
        except Exception:
            wps_target = None
        wps_wheel_forward = bool(wps_target) and self._effective_wps_wheel_forward(wps_target)
        wps_foreground = False
        if wps_target:
            if self._is_wps_slideshow_target(wps_target):
                wps_foreground = self._wps_target_has_foreground_focus(wps_target)
            else:
                wps_foreground = self._target_has_foreground_focus(wps_target)
        if wheel_delta and self._wps_nav_hook_active and self._wps_hook_intercept_wheel:
            self._log_nav_trace("wheel_swallow", reason="wps_hook_active", delta=wheel_delta)
            e.accept()
            return
        if wheel_delta and self._wps_hook_recently_fired() and self._wps_hook_intercept_wheel:
            self._log_nav_trace("wheel_swallow", reason="wps_hook_recent", delta=wheel_delta)
            e.accept()
            return
        allow_cursor = self.mode == "cursor" or self.navigation_active
        if (
            wheel_delta
            and wps_target
            and not self.whiteboard_active
            and wps_foreground
            and not wps_wheel_forward
        ):
            self._log_nav_trace(
                "wheel_passthrough",
                reason="foreground_focus",
                delta=wheel_delta,
                target=hex(wps_target),
            )
            e.accept()
            return
        if wheel_delta and wps_target and not self.whiteboard_active and wps_wheel_forward:
            if self._send_navigation_wheel(wheel_delta):
                e.accept()
                return
        # 对于WPS，统一使用虚拟键处理，避免Qt系统事件的二次响应
        # if self._wps_target_has_foreground_focus(target):
        #     super().wheelEvent(e)
        #     return

        # 特殊处理PowerPoint演示文稿
        if (
            wheel_delta
            and target
            and not self.whiteboard_active
            and self._is_ms_slideshow_target(target)
        ):
            self._set_navigation_reason("wheel", True)
            try:
                if wheel_delta < 0:
                    self._send_slide_virtual_key(VK_DOWN, via_toolbar=False)
                else:
                    self._send_slide_virtual_key(VK_UP, via_toolbar=False)
            finally:
                self._set_navigation_reason("wheel", False)
            e.accept()
            return

        handled = False
        if self._forwarder:
            handled = self._forwarder.forward_wheel(e, allow_cursor=allow_cursor)
        if not handled and not self.whiteboard_active and self.mode != "cursor":
            if wheel_delta:
                if wps_target and not wps_wheel_forward:
                    if self._send_navigation_wheel(wheel_delta):
                        handled = True
                elif not wps_target:
                    if self._send_navigation_wheel(wheel_delta):
                        handled = True
        if handled:
            e.accept()
            return
        super().wheelEvent(e)

    def _reset_brush_tracking(self) -> None:
        self._release_uniform_painter()
        self._stroke_points.clear()
        self._stroke_timestamps.clear()
        self._stroke_last_midpoint = None
        self._stroke_filter_point = None
        self._stroke_speed = 0.0
        self._stroke_target_width = float(self.last_width)
        self._stroke_smoothed_target = float(self.last_width)
        self._stroke_width_velocity = 0.0
        self._stroke_total_length = 0.0
        self._stroke_tail_state = 0.0
        self._stroke_jitter_offset = QPointF()
        self._stroke_uniform_active = False
        self._stroke_uniform_bounds = None
        self._stroke_segments.clear()
        self._stroke_release_taper = False

    def _compute_release_tail_params(
        self,
        config: PenStyleConfig,
    ) -> tuple[float, float, float, float, float, int]:
        base_size = float(max(1.0, self.pen_base_size))
        effective_base = max(1.0, base_size * config.width_multiplier)
        min_w = max(1.0, effective_base * config.target_min_factor)
        tail_min_w = max(self._RELEASE_TAIL_MIN_ABS, effective_base * self._RELEASE_TAIL_MIN_RATIO)
        tail_min_w = min(tail_min_w, min_w)
        start_w = max(min_w, float(self.last_width))
        tail_len = clamp(
            effective_base * self._RELEASE_TAIL_LEN_FACTOR,
            self._RELEASE_TAIL_MIN_LEN,
            self._RELEASE_TAIL_MAX_LEN * self._RELEASE_TAIL_LEN_MAX_SCALE,
        )
        steps = max(3, int(self._RELEASE_TAIL_STEPS * self._RELEASE_TAIL_STEP_FACTOR))
        return effective_base, min_w, start_w, tail_min_w, tail_len, steps

    def _recent_motion_stats(self) -> tuple[float, float, float]:
        count = min(len(self._stroke_points), len(self._stroke_timestamps))
        if count < 2:
            return 0.0, 0.0, 0.0
        start_index = max(1, count - 4)
        speeds: List[float] = []
        last_speed = 0.0
        last_dist = 0.0
        for idx in range(start_index, count):
            dt = self._stroke_timestamps[idx] - self._stroke_timestamps[idx - 1]
            if dt <= 1e-4:
                continue
            dx = self._stroke_points[idx].x() - self._stroke_points[idx - 1].x()
            dy = self._stroke_points[idx].y() - self._stroke_points[idx - 1].y()
            dist = math.hypot(dx, dy)
            if dist <= 1e-4:
                continue
            speed = dist / dt
            speeds.append(speed)
            last_speed = speed
            last_dist = dist
        if not speeds:
            return 0.0, 0.0, 0.0
        avg_speed = sum(speeds) / len(speeds)
        return last_speed, avg_speed, last_dist

    def _should_apply_release_taper(self, effective_base: float) -> bool:
        if not self._stroke_timestamps:
            return False
        time_since_last = time.time() - self._stroke_timestamps[-1]
        if time_since_last >= self._RELEASE_TAIL_PAUSE_SECONDS:
            return False
        last_speed, avg_speed, last_dist = self._recent_motion_stats()
        if last_speed <= 0.0:
            return False
        threshold = max(self._RELEASE_TAIL_SPEED_MIN, effective_base * self._RELEASE_TAIL_SPEED_SCALE)
        if last_speed < threshold:
            return False
        min_move = max(self._RELEASE_TAIL_MIN_MOVE_ABS, effective_base * self._RELEASE_TAIL_MIN_MOVE_RATIO)
        if last_dist < min_move:
            return False
        if avg_speed > 1e-3:
            ratio = last_speed / avg_speed
            if ratio < self._RELEASE_TAIL_SPEED_RATIO:
                return False
        return True

    def _finish_brush_stroke(self) -> Optional[QRectF]:
        if self.mode != "brush":
            return None
        if len(self._stroke_points) < 2:
            return None
        last_point = QPointF(self._stroke_points[-1])
        prev_point = QPointF(self._stroke_points[-2])
        dx = last_point.x() - prev_point.x()
        dy = last_point.y() - prev_point.y()
        distance = math.hypot(dx, dy)
        if distance <= 0.01:
            return None
        dir_x = dx / distance
        dir_y = dy / distance
        config = get_pen_style_config(self.pen_style)
        effective_base, _min_w, start_w, tail_min_w, tail_len, steps = self._compute_release_tail_params(
            config
        )
        apply_tail = self._should_apply_release_taper(effective_base)
        self._stroke_release_taper = apply_tail
        if not apply_tail:
            return None

        if getattr(self, "_stroke_uniform_active", False) and self._stroke_uniform_canvas is not None:
            last_mid = QPointF(self._stroke_last_midpoint) if self._stroke_last_midpoint else QPointF(prev_point)
            current_point = QPointF(last_point)
            dirty: Optional[QRectF] = None
            uniform_painter = self._ensure_uniform_painter()
            if uniform_painter is None:
                return None

            for idx in range(1, steps + 1):
                t = idx / steps
                tip_t = t ** self._RELEASE_TAIL_TIP_EXP
                next_point = QPointF(
                    last_point.x() + dir_x * tail_len * t,
                    last_point.y() + dir_y * tail_len * t,
                )
                width = start_w * (1.0 - tip_t) + tail_min_w * tip_t
                alpha_factor = 1.0 - (t ** 1.4) * self._RELEASE_TAIL_ALPHA_FALLOFF
                fade_alpha = int(
                    clamp(self._active_fade_max * alpha_factor, self._active_fade_min, self._active_fade_max)
                )
                self._update_brush_pen_appearance(width, fade_alpha)
                current_mid = (current_point + next_point) / 2.0
                path = QPainterPath(last_mid)
                path.quadTo(current_point, current_mid)
                self._stroke_segments.append(
                    (QPointF(last_mid), QPointF(current_point), QPointF(current_mid), float(width))
                )
                shadow_alpha_current = (
                    self._brush_shadow_pen.color().alpha() if self._brush_shadow_pen is not None else 0
                )
                if shadow_alpha_current > 0:
                    uniform_painter.setPen(self._brush_shadow_pen)
                    uniform_painter.drawPath(path)
                pen_alpha_current = self._brush_pen.color().alpha() if self._brush_pen is not None else 0
                if pen_alpha_current > 0:
                    uniform_painter.setPen(self._brush_pen)
                    uniform_painter.drawPath(path)
                bounds = path.boundingRect()
                dirty = bounds if dirty is None else dirty.united(bounds)
                last_mid = QPointF(current_mid)
                current_point = QPointF(next_point)

            if dirty is None:
                return None
            margin = max(start_w * 0.6, 6.0)
            dirty = dirty.adjusted(-margin, -margin, margin, margin)
            if self._stroke_uniform_bounds is None:
                self._stroke_uniform_bounds = QRectF(dirty)
            else:
                self._stroke_uniform_bounds = self._stroke_uniform_bounds.united(dirty)
            return dirty

        painter = self._ensure_brush_painter()
        last_mid = QPointF(self._stroke_last_midpoint) if self._stroke_last_midpoint else QPointF(prev_point)
        current_point = QPointF(last_point)
        dirty: Optional[QRectF] = None

        for idx in range(1, steps + 1):
            t = idx / steps
            tip_t = t ** self._RELEASE_TAIL_TIP_EXP
            next_point = QPointF(
                last_point.x() + dir_x * tail_len * t,
                last_point.y() + dir_y * tail_len * t,
            )
            width = start_w * (1.0 - tip_t) + tail_min_w * tip_t
            alpha_factor = 1.0 - (t ** 1.4) * self._RELEASE_TAIL_ALPHA_FALLOFF
            fade_alpha = int(
                clamp(self._active_fade_max * alpha_factor, self._active_fade_min, self._active_fade_max)
            )
            self._update_brush_pen_appearance(width, fade_alpha)
            current_mid = (current_point + next_point) / 2.0
            path = QPainterPath(last_mid)
            path.quadTo(current_point, current_mid)

            shadow_alpha_current = (
                self._brush_shadow_pen.color().alpha() if self._brush_shadow_pen is not None else 0
            )
            if shadow_alpha_current > 0:
                painter.setPen(self._brush_shadow_pen)
                painter.drawPath(path)
            pen_alpha_current = self._brush_pen.color().alpha() if self._brush_pen is not None else 0
            if pen_alpha_current > 0:
                painter.setPen(self._brush_pen)
                painter.drawPath(path)

            bounds = path.boundingRect()
            dirty = bounds if dirty is None else dirty.united(bounds)
            last_mid = QPointF(current_mid)
            current_point = QPointF(next_point)

        if dirty is None:
            return None
        overlay_factor = max(
            config.shadow_width_scale,
            1.0 + config.feather_strength,
            1.0 + config.noise_strength * 0.45,
        )
        margin = max(start_w * (0.6 + (overlay_factor - 1.0) * 0.7), start_w * 0.6) + 6.0
        return dirty.adjusted(-margin, -margin, margin, margin)

    def _redraw_uniform_stroke(self) -> Optional[QRectF]:
        if self._stroke_uniform_canvas is None or not self._stroke_segments:
            return None
        config = get_pen_style_config(self.pen_style)
        _effective_base, _min_w, _start_w, tail_min_w, _tail_len, steps = self._compute_release_tail_params(
            config
        )
        segment_count = len(self._stroke_segments)
        apply_taper = bool(getattr(self, "_stroke_release_taper", False))
        if apply_taper:
            taper_count = min(segment_count, max(4, int(steps * self._RELEASE_TAIL_REDRAW_MULT)))
        else:
            taper_count = 0
        taper_start = segment_count - taper_count
        overlay_factor = max(
            config.shadow_width_scale,
            1.0 + config.feather_strength,
            1.0 + config.noise_strength * 0.45,
        )

        self._release_uniform_painter()
        self._stroke_uniform_canvas.fill(Qt.GlobalColor.transparent)
        painter = QPainter(self._stroke_uniform_canvas)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setCompositionMode(QPainter.CompositionMode.CompositionMode_Source)
        bounds: Optional[QRectF] = None

        for idx, (start, control, end, width) in enumerate(self._stroke_segments):
            width_value = float(width)
            if idx >= taper_start and taper_count > 0:
                t = (idx - taper_start + 1) / taper_count
                tip_t = t ** self._RELEASE_TAIL_TIP_EXP
                width_value = width_value * (1.0 - tip_t) + tail_min_w * tip_t
            self._update_brush_pen_appearance(width_value, self._active_fade_max)
            path = QPainterPath(start)
            path.quadTo(control, end)
            shadow_alpha_current = (
                self._brush_shadow_pen.color().alpha() if self._brush_shadow_pen is not None else 0
            )
            if shadow_alpha_current > 0:
                painter.setPen(self._brush_shadow_pen)
                painter.drawPath(path)
            pen_alpha_current = self._brush_pen.color().alpha() if self._brush_pen is not None else 0
            if pen_alpha_current > 0:
                painter.setPen(self._brush_pen)
                painter.drawPath(path)
            dirty = path.boundingRect()
            margin = max(
                width_value * (0.6 + (overlay_factor - 1.0) * 0.7),
                width_value * 0.6,
            ) + 6.0
            dirty = dirty.adjusted(-margin, -margin, margin, margin)
            bounds = dirty if bounds is None else bounds.united(dirty)

        painter.end()
        self._stroke_uniform_bounds = bounds
        return bounds

    def _start_paint_session(self, event) -> None:
        self._push_history()
        self.drawing = True
        origin = QPointF(event.position())
        self.last_point = QPointF(origin)
        now = time.time()
        self.last_time = now
        self._reset_brush_tracking()
        try:
            seed = time.time_ns() ^ (hash((origin.x(), origin.y())) << 1)
        except AttributeError:
            seed = int(time.time() * 1_000_000) ^ hash((origin.x(), origin.y()))
        self._stroke_rng = random.Random(seed)
        self._stroke_points.append(QPointF(origin))
        self._stroke_timestamps.append(now)
        self._stroke_last_midpoint = QPointF(origin)
        self._stroke_filter_point = QPointF(origin)
        self.last_width = max(1.0, float(self.pen_size) * 0.4)
        self.shape_start_point = event.pos() if self.mode == "shape" else None
        if self.mode == "shape":
            self._last_preview_bounds = None
        self._eraser_last_point = event.pos() if self.mode == "eraser" else None
        if self.mode == "brush":
            self._stroke_uniform_active = bool(self._brush_opacity < 255)
            if self._stroke_uniform_active:
                if (
                    self._stroke_uniform_canvas is None
                    or self._stroke_uniform_canvas.size() != self.canvas.size()
                ):
                    self._stroke_uniform_canvas = QPixmap(self.canvas.size())
                self._release_uniform_painter()
                self._stroke_uniform_canvas.fill(Qt.GlobalColor.transparent)
            else:
                self._ensure_brush_painter()
            config = get_pen_style_config(self.pen_style)
            base_width = self._effective_brush_width()
            self.last_width = max(1.0, base_width * config.target_min_factor)
            self._refresh_pen_alpha_state()
            self._update_brush_pen_appearance(base_width, self._active_fade_max)
            self._stroke_target_width = float(self.last_width)
            self._stroke_smoothed_target = float(self.last_width)
            self._stroke_width_velocity = 0.0
        elif self.mode == "eraser":
            self._ensure_eraser_painter()

    def _finalize_paint_session(self, release_pos: QPoint) -> Optional[Union[QRect, QRectF]]:
        dirty_region: Optional[Union[QRect, QRectF]] = None
        if self.mode == "shape" and self.current_shape:
            dirty_region = self._draw_shape_final(release_pos)
        self.drawing = False
        self.shape_start_point = None
        if self.mode == "eraser":
            self._eraser_last_point = None
            self._release_eraser_painter()
        elif self.mode == "brush":
            tail_dirty = self._finish_brush_stroke()
            if tail_dirty is not None:
                dirty_region = tail_dirty
            if getattr(self, "_stroke_uniform_active", False) and self._stroke_uniform_canvas is not None:
                self._release_uniform_painter()
                redraw_bounds = self._redraw_uniform_stroke()
                if redraw_bounds is not None and not redraw_bounds.isNull():
                    if dirty_region is None:
                        dirty_region = redraw_bounds
                    else:
                        dirty_region = dirty_region.united(redraw_bounds)
                elif self._stroke_uniform_bounds is not None and not self._stroke_uniform_bounds.isNull():
                    if dirty_region is None:
                        dirty_region = self._stroke_uniform_bounds
                    else:
                        dirty_region = dirty_region.united(self._stroke_uniform_bounds)
                painter = QPainter(self.canvas)
                painter.setRenderHint(QPainter.RenderHint.Antialiasing)
                painter.setCompositionMode(QPainter.CompositionMode.CompositionMode_SourceOver)
                painter.drawPixmap(0, 0, self._stroke_uniform_canvas)
                painter.end()
                self._stroke_uniform_canvas.fill(Qt.GlobalColor.transparent)
            self._reset_brush_tracking()
            self._release_brush_painter()
        return dirty_region

    def mousePressEvent(self, e) -> None:
        global_point = e.globalPosition().toPoint()
        inside_toolbar = self._toolbar_contains_global(global_point)

        if (
            not inside_toolbar
            and self.mode not in {"cursor", "region_erase"}
            and e.button() in (Qt.MouseButton.LeftButton, Qt.MouseButton.RightButton)
        ):
            self._nav_pointer_button = e.button()
            self._nav_pointer_press_pos = QPointF(e.position())
            self._nav_pointer_press_global = QPointF(e.globalPosition())
            self._nav_pointer_press_modifiers = e.modifiers()
            self._nav_pointer_started_draw = False
            if self.mode != "region_erase":
                self._set_navigation_reason("pointer", True)
            self.cancel_pending_tool_restore()
            self._cancel_navigation_cursor_hold()

        # 如果需要穿透，则不处理画笔相关逻辑
        if (
            e.button() == Qt.MouseButton.LeftButton
            and self.mode != "cursor"
            and not self.navigation_active
        ):
            self._ensure_keyboard_capture()
            if self.mode == "region_erase":
                self._begin_region_selection(e)
                self.update_cursor()
            else:
                self._start_paint_session(e)
            self.raise_toolbar()
            e.accept()
        super().mousePressEvent(e)
        # 白板 + 光标模式：点击后 WPS 全屏窗口可能抢占 Z 序，导致工具条看似“消失”。
        if self.whiteboard_active and self.mode == "cursor" and not inside_toolbar:
            try:
                QTimer.singleShot(0, self.raise_toolbar)
            except Exception:
                try:
                    self.raise_toolbar()
                except Exception:
                    pass

    def mouseMoveEvent(self, e) -> None:
        global_pos = e.globalPosition().toPoint()
        hovering_toolbar = self._toolbar_contains_global(global_pos)

        # [调试日志] 追踪 overlay mouseMoveEvent 调用
        if self.drawing:
            toolbar_geom = getattr(self.toolbar, 'geometry', lambda: None)() if hasattr(self, 'toolbar') and self.toolbar else None
            logger.debug(f"[Overlay] mouseMoveEvent: local_pos=({e.pos().x()}, {e.pos().y()}), global_pos=({global_pos.x()}, {global_pos.y()}), hovering_toolbar={hovering_toolbar}, toolbar_geom={toolbar_geom}")

        if hovering_toolbar and not self._toolbar_hovering:
            self._toolbar_hovering = True
            self.handle_toolbar_enter()
        elif (not hovering_toolbar) and self._toolbar_hovering:
            self._toolbar_hovering = False
            self.handle_toolbar_leave()
        if (
            self._nav_pointer_button == Qt.MouseButton.LeftButton
            and not self._nav_pointer_started_draw
            and (e.buttons() & Qt.MouseButton.LeftButton)
            and self.mode != "region_erase"
        ):
            delta = e.position() - self._nav_pointer_press_pos
            if abs(delta.x()) >= 2 or abs(delta.y()) >= 2:
                self._nav_pointer_started_draw = True
                self._set_navigation_reason("pointer", False)
                synthetic_press = QMouseEvent(
                    QEvent.Type.MouseButtonPress,
                    QPointF(self._nav_pointer_press_pos),
                    QPointF(self._nav_pointer_press_global),
                    Qt.MouseButton.LeftButton,
                    Qt.MouseButton.LeftButton,
                    self._nav_pointer_press_modifiers,
                )
                self._ensure_keyboard_capture()
                self._start_paint_session(synthetic_press)
                self.raise_toolbar()
                self._nav_pointer_button = Qt.MouseButton.NoButton
        if (
            self.mode == "region_erase"
            and self._region_previewing
            and (e.buttons() & Qt.MouseButton.LeftButton)
        ):
            self._update_region_selection_preview(e.pos())
            self.raise_toolbar()
        if self.drawing and self.mode != "cursor":
            p = e.pos()
            pf = e.position()
            dirty_region = None
            if self.mode == "brush":
                logger.debug(f"[Overlay] 调用 _draw_brush_line, pos=({p.x()}, {p.y()}), hovering_toolbar={hovering_toolbar}")
                dirty_region = self._draw_brush_line(pf)
            elif self.mode == "eraser":
                dirty_region = self._erase_at(p)
            elif self.mode == "shape" and self.current_shape:
                dirty_region = self._draw_shape_preview(p)
            self._apply_dirty_region(dirty_region)
            # [修复] 即使鼠标在工具栏区域，也继续绘画，不让事件中断
            if not hovering_toolbar:
                self.raise_toolbar()
        # [关键修复] 当鼠标在工具栏区域时，不处理事件，让其穿透到 toolbar
        # 这样 toolbar 才能接收鼠标事件并转发回 overlay
        if hovering_toolbar and self.drawing:
            # 鼠标在工具栏上且正在绘图：忽略事件，让 Qt 传递给 toolbar
            return
        super().mouseMoveEvent(e)

    def mouseReleaseEvent(self, e) -> None:
        global_point = e.globalPosition().toPoint()
        inside_toolbar = self._toolbar_contains_global(global_point)

        if e.button() == self._nav_pointer_button:
            self._set_navigation_reason("pointer", False)
            self._nav_pointer_button = Qt.MouseButton.NoButton
            self._nav_pointer_started_draw = False
        if e.button() == Qt.MouseButton.LeftButton and self._region_previewing:
            self._finalize_region_selection(e.pos())
            self.raise_toolbar()
            e.accept()
            super().mouseReleaseEvent(e)
            return
        # 如果需要穿透，则不处理画笔相关逻辑
        if e.button() == Qt.MouseButton.LeftButton and self.drawing:
            dirty_region = self._finalize_paint_session(e.pos())
            if dirty_region is not None:
                self._apply_dirty_region(dirty_region)
            self.raise_toolbar()
            e.accept()
        super().mouseReleaseEvent(e)
        if self.whiteboard_active and self.mode == "cursor" and not inside_toolbar:
            try:
                QTimer.singleShot(0, self.raise_toolbar)
            except Exception:
                try:
                    self.raise_toolbar()
                except Exception:
                    pass

    def _should_handle_navigation_key(self, key: int) -> bool:
        if key not in _QT_NAVIGATION_KEYS:
            return False
        target = self._resolve_control_target()
        if not target or not self._presentation_control_allowed(target, log=False):
            return False
        return True

    def keyPressEvent(self, e: QKeyEvent) -> None:
        key = e.key()
        wps_target: Optional[int] = None
        wps_forced = False
        if key in _QT_WPS_SLIDESHOW_KEYS:
            try:
                forced_target = self._resolve_wps_nav_target()
            except Exception:
                forced_target = None
            if forced_target:
                wps_target = forced_target
                wps_forced = True
            else:
                wps_target = self._resolve_control_target()
        if key in _QT_WPS_SLIDESHOW_KEYS and self._is_wps_raw_input_passthrough(wps_target):
            self._log_nav_trace(
                "key_passthrough",
                reason="foreground_passthrough",
                key=key,
                target=hex(wps_target) if wps_target else "0x0",
            )
            e.accept()
            return
        if key in _QT_WPS_SLIDESHOW_KEYS and self._wps_nav_hook_active and self._wps_hook_intercept_keyboard:
            self._log_nav_trace("key_swallow", reason="wps_hook_active", key=key)
            e.accept()
            return
        if key in _QT_WPS_SLIDESHOW_KEYS and self._wps_hook_recently_fired() and self._wps_hook_intercept_keyboard:
            self._log_nav_trace("key_swallow", reason="wps_hook_recent", key=key)
            e.accept()
            return
        is_wps_nav = bool(wps_target and (wps_forced or self._is_wps_slideshow_target(wps_target)))
        # WPS 放映：若启用全局拦截钩子，则吞掉本地事件，避免双触发。
        if key in _QT_WPS_SLIDESHOW_KEYS:
            if self.whiteboard_active:
                e.accept()
                return
            target = wps_target if wps_target is not None else self._resolve_control_target()
            if (
                target
                and is_wps_nav
                and self._presentation_control_allowed(target)
            ):
                if self._wps_nav_hook_active and self._wps_hook_intercept_keyboard:
                    self._log_nav_trace(
                        "key_swallow",
                        reason="wps_hook_active_target",
                        key=key,
                        target=hex(target),
                    )
                    e.accept()
                    return
        direction = 0
        if key in _QT_NAVIGATION_FORWARD_KEYS:
            direction = 1
        elif key in _QT_NAVIGATION_BACK_KEYS:
            direction = -1
        if direction and self._should_handle_navigation_key(key):
            if self.whiteboard_active:
                e.accept()
                return
            target = self._resolve_control_target()
            if not target or not self._presentation_control_allowed(target):
                super().keyPressEvent(e)
                return
            # 对于WPS，阻止Qt系统事件，只使用虚拟键处理
            if is_wps_nav:
                e.accept()
            else:
                super().keyPressEvent(e)
            is_auto = e.isAutoRepeat()
            if not is_auto:
                self._active_navigation_keys.add(key)
                self._set_navigation_reason("keyboard", True)
            origin_key = None if is_auto else key
            self._log_nav_trace(
                "key_nav",
                key=key,
                dir=direction,
                target=hex(target) if target else "0x0",
                hook_active=self._wps_nav_hook_active,
            )
            if direction > 0:
                self.go_to_next_slide(originating_key=origin_key, from_keyboard=True)
            else:
                self.go_to_previous_slide(originating_key=origin_key, from_keyboard=True)
            e.accept()
            return
        allow_cursor = (self.mode == "cursor" or self.navigation_active) and not self.whiteboard_active
        if self._forwarder and self._forwarder.forward_key(
            e,
            is_press=True,
            allow_cursor=allow_cursor,
        ):
            e.accept()
            return
        if key == Qt.Key.Key_Escape:
            self.set_mode("cursor")
            return
        super().keyPressEvent(e)

    def keyReleaseEvent(self, e: QKeyEvent) -> None:
        key = e.key()
        wps_target: Optional[int] = None
        wps_forced = False
        if key in _QT_WPS_SLIDESHOW_KEYS:
            try:
                forced_target = self._resolve_wps_nav_target()
            except Exception:
                forced_target = None
            if forced_target:
                wps_target = forced_target
                wps_forced = True
            else:
                wps_target = self._resolve_control_target()
        is_wps_nav = bool(wps_target and (wps_forced or self._is_wps_slideshow_target(wps_target)))
        if key in _QT_WPS_SLIDESHOW_KEYS and self._wps_nav_hook_active and self._wps_hook_intercept_keyboard:
            self._log_nav_trace("key_release_swallow", reason="wps_hook_active", key=key)
            e.accept()
            return
        if key in _QT_WPS_SLIDESHOW_KEYS and self._wps_hook_recently_fired() and self._wps_hook_intercept_keyboard:
            self._log_nav_trace("key_release_swallow", reason="wps_hook_recent", key=key)
            e.accept()
            return
        # WPS 放映：仅在启用全局拦截钩子时吞掉 KeyRelease，避免 KeyUp 二次响应。
        if key in _QT_WPS_SLIDESHOW_KEYS:
            target = wps_target if wps_target is not None else self._resolve_control_target()
            if (
                target
                and is_wps_nav
                and self._wps_nav_hook_active
                and self._wps_hook_intercept_keyboard
            ):
                e.accept()
                return
        if key in self._active_navigation_keys:
            if self.whiteboard_active:
                e.accept()
                return
            if not e.isAutoRepeat():
                self._release_keyboard_navigation_state(key)
            e.accept()
            return
        if self._should_handle_navigation_key(key):
            if self.whiteboard_active:
                e.accept()
                return
            e.accept()
            return
        allow_cursor = (self.mode == "cursor" or self.navigation_active) and not self.whiteboard_active
        if self._forwarder and self._forwarder.forward_key(
            e,
            is_press=False,
            allow_cursor=allow_cursor,
        ):
            e.accept()
            return
        super().keyReleaseEvent(e)

    def _style_profile_adjustment(
        self,
        *,
        cur_width: float,
        effective_base: float,
        min_width: float,
        max_width: float,
        speed_scale: float,
        curve_scale: float,
        pressure: float,
        tail_state: float,
        fade_alpha: int,
        prev_width: Optional[float] = None,
    ) -> Tuple[float, int]:
        fade_min = getattr(self, "_active_fade_min", 0)
        fade_max = getattr(self, "_active_fade_max", 255)

        def _clamp_width(value: float) -> float:
            return float(clamp(value, min_width, max_width))

        def _clamp_alpha(value: float) -> int:
            return int(clamp(value, fade_min, fade_max))
        stable = _clamp_width(cur_width)
        if prev_width is not None:
            easing = clamp(0.9 + speed_scale * 0.05, 0.9, 0.96)
            cur_width = _clamp_width(prev_width * easing + stable * (1.0 - easing))
            step_limit = max(0.12, effective_base * 0.004)
            delta = cur_width - prev_width
            if abs(delta) > step_limit:
                cur_width = _clamp_width(prev_width + math.copysign(step_limit, delta))
        else:
            cur_width = _clamp_width(cur_width * 0.2 + stable * 0.8)

        if tail_state > 0.0:
            tip = clamp(tail_state * 1.2, 0.0, 1.0)
            cur_width = _clamp_width(cur_width * (1.0 - 0.45 * tip) + min_width * 0.45 * tip)

        fade_alpha = _clamp_alpha(fade_max)
        return cur_width, fade_alpha

    def _draw_brush_line(self, cur: QPointF) -> Optional[QRectF]:
        now = time.time()
        cur_point = QPointF(cur)
        if not self._stroke_points:
            self._stroke_points.clear()
            self._stroke_timestamps.clear()
            self._stroke_points.append(QPointF(cur_point))
            self._stroke_timestamps.append(now)
            self.last_point = QPointF(cur_point)
            self._stroke_last_midpoint = QPointF(cur_point)
            self._stroke_filter_point = QPointF(cur_point)
            self.last_time = now
            if not getattr(self, "_stroke_uniform_active", False):
                self._ensure_brush_painter()
            return None

        painter: Optional[QPainter] = None
        if not getattr(self, "_stroke_uniform_active", False):
            painter = self._ensure_brush_painter()
        config = get_pen_style_config(self.pen_style)
        base_size = float(max(1.0, self.pen_base_size))
        effective_base = max(1.0, base_size * config.width_multiplier)
        last_point = QPointF(self._stroke_points[-1])
        filter_point = QPointF(self._stroke_filter_point) if self._stroke_filter_point else QPointF(last_point)
        smoothing = config.smoothing
        smoothed_x = filter_point.x() + (cur_point.x() - filter_point.x()) * smoothing
        smoothed_y = filter_point.y() + (cur_point.y() - filter_point.y()) * smoothing
        cur_point = QPointF(smoothed_x, smoothed_y)
        jitter_strength = float(getattr(config, "jitter_strength", 0.0) or 0.0)
        if jitter_strength > 0.0:
            rng = getattr(self, "_stroke_rng", None)
            if rng is None:
                rng = random.Random()
                self._stroke_rng = rng
            jitter_target = QPointF(rng.random() - 0.5, rng.random() - 0.5)
            prev_jitter = getattr(self, "_stroke_jitter_offset", QPointF())
            jitter_blend = 0.18 + min(0.32, jitter_strength * 0.12)
            jitter = QPointF(
                prev_jitter.x() + (jitter_target.x() - prev_jitter.x()) * jitter_blend,
                prev_jitter.y() + (jitter_target.y() - prev_jitter.y()) * jitter_blend,
            )
            self._stroke_jitter_offset = jitter
            jitter_scale = min(2.4, max(0.0, effective_base * 0.05 * jitter_strength))
            if jitter_scale > 0.0:
                cur_point = QPointF(
                    cur_point.x() + jitter.x() * jitter_scale,
                    cur_point.y() + jitter.y() * jitter_scale,
                )
        self._stroke_filter_point = QPointF(cur_point)

        self._stroke_points.append(cur_point)
        self._stroke_timestamps.append(now)
        if len(self._stroke_timestamps) < 2:
            return None

        elapsed = max(1e-4, now - self._stroke_timestamps[-2])
        distance = math.hypot(cur_point.x() - last_point.x(), cur_point.y() - last_point.y())
        self._stroke_total_length += distance
        if distance < 0.08 and elapsed < 0.012:
            return None
        speed = distance / elapsed
        self._stroke_speed = self._stroke_speed * 0.72 + speed * 0.28

        curvature = 0.0
        if len(self._stroke_points) >= 3:
            p0 = self._stroke_points[-3]
            p1 = self._stroke_points[-2]
            p2 = self._stroke_points[-1]
            v1x, v1y = p1.x() - p0.x(), p1.y() - p0.y()
            v2x, v2y = p2.x() - p1.x(), p2.y() - p1.y()
            denom = math.hypot(v1x, v1y) * math.hypot(v2x, v2y)
            if denom > 1e-5:
                curvature = abs(v1x * v2y - v1y * v2x) / denom

        travel = distance / max(1.0, effective_base)
        pressure = min(
            1.0,
            (now - self.last_time) * config.pressure_time_weight + travel * config.travel_weight,
        )
        self.last_time = now

        speed_scale = 1.0 / (
            1.0
            + self._stroke_speed
            / (effective_base * config.speed_base_multiplier + config.speed_base_offset)
        )
        curve_scale = min(1.0, curvature * effective_base * config.curve_sensitivity)
        target_w = effective_base * (
            config.target_min_factor
            + config.target_speed_factor * speed_scale
            + config.target_curve_factor * curve_scale
        )
        target_w *= 1.0 + pressure * config.pressure_factor
        min_w = effective_base * config.target_min_factor
        max_w = effective_base * max(config.target_min_factor, config.target_max_factor)
        target_w = max(min_w, min(max_w, target_w))
        gamma = float(getattr(config, "width_gamma", 1.0) or 1.0)
        if abs(gamma - 1.0) > 1e-3 and (max_w - min_w) > 1e-3:
            norm = (target_w - min_w) / (max_w - min_w)
            norm = max(0.0, min(1.0, norm))
            norm = norm ** max(0.2, min(5.0, gamma))
            target_w = min_w + norm * (max_w - min_w)

        prev_target = getattr(self, "_stroke_target_width", self.last_width)
        blend = max(0.0, min(1.0, getattr(config, "target_blend", 0.3)))
        target_w = prev_target * (1.0 - blend) + target_w * blend
        target_step_limit = max(0.12, effective_base * config.width_change_limit)
        delta_target = target_w - prev_target
        if abs(delta_target) > target_step_limit:
            target_w = prev_target + math.copysign(target_step_limit, delta_target)
        target_w = max(min_w, min(max_w, target_w))
        self._stroke_target_width = target_w

        responsiveness = clamp(getattr(config, "target_responsiveness", 0.35), 0.05, 0.95)
        smoothed_prev = getattr(self, "_stroke_smoothed_target", self.last_width)
        smoothed_target = smoothed_prev + (target_w - smoothed_prev) * responsiveness
        smoothed_target = float(clamp(smoothed_target, min_w, max_w))
        self._stroke_smoothed_target = smoothed_target

        velocity = getattr(self, "_stroke_width_velocity", 0.0)
        accel = clamp(getattr(config, "width_accel", 0.18), 0.02, 0.6)
        velocity += (smoothed_target - self.last_width) * accel
        damping = clamp(getattr(config, "width_velocity_damping", 0.7), 0.4, 0.95)
        velocity *= damping
        velocity_limit = max(
            0.06,
            target_step_limit * 0.6,
            effective_base * clamp(getattr(config, "width_velocity_limit", 0.22), 0.05, 0.6),
        )
        velocity = float(clamp(velocity, -velocity_limit, velocity_limit))
        cur_w = self.last_width + velocity
        memory = clamp(getattr(config, "width_memory", 0.9), 0.6, 0.985)
        cur_w = float(clamp(self.last_width * memory + cur_w * (1.0 - memory), min_w, max_w))
        self._stroke_width_velocity = velocity
        entry_strength = float(getattr(config, "entry_taper_strength", 0.0) or 0.0)
        entry_distance = float(getattr(config, "entry_taper_distance", 0.0) or 0.0)
        if entry_strength > 0.0 and entry_distance > 0.0:
            entry_progress = clamp(self._stroke_total_length / max(4.0, entry_distance), 0.0, 1.0)
            entry_curve = clamp(float(getattr(config, "entry_taper_curve", 1.0) or 1.0), 0.3, 4.0)
            entry_mix = entry_progress ** entry_curve
            entry_weight = clamp(entry_strength * (1.0 - entry_mix), 0.0, 1.0)
            if entry_weight > 0.0:
                cur_w = max(min_w, cur_w * (1.0 - entry_weight) + min_w * entry_weight)

        tail_strength = float(getattr(config, "exit_taper_strength", 0.0) or 0.0)
        tail_speed_threshold = float(getattr(config, "exit_taper_speed", 0.0) or 0.0)
        tail_curve = clamp(float(getattr(config, "exit_taper_curve", 1.0) or 1.0), 0.3, 4.0)
        tail_state = float(getattr(self, "_stroke_tail_state", 0.0))
        if tail_strength > 0.0 and tail_speed_threshold > 0.0:
            tail_speed_norm = clamp(speed / max(20.0, tail_speed_threshold), 0.0, 1.0)
            tail_target = (1.0 - tail_speed_norm) ** tail_curve
            tail_state = tail_state * 0.62 + tail_target * 0.38
        else:
            tail_state *= 0.72
        tail_state = float(clamp(tail_state, 0.0, 1.0))
        self._stroke_tail_state = tail_state
        if tail_strength > 0.0 and tail_state > 0.0:
            tail_weight = clamp(tail_strength * tail_state, 0.0, 1.0)
            if tail_weight > 0.0:
                cur_w = max(min_w, cur_w * (1.0 - tail_weight) + min_w * tail_weight)
        cur_w = float(clamp(cur_w, min_w, max_w))

        last_mid = QPointF(self._stroke_last_midpoint) if self._stroke_last_midpoint else QPointF(last_point)
        current_mid = (last_point + cur_point) / 2.0

        path = QPainterPath(last_mid)
        path.quadTo(last_point, current_mid)

        fade_candidate = (
            config.fade_speed_weight * speed_scale
            + config.fade_curve_weight * curve_scale
        ) * max(0.0, self._active_alpha_scale)
        fade_alpha = int(clamp(fade_candidate, self._active_fade_min, self._active_fade_max))
        tail_alpha_fade = float(getattr(config, "tail_alpha_fade", 0.0) or 0.0)
        if tail_alpha_fade > 0.0 and tail_state > 0.0:
            fade_alpha = int(
                fade_alpha * (1.0 - clamp(tail_alpha_fade * tail_state, 0.0, 0.9))
            )
        fade_alpha = int(clamp(fade_alpha, self._active_fade_min, self._active_fade_max))
        cur_w, fade_alpha = self._style_profile_adjustment(
            cur_width=cur_w,
            effective_base=effective_base,
            min_width=min_w,
            max_width=max_w,
            speed_scale=speed_scale,
            curve_scale=curve_scale,
            pressure=pressure,
            tail_state=tail_state,
            fade_alpha=fade_alpha,
            prev_width=self.last_width,
        )
        self._update_brush_pen_appearance(cur_w, fade_alpha)
        if getattr(self, "_stroke_uniform_active", False) and self._stroke_uniform_canvas is not None:
            self._stroke_segments.append(
                (QPointF(last_mid), QPointF(last_point), QPointF(current_mid), float(cur_w))
            )
            uniform_painter = self._ensure_uniform_painter()
            if uniform_painter is None:
                return None
            shadow_alpha_current = (
                self._brush_shadow_pen.color().alpha() if self._brush_shadow_pen is not None else 0
            )
            if shadow_alpha_current > 0:
                uniform_painter.setPen(self._brush_shadow_pen)
                uniform_painter.drawPath(path)
            pen_alpha_current = self._brush_pen.color().alpha() if self._brush_pen is not None else 0
            if pen_alpha_current > 0:
                uniform_painter.setPen(self._brush_pen)
                uniform_painter.drawPath(path)

            dirty = path.boundingRect()
            overlay_factor = max(
                config.shadow_width_scale,
                1.0 + config.feather_strength,
                1.0 + config.noise_strength * 0.45,
            )
            margin = max(cur_w * (0.6 + (overlay_factor - 1.0) * 0.7), cur_w * 0.6) + 6.0
            dirty = dirty.adjusted(-margin, -margin, margin, margin)
            if self._stroke_uniform_bounds is None:
                self._stroke_uniform_bounds = QRectF(dirty)
            else:
                self._stroke_uniform_bounds = self._stroke_uniform_bounds.united(dirty)
            self.last_point = QPointF(cur_point)
            self._stroke_last_midpoint = QPointF(current_mid)
            self.last_width = cur_w
            return dirty
        else:
            shadow_alpha_current = (
                self._brush_shadow_pen.color().alpha() if self._brush_shadow_pen is not None else 0
            )
            if shadow_alpha_current > 0 and painter is not None:
                painter.setPen(self._brush_shadow_pen)
                painter.drawPath(path)
            pen_alpha_current = self._brush_pen.color().alpha() if self._brush_pen is not None else 0
            if pen_alpha_current > 0 and painter is not None:
                painter.setPen(self._brush_pen)
                painter.drawPath(path)

        self.last_point = QPointF(cur_point)
        self._stroke_last_midpoint = QPointF(current_mid)
        self.last_width = cur_w

        dirty = path.boundingRect()
        overlay_factor = max(
            config.shadow_width_scale,
            1.0 + config.feather_strength,
            1.0 + config.noise_strength * 0.45,
        )
        margin = max(cur_w * (0.6 + (overlay_factor - 1.0) * 0.7), cur_w * 0.6) + 6.0
        return dirty.adjusted(-margin, -margin, margin, margin)

    def _erase_at(self, pos) -> Optional[QRectF]:
        current = QPointF(pos) if isinstance(pos, QPointF) else QPointF(QPoint(pos))
        if isinstance(self._eraser_last_point, QPoint):
            start_point = QPointF(self._eraser_last_point)
            distance = math.hypot(current.x() - start_point.x(), current.y() - start_point.y())
        else:
            start_point = QPointF(current)
            distance = 0.0

        base_eraser = float(clamp(self.eraser_size, 1.0, 50.0))
        radius = max(4.0, base_eraser)
        target_width = max(8.0, radius * 2.0)
        if abs(target_width - self._eraser_stroker_width) > 0.5:
            self._eraser_stroker.setWidth(target_width)
            self._eraser_stroker_width = target_width

        path = QPainterPath(start_point)
        if distance >= 0.35:
            path.lineTo(current)

        erase_path = QPainterPath()
        if distance >= 0.35:
            erase_path = self._eraser_stroker.createStroke(path)
        erase_path.addEllipse(current, radius, radius)
        if distance >= 0.35:
            erase_path.addEllipse(start_point, radius, radius)

        painter = self._ensure_eraser_painter()
        painter.fillPath(erase_path, QColor(0, 0, 0, 0))

        self._eraser_last_point = current.toPoint()

        dirty = erase_path.boundingRect()
        if dirty.isNull():
            return None
        margin = max(radius * 0.5, 6.0)
        return dirty.adjusted(-margin, -margin, margin, margin)

    def _draw_shape_preview(self, end_point) -> Optional[QRect]:
        if not self.shape_start_point:
            return None
        self.temp_canvas.fill(Qt.GlobalColor.transparent)
        p = QPainter(self.temp_canvas)
        p.setRenderHint(QPainter.RenderHint.Antialiasing)
        pen = QPen(self.pen_color, self.pen_size)
        if self.current_shape and "dashed" in self.current_shape:
            pen.setStyle(Qt.PenStyle.DashLine)
        p.setPen(pen)
        self._draw_shape(p, self.shape_start_point, end_point)
        p.end()
        self.raise_toolbar()
        bounds = self._shape_dirty_bounds(self.shape_start_point, end_point, self.pen_size)
        if bounds is not None and self._last_preview_bounds is not None:
            bounds = bounds.united(self._last_preview_bounds)
        self._last_preview_bounds = bounds
        return bounds

    def _draw_shape_final(self, end_point) -> Optional[QRect]:
        if not self.shape_start_point:
            return None
        bounds = self._shape_dirty_bounds(self.shape_start_point, end_point, self.pen_size)
        p = QPainter(self.canvas)
        p.setRenderHint(QPainter.RenderHint.Antialiasing)
        pen = QPen(self.pen_color, self.pen_size)
        if self.current_shape and "dashed" in self.current_shape:
            pen.setStyle(Qt.PenStyle.DashLine)
        p.setPen(pen)
        self._draw_shape(p, self.shape_start_point, end_point)
        p.end()
        self.temp_canvas.fill(Qt.GlobalColor.transparent)
        self.raise_toolbar()
        last_bounds = self._last_preview_bounds
        self._last_preview_bounds = None
        if bounds is not None and last_bounds is not None:
            bounds = bounds.united(last_bounds)
        return bounds

    def _draw_shape(self, painter: QPainter, start_point, end_point) -> None:
        rect = QRect(start_point, end_point)
        shape = (self.current_shape or "line").replace("dashed_", "")
        if shape == "rect":
            painter.drawRect(rect.normalized())
        elif shape == "rect_fill":
            normalized = rect.normalized()
            painter.save()
            painter.setBrush(QBrush(QColor(self.pen_color)))
            painter.drawRect(normalized)
            painter.restore()
        elif shape == "circle":
            painter.drawEllipse(rect.normalized())
        else:
            painter.drawLine(start_point, end_point)

    def paintEvent(self, e) -> None:
        p = QPainter(self)
        if self.whiteboard_active:
            p.fillRect(self.rect(), self.whiteboard_color)
        else:
            p.fillRect(self.rect(), QColor(0, 0, 0, 1))
        p.drawPixmap(0, 0, self.canvas)
        if (
            getattr(self, "_stroke_uniform_active", False)
            and self.mode == "brush"
            and self._stroke_uniform_canvas is not None
        ):
            p.drawPixmap(0, 0, self._stroke_uniform_canvas)
        if (self.drawing and self.mode == "shape") or self._region_previewing:
            p.drawPixmap(0, 0, self.temp_canvas)
        p.end()

    def showEvent(self, e) -> None:
        super().showEvent(e)
        self.raise_toolbar()

    def closeEvent(self, e) -> None:
        self._release_canvas_painters()
        self.save_settings()
        self.save_window_position()
        super().closeEvent(e)


# ---------- 语音 ----------
class TTSManager(QObject):
    """简单封装语音播报，优先使用 pyttsx3，必要时回退到 PowerShell。"""

    def __init__(
        self,
        preferred_voice_id: str = "",
        preferred_output_id: str = "",
        preferred_engine: str = "pyttsx3",
        parent: Optional[QObject] = None,
    ) -> None:
        super().__init__(parent)
        self.engine = None
        self.voice_ids: List[str] = []
        self.default_voice_id = ""
        self.current_voice_id = ""
        self.output_ids: List[str] = []
        self.default_output_id = ""
        self.current_output_id = ""
        self.supports_output_selection = False
        self.failure_reason = ""
        self.failure_suggestions: List[str] = []
        self.supports_voice_selection = False
        self._mode: str = "none"
        self._powershell_path = ""
        self._powershell_busy = False
        self._sapi_voice = None
        self._sapi_busy = False
        self._sapi_outputs: Dict[str, Any] = {}
        self._output_descriptions: Dict[str, str] = {}
        self._queue: Queue[str] = Queue()
        self._timer = QTimer(self)
        self._timer.timeout.connect(self._pump)
        self._preferred_voice_id = preferred_voice_id
        self._preferred_output_id = preferred_output_id
        self._preferred_engine = preferred_engine.strip().lower() or "pyttsx3"
        self._initialized = False
        self._closing = False

    def _clear_queue(self) -> None:
        while not self._queue.empty():
            try:
                self._queue.get_nowait()
            except Empty:
                break

    def _schedule_shutdown(self) -> None:
        try:
            QTimer.singleShot(0, self, self.shutdown)
        except Exception as exc:
            logger.debug("Failed to schedule TTS shutdown: %s", exc)

    def _lazy_init(self) -> None:
        if self._closing or is_app_closing():
            return
        if self._initialized:
            return
        self._initialized = True
        missing_reason = ""
        preferred = self._preferred_engine

        def _try_pyttsx3() -> bool:
            nonlocal missing_reason
            if pyttsx3 is None:
                missing_reason = "未检测到 pyttsx3 模块"
                return False
            try:
                init_kwargs = {"driverName": "sapi5"} if sys.platform == "win32" else {}
                self.engine = pyttsx3.init(**init_kwargs)
                voices = self.engine.getProperty("voices") or []
                self.voice_ids = [v.id for v in voices if getattr(v, "id", None)]
                if not self.voice_ids:
                    self._record_failure("未检测到任何可用的发音人")
                    self.engine = None
                    return False
                self.default_voice_id = self.voice_ids[0]
                preferred_voice = self._preferred_voice_id or self.default_voice_id
                self.current_voice_id = preferred_voice if preferred_voice in self.voice_ids else self.default_voice_id
                if self.current_voice_id:
                    try:
                        self.engine.setProperty("voice", self.current_voice_id)
                    except Exception as exc:
                        self._record_failure("无法设置默认发音人", exc)
                        self.engine = None
                        return False
                if self.engine is not None:
                    self.supports_voice_selection = True
                    self._mode = "pyttsx3"
                    # pyttsx3 不支持在此路径下切换输出设备，交给系统默认设备。
                    self.supports_output_selection = False
                    self.engine.startLoop(False)
                    self._timer.start(100)
                    return True
            except Exception as exc:
                self._record_failure("初始化语音引擎失败", exc)
                self.engine = None
            return False

        def _try_win32com() -> bool:
            self._init_win32com_fallback()
            return self.available and self._mode == "win32com"

        def _try_powershell() -> bool:
            self._init_powershell_fallback()
            return self.available and self._mode == "powershell"

        # 根据用户偏好选择初始化顺序
        if preferred == "sapi":
            if _try_win32com():
                return
            if _try_pyttsx3():
                return
        else:
            if _try_pyttsx3():
                return
            if _try_win32com():
                return

        if _try_powershell():
            return

        if missing_reason:
            if self.failure_reason:
                if missing_reason not in self.failure_reason:
                    self.failure_reason = f"{self.failure_reason}；{missing_reason}"
            else:
                self.failure_reason = missing_reason
        if not self.failure_reason:
            self.failure_reason = "未检测到可用的语音播报方式"
        env_reason, env_suggestions = detect_speech_environment_issues(force_refresh=True)
        if env_reason:
            if env_reason not in self.failure_reason:
                self.failure_reason = f"{self.failure_reason}；{env_reason}" if self.failure_reason else env_reason
        if env_suggestions:
            combined = list(self.failure_suggestions)
            combined.extend(env_suggestions)
            self.failure_suggestions = dedupe_strings(combined)

    @property
    def available(self) -> bool:
        self._lazy_init()
        return self._mode in {"pyttsx3", "win32com", "powershell"}

    def diagnostics(self) -> tuple[str, List[str]]:
        self._lazy_init()
        reason = self.failure_reason
        suggestions = list(self.failure_suggestions)
        env_reason, env_suggestions = detect_speech_environment_issues()
        if env_reason:
            if reason:
                if env_reason not in reason:
                    reason = f"{reason}；{env_reason}"
            else:
                reason = env_reason
        suggestions.extend(env_suggestions)
        return reason, dedupe_strings(suggestions)

    def _init_win32com_fallback(self) -> None:
        if sys.platform != "win32" or not WIN32COM_AVAILABLE:
            return
        try:
            speaker = win32com.client.Dispatch("SAPI.SpVoice")  # type: ignore[attr-defined]
            voices = list(getattr(speaker, "GetVoices", lambda: [])())  # type: ignore[call-arg]
            outputs = list(getattr(speaker, "GetAudioOutputs", lambda: [])())  # type: ignore[call-arg]
        except Exception as exc:
            self._record_failure("初始化 SAPI 语音失败", exc)
            return
        voice_ids: List[str] = []
        for v in voices:
            vid = getattr(v, "Id", None)
            if vid:
                voice_ids.append(str(vid))
        output_ids: List[str] = []
        output_map: Dict[str, Any] = {}
        output_descriptions: Dict[str, str] = {}
        for out in outputs:
            oid = getattr(out, "Id", None)
            if not oid:
                continue
            oid_str = str(oid)
            output_ids.append(oid_str)
            output_map[oid_str] = out
            try:
                desc = str(out.GetDescription())
            except Exception:
                desc = ""
            output_descriptions[oid_str] = desc
        if not voice_ids:
            self._record_failure("未检测到任何可用的发音人")
            return
        self.voice_ids = voice_ids
        self.default_voice_id = voice_ids[0]
        preferred = self._preferred_voice_id or self.default_voice_id
        self.current_voice_id = preferred if preferred in voice_ids else self.default_voice_id
        try:
            if self.current_voice_id:
                for v in voices:
                    if str(getattr(v, "Id", "")) == self.current_voice_id:
                        speaker.Voice = v  # type: ignore[attr-defined]
                        break
        except Exception:
            pass
        self.output_ids = output_ids
        self._sapi_outputs = output_map
        self._output_descriptions = output_descriptions
        if self.output_ids:
            self.default_output_id = self.output_ids[0]
            preferred_out = self._preferred_output_id or self.default_output_id
            self.current_output_id = preferred_out if preferred_out in self.output_ids else self.default_output_id
            self.supports_output_selection = True
            self._apply_sapi_output_device(speaker)
        self._sapi_voice = speaker
        self.supports_voice_selection = True
        self.failure_reason = ""
        self.failure_suggestions = []
        self._mode = "win32com"
        self._timer.start(60)

    def _init_powershell_fallback(self) -> None:
        if sys.platform != "win32":
            return
        path = _find_powershell_executable()
        if not path:
            if not self.failure_reason:
                self._record_failure("未检测到 PowerShell，可用的语音播报方式受限")
            return
        self._powershell_path = os.path.abspath(path)
        ps_ok, ps_reason = _probe_powershell_speech_runtime(self._powershell_path)
        if not ps_ok:
            message = ps_reason or "PowerShell 语音环境检测失败"
            self._record_failure(message)
            return
        self.engine = object()
        self.voice_ids = []
        self.default_voice_id = ""
        self.current_voice_id = ""
        self.supports_voice_selection = False
        self.failure_reason = ""
        self.failure_suggestions = []
        self._mode = "powershell"
        self._timer.start(80)

    def _record_failure(self, fallback: str, exc: Optional[Exception] = None) -> None:
        message = ""
        if exc is not None:
            message = str(exc).strip()
        if message and message not in fallback:
            reason = f"{fallback}：{message}"
        else:
            reason = fallback
        self.failure_reason = reason
        suggestions: List[str] = []
        lower = message.lower()
        if "comtypes" in lower:
            suggestions.append("请安装 comtypes（pip install comtypes）后重新启动程序。")
        if "pywin32" in lower or "win32" in lower:
            suggestions.append("请安装 pywin32（pip install pywin32）后重新启动程序。")
        platform_hint = []
        if sys.platform == "win32":
            platform_hint.append("请确认 Windows 已启用 SAPI5 中文语音包。")
        elif sys.platform == "darwin":
            platform_hint.append("请在系统“辅助功能 -> 语音”中启用所需的语音包。")
        else:
            platform_hint.append("请确保系统已安装可用的语音引擎（如 espeak）并重新启动程序。")
        platform_hint.append("可尝试重新安装 pyttsx3 或检查语音服务状态后重启软件。")
        for hint in platform_hint:
            if hint not in suggestions:
                suggestions.append(hint)
        self.failure_suggestions = suggestions

    def _apply_sapi_output_device(self, speaker: Any) -> None:
        if not self.supports_output_selection or speaker is None:
            return
        target_id = self.current_output_id or self.default_output_id
        token = None
        if target_id and target_id in self._sapi_outputs:
            token = self._sapi_outputs.get(target_id)
        elif self._sapi_outputs:
            token = next(iter(self._sapi_outputs.values()))
        if token is None:
            return
        try:
            speaker.AudioOutput = token  # type: ignore[attr-defined]
        except Exception:
            return

    def set_voice(self, voice_id: str) -> None:
        self._lazy_init()
        if not self.supports_voice_selection:
            return
        if voice_id in self.voice_ids:
            self.current_voice_id = voice_id
            if self.engine:
                try:
                    self.engine.setProperty("voice", voice_id)
                except Exception:
                    pass

    def set_output(self, output_id: str) -> None:
        self._lazy_init()
        if not self.supports_output_selection:
            return
        if output_id in self.output_ids:
            self.current_output_id = output_id
            if self._mode == "win32com" and self._sapi_voice is not None:
                self._apply_sapi_output_device(self._sapi_voice)

    def speak(self, text: str) -> None:
        self._lazy_init()
        if self._closing or is_app_closing():
            return
        if not self.available:
            return
        payload = str(text or "").strip()
        if not payload:
            return
        self._clear_queue()
        self._queue.put(payload)

    def _pump(self) -> None:
        if self._closing or is_app_closing():
            return
        if self._mode == "pyttsx3":
            if not self.engine:
                return
            try:
                text = self._queue.get_nowait()
                self.engine.stop()
                if self.current_voice_id:
                    self.engine.setProperty("voice", self.current_voice_id)
                self.engine.say(text)
            except Empty:
                pass
            try:
                self.engine.iterate()
            except Exception as exc:
                self._record_failure("语音引擎运行异常", exc)
                self.shutdown()
        elif self._mode == "win32com":
            if self._sapi_busy or self._sapi_voice is None:
                return
            try:
                text = self._queue.get_nowait()
            except Empty:
                return
            if self._closing or is_app_closing():
                self._clear_queue()
                return
            self._sapi_busy = True
            worker = threading.Thread(target=self._run_win32com_speech, args=(text,), daemon=True)
            worker.start()
        elif self._mode == "powershell":
            if self._powershell_busy:
                return
            try:
                text = self._queue.get_nowait()
            except Empty:
                return
            if self._closing or is_app_closing():
                self._clear_queue()
                return
            self._powershell_busy = True
            worker = threading.Thread(target=self._run_powershell_speech, args=(text,), daemon=True)
            worker.start()

    def _run_win32com_speech(self, text: str) -> None:
        try:
            if not text or self._sapi_voice is None:
                return
            if pythoncom is not None:
                try:
                    pythoncom.CoInitialize()
                except Exception:
                    pass
            try:
                if self.current_voice_id:
                    voices = getattr(self._sapi_voice, "GetVoices", lambda: [])()  # type: ignore[call-arg]
                    for v in voices:
                        if str(getattr(v, "Id", "")) == self.current_voice_id:
                            try:
                                self._sapi_voice.Voice = v  # type: ignore[attr-defined]
                            except Exception:
                                pass
                            break
            except Exception:
                pass
            try:
                self._apply_sapi_output_device(self._sapi_voice)
            except Exception:
                pass
            self._sapi_voice.Speak(text)  # type: ignore[attr-defined]
        except Exception as exc:
            self._record_failure("SAPI 语音播报失败", exc)
            self._schedule_shutdown()
        finally:
            self._sapi_busy = False
            if pythoncom is not None:
                try:
                    pythoncom.CoUninitialize()
                except Exception:
                    pass

    def _run_powershell_speech(self, text: str) -> None:
        try:
            if not text or not self._powershell_path:
                return
            payload = base64.b64encode(text.encode("utf-8")).decode("ascii")
            script = (
                "$msg = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('" + payload + "'));"
                "Add-Type -AssemblyName System.Speech;"
                "$sp = New-Object System.Speech.Synthesis.SpeechSynthesizer;"
                "$sp.Speak($msg);"
            )
            startupinfo = None
            if os.name == "nt":
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            subprocess.run(
                [self._powershell_path, "-NoLogo", "-NonInteractive", "-NoProfile", "-Command", script],
                check=True,
                timeout=30,
                startupinfo=startupinfo,
            )
        except Exception as exc:
            self._record_failure("PowerShell 语音播报失败", exc)
            self._schedule_shutdown()
        finally:
            self._powershell_busy = False

    def shutdown(self, *, permanent: bool = False) -> None:
        """停止当前语音引擎。

        - permanent=True：永久关闭该实例（通常用于应用退出），后续不再允许重新初始化。
        - permanent=False：仅释放当前资源，后续仍可在 speak() 时重新初始化（用于故障降级/恢复）。
        """

        if permanent or is_app_closing():
            self._closing = True
        self._clear_queue()
        if self._mode == "pyttsx3" and self.engine:
            try:
                self.engine.endLoop()
            except Exception:
                pass
            try:
                self.engine.stop()
            except Exception:
                pass
        self.engine = None
        self._sapi_voice = None
        self._mode = "none"
        self._powershell_busy = False
        self._sapi_busy = False
        self._timer.stop()
        self._initialized = False


# ---------- 点名/计时 ----------
class CountdownSettingsDialog(_EnsureOnScreenMixin, QDialog):
    """设置倒计时分钟和秒数的小窗口。"""

    def __init__(self, parent: Optional[QWidget], minutes: int, seconds: int) -> None:
        super().__init__(parent)
        self.setWindowTitle("设置倒计时")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.result: Optional[tuple[int, int]] = None

        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(6)

        minute_label = QLabel("分钟 (0-150，滑块 0-25):")
        ml = QHBoxLayout()
        ml.addWidget(minute_label)

        self.minutes_spin = QSpinBox()
        self.minutes_spin.setRange(0, 150)
        self.minutes_spin.setValue(max(0, min(150, minutes)))

        minute_slider = QSlider(Qt.Orientation.Horizontal)
        minute_slider.setRange(0, 25)
        minute_slider.setValue(min(self.minutes_spin.value(), minute_slider.maximum()))
        minute_slider.valueChanged.connect(self.minutes_spin.setValue)

        def sync(v: int, slider=minute_slider):
            if v <= slider.maximum():
                prev = slider.blockSignals(True)
                slider.setValue(v)
                slider.blockSignals(prev)
        self.minutes_spin.valueChanged.connect(sync)
        ml.addWidget(self.minutes_spin)
        layout.addLayout(ml)
        layout.addWidget(minute_slider)

        sl = QHBoxLayout()
        sl.addWidget(QLabel("秒 (0-59):"))

        self.seconds_spin = QSpinBox()
        self.seconds_spin.setRange(0, 59)
        self.seconds_spin.setValue(max(0, min(59, seconds)))

        second_slider = QSlider(Qt.Orientation.Horizontal)
        second_slider.setRange(0, 59)
        second_slider.setValue(self.seconds_spin.value())
        second_slider.valueChanged.connect(self.seconds_spin.setValue)
        self.seconds_spin.valueChanged.connect(second_slider.setValue)
        sl.addWidget(self.seconds_spin)
        layout.addLayout(sl)
        layout.addWidget(second_slider)

        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self._accept)
        buttons.rejected.connect(self.reject)
        style_dialog_buttons(
            buttons,
            {
                QDialogButtonBox.StandardButton.Ok: ButtonStyles.TOOLBAR,
                QDialogButtonBox.StandardButton.Cancel: ButtonStyles.TOOLBAR,
            },
            extra_padding=10,
            minimum_height=32,
            uniform_width=True,  # 确保确定和取消按钮宽度相同
        )
        ok_button = buttons.button(QDialogButtonBox.StandardButton.Ok)
        if ok_button is not None:
            ok_button.setText("确定")
        cancel_button = buttons.button(QDialogButtonBox.StandardButton.Cancel)
        if cancel_button is not None:
            cancel_button.setText("取消")
        layout.addWidget(buttons)
        self.setFixedSize(self.sizeHint())

    def _accept(self) -> None:
        self.result = (self.minutes_spin.value(), self.seconds_spin.value())
        self.accept()


class RollCallSettingsDialog(_EnsureOnScreenMixin, QDialog):
    """点名设置窗口：集中管理显示、照片、语音与倒计时提示等选项。"""

    _PHOTO_DURATION_CHOICES = [
        (0, "不自动关闭"),
        (3, "3 秒"),
        (5, "5 秒"),
        (10, "10 秒"),
        (15, "15 秒"),
    ]
    _REMOTE_KEY_CHOICES = [
        ("tab", "Tab键（切换超链接）"),
        ("b", "B键（长按黑屏）"),
    ]
    _ENGINE_CHOICES = [
        ("pyttsx3", "pyttsx3（默认，跟随系统输出）"),
        ("sapi", "SAPI（win32com，可选输出设备）"),
    ]
    _TIMER_SOUND_CHOICES = [
        ("gentle", "柔和铃声"),
        ("bell", "上课铃"),
        ("digital", "电子滴答"),
        ("buzz", "蜂鸣器"),
        ("urgent", "紧张倒计时"),
    ]
    _REMINDER_SOUND_CHOICES = [
        ("soft_beep", "轻柔提示"),
        ("ping", "清脆提示"),
        ("chime", "简洁钟声"),
        ("pulse", "节奏哔哔"),
        ("short_bell", "短铃提示"),
    ]
    _REMINDER_INTERVAL_CHOICES = [
        (1, "每 1 分钟"),
        (3, "每 3 分钟"),
        (5, "每 5 分钟"),
        (10, "每 10 分钟"),
    ]

    def __init__(
        self,
        parent: Optional[QWidget],
        *,
        show_id: bool,
        show_name: bool,
        show_photo: bool,
        photo_duration_seconds: int,
        photo_shared_class: str,
        available_classes: List[str],
        speech_enabled: bool,
        speech_engine: str,
        voice_ids: List[str],
        current_voice_id: str,
        output_ids: List[str],
        output_labels: Optional[Mapping[str, str]],
        current_output_id: str,
        timer_sound_enabled: bool,
        timer_sound_variant: str,
        timer_reminder_enabled: bool,
        timer_reminder_interval_minutes: int,
        timer_reminder_sound_variant: str,
        remote_presenter_enabled: bool,
        remote_presenter_key: str,
        sounddevice_available: bool,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle("点名设置")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.setSizeGripEnabled(True)
        self.setObjectName("rollCallSettingsDialog")
        self.setStyleSheet(
            """
            QDialog#rollCallSettingsDialog QCheckBox {
                spacing: 8px;
                color: #2d2d2d;
            }
            QDialog#rollCallSettingsDialog QCheckBox::indicator {
                width: 18px;
                height: 18px;
                border-radius: 5px;
                border: 1px solid #b0b6c0;
                background: #f6f8fa;
            }
            QDialog#rollCallSettingsDialog QCheckBox::indicator:hover {
                border-color: #64b5f6;
            }
            QDialog#rollCallSettingsDialog QCheckBox::indicator:checked {
                background: #4285f4;
                border-color: #4285f4;
            }
            QDialog#rollCallSettingsDialog QCheckBox::indicator:checked:disabled {
                background: #a8c7fa;
                border-color: #a8c7fa;
            }
            """
        )

        self._initial_voice_id = str(current_voice_id or "")
        self._initial_output_id = str(current_output_id or "")
        self._output_ids = list(output_ids)
        self._output_labels = output_labels or {}
        self._sounddevice_available = bool(sounddevice_available)

        root = QVBoxLayout(self)
        root.setContentsMargins(12, 12, 12, 12)
        root.setSpacing(10)

        tabs = QTabWidget(self)
        tabs.setDocumentMode(True)
        root.addWidget(tabs, 1)

        display_tab = QWidget()
        display_layout = QFormLayout(display_tab)
        display_layout.setContentsMargins(10, 10, 10, 10)
        display_layout.setSpacing(8)

        self.show_id_check = QCheckBox("学号")
        self.show_id_check.setTristate(False)
        self.show_id_check.setChecked(bool(show_id))
        self.show_name_check = QCheckBox("姓名")
        self.show_name_check.setTristate(False)
        self.show_name_check.setChecked(bool(show_name))
        self.show_photo_check = QCheckBox("显示照片")
        self.show_photo_check.setTristate(False)
        self.show_photo_check.setChecked(bool(show_photo))

        display_layout.addRow("学生信息:", _wrap_checkbox_row(self.show_id_check, self.show_name_check))
        display_layout.addRow("照片显示:", self.show_photo_check)

        self.photo_duration_slider = QSlider(Qt.Orientation.Horizontal)
        self.photo_duration_slider.setRange(0, 10)
        self.photo_duration_label = QLabel("")
        duration_value = int(max(0, min(10, photo_duration_seconds)))
        self.photo_duration_slider.setValue(duration_value)
        self.photo_duration_slider.valueChanged.connect(self._update_photo_duration_label)
        self._update_photo_duration_label()
        display_layout.addRow("照片显示时间:", self._wrap_slider_row(self.photo_duration_slider, self.photo_duration_label))

        self.photo_shared_combo = QComboBox()
        self.photo_shared_combo.setEditable(False)
        self.photo_shared_combo.addItem("各班使用各自文件夹中的照片", "")
        for name in available_classes:
            if name and name not in {"全部"}:
                self.photo_shared_combo.addItem(f"共用{name}照片文件夹", name)
        if photo_shared_class:
            _set_combo_value(self.photo_shared_combo, photo_shared_class)
        display_layout.addRow("照片文件夹", self.photo_shared_combo)

        tabs.addTab(display_tab, "显示/照片")

        speech_tab = QWidget()
        speech_layout = QFormLayout(speech_tab)
        speech_layout.setContentsMargins(10, 10, 10, 10)
        speech_layout.setSpacing(8)

        self.speech_enabled_check = QCheckBox("")
        self.speech_enabled_check.setTristate(False)
        self.speech_enabled_check.setChecked(bool(speech_enabled))
        speech_layout.addRow("启用语音播报:", self.speech_enabled_check)

        self.engine_combo = QComboBox()
        for key, label in self._ENGINE_CHOICES:
            self.engine_combo.addItem(label, key)
        _set_combo_value(self.engine_combo, speech_engine)
        speech_layout.addRow("语音引擎:", self.engine_combo)

        self.voice_combo = QComboBox()
        if voice_ids:
            for vid in voice_ids:
                self.voice_combo.addItem(vid, vid)
            _set_combo_value(self.voice_combo, current_voice_id)
        else:
            self.voice_combo.addItem("暂无可选发音人", "")
            self.voice_combo.setEnabled(False)
        speech_layout.addRow("发音人:", self.voice_combo)

        self.output_combo = QComboBox()
        if output_ids:
            for oid in output_ids:
                label = self._output_labels.get(oid, "") if isinstance(self._output_labels, Mapping) else ""
                text = label or oid
                self.output_combo.addItem(text, oid)
            _set_combo_value(self.output_combo, current_output_id)
        else:
            self.output_combo.addItem("当前引擎不支持输出选择", "")
            self.output_combo.setEnabled(False)
        speech_layout.addRow("输出设备:", self.output_combo)
        self.engine_combo.currentIndexChanged.connect(self._sync_output_controls)
        self._sync_output_controls()

        tabs.addTab(speech_tab, "语音播报")

        timer_tab = QWidget()
        timer_layout = QFormLayout(timer_tab)
        timer_layout.setContentsMargins(10, 10, 10, 10)
        timer_layout.setSpacing(8)

        self.timer_sound_check = QCheckBox("")
        self.timer_sound_check.setTristate(False)
        self.timer_sound_check.setChecked(bool(timer_sound_enabled))
        timer_layout.addRow("启用结束提示音:", self.timer_sound_check)

        self.timer_sound_combo = QComboBox()
        for key, label in self._TIMER_SOUND_CHOICES:
            self.timer_sound_combo.addItem(label, key)
        _set_combo_value(self.timer_sound_combo, timer_sound_variant)
        timer_layout.addRow("结束提示音样式:", self.timer_sound_combo)

        self.reminder_sound_check = QCheckBox("")
        self.reminder_sound_check.setTristate(False)
        self.reminder_sound_check.setChecked(bool(timer_reminder_enabled))
        timer_layout.addRow("启用中途提示音:", self.reminder_sound_check)

        self.reminder_sound_combo = QComboBox()
        for key, label in self._REMINDER_SOUND_CHOICES:
            self.reminder_sound_combo.addItem(label, key)
        _set_combo_value(self.reminder_sound_combo, timer_reminder_sound_variant)
        timer_layout.addRow("中途提示音样式:", self.reminder_sound_combo)

        self.reminder_interval_slider = QSlider(Qt.Orientation.Horizontal)
        self.reminder_interval_slider.setRange(1, 20)
        interval_value = int(timer_reminder_interval_minutes)
        if interval_value <= 0:
            interval_value = 3
        self.reminder_interval_slider.setValue(int(max(1, min(20, interval_value))))
        self.reminder_interval_label = QLabel("")
        self.reminder_interval_slider.valueChanged.connect(self._update_reminder_interval_label)
        self._update_reminder_interval_label()
        timer_layout.addRow("中途提示间隔:", self._wrap_slider_row(self.reminder_interval_slider, self.reminder_interval_label))

        if not self._sounddevice_available:
            self.timer_sound_combo.setToolTip("当前环境未检测到音频播放库，选择会保存，安装音频依赖后生效。")
            self.reminder_sound_combo.setToolTip("当前环境未检测到音频播放库，选择会保存，安装音频依赖后生效。")

        self.timer_sound_check.toggled.connect(self._sync_timer_controls)
        self.reminder_sound_check.toggled.connect(self._sync_timer_controls)
        self._sync_timer_controls()
        tabs.addTab(timer_tab, "倒计时提示")

        remote_tab = QWidget()
        remote_layout = QFormLayout(remote_tab)
        remote_layout.setContentsMargins(10, 10, 10, 10)
        remote_layout.setSpacing(8)

        self.remote_enabled_check = QCheckBox("")
        self.remote_enabled_check.setTristate(False)
        self.remote_enabled_check.setChecked(bool(remote_presenter_enabled))
        remote_layout.addRow("启用翻页笔遥控点名:", self.remote_enabled_check)

        self.remote_key_combo = QComboBox()
        for key, label in self._REMOTE_KEY_CHOICES:
            self.remote_key_combo.addItem(label, key)
        _set_combo_value(self.remote_key_combo, remote_presenter_key)
        remote_layout.addRow("翻页笔按键:", self.remote_key_combo)
        tabs.addTab(remote_tab, "遥控点名")

        footer = QHBoxLayout()
        footer.setContentsMargins(0, 0, 0, 0)
        footer.setSpacing(8)
        self.diagnostic_button = QPushButton("系统兼容性诊断")
        apply_button_style(
            self.diagnostic_button,
            ButtonStyles.TOOLBAR,
            height=recommended_control_height(self.diagnostic_button.font(), extra=10, minimum=32),
        )
        footer.addWidget(self.diagnostic_button, 0, Qt.AlignmentFlag.AlignLeft)
        footer.addStretch(1)

        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        style_dialog_buttons(
            buttons,
            {
                QDialogButtonBox.StandardButton.Ok: ButtonStyles.TOOLBAR,
                QDialogButtonBox.StandardButton.Cancel: ButtonStyles.TOOLBAR,
            },
            extra_padding=10,
            minimum_height=32,
            uniform_width=True,  # 确保确定和取消按钮宽度相同
        )
        ok_button = buttons.button(QDialogButtonBox.StandardButton.Ok)
        if ok_button is not None:
            ok_button.setText("确定")
        cancel_button = buttons.button(QDialogButtonBox.StandardButton.Cancel)
        if cancel_button is not None:
            cancel_button.setText("取消")
        footer.addWidget(buttons, 0, Qt.AlignmentFlag.AlignRight)
        root.addLayout(footer)

        self.resize(460, self.sizeHint().height())
        self.setMinimumWidth(420)
        self.setMaximumWidth(520)

    def _sync_timer_controls(self) -> None:
        self.timer_sound_combo.setEnabled(self.timer_sound_check.isChecked())
        reminder_enabled = self.reminder_sound_check.isChecked()
        self.reminder_sound_combo.setEnabled(reminder_enabled)
        self.reminder_interval_slider.setEnabled(reminder_enabled)

    def _sync_output_controls(self) -> None:
        engine = str(self.engine_combo.currentData() or "").strip().lower()
        if engine == "pyttsx3":
            self.output_combo.setEnabled(False)
            self.output_combo.setToolTip("pyttsx3 不支持切换输出设备。")
            return
        if self._output_ids:
            self.output_combo.setEnabled(True)
            self.output_combo.setToolTip("")
        else:
            self.output_combo.setEnabled(False)
            self.output_combo.setToolTip("未检测到可用的输出设备。")

    def _selected_photo_shared_class(self) -> str:
        text = self.photo_shared_combo.currentText().strip()
        data = self.photo_shared_combo.currentData()
        if data:
            return str(data).strip()
        if text == "各班使用各自文件夹中的照片":
            return ""
        return text

    def get_settings(self) -> Dict[str, Any]:
        engine = str(self.engine_combo.currentData() or "pyttsx3").strip().lower()
        output_id = ""
        if engine != "pyttsx3":
            output_id = str(self.output_combo.currentData() or self._initial_output_id)
        return {
            "show_id": self.show_id_check.isChecked(),
            "show_name": self.show_name_check.isChecked(),
            "show_photo": self.show_photo_check.isChecked(),
            "photo_duration_seconds": int(self.photo_duration_slider.value()),
            "photo_shared_class": self._selected_photo_shared_class(),
            "speech_enabled": self.speech_enabled_check.isChecked(),
            "speech_engine": engine,
            "speech_voice_id": str(self.voice_combo.currentData() or self._initial_voice_id),
            "speech_output_id": output_id,
            "timer_sound_enabled": self.timer_sound_check.isChecked(),
            "timer_sound_variant": str(self.timer_sound_combo.currentData() or "gentle"),
            "timer_reminder_enabled": self.reminder_sound_check.isChecked(),
            "timer_reminder_interval_minutes": int(self.reminder_interval_slider.value()),
            "timer_reminder_sound_variant": str(self.reminder_sound_combo.currentData() or "soft_beep"),
            "remote_presenter_enabled": self.remote_enabled_check.isChecked(),
            "remote_presenter_key": str(self.remote_key_combo.currentData() or "tab"),
        }

    def _wrap_slider_row(self, slider: QSlider, label: QLabel) -> QWidget:
        container = QWidget()
        layout = QHBoxLayout(container)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(8)
        layout.addWidget(slider, 1)
        label.setMinimumWidth(68)
        label.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        layout.addWidget(label, 0)
        return container

    def _update_photo_duration_label(self) -> None:
        seconds = int(self.photo_duration_slider.value())
        if seconds <= 0:
            self.photo_duration_label.setText("不自动关闭")
        else:
            self.photo_duration_label.setText(f"{seconds} 秒")

    def _update_reminder_interval_label(self) -> None:
        minutes = int(self.reminder_interval_slider.value())
        self.reminder_interval_label.setText(f"每 {minutes} 分钟")


class ClickableFrame(QFrame):
    clicked = pyqtSignal()
    def mousePressEvent(self, e) -> None:
        if e.button() == Qt.MouseButton.LeftButton:
            self.clicked.emit()
        super().mousePressEvent(e)


class StudentListDialog(QDialog):
    def __init__(self, parent: Optional[QWidget], students: List[tuple[str, str, int, bool]]) -> None:
        super().__init__(parent)
        self.setWindowTitle("学生名单")
        self.setModal(True)
        self._selected_index: Optional[int] = None

        layout = QVBoxLayout(self)
        layout.setContentsMargins(12, 12, 12, 12)
        layout.setSpacing(12)

        grid = QGridLayout()
        grid.setContentsMargins(0, 0, 0, 0)
        grid.setHorizontalSpacing(6)
        grid.setVerticalSpacing(6)
        grid.setAlignment(Qt.AlignmentFlag.AlignTop | Qt.AlignmentFlag.AlignHCenter)

        button_font = QFont("Microsoft YaHei UI", 10, QFont.Weight.Medium)
        metrics = QFontMetrics(button_font)

        def _format_entry_text(sid: str, name: str) -> str:
            return f"{sid} {name}".strip()

        def _measure_text(sid: str, name: str, called: bool) -> int:
            base = f"{sid} {name}".strip()
            extra = metrics.horizontalAdvance(" ●") if not called else 0
            return metrics.horizontalAdvance(base) + extra

        max_text = max(
            (_measure_text(sid, name, called) for sid, name, _, called in students),
            default=120,
        )
        min_button_width = max(120, max_text + 24)
        button_height = recommended_control_height(button_font, extra=12, minimum=34)

        screen = QApplication.primaryScreen()
        available_width = screen.availableGeometry().width() if screen else 1280
        max_width_per_button = max(96, int((available_width * 0.9 - 40) / 10))
        button_width = min(min_button_width, max_width_per_button)
        button_size = QSize(button_width, button_height)

        total_rows = max(1, math.ceil(len(students) / 10))

        for column in range(10):
            grid.setColumnStretch(column, 0)
            grid.setColumnMinimumWidth(column, button_width)

        for row in range(total_rows):
            grid.setRowStretch(row, 0)
            grid.setRowMinimumHeight(row, button_height)

        dot_size = 10
        dot_pixmap = QPixmap(dot_size, dot_size)
        dot_pixmap.fill(Qt.GlobalColor.transparent)
        painter = QPainter(dot_pixmap)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setBrush(QColor("#d93025"))
        painter.setPen(Qt.PenStyle.NoPen)
        painter.drawEllipse(1, 1, dot_size - 2, dot_size - 2)
        painter.end()
        dot_icon = QIcon(dot_pixmap)

        for position, (sid, name, data_index, called) in enumerate(students):
            row = position // 10
            column = position % 10
            button = QPushButton(_format_entry_text(sid, name))
            button.setFont(button_font)
            button.setFixedSize(button_size)
            button.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
            apply_button_style(button, ButtonStyles.GRID, height=button_height)
            button.setLayoutDirection(Qt.LayoutDirection.RightToLeft)
            button.setIcon(dot_icon if not called else QIcon())
            button.setIconSize(dot_pixmap.size())
            button.setToolTip("本轮已点名" if called else "本轮未点名")
            button.clicked.connect(lambda _checked=False, value=data_index: self._select_student(value))
            grid.addWidget(button, row, column, Qt.AlignmentFlag.AlignCenter)

        layout.addLayout(grid)

        legend = QLabel("红点标记本轮未点名（无标记即已点名）")
        legend.setStyleSheet("color: #4a525a;")
        legend.setAlignment(Qt.AlignmentFlag.AlignLeft)
        layout.addWidget(legend)

        box = QDialogButtonBox(QDialogButtonBox.StandardButton.Close, parent=self)
        box.rejected.connect(self.reject)
        close_button = box.button(QDialogButtonBox.StandardButton.Close)
        if close_button is not None:
            close_button.setText("关闭")
            apply_button_style(
                close_button,
                ButtonStyles.PRIMARY,
                height=recommended_control_height(close_button.font(), extra=10, minimum=32),
            )
        layout.addWidget(box)

        if screen is not None:
            available = screen.availableGeometry()
            rows = total_rows
            h_spacing = grid.horizontalSpacing() if grid.horizontalSpacing() is not None else 6
            v_spacing = grid.verticalSpacing() if grid.verticalSpacing() is not None else 6
            preferred_width = min(int(available.width() * 0.9), button_width * 10 + h_spacing * 9 + 40)
            preferred_height = min(
                int(available.height() * 0.85),
                rows * button_height + max(0, rows - 1) * v_spacing + box.sizeHint().height() + 48,
            )
            self.resize(preferred_width, preferred_height)

    def _select_student(self, index: int) -> None:
        self._selected_index = index
        self.accept()

    @property
    def selected_index(self) -> Optional[int]:
        return self._selected_index


@dataclass
class ClassRollState:
    current_group: str
    group_remaining: Dict[str, List[Union[int, str]]]
    group_last: Dict[str, Optional[Union[int, str]]]
    global_drawn: List[Union[int, str]]
    current_student: Optional[Union[int, str]] = None
    pending_student: Optional[Union[int, str]] = None

    def to_json(self) -> Dict[str, Any]:
        payload: Dict[str, Any] = {
            "current_group": self.current_group,
            "group_remaining": {group: list(values) for group, values in self.group_remaining.items()},
            "group_last": {group: value for group, value in self.group_last.items()},
            "global_drawn": list(self.global_drawn),
            "current_student": self.current_student,
            "pending_student": self.pending_student,
        }
        return payload

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> Optional["ClassRollState"]:
        if not isinstance(data, Mapping):
            return None

        current_group = str(data.get("current_group", "") or "")

        remaining_raw = data.get("group_remaining", {})
        remaining: Dict[str, List[Union[int, str]]] = {}
        if isinstance(remaining_raw, Mapping):
            for key, values in remaining_raw.items():
                if not isinstance(key, str):
                    continue
                if isinstance(values, Iterable) and not isinstance(values, (str, bytes)):
                    cleaned: List[Union[int, str]] = []
                    for value in values:
                        if isinstance(value, str):
                            text = value.strip()
                            if text:
                                cleaned.append(text)
                            continue
                        try:
                            cleaned.append(int(value))
                        except (TypeError, ValueError):
                            continue
                    remaining[key] = cleaned

        last_raw = data.get("group_last", {})
        last: Dict[str, Optional[Union[int, str]]] = {}
        if isinstance(last_raw, Mapping):
            for key, value in last_raw.items():
                if not isinstance(key, str):
                    continue
                if value is None:
                    last[key] = None
                    continue
                if isinstance(value, str):
                    text = value.strip()
                    if text:
                        last[key] = text
                    continue
                try:
                    last[key] = int(value)
                except (TypeError, ValueError):
                    continue

        global_raw = data.get("global_drawn", [])
        global_drawn: List[Union[int, str]] = []
        if isinstance(global_raw, Iterable) and not isinstance(global_raw, (str, bytes)):
            for value in global_raw:
                if isinstance(value, str):
                    text = value.strip()
                    if text:
                        global_drawn.append(text)
                    continue
                try:
                    global_drawn.append(int(value))
                except (TypeError, ValueError):
                    continue

        def _parse_optional_key(value: Any) -> Optional[Union[int, str]]:
            if value is None:
                return None
            if isinstance(value, str):
                text = value.strip()
                return text or None
            try:
                return int(value)
            except (TypeError, ValueError):
                return None

        current_student = _parse_optional_key(data.get("current_student"))
        pending_student = _parse_optional_key(data.get("pending_student"))

        return cls(
            current_group=current_group,
            group_remaining=remaining,
            group_last=last,
            global_drawn=global_drawn,
            current_student=current_student,
            pending_student=pending_student,
        )


class StudentPhotoOverlay(QWidget):
    closed_by_user = pyqtSignal()
    auto_closed = pyqtSignal()

    def __init__(self, owner: Optional[QWidget] = None) -> None:
        flags = (
            Qt.WindowType.Tool
            | Qt.WindowType.FramelessWindowHint
            | Qt.WindowType.WindowStaysOnTopHint
        )
        super().__init__(None, flags)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground, True)
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, True)
        self.setFocusPolicy(Qt.FocusPolicy.NoFocus)
        self.setStyleSheet("background: transparent;")
        self._owner = owner
        self._auto_close_duration_ms = 0
        self._auto_close_timer = QTimer(self)
        self._auto_close_timer.setSingleShot(True)
        self._auto_close_timer.timeout.connect(self._handle_auto_close)

        self._photo_label = QLabel(self)
        self._photo_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._photo_label.setStyleSheet("background: transparent;")
        self._photo_label.setSizePolicy(
            QSizePolicy.Policy.Fixed,
            QSizePolicy.Policy.Fixed,
        )
        self._photo_label.installEventFilter(self)

        self._name_label = QLabel(self)
        self._name_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._name_label.setWordWrap(True)
        self._name_label.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        self._name_label.setStyleSheet(
            "color: #f0f2f5; background: transparent; border: none; "
            "padding: 14px 24px; font-weight: 700; font-size: 56px;"
        )
        shadow = QGraphicsDropShadowEffect(self)
        shadow.setBlurRadius(18)
        shadow.setColor(QColor(0, 0, 0, 180))
        shadow.setOffset(0, 2)
        self._name_label.setGraphicsEffect(shadow)
        self._name_label.hide()
        self._current_student_name: str = ""
        self._name_shadow_safe_margin = 24

        self._left_close = self._make_close_button()
        self._right_close = self._make_close_button()
        self._left_close.clicked.connect(lambda: self._handle_close_request(manual=True))
        self._right_close.clicked.connect(lambda: self._handle_close_request(manual=True))
        self._left_close.pressed.connect(self._safe_stack_below_owner)
        self._right_close.pressed.connect(self._safe_stack_below_owner)

    def update_owner(self, owner: Optional[QWidget]) -> None:
        self._owner = owner

    def _make_close_button(self) -> QToolButton:
        button = QToolButton(self)
        button.setAutoRaise(True)
        button.setFocusPolicy(Qt.FocusPolicy.NoFocus)
        button.setCursor(Qt.CursorShape.PointingHandCursor)
        button.setText("✕")
        button.setToolTip("关闭照片")
        button.setFixedSize(22, 22)
        button.setStyleSheet(
            """
            QToolButton {
                color: #f0f2f5;
                background-color: rgba(28, 30, 36, 178);
                border: 1px solid rgba(255, 255, 255, 135);
                border-radius: 11px;
                padding: 0;
            }
            QToolButton:hover {
                background-color: rgba(28, 30, 36, 220);
                border-color: rgba(102, 157, 246, 200);
            }
            QToolButton:pressed {
                background-color: rgba(28, 30, 36, 255);
            }
            """
        )
        return button

    def _handle_close_request(self, *, manual: bool) -> None:
        if is_app_closing():
            with contextlib.suppress(Exception):
                self._auto_close_timer.stop()
            with contextlib.suppress(Exception):
                super().hide()
            return
        self._auto_close_timer.stop()
        super().hide()
        if manual:
            self.closed_by_user.emit()
        else:
            self.auto_closed.emit()

    def _handle_auto_close(self) -> None:
        if is_app_closing():
            return
        self._handle_close_request(manual=False)

    def cancel_auto_close(self) -> None:
        self._auto_close_timer.stop()

    def schedule_auto_close(self, duration_ms: int) -> None:
        self._auto_close_duration_ms = max(0, int(duration_ms))
        self._auto_close_timer.stop()
        if self._auto_close_duration_ms > 0 and self.isVisible():
            self._auto_close_timer.start(self._auto_close_duration_ms)

    def display_photo(
        self,
        pixmap: QPixmap,
        screen_rect: QRect,
        duration_ms: int,
        student_name: Optional[str] = None,
    ) -> None:
        if is_app_closing():
            return
        if pixmap.isNull():
            self.hide()
            return

        # Qt 的分层窗口在少数环境下更新可能失败（UpdateLayeredWindowIndirect 参数错误），
        # 通过先隐藏再显示来强制刷新，避免“上一张未关下一张不更新”的现象。
        was_visible = self.isVisible()
        if was_visible:
            try:
                super().hide()
            except Exception:
                self.hide()

        self._current_student_name = (student_name or "").strip()
        self._auto_close_duration_ms = max(0, int(duration_ms))
        self._auto_close_timer.stop()
        available_size = screen_rect.size()
        original_size = pixmap.size()
        if (
            original_size.width() > available_size.width()
            or original_size.height() > available_size.height()
        ):
            scaled = pixmap.scaled(
                available_size,
                Qt.AspectRatioMode.KeepAspectRatio,
                Qt.TransformationMode.SmoothTransformation,
            )
        else:
            scaled = pixmap
        self._photo_label.setPixmap(scaled)
        target_size = scaled.size()
        self.resize(target_size)
        self._photo_label.resize(target_size)
        self._photo_label.move(0, 0)
        self._update_name_label(target_size)
        x = screen_rect.x() + max(0, (screen_rect.width() - target_size.width()) // 2)
        y = screen_rect.y() + max(0, (screen_rect.height() - target_size.height()) // 2)
        self.move(int(x), int(y))
        self.show()
        self._update_close_button_positions()
        self._stack_below_owner()
        self.schedule_auto_close(self._auto_close_duration_ms)

    def _safe_stack_below_owner(self) -> None:
        try:
            self._stack_below_owner()
        except Exception:
            pass

    def _stack_below_owner(self) -> None:
        owner = self._owner
        if owner is None:
            return
        owner_chain = self._collect_owner_chain(owner)
        if not owner_chain:
            return
        overlay_rect = self._widget_frame_rect(self)
        if overlay_rect is None:
            overlay_rect = QRect(self.pos(), self.size())
        owner_rects: List[Tuple[QWidget, QRect]] = []
        needs_raise = False
        for widget in owner_chain:
            rect = self._widget_frame_rect(widget)
            if rect is None:
                continue
            owner_rects.append((widget, rect))
            if overlay_rect.intersects(rect):
                needs_raise = True
        if not needs_raise:
            return
        owner_hwnds = self._collect_owner_hwnds(owner_chain)
        if win32gui is not None and win32con is not None and owner_hwnds:
            flags = (
                win32con.SWP_NOMOVE
                | win32con.SWP_NOSIZE
                | win32con.SWP_NOOWNERZORDER
                | win32con.SWP_NOACTIVATE
            )
            for hwnd in reversed(owner_hwnds):
                try:
                    win32gui.SetWindowPos(hwnd, win32con.HWND_TOPMOST, 0, 0, 0, 0, flags)
                except Exception:
                    logger.debug(
                        "win32gui.SetWindowPos failed for owner window above photo overlay",
                        exc_info=True,
                    )
            try:
                if owner_hwnds:
                    _user32_focus_window(owner_hwnds[0])
            except Exception:
                pass
        else:
            for widget, _ in reversed(owner_rects):
                try:
                    widget.raise_()
                except Exception:
                    continue
            try:
                owner_rects[0][0].activateWindow()
            except Exception:
                pass

    def _update_close_button_positions(self) -> None:
        label_geometry = self._photo_label.geometry()
        if label_geometry.width() <= 0 or label_geometry.height() <= 0:
            return
        margin = 8
        self._left_close.show()
        self._right_close.show()
        left_x = label_geometry.left() + margin
        right_x = label_geometry.right() - self._right_close.width() - margin
        y = label_geometry.bottom() - self._left_close.height() - margin
        self._left_close.move(int(left_x), int(y))
        self._right_close.move(int(right_x), int(y))
        self._left_close.raise_()
        self._right_close.raise_()

    def _update_name_label(self, target_size: QSize) -> None:
        name = self._current_student_name.strip()
        if not name or target_size.isEmpty():
            self._name_label.hide()
            return
        self._name_label.setText(name)
        max_width = max(80, min(target_size.width() - 12, int(target_size.width() * 0.9)))
        self._name_label.setFixedWidth(max_width)
        self._name_label.adjustSize()
        label_width = min(self._name_label.sizeHint().width(), max_width)
        self._name_label.setFixedWidth(label_width)
        safe = int(getattr(self, "_name_shadow_safe_margin", 24))
        x = max(safe, (target_size.width() - label_width) // 2)
        y = max(safe, int(target_size.height() * 0.01))
        max_x = max(0, target_size.width() - label_width)
        max_y = max(0, target_size.height() - self._name_label.height())
        x = min(int(x), int(max_x))
        y = min(int(y), int(max_y))
        self._name_label.move(int(x), int(y))
        self._name_label.show()
        self._name_label.raise_()

    def resizeEvent(self, event) -> None:
        self._photo_label.resize(self.size())
        self._photo_label.move(0, 0)
        self._update_name_label(self.size())
        self._update_close_button_positions()
        super().resizeEvent(event)

    def mousePressEvent(self, event) -> None:
        self._safe_stack_below_owner()
        super().mousePressEvent(event)

    def eventFilter(self, watched: QObject, event: QEvent) -> bool:
        if watched is self._photo_label and event.type() == QEvent.Type.MouseButtonPress:
            self._safe_stack_below_owner()
        return super().eventFilter(watched, event)

    @staticmethod
    def _collect_owner_chain(owner: QWidget) -> List[QWidget]:
        chain: List[QWidget] = []
        current: Optional[QWidget] = owner
        while isinstance(current, QWidget):
            chain.append(current)
            current = current.parentWidget()
        return chain

    @staticmethod
    def _collect_owner_hwnds(owner_chain: Iterable[QWidget]) -> List[int]:
        handles: List[int] = []
        for widget in owner_chain:
            try:
                handle = int(widget.winId()) if widget.winId() else 0
            except Exception:
                handle = 0
            if handle:
                handles.append(handle)
        return handles

    @staticmethod
    def _widget_frame_rect(widget: QWidget) -> Optional[QRect]:
        if widget is None or not widget.isVisible():
            return None
        try:
            rect = widget.frameGeometry()
        except Exception:
            rect = widget.geometry()
        if rect.isNull() or not rect.isValid():
            return None
        return QRect(rect)


class RollCallLogic(QObject):
    """封装点名核心逻辑，便于在后台或遥控触发时复用。"""

    def __init__(self, window: "RollCallTimerWindow") -> None:
        super().__init__(window)
        self.window = window

    # ---- 公开接口 ----
    def roll_student(self, speak: bool = True, *, ensure_mode: bool = False) -> bool:
        w = self.window
        if ensure_mode and w.mode != "roll_call":
            w.mode = "roll_call"
            w.update_mode_ui(force_timer_reset=False)
        if w.mode != "roll_call":
            return False
        if not w._ensure_student_data_ready():
            return False
        self.validate_and_repair_state(context="roll_student")
        group_name = w.current_group_name
        pool = w._group_remaining_indices.get(group_name)
        if pool is None:
            self._ensure_group_pool(group_name)
            pool = w._group_remaining_indices.get(group_name, [])
        if not pool:
            base_total = w._group_all_indices.get(group_name, [])
            if not base_total:
                show_quiet_information(w, f"'{group_name}' 分组当前没有可点名的学生。")
                w.current_student_index = None
                w.display_current_student()
                return False
            if self._all_groups_completed():
                show_quiet_information(w, "所有学生都已完成点名，请点击“重置”按钮重新开始。")
            else:
                show_quiet_information(w, f"'{group_name}' 的同学已经全部点到，请切换其他分组或点击“重置”按钮。")
            return False
        draw_index = w._rng.randrange(len(pool)) if len(pool) > 1 else 0
        w.current_student_index = pool.pop(draw_index)
        w._pending_passive_student = w.current_student_index
        w._group_last_student[group_name] = w.current_student_index
        self._mark_student_drawn(w.current_student_index)
        w.display_current_student()
        if speak:
            w._announce_current_student()
        # 点名操作频繁时进行节流保存，兼顾性能与数据安全。
        w._schedule_roll_state_save()
        return True

    def reset_roll_call_state(self) -> None:
        self.window.settings_manager.clear_roll_call_history()
        self.window._pending_passive_student = None
        self._rebuild_group_indices()
        self._ensure_group_pool(self.window.current_group_name)
        self.validate_and_repair_state(context="reset_roll_call_state")

    def reset_single_group(self, group_name: str) -> None:
        if group_name == "全部":
            return
        w = self.window
        base_indices_raw = w._group_all_indices.get(group_name)
        if base_indices_raw is None:
            return
        base_indices = self._collect_base_indices(base_indices_raw)
        history = w._group_drawn_history.setdefault(group_name, set())
        if history:
            for idx in list(history):
                self._remove_from_global_history(idx, ignore_group=group_name)
            history.clear()
        global_drawn = set(w._global_drawn_students)
        shuffled = [idx for idx in base_indices if idx not in global_drawn]
        self._shuffle(shuffled)
        w._group_remaining_indices[group_name] = shuffled
        w._group_initial_sequences[group_name] = list(shuffled)
        w._group_last_student[group_name] = None
        if w._pending_passive_student in base_indices:
            w._pending_passive_student = None
        self._refresh_all_group_pool()
        self.validate_and_repair_state(context=f"reset_single_group:{group_name}")

    def validate_and_repair_state(self, *, context: str = "") -> None:
        """校验并修复点名状态不变量，尽量避免名单错乱与索引越界。"""

        w = self.window
        if not PANDAS_READY or not isinstance(w.student_data, pd.DataFrame):
            return

        changed = False
        base_all = self._collect_base_indices(list(w.student_data.index))
        base_all_set = set(base_all)

        # 清理未知分组的残留状态（通常来自配置/文件损坏或切换班级后的旧状态）
        known_groups = set(w._group_all_indices.keys()) | set(w.groups or [])
        for group in list(w._group_remaining_indices.keys()):
            if group not in known_groups:
                w._group_remaining_indices.pop(group, None)
                changed = True
        for group in list(w._group_last_student.keys()):
            if group not in known_groups:
                w._group_last_student.pop(group, None)
                changed = True
        for group in list(w._group_drawn_history.keys()):
            if group not in known_groups and group != "全部":
                w._group_drawn_history.pop(group, None)
                changed = True

        # 修复“全部”分组的引用关系
        if w._group_drawn_history.get("全部") is not w._global_drawn_students:
            w._group_drawn_history["全部"] = w._global_drawn_students
            changed = True

        # 全局已点名集合必须是有效索引子集
        if w._global_drawn_students:
            filtered_global = {idx for idx in w._global_drawn_students if idx in base_all_set}
            if filtered_global != w._global_drawn_students:
                w._global_drawn_students = set(filtered_global)
                w._group_drawn_history["全部"] = w._global_drawn_students
                changed = True

        # 修复各分组剩余池/历史/最后点名
        for group, raw_base in list(w._group_all_indices.items()):
            base_list = self._collect_base_indices(raw_base)
            base_set = set(base_list)
            if group == "全部":
                continue

            history_raw = w._group_drawn_history.get(group, set())
            try:
                history = {int(v) for v in history_raw if int(v) in base_set}
            except Exception:
                history = set()
            if history != history_raw:
                w._group_drawn_history[group] = set(history)
                changed = True

            # 子分组历史与全局集合应保持一致（至少应是全局集合的子集）
            if history and not history.issubset(w._global_drawn_students):
                w._global_drawn_students.update(history)
                w._group_drawn_history["全部"] = w._global_drawn_students
                changed = True

            pool_raw = w._group_remaining_indices.get(group, [])
            normalized_pool = self._normalize_indices(pool_raw, allowed=base_set)
            if w._global_drawn_students or history:
                normalized_pool = [
                    idx
                    for idx in normalized_pool
                    if idx not in w._global_drawn_students and idx not in history
                ]
            if normalized_pool != pool_raw:
                w._group_remaining_indices[group] = list(normalized_pool)
                changed = True

            last_value = w._group_last_student.get(group)
            if last_value is not None:
                try:
                    last_idx = int(last_value)
                except (TypeError, ValueError):
                    last_idx = None
                if last_idx is None or (base_set and last_idx not in base_set):
                    w._group_last_student[group] = None
                    changed = True

            seq_raw = w._group_initial_sequences.get(group)
            if not isinstance(seq_raw, list) or not seq_raw:
                # 保持现有池顺序优先，其次补齐剩余学生，避免重新洗牌导致体验变化
                seq = list(normalized_pool)
                seq.extend(idx for idx in base_list if idx not in seq)
                w._group_initial_sequences[group] = seq
                changed = True

        # 当前分组必须有效
        if w.groups:
            if w.current_group_name not in w.groups:
                fallback = "全部" if "全部" in w.groups else w.groups[0]
                w.current_group_name = fallback
                changed = True
        else:
            if w.current_group_name:
                w.current_group_name = ""
                changed = True

        def _sanitize_current(value: Any) -> Optional[int]:
            if value is None:
                return None
            try:
                idx = int(value)
            except (TypeError, ValueError):
                return None
            if base_all_set and idx not in base_all_set:
                return None
            return idx

        current_student = _sanitize_current(w.current_student_index)
        if current_student != w.current_student_index:
            w.current_student_index = current_student
            changed = True

        pending_student = _sanitize_current(getattr(w, "_pending_passive_student", None))
        if pending_student != getattr(w, "_pending_passive_student", None):
            w._pending_passive_student = pending_student
            changed = True

        if changed and context:
            logger.warning("点名状态已自动修复（%s）", context)

        if changed:
            # “全部”分组由全局集合重新推导，保持一致性
            w._refresh_all_group_pool()
        return

    # ---- 核心内部逻辑 ----
    def _rebuild_group_indices(self) -> None:
        w = self.window
        all_indices: Dict[str, List[int]] = {}
        remaining: Dict[str, List[int]] = {}
        last_student: Dict[str, Optional[int]] = {}
        student_groups: Dict[int, set[str]] = {}
        initial_sequences: Dict[str, List[int]] = {}
        if w.student_data.empty:
            all_indices["全部"] = []
        else:
            all_indices["全部"] = list(w.student_data.index)
            for idx in all_indices["全部"]:
                key = self._to_index_key(idx)
                if key is None:
                    continue
                student_groups.setdefault(key, set()).add("全部")

            group_names = [name for name in (w.groups or []) if name and name != "全部"]
            group_name_set = set(group_names)
            group_index_map: Dict[str, List[int]] = {name: [] for name in group_names}
            group_series = None
            if group_names and "分组" in w.student_data.columns:
                try:
                    group_series = w.student_data["分组"].map(_normalize_group_name)
                except (KeyError, TypeError, ValueError):
                    group_series = None
            if group_series is not None:
                for idx, value in group_series.items():
                    group = _normalize_group_name(value)
                    if group and group in group_name_set:
                        group_index_map[group].append(idx)
                        key = self._to_index_key(idx)
                        if key is None:
                            continue
                        student_groups.setdefault(key, set()).add(group)

            for group_name in group_names:
                all_indices[group_name] = list(group_index_map.get(group_name, []))
        for group_name, indices in all_indices.items():
            pool = list(indices)
            self._shuffle(pool)
            remaining[group_name] = pool
            initial_sequences[group_name] = list(pool)
            last_student[group_name] = None
        w._group_all_indices = all_indices
        w._group_remaining_indices = remaining
        w._group_last_student = last_student
        w._group_initial_sequences = initial_sequences
        w._student_groups = student_groups
        w._group_drawn_history = {group: set() for group in all_indices}
        # “全部”分组直接引用全局集合，避免重复维护两份数据造成不一致
        if "全部" in w._group_drawn_history:
            w._global_drawn_students.clear()
            w._group_drawn_history["全部"] = w._global_drawn_students
        else:
            w._group_drawn_history["全部"] = w._global_drawn_students
        self._refresh_all_group_pool()

    def _mark_student_drawn(self, student_index: int) -> None:
        w = self.window
        student_key = self._to_index_key(student_index)
        if student_key is None:
            return
        groups = w._student_groups.get(student_key)
        if not groups:
            return
        w._global_drawn_students.add(student_key)
        for group in groups:
            if group == "全部":
                history = w._group_drawn_history.setdefault("全部", w._global_drawn_students)
            else:
                history = w._group_drawn_history.setdefault(group, set())
            history.add(student_key)
            pool = w._group_remaining_indices.get(group)
            if not pool:
                continue
            cleaned: List[int] = []
            for value in pool:
                try:
                    idx = int(value)
                except (TypeError, ValueError):
                    continue
                if idx != student_key:
                    cleaned.append(idx)
            w._group_remaining_indices[group] = cleaned
        self._refresh_all_group_pool()

    def _remove_from_global_history(self, student_index: int, ignore_group: Optional[str] = None) -> None:
        w = self.window
        student_key = self._to_index_key(student_index)
        if student_key is None:
            return
        for group, history in w._group_drawn_history.items():
            if group == "全部" or group == ignore_group:
                continue
            if student_key in history:
                return
        w._global_drawn_students.discard(student_key)

    def _refresh_all_group_pool(self) -> None:
        w = self.window
        base_all_list = self._collect_base_indices(w._group_all_indices.get("全部", []))
        base_all_set = set(base_all_list)
        subgroup_base: Dict[str, Tuple[List[int], Set[int]]] = {}
        subgroup_remaining: Dict[str, List[int]] = {}
        subgroup_remaining_union: Set[int] = set()
        drawn_from_subgroups: Set[int] = set()
        for group, raw_indices in w._group_all_indices.items():
            if group == "全部":
                continue
            base_list = self._collect_base_indices(raw_indices)
            base_set = set(base_list)
            subgroup_base[group] = (base_list, base_set)
            pool = w._group_remaining_indices.get(group, [])
            sanitized = self._normalize_indices(pool, allowed=base_set)
            if sanitized != pool:
                w._group_remaining_indices[group] = sanitized
            subgroup_remaining[group] = sanitized
            subgroup_remaining_union.update(sanitized)
            drawn_from_subgroups.update(idx for idx in base_set if idx not in sanitized)
            initial = w._group_initial_sequences.get(group)
            if initial is None:
                w._group_initial_sequences[group] = list(base_list)
            else:
                cleaned_initial = self._normalize_indices(initial, allowed=base_set)
                if cleaned_initial != list(initial):
                    w._group_initial_sequences[group] = cleaned_initial
                for idx in base_list:
                    if idx not in w._group_initial_sequences[group]:
                        w._group_initial_sequences[group].append(idx)
        valid_global = {
            idx
            for idx in w._global_drawn_students
            if idx in base_all_set and idx not in subgroup_remaining_union
        }
        new_global = {idx for idx in drawn_from_subgroups if idx in base_all_set}
        new_global.update(valid_global)
        w._global_drawn_students = set(new_global)
        w._group_drawn_history["全部"] = w._global_drawn_students
        for group, (_base_list, base_set) in subgroup_base.items():
            pool = subgroup_remaining.get(group, [])
            filtered = [idx for idx in pool if idx in base_set and idx not in w._global_drawn_students]
            if filtered != pool:
                w._group_remaining_indices[group] = filtered
                subgroup_remaining[group] = filtered
            drawn_set = {idx for idx in base_set if idx not in filtered}
            w._group_drawn_history[group] = drawn_set
        order_hint = w._group_initial_sequences.get("全部")
        if order_hint is None:
            shuffled = list(base_all_list)
            self._shuffle(shuffled)
            order_hint = shuffled
        else:
            cleaned_all = self._normalize_indices(order_hint, allowed=base_all_set)
            if cleaned_all != list(order_hint):
                order_hint = cleaned_all
            else:
                order_hint = list(order_hint)
            for idx in base_all_list:
                if idx not in order_hint:
                    order_hint.append(idx)
        w._group_initial_sequences["全部"] = list(order_hint)
        normalized_all = [idx for idx in order_hint if idx not in w._global_drawn_students]
        seen_all: Set[int] = set(normalized_all)
        for idx in base_all_list:
            if idx in seen_all or idx in w._global_drawn_students:
                continue
            normalized_all.append(idx)
            seen_all.add(idx)
        w._group_remaining_indices["全部"] = normalized_all

    def _ensure_group_pool(self, group_name: str, force_reset: bool = False) -> None:
        w = self.window
        if group_name not in w._group_all_indices:
            if w.student_data.empty:
                base_list: List[int] = []
            elif group_name == "全部":
                base_list = list(w.student_data.index)
            else:
                base_list = []
                if "分组" in w.student_data.columns:
                    try:
                        group_series = w.student_data["分组"].map(_normalize_group_name)
                    except (KeyError, TypeError, ValueError):
                        group_series = None
                    if group_series is not None:
                        base_list = list(w.student_data[group_series == group_name].index)
            w._group_all_indices[group_name] = base_list
            w._group_remaining_indices[group_name] = []
            w._group_last_student.setdefault(group_name, None)
            if group_name == "全部":
                w._group_drawn_history[group_name] = w._global_drawn_students
            else:
                w._group_drawn_history.setdefault(group_name, set())
            for idx in base_list:
                key = self._to_index_key(idx)
                if key is None:
                    continue
                entry = w._student_groups.setdefault(key, set())
                entry.add(group_name)
                entry.add("全部")
            # 新增分组时同步生成初始顺序
            shuffled = list(base_list)
            self._shuffle(shuffled)
            w._group_initial_sequences[group_name] = shuffled
        base_indices = self._collect_base_indices(w._group_all_indices.get(group_name, []))
        if group_name == "全部":
            drawn_history = w._group_drawn_history.setdefault("全部", w._global_drawn_students)
            reference_drawn = w._global_drawn_students
        else:
            drawn_history = w._group_drawn_history.setdefault(group_name, set())
            reference_drawn = drawn_history
        if group_name == "全部":
            # “全部”分组直接依据全局集合生成剩余名单，避免与各子分组脱节
            if group_name not in w._group_initial_sequences:
                shuffled = list(base_indices)
                self._shuffle(shuffled)
                w._group_initial_sequences[group_name] = shuffled
            self._refresh_all_group_pool()
            w._group_last_student.setdefault(group_name, None)
            return
        if force_reset or group_name not in w._group_remaining_indices:
            drawn_history.clear()
            pool = list(base_indices)
            self._shuffle(pool)
            w._group_remaining_indices[group_name] = pool
            w._group_last_student.setdefault(group_name, None)
            w._group_initial_sequences[group_name] = list(pool)
            self._refresh_all_group_pool()
            return
        raw_pool = w._group_remaining_indices.get(group_name, [])
        normalized_pool: List[int] = []
        seen: set[int] = set()
        for value in raw_pool:
            try:
                idx = int(value)
            except (TypeError, ValueError):
                continue
            if idx in base_indices and idx not in seen and idx not in reference_drawn:
                normalized_pool.append(idx)
                seen.add(idx)
        source_order = w._group_initial_sequences.get(group_name)
        if source_order is None:
            # 如果未记录初始顺序，则退回数据原有顺序
            source_order = list(base_indices)
            w._group_initial_sequences[group_name] = list(source_order)
        additional: List[int] = []
        for idx in source_order:
            if idx in reference_drawn or idx in seen or idx not in base_indices:
                continue
            normalized_pool.append(idx)
            seen.add(idx)
        for idx in base_indices:
            if idx in reference_drawn or idx in seen:
                continue
            additional.append(idx)
            seen.add(idx)
        if additional:
            self._shuffle(additional)
            for value in additional:
                insert_at = w._rng.randrange(len(normalized_pool) + 1) if normalized_pool else 0
                normalized_pool.insert(insert_at, value)
        w._group_remaining_indices[group_name] = normalized_pool
        w._group_initial_sequences[group_name] = list(normalized_pool)
        w._group_last_student.setdefault(group_name, None)
        self._refresh_all_group_pool()

    def _all_groups_completed(self) -> bool:
        w = self.window
        total_students = len(w._group_all_indices.get("全部", []))
        if total_students == 0:
            return True
        if len(w._global_drawn_students) < total_students:
            return False
        for group, base in w._group_all_indices.items():
            if not base:
                continue
            remaining = w._group_remaining_indices.get(group, [])
            if remaining:
                return False
        return True

    # ---- 工具方法 ----
    def _shuffle(self, values: List[int]) -> None:
        w = self.window
        try:
            w._rng.shuffle(values)
        except Exception:
            random.shuffle(values)

    @staticmethod
    def _normalize_indices(values: Iterable[Any], *, allowed: Optional[Set[int]] = None) -> List[int]:
        normalized: List[int] = []
        seen: Set[int] = set()
        for value in values:
            try:
                idx = int(value)
            except (TypeError, ValueError):
                continue
            if allowed is not None and idx not in allowed:
                continue
            if idx in seen:
                continue
            normalized.append(idx)
            seen.add(idx)
        return normalized

    @staticmethod
    def _to_index_key(value: Any) -> Optional[int]:
        try:
            return int(value)
        except (TypeError, ValueError):
            return None

    def _collect_base_indices(self, values: Optional[Iterable[Any]]) -> List[int]:
        if values is None:
            return []
        return self._normalize_indices(values)


class RemotePresenterHotkey(QObject):
    """全局监听翻页笔按键，使用底层键盘钩子拦截特定按键。"""

    hotkey_pressed = pyqtSignal()

    _WH_KEYBOARD_LL = 13
    _WM_KEYDOWN = 0x0100
    _WM_KEYUP = 0x0101
    _WM_SYSKEYDOWN = 0x0104
    _WM_SYSKEYUP = 0x0105
    _HC_ACTION = 0

    class _KBDLLHOOKSTRUCT(ctypes.Structure):
        _fields_ = [
            ("vkCode", wintypes.DWORD),
            ("scanCode", wintypes.DWORD),
            ("flags", wintypes.DWORD),
            ("time", wintypes.DWORD),
            ("dwExtraInfo", wintypes.ULONG_PTR),
        ]

    _HOOKPROC = _HOOKPROC_TYPE

    def __init__(self, parent: Optional[QObject] = None) -> None:
        super().__init__(parent)
        self._hook_handle: Optional[int] = None
        self._c_hook_proc = None
        self._vk_code = self._normalize_vk("tab")
        self._intercept_enabled = False

    @property
    def available(self) -> bool:
        return bool(_USER32 and _KERNEL32)

    def set_key(self, key: str) -> None:
        vk = self._normalize_vk(key)
        if vk != self._vk_code:
            self._vk_code = vk

    def start(self) -> bool:
        if not self.available:
            return False
        if self._hook_handle:
            return True

        def hook_proc(nCode: int, wParam: int, lParam: int) -> int:
            if is_app_closing():
                return _USER32.CallNextHookEx(self._hook_handle, nCode, wParam, lParam)
            if nCode == self._HC_ACTION and self._intercept_enabled:
                try:
                    if wParam == self._WM_KEYDOWN or wParam == self._WM_SYSKEYDOWN:
                        kb_struct = ctypes.cast(lParam, ctypes.POINTER(self._KBDLLHOOKSTRUCT)).contents
                        if kb_struct.vkCode == self._vk_code:
                            QTimer.singleShot(0, self.hotkey_pressed.emit)
                            return 1

                    elif wParam == self._WM_KEYUP or wParam == self._WM_SYSKEYUP:
                        kb_struct = ctypes.cast(lParam, ctypes.POINTER(self._KBDLLHOOKSTRUCT)).contents
                        if kb_struct.vkCode == self._vk_code:
                            return 1
                except Exception:
                    pass

            return _USER32.CallNextHookEx(self._hook_handle, nCode, wParam, lParam)

        self._c_hook_proc = _HOOKPROC_TYPE(hook_proc)

        h_mod = 0
        try:
            # 将回调所在模块句柄传入，而不是 user32.dll，避免某些环境下钩子注册失败。
            h_mod = _KERNEL32.GetModuleHandleW(None) if _KERNEL32 is not None else 0
        except Exception:
            h_mod = 0

        try:
            self._hook_handle = _USER32.SetWindowsHookExW(
                self._WH_KEYBOARD_LL,
                self._c_hook_proc,
                h_mod,
                0,
            )
        except Exception as e:
            logger.error("SetWindowsHookExW failed: %s", e)
        if self._hook_handle:
            self._intercept_enabled = True
            return True
        return False

    def stop(self) -> None:
        self._intercept_enabled = False
        if self._hook_handle:
            try:
                _USER32.UnhookWindowsHookEx(self._hook_handle)
            except Exception:
                pass
            self._hook_handle = None
        self._c_hook_proc = None

    @staticmethod
    def _normalize_vk(key: str) -> int:
        normalized = (key or "").strip().lower()
        if normalized in {"tab", "vk_tab"}:
            return 0x09
        if normalized in {"b", "key_b"}:
            return 0x42
        return 0x09


class RemotePresenterController(QObject):
    """管理“翻页笔遥控点名”的全局按键监听与触发。"""

    def __init__(self, window: "RollCallTimerWindow") -> None:
        super().__init__(window)
        self.window = window
        self.hotkey = RemotePresenterHotkey(self)
        self.hotkey.hotkey_pressed.connect(self._handle_hotkey)
        self._enabled = False

        self._watchdog_timer = QTimer(self)
        self._watchdog_timer.setInterval(2000)
        self._watchdog_timer.timeout.connect(self._check_hook_health)

    def is_available(self) -> bool:
        return self.hotkey.available

    def set_key(self, key: str) -> None:
        self.hotkey.set_key(key)

    def set_enabled(self, enabled: bool) -> bool:
        if enabled == self._enabled:
            return self._enabled

        self._enabled = enabled
        if enabled:
            success = self.hotkey.start()
            if success:
                self._watchdog_timer.start()
                return True
            self._enabled = False
            return False

        self.stop()
        return False

    def restart(self) -> bool:
        """重新启用监听以应用最新的按键配置。"""

        was_enabled = self._enabled
        self.stop()
        if was_enabled:
            return self.set_enabled(True)
        return False

    def stop(self) -> None:
        self._watchdog_timer.stop()
        self.hotkey.stop()
        self._enabled = False

    def _handle_hotkey(self) -> None:
        if is_app_closing():
            return
        self.window.trigger_remote_presenter_call()

    def _check_hook_health(self) -> None:
        if self._enabled and not self.hotkey._hook_handle:
            self.hotkey.start()

class RollCallTimerWindow(QWidget):
    """集成点名与计时的主功能窗口。"""
    window_closed = pyqtSignal()
    visibility_changed = pyqtSignal(bool)

    STUDENT_FILE = _STUDENT_RESOURCES.plain
    STUDENT_FILE_CANDIDATES = _STUDENT_RESOURCES.plain_candidates
    MIN_FONT_SIZE = 5
    MAX_FONT_SIZE = 220
    _PHOTO_INDEX_CACHE_LIMIT = 24
    _PHOTO_PIXMAP_CACHE_LIMIT = 20
    _SOUND_WAVE_CACHE_LIMIT = 16

    def __init__(
        self,
        settings_manager: SettingsManager,
        student_workbook: Optional[StudentWorkbook],
        parent: Optional[QWidget] = None,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle("点名 / 计时")
        flags = (
            Qt.WindowType.Window
            | Qt.WindowType.WindowTitleHint
            | Qt.WindowType.WindowCloseButtonHint
            | Qt.WindowType.WindowStaysOnTopHint
            | Qt.WindowType.CustomizeWindowHint
        )
        self.setWindowFlags(flags)
        self.setAttribute(Qt.WidgetAttribute.WA_DeleteOnClose)
        self.settings_manager = settings_manager
        self._plain_file_path = self.STUDENT_FILE
        self.student_workbook: Optional[StudentWorkbook] = student_workbook
        self._refresh_student_file_paths()
        base_dataframe: Optional[PandasDataFrame] = None
        if PANDAS_READY and self.student_workbook is not None:
            try:
                base_dataframe = self.student_workbook.get_active_dataframe()
            except Exception:
                base_dataframe = _new_student_dataframe() or pd.DataFrame(columns=DEFAULT_STUDENT_COLUMNS)
        if base_dataframe is None and PANDAS_READY:
            base_dataframe = _new_student_dataframe() or pd.DataFrame(columns=DEFAULT_STUDENT_COLUMNS)
        self.student_data = base_dataframe
        self._student_data_pending_load = False
        try:
            self._rng = random.SystemRandom()
        except NotImplementedError:
            self._rng = random.Random()

        self.roll_call_config = self.settings_manager.get_roll_call_settings()
        config = self.roll_call_config
        apply_geometry_from_text(self, config.geometry)
        self.setMinimumSize(260, 160)
        # 记录初始最小宽高，便于在调整后恢复默认限制
        self._base_minimum_width = self.minimumWidth()
        self._base_minimum_height = self.minimumHeight()
        self._ensure_min_width = self._base_minimum_width
        self._ensure_min_height = self._base_minimum_height

        # 打开窗口默认进入点名模式，避免计时模式影响首屏体验
        saved_mode = str(config.mode).strip().lower()
        self.mode = saved_mode if saved_mode in {"roll_call", "timer"} else "roll_call"
        self.timer_modes = ["countdown", "stopwatch", "clock"]
        self.timer_mode_index = self.timer_modes.index(config.timer_mode) if config.timer_mode in self.timer_modes else 0
        self._active_timer_mode: Optional[str] = None

        self.timer_countdown_minutes = config.timer_countdown_minutes
        self.timer_countdown_seconds = config.timer_countdown_seconds
        self.timer_sound_enabled = config.timer_sound_enabled
        self.timer_sound_variant = config.timer_sound_variant or "gentle"
        self.timer_reminder_enabled = bool(config.timer_reminder_enabled)
        self.timer_reminder_interval_minutes = max(0, int(config.timer_reminder_interval_minutes))
        self.timer_reminder_sound_variant = config.timer_reminder_sound_variant or "soft_beep"

        self.show_id = config.show_id
        self.show_name = config.show_name
        self.show_photo = config.show_photo
        self.photo_duration_seconds = max(0, config.photo_duration_seconds)
        self.photo_shared_class = str(getattr(config, "photo_shared_class", "") or "").strip()
        if not (self.show_id or self.show_name):
            self.show_name = True

        self.photo_root_path, self._photo_search_roots = _determine_student_photo_roots()
        self._photo_extensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif"]
        self._photo_overlay: Optional[StudentPhotoOverlay] = None
        self._last_photo_student_id: Optional[str] = None
        self._photo_manual_hidden = False
        self._photo_load_token = 0
        self._photo_dirs_ensured: Set[str] = set()
        self._ensure_photo_root_directory()
        self._photo_dir_index_cache: "OrderedDict[str, Tuple[float, float, Dict[str, str]]]" = OrderedDict()
        self._photo_pixmap_cache: "OrderedDict[str, QPixmap]" = OrderedDict()
        self._sound_wave_cache: "OrderedDict[Tuple[str, str], Tuple[Any, int]]" = OrderedDict()

        self.current_class_name = str(config.current_class).strip()
        self.current_group_name = _normalize_group_name(config.current_group) or "全部"
        self.groups = ["全部"]

        self.current_student_index: Optional[int] = None
        self._placeholder_on_show = True
        self._group_all_indices: Dict[str, List[int]] = {}
        self._group_remaining_indices: Dict[str, List[int]] = {}
        self._group_last_student: Dict[str, Optional[int]] = {}
        # 记录各分组初始的随机顺序，便于在界面切换时保持未点名名单不被重新洗牌
        self._group_initial_sequences: Dict[str, List[int]] = {}
        # 记录每个分组已点过名的学生索引，便于核对剩余名单
        self._group_drawn_history: Dict[str, set[int]] = {}
        # 统一维护一个全局已点名集合，确保“全部”分组与子分组状态一致
        self._global_drawn_students: set[int] = set()
        self._student_groups: Dict[int, set[str]] = {}
        self._class_roll_states: Dict[str, ClassRollState] = {}
        self.timer_seconds_left = max(0, config.timer_seconds_left)
        self.timer_stopwatch_seconds = max(0, config.timer_stopwatch_seconds)
        self.timer_running = bool(config.timer_running)

        self.last_id_font_size = max(self.MIN_FONT_SIZE, int(config.id_font_size))
        self.last_name_font_size = max(self.MIN_FONT_SIZE, int(config.name_font_size))
        self.last_timer_font_size = max(self.MIN_FONT_SIZE, int(config.timer_font_size))

        self.count_timer = QTimer(self)
        self.count_timer.setInterval(1000)
        self.count_timer.timeout.connect(self._on_count_timer)
        self.clock_timer = QTimer(self)
        self.clock_timer.setInterval(1000)
        self.clock_timer.timeout.connect(self._update_clock)
        self._reminder_elapsed_seconds = 0

        self.tts_manager: Optional[TTSManager] = None
        self.speech_enabled = bool(config.speech_enabled)
        self.selected_voice_id = config.speech_voice_id
        self.selected_output_id = config.speech_output_id
        self.selected_engine = (config.speech_engine or "pyttsx3").strip().lower()
        self._speech_init_pending = bool(self.speech_enabled)
        self._speech_init_scheduled = False
        self._speech_issue_reported = False
        self._speech_check_scheduled = False
        self._pending_passive_student: Optional[int] = None
        self._settings_write_lock = QMutex()
        self._student_data_loading = False
        self._io_pool = QThreadPool.globalInstance()

        # 点名逻辑与后台触发控制
        self.roll_logic = RollCallLogic(self)
        self.remote_presenter_enabled = bool(config.remote_roll_enabled)
        self._remote_presenter_paused = False
        self.remote_presenter_key = (config.remote_roll_key or "tab").strip().lower() or "tab"
        self.remote_presenter_controller = RemotePresenterController(self)
        self.remote_presenter_controller.set_key(self.remote_presenter_key)
        self._apply_remote_presenter_runtime_state()

        families = _get_available_font_families()
        self.name_font_family = "楷体" if "楷体" in families else ("KaiTi" if "KaiTi" in families else "Microsoft YaHei UI")

        # 使用轻量级的延迟写入机制，避免频繁操作磁盘。
        self._save_timer = QTimer(self)
        self._save_timer.setSingleShot(True)
        self._save_timer.setInterval(250)
        self._save_timer.timeout.connect(self.save_settings)
        self._last_immediate_save_at = 0.0
        self._settings_dirty = False
        self._auto_save_timer = QTimer(self)
        self._auto_save_timer.setInterval(int(SETTINGS_AUTOSAVE_INTERVAL_SECONDS * 1000))
        self._auto_save_timer.timeout.connect(self._flush_dirty_settings)
        self._auto_save_timer.start()

        # 将点名状态写回 students.xlsx 的操作改为延迟+后台执行，避免频繁写盘阻塞 UI。
        self._workbook_persist_timer = QTimer(self)
        self._workbook_persist_timer.setSingleShot(True)
        self._workbook_persist_timer.setInterval(1200)
        self._workbook_persist_timer.timeout.connect(self._queue_workbook_persist)
        self._workbook_persist_inflight = False
        self._workbook_persist_dirty = False

        self._build_ui()
        if self.student_workbook is not None:
            self._apply_student_workbook(self.student_workbook, propagate=False)
        else:
            self._set_student_dataframe(self.student_data, propagate=False)
        embedded_state = _consume_embedded_roll_state()
        if embedded_state:
            self.roll_call_config.class_states = embedded_state
        self._apply_saved_fonts()
        self._update_menu_state()
        self._restore_group_state(config.to_mapping())
        self.update_mode_ui(force_timer_reset=self.mode == "timer")
        self.on_group_change(initial=True)
        self.display_current_student()

    def _build_ui(self) -> None:
        self.setStyleSheet(
            f"""
            QWidget {{
                background-color: {StyleConfig.BACKGROUND_COLOR};
                color: {StyleConfig.TEXT_PRIMARY};
            }}
            QLabel {{
                color: {StyleConfig.TEXT_PRIMARY};
                background-color: transparent;
            }}
            """
        )
        layout = QVBoxLayout(self)
        layout.setContentsMargins(8, 8, 8, 8)
        layout.setSpacing(6)

        toolbar_layout = QVBoxLayout()
        toolbar_layout.setContentsMargins(0, 0, 0, 0)
        toolbar_layout.setSpacing(2)

        top = QHBoxLayout()
        top.setContentsMargins(0, 0, 0, 0)
        top.setSpacing(4)
        self.title_label = QLabel("点名")
        title_font = QFont("Microsoft YaHei UI", 10, QFont.Weight.Bold)
        self.title_label.setFont(title_font)
        self.title_label.setStyleSheet(f"color: {StyleConfig.TEXT_PRIMARY}; background: transparent;")
        self.title_label.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        top.addWidget(self.title_label, 0, Qt.AlignmentFlag.AlignLeft)

        # 固定字体和尺寸计算
        compact_font = QFont("Microsoft YaHei UI", 9, QFont.Weight.Medium)
        # 使用 recommended_control_height 函数确保高度计算正确
        # extra=14 包含：padding(8px) + border(2px) + 余量(4px)
        self._toolbar_height = recommended_control_height(compact_font, extra=14, minimum=32)
        toolbar_height = self._toolbar_height

        self.mode_button = QPushButton("切换到计时")
        self.mode_button.setFont(compact_font)
        apply_button_style(self.mode_button, ButtonStyles.TOOLBAR)
        # 计算宽度
        mode_fm = QFontMetrics(compact_font)
        max_text = max(("切换到计时", "切换到点名"), key=lambda t: mode_fm.horizontalAdvance(t))
        mode_width = mode_fm.horizontalAdvance(max_text) + 34
        self.mode_button.setMinimumWidth(mode_width)
        self.mode_button.setMaximumWidth(mode_width)
        self.mode_button.setMinimumHeight(toolbar_height)
        self.mode_button.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.mode_button.clicked.connect(self.toggle_mode)
        top.addWidget(self.mode_button, 0, Qt.AlignmentFlag.AlignLeft)

        def _setup_secondary_button(button: QPushButton) -> None:
            apply_button_style(button, ButtonStyles.TOOLBAR)
            button.setFont(compact_font)
            # 使用 setMinimumHeight 而不是 setFixedHeight，让Qt自动处理
            button.setMinimumHeight(toolbar_height)

        def _lock_button_width(button: QPushButton) -> None:
            """将按钮的宽度锁定，高度保持最小高度设置。"""
            # 计算合适的宽度
            fm = QFontMetrics(button.font())
            text_width = fm.horizontalAdvance(button.text())
            # 计算宽度：文本宽度 + 左右padding(12*2) + 左右border(1*2) + 余量(8)
            calculated_width = text_width + 24 + 2 + 8
            button.setMinimumWidth(calculated_width)
            button.setMaximumWidth(calculated_width)
            button.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)

        control_bar = QWidget()
        control_bar.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Preferred)
        control_layout = QHBoxLayout(control_bar)
        control_layout.setContentsMargins(0, 0, 0, 0)
        control_layout.setSpacing(2)
        # 确保所有按钮顶部对齐
        control_layout.setAlignment(Qt.AlignmentFlag.AlignTop)

        def _recycle_button(button: Optional[QPushButton]) -> None:
            if button is None:
                return
            parent = button.parentWidget()
            layout = parent.layout() if parent is not None else None
            if isinstance(layout, QHBoxLayout):
                layout.removeWidget(button)
            button.setParent(None)
            button.deleteLater()

        existing_class_button = getattr(self, "class_button", None)
        if isinstance(existing_class_button, QPushButton):
            _recycle_button(existing_class_button)
        # 仅保留一个班级切换按钮，并将其固定在"重置"按钮左侧。
        self.class_button = QPushButton("班级")
        _setup_secondary_button(self.class_button)
        self.class_button.clicked.connect(self.show_class_selector)
        _lock_button_width(self.class_button)
        control_layout.addWidget(self.class_button)

        self.list_button = QPushButton("名单")
        _setup_secondary_button(self.list_button)
        self.list_button.clicked.connect(self.show_student_selector)
        _lock_button_width(self.list_button)
        control_layout.addWidget(self.list_button)

        self.settings_button = QPushButton("设置")
        _setup_secondary_button(self.settings_button)
        self.settings_button.clicked.connect(self.show_roll_call_settings_dialog)
        self.main_menu = self._build_menu()
        # 计算宽度
        settings_fm = QFontMetrics(self.settings_button.font())
        settings_text_width = settings_fm.horizontalAdvance("设置")
        settings_width = settings_text_width + 34
        self.settings_button.setMinimumWidth(settings_width)
        self.settings_button.setMaximumWidth(settings_width)
        control_layout.addWidget(self.settings_button)

        top.addWidget(control_bar, 0, Qt.AlignmentFlag.AlignLeft)
        top.addStretch(1)
        toolbar_layout.addLayout(top)

        group_row = QHBoxLayout()
        group_row.setContentsMargins(0, 0, 0, 0)
        group_row.setSpacing(2)

        self.group_label = QLabel("分组")
        self.group_label.setFont(QFont("Microsoft YaHei UI", 9, QFont.Weight.Medium))
        self.group_label.setStyleSheet(f"color: {StyleConfig.TEXT_SECONDARY}; background: transparent;")
        self.group_label.setAlignment(Qt.AlignmentFlag.AlignVCenter | Qt.AlignmentFlag.AlignLeft)
        self.group_label.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        group_row.addWidget(self.group_label, 0, Qt.AlignmentFlag.AlignLeft)

        group_container = QWidget()
        group_container.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Preferred)
        group_container_layout = QHBoxLayout(group_container)
        group_container_layout.setContentsMargins(0, 0, 0, 0)
        group_container_layout.setSpacing(4)
        # 确保所有按钮顶部对齐
        group_container_layout.setAlignment(Qt.AlignmentFlag.AlignTop)

        self.group_container = group_container

        self.group_bar = QWidget(group_container)
        self.group_bar.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        self.group_bar_layout = QHBoxLayout(self.group_bar)
        self.group_bar_layout.setContentsMargins(0, 0, 0, 0)
        self.group_bar_layout.setSpacing(1)
        self.group_button_group = QButtonGroup(self)
        self.group_button_group.setExclusive(True)
        self.group_buttons: Dict[str, QPushButton] = {}
        self._rebuild_group_buttons_ui()
        group_container_layout.addWidget(self.group_bar, 1, Qt.AlignmentFlag.AlignLeft)

        self.reset_button = QPushButton("重置")
        _setup_secondary_button(self.reset_button)
        self.reset_button.clicked.connect(self.reset_roll_call_pools)
        _lock_button_width(self.reset_button)
        group_container_layout.addWidget(self.reset_button, 0, Qt.AlignmentFlag.AlignLeft)
        self._sync_settings_button_width(toolbar_height)

        group_row.addWidget(group_container, 1, Qt.AlignmentFlag.AlignLeft)
        group_row.addStretch(1)
        toolbar_layout.addLayout(group_row)
        layout.addLayout(toolbar_layout)

        self.stack = QStackedWidget()
        layout.addWidget(self.stack, 1)

        self.roll_call_frame = ClickableFrame()
        self.roll_call_frame.setFrameShape(QFrame.Shape.NoFrame)

        rl = QGridLayout(self.roll_call_frame)
        rl.setContentsMargins(6, 6, 6, 6)
        rl.setSpacing(6)

        self.id_label = QLabel("")
        self.name_label = QLabel("")
        for lab in (self.id_label, self.name_label):
            lab.setAlignment(Qt.AlignmentFlag.AlignCenter)
            lab.setStyleSheet(f"color: #FFFFFF; background-color: {StyleConfig.PRIMARY_COLOR}; border-radius: 8px; padding: 8px;")
            lab.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)

        rl.addWidget(self.id_label, 0, 0)
        rl.addWidget(self.name_label, 0, 1)
        self.stack.addWidget(self.roll_call_frame)

        self.timer_frame = QWidget()
        tl = QVBoxLayout(self.timer_frame)
        tl.setContentsMargins(6, 6, 6, 6)
        tl.setSpacing(6)

        self.time_display_label = QLabel("00:00")
        self.time_display_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.time_display_label.setStyleSheet(f"color: #FFFFFF; background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 {StyleConfig.TEXT_PRIMARY}, stop:1 #37474F); border-radius: 8px; padding: 10px;")
        self.time_display_label.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)
        tl.addWidget(self.time_display_label, 1)

        ctrl = QHBoxLayout()
        ctrl.setSpacing(4)

        self.timer_mode_button = QPushButton("倒计时")
        self.timer_mode_button.clicked.connect(self.toggle_timer_mode)
        self.timer_start_pause_button = QPushButton("开始")
        self.timer_start_pause_button.clicked.connect(self.start_pause_timer)
        self.timer_reset_button = QPushButton("重置")
        self.timer_reset_button.clicked.connect(self.reset_timer)
        self.timer_set_button = QPushButton("设定")
        self.timer_set_button.clicked.connect(self.set_countdown_time)
        for b in (self.timer_mode_button, self.timer_start_pause_button, self.timer_reset_button, self.timer_set_button):
            b.setFont(compact_font)
        # 使用 toolbar_height 作为固定高度
        for b in (self.timer_mode_button, self.timer_start_pause_button, self.timer_reset_button, self.timer_set_button):
            apply_button_style(b, ButtonStyles.TOOLBAR)
            b.setFixedHeight(toolbar_height)
            b.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)
            ctrl.addWidget(b)
        tl.addLayout(ctrl)
        self.stack.addWidget(self.timer_frame)

        self.roll_call_frame.clicked.connect(self.roll_student)
        self.id_label.installEventFilter(self)
        self.name_label.installEventFilter(self)

    def _refresh_student_file_paths(self) -> None:
        def _prefer_dir(target_dir: str, paths: tuple[str, ...]) -> Optional[str]:
            target = os.path.normcase(os.path.abspath(target_dir))
            for path in paths:
                if path and os.path.exists(path):
                    if os.path.normcase(os.path.dirname(os.path.abspath(path))) == target:
                        return path
            return None

        module_dir = os.path.dirname(os.path.abspath(__file__))
        cwd = os.path.abspath(os.getcwd())

        existing_plain_current = getattr(self, "_plain_file_path", None)
        if existing_plain_current and os.path.exists(existing_plain_current) and _ensure_writable_directory(os.path.dirname(existing_plain_current) or os.getcwd()):
            current_plain = os.path.abspath(existing_plain_current)
        else:
            current_plain = None

        existing_plain = (
            current_plain
            or _prefer_dir(module_dir, self.STUDENT_FILE_CANDIDATES)
            or _prefer_dir(cwd, self.STUDENT_FILE_CANDIDATES)
            or _any_existing_path(self.STUDENT_FILE_CANDIDATES)
        )

        def _writable_target(path: str) -> bool:
            directory = os.path.dirname(path) or os.getcwd()
            return _ensure_writable_directory(directory)

        plain_path = self.STUDENT_FILE
        if existing_plain and _writable_target(existing_plain):
            plain_path = existing_plain

        plain_dir = os.path.dirname(plain_path) or os.getcwd()
        if not _ensure_writable_directory(plain_dir):
            fallback_plain = os.path.join(os.getcwd(), os.path.basename(self.STUDENT_FILE))
            if _ensure_writable_directory(os.path.dirname(fallback_plain) or os.getcwd()):
                plain_path = fallback_plain

        self._plain_file_path = plain_path
    def _collect_group_names(self, df: PandasDataFrame) -> List[str]:
        """Extract and normalize unique group names from the '分组' column."""

        if df is None or df.empty:
            return []
        if "分组" not in df.columns:
            return []
        try:
            group_series = df["分组"].dropna()
        except Exception:
            return []
        if group_series.empty:
            return []
        try:
            normalized = group_series.map(_normalize_group_name)
            group_values = [g for g in normalized if g and g != "全部"]
        except Exception:
            group_values = []
            for value in group_series:
                name = _normalize_group_name(value)
                if name and name != "全部":
                    group_values.append(name)
        return sorted(set(group_values))

    def _set_student_dataframe(self, df: Optional[PandasDataFrame], *, propagate: bool = True) -> None:
        if not PANDAS_READY:
            self.student_data = df
            return
        if df is None:
            df = _new_student_dataframe() or pd.DataFrame(columns=DEFAULT_STUDENT_COLUMNS)
        try:
            working = df.copy()
        except Exception:
            working = pd.DataFrame(df)
        self.student_data = working
        self.groups = ["全部"]
        group_values = self._collect_group_names(working)
        if group_values:
            self.groups.extend(group_values)

        if self.current_group_name not in self.groups:
            self.current_group_name = "全部"
        self._group_all_indices = {}
        self._group_remaining_indices = {}
        self._group_last_student = {}
        self._group_initial_sequences = {}
        self._group_drawn_history = {}
        self._global_drawn_students = set()
        self._student_groups = {}
        self._rebuild_group_buttons_ui()
        self._rebuild_group_indices()
        self._ensure_group_pool(self.current_group_name, force_reset=True)
        self.current_student_index = None
        self._pending_passive_student = None
        self._restore_active_class_state(restore_current_student=False)
        self._store_active_class_state()
        self._update_class_button_label()
        if propagate:
            self._propagate_student_dataframe()
        self.display_current_student()

    def _apply_student_workbook(self, workbook: StudentWorkbook, *, propagate: bool) -> None:
        self.student_workbook = workbook
        self._prune_orphan_class_states()
        if not PANDAS_READY:
            self.current_class_name = workbook.active_class
            self.student_data = None
            return
        # 优先使用当前窗口或配置中记录的班级名称，避免每次都回落到第一个工作表
        config = getattr(self, "roll_call_config", None)
        saved_class = ""
        if config is not None:
            saved_class = str(getattr(config, "current_class", "") or "").strip()
        target_class = str(self.current_class_name or saved_class or "").strip()
        if target_class and target_class in workbook.class_names():
            workbook.set_active_class(target_class)
        else:
            # 没有有效记录时，保持 workbook 自身的 active_class
            target_class = workbook.active_class
        self.current_class_name = target_class
        df = workbook.get_active_dataframe()
        self._set_student_dataframe(df, propagate=propagate)

    def _snapshot_current_class(self) -> None:
        if not PANDAS_READY:
            return
        if self.student_workbook is None:
            return
        if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
            return
        class_name = (self.current_class_name or self.student_workbook.active_class or "").strip()
        if not class_name:
            available = self.student_workbook.class_names()
            class_name = available[0] if available else self.student_workbook.active_class
        if class_name not in self.student_workbook.class_names():
            class_name = self.student_workbook.active_class
        try:
            snapshot = self.student_data.copy()
        except Exception:
            snapshot = pd.DataFrame(self.student_data)
        self.student_workbook.update_class(class_name, snapshot)
        self.student_workbook.set_active_class(class_name)
        self.current_class_name = class_name
        self._store_active_class_state(class_name)

    def _resolve_active_class_name(self) -> str:
        base = self.current_class_name
        if not base and self.student_workbook is not None:
            base = self.student_workbook.active_class
        return str(base or "").strip()

    def _resolve_photo_class_name(self) -> str:
        shared = str(getattr(self, "photo_shared_class", "") or "").strip()
        if shared:
            return shared
        return self._resolve_active_class_name()

    def _capture_roll_state(self) -> Optional[ClassRollState]:
        if not PANDAS_READY:
            return None
        if not isinstance(self.student_data, pd.DataFrame):
            return None

        base_sets: Dict[str, Set[int]] = {}
        for group, indices in self._group_all_indices.items():
            base_list = self._collect_base_indices(indices)
            base_sets[group] = set(base_list)

        if "全部" not in base_sets:
            try:
                base_sets["全部"] = set(self._collect_base_indices(list(self.student_data.index)))
            except Exception:
                base_sets["全部"] = set()

        all_set = base_sets.get("全部", set())

        remaining_payload: Dict[str, List[Union[int, str]]] = {}
        for group, indices in self._group_remaining_indices.items():
            base_set = base_sets.get(group, all_set)
            if base_set:
                restored = self._normalize_indices(indices, allowed=base_set)
            else:
                restored = []
            mapped: List[Union[int, str]] = []
            for idx in restored:
                mapped.append(self._index_to_save_key(idx))
            remaining_payload[group] = mapped

        last_payload: Dict[str, Optional[Union[int, str]]] = {}
        for group, value in self._group_last_student.items():
            base_set = base_sets.get(group, all_set)
            if value is None:
                last_payload[group] = None
                continue
            try:
                idx = int(value)
            except (TypeError, ValueError):
                last_payload[group] = None
                continue
            if base_set and idx not in base_set:
                last_payload[group] = None
            else:
                last_payload[group] = self._index_to_save_key(idx)

        global_drawn_payload: List[Union[int, str]] = []
        for value in sorted(self._global_drawn_students):
            try:
                idx = int(value)
            except (TypeError, ValueError):
                continue
            if not all_set or idx in all_set:
                global_drawn_payload.append(self._index_to_save_key(idx))

        if self.groups:
            if self.current_group_name in self.groups:
                target_group = self.current_group_name
            elif "全部" in self.groups:
                target_group = "全部"
            else:
                target_group = self.groups[0]
        else:
            target_group = ""

        def _sanitize_index(value: Any) -> Optional[Union[int, str]]:
            if value is None:
                return None
            try:
                idx = int(value)
            except (TypeError, ValueError):
                return None
            if all_set and idx not in all_set:
                return None
            return self._index_to_save_key(idx)

        current_student = _sanitize_index(self.current_student_index)
        pending_student = _sanitize_index(self._pending_passive_student)

        return ClassRollState(
            current_group=target_group,
            group_remaining=remaining_payload,
            group_last=last_payload,
            global_drawn=global_drawn_payload,
            current_student=current_student,
            pending_student=pending_student,
        )

    def _store_active_class_state(self, class_name: Optional[str] = None) -> None:
        if not PANDAS_READY:
            return

        # --- 核心修复：如果处于“等待加载”状态，绝对禁止保存当前状态 ---
        # 原因：此时内存中的是空白模板数据，保存它会覆盖掉配置文件中读取的真实历史记录。
        if getattr(self, "_student_data_pending_load", False):
            return
        # ---------------------------------------------------------

        self._prune_orphan_class_states()
        target = (class_name or self._resolve_active_class_name()).strip()
        if not target:
            return
        snapshot = self._capture_roll_state()
        if snapshot is None:
            return
        if not getattr(self, "_roll_state_mapping_checked", False):
            matched, total, missing = _evaluate_roll_state_mapping(snapshot, self.student_data)
            if total > 0 and missing > 0:
                logger.warning(
                    "点名状态与当前名单存在无法匹配的记录：%d/%d",
                    missing,
                    total,
                )
                if self.isVisible():
                    show_quiet_information(
                        self,
                        f"检测到 {missing} 条点名记录无法匹配当前名单，已自动忽略。",
                        "点名状态提示",
                    )
            self._roll_state_mapping_checked = True
        self._class_roll_states[target] = snapshot

    def _prune_orphan_class_states(self) -> None:
        if not self._class_roll_states:
            return
        # 修复：数据未加载时，不知道哪些班级有效，禁止清理以保护历史记录
        if getattr(self, "_student_data_pending_load", False):
            return

        workbook = self.student_workbook
        if workbook is None:
            return
        try:
            valid = {str(name).strip() for name in workbook.class_names() if str(name).strip()}
        except Exception:
            valid = set()
        if not valid:
            self._class_roll_states.clear()
            return
        for stored_name in list(self._class_roll_states.keys()):
            trimmed = str(stored_name).strip()
            if not trimmed or trimmed not in valid:
                self._class_roll_states.pop(stored_name, None)

    def _encode_class_states(self) -> str:
        payload = {name: state.to_json() for name, state in self._class_roll_states.items()}
        return json.dumps(payload, ensure_ascii=False)

    def _parse_legacy_roll_state(self, section: Mapping[str, str]) -> Optional[ClassRollState]:
        def _load_dict(key: str) -> Dict[str, Any]:
            raw = section.get(key, "")
            if not raw:
                return {}
            try:
                data = json.loads(raw)
            except Exception:
                return {}
            return data if isinstance(data, dict) else {}

        remaining = _load_dict("group_remaining")
        last = _load_dict("group_last")

        global_drawn_raw = section.get("global_drawn", "")
        global_payload: List[Union[int, str]] = []
        if global_drawn_raw:
            try:
                payload = json.loads(global_drawn_raw)
            except Exception:
                payload = []
            if isinstance(payload, list):
                for value in payload:
                    if isinstance(value, str):
                        text = value.strip()
                        if text:
                            global_payload.append(text)
                        continue
                    try:
                        global_payload.append(int(value))
                    except (TypeError, ValueError):
                        continue

        payload_map: Dict[str, Any] = {
            "current_group": section.get("current_group", self.current_group_name),
            "group_remaining": remaining,
            "group_last": last,
            "global_drawn": global_payload,
        }
        return ClassRollState.from_mapping(payload_map)

    def _restore_active_class_state(self, *, restore_current_student: bool = False) -> None:
        if not PANDAS_READY:
            return
        class_name = self._resolve_active_class_name()
        if not class_name:
            return
        snapshot = self._class_roll_states.get(class_name)
        if snapshot is None:
            return
        self._apply_roll_state(snapshot, restore_current_student=restore_current_student)

    def _can_apply_roll_state(self) -> bool:
        """检查当前是否具备恢复点名状态所需的数据上下文。"""

        if not PANDAS_READY:
            return False
        if self._student_data_pending_load:
            return False
        return isinstance(self.student_data, pd.DataFrame)

    def _apply_roll_state(self, snapshot: ClassRollState, *, restore_current_student: bool = True) -> None:
        if not self._can_apply_roll_state():
            return

        row_id_map = self._row_id_to_index_map()
        row_key_map = self._row_key_to_index_map()

        def _resolve_saved_index(value: Any) -> Optional[int]:
            if value is None:
                return None
            if isinstance(value, str):
                text = value.strip()
                if not text:
                    return None
                if text.startswith("rk:"):
                    mapped = row_key_map.get(text)
                    if mapped is not None:
                        return mapped
                mapped = row_id_map.get(text)
                if mapped is not None:
                    return mapped
                mapped = row_key_map.get(text)
                if mapped is not None:
                    return mapped
                if text.isdigit():
                    with contextlib.suppress(ValueError):
                        return int(text)
                return None
            try:
                return int(value)
            except (TypeError, ValueError):
                return None

        remaining_data = snapshot.group_remaining or {}
        last_data = snapshot.group_last or {}
        restored_global: Set[int] = set()
        for value in snapshot.global_drawn:
            idx = _resolve_saved_index(value)
            if idx is not None:
                restored_global.add(idx)

        existing_global = set(restored_global)
        self._global_drawn_students = set()
        self._group_drawn_history["全部"] = self._global_drawn_students

        for group, indices in remaining_data.items():
            if group not in self._group_all_indices:
                continue
            base_list = self._collect_base_indices(self._group_all_indices[group])
            base_set = set(base_list)
            if base_set:
                resolved = [idx for idx in (_resolve_saved_index(v) for v in indices) if idx is not None]
                restored_list = self._normalize_indices(resolved, allowed=base_set)
            else:
                restored_list = []
            self._group_remaining_indices[group] = restored_list

        for group, value in last_data.items():
            if group not in self._group_all_indices:
                continue
            if value is None:
                self._group_last_student[group] = None
                continue
            idx = _resolve_saved_index(value)
            if idx is None:
                continue
            base_indices = self._collect_base_indices(self._group_all_indices[group])
            base_set = set(base_indices)
            if base_set and idx not in base_set:
                continue
            self._group_last_student[group] = idx

        for group, base_indices in self._group_all_indices.items():
            normalized_base = self._collect_base_indices(base_indices)
            remaining_set = set(self._normalize_indices(self._group_remaining_indices.get(group, [])))
            drawn = {idx for idx in normalized_base if idx not in remaining_set}
            if group != "全部" and existing_global:
                drawn.update(idx for idx in existing_global if idx in normalized_base)
            seq = list(self._group_remaining_indices.get(group, []))
            seq.extend(idx for idx in normalized_base if idx not in seq)
            self._group_initial_sequences[group] = seq
            if group == "全部":
                self._global_drawn_students.update(drawn)
            else:
                self._group_drawn_history[group] = drawn
                self._global_drawn_students.update(drawn)

        if existing_global:
            self._global_drawn_students.update(existing_global)

        self._group_drawn_history["全部"] = self._global_drawn_students
        self._refresh_all_group_pool()

        target_group = _normalize_group_name(snapshot.current_group) if snapshot.current_group else ""
        if target_group not in self.groups:
            target_group = "全部" if "全部" in self.groups else (self.groups[0] if self.groups else "全部")
        self.current_group_name = target_group
        self._update_group_button_state(target_group)

        base_all = self._collect_base_indices(self._group_all_indices.get("全部", []))
        base_all_set = set(base_all)

        def _valid_index(value: Any) -> Optional[int]:
            idx = _resolve_saved_index(value)
            if idx is None:
                return None
            if base_all_set and idx not in base_all_set:
                return None
            return idx

        if restore_current_student:
            self.current_student_index = _valid_index(snapshot.current_student)
            self._pending_passive_student = _valid_index(snapshot.pending_student)
        else:
            self.current_student_index = None
            self._pending_passive_student = None

        self._store_active_class_state(self._resolve_active_class_name())
        self.roll_logic.validate_and_repair_state(context="apply_roll_state")

    def _update_class_button_label(self) -> None:
        if not hasattr(self, "class_button"):
            return
        name = ""
        if self.student_workbook is not None:
            base_name = self.current_class_name or self.student_workbook.active_class
            name = base_name.strip()
        text = name or "班级"
        self.class_button.setText(text)
        metrics = self.class_button.fontMetrics()
        baseline = metrics.horizontalAdvance("班级")
        active_width = metrics.horizontalAdvance(text)
        minimum = max(baseline, active_width) + 24
        if self.class_button.minimumWidth() != minimum:
            self.class_button.setMinimumWidth(minimum)
        has_data = self.student_workbook is not None and not self.student_workbook.is_empty()
        can_select = self.mode == "roll_call" and (has_data or self._student_data_pending_load)
        self.class_button.setEnabled(can_select)
        if has_data:
            self.class_button.setToolTip("选择班级")
        else:
            self.class_button.setToolTip("暂无学生数据，无法选择班级")

    def _ensure_student_data_ready(self) -> bool:
        """确保在需要访问学生数据前已经完成懒加载。"""

        if not self._student_data_pending_load:
            return True
        return self._load_student_data_if_needed()

    def show_class_selector(self) -> None:
        if self.mode != "roll_call":
            return
        if not self._ensure_student_data_ready():
            return
        workbook = self.student_workbook
        if workbook is None:
            show_quiet_information(self, "暂无学生数据，无法选择班级。")
            return
        class_names = workbook.class_names()
        if not class_names:
            show_quiet_information(self, "暂无班级可供选择。")
            return
        menu = QMenu(self)
        current = self.current_class_name or workbook.active_class
        for name in class_names:
            action = menu.addAction(name)
            action.setCheckable(True)
            action.setChecked(name == current)
            action.triggered.connect(lambda _checked=False, n=name: self._switch_class(n))
        pos = self.class_button.mapToGlobal(self.class_button.rect().bottomLeft())
        menu.exec(pos)

    def _switch_class(self, class_name: str) -> None:
        if self.student_workbook is None:
            return
        if class_name not in self.student_workbook.class_names():
            return
        target = class_name.strip()
        current = self.current_class_name or self.student_workbook.active_class
        if target == current:
            return
        if not self._ensure_student_data_ready():
            return
        self._snapshot_current_class()
        # 通过统一的 apply 流程切换班级，确保状态与 UI 同步
        self.student_workbook.set_active_class(target)
        self.current_class_name = target
        self._apply_student_workbook(self.student_workbook, propagate=True)
        self._persist_roll_state_immediately()
        self._schedule_save()

    def _load_student_data_if_needed(self) -> bool:
        if self.student_workbook is not None:
            return True
        if self._student_data_loading:
            return False
        if not (PANDAS_AVAILABLE and OPENPYXL_AVAILABLE):
            return False
        self._student_data_loading = True
        try:
            workbook = load_student_data(self)
        finally:
            self._student_data_loading = False
        if workbook is None:
            return False
        self.student_workbook = workbook
        self._apply_student_workbook(workbook, propagate=True)
        self._student_data_pending_load = False
        self._update_class_button_label()
        self.display_current_student()
        self._schedule_save()
        return True
    def _propagate_student_dataframe(self) -> None:
        parent = self.parent()
        if parent is None:
            return
        if hasattr(parent, "student_data"):
            try:
                parent.student_data = self.student_data  # type: ignore[assignment]
            except Exception:
                pass
        if hasattr(parent, "student_workbook"):
            try:
                parent.student_workbook = self.student_workbook  # type: ignore[assignment]
            except Exception:
                pass

    def _apply_saved_fonts(self) -> None:
        id_font = QFont("Microsoft YaHei UI", self.last_id_font_size, QFont.Weight.Bold)
        name_weight = QFont.Weight.Normal if self.name_font_family in {"楷体", "KaiTi"} else QFont.Weight.Bold
        name_font = QFont(self.name_font_family, self.last_name_font_size, name_weight)
        timer_font = QFont("Consolas", self.last_timer_font_size, QFont.Weight.Bold)
        self.id_label.setFont(id_font)
        self.name_label.setFont(name_font)
        self.time_display_label.setFont(timer_font)

    def _sync_settings_button_width(self, toolbar_height: int) -> None:
        if not hasattr(self, "settings_button"):
            return
        # 同步设置按钮的宽度与名单按钮一致
        if hasattr(self, "list_button") and isinstance(self.list_button, QPushButton):
            target_width = self.list_button.minimumWidth()
            self.settings_button.setMinimumWidth(target_width)
            self.settings_button.setMaximumWidth(target_width)

    def _build_menu(self) -> QMenu:
        menu = QMenu("设置", self)
        menu.setStyleSheet(StyleConfig.MENU_STYLE)
        self._add_menu_section_display(menu)
        menu.addSeparator()
        self._add_menu_section_remote_presenter(menu)
        menu.addSeparator()
        self._add_menu_section_speech(menu)
        menu.addSeparator()
        self._add_menu_section_timer(menu)
        menu.addSeparator()
        self._add_menu_section_diagnostics(menu)

        return menu

    def _add_menu_section_display(self, menu: QMenu) -> None:
        disp = menu.addMenu("显示选项")
        self.show_id_action = _menu_add_check_action(
            disp,
            "显示学号",
            checked=self.show_id,
            on_toggled=self._on_display_option_changed,
        )
        self.show_name_action = _menu_add_check_action(
            disp,
            "显示姓名",
            checked=self.show_name,
            on_toggled=self._on_display_option_changed,
        )
        self.show_photo_action = _menu_add_check_action(
            disp,
            "显示照片",
            checked=self.show_photo,
            on_toggled=self._on_display_option_changed,
        )
        self.photo_duration_menu = disp.addMenu("照片显示时间")
        duration_choices: List[Tuple[int, str]] = [
            (0, "不自动关闭"),
            (3, "3 秒"),
            (5, "5 秒"),
            (10, "10 秒"),
        ]
        current_seconds = int(self.photo_duration_seconds)
        self.photo_duration_actions = _menu_add_choice_actions(
            self.photo_duration_menu,
            duration_choices,
            current_key=current_seconds,
            on_select=self._set_photo_duration,
        )
        self._sync_photo_duration_actions()

    def _add_menu_section_remote_presenter(self, menu: QMenu) -> None:
        remote_menu = menu.addMenu("翻页笔遥控点名")
        self.remote_presenter_action = _menu_add_check_action(
            remote_menu,
            "启用",
            checked=self.remote_presenter_enabled,
            on_toggled=self._toggle_remote_presenter_call,
        )
        self.remote_key_menu = remote_menu.addMenu("翻页笔按键")
        remote_key_choices: List[Tuple[str, str]] = [
            ("tab", "Tab键（切换超链接）"),
            ("b", "B键（长按黑屏）"),
        ]
        self.remote_key_actions = _menu_add_choice_actions(
            self.remote_key_menu,
            remote_key_choices,
            current_key=self.remote_presenter_key,
            on_select=self._set_remote_presenter_key,
        )
        self._sync_remote_presenter_actions()

    def _add_menu_section_speech(self, menu: QMenu) -> None:
        speech = menu.addMenu("语音播报")
        manager = self.tts_manager
        checked = bool(self.speech_enabled)
        self.speech_enabled_action = _menu_add_check_action(
            speech,
            "启用语音播报",
            checked=checked,
            on_toggled=self._toggle_speech,
        )
        self.voice_menu = speech.addMenu("选择发音人")
        self.voice_actions = []
        self.engine_menu = speech.addMenu("语音引擎")
        engine_choices = [
            ("pyttsx3", "pyttsx3（默认，跟随系统输出）"),
            ("sapi", "SAPI（win32com，可选输出设备）"),
        ]
        self.engine_actions = _menu_add_choice_actions(
            self.engine_menu,
            engine_choices,
            current_key=self.selected_engine,
            on_select=self._set_engine,
        )
        self.output_menu = speech.addMenu("选择输出设备")
        self.output_actions = []
        if manager and manager.available:
            self.speech_enabled_action.setEnabled(True)
            self.speech_enabled_action.setToolTip("点名时自动朗读当前学生姓名。")
            self._refresh_voice_menu_actions(manager)
            self._refresh_output_menu_actions(manager)
        else:
            self._disable_voice_menu_with_reason()
            self._disable_output_menu_with_reason()

    def _add_menu_section_timer(self, menu: QMenu) -> None:
        timer_menu = menu.addMenu("倒计时提示")
        self._timer_sound_choices = [
            ("gentle", "柔和铃声"),
            ("bell", "上课铃"),
            ("digital", "电子滴答"),
            ("buzz", "蜂鸣器"),
            ("urgent", "紧张倒计时"),
        ]
        end_menu = timer_menu.addMenu("结束提示音")
        self.timer_sound_action = _menu_add_check_action(
            end_menu,
            "启用结束提示音",
            checked=self.timer_sound_enabled,
            on_toggled=self._toggle_timer_sound,
        )
        self.timer_sound_menu = end_menu.addMenu("声音样式")
        self.timer_sound_actions = _menu_add_choice_actions(
            self.timer_sound_menu,
            self._timer_sound_choices,
            current_key=self.timer_sound_variant,
            on_select=lambda k: self._set_timer_sound_variant(str(k), preview=True),
        )

        reminder_menu = timer_menu.addMenu("中途提示音")
        self.timer_reminder_action = _menu_add_check_action(
            reminder_menu,
            "启用中途提示音",
            checked=self.timer_reminder_enabled,
            on_toggled=self._toggle_timer_reminder,
        )
        self._reminder_sound_choices = [
            ("soft_beep", "轻柔提示"),
            ("ping", "清脆提示"),
            ("chime", "简洁钟声"),
            ("pulse", "节奏哔哔"),
            ("short_bell", "短铃提示"),
        ]
        self.reminder_sound_menu = reminder_menu.addMenu("声音样式")
        self.reminder_sound_actions = _menu_add_choice_actions(
            self.reminder_sound_menu,
            self._reminder_sound_choices,
            current_key=self.timer_reminder_sound_variant,
            on_select=lambda k: self._set_reminder_sound_variant(str(k), preview=True),
        )

        self.reminder_interval_menu = reminder_menu.addMenu("提醒间隔")
        if self.timer_reminder_interval_minutes <= 0:
            self.timer_reminder_interval_minutes = 3
        interval_choices: List[Tuple[int, str]] = [
            (1, "每 1 分钟"),
            (3, "每 3 分钟"),
            (5, "每 5 分钟"),
            (10, "每 10 分钟"),
        ]
        self.reminder_interval_actions = _menu_add_choice_actions(
            self.reminder_interval_menu,
            interval_choices,
            current_key=int(self.timer_reminder_interval_minutes),
            on_select=self._set_reminder_interval,
        )
        self._sync_timer_sound_actions()
        self._sync_reminder_menu_state()

    def _add_menu_section_diagnostics(self, menu: QMenu) -> None:
        diag = menu.addAction("系统兼容性诊断")
        diag.triggered.connect(self.show_system_diagnostics)

    def show_system_diagnostics(self) -> None:
        result = collect_system_diagnostics()
        show_diagnostic_result(self, result)

    def show_roll_call_settings_dialog(self) -> None:
        available_classes: List[str] = []
        workbook = self.student_workbook
        if workbook is not None:
            try:
                available_classes = [str(name).strip() for name in workbook.class_names() if str(name).strip()]
            except Exception:
                available_classes = []
        if self.current_class_name and self.current_class_name not in available_classes:
            available_classes.append(self.current_class_name)

        manager = self.tts_manager
        voice_ids = manager.voice_ids if manager and manager.available else []
        output_ids = manager.output_ids if manager and manager.available else []
        output_labels = getattr(manager, "_output_descriptions", None) if manager else None
        fallback_output_ids, fallback_output_labels = _collect_sapi_outputs()
        if fallback_output_ids:
            output_ids = fallback_output_ids
            output_labels = fallback_output_labels or output_labels

        dialog = RollCallSettingsDialog(
            self,
            show_id=self.show_id,
            show_name=self.show_name,
            show_photo=self.show_photo,
            photo_duration_seconds=self.photo_duration_seconds,
            photo_shared_class=self.photo_shared_class,
            available_classes=available_classes,
            speech_enabled=self.speech_enabled,
            speech_engine=self.selected_engine,
            voice_ids=voice_ids,
            current_voice_id=self.selected_voice_id,
            output_ids=output_ids,
            output_labels=output_labels,
            current_output_id=self.selected_output_id,
            timer_sound_enabled=self.timer_sound_enabled,
            timer_sound_variant=self.timer_sound_variant,
            timer_reminder_enabled=self.timer_reminder_enabled,
            timer_reminder_interval_minutes=self.timer_reminder_interval_minutes,
            timer_reminder_sound_variant=self.timer_reminder_sound_variant,
            remote_presenter_enabled=self.remote_presenter_enabled,
            remote_presenter_key=self.remote_presenter_key,
            sounddevice_available=SOUNDDEVICE_AVAILABLE,
        )
        dialog.diagnostic_button.clicked.connect(self.show_system_diagnostics)
        if dialog.exec():
            settings = dialog.get_settings()
            self._apply_roll_call_settings(settings)

    def _apply_roll_call_settings(self, settings: Mapping[str, Any]) -> None:
        show_id = bool(settings.get("show_id", True))
        show_name = bool(settings.get("show_name", True))
        if not (show_id or show_name):
            show_name = True
        self.show_id = show_id
        self.show_name = show_name

        show_photo = bool(settings.get("show_photo", False))
        if self.show_photo != show_photo:
            self.show_photo = show_photo
            if not show_photo:
                self._hide_student_photo(force=True)
        else:
            self.show_photo = show_photo

        self.update_display_layout()
        self.display_current_student()

        duration = int(settings.get("photo_duration_seconds", self.photo_duration_seconds))
        self._set_photo_duration(duration)

        shared_class = str(settings.get("photo_shared_class", "") or "").strip()
        if shared_class != self.photo_shared_class:
            self.photo_shared_class = shared_class
            if self.show_photo and self.mode == "roll_call":
                self._hide_student_photo(force=True)
                self.display_current_student()

        engine = str(settings.get("speech_engine", self.selected_engine) or "").strip().lower()
        if engine and engine != self.selected_engine:
            self._set_engine(engine)
        speech_enabled = bool(settings.get("speech_enabled", self.speech_enabled))
        if speech_enabled != self.speech_enabled:
            self._toggle_speech(speech_enabled)

        voice_id = str(settings.get("speech_voice_id", "") or "").strip()
        if voice_id:
            self._set_voice(voice_id)
        output_id = str(settings.get("speech_output_id", "") or "").strip()
        if output_id:
            self._set_output(output_id)

        self._toggle_timer_sound(bool(settings.get("timer_sound_enabled", self.timer_sound_enabled)))
        self._set_timer_sound_variant(str(settings.get("timer_sound_variant", self.timer_sound_variant)), preview=False)
        self._toggle_timer_reminder(bool(settings.get("timer_reminder_enabled", self.timer_reminder_enabled)))
        self._set_reminder_interval(int(settings.get("timer_reminder_interval_minutes", self.timer_reminder_interval_minutes)))
        self._set_reminder_sound_variant(
            str(settings.get("timer_reminder_sound_variant", self.timer_reminder_sound_variant)),
            preview=False,
        )

        self._toggle_remote_presenter_call(bool(settings.get("remote_presenter_enabled", self.remote_presenter_enabled)))
        remote_key = str(settings.get("remote_presenter_key", self.remote_presenter_key) or "").strip().lower()
        if remote_key:
            self._set_remote_presenter_key(remote_key)
        self._update_menu_state()

    def _update_menu_state(self) -> None:
        if self.show_id_action.isChecked() != self.show_id:
            _set_action_checked_safely(self.show_id_action, self.show_id)
        if self.show_name_action.isChecked() != self.show_name:
            _set_action_checked_safely(self.show_name_action, self.show_name)
        _set_action_checked_safely(self.timer_sound_action, self.timer_sound_enabled)
        if SOUNDDEVICE_AVAILABLE:
            self.timer_sound_action.setEnabled(True)
            self.timer_sound_menu.setEnabled(True)
        else:
            self.timer_sound_action.setEnabled(False)
            self.timer_sound_menu.setEnabled(False)
        self._sync_timer_sound_actions()
        self._sync_reminder_menu_state()
        for action in getattr(self, "engine_actions", []):
            if not isinstance(action, QAction):
                continue
            data = action.data()
            try:
                key = str(data) if data is not None else ""
            except Exception:
                key = ""
            _set_action_checked_safely(action, key == self.selected_engine)
        manager = self.tts_manager
        if self._speech_init_pending:
            if hasattr(self, "speech_enabled_action"):
                self.speech_enabled_action.setEnabled(False)
                _set_action_checked_safely(self.speech_enabled_action, self.speech_enabled)
                self.speech_enabled_action.setToolTip("语音引擎初始化中，请稍候。")
            if hasattr(self, "voice_menu") and self.voice_menu is not None:
                self.voice_menu.setEnabled(False)
                self.voice_menu.setToolTip("语音引擎初始化中，请稍候。")
            if hasattr(self, "output_menu") and self.output_menu is not None:
                self.output_menu.setEnabled(False)
                self.output_menu.setToolTip("语音引擎初始化中，请稍候。")
            self._schedule_speech_init()
            self._sync_remote_presenter_actions()
            return
        if manager and manager.available:
            self.speech_enabled_action.setEnabled(True)
            _set_action_checked_safely(self.speech_enabled_action, self.speech_enabled)
            self.speech_enabled_action.setToolTip("点名时自动朗读当前学生姓名。")
            if (manager.supports_voice_selection and manager.voice_ids) and not self.voice_actions:
                self._refresh_voice_menu_actions(manager)
            if (manager.supports_output_selection and manager.output_ids) and not self.output_actions:
                self._refresh_output_menu_actions(manager)
            if manager.supports_voice_selection and manager.voice_ids:
                self.voice_menu.setEnabled(True)
                for act in self.voice_actions:
                    _set_action_checked_safely(act, act.text() == manager.current_voice_id)
                self.voice_menu.setToolTip("")
            else:
                self.voice_menu.setEnabled(False)
                self.voice_menu.setToolTip("当前语音引擎不支持切换发音人。")
            if manager.supports_output_selection and manager.output_ids:
                self.output_menu.setEnabled(True)
                for act in self.output_actions:
                    data = act.data()
                    try:
                        value = str(data) if data is not None else ""
                    except Exception:
                        value = ""
                    _set_action_checked_safely(act, value == manager.current_output_id)
                self.output_menu.setToolTip("")
            else:
                self._disable_output_menu_with_reason()
        else:
            self._disable_voice_menu_with_reason()
            self._disable_output_menu_with_reason()
            if not self._speech_issue_reported:
                self._diagnose_speech_engine()
        self._sync_remote_presenter_actions()

    def _sync_timer_sound_actions(self) -> None:
        for act in getattr(self, "timer_sound_actions", []):
            data = act.data()
            key = str(data) if data is not None else ""
            _set_action_checked_safely(act, key == self.timer_sound_variant)

    def _sync_reminder_menu_state(self) -> None:
        enabled = bool(self.timer_reminder_enabled and self.timer_modes[self.timer_mode_index] == "countdown")
        if hasattr(self, "timer_reminder_action"):
            _set_action_checked_safely(self.timer_reminder_action, self.timer_reminder_enabled)
            self.timer_reminder_action.setEnabled(True)
        for act in getattr(self, "reminder_interval_actions", []):
            try:
                minutes = int(act.data())
            except Exception:
                minutes = -1
            _set_action_checked_safely(act, minutes == self.timer_reminder_interval_minutes)
            act.setEnabled(True)
        for act in getattr(self, "reminder_sound_actions", []):
            data = act.data()
            key = str(data) if data is not None else ""
            _set_action_checked_safely(act, key == self.timer_reminder_sound_variant)
            act.setEnabled(True)
        if hasattr(self, "reminder_interval_menu"):
            self.reminder_interval_menu.setEnabled(True)
        if hasattr(self, "reminder_sound_menu"):
            self.reminder_sound_menu.setEnabled(True)
            tooltip = "" if SOUNDDEVICE_AVAILABLE else "当前环境未检测到音频播放库，选择会保存，安装音频依赖后生效。"
            self.reminder_sound_menu.setToolTip(tooltip)

    def _apply_remote_presenter_runtime_state(self, *, show_feedback: bool = False) -> None:
        controller = getattr(self, "remote_presenter_controller", None)
        available = bool(controller and controller.is_available())
        self._remote_presenter_paused = False
        if not available:
            if controller is not None:
                controller.stop()
            if self.remote_presenter_enabled:
                self.remote_presenter_enabled = False
                if show_feedback:
                    show_quiet_information(self, "当前系统无法拦截翻页笔按键，已自动关闭遥控点名。")
            self._sync_remote_presenter_actions()
            return
        if not self.remote_presenter_enabled:
            controller.stop()
            self._sync_remote_presenter_actions()
            return
        if self.mode != "roll_call":
            controller.stop()
            self._remote_presenter_paused = True
            self._sync_remote_presenter_actions()
            if show_feedback:
                show_quiet_information(self, "计时/秒表模式下已暂时停用遥控点名，切回点名窗口后自动恢复。")
            return
        if not controller.set_enabled(True):
            self.remote_presenter_enabled = False
            self._remote_presenter_paused = False
            self._sync_remote_presenter_actions()
            if show_feedback:
                show_quiet_information(self, "启用遥控点名失败，可能缺少系统权限。")
            return
        self._sync_remote_presenter_actions()

    def _sync_remote_presenter_actions(self) -> None:
        controller = getattr(self, "remote_presenter_controller", None)
        available = bool(controller and controller.is_available())
        paused = bool(getattr(self, "_remote_presenter_paused", False))
        status_checked = self.remote_presenter_enabled and available
        if hasattr(self, "remote_presenter_action"):
            _set_action_checked_safely(self.remote_presenter_action, status_checked)
            self.remote_presenter_action.setEnabled(available)
            tooltip = ""
            if not available:
                tooltip = "需要在 Windows 中授予全局键盘监听权限。"
            elif paused:
                tooltip = "计时/秒表模式下暂时停用，返回点名窗口后自动恢复。"
            self.remote_presenter_action.setToolTip(tooltip)
        for act in getattr(self, "remote_key_actions", []):
            if not isinstance(act, QAction):
                continue
            data = act.data()
            try:
                key = str(data) if data is not None else ""
            except Exception:
                key = ""
            _set_action_checked_safely(act, key == self.remote_presenter_key)
            act.setEnabled(available)
            if paused:
                act.setToolTip("计时/秒表模式下遥控点名已暂停。")
            else:
                act.setToolTip("" if available else "当前系统不支持全局监听。")
        if hasattr(self, "remote_key_menu"):
            self.remote_key_menu.setEnabled(available)
            if paused:
                self.remote_key_menu.setToolTip("计时/秒表模式下遥控点名已暂停。")
            else:
                self.remote_key_menu.setToolTip("" if available else "当前系统不支持全局监听。")

    def _toggle_remote_presenter_call(self, enabled: bool) -> None:
        controller = getattr(self, "remote_presenter_controller", None)
        self.remote_presenter_enabled = bool(enabled)
        self._apply_remote_presenter_runtime_state(show_feedback=True)
        self._schedule_save()

    def _set_remote_presenter_key(self, key: str) -> None:
        normalized = (key or "tab").strip().lower() or "tab"
        if normalized == self.remote_presenter_key:
            self._sync_remote_presenter_actions()
            return
        self.remote_presenter_key = normalized
        controller = getattr(self, "remote_presenter_controller", None)
        if controller is not None:
            controller.set_key(normalized)
            if self.remote_presenter_enabled:
                controller.restart()
        self._sync_remote_presenter_actions()
        self._schedule_save()

    def trigger_remote_presenter_call(self) -> None:
        if not self.remote_presenter_enabled or getattr(self, "_remote_presenter_paused", False):
            return
        if not self._ensure_student_data_ready():
            return
        self.roll_student(speak=True, ensure_mode=True)

    def _refresh_voice_menu_actions(self, manager: Optional["TTSManager"]) -> None:
        """在语音引擎延迟初始化后重建发音人菜单，保持 UI 可用。"""

        if manager is None or not manager.available or self.voice_menu is None:
            return
        self.voice_menu.clear()
        self.voice_actions = []
        if manager.supports_voice_selection and manager.voice_ids:
            for vid in manager.voice_ids:
                act = self.voice_menu.addAction(vid)
                act.setCheckable(True)
                act.setChecked(vid == manager.current_voice_id)
                act.triggered.connect(lambda _c, v=vid: self._set_voice(v))
                self.voice_actions.append(act)
            self.voice_menu.setEnabled(True)
            self.voice_menu.setToolTip("")
        else:
            self.voice_menu.setEnabled(False)
            self.voice_menu.setToolTip("当前语音引擎不支持切换发音人。")

    def _refresh_output_menu_actions(self, manager: Optional["TTSManager"]) -> None:
        """刷新输出设备菜单，支持 win32com SAPI 输出切换。"""

        if manager is None or not manager.available or self.output_menu is None:
            return
        self.output_menu.clear()
        self.output_actions = []
        if manager.supports_output_selection and manager.output_ids:
            for oid in manager.output_ids:
                label = manager._output_descriptions.get(oid, "") if hasattr(manager, "_output_descriptions") else ""
                text = label or oid
                act = self.output_menu.addAction(text)
                act.setCheckable(True)
                act.setData(oid)
                act.setChecked(oid == manager.current_output_id)
                act.triggered.connect(lambda _c, v=oid: self._set_output(v))
                self.output_actions.append(act)
            self.output_menu.setEnabled(True)
            self.output_menu.setToolTip("")
        else:
            self._disable_output_menu_with_reason()

    def _disable_voice_menu_with_reason(self) -> None:
        self.voice_menu.setEnabled(False)
        self.speech_enabled_action.setEnabled(False)
        _set_action_checked_safely(self.speech_enabled_action, self.speech_enabled)
        reason, suggestions = self._collect_speech_issue_details(force_refresh=False)
        tooltip_lines = [reason] if reason else []
        tooltip_lines.extend(suggestions)
        if tooltip_lines:
            self.speech_enabled_action.setToolTip("\n".join(tooltip_lines))

    def _disable_output_menu_with_reason(self) -> None:
        if hasattr(self, "output_menu") and self.output_menu is not None:
            self.output_menu.setEnabled(False)
            self.output_menu.setToolTip("当前语音引擎不支持切换输出设备。")

    def _schedule_speech_init(self) -> None:
        if not self._speech_init_pending or self._speech_init_scheduled:
            return
        self._speech_init_scheduled = True
        QTimer.singleShot(0, self._warm_speech_engine)

    def _warm_speech_engine(self) -> None:
        self._speech_init_scheduled = False
        if not self._speech_init_pending:
            return
        manager = self.tts_manager
        if manager is None:
            manager = self._ensure_speech_manager()
        if manager is None:
            self._speech_init_pending = False
            return
        _ = manager.available
        self._speech_init_pending = False
        QTimer.singleShot(0, self._update_menu_state)

    def _default_speech_suggestions(self) -> List[str]:
        hints: List[str] = []
        if sys.platform == "win32":
            hints.append("请确认 Windows 已启用 SAPI5 中文语音包。")
        elif sys.platform == "darwin":
            hints.append("请在系统“辅助功能 -> 语音”中启用所需的语音包。")
        else:
            hints.append("请确保系统已安装可用的语音引擎（如 espeak）并重新启动程序。")
        hints.append("可尝试重新安装 pyttsx3 或检查语音服务状态后重启软件。")
        return hints

    def _dedupe_suggestions(self, values: List[str]) -> List[str]:
        return dedupe_strings(values)

    def _collect_speech_issue_details(self, *, force_refresh: bool = False) -> tuple[str, List[str]]:
        manager = self.tts_manager
        reason = ""
        suggestions: List[str] = []
        if manager is None:
            reason = "无法初始化系统语音引擎"
            suggestions = self._default_speech_suggestions()
        elif not manager.available:
            reason, suggestions = manager.diagnostics()
            reason = reason or "无法初始化系统语音引擎"
            if not suggestions:
                suggestions = self._default_speech_suggestions()
        elif manager.supports_voice_selection and not getattr(manager, "voice_ids", []):
            reason = "未检测到任何可用的发音人"
            suggestions = self._default_speech_suggestions()
            suggestions.append("请在操作系统语音设置中添加语音包后重新启动程序。")

        env_reason, env_suggestions = detect_speech_environment_issues(force_refresh=force_refresh)
        if env_reason:
            if not reason:
                reason = env_reason
            elif env_reason not in reason:
                reason = f"{reason}；{env_reason}"
        suggestions.extend(env_suggestions)
        if not suggestions and reason:
            suggestions = self._default_speech_suggestions()
        return reason, self._dedupe_suggestions(suggestions)

    def _ensure_speech_manager(self) -> Optional[TTSManager]:
        manager = self.tts_manager
        if manager and manager.available:
            return manager
        if manager is not None:
            try:
                manager.shutdown()
            except Exception:
                pass
        manager = TTSManager(
            self.selected_voice_id,
            self.selected_output_id,
            preferred_engine=self.selected_engine,
            parent=self,
        )
        self.tts_manager = manager
        if manager.available:
            self._speech_issue_reported = False
            QTimer.singleShot(0, self._update_menu_state)
        return manager

    def _diagnose_speech_engine(self) -> None:
        if self._speech_issue_reported:
            return
        action = getattr(self, "speech_enabled_action", None)
        if action is None or action.isEnabled():
            return
        if not self.isVisible():
            if not self._speech_check_scheduled:
                self._speech_check_scheduled = True
                QTimer.singleShot(200, self._diagnose_speech_engine)
            return
        reason, suggestions = self._collect_speech_issue_details(force_refresh=True)
        if not reason:
            return
        advice = "\n".join(f"· {line}" for line in suggestions)
        message = f"语音播报功能当前不可用：{reason}"
        if advice:
            message = f"{message}\n{advice}"
        show_quiet_information(self, message, "语音播报提示")
        self._speech_issue_reported = True
        self._speech_check_scheduled = False

    def eventFilter(self, obj, e):
        if obj in (self.id_label, self.name_label) and e.type() == QEvent.Type.MouseButtonPress:
            if e.button() == Qt.MouseButton.LeftButton:
                self.roll_student()
                return True
        return super().eventFilter(obj, e)

    def _on_display_option_changed(self) -> None:
        photo_checked = getattr(self, "show_photo_action", None)
        sender = self.sender()
        if not self.show_id_action.isChecked() and not self.show_name_action.isChecked():
            if sender is self.show_id_action and hasattr(self, "show_name_action"):
                self.show_name_action.setChecked(True)
            else:
                self.show_id_action.setChecked(True)
            return
        self.show_id = self.show_id_action.isChecked()
        self.show_name = self.show_name_action.isChecked()
        if photo_checked is not None:
            self.show_photo = photo_checked.isChecked()
        if not self.show_photo:
            self._hide_student_photo(force=True)
        self.update_display_layout()
        self.display_current_student()
        self._schedule_save()

    def _set_photo_duration(self, seconds: int) -> None:
        seconds = max(0, int(seconds))
        if self.photo_duration_seconds == seconds:
            self._sync_photo_duration_actions()
            return
        self.photo_duration_seconds = seconds
        self._sync_photo_duration_actions()
        overlay = getattr(self, "_photo_overlay", None)
        if overlay is not None and overlay.isVisible():
            overlay.schedule_auto_close(int(self.photo_duration_seconds * 1000))
        self._schedule_save()

    def _sync_photo_duration_actions(self) -> None:
        current = int(self.photo_duration_seconds)
        for action in getattr(self, "photo_duration_actions", []):
            if not isinstance(action, QAction):
                continue
            data = action.data()
            try:
                value = int(data) if data is not None else 0
            except (TypeError, ValueError):
                value = 0
            _set_action_checked_safely(action, value == current)

    def _toggle_speech(self, enabled: bool) -> None:
        if not enabled:
            self.speech_enabled = False
            self._schedule_save()
            return
        manager = self._ensure_speech_manager()
        if not manager or not manager.available:
            reason, suggestions = self._collect_speech_issue_details(force_refresh=True)
            message = reason or "未检测到语音引擎，无法开启语音播报。"
            advice = "\n".join(f"· {line}" for line in suggestions)
            if advice:
                message = f"{message}\n{advice}"
            show_quiet_information(self, message, "语音播报提示")
            self.speech_enabled_action.setChecked(False)
            self._speech_issue_reported = True
            return
        if manager.supports_voice_selection and not getattr(manager, "voice_ids", []):
            reason, suggestions = self._collect_speech_issue_details(force_refresh=True)
            message = reason or "未检测到可用的发音人。"
            advice = "\n".join(f"· {line}" for line in suggestions)
            if advice:
                message = f"{message}\n{advice}"
            show_quiet_information(self, message, "语音播报提示")
            self.speech_enabled_action.setChecked(False)
            self._speech_issue_reported = True
            return
        self.speech_enabled = enabled
        self._schedule_save()

    def _set_voice(self, voice_id: str) -> None:
        manager = self._ensure_speech_manager()
        if not manager or not manager.supports_voice_selection:
            return
        manager.set_voice(voice_id)
        self.selected_voice_id = voice_id
        for action in self.voice_actions:
            _set_action_checked_safely(action, action.text() == voice_id)
        self._schedule_save()

    def _set_output(self, output_id: str) -> None:
        manager = self._ensure_speech_manager()
        if not manager or not manager.supports_output_selection:
            return
        manager.set_output(output_id)
        self.selected_output_id = output_id
        for action in self.output_actions:
            data = action.data()
            try:
                value = str(data) if data is not None else ""
            except Exception:
                value = ""
            _set_action_checked_safely(action, value == output_id)
        self._schedule_save()

    def _set_engine(self, engine: str) -> None:
        engine = (engine or "pyttsx3").strip().lower()
        if engine == self.selected_engine:
            return
        self.selected_engine = engine
        self._speech_issue_reported = False
        if self.tts_manager is not None:
            try:
                self.tts_manager.shutdown()
            except Exception:
                pass
        self.tts_manager = None
        if self.speech_enabled:
            self._ensure_speech_manager()
        self._update_menu_state()
        self._schedule_save()

    def _toggle_timer_sound(self, enabled: bool) -> None:
        self.timer_sound_enabled = enabled
        self._sync_timer_sound_actions()
        self._sync_reminder_menu_state()
        self._schedule_save()

    def _set_timer_sound_variant(self, variant: str, *, preview: bool = False) -> None:
        variant = (variant or "gentle").strip().lower()
        if variant == self.timer_sound_variant:
            self._sync_timer_sound_actions()
            return
        self.timer_sound_variant = variant
        self._sync_timer_sound_actions()
        if preview:
            self.play_timer_sound(kind="end", preview=True, variant_override=variant)
        self._schedule_save()

    def _toggle_timer_reminder(self, enabled: bool) -> None:
        self.timer_reminder_enabled = enabled
        if enabled and self.timer_reminder_interval_minutes <= 0:
            self.timer_reminder_interval_minutes = 3
        if not enabled:
            self._reset_reminder_progress()
        self._sync_reminder_menu_state()
        self._schedule_save()

    def _set_reminder_interval(self, minutes: int) -> None:
        minutes = max(0, int(minutes))
        if minutes == self.timer_reminder_interval_minutes:
            self._sync_reminder_menu_state()
            return
        self.timer_reminder_interval_minutes = minutes
        if minutes == 0:
            self.timer_reminder_enabled = False
        elif not self.timer_reminder_enabled:
            self.timer_reminder_enabled = True
        self._reset_reminder_progress()
        self._sync_reminder_menu_state()
        self._schedule_save()

    def _set_reminder_sound_variant(self, variant: str, *, preview: bool = False) -> None:
        variant = (variant or "soft_beep").strip().lower()
        if variant == self.timer_reminder_sound_variant:
            self._sync_reminder_menu_state()
            return
        self.timer_reminder_sound_variant = variant
        self._sync_reminder_menu_state()
        if preview:
            self.play_timer_sound(kind="reminder", preview=True, variant_override=variant)
        self._schedule_save()

    def _speak_text(self, text: str) -> None:
        if not text:
            return
        manager = self.tts_manager
        if manager is None and self.speech_enabled:
            manager = self._ensure_speech_manager()
        if not (self.speech_enabled and manager and manager.available):
            return
        manager.speak(text)

    def _announce_current_student(self) -> None:
        if (
            not self.speech_enabled
            or self.tts_manager is None
            or not self.tts_manager.available
            or self.current_student_index is None
            or self.student_data is None
            or self.student_data.empty
        ):
            return
        stu = self._safe_get_student_row(self.current_student_index)
        if stu is None:
            return
        name_value = stu.get("姓名", "")
        if isinstance(name_value, str):
            name = name_value.strip()
        else:
            name = str(name_value).strip() if pd.notna(name_value) else ""
        if name:
            self._speak_text(name)

    def show_student_selector(self) -> None:
        if self.mode != "roll_call":
            return
        if self.student_data is None or self.student_data.empty:
            show_quiet_information(self, "暂无学生数据，无法显示名单。")
            return
        df = self.student_data
        sid_series = (
            df["学号"].map(_compact_text)
            if "学号" in df.columns
            else pd.Series([""] * len(df), index=df.index)
        )
        name_series = (
            df["姓名"].map(_normalize_text)
            if "姓名" in df.columns
            else pd.Series([""] * len(df), index=df.index)
        )
        ids = sid_series.tolist()
        names = name_series.tolist()

        records: List[tuple[Tuple[int, Union[int, str], str, str], str, str, int]] = []
        for idx, sid_display, name in zip(df.index.tolist(), ids, names):
            try:
                normalized_idx = int(idx)
            except (TypeError, ValueError):
                normalized_idx = idx
            sort_key = _student_sort_key(str(sid_display), str(name))
            records.append((sort_key, sid_display, name, normalized_idx))
        if not records:
            show_quiet_information(self, "当前没有可显示的学生名单。")
            return

        group_name = self.current_group_name or "全部"
        base_indices = set(self._collect_base_indices(self._group_all_indices.get(group_name, [])))
        called_set: set[int] = set()
        if group_name == "全部":
            called_set = set(self._global_drawn_students)
            if not base_indices:
                base_indices = set(self._collect_base_indices(self._group_all_indices.get("全部", [])))
        else:
            called_set = set(self._group_drawn_history.get(group_name, set()))
            if not base_indices:
                base_indices = set(self._collect_base_indices(self._group_all_indices.get("全部", [])))
            called_set.update(idx for idx in self._global_drawn_students if idx in base_indices)
        if base_indices:
            called_set = {idx for idx in called_set if idx in base_indices}
        if base_indices and group_name != "全部":
            records = [record for record in records if record[3] in base_indices]
            if not records:
                show_quiet_information(self, "当前分组没有可显示的学生名单。")
                return

        records.sort(key=lambda item: item[0])

        dialog_data = []
        for _, sid, name, data_idx in records:
            display_sid = sid if sid else "无学号"
            display_name = name or "未命名"
            called = False
            try:
                called = int(data_idx) in called_set
            except (TypeError, ValueError):
                called = data_idx in called_set
            dialog_data.append((display_sid, display_name, data_idx, called))
        dialog = StudentListDialog(self, dialog_data)
        if dialog.exec() == QDialog.DialogCode.Accepted and dialog.selected_index is not None:
            selected = dialog.selected_index
            if selected in self.student_data.index:
                self.current_student_index = selected
                self._pending_passive_student = None
                self.display_current_student()
                self._announce_current_student()

    def toggle_mode(self) -> None:
        self.mode = "timer" if self.mode == "roll_call" else "roll_call"
        if self.mode == "roll_call":
            self._placeholder_on_show = True
        self.update_mode_ui(force_timer_reset=False)
        self._schedule_save()

    def update_mode_ui(self, force_timer_reset: bool = False) -> None:
        is_roll = self.mode == "roll_call"
        timer_reset_required = force_timer_reset
        if is_roll and not self._ensure_student_data_ready():
            self.mode = "timer"
            is_roll = False
            timer_reset_required = True
        self.title_label.setText("点名" if is_roll else "计时")
        self.mode_button.setText("切换到计时" if is_roll else "切换到点名")
        self.group_label.setVisible(is_roll)
        if hasattr(self, "group_container"):
            self.group_container.setVisible(is_roll)
        if hasattr(self, "group_bar"):
            self.group_bar.setVisible(is_roll)
        self._update_roll_call_controls()
        self._update_class_button_label()
        if is_roll:
            if self._placeholder_on_show:
                self.current_student_index = None
            self.stack.setCurrentWidget(self.roll_call_frame)
            # 保持计时/秒表在后台运行，不强制重置/暂停
            if self.timer_modes[self.timer_mode_index] in {"countdown", "stopwatch"}:
                if self.timer_running and not self.count_timer.isActive():
                    self.count_timer.start()
            elif self.timer_modes[self.timer_mode_index] == "clock":
                if not self.clock_timer.isActive():
                    self.clock_timer.start()
            self.update_display_layout()
            self.display_current_student()
            self.schedule_font_update()
            self._placeholder_on_show = False
        else:
            self.stack.setCurrentWidget(self.timer_frame)
            changed = False
            if timer_reset_required:
                changed = self.reset_timer(persist=False)
            self.update_timer_mode_ui()
            if changed:
                self._schedule_save()
            self.schedule_font_update()
            self._hide_student_photo(force=True)
        if hasattr(self, "reset_button"):
            self.reset_button.setVisible(is_roll)
        self._apply_remote_presenter_runtime_state()
        self.updateGeometry()
        self._sync_reminder_menu_state()

    def _handle_timer_mode_transition(self, previous_mode: Optional[str], new_mode: str) -> None:
        if previous_mode == new_mode:
            return
        if new_mode in {"countdown", "stopwatch"}:
            self.timer_running = False
            self.count_timer.stop()
            self.timer_start_pause_button.setText("开始")
            if new_mode == "countdown":
                total = max(0, self.timer_countdown_minutes * 60 + self.timer_countdown_seconds)
                self.timer_seconds_left = total
                self._reset_reminder_progress()
            else:
                self.timer_stopwatch_seconds = 0
                self._reset_reminder_progress()
        else:
            self._reset_reminder_progress()

    def update_timer_mode_ui(self) -> None:
        mode = self.timer_modes[self.timer_mode_index]
        previous_mode = self._active_timer_mode
        if previous_mode is not None:
            self._handle_timer_mode_transition(previous_mode, mode)
        self._active_timer_mode = mode
        self.clock_timer.stop()
        if mode == "countdown":
            self.timer_mode_button.setText("倒计时")
            self.timer_start_pause_button.setEnabled(True)
            self.timer_reset_button.setEnabled(True)
            self.timer_set_button.setEnabled(True)
            self.timer_start_pause_button.setText("暂停" if self.timer_running else "开始")
            if self.timer_running and not self.count_timer.isActive():
                self.count_timer.start()
            self.update_timer_display()
        elif mode == "stopwatch":
            self.timer_mode_button.setText("秒表")
            self.timer_start_pause_button.setEnabled(True)
            self.timer_reset_button.setEnabled(True)
            self.timer_set_button.setEnabled(False)
            self.timer_start_pause_button.setText("暂停" if self.timer_running else "开始")
            if self.timer_running and not self.count_timer.isActive():
                self.count_timer.start()
            self.update_timer_display()
        else:
            self.timer_mode_button.setText("时钟")
            self.timer_start_pause_button.setEnabled(False)
            self.timer_reset_button.setEnabled(False)
            self.timer_set_button.setEnabled(False)
            self.timer_running = False
            self.count_timer.stop()
            self._update_clock()
            self.clock_timer.start()
            self.timer_start_pause_button.setText("开始")
        self.schedule_font_update()
        self._sync_reminder_menu_state()

    def toggle_timer_mode(self) -> None:
        if self.timer_running:
            return
        self.timer_mode_index = (self.timer_mode_index + 1) % len(self.timer_modes)
        self.update_timer_mode_ui()
        self._schedule_save()

    def start_pause_timer(self) -> None:
        if self.timer_modes[self.timer_mode_index] == "clock":
            return
        starting = not self.timer_running
        if starting and self.timer_modes[self.timer_mode_index] == "countdown":
            if self.timer_seconds_left <= 0:
                self.reset_timer(persist=False)
        self.timer_running = starting
        if self.timer_running:
            self.timer_start_pause_button.setText("暂停")
            if not self.count_timer.isActive():
                self.count_timer.start()
        else:
            self.timer_start_pause_button.setText("开始")
            self.count_timer.stop()
        self._sync_reminder_menu_state()
        self._schedule_save()

    def reset_timer(self, persist: bool = True) -> bool:
        changed = self.timer_running
        self.timer_running = False
        self.count_timer.stop()
        self.timer_start_pause_button.setText("开始")
        self._reset_reminder_progress()
        m = self.timer_modes[self.timer_mode_index]
        if m == "countdown":
            baseline = max(0, self.timer_countdown_minutes * 60 + self.timer_countdown_seconds)
            if self.timer_seconds_left != baseline:
                self.timer_seconds_left = baseline
                changed = True
        elif m == "stopwatch":
            if self.timer_stopwatch_seconds != 0:
                self.timer_stopwatch_seconds = 0
                changed = True
        self.update_timer_display()
        if persist and changed:
            self._schedule_save()
        return changed

    def set_countdown_time(self) -> None:
        d = CountdownSettingsDialog(self, self.timer_countdown_minutes, self.timer_countdown_seconds)
        if d.exec() and d.result:
            mi, se = d.result
            self.timer_countdown_minutes = mi
            self.timer_countdown_seconds = se
            changed = self.reset_timer()
            if not changed:
                self._schedule_save()

    def _on_count_timer(self) -> None:
        m = self.timer_modes[self.timer_mode_index]
        if m == "countdown":
            if self.timer_seconds_left > 0:
                self.timer_seconds_left -= 1
                self._handle_mid_reminder_tick()
            else:
                self.count_timer.stop()
                self.timer_running = False
                self.timer_start_pause_button.setText("开始")
                self.update_timer_display()
                self._reset_reminder_progress()
                self.play_timer_sound(kind="end")
                return
        elif m == "stopwatch":
            self.timer_stopwatch_seconds += 1
        self.update_timer_display()

    def update_timer_display(self) -> None:
        m = self.timer_modes[self.timer_mode_index]
        if m == "countdown":
            seconds = max(0, self.timer_seconds_left)
        elif m == "stopwatch":
            seconds = max(0, self.timer_stopwatch_seconds)
        else:
            seconds = 0
        if m in {"countdown", "stopwatch"}:
            mi, se = divmod(seconds, 60)
            self.time_display_label.setText(f"{int(mi):02d}:{int(se):02d}")
        else:
            self.time_display_label.setText(time.strftime("%H:%M:%S"))
        self.schedule_font_update()

    def _update_clock(self) -> None:
        self.time_display_label.setText(time.strftime("%H:%M:%S"))
        self.schedule_font_update()

    def _reminder_interval_seconds(self) -> int:
        try:
            return max(0, int(self.timer_reminder_interval_minutes) * 60)
        except Exception:
            return 0

    def _reset_reminder_progress(self) -> None:
        self._reminder_elapsed_seconds = 0

    def _handle_mid_reminder_tick(self) -> None:
        if not self.timer_reminder_enabled:
            return
        if self.timer_modes[self.timer_mode_index] != "countdown":
            return
        interval = self._reminder_interval_seconds()
        if interval <= 0:
            return
        self._reminder_elapsed_seconds += 1
        if self._reminder_elapsed_seconds >= interval:
            self._reminder_elapsed_seconds = 0
            self.play_timer_sound(kind="reminder")

    def _build_tone(self, freq: float, duration: float, *, fs: int = 44100, amplitude: float = 0.35) -> "np.ndarray":
        if not SOUNDDEVICE_AVAILABLE or np is None:
            raise RuntimeError("sound backend unavailable")
        t = np.linspace(0, duration, int(fs * duration), endpoint=False)
        tone = amplitude * np.sin(2 * np.pi * freq * t)
        # 简单淡入淡出，避免爆音
        fade_len = max(1, int(0.01 * fs))
        fade = np.linspace(0, 1, fade_len)
        tone[:fade_len] *= fade
        tone[-fade_len:] *= fade[::-1]
        return tone

    def _build_sound_wave(self, variant: str, kind: str) -> tuple[Optional["np.ndarray"], int]:
        if not SOUNDDEVICE_AVAILABLE or np is None:
            return None, 0
        fs = 44100
        v = (variant or "").strip().lower()
        if kind == "reminder":
            allowed = {"soft_beep", "ping", "chime", "pulse", "short_bell"}
            if v not in allowed:
                v = "soft_beep"
        else:
            allowed = {"gentle", "bell", "digital", "buzz", "urgent"}
            if v not in allowed:
                v = "gentle"

        cache = getattr(self, "_sound_wave_cache", None)
        cache_key = (str(kind or "").strip().lower(), v)
        if isinstance(cache, OrderedDict):
            cached = cache.get(cache_key)
            if cached is not None:
                try:
                    data, cached_fs = cached
                except Exception:
                    data, cached_fs = None, 0
                else:
                    if data is not None and cached_fs:
                        cache.move_to_end(cache_key)
                        return data, int(cached_fs)

        segments: List[np.ndarray] = []
        silence = lambda d: np.zeros(int(fs * d))

        def add_tone(freq: float, dur: float, amp: float = 0.35) -> None:
            try:
                segments.append(self._build_tone(freq, dur, fs=fs, amplitude=amp))
            except Exception:
                segments.append(np.zeros(int(fs * dur)))

        if kind == "reminder":
            if v == "short_bell":
                add_tone(980, 0.14, 0.42)
                segments.append(silence(0.05))
                add_tone(760, 0.12, 0.36)
            elif v == "ping":
                add_tone(1150, 0.08, 0.36)
                segments.append(silence(0.03))
                add_tone(820, 0.1, 0.3)
            elif v == "chime":
                add_tone(640, 0.12, 0.34)
                segments.append(silence(0.04))
                add_tone(960, 0.16, 0.3)
            elif v == "pulse":
                for _ in range(3):
                    add_tone(700, 0.07, 0.32)
                    segments.append(silence(0.06))
            else:  # soft_beep
                add_tone(560, 0.22, 0.4)
                segments.append(silence(0.04))
                add_tone(620, 0.12, 0.34)
        else:
            if v == "bell":
                add_tone(880, 0.12, 0.4)
                segments.append(silence(0.05))
                add_tone(660, 0.18, 0.36)
            elif v == "digital":
                for freq in (900, 1200, 1500):
                    add_tone(freq, 0.05, 0.32)
                    segments.append(silence(0.04))
            elif v == "buzz":
                add_tone(220, 0.22, 0.42)
                add_tone(180, 0.14, 0.36)
            elif v == "urgent":
                for _ in range(3):
                    add_tone(1150, 0.09, 0.4)
                    segments.append(silence(0.05))
            else:  # gentle
                add_tone(660, 0.18, 0.32)
                segments.append(silence(0.04))
                add_tone(880, 0.16, 0.28)

        if not segments:
            return None, 0
        data = np.concatenate(segments)
        peak = np.max(np.abs(data)) or 1.0
        if peak > 0.95:
            data = data * (0.95 / peak)
        data = data.astype(np.float32)

        if isinstance(cache, OrderedDict):
            cache[cache_key] = (data, int(fs))
            cache.move_to_end(cache_key)
            limit = int(getattr(self, "_SOUND_WAVE_CACHE_LIMIT", 16))
            while len(cache) > limit:
                cache.popitem(last=False)
        return data, fs

    def _play_sound_async(self, data: "np.ndarray", fs: int) -> None:
        if not SOUNDDEVICE_AVAILABLE or sd is None or data is None or fs <= 0:
            return
        if is_app_closing():
            return

        def _play() -> None:
            try:
                sd.stop()
                sd.play(data, fs)
                sd.wait()
            except Exception:
                pass

        threading.Thread(target=_play, daemon=True).start()

    def play_timer_sound(
        self,
        *,
        kind: str = "end",
        preview: bool = False,
        variant_override: Optional[str] = None,
    ) -> None:
        if not SOUNDDEVICE_AVAILABLE or np is None:
            return
        if not preview:
            if kind == "end" and not self.timer_sound_enabled:
                return
            if kind == "reminder" and not self.timer_reminder_enabled:
                return
        variant = variant_override or (self.timer_sound_variant if kind == "end" else self.timer_reminder_sound_variant)
        data, fs = self._build_sound_wave(variant, kind)
        if data is None or fs <= 0:
            return
        self._play_sound_async(data, fs)

    def on_group_change(self, group_name: Optional[str] = None, initial: bool = False) -> None:
        if not self.groups:
            return
        if group_name is None:
            group_name = self.current_group_name
        if group_name not in self.groups:
            group_name = "全部" if "全部" in self.groups else self.groups[0]
        previous_group = self.current_group_name
        self.current_group_name = group_name
        self._update_group_button_state(group_name)
        if self.student_data.empty:
            self.current_student_index = None
            self.display_current_student()
            if not initial and previous_group != group_name:
                self._schedule_save()
            return
        self._pending_passive_student = None
        self._ensure_group_pool(group_name)
        self.roll_logic.validate_and_repair_state(context="on_group_change")
        self.current_student_index = None
        self.display_current_student()
        if not initial and previous_group != group_name:
            self._schedule_save()

    def roll_student(self, speak: bool = True, *, ensure_mode: bool = False) -> None:
        self.roll_logic.roll_student(speak=speak, ensure_mode=ensure_mode)

    def keyPressEvent(self, event: QKeyEvent) -> None:
        if event.key() == Qt.Key.Key_Escape:
            overlay = getattr(self, "_photo_overlay", None)
            if overlay is not None and isinstance(overlay, StudentPhotoOverlay) and overlay.isVisible():
                try:
                    overlay._handle_close_request(manual=True)
                except Exception:
                    overlay.hide()
                event.accept()
                return
        super().keyPressEvent(event)

    def _all_groups_completed(self) -> bool:
        """判断是否所有分组的学生都已点名完毕。"""

        return self.roll_logic._all_groups_completed()

    def _reset_roll_call_state(self) -> None:
        """清空全部点名历史并重新洗牌。"""

        self.roll_logic.reset_roll_call_state()

    def _shuffle(self, values: List[int]) -> None:
        self.roll_logic._shuffle(values)

    def _normalize_indices(self, values: Iterable[Any], *, allowed: Optional[Set[int]] = None) -> List[int]:
        """Convert an iterable of values to a deduplicated integer list."""

        return self.roll_logic._normalize_indices(values, allowed=allowed)

    def _collect_base_indices(self, values: Optional[Iterable[Any]]) -> List[int]:
        """Normalize the raw index list preserved in each group."""

        return self.roll_logic._collect_base_indices(values)

    def reset_roll_call_pools(self) -> None:
        """根据当前分组执行重置：子分组独立重置，“全部”重置所有。"""

        if self.mode != "roll_call":
            return
        group_name = self.current_group_name
        if self.student_data is None or getattr(self.student_data, "empty", True):
            show_quiet_information(self, "暂无学生数据可供重置。")
            return
        if group_name == "全部":
            prompt = "确定要重置所有分组的点名状态并重新开始吗？"
        else:
            prompt = f"确定要重置“{group_name}”分组的点名状态并重新开始吗？"
        if not ask_quiet_confirmation(self, prompt, "确认重置"):
            return
        if group_name == "全部":
            self._reset_roll_call_state()
        else:
            self._reset_single_group(group_name)
        self.current_student_index = None
        self._pending_passive_student = None
        self.display_current_student()
        self.save_settings()

    def _reset_single_group(self, group_name: str) -> None:
        """仅重置指定分组，同时保持其它分组及全局状态不变。"""

        self.roll_logic.reset_single_group(group_name)

    def _rebuild_group_indices(self) -> None:
        """重新构建各分组的学生索引池。"""

        self.roll_logic._rebuild_group_indices()

    def _remove_from_global_history(self, student_index: int, ignore_group: Optional[str] = None) -> None:
        """若学生未在其它分组被点名，则从全局记录中移除。"""

        self.roll_logic._remove_from_global_history(student_index, ignore_group=ignore_group)

    def _restore_group_state(self, section: Mapping[str, str]) -> None:
        """从配置中恢复各分组剩余学生池，保持未抽学生不重复。"""

        if not PANDAS_READY:
            return

        raw_states = section.get("class_states", "")
        restored_states: Dict[str, ClassRollState] = {}
        if raw_states:
            try:
                payload = json.loads(raw_states)
            except Exception:
                payload = {}
            if isinstance(payload, dict):
                for name, state_data in payload.items():
                    key = str(name).strip()
                    if not key:
                        continue
                    state = ClassRollState.from_mapping(state_data)
                    if state is not None:
                        restored_states[key] = state

        self._class_roll_states = restored_states
        self._prune_orphan_class_states()

        active_class = self._resolve_active_class_name()
        snapshot = self._class_roll_states.get(active_class)
        if snapshot is None:
            legacy = self._parse_legacy_roll_state(section)
            if legacy is not None and active_class:
                self._class_roll_states[active_class] = legacy
                snapshot = legacy

        if not self._can_apply_roll_state():
            return

        if snapshot is None:
            self._ensure_group_pool(self.current_group_name)
            return

        self._apply_roll_state(snapshot, restore_current_student=False)
        sanitized = self._capture_roll_state()
        if sanitized is not None and active_class:
            self._class_roll_states[active_class] = sanitized

    def _ensure_group_pool(self, group_name: str, force_reset: bool = False) -> None:
        """确保指定分组仍有待抽取的学生，必要时重新洗牌。"""

        self.roll_logic._ensure_group_pool(group_name, force_reset=force_reset)

    def _mark_student_drawn(self, student_index: int) -> None:
        """抽中学生后，从所有关联分组的候选列表中移除该学生。"""

        self.roll_logic._mark_student_drawn(student_index)

    def _refresh_all_group_pool(self) -> None:
        """同步“全部”分组的剩余名单，使其与各子分组保持一致。"""

        self.roll_logic._refresh_all_group_pool()

    def _safe_get_student_row(self, student_index: Any) -> Optional[Any]:
        """安全获取学生行数据，避免索引越界或类型异常导致崩溃。"""

        if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
            return None
        if self.student_data.empty:
            return None
        try:
            row = self.student_data.loc[student_index]
        except KeyError:
            return None
        except Exception:
            return None
        if isinstance(row, pd.DataFrame):
            if row.empty:
                return None
            return row.iloc[0]
        return row

    def display_current_student(self) -> None:
        photo_student_id: Optional[str] = None
        photo_student_name: Optional[str] = None

        def _reset_placeholder() -> None:
            self.current_student_index = None
            self.id_label.setText("学号" if self.show_id else "")
            self.name_label.setText("学生" if self.show_name else "")

        if self.student_data is None or getattr(self.student_data, "empty", True):
            _reset_placeholder()
        elif self.current_student_index is None:
            _reset_placeholder()
        else:
            stu = self._safe_get_student_row(self.current_student_index)
            if stu is None:
                logger.warning("当前学生索引无效，已自动清空：%s", self.current_student_index)
                _reset_placeholder()
                self.update_display_layout()
                self._update_roll_call_controls()
                self.schedule_font_update()
                self._maybe_show_student_photo(None, None)
                return

            raw_sid = stu.get("学号", "")
            raw_name = stu.get("姓名", "")
            sid = _compact_text(raw_sid)
            name = _normalize_text(raw_name)

            self.id_label.setText(sid if self.show_id else "")
            self.name_label.setText(name if self.show_name else "")

            photo_student_id = sid or None
            photo_student_name = name or None
        self.update_display_layout()
        self._update_roll_call_controls()
        self.schedule_font_update()
        self._maybe_show_student_photo(photo_student_id, photo_student_name)

    def update_display_layout(self) -> None:
        self.id_label.setVisible(self.show_id)
        self.name_label.setVisible(self.show_name)
        layout: QGridLayout = self.roll_call_frame.layout()
        layout.setColumnStretch(0, 1)
        layout.setColumnStretch(1, 1)
        if not self.show_id:
            layout.setColumnStretch(0, 0)
        if not self.show_name:
            layout.setColumnStretch(1, 0)
        self.schedule_font_update()

    def _ensure_photo_root_directory(self) -> None:
        try:
            os.makedirs(self.photo_root_path, exist_ok=True)
        except Exception:
            logger.debug("Failed to create photo root directory at %s", self.photo_root_path, exc_info=True)

    def _get_photo_index_for_directory(self, base_dir: str) -> Dict[str, str]:
        if not base_dir:
            return {}
        cache = getattr(self, "_photo_dir_index_cache", None)
        if not isinstance(cache, OrderedDict):
            return {}
        normalized_dir = os.path.normpath(base_dir)
        now = time.monotonic()
        try:
            mtime = float(os.path.getmtime(normalized_dir))
        except OSError:
            mtime = -1.0

        cached = cache.get(normalized_dir)
        if cached is not None:
            cached_mtime, cached_at, index = cached
            if (
                cached_mtime == mtime
                and isinstance(index, dict)
                and now - cached_at < PHOTO_INDEX_CACHE_TTL_SECONDS
            ):
                cache.move_to_end(normalized_dir)
                return index
            cache.pop(normalized_dir, None)

        index: Dict[str, str] = {}
        if not os.path.isdir(normalized_dir):
            return index
        try:
            scanned = 0
            for entry in os.scandir(normalized_dir):
                scanned += 1
                if scanned > PHOTO_INDEX_SCAN_LIMIT:
                    logger.warning("照片目录文件过多，已限制索引数量：%s", normalized_dir)
                    break
                if not entry.is_file():
                    continue
                name = entry.name
                stem, ext = os.path.splitext(name)
                if not stem or not ext:
                    continue
                if ext.lower() not in self._photo_extensions:
                    continue
                key = stem.lower()
                if key not in index:
                    index[key] = entry.path
        except OSError:
            logger.debug("Failed to scan photo directory %s", normalized_dir, exc_info=True)
            index = {}

        cache[normalized_dir] = (mtime, now, index)
        cache.move_to_end(normalized_dir)
        limit = int(getattr(self, "_PHOTO_INDEX_CACHE_LIMIT", 24))
        while len(cache) > limit:
            cache.popitem(last=False)
        return index

    @staticmethod
    def _scaled_photo_size(original: QSize, max_size: QSize) -> QSize:
        if not original.isValid() or not max_size.isValid():
            return QSize()
        width = original.width()
        height = original.height()
        if width <= 0 or height <= 0:
            return QSize()
        scale = min(max_size.width() / width, max_size.height() / height, 1.0)
        if scale >= 1.0:
            return QSize()
        return QSize(max(1, int(width * scale)), max(1, int(height * scale)))

    def _load_student_photo_pixmap(self, path: str, max_size: QSize) -> QPixmap:
        cache = getattr(self, "_photo_pixmap_cache", None)
        cache_key = path
        if max_size.isValid():
            cache_key = f"{path}|{max_size.width()}x{max_size.height()}"
        if isinstance(cache, OrderedDict):
            cached = cache.get(cache_key)
            if cached is not None and isinstance(cached, QPixmap) and not cached.isNull():
                cache.move_to_end(cache_key)
                return cached
        pixmap = QPixmap()
        try:
            reader = QImageReader(path)
            reader.setAutoTransform(True)
            scaled = self._scaled_photo_size(reader.size(), max_size)
            if scaled.isValid():
                reader.setScaledSize(scaled)
            image = reader.read()
        except Exception:
            image = QImage()
        if image.isNull():
            if isinstance(cache, OrderedDict):
                cache.pop(cache_key, None)
            return pixmap
        if max_size.isValid() and (
            image.width() > max_size.width() or image.height() > max_size.height()
        ):
            image = image.scaled(
                max_size,
                Qt.AspectRatioMode.KeepAspectRatio,
                Qt.TransformationMode.SmoothTransformation,
            )
        pixmap = QPixmap.fromImage(image)
        if pixmap.isNull():
            if isinstance(cache, OrderedDict):
                cache.pop(cache_key, None)
            return pixmap
        if isinstance(cache, OrderedDict):
            cache[cache_key] = pixmap
            cache.move_to_end(cache_key)
            limit = int(getattr(self, "_PHOTO_PIXMAP_CACHE_LIMIT", 20))
            while len(cache) > limit:
                cache.popitem(last=False)
        return pixmap

    @staticmethod
    def _load_student_photo_image(path: str, max_size: QSize) -> Optional[QImage]:
        """在后台线程中读取并缩放照片，避免阻塞 UI。

        注意：后台线程不要创建 QPixmap，只返回 QImage，回到 UI 线程再转换。
        """

        if not path:
            return None
        try:
            reader = QImageReader(path)
            reader.setAutoTransform(True)
            scaled = RollCallTimerWindow._scaled_photo_size(reader.size(), max_size)
            if scaled.isValid():
                reader.setScaledSize(scaled)
            image = reader.read()
        except Exception:
            return None
        if image.isNull():
            return None
        try:
            if (
                max_size.isValid()
                and (image.width() > max_size.width() or image.height() > max_size.height())
            ):
                image = image.scaled(
                    max_size,
                    Qt.AspectRatioMode.KeepAspectRatio,
                    Qt.TransformationMode.SmoothTransformation,
                )
        except Exception:
            pass
        return image

    def _row_id_to_index_map(self) -> Dict[str, int]:
        if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
            return {}
        if STUDENT_ROW_ID_COLUMN not in self.student_data.columns:
            return {}
        mapping: Dict[str, int] = {}
        try:
            series = self.student_data[STUDENT_ROW_ID_COLUMN]
        except Exception:
            return {}
        for idx, value in series.items():
            text = _compact_text(value)
            if not text:
                continue
            try:
                mapping[text] = int(idx)
            except (TypeError, ValueError):
                continue
        return mapping

    def _row_key_to_index_map(self) -> Dict[str, int]:
        return _build_row_key_map(self.student_data) if self.student_data is not None else {}

    def _index_to_row_id(self, student_index: Any) -> Optional[str]:
        if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
            return None
        if STUDENT_ROW_ID_COLUMN not in self.student_data.columns:
            return None
        try:
            value = self.student_data.at[student_index, STUDENT_ROW_ID_COLUMN]
        except Exception:
            return None
        text = _compact_text(value)
        return text or None

    def _index_to_row_key(self, student_index: Any) -> Optional[str]:
        if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
            return None
        try:
            row = self.student_data.loc[student_index]
        except Exception:
            return None
        key = _student_identity_key(
            row.get("学号", ""),
            row.get("姓名", ""),
            row.get("班级", ""),
            row.get("分组", ""),
        )
        return key or None

    def _index_to_save_key(self, student_index: Any) -> Union[int, str]:
        key = self._index_to_row_key(student_index)
        if key:
            return key
        row_id = self._index_to_row_id(student_index)
        if row_id:
            return row_id
        try:
            return int(student_index)
        except (TypeError, ValueError):
            return str(student_index)

    def _resolve_photo_screen_geometry(self) -> Optional[QRect]:
        screen = (self.windowHandle().screen() if self.windowHandle() else None) or QApplication.primaryScreen()
        if screen is None:
            return None
        return screen.geometry()

    def _display_photo_pixmap(self, pixmap: QPixmap, screen_rect: QRect, student_name: Optional[str]) -> None:
        overlay = self._ensure_photo_overlay()
        overlay.display_photo(
            pixmap,
            screen_rect,
            int(max(0, self.photo_duration_seconds) * 1000),
            student_name=student_name,
        )

    def _queue_student_photo_load(
        self,
        *,
        token: int,
        student_id: str,
        path: str,
        screen_rect: QRect,
        student_name: Optional[str],
    ) -> None:
        max_size = screen_rect.size()
        cache_key = path
        if max_size.isValid():
            cache_key = f"{path}|{max_size.width()}x{max_size.height()}"

        def _task() -> Tuple[int, str, str, QSize, str, Optional[QImage]]:
            image = self._load_student_photo_image(path, max_size)
            return token, student_id, path, max_size, cache_key, image

        worker = _IOWorker(_task)

        def _finish(payload: object) -> None:
            if is_app_closing():
                return
            if not isinstance(payload, tuple) or len(payload) != 6:
                return
            done_token, done_id, done_path, _done_size, done_cache_key, image = payload
            if int(done_token) != int(self._photo_load_token):
                return
            if str(done_id) != str(student_id):
                return
            if image is None or not isinstance(image, QImage) or image.isNull():
                self._hide_student_photo()
                self._last_photo_student_id = student_id
                return
            pix = QPixmap.fromImage(image)
            if pix.isNull():
                self._hide_student_photo()
                self._last_photo_student_id = student_id
                return
            cache = getattr(self, "_photo_pixmap_cache", None)
            if isinstance(cache, OrderedDict):
                cache[done_cache_key] = pix
                cache.move_to_end(done_cache_key)
                limit = int(getattr(self, "_PHOTO_PIXMAP_CACHE_LIMIT", 20))
                while len(cache) > limit:
                    cache.popitem(last=False)
            if not self.show_photo or self.mode != "roll_call":
                return
            if self._photo_manual_hidden and self._last_photo_student_id == student_id:
                return
            self._display_photo_pixmap(pix, screen_rect, student_name)

        def _error(_message: str, _exc: object) -> None:
            pass

        worker.signals.finished.connect(_finish)
        worker.signals.error.connect(_error)
        self._io_pool.start(worker)

    def _maybe_show_student_photo(self, student_id: Optional[str], student_name: Optional[str] = None) -> None:
        if not self.show_photo or self.mode != "roll_call":
            self._hide_student_photo(force=True)
            return
        if not student_id:
            self._hide_student_photo()
            self._last_photo_student_id = None
            return
        normalized_id = student_id.strip()
        if not normalized_id:
            self._hide_student_photo()
            self._last_photo_student_id = None
            return
        if self._last_photo_student_id != normalized_id:
            self._photo_manual_hidden = False
            # 先隐藏上一张照片，避免在新照片加载/缩放期间短暂显示旧照片。
            self._hide_student_photo()
        if self._photo_manual_hidden and self._last_photo_student_id == normalized_id:
            return
        path = self._resolve_student_photo_path(normalized_id)
        if not path:
            self._hide_student_photo()
            self._last_photo_student_id = normalized_id
            return
        screen_rect = self._resolve_photo_screen_geometry()
        if screen_rect is None:
            self._hide_student_photo()
            self._last_photo_student_id = normalized_id
            return

        # 若缓存命中则直接显示，否则后台加载，避免 UI 卡顿与“旧图闪回”。
        pixmap = self._load_student_photo_pixmap(path, screen_rect.size())
        if not pixmap.isNull():
            self._display_photo_pixmap(pixmap, screen_rect, student_name)
        else:
            self._photo_load_token += 1
            token = int(self._photo_load_token)
            self._queue_student_photo_load(
                token=token,
                student_id=normalized_id,
                path=path,
                screen_rect=screen_rect,
                student_name=student_name,
            )
        self._last_photo_student_id = normalized_id
        self._photo_manual_hidden = False

    def _hide_student_photo(self, force: bool = False) -> None:
        overlay = getattr(self, "_photo_overlay", None)
        if overlay is not None:
            overlay.cancel_auto_close()
            if overlay.isVisible():
                overlay.hide()
            overlay._current_student_name = ""
            overlay._name_label.hide()
        if force:
            self._photo_manual_hidden = False
            self._last_photo_student_id = None

    def _ensure_photo_overlay(self) -> StudentPhotoOverlay:
        if self._photo_overlay is None:
            self._photo_overlay = StudentPhotoOverlay(owner=self)
            self._photo_overlay.closed_by_user.connect(self._on_photo_overlay_closed)
            self._photo_overlay.auto_closed.connect(self._on_photo_overlay_auto_closed)
        else:
            self._photo_overlay.update_owner(self)
        return self._photo_overlay

    def _on_photo_overlay_closed(self) -> None:
        self._photo_manual_hidden = True
        overlay = getattr(self, "_photo_overlay", None)
        if overlay is not None:
            overlay.cancel_auto_close()

    def _on_photo_overlay_auto_closed(self) -> None:
        self._photo_manual_hidden = False

    def _resolve_student_photo_path(self, student_id: str) -> Optional[str]:
        class_name = self._sanitize_photo_segment(self._resolve_photo_class_name())
        if not class_name:
            class_name = "default"
        search_roots = list(getattr(self, "_photo_search_roots", []))
        if not search_roots:
            search_roots = [self.photo_root_path]
        primary_root = os.path.normpath(self.photo_root_path)
        visited: Set[str] = set()

        for root in search_roots:
            if not root:
                continue
            normalized_root = os.path.normpath(root)
            if normalized_root in visited:
                continue
            visited.add(normalized_root)
            base_dir = os.path.join(root, class_name)
            if normalized_root == primary_root:
                normalized_base = os.path.normpath(base_dir)
                if normalized_base not in self._photo_dirs_ensured:
                    try:
                        os.makedirs(base_dir, exist_ok=True)
                        self._photo_dirs_ensured.add(normalized_base)
                    except OSError:
                        logger.debug("Unable to ensure photo directory %s", base_dir, exc_info=True)
            if not os.path.isdir(base_dir):
                continue
            for ext in self._photo_extensions:
                candidate = os.path.join(base_dir, f"{student_id}{ext}")
                if os.path.isfile(candidate):
                    return candidate
                upper = os.path.join(base_dir, f"{student_id}{ext.upper()}")
                if os.path.isfile(upper):
                    return upper

            index = self._get_photo_index_for_directory(base_dir)
            matched = index.get(student_id.lower())
            if matched and os.path.isfile(matched):
                return matched
        return None

    @staticmethod
    def _sanitize_photo_segment(value: str) -> str:
        text = str(value or "").strip()
        if not text:
            return ""
        sanitized = _PHOTO_SEGMENT_SANITIZER.sub("_", text)
        sanitized = sanitized.strip("_")
        return sanitized or "default"

    def _rebuild_group_buttons_ui(self) -> None:
        if not hasattr(self, "group_bar_layout"):
            return
        for button in list(self.group_button_group.buttons()):
            self.group_button_group.removeButton(button)
        while self.group_bar_layout.count():
            item = self.group_bar_layout.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.deleteLater()
        self.group_buttons = {}
        if not self.groups:
            return
        button_font = QFont("Microsoft YaHei UI", 9, QFont.Weight.Medium)
        # 使用统一的工具栏高度
        button_height = getattr(self, "_toolbar_height", 32)
        for group in self.groups:
            button = QPushButton(group)
            button.setCheckable(True)
            button.setFont(button_font)
            apply_button_style(button, ButtonStyles.TOOLBAR)
            # 计算合适的宽度
            fm = QFontMetrics(button_font)
            text_width = fm.horizontalAdvance(group)
            calc_width = text_width + 34
            button.setMinimumWidth(calc_width)
            button.setMaximumWidth(calc_width)
            button.setMinimumHeight(button_height)
            button.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
            button.clicked.connect(lambda _checked=False, name=group: self.on_group_change(name))
            self.group_bar_layout.addWidget(button)
            self.group_button_group.addButton(button)
            self.group_buttons[group] = button
        self.group_bar_layout.addStretch(1)
        self._update_group_button_state(self.current_group_name)

    def _update_group_button_state(self, active_group: str) -> None:
        if not hasattr(self, "group_buttons"):
            return
        for name, button in self.group_buttons.items():
            block = button.blockSignals(True)
            button.setChecked(name == active_group)
            button.blockSignals(block)

    def _update_roll_call_controls(self) -> None:
        if not hasattr(self, "list_button"):
            return
        is_roll = self.mode == "roll_call"
        self.list_button.setVisible(is_roll)
        has_data = self.student_data is not None and not getattr(self.student_data, "empty", True)
        self.list_button.setEnabled(is_roll and has_data)
        if hasattr(self, "class_button"):
            has_workbook = self.student_workbook is not None and not self.student_workbook.is_empty()
            can_select = is_roll and (has_workbook or self._student_data_pending_load)
            self.class_button.setVisible(is_roll)
            self.class_button.setEnabled(can_select)
        if hasattr(self, "reset_button"):
            self.reset_button.setEnabled(is_roll and has_data)

    def schedule_font_update(self) -> None:
        QTimer.singleShot(0, self.update_dynamic_fonts)

    def update_dynamic_fonts(self) -> None:
        name_font_size = self.last_name_font_size
        for lab in (self.id_label, self.name_label):
            if not lab.isVisible():
                continue
            w = max(40, lab.width())
            h = max(40, lab.height())
            text = lab.text()
            size = self._calc_font_size(w, h, text)
            if lab is self.name_label:
                weight = QFont.Weight.Normal if self.name_font_family in {"楷体", "KaiTi"} else QFont.Weight.Bold
                lab.setFont(QFont(self.name_font_family, size, weight))
                self.last_name_font_size = size
                name_font_size = size
            else:
                lab.setFont(QFont("Microsoft YaHei UI", size, QFont.Weight.Bold))
                self.last_id_font_size = size
        if self.timer_frame.isVisible():
            text = self.time_display_label.text()
            w = max(60, self.time_display_label.width())
            h = max(60, self.time_display_label.height())
            size = self._calc_font_size(w, h, text, monospace=True)
            self.time_display_label.setFont(QFont("Consolas", size, QFont.Weight.Bold))
            self.last_timer_font_size = size

    def _calc_font_size(self, w: int, h: int, text: str, monospace: bool = False) -> int:
        if not text or w < 20 or h < 20:
            return self.MIN_FONT_SIZE
        w_eff = max(1, w - 16)
        h_eff = max(1, h - 16)
        is_cjk = any("\u4e00" <= c <= "\u9fff" for c in text)
        length = max(1, len(text))
        width_char_factor = 1.0 if is_cjk else (0.58 if monospace else 0.6)
        size_by_width = w_eff / (length * width_char_factor)
        size_by_height = h_eff * 0.70
        final_size = int(min(size_by_width, size_by_height))
        return max(self.MIN_FONT_SIZE, min(self.MAX_FONT_SIZE, final_size))

    def showEvent(self, e) -> None:
        super().showEvent(e)
        if self.mode == "roll_call" and self._placeholder_on_show:
            self.current_student_index = None
            self.display_current_student()
            self._placeholder_on_show = False
        elif self.mode == "timer":
            active_mode = self.timer_modes[self.timer_mode_index]
            if active_mode in {"countdown", "stopwatch"}:
                self.update_timer_mode_ui()
                self.update_timer_display()
        self.visibility_changed.emit(True)
        self.schedule_font_update()
        ensure_widget_within_screen(self)
        if self._speech_init_pending:
            self._schedule_speech_init()
        elif not self._speech_issue_reported:
            self._diagnose_speech_engine()

    def resizeEvent(self, e: QResizeEvent) -> None:
        super().resizeEvent(e)
        self.schedule_font_update()

    def hideEvent(self, e) -> None:
        self._hide_student_photo(force=True)
        super().hideEvent(e)
        self._placeholder_on_show = True
        if not is_app_closing():
            self.save_settings()
        self.visibility_changed.emit(False)

    def closeEvent(self, e) -> None:
        # 改为隐藏窗口而非销毁，保持计时/点名状态继续运行
        e.ignore()
        self.hide()

    def shutdown_resources(self) -> None:
        """应用退出时释放资源，避免后台回调/播报残留。"""

        try:
            self._hide_student_photo(force=True)
        except Exception:
            pass
        if SOUNDDEVICE_AVAILABLE and sd is not None:
            with contextlib.suppress(Exception):
                sd.stop()
        controller = getattr(self, "remote_presenter_controller", None)
        if controller is not None:
            with contextlib.suppress(Exception):
                controller.stop()
        for timer_attr in ("_save_timer", "_auto_save_timer", "count_timer", "clock_timer"):
            timer = getattr(self, timer_attr, None)
            if isinstance(timer, QTimer):
                with contextlib.suppress(Exception):
                    timer.stop()
        manager = getattr(self, "tts_manager", None)
        if manager is not None:
            with contextlib.suppress(Exception):
                manager.shutdown(permanent=True)

    def _schedule_save(self) -> None:
        """延迟写入设置，避免频繁保存导致的磁盘抖动。"""

        self._settings_dirty = True
        if self._save_timer.isActive():
            self._save_timer.stop()
        self._save_timer.start()

    def _flush_dirty_settings(self) -> None:
        if self._settings_dirty:
            self.save_settings()

    def _schedule_roll_state_save(self) -> None:
        """在点名频繁操作时平衡即时性与性能。"""

        now = time.monotonic()
        if now - self._last_immediate_save_at >= ROLL_CALL_IMMEDIATE_SAVE_COOLDOWN_SECONDS:
            self._last_immediate_save_at = now
            self.save_settings()
        else:
            self._schedule_save()

    def _threaded_save_settings(self, payload: Dict[str, Dict[str, str]]) -> bool:
        locker = QMutexLocker(self._settings_write_lock)
        try:
            self.settings_manager.save_settings(payload)
        finally:
            del locker
        return True

    def _queue_settings_save(self, settings: Dict[str, Dict[str, str]]) -> None:
        worker = _IOWorker(self._threaded_save_settings, settings)

        def _log_error(message: str, _exc: object) -> None:
            if is_app_closing():
                return
            logger.error("Failed to persist settings: %s", message)

        worker.signals.error.connect(_log_error)
        self._io_pool.start(worker)

    def save_settings(self) -> None:
        if self._save_timer.isActive():
            self._save_timer.stop()
        if self.mode == "roll_call" and not getattr(self, "_student_data_pending_load", False):
            self.roll_logic.validate_and_repair_state(context="save_settings")
        self.roll_call_config.geometry = geometry_to_text(self)
        self.roll_call_config.show_id = bool(self.show_id)
        self.roll_call_config.show_name = bool(self.show_name)
        self.roll_call_config.show_photo = bool(self.show_photo)
        self.roll_call_config.photo_duration_seconds = int(self.photo_duration_seconds)
        self.roll_call_config.photo_shared_class = str(self.photo_shared_class or "")
        self.roll_call_config.speech_enabled = bool(self.speech_enabled)
        self.roll_call_config.speech_voice_id = self.selected_voice_id
        self.roll_call_config.speech_output_id = self.selected_output_id
        self.roll_call_config.speech_engine = self.selected_engine
        self.roll_call_config.current_class = self.current_class_name
        self.roll_call_config.current_group = self.current_group_name
        self.roll_call_config.timer_countdown_minutes = int(self.timer_countdown_minutes)
        self.roll_call_config.timer_countdown_seconds = int(self.timer_countdown_seconds)
        self.roll_call_config.timer_sound_enabled = bool(self.timer_sound_enabled)
        self.roll_call_config.timer_sound_variant = self.timer_sound_variant
        self.roll_call_config.timer_reminder_enabled = bool(self.timer_reminder_enabled)
        self.roll_call_config.timer_reminder_interval_minutes = int(self.timer_reminder_interval_minutes)
        self.roll_call_config.timer_reminder_sound_variant = self.timer_reminder_sound_variant
        self.roll_call_config.mode = self.mode
        self.roll_call_config.timer_mode = self.timer_modes[self.timer_mode_index]
        self.roll_call_config.timer_seconds_left = int(self.timer_seconds_left)
        self.roll_call_config.timer_stopwatch_seconds = int(self.timer_stopwatch_seconds)
        self.roll_call_config.timer_running = bool(self.timer_running)
        self.roll_call_config.id_font_size = int(self.last_id_font_size)
        self.roll_call_config.name_font_size = int(self.last_name_font_size)
        self.roll_call_config.timer_font_size = int(self.last_timer_font_size)
        self.roll_call_config.remote_roll_enabled = bool(self.remote_presenter_enabled)
        self.roll_call_config.remote_roll_key = self.remote_presenter_key
        self._prune_orphan_class_states()
        if not self._student_data_pending_load:
            self._store_active_class_state()
        self.roll_call_config.class_states = self._encode_class_states()
        if self._student_data_pending_load:
            # 在尚未加载真实名单数据时，保留磁盘上已有的未点名状态，避免误把占位空列表写回
            # 此时直接返回，保持上一轮保存的名单信息不被覆盖。
            payload = self.roll_call_config.to_mapping()
            settings = self.settings_manager.load_settings()
            settings["RollCallTimer"] = payload
            self.settings_manager.save_settings(settings)
            self._settings_dirty = False
            return

        # 名单已经加载完成，正常序列化各分组的剩余名单及历史记录
        remaining_payload: Dict[str, List[Union[int, str]]] = {}
        for group, indices in self._group_remaining_indices.items():
            cleaned: List[Union[int, str]] = []
            for idx in indices:
                try:
                    idx_int = int(idx)
                except (TypeError, ValueError):
                    continue
                cleaned.append(self._index_to_save_key(idx_int))
            remaining_payload[group] = cleaned
        last_payload: Dict[str, Optional[Union[int, str]]] = {}
        all_groups = set(self._group_all_indices.keys()) | set(self._group_last_student.keys())
        for group in all_groups:
            value = self._group_last_student.get(group)
            if value is None:
                last_payload[group] = None
            else:
                try:
                    idx_int = int(value)
                except (TypeError, ValueError):
                    last_payload[group] = None
                else:
                    last_payload[group] = self._index_to_save_key(idx_int)
        try:
            self.roll_call_config.group_remaining = json.dumps(remaining_payload, ensure_ascii=False)
        except TypeError:
            self.roll_call_config.group_remaining = json.dumps({}, ensure_ascii=False)
        try:
            self.roll_call_config.group_last = json.dumps(last_payload, ensure_ascii=False)
        except TypeError:
            self.roll_call_config.group_last = json.dumps({}, ensure_ascii=False)
        try:
            global_drawn_payload: List[Union[int, str]] = []
            for idx in sorted(self._global_drawn_students):
                try:
                    idx_int = int(idx)
                except (TypeError, ValueError):
                    continue
                global_drawn_payload.append(self._index_to_save_key(idx_int))
            self.roll_call_config.global_drawn = json.dumps(global_drawn_payload, ensure_ascii=False)
        except TypeError:
            self.roll_call_config.global_drawn = json.dumps([], ensure_ascii=False)
        settings = self.settings_manager.load_settings()
        settings["RollCallTimer"] = self.roll_call_config.to_mapping()
        self._queue_settings_save(settings)
        self._schedule_workbook_persist()
        self._settings_dirty = False

    def _persist_roll_state_immediately(self) -> None:
        """立即抓取并写入点名状态，避免切班或退出时状态丢失。"""

        if not PANDAS_READY:
            return
        if getattr(self, "_student_data_pending_load", False):
            # 等待真实数据加载时不写入，避免用占位数据覆盖历史状态
            return
        snapshot = self._capture_roll_state()
        if snapshot is None:
            return
        active_class = self._resolve_active_class_name()
        if active_class:
            self._class_roll_states[active_class] = snapshot
        self.roll_call_config.class_states = self._encode_class_states()
        settings = self.settings_manager.load_settings()
        settings["RollCallTimer"] = self.roll_call_config.to_mapping()
        self.settings_manager.save_settings(settings)
        self._flush_workbook_persist()

    def _persist_roll_state_to_workbook(self) -> None:
        """将当前点名状态嵌入学生数据文件。"""

        if not PANDAS_READY:
            return
        if getattr(self, "_student_data_pending_load", False):
            # 尚未加载实际数据时跳过写入，避免把占位状态写入磁盘
            return
        if not self._ensure_student_data_ready():
            return
        roll_state_json = self._encode_class_states()
        if roll_state_json is None:
            return
        if self.student_workbook is None:
            if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
                return
            try:
                snapshot = self.student_data.copy()
            except Exception:
                snapshot = pd.DataFrame(self.student_data)
            class_name = self.current_class_name or "班级1"
            self.current_class_name = class_name
            self.student_workbook = StudentWorkbook(
                OrderedDict({class_name: snapshot}),
                active_class=class_name,
            )
        else:
            self._snapshot_current_class()
        if self.student_workbook is None:
            return
        try:
            data = self.student_workbook.as_dict()
        except Exception:
            return
        self._refresh_student_file_paths()
        try:
            _save_student_workbook(
                data,
                self._plain_file_path,
                roll_state_json=roll_state_json,
                include_internal_columns=False,
            )
        except Exception as exc:
            logger.debug("Failed to persist roll state into workbook: %s", exc, exc_info=True)

    def _schedule_workbook_persist(self) -> None:
        if is_app_closing():
            return
        timer = getattr(self, "_workbook_persist_timer", None)
        if isinstance(timer, QTimer):
            timer.start()

    def _flush_workbook_persist(self) -> None:
        timer = getattr(self, "_workbook_persist_timer", None)
        if isinstance(timer, QTimer) and timer.isActive():
            timer.stop()
        # 退出/切班等关键节点使用同步写入，尽量保证状态落盘。
        self._persist_roll_state_to_workbook()

    def _queue_workbook_persist(self) -> None:
        if is_app_closing():
            return
        if getattr(self, "_workbook_persist_inflight", False):
            self._workbook_persist_dirty = True
            return
        if not PANDAS_READY or getattr(self, "_student_data_pending_load", False):
            return
        if not self._ensure_student_data_ready():
            return

        roll_state_json = self._encode_class_states()
        if not roll_state_json:
            return

        # 在 UI 线程中准备好写入参数（仅包含纯数据），后台线程只做 I/O 写盘。
        if self.student_workbook is None:
            if self.student_data is None or not isinstance(self.student_data, pd.DataFrame):
                return
            try:
                snapshot = self.student_data.copy()
            except Exception:
                snapshot = pd.DataFrame(self.student_data)
            class_name = self.current_class_name or "班级1"
            self.current_class_name = class_name
            self.student_workbook = StudentWorkbook(
                OrderedDict({class_name: snapshot}),
                active_class=class_name,
            )
        else:
            self._snapshot_current_class()
        try:
            data = self.student_workbook.as_dict() if self.student_workbook is not None else None
        except Exception:
            data = None
        if not data:
            return
        data = _snapshot_workbook_data(data)
        self._refresh_student_file_paths()
        file_path = str(getattr(self, "_plain_file_path", "") or "").strip()
        if not file_path:
            return

        self._workbook_persist_inflight = True
        worker = _IOWorker(
            _save_student_workbook,
            data,
            file_path,
            roll_state_json=roll_state_json,
            include_internal_columns=False,
        )

        def _finish(_result: object) -> None:
            self._workbook_persist_inflight = False
            if getattr(self, "_workbook_persist_dirty", False):
                self._workbook_persist_dirty = False
                self._schedule_workbook_persist()

        def _error(_message: str, _exc: object) -> None:
            self._workbook_persist_inflight = False
            if getattr(self, "_workbook_persist_dirty", False):
                self._workbook_persist_dirty = False
                self._schedule_workbook_persist()

        worker.signals.finished.connect(_finish)
        worker.signals.error.connect(_error)
        self._io_pool.start(worker)


# ---------- 关于 ----------
class AboutDialog(_EnsureOnScreenMixin, QDialog):
    def __init__(self, parent: Optional[QWidget] = None) -> None:
        super().__init__(parent)
        self.setWindowTitle("关于")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(18, 18, 18, 18)
        layout.setSpacing(12)
        info = QLabel(
            (
                "<b>课堂工具箱 V6.0</b><br>"
                "作者：广州番禺王耀强<br>"
                "知乎主页：<a href='https://www.zhihu.com/people/sciman/columns'>sciman</a><br>"
                "公众号：sciman逸居<br>"
                "GitHub：<a href='https://github.com/sciman-top/ClassroomTools'>sciman-top/ClassroomTools</a><br>"
                "“初中物理教研”Q群：323728546"
            )
        )
        info.setOpenExternalLinks(False)
        info.setTextFormat(Qt.TextFormat.RichText)
        info.linkActivated.connect(self._open_link)
        layout.addWidget(info)
        # 移除确定按钮，用户可直接点击对话框关闭按钮
        self.setFixedSize(self.sizeHint())

    def _open_link(self, url: str) -> None:
        if not url:
            return
        try:
            QDesktopServices.openUrl(QUrl(url))
        except Exception:
            pass

# ---------- 数据 ----------
def _snapshot_workbook_data(
    data: Mapping[str, PandasDataFrame],
) -> "OrderedDict[str, PandasDataFrame]":
    """Create a stable snapshot for background workbook persistence."""

    snapshot: "OrderedDict[str, PandasDataFrame]" = OrderedDict()
    for name, df in data.items():
        try:
            snapshot[name] = df.copy()
        except Exception:
            try:
                snapshot[name] = pd.DataFrame(df)
            except Exception:
                snapshot[name] = df
    return snapshot


def _write_student_workbook(
    file_path: str,
    data: Mapping[str, PandasDataFrame],
    roll_state_json: Optional[str] = None,
    *,
    include_internal_columns: bool = True,
) -> None:
    """Write the provided workbook mapping to *file_path* atomically."""

    payload = _export_student_workbook_bytes(
        data,
        roll_state_json=roll_state_json,
        include_internal_columns=include_internal_columns,
    )
    _atomic_write_bytes(file_path, payload, suffix=".xlsx", description="workbook")


def _read_student_workbook(
    existing_plain: Optional[str],
    plain_file_path: str,
) -> Tuple[Optional[StudentWorkbook], bool, Optional[str]]:
    """Load student data from an existing Excel file or create a template."""

    if not (PANDAS_AVAILABLE and OPENPYXL_AVAILABLE):
        raise RuntimeError("缺少 pandas 和 openpyxl")

    def _ensure_readable_student_file(path: Optional[str], label: str) -> str:
        normalized = os.path.abspath(path or "")
        if not path or not os.path.isfile(normalized):
            raise FileNotFoundError(f"{label}不存在或不可访问: {normalized}")
        return normalized

    created_template = False
    embedded_roll_state: Optional[str] = None

    target_path = existing_plain or plain_file_path
    if not target_path or not os.path.exists(target_path):
        template = pd.DataFrame(
            {
                "学号": [101, 102, 103],
                "姓名": ["张三", "李四", "王五"],
                "班级": ["A", "B", "A"],
                "分组": ["一组", "二组", "一组"],
            }
        )
        workbook = StudentWorkbook(OrderedDict({"班级1": template}), active_class="班级1")
        _save_student_workbook(
            workbook.as_dict(),
            plain_file_path,
            roll_state_json=None,
        )
        created_template = True
        return workbook, created_template, embedded_roll_state

    read_path = _ensure_readable_student_file(target_path, "学生数据文件")

    try:
        raw_data = pd.read_excel(read_path, sheet_name=None)
    except Exception as exc:
        raise RuntimeError(f"无法读取学生数据文件 {read_path}: {exc}") from exc

    if isinstance(raw_data, dict):
        for sheet_name, df in raw_data.items():
            if sheet_name == "_ROLL_STATE":
                continue
            missing = _missing_student_columns(df)
            if missing:
                logger.warning(
                    "学生数据表 %s 缺少必要列：%s",
                    sheet_name,
                    "、".join(missing),
                )

    if isinstance(raw_data, dict) and "_ROLL_STATE" in raw_data:
        try:
            state_df = raw_data.pop("_ROLL_STATE")
            if isinstance(state_df, pd.DataFrame) and not state_df.empty:
                value = state_df.iloc[0, 0]
                embedded_roll_state = str(value) if isinstance(value, str) else None
        except Exception:
            embedded_roll_state = None
    _set_embedded_roll_state(embedded_roll_state)

    workbook = StudentWorkbook(OrderedDict(raw_data), active_class="")
    # 读取现有文件成功后，“镜像保存到程序目录”属于优化路径：失败不应阻塞点名功能。
    try:
        _save_student_workbook(
            workbook.as_dict(),
            plain_file_path,
            roll_state_json=embedded_roll_state,
        )
    except OSError as exc:
        logger.warning("无法写入学生数据镜像文件 %s: %s", plain_file_path, exc)
    except Exception as exc:
        logger.warning("写入学生数据镜像文件失败 %s: %s", plain_file_path, exc)
    return workbook, created_template, embedded_roll_state


def _export_student_workbook_bytes(
    data: Mapping[str, PandasDataFrame],
    roll_state_json: Optional[str] = None,
    *,
    include_internal_columns: bool = True,
) -> bytes:
    """Normalize the workbook mapping and render it to Excel bytes."""

    normalized: "OrderedDict[str, PandasDataFrame]" = OrderedDict()
    used_names: Set[str] = set()
    for idx, (name, df) in enumerate(data.items(), start=1):
        fallback = f"班级{idx}" if idx > 1 else "班级1"
        sheet_name = _unique_sheet_name(name, fallback, used_names)
        try:
            normalized_df = _normalize_student_dataframe(df, drop_incomplete=False)
        except Exception:
            normalized_df = pd.DataFrame(df)
        if not include_internal_columns and STUDENT_ROW_ID_COLUMN in normalized_df.columns:
            normalized_df = normalized_df.drop(columns=[STUDENT_ROW_ID_COLUMN], errors="ignore")
        normalized[sheet_name] = normalized_df
    if roll_state_json is not None:
        normalized["_ROLL_STATE"] = pd.DataFrame({"ROLL_STATE_JSON": [roll_state_json]})
    if not normalized:
        normalized["班级1"] = _empty_student_dataframe().copy()

    def _render_bytes(engine: Optional[str]) -> bytes:
        buffer = io.BytesIO()
        with pd.ExcelWriter(buffer, engine=engine) as writer:  # type: ignore[call-arg]
            for sheet_name, df in normalized.items():
                df.to_excel(writer, sheet_name=sheet_name, index=False)
        buffer.seek(0)
        return buffer.getvalue()

    engine = "openpyxl" if OPENPYXL_AVAILABLE else None
    try:
        return _render_bytes(engine)
    except Exception as exc:
        fallback_engine = None if engine else ("openpyxl" if OPENPYXL_AVAILABLE else None)
        try:
            return _render_bytes(fallback_engine)
        except Exception:
            raise RuntimeError(f"Failed to export student workbook: {exc}") from exc


def _save_student_workbook(
    data: Mapping[str, PandasDataFrame],
    file_path: str,
    *,
    roll_state_json: Optional[str] = None,
    include_internal_columns: bool = True,
) -> None:
    """Persist the workbook to disk and clean up duplicate candidate files."""

    _write_student_workbook(
        file_path,
        data,
        roll_state_json=roll_state_json,
        include_internal_columns=include_internal_columns,
    )
    keep_plain = file_path if os.path.exists(file_path) else None
    _cleanup_student_candidates(keep_plain)


def _load_student_workbook_background() -> Tuple[Optional[StudentWorkbook], bool, Optional[str], str]:
    """后台读取学生数据，避免 UI 线程阻塞。"""

    resources = _STUDENT_RESOURCES
    file_path = resources.plain
    if not (PANDAS_AVAILABLE and OPENPYXL_AVAILABLE):
        return None, False, "缺少 pandas/openpyxl，点名功能不可用。", file_path
    existing_plain = _any_existing_path(resources.plain_candidates)
    try:
        workbook, created, _embedded = _read_student_workbook(
            existing_plain,
            file_path,
        )
        return workbook, created, None, file_path
    except Exception as exc:
        return None, False, str(exc), file_path
def load_student_data(parent: Optional[QWidget]) -> Optional[StudentWorkbook]:
    """从 students.xlsx 读取点名所需的数据，不存在时自动生成模板。"""

    if not ensure_rollcall_dependencies(parent):
        return None

    resources = _STUDENT_RESOURCES
    file_path = resources.plain
    existing_plain = _any_existing_path(resources.plain_candidates)

    try:
        workbook, created, _embedded = _read_student_workbook(
            existing_plain,
            file_path,
        )
        if workbook is None:
            return None
        if created:
            show_quiet_information(parent, f"未找到学生数据，已为你创建模板文件：{file_path}")
        return workbook
    except Exception as exc:
        QMessageBox.critical(parent, "错误", f"无法加载学生数据，文件格式异常\n详情: {exc}")
        return None


# ---------- 启动器 ----------
class LauncherBubble(_EnsureOnScreenMixin, QWidget):
    """启动器缩小时显示的悬浮圆球，负责发出恢复指令。"""

    restore_requested = pyqtSignal()
    position_changed = pyqtSignal(QPoint)

    def __init__(self, diameter: int = 42) -> None:
        super().__init__(
            None,
            Qt.WindowType.Tool
            | Qt.WindowType.FramelessWindowHint
            | Qt.WindowType.WindowStaysOnTopHint,
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground, True)
        self.setAttribute(Qt.WidgetAttribute.WA_NoSystemBackground, True)
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, True)
        self.setCursor(Qt.CursorShape.PointingHandCursor)
        self.setWindowTitle("ClassroomTools Bubble")
        self._diameter = max(32, diameter)
        self.setFixedSize(self._diameter, self._diameter)
        self.setWindowOpacity(0.74)
        self._ensure_min_width = self._diameter
        self._ensure_min_height = self._diameter
        self._dragging = False
        self._drag_offset = QPoint()
        self._moved = False

    def place_near(self, target: QPoint, screen: Optional[QScreen]) -> None:
        """将气泡吸附到距离 target 最近的屏幕边缘。"""

        if screen is None:
            screen = QApplication.screenAt(target) or QApplication.primaryScreen()
        if screen is None:
            return
        available = screen.availableGeometry()
        margin = 6
        bubble_size = self.size()
        center = QPoint(int(target.x()), int(target.y()))
        center.setX(max(available.left(), min(center.x(), available.right())))
        center.setY(max(available.top(), min(center.y(), available.bottom())))

        distances = {
            "left": abs(center.x() - available.left()),
            "right": abs(available.right() - center.x()),
            "top": abs(center.y() - available.top()),
            "bottom": abs(available.bottom() - center.y()),
        }
        nearest_edge = min(distances, key=distances.get)
        if nearest_edge == "left":
            x = available.left() + margin
            y = center.y() - bubble_size.height() // 2
        elif nearest_edge == "right":
            x = available.right() - bubble_size.width() - margin
            y = center.y() - bubble_size.height() // 2
        elif nearest_edge == "top":
            y = available.top() + margin
            x = center.x() - bubble_size.width() // 2
        else:
            y = available.bottom() - bubble_size.height() - margin
            x = center.x() - bubble_size.width() // 2

        x = max(available.left() + margin, min(x, available.right() - bubble_size.width() - margin))
        y = max(available.top() + margin, min(y, available.bottom() - bubble_size.height() - margin))
        self.move(int(x), int(y))
        self.position_changed.emit(self.pos())

    def snap_to_edge(self) -> None:
        screen = self.screen() or QApplication.screenAt(self.frameGeometry().center()) or QApplication.primaryScreen()
        if screen is None:
            return
        self.place_near(self.frameGeometry().center(), screen)

    def paintEvent(self, event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing, True)
        rect = self.rect()
        color = QColor(28, 30, 36, 158)
        highlight = QColor(102, 157, 246, 172)
        painter.setBrush(QBrush(color))
        painter.setPen(Qt.PenStyle.NoPen)
        painter.drawEllipse(rect)
        painter.setBrush(QBrush(highlight))
        painter.drawEllipse(rect.adjusted(rect.width() // 3, rect.height() // 3, -rect.width() // 6, -rect.height() // 6))

    def mousePressEvent(self, event) -> None:
        if event.button() == Qt.MouseButton.LeftButton:
            self._dragging = True
            self._drag_offset = event.position().toPoint()
            self._moved = False
        super().mousePressEvent(event)

    def mouseMoveEvent(self, event) -> None:
        if self._dragging and event.buttons() & Qt.MouseButton.LeftButton:
            new_pos = event.globalPosition().toPoint() - self._drag_offset
            self.move(new_pos)
            self._moved = True
        super().mouseMoveEvent(event)

    def mouseReleaseEvent(self, event) -> None:
        if event.button() == Qt.MouseButton.LeftButton:
            if not self._moved:
                self.restore_requested.emit()
            else:
                self.snap_to_edge()
                self.position_changed.emit(self.pos())
            self._dragging = False
        super().mouseReleaseEvent(event)

class LauncherWindow(QWidget):
    def __init__(self, settings_manager: SettingsManager, student_workbook: Optional[StudentWorkbook]) -> None:
        super().__init__(
            None,
            Qt.WindowType.Tool | Qt.WindowType.FramelessWindowHint | Qt.WindowType.WindowStaysOnTopHint,
        )
        self.settings_manager = settings_manager
        self._init_state(student_workbook)
        self._configure_window()
        container = self._build_ui()
        self._apply_button_metrics()
        self._finalize_drag_regions(container)
        self._apply_saved_state()
        self._enforce_feature_availability()

    def _init_state(self, student_workbook: Optional[StudentWorkbook]) -> None:
        self.student_workbook = student_workbook
        self.student_data: Optional[PandasDataFrame] = None
        if PANDAS_READY and student_workbook is not None:
            try:
                self.student_data = student_workbook.get_active_dataframe()
            except Exception:
                self.student_data = _new_student_dataframe() or pd.DataFrame(columns=DEFAULT_STUDENT_COLUMNS)
        self.overlay: Optional[OverlayWindow] = None
        self.roll_call_window: Optional[RollCallTimerWindow] = None
        self._dragging = False
        self._drag_origin: Optional[QPoint] = None
        self._drag_offset = QPoint()
        self.bubble: Optional[LauncherBubble] = None
        self._last_position = QPoint()
        self._bubble_position = QPoint()
        self._minimized = False
        self._minimized_on_start = False
        self._auto_exit_seconds = 0
        self._auto_exit_timer = QTimer(self)
        self._auto_exit_timer.setSingleShot(True)
        self._auto_exit_timer.timeout.connect(self.request_exit)
        self._student_load_inflight = False
        self._student_load_pending_open = False
        self._student_load_error: Optional[str] = None
        self._student_load_worker: Optional[_IOWorker] = None

    def _configure_window(self) -> None:
        self.setAttribute(Qt.WidgetAttribute.WA_StyledBackground, True)
        self.setStyleSheet(
            f"""
            QWidget#launcherContainer {{
                background: qlineargradient(x1:0, y1:0, x2:0, y2:1,
                    stop:0 rgba(45, 45, 55, 250),
                    stop:1 rgba(30, 35, 45, 250));
                border-radius: 10px;
                border: 1px solid rgba(120, 130, 150, 50);
            }}
            QPushButton {{
                color: #E8EAED;
                background-color: rgba(255, 255, 255, 12);
                border: 1px solid rgba(255, 255, 255, 20);
                border-radius: 6px;
                padding: 4px 14px;
                min-height: 28px;
                font-weight: 500;
                font-size: 13px;
            }}
            QPushButton:hover {{
                background-color: rgba(66, 133, 244, 200);
                border-color: rgba(66, 133, 244, 255);
                color: #FFFFFF;
            }}
            QPushButton:pressed {{
                background-color: rgba(66, 133, 244, 255);
                border-color: rgba(66, 133, 244, 255);
                color: #FFFFFF;
            }}
            QPushButton:focus {{
                border-color: rgba(66, 133, 244, 200);
                outline: none;
            }}
            QCheckBox {{
                color: #E8EAED;
                spacing: 6px;
                font-size: 13px;
            }}
            QCheckBox::indicator {{
                width: 16px;
                height: 16px;
                border-radius: 3px;
                border: 1px solid rgba(255, 255, 255, 120);
                background: rgba(255, 255, 255, 10);
            }}
            QCheckBox::indicator:checked {{
                background-color: {StyleConfig.PRIMARY_COLOR};
                border-color: {StyleConfig.PRIMARY_COLOR};
            }}
            QCheckBox::indicator:hover {{
                border-color: rgba(66, 133, 244, 200);
                background: rgba(255, 255, 255, 18);
            }}
            """
        )

    def _build_ui(self) -> QWidget:
        container = QWidget(self)
        container.setObjectName("launcherContainer")

        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.addWidget(container)

        container_layout = QVBoxLayout(container)
        container_layout.setContentsMargins(8, 8, 8, 8)
        container_layout.setSpacing(5)

        action_row = QGridLayout()
        action_row.setContentsMargins(0, 0, 0, 0)
        action_row.setHorizontalSpacing(0)
        for column in (0, 2, 4):
            action_row.setColumnMinimumWidth(column, 12)
            action_row.setColumnStretch(column, 1)

        self.paint_button = QPushButton("画笔")
        self.paint_button.clicked.connect(self.toggle_paint)
        action_row.addItem(
            QSpacerItem(0, 0, QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Minimum),
            0,
            0,
        )
        action_row.addWidget(self.paint_button, 0, 1)
        action_row.addItem(
            QSpacerItem(0, 0, QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Minimum),
            0,
            2,
        )

        self.roll_call_button = QPushButton("点名")
        self.roll_call_button.clicked.connect(self.toggle_roll_call)
        action_row.addWidget(self.roll_call_button, 0, 3)
        action_row.addItem(
            QSpacerItem(0, 0, QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Minimum),
            0,
            4,
        )

        container_layout.addLayout(action_row)

        bottom_row = QHBoxLayout()
        bottom_row.setSpacing(3)

        self.minimize_button = QPushButton("缩小")
        self.minimize_button.clicked.connect(self.minimize_launcher)
        bottom_row.addWidget(self.minimize_button)

        bottom_row.addStretch(1)

        self.about_button = QPushButton("关于")
        self.about_button.clicked.connect(self.show_about)
        bottom_row.addWidget(self.about_button)

        self.settings_button = QPushButton("设置")
        self.settings_button.clicked.connect(self.show_settings_dialog)
        bottom_row.addWidget(self.settings_button)

        self.exit_button = QPushButton("退出")
        self.exit_button.clicked.connect(self.request_exit)
        bottom_row.addWidget(self.exit_button)

        container_layout.addLayout(bottom_row)

        return container

    def _apply_button_metrics(self) -> None:
        unified_width = self._action_button_width()
        self.paint_button.setFixedWidth(unified_width)
        self.roll_call_button.setFixedWidth(unified_width)

        auxiliary_width = max(self.minimize_button.sizeHint().width(), 52)
        self.minimize_button.setFixedWidth(auxiliary_width)

        info_width = max(
            self.about_button.sizeHint().width(),
            self.settings_button.sizeHint().width(),
            self.exit_button.sizeHint().width(),
            52,
        )
        self.about_button.setFixedWidth(info_width)
        self.settings_button.setFixedWidth(info_width)
        self.exit_button.setFixedWidth(info_width)

        button_heights = [
            self.paint_button.sizeHint().height(),
            self.roll_call_button.sizeHint().height(),
            self.minimize_button.sizeHint().height(),
            self.about_button.sizeHint().height(),
            self.settings_button.sizeHint().height(),
            self.exit_button.sizeHint().height(),
        ]
        target_height = max(button_heights)
        for button in (
            self.paint_button,
            self.roll_call_button,
            self.minimize_button,
            self.about_button,
            self.settings_button,
            self.exit_button,
        ):
            button.setFixedHeight(target_height)

    def _finalize_drag_regions(self, container: QWidget) -> None:
        for widget in (
            self,
            container,
            self.paint_button,
            self.roll_call_button,
            self.minimize_button,
        ):
            widget.installEventFilter(self)

        self.adjustSize()
        self.setFixedSize(self.sizeHint())
        self._base_minimum_width = self.minimumWidth()
        self._base_minimum_height = self.minimumHeight()
        self._ensure_min_width = self.width()
        self._ensure_min_height = self.height()

    def _apply_saved_state(self) -> None:
        launcher_settings = self.settings_manager.get_launcher_state()

        position = QPoint(launcher_settings.position)
        self.move(position)
        self._last_position = QPoint(position)

        bubble_position = QPoint(launcher_settings.bubble_position)
        if bubble_position.isNull():
            bubble_position = QPoint(position)
        self._bubble_position = bubble_position

        self._minimized = launcher_settings.minimized
        self._minimized_on_start = launcher_settings.minimized
        self._auto_exit_seconds = max(0, int(launcher_settings.auto_exit_seconds))
        self._schedule_auto_exit_timer()

    def _schedule_auto_exit_timer(self) -> None:
        self._auto_exit_timer.stop()
        if self._auto_exit_seconds > 0:
            self._auto_exit_timer.start(self._auto_exit_seconds * 1000)

    def _enforce_feature_availability(self) -> None:
        if not (PANDAS_AVAILABLE and OPENPYXL_AVAILABLE):
            self.roll_call_button.setEnabled(False)

    def _action_button_width(self) -> int:
        """计算“画笔”与“点名/计时”按钮的统一宽度，保证观感一致。"""

        paint_metrics = QFontMetrics(self.paint_button.font())
        roll_metrics = QFontMetrics(self.roll_call_button.font())
        paint_texts = ["画笔", "隐藏画笔"]
        roll_texts = ["点名/计时", "显示点名", "隐藏点名"]
        max_width = max(
            max(paint_metrics.horizontalAdvance(text) for text in paint_texts),
            max(roll_metrics.horizontalAdvance(text) for text in roll_texts),
        )
        return max_width + 28

    def showEvent(self, e) -> None:
        super().showEvent(e)
        ensure_widget_within_screen(self)
        self._last_position = self.pos()
        if self._minimized_on_start:
            QTimer.singleShot(0, self._restore_minimized_state)

        if not getattr(self, "_system_checked", False):
            self._system_checked = True
            # 使用后台线程执行轻量检测，避免启动阶段阻塞 UI
            worker = _IOWorker(collect_quick_diagnostics)
            worker.signals.finished.connect(self._on_system_check_finished)
            QThreadPool.globalInstance().start(worker)

        if not getattr(self, "_student_data_prefetched", False):
            self._student_data_prefetched = True
            self._start_student_background_load()

    def _on_system_check_finished(self, result: Any) -> None:
        if not isinstance(result, tuple) or len(result) != 4:
            return
        has_issues = result[0]
        if has_issues:
            show_diagnostic_result(self, result)

    def _apply_loaded_workbook(self, workbook: StudentWorkbook) -> None:
        self.student_workbook = workbook
        if PANDAS_READY:
            try:
                self.student_data = workbook.get_active_dataframe()
            except Exception:
                self.student_data = _new_student_dataframe() or pd.DataFrame(
                    columns=DEFAULT_STUDENT_COLUMNS
                )

    def _start_student_background_load(self) -> None:
        if self.student_workbook is not None:
            return
        if self._student_load_inflight:
            return
        if not (PANDAS_AVAILABLE and OPENPYXL_AVAILABLE):
            return
        self._student_load_inflight = True
        worker = _IOWorker(_load_student_workbook_background)
        self._student_load_worker = worker
        worker.signals.finished.connect(self._on_student_load_finished)
        worker.signals.error.connect(self._on_student_load_failed)
        QThreadPool.globalInstance().start(worker)

    def _on_student_load_failed(self, message: str, exc: object) -> None:
        self._student_load_inflight = False
        self._student_load_error = message or "学生数据加载失败"
        if isinstance(exc, BaseException):
            logger.warning("Background student load failed: %s", message, exc_info=exc)
        else:
            logger.warning("Background student load failed: %s", message)
        if self._student_load_pending_open:
            self._student_load_pending_open = False
            show_quiet_information(self, f"学生数据加载失败，请稍后重试。\n详情：{self._student_load_error}")

    def _on_student_load_finished(self, result: Any) -> None:
        self._student_load_inflight = False
        if not isinstance(result, tuple) or len(result) < 4:
            return
        workbook, created, error, file_path = result[0], result[1], result[2], result[3]
        if error:
            self._student_load_error = str(error)
            logger.warning("Background student load failed: %s", self._student_load_error)
            if self._student_load_pending_open:
                self._student_load_pending_open = False
                show_quiet_information(self, f"学生数据加载失败，请稍后重试。\n详情：{self._student_load_error}")
            return
        self._student_load_error = None
        if workbook is not None and self.student_workbook is None:
            self._apply_loaded_workbook(workbook)
        if created and file_path:
            show_quiet_information(self, f"未找到学生数据，已为你创建模板文件：{file_path}")
        if self._student_load_pending_open:
            self._student_load_pending_open = False
            self._open_roll_call_window()

    def eventFilter(self, obj, e) -> bool:
        drag_blockers = {self.paint_button, self.roll_call_button, self.minimize_button}
        if obj not in drag_blockers:
            if e.type() == QEvent.Type.MouseButtonPress and e.button() == Qt.MouseButton.LeftButton:
                self._drag_origin = e.globalPosition().toPoint()
                self._drag_offset = self._drag_origin - self.pos()
                self._dragging = False
            elif e.type() == QEvent.Type.MouseMove and self._drag_origin and e.buttons() & Qt.MouseButton.LeftButton:
                delta = e.globalPosition().toPoint() - self._drag_origin
                if (not self._dragging) and (abs(delta.x()) >= 3 or abs(delta.y()) >= 3):
                    self._dragging = True
                if self._dragging:
                    self.move(e.globalPosition().toPoint() - self._drag_offset)
            elif e.type() == QEvent.Type.MouseButtonRelease and e.button() == Qt.MouseButton.LeftButton:
                if self._dragging:
                    self._last_position = self.pos()
                    self.save_position()
                self._dragging = False
                self._drag_origin = None
        return super().eventFilter(obj, e)

    def save_position(self) -> None:
        anchor_position = (
            self._last_position if (self._minimized and not self._last_position.isNull()) else self.pos()
        )
        position = QPoint(anchor_position)
        bubble_source = self._bubble_position if not self._bubble_position.isNull() else position
        bubble_position = QPoint(bubble_source)

        launcher_settings = LauncherSettings(
            position=position,
            bubble_position=bubble_position,
            minimized=self._minimized,
            auto_exit_seconds=self._auto_exit_seconds,
        )
        self.settings_manager.update_launcher_settings(launcher_settings)


    def show_settings_dialog(self) -> None:
        current_minutes = max(0, int(round(self._auto_exit_seconds / 60)))
        dialog = QInputDialog(self)
        dialog.setWindowTitle("设置自动关闭")
        dialog.setLabelText("软件自动关闭时间\n（分钟，0 表示不自动关闭）：")
        dialog.setInputMode(QInputDialog.InputMode.IntInput)
        dialog.setIntRange(0, 1440)
        dialog.setIntValue(current_minutes)
        # 设置样式，确保按钮可见
        dialog.setStyleSheet(StyleConfig.DIAGNOSTIC_DIALOG_STYLE)
        # 先显示对话框，确保按钮已创建
        dialog.show()
        # 修改按钮文案为中文并限制尺寸
        buttons = dialog.findChildren(QPushButton)
        for btn in buttons:
            text = btn.text()
            if "OK" in text:
                btn.setText("确定")
                btn.setFixedSize(80, 28)  # 宽度80px，高度28px，紧凑
            elif "Cancel" in text:
                btn.setText("取消")
                btn.setFixedSize(80, 28)  # 宽度80px，高度28px，紧凑
        result = dialog.exec()
        if result == QDialog.DialogCode.Rejected:
            return
        minutes = dialog.intValue()
        self._auto_exit_seconds = max(0, int(minutes)) * 60
        self._schedule_auto_exit_timer()
        self.save_position()

    def _ensure_overlay_ready(self) -> "OverlayWindow":
        if self.overlay is None:
            self.overlay = OverlayWindow(self.settings_manager)
            # 确保新创建的窗口初始为隐藏状态
            self.overlay.hide()
            if hasattr(self.overlay, "toolbar") and self.overlay.toolbar:
                self.overlay.toolbar.hide()
        return self.overlay

    def toggle_paint(self) -> None:
        """打开或隐藏屏幕画笔覆盖层。"""
        try:
            overlay = self._ensure_overlay_ready()
            if overlay.isVisible():
                overlay.hide_overlay()
                self.paint_button.setText("画笔")
            else:
                overlay.show_overlay()
                self.raise_()
                self.paint_button.setText("隐藏画笔")
        except Exception as exc:
            logger.exception("Failed to toggle paint overlay: %s", exc)
            log_path = _resolve_log_path()
            message = "画笔功能启动失败，请查看日志或运行系统兼容性诊断。"
            if log_path:
                message += f"\n日志路径：{log_path}"
            show_quiet_information(self, message, "提示")

    def toggle_roll_call(self) -> None:
        """切换点名/计时窗口的显示状态，必要时先创建窗口。"""
        try:
            if self.roll_call_window is None:
                self._open_roll_call_window()
            else:
                if self.roll_call_window.isVisible():
                    self.roll_call_window.hide()
                    self.roll_call_button.setText("点名")
                else:
                    self.roll_call_window.show()
                    self.roll_call_window.raise_()
                    self.roll_call_button.setText("隐藏点名")
        except Exception as exc:
            logger.exception("Failed to toggle roll call window: %s", exc)
            log_path = _resolve_log_path()
            message = "点名窗口启动失败，请查看日志或运行系统兼容性诊断。"
            if log_path:
                message += f"\n日志路径：{log_path}"
            show_quiet_information(self, message, "提示")

    def _open_roll_call_window(self) -> None:
        if self.roll_call_window is not None:
            return
        if not ensure_rollcall_dependencies(self):
            return
        if self._student_load_inflight:
            self._student_load_pending_open = True
            show_quiet_information(self, "正在加载学生数据，请稍候再试。")
            return
        config = self.settings_manager.get_roll_call_settings()
        if self.student_workbook is None:
            workbook = load_student_data(self)
            if workbook is None:
                QMessageBox.warning(self, "提示", "学生数据加载失败，无法打开点名器。")
                return
            # 使用上次保存的班级作为初始 active_class，避免默认落到第一张表
            saved_class = str(config.current_class).strip()
            if saved_class and saved_class in workbook.class_names():
                workbook.set_active_class(saved_class)
            self._apply_loaded_workbook(workbook)
        else:
            saved_class = str(config.current_class).strip()
            if saved_class and saved_class in self.student_workbook.class_names():
                self.student_workbook.set_active_class(saved_class)
        self.roll_call_window = RollCallTimerWindow(
            self.settings_manager,
            self.student_workbook,
            parent=self,
        )
        self.roll_call_window.window_closed.connect(self.on_roll_call_window_closed)
        self.roll_call_window.visibility_changed.connect(self.on_roll_call_visibility_changed)
        self.roll_call_window.show()
        self.roll_call_button.setText("隐藏点名")

    def on_roll_call_window_closed(self) -> None:
        window = self.roll_call_window
        if window is not None:
            try:
                self.student_workbook = window.student_workbook
                self.student_data = window.student_data
            except Exception:
                pass
        self.roll_call_window = None
        self.roll_call_button.setText("点名")

    def on_roll_call_visibility_changed(self, visible: bool) -> None:
        self.roll_call_button.setText("隐藏点名" if visible else "点名")

    def request_exit(self) -> None:
        """优雅地关闭应用程序，确保所有窗口在退出前持久化状态。"""

        begin_app_shutdown("launcher_request_exit")
        app = QApplication.instance()
        if not self.close():
            return
        if app is not None:
            QTimer.singleShot(0, app.quit)

    def handle_about_to_quit(self) -> None:
        """在应用退出前的最后一道保险，保证关键状态已经写入配置。"""

        begin_app_shutdown("about_to_quit")
        self.save_position()
        window = self.roll_call_window
        if window is not None:
            try:
                window.shutdown_resources()
                window.save_settings()
                window._flush_workbook_persist()
            except RuntimeError:
                pass

    def minimize_launcher(self, from_settings: bool = False) -> None:
        """将启动器收纳为悬浮圆球。"""

        if self._minimized and not from_settings:
            return
        if self.bubble is None:
            self.bubble = LauncherBubble()
            self.bubble.restore_requested.connect(self.restore_from_bubble)
            self.bubble.position_changed.connect(self._on_bubble_position_changed)
        target_center = self.frameGeometry().center()
        screen = self.screen() or QApplication.screenAt(target_center) or QApplication.primaryScreen()
        if not from_settings:
            self._last_position = self.pos()
        self.hide()
        if from_settings and not self._bubble_position.isNull():
            self.bubble.place_near(self._bubble_position, screen)
        else:
            self.bubble.place_near(target_center, screen)
        self.bubble.setWindowOpacity(0.74)
        self.bubble.show()
        self.bubble.raise_()
        self._minimized = True
        self.save_position()

    def _restore_minimized_state(self) -> None:
        if not self._minimized_on_start:
            return
        self._minimized_on_start = False
        self.minimize_launcher(from_settings=True)

    def restore_from_bubble(self) -> None:
        """从悬浮球恢复主启动器窗口。"""

        self._minimized = False
        target_pos: Optional[QPoint] = None
        screen = None
        if self.bubble:
            self._bubble_position = self.bubble.pos()
            bubble_geom = self.bubble.frameGeometry()
            bubble_center = bubble_geom.center()
            screen = self.bubble.screen() or QApplication.screenAt(bubble_center) or QApplication.primaryScreen()
            margin = 12
            width = self.width() or self.sizeHint().width()
            height = self.height() or self.sizeHint().height()
            if screen is not None:
                available = screen.availableGeometry()
                distances = {
                    "left": abs(bubble_center.x() - available.left()),
                    "right": abs(available.right() - bubble_center.x()),
                    "top": abs(bubble_center.y() - available.top()),
                    "bottom": abs(available.bottom() - bubble_center.y()),
                }
                nearest_edge = min(distances, key=distances.get)
                if nearest_edge == "left":
                    x = bubble_geom.right() + margin
                    y = bubble_center.y() - height // 2
                elif nearest_edge == "right":
                    x = bubble_geom.left() - width - margin
                    y = bubble_center.y() - height // 2
                elif nearest_edge == "top":
                    y = bubble_geom.bottom() + margin
                    x = bubble_center.x() - width // 2
                else:
                    y = bubble_geom.top() - height - margin
                    x = bubble_center.x() - width // 2
                x = max(available.left(), min(int(x), available.right() - width))
                y = max(available.top(), min(int(y), available.bottom() - height))
                target_pos = QPoint(x, y)
            self.bubble.hide()
        if target_pos is None and not self._last_position.isNull():
            target_pos = QPoint(self._last_position)
        if target_pos is not None:
            self.move(target_pos)
        self.show()
        self.raise_()
        self.activateWindow()
        ensure_widget_within_screen(self)
        self._last_position = self.pos()
        self.save_position()

    def _on_bubble_position_changed(self, pos: QPoint) -> None:
        self._bubble_position = QPoint(pos.x(), pos.y())
        if self._minimized:
            self.save_position()

    def show_about(self) -> None:
        AboutDialog(self).exec()

    def closeEvent(self, e) -> None:
        begin_app_shutdown("launcher_close_event")
        self.save_position()
        if self.bubble is not None:
            self.bubble.close()
        if self.roll_call_window is not None:
            try:
                self.roll_call_window.shutdown_resources()
            except Exception:
                pass
            self.roll_call_window.close()
        if self.overlay is not None:
            self.overlay.close()
        super().closeEvent(e)


@dataclass
class ApplicationContext:
    settings_manager: SettingsManager
    student_workbook: Optional[StudentWorkbook]

    @classmethod
    def create(cls) -> "ApplicationContext":
        settings_manager = SettingsManager()
        workbook: Optional[StudentWorkbook] = None
        return cls(settings_manager=settings_manager, student_workbook=workbook)

    def create_launcher_window(self) -> LauncherWindow:
        return LauncherWindow(self.settings_manager, self.student_workbook)


# ---------- 入口 ----------
def main() -> None:
    """应用程序入口：初始化 DPI、加载设置并启动启动器窗口。"""
    _configure_logging()
    _install_exception_hook()
    ensure_high_dpi_awareness()
    _setup_qt_plugin_paths()
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    QToolTip.setFont(QFont("Microsoft YaHei UI", 9))
    app.aboutToQuit.connect(lambda: begin_app_shutdown("qt_aboutToQuit"))

    context = ApplicationContext.create()
    _run_startup_self_check(context.settings_manager)
    window = context.create_launcher_window()
    app.aboutToQuit.connect(window.handle_about_to_quit)
    window.show()
    try:
        if not parse_bool(os.environ.get("CTOOL_NO_STARTUP_DIAG"), False):
            has_issues, title, detail, suggest = collect_quick_diagnostics()
            if has_issues:
                logger.warning("启动诊断发现潜在问题：%s\n%s\n%s", title, detail, suggest)
                QTimer.singleShot(
                    300, lambda: show_diagnostic_result(window, (has_issues, title, detail, suggest))
                )
    except Exception:
        logger.debug("启动诊断流程异常，已忽略", exc_info=True)
    sys.exit(app.exec())

if __name__ == "__main__":
    main()
