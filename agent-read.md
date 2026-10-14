---
title: DeskBox fork 复话交接(AI 入口)
version: "1.0"
updated: 2026-10-14
tags: [deskbox, fork, revival, playbook, ci]
适用场景: 用户要求"重新启用 / 复话 / 继续维护 DeskBox fork"时,从这里开始
---

# agent-read.md — DeskBox fork 复话交接

> **给 AI**:这是本仓库的交接入口。用户说"启用这个项目 / 复话 DeskBox"时:先读完本文,
> 向用户确认复话动机(见流程第 0 步),再按「复话流程」执行;遇到问题先查「踩坑记录」。
> 人类向的封存说明在 [FORK-ARCHIVED.md](FORK-ARCHIVED.md)。

## 1. 这个仓库是什么

- 上游 https://github.com/Tianyu199509/DeskBox:活跃开发的开源 Windows 桌面整理工具(WinUI 3)。
- 本仓库是其 fork,曾维护 4 个补丁并提供自动构建,**2026-10-14 封存**(用户改用其他搜索方案,不再需要补丁)。
- 封存时 main = ba7c70f,此后无维护。构建产物发布在本仓库 Releases,应用内更新器读取 `releases/latest`。

### 补丁清单(复话 = 把它们移植到上游当时的新架构)

| # | 补丁 | 关键提交 | 涉及面 |
|---|---|---|---|
| 1 | 搜索热键"双击 Ctrl"预设(设置项 `searchHotkeyUseDoubleControl`) | `9de586b` `97e73fc` `632afcb` `b8786ea` | SearchSettingsSlice、SearchHotkeyService、ReservedHotkeyHookService 的 DoubleControl 状态机、搜索设置 UI |
| 2 | 搜索弹窗失焦自动隐藏(`HidePopupOnDeactivation`,QuickLook 预览 3 秒内与图标拖拽中抑制) | `f2a65ca` | `src/DeskBox/Views/SearchPopupWindow.xaml.cs` |
| 3 | 应用内更新指向本 fork(`releases/latest`) | `9de586b` | `src/DeskBox/Services/AppUpdateService.cs` |
| 4 | Fork Build 工作流(merge 上游 → 构建安装包 → 发 Releases) | `9de586b` `4947493` | `.github/workflows/fork-build.yml` |

克隆后用 `git show <hash>` 可看每个补丁的完整 diff,补丁必须保持最小。

## 2. 封存时的状态(2026-10-14)

