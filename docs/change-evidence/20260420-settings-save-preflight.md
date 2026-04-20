# 2026-04-20 Settings Save Preflight Hardening

- 规则映射: R1/R2/R3/R6/R8
- 风险等级: Low
- 当前落点: `src/ClassroomToolkit.Infra/Settings`
- 目标归宿: 在不改变 `settings.ini` / `settings.json` 数据格式、键语义与正常读写路径的前提下，阻止 `Save` 在“首次直存”“文件已验证后被外部修改/损坏”以及“内容被替换但时间戳被恢复”三类场景中覆盖现有损坏配置文件。

## 变更依据

- `SettingsRepository` 仅在调用过 `Load()` 后，才会利用 `LastLoadSucceeded` 阻止覆盖损坏的 INI；直接 `Save()` 会跳过该保护。
- `JsonSettingsDocumentStoreAdapter` 同样依赖先前 `Load()` 建立损坏阻断状态；直接 `Save()` 会覆盖现有损坏 JSON。
- 即便完成过一次成功 `Load()`，若配置文件随后被外部进程修改或损坏，原实现也不会再次预检当前文件状态，后续 `Save()` 仍可能直接覆盖。
- 仅依赖 `LastWriteTimeUtc` 仍有边界缺口：外部进程可以在替换文件内容后将时间戳恢复，导致时间戳探针误判“文件未变化”。
- 这与项目的兼容/防覆盖要求冲突：配置文件已损坏时，保存应优先保护原文件，而不是在未验证状态下直接覆盖。

## 诊断矩阵

| cmd | exit_code | key_output | timestamp |
|---|---:|---|---|
| `codex --version` | 0 | `codex-cli 0.121.0` | 2026-04-20 |
| `codex --help` | 0 | help 中未列出 `status` 子命令 | 2026-04-20 |
| `codex status` | 1 | 无输出；结合 `codex --help` 视为平台不支持该入口 | 2026-04-20 |

### platform_na

- kind: `platform_na`
- reason: 当前 Codex CLI `0.121.0` 的 `--help` 未暴露 `status` 子命令，`codex status` 返回 `exit_code=1` 且无可用输出。
- alternative_verification: 使用 `codex --help` 证明命令面板中不存在 `status`；活动规则来源为仓库根 `D:\OneDrive\CODE\ClassroomToolkit\AGENTS.md`，并承接用户提供的全局 `AGENTS.md`。
- evidence_link: `docs/change-evidence/20260420-settings-save-preflight.md`
- expires_at: `2026-05-20`

## 实施变更

1. `src/ClassroomToolkit.Infra/Settings/SettingsRepository.cs`
   - 在首次 `Save()` 前增加现有文件状态预检。
   - 若现有 INI 无法被 `IniSettingsStore.TryLoad()` 成功读取，则阻止写入，避免覆盖损坏文件。
   - 记录已验证文件的 `LastWriteTimeUtc + SHA-256 内容指纹`；只有两者都未变化时才跳过重验。
   - 成功保存后显式回写 `LastLoadSucceeded=true` 与已校验状态。

2. `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
   - 在首次 `Save()` 前增加现有 JSON 文件状态预检。
   - 若预检遇到 `JsonException`，沿用既有损坏阻断语义，拒绝覆盖现有 JSON。
   - 记录已验证文件的 `LastWriteTimeUtc + SHA-256 内容指纹`；只有两者都未变化时才跳过重验。
   - 对正常 JSON、缺失文件、以及既有“瞬时 IO 失败不阻断保存”的语义保持不变。

3. 测试补充
   - `tests/ClassroomToolkit.Tests/SettingsRepositoryTests.cs`
   - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
   - 新增“未先 `Load()` 直接 `Save()` 也必须保护已损坏配置文件”的回归测试。
   - 新增“已成功 `Load()` 后，文件被外部损坏，后续 `Save()` 仍必须拒绝覆盖”的回归测试。
   - 新增“文件内容被外部替换后再恢复原时间戳，后续 `Save()` 仍必须拒绝覆盖”的回归测试。

## 验证命令与结果

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: PASS
   - key_output: `0 warnings, 0 errors`

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: PASS
   - key_output: `Passed: 3343, Failed: 0, Skipped: 0`

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: PASS
   - key_output: `Passed: 28, Failed: 0, Skipped: 0`

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: PASS
   - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Hotspot 人工复核

- 复核对象: `SettingsRepository.cs`, `JsonSettingsDocumentStoreAdapter.cs`, 对应两组测试。
- 结论:
  - 仅增加保存前预检与状态记录，未修改配置结构、键名、编码策略、迁移版本写入逻辑。
  - 现有“非对象 JSON 允许后续恢复保存”的语义保持不变。
  - `INI` 对瞬时文件访问问题仍落回既有 `TryLoad()` 结果；`JSON` 仍只对 `JsonException` 触发覆盖阻断。
  - 未触碰 UI、Interop、业务名册、持久化格式或外部接口。
  - 变更总量集中、回滚边界清晰。

## 回滚

- 回滚文件:
  - `src/ClassroomToolkit.Infra/Settings/SettingsRepository.cs`
  - `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - `tests/ClassroomToolkit.Tests/SettingsRepositoryTests.cs`
  - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
- 回滚动作: 还原上述四个文件到本次变更前版本后，重新执行同一套 `build -> test -> contract/invariant -> hotspot`。
