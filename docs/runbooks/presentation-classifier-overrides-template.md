# PPT/WPS 识别规则覆盖模板与回滚（ClassroomToolkit）

最后更新：2026-03-18  
适用范围：PPT/WPS 新版本或定制壳导致放映窗口识别不稳定

## 1. 何时使用

- 现象：放映已全屏，但工具未识别为演示窗口，翻页控制失效或不稳定。
- 前置：已确认权限级别一致、未被安全软件拦截、基础运行环境正常。

## 2. 采集输入（建议先做）

在“系统兼容性诊断”中记录：

1. 前台窗口进程名
2. 前台窗口类名
3. 演示窗口判定与评分

优先从真实故障场景采集 2-3 组样本，再写覆盖。

## 3. 覆盖 JSON 模板

将以下 JSON 作为 `presentation_classifier_overrides_json` 的模板（按需删减）：

```json
{
  "AdditionalWpsClassTokens": [
    "YourWpsSlideShowClass"
  ],
  "AdditionalOfficeClassTokens": [
    "YourOfficeSlideShowClass"
  ],
  "AdditionalSlideshowClassTokens": [
    "YourSharedSlideShowClass"
  ],
  "AdditionalWpsProcessTokens": [
    "your_wps_process"
  ],
  "AdditionalOfficeProcessTokens": [
    "your_office_process"
  ],
  "ClassMatchWeight": 10,
  "ProcessMatchWeight": 3,
  "NoCaptionWeight": 1,
  "IsFullscreenWeight": 2,
  "FullscreenClassMatchBonus": 100,
  "RequireClassMatchOrFullscreen": true,
  "MinimumCandidateScore": 1
}
```

## 4. 配置原则

1. 先加 token，后调权重。
2. 每次只做一类小变更（例如只加 `AdditionalOfficeClassTokens`）。
3. 保留默认评分策略，除非有明确误判证据。
4. 覆盖项尽量短且稳定，避免匹配过宽导致误识别。

## 5. 生效与验证

1. 保存设置后重启应用。
2. 复测场景：
   - 进入全屏放映
   - 前进/后退翻页
   - 前后台切换后再翻页
3. 在“系统兼容性诊断”确认：
   - 能检测到演示窗口
   - 判定类型正确（Wps/Office）
   - 评分不低于最小阈值

## 6. 回滚步骤

### 6.1 推荐回滚（UI）

1. 打开设置面板；
2. 勾选“清空演示识别规则覆盖（本次保存生效）”；
3. 保存并重启应用。

### 6.2 文件回滚（配置文件）

1. 先备份 `settings.json` 或 `settings.ini`；
2. 将以下键恢复为空：
   - `presentation_classifier_overrides_json`
   - `presentation_classifier_last_learn_utc`
   - `presentation_classifier_last_learn_detail`
   - `presentation_classifier_recent_learn_records_json`
3. 重启应用并复测。

## 7. 问题收敛顺序（固定）

1. 权限级别一致性（应用与 PPT/WPS 同级）  
2. 安全软件拦截排查（Hook/输入注入）  
3. token 覆盖补充  
4. 必要时再调评分参数  

## 8. 变更记录建议

每次覆盖调整建议记录：

- 触发版本（PPT/WPS 版本号）
- 新增 token
- 调整前后诊断输出
- 回滚点（配置备份文件名）
