# 发布前检查清单

适用范围：
- GitHub 版本发布
- 标准版打包
- 离线版打包

## 1. 工作区

- `git status` 为空
- 没有误提交的临时文件
- 没有未纳入 `.gitignore` 的生成物

## 2. 必留内容

- `tests/ClassroomToolkit.Tests/` 下的长期自动化测试代码
- `tests/Baselines/` 下的基线数据
- `scripts/release/` 下的发布脚本和模板
- `docs/` 下的设计、验证、运行手册

## 3. 应清理内容

- `logs/`
- `artifacts/`
- `tests/.tmp/`
- `bin/`
- `obj/`
- 测试输出目录
- 一次性调试文件
- 临时缓存目录

## 4. 忽略规则

确认 `.gitignore` 已覆盖：

- `bin/`
- `obj/`
- `artifacts/`
- `logs/`
- `**/TestResults/`
- `**/.tmp/`
- `**/.cache/`
- `**/.temp/`

## 5. 文档检查

- `README.md` 已说明三种发布形态
- `使用指南.md` 可直接给教师使用
- 离线版说明已写清 runtime 捆绑方式
- 标准版说明已写清 runtime 不随包分发

## 6. 发布脚本检查

- 标准版和离线版输出目录分开
- 标准版不包含 `.NET Desktop Runtime 10 x64`
- 离线版包含 `prereq/`
- 离线版运行时安装包可自动下载或复制

## 7. GitHub 检查

- 默认分支是 `main`
- `About` 描述已设置
- topics 已设置
- 不需要发版时不创建 Release

## 8. 推荐顺序

1. 清理工作区
2. 跑测试
3. 跑发布脚本
4. 检查产物目录
5. 检查文档和仓库元数据
6. 再决定是否提交和推送

