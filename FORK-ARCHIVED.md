# FORK-ARCHIVED.md — 本 fork 已封存

> **状态:🔒 已封存(2026-10-14)。** 本仓库是上游 [Tianyu199509/DeskBox](https://github.com/Tianyu199509/DeskBox) 的个人 fork,
> 曾用于维护"双击 Ctrl 唤起搜索"补丁并提供自动构建。该功能已不再需要,仓库停止维护、不再跟随上游。
> 想使用 DeskBox 请直接安装[官方版本](https://github.com/Tianyu199509/DeskBox/releases/latest)。

## 本 fork 曾经包含的补丁

| 补丁 | 内容 | 主要提交 |
|---|---|---|
| 双击 Ctrl 搜索预设 | 设置 → 搜索新增"双击 Ctrl"热键预设(`searchHotkeyUseDoubleControl`),复用 `ReservedHotkeyHookService` 的 DoubleControl 状态机 | `9de586b` `97e73fc` `632afcb` |
| 弹窗失焦自动隐藏 | 热键唤起的搜索弹窗在点击其他窗口/桌面时自动隐藏(QuickLook 预览与图标拖拽期间抑制) | `f2a65ca` |
| 更新指向 fork | 应用内更新器改为读取本 fork 的 releases/latest | `9de586b` |
| Fork Build 工作流 | 手动/push 触发:merge 上游 → 构建安装包 → 发布到本 fork Releases(每日定时已于 2026-10-07 移除) | `9de586b` `4947493` |

## 为什么封存

- fork 补丁固定使设置契约测试的计数断言比上游多 1(`searchHotkeyUseDoubleControl`),
  上游每新增设置字段必然产生合并冲突,持续维护成本高;
- 维护者已改用其他搜索方案,补丁价值消失。

## 维护期踩坑记录(供同类 fork 维护者参考)

1. **设置契约测试冲突是结构性的**:`AppSettingsFacadeNarrowingContractTests` / `SettingsSliceOwnershipContractTests`
   冻结了设置字段数量与 facade 访问预算,fork 私有字段会让所有计数比上游多 1。校准预算不必本地构建,
   用 Python 按测试同款正则对源码复算即可。
2. **完整构建依赖 MSVC + Windows SDK**:Rust 原生模块(deskbox_native、thumbnail proxy)在没有
   VS Build Tools 的机器上无法链接;全量测试验证依赖 GitHub CI。
3. **含 workflow 改动的提交需要 workflow scope 的凭据**,否则 push 会被 remote rejected。
4. **重新构建发版必须递增版本号**:应用内更新器按版本号比较,重建同版本号不会推送给已装用户。

## 数据与安装说明(维护者本人)

- 数据目录 `%LOCALAPPDATA%\DeskBox\data\` 与安装目录无关,卸载/重装/换官方版均无缝沿用;
- 本 fork 安装器与官方安装器 AppId 相同(`{5E052824-3456-427E-9759-3BCAE078A1D3}`),官方安装包可直接覆盖安装;
- 本地数据备份与封存文档(更详细)在维护者机器 `D:\AiDev\Project\Github\Deskbox-archive\`。

## 复话指南

若未来想恢复维护:`git fetch upstream` 查看差距 → 按上表补丁清单对照上游新架构重新移植 →
本地解决契约测试冲突(参考坑 1)→ push 触发 Fork Build。上游演进较大时建议对照
`docs/architecture/` 与 `tests/DeskBox.Tests/` 的契约测试理解新结构。
