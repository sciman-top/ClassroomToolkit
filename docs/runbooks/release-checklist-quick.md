# 发布快速清单

## 发布前

- [ ] `git status` 为空
- [ ] `logs/` 已清理
- [ ] `artifacts/` 已清理
- [ ] `tests/.tmp/` 已清理
- [ ] 没有误提交的临时文件
- [ ] `.gitignore` 已覆盖常见生成物

## 内容检查

- [ ] `tests/ClassroomToolkit.Tests/` 保留的是长期测试
- [ ] `tests/Baselines/` 保留的是基线数据
- [ ] `README.md` 说明了三种发布形态
- [ ] `使用指南.md` 可直接给教师使用
- [ ] 标准版不含 runtime
- [ ] 离线版包含 `prereq/`

## 仓库检查

- [ ] 默认分支是 `main`
- [ ] `About` 已设置
- [ ] topics 已设置
- [ ] 本次不需要发版就不创建 Release

## 发布顺序

1. 清理工作区
2. 跑测试
3. 跑发布脚本
4. 检查产物目录
5. 再提交和推送