- 用户机器:fork 版已卸载,装的是**官方 v1.5.8**(数据无缝沿用;官方版已把 settings schema 迁到 v14)。
- 本地克隆已删除;仓库保留;**Fork Build 工作流已 `gh workflow disable`(状态 disabled_manually)**。
- 数据备份(含桌面布局 widget-layout.json、settings.json):`D:\AiDev\Project\Github\Deskbox-archive\deskbox-data-backup-20261014\`
- 更详细的封存文档(维护者机器):`D:\AiDev\Project\Github\Deskbox-archive\ARCHIVED-DeskBox-fork.md`
- 应用数据目录 `%LOCALAPPDATA%\DeskBox\data\` 与安装/卸载无关;fork 私有字段(如 `searchHotkeyUseDoubleControl`)官方版会忽略,向后兼容。

## 3. 复话流程

**第 0 步 — 确认动机**(必须先问用户):
- A. 要回"双击 Ctrl 搜索"功能 → 完整复话(移植补丁 + 恢复构建发版);
- B. 只是想跟随上游用最新版 → 不需要这个 fork,直接升级官方版即可;
- C. 上游可能已原生支持类似热键 → 先查上游 changelog,若已支持选 B。

**完整复话(A 路线)**:

1. 重新克隆并挂上游:
   ```bash
   git clone https://github.com/Ci-Tu/DeskBox.git && cd DeskBox
   git remote add upstream https://github.com/Tianyu199509/DeskBox.git
   git fetch upstream main && git log --oneline main..upstream/main   # 评估差距
   ```
2. 合并上游并移植补丁:历史做法是 `git merge upstream/main` 然后解决冲突;
   补丁 1/2 需对照上游当前架构重接(上游重构频繁,先读 `docs/architecture/` 和 `tests/DeskBox.Tests/` 的契约测试理解新结构)。
3. 契约测试冲突按坑 1 的方法校准(不必本地构建)。
4. **递增版本号**:同时改 `installer/DeskBox.iss`(`MyAppVersion`/`MyAppVersionInfo`)和
   `src/DeskBox/DeskBox.csproj`(`Version`/`AssemblyVersion`/`FileVersion`/`InformationalVersion`),
   必须严格大于用户机器当前版本(见坑 5)。
5. 若 Fork Build 仍处 disabled,先恢复:
   ```bash
   gh workflow enable fork-build.yml --repo Ci-Tu/DeskBox
   ```
   然后 push(推送含 workflow 文件改动的提交见坑 4)。Fork Build 在 push 到 main 时自动触发:merge 上游 → 构建 → 发 Release(tag = `v<MyAppVersion>`,资产 `DeskBox_Setup_<版本>_x64.exe` + `.sha256`)。
6. 验证:GitHub CI 全绿(约 9 分钟)→ Releases 出现新安装包 → 用户机器升级 → 设置页版本正确 → 双击 Ctrl 唤起 + 点别的窗口自动隐藏。
7. 仅当用户要求时还原数据:把备份的 `data/` 拷回 `%LOCALAPPDATA%\DeskBox\data\`。

## 4. 踩坑记录(复话必读,均为实测)

1. **设置契约测试结构性冲突**:fork 的 `searchHotkeyUseDoubleControl` 使
   `AppSettingsFacadeNarrowingContractTests` / `SettingsSliceOwnershipContractTests` 的计数断言永远比上游多 1,
   上游每加设置字段必冲突。**预算校准不必本地构建**:用 Python 按测试同款正则对源码复算——
   `AppSettings.cs` 中 `{ get =>` 属性计数(排除 `SchemaVersion` 与 slice 类型)= FacadeProperties/Passthroughs 期望值;
   facade 访问预算用正则 `(?<![A-Za-z])settings\.(?:属性名…)\b`(不区分大小写)逐文件计数后更新 manifest。
2. **不要恢复每日定时同步**(已于 `4947493` 移除):上游活跃,每个设置批次都撞坑 1,定时任务只会天天报错。跟随上游请手动触发。
3. **本地跑不了全量测试**:机器无 VS Build Tools / Windows SDK 时,Rust 原生模块(deskbox_native、thumbnail proxy)链接失败。
   快速编译检查可用 `dotnet build -p:DeskBoxShellThumbnailProxy=false`,但测试工程的原生模块无跳过开关;**全量验证靠 GitHub CI**。`~/.cargo/bin` 有 Rust 1.96(需手动加 PATH)。
4. **凭据与推送**:
   - HTTPS + token 可以推普通文件改动;**含 `.github/workflows/` 改动的推送会被拒**(凭据缺 workflow scope)。
   - workflow 改动走 SSH:`GIT_SSH_COMMAND="ssh -F /d/AiDev/Project/Github/env/ssh_config" git push git@github.com:Ci-Tu/DeskBox.git main`
   - ⚠️ 2026-10-14 起该 SSH key(指纹 `SHA256:beELc4r…`,文件 `env/zcode`)被 GitHub 拒绝——本地密钥有效,疑似账号端被移除。复话时若 SSH 仍失败,先让用户到 GitHub → Settings → SSH keys 检查并重新登记,或改用具备 workflow scope 的新凭据。
   - token 在 `env/github_token.txt`(工作区规则:仅注入当前进程环境,用完清除;严禁写入文件/提交/日志)。
5. **发版必须递增版本号**:`AppUpdateService.IsRemoteVersionNewer` 按版本比较,重建同版本号不会推送给已装用户(教训:2026-10-02 的修复困在 1.5.5 一周没送达)。
6. **文件编码敏感**:`.cs` 带 BOM、`Strings/*.json` 不带 BOM;Windows 上不要用 PowerShell 5 写 UTF-8,编辑后用 `git diff` 校验。

## 5. 环境

- gh CLI:`D:\AiDev\Lib\bin\gh.exe`(已认证);pwsh7:`C:\Program Files\PowerShell\7\pwsh.exe`
- 构建脚本(Fork Build 内部使用):`scripts/build-stage-7c1-distribution.ps1 -Platform x64`
- fork 与官方安装器 AppId 相同(`{5E052824-3456-427E-9759-3BCAE078A1D3}`),官方安装包可直接覆盖安装 fork 版,反之亦然。
