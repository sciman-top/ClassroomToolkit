# 变更证据：students.xlsx / student_photos 自动创建与自修复

- 日期：2026-03-31
- 规则 ID：R1/R2/R6/R8，C.2/C.5
- 风险等级：中（涉及名册模板与持久化规范化）

## 目标
- 当运行目录缺少 `students.xlsx` 或 `student_photos` 时自动创建。
- 自动确保并修复 `_ROLL_STATE` 工作表。
- 初始模板固定为：
  - 工作表：`1班`、`_ROLL_STATE`
  - 列：`学号`、`姓名`、`分组`、`__row_id__`
  - 样例：`01 张三 A`、`02 李四 B`、`03 王五 C`
  - 固定列宽（学号/姓名/分组/row_id）
- 当已有 `students.xlsx` 列数、顺序、结构不合规时自动规范化并尽力回写。
- `student_photos` 下自动确保 `1班` 子目录存在。

## 改动文件
- `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
- `src/ClassroomToolkit.App/Helpers/StudentResourceLocator.cs`
- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.cs`
- `tests/ClassroomToolkit.Tests/StudentWorkbookStoreTests.cs`
- `tests/ClassroomToolkit.Tests/StudentResourceLocatorTests.cs`

## 关键实现
- `StudentWorkbookStore.LoadOrCreate`：
  - 缺失文件时按新模板创建并落盘。
  - 读取已有文件时，自动校正：
    - 缺失/错误 `_ROLL_STATE` 表头
    - 列顺序与核心列集合
    - 缺失 `__row_id__`
    - 行内 `班级` 列冗余映射（按工作表名归一）
  - 自愈写回失败（文件占用等）时不阻断运行，保留内存态可用。
- `StudentWorkbookStore.Save`：
  - 无论是否有状态 JSON，始终写入 `_ROLL_STATE` 工作表并写入规范 JSON（空状态也生成）。
  - 核心列固定列宽，不再依赖 `AdjustToContents`。
- `StudentResourceLocator.ResolveStudentPhotoRoot`：
  - 自动创建 `student_photos` 与默认子目录 `1班`。
- `RollCallViewModel`：
  - 照片根目录从硬编码相对路径切换为 `StudentResourceLocator.ResolveStudentPhotoRoot()`。

## 执行命令与结果
1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StudentWorkbookStoreTests|FullyQualifiedName~StudentResourceLocatorTests"`  
   - 初次失败：定位到回退模板时文件占用导致回写异常。  
   - 修复后重跑通过（11 passed）。
2. `dotnet build ClassroomToolkit.sln -c Debug`  
   - 通过
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
   - 通过（3025 passed）
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`  
   - 通过（24 passed）
5. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`  
   - 通过（PASS）

## 回滚动作
1. 回滚文件：
   - `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
   - `src/ClassroomToolkit.App/Helpers/StudentResourceLocator.cs`
   - `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.cs`
   - `tests/ClassroomToolkit.Tests/StudentWorkbookStoreTests.cs`
   - `tests/ClassroomToolkit.Tests/StudentResourceLocatorTests.cs`
2. 复验门禁：
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
