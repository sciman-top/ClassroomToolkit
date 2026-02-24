# Classroom Writing Mode Design

**Date:** 2026-02-22  
**Scope:** Paint brush tuning for classroom all-in-one touch devices

## Goal

Introduce a device-level writing mode for classroom touch all-in-one machines while keeping style presets independent and easy for teachers.

## Decisions

1. Keep two independent concepts:
- `风格预设`: controls visual stroke style.
- `课室一体机模式`: controls device adaptation and stability.

2. Use layered UI:
- Style controls stay in the main paint settings area.
- Device mode is placed under a default-collapsed advanced section.

3. Runtime composition rule:
- Style preset provides baseline brush parameters.
- Device mode applies bounded multipliers/thresholds on top of baseline.

## Mode Mapping

`ClassroomWritingMode`:
- `Stable`: prioritize anti-jitter and robustness.
- `Balanced`: default for most classrooms.
- `Responsive`: prioritize low-latency and responsiveness.

Mapped runtime controls:
- Marker pressure width factor multiplier.
- Calligraphy real-pressure influence/scale multipliers.
- Stylus pseudo-pressure low/high thresholds.
- Calligraphy preview minimum distance.

## Parameter Matrix

| Mode | Marker pressure multiplier | Calligraphy influence multiplier | Calligraphy scale multiplier | Pseudo pressure low/high | Calligraphy preview min distance |
| --- | --- | --- | --- | --- | --- |
| Stable | 0.85 | 0.84 | 0.82 | 0.0002 / 0.9998 | 2.6 |
| Balanced | 1.00 | 1.00 | 1.00 | 0.0001 / 0.9999 | 2.0 |
| Responsive | 1.18 | 1.14 | 1.12 | 0.00005 / 0.99995 | 1.4 |

Runtime safety clamps:
- Marker `PressureWidthFactor`: `[0.02, 0.45]`
- Calligraphy `RealPressureWidthInfluence`: `[0.2, 0.9]`
- Calligraphy `RealPressureWidthScale`: `[0.12, 0.55]`
- Pseudo pressure low threshold: `[0.0, 0.49]`
- Pseudo pressure high threshold: `[low + 0.001, 1.0]`
- Calligraphy preview minimum distance: `[1.0, 4.0]`

Pseudo-pressure classification rule:
- If `rawPressure` is non-finite (`NaN`, `+Inf`, `-Inf`) -> treat as unavailable.
- If `rawPressure <= lowThreshold` or `rawPressure >= highThreshold` -> treat as unavailable.
- Only `(lowThreshold, highThreshold)` open interval is treated as valid stylus pressure.

## Persistence

- Add `classroom_writing_mode` to `[Paint]` in `settings.ini`.
- Fallback to `Balanced` when missing/invalid.

## Compatibility

- No ink serialization format changes.
- Existing style presets and settings remain valid.
- No dependency changes.

## Replay Fixture Validation

Stylus replay fixture categories:
- Legacy pseudo-pressure trace: alternating `0/1`, should fully downgrade to pointer samples.
- Threshold-edge trace: pressures around low/high boundaries, should show mode-specific segmentation:
  - Stable accepts least
  - Balanced accepts mid
  - Responsive accepts most
- Modern continuous trace: all samples should remain stylus pressure and preserve width ordering:
  - Marker: Stable `<` Balanced `<` Responsive
  - Calligraphy: mode-sensitive output differences should be observable (not near-identical)
