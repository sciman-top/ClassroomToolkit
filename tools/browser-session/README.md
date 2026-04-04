# Browser Session Helper

This folder is intentionally copyable to target repositories.

## What It Does

The helper starts a dedicated Chromium-based browser instance with:

- a separate user-data directory
- a fixed remote debugging port
- a repeatable attach command for `agent-browser`
- session metadata (`.meta/<name>.json`) for status/stop/cleanup
- CDP handshake checks to avoid attaching to the wrong process

That gives one stable browser profile for automation tasks, without mixing it with daily browsing profiles.

## Files

- `start-browser-session.ps1`
- `start-browser-session.bat`

## Quick Start

```powershell
powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Name github -Url https://github.com
agent-browser --cdp 9222 open https://github.com
```

If an existing browser session is already running:

```powershell
powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Name github -AttachOnly
```

Check status and stop:

```powershell
powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action status -Name github
powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action stop -Name github
```

Cleanup profile + metadata:

```powershell
powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action cleanup -Name github
```

## Defaults and Safety

- Default browser mode is `-Browser auto` (prefer Chrome, fallback Edge).
- Default launch disables extensions for automation isolation.
- Use `-AllowExtensions` only when your flow explicitly needs them.
- If port is listening but CDP handshake fails, script exits with error instead of attaching blindly.

## AGENTS Integration Snippet

```text
When browser automation needs repeated login state, prefer tools/browser-session/start-browser-session.ps1 to start a dedicated Chromium session, then attach with agent-browser --cdp <port>.
```
