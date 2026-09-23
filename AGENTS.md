# DeskBox agent instructions

These instructions are based on the checked-in project files and workflows. When they disagree with an old handover note, inspect the current project file, script, test, or workflow and follow that source.

## Project shape

- DeskBox is a Windows desktop app targeting .NET 10 and WinUI 3 / Windows App SDK. The supported architectures are x64 and ARM64; keep `Platform` and `RuntimeIdentifier` paired (`x64`/`win-x64`, `ARM64`/`win-arm64`).
- `DeskBox.sln` contains `src/DeskBox` (app), `src/DeskBox.Updater` (updater project, AOT-compatible and published with Native AOT), and `tests/DeskBox.Tests` (xUnit). `App.xaml.cs` is the startup and application-lifecycle composition root; `Services/ServiceRegistry.cs` registers shared core services. Many window-, hook-, and watcher-bound services are instead created and disposed by `App`; follow their owner lifetime rather than registering them as process-wide singletons by default.
- `Services/DeskBoxDataPathService.Current` is the authority for app-owned data, updates, logs, the single-instance mutex/event scope, and recovery location. Production data is under `%LOCALAPPDATA%\DeskBox`; Debug uses `DESKBOX_DEV_DATA_ROOT`, and AOT preview uses `DESKBOX_AOT_PREVIEW_DATA_ROOT`. Route new owned storage through this service rather than inventing per-feature LocalAppData paths.
- Startup handles single-instance activation, startup-task launches, Jump List activation, and native notification activation. These paths share an activation event and, for notification payloads, a persisted envelope handoff; inspect the activation code and its tests before changing startup ordering or shutdown.
- Main areas are `Views`, `ViewModels`, `Services`, `Models`, `Controls`, `Contracts`, `Platform`, and `Sync`. Follow nearby patterns and keep UI, state/coordination, Windows interop, and shared contracts in their existing layers.
- Native Windows functionality is also implemented in the Rust workspace under `native`. The managed/native ABI is declared in `native/include/deskbox_native.h`; when changing an ABI or native behavior, inspect its managed callers, Rust exports, architecture build scripts, and related contract tests together.
- `src/DeskBox/Strings/*.json` contains 12 locale files. Keep resource keys and format placeholders aligned across locales when changing localized strings.

## Change safely

- Read the nearby implementation and tests before editing. For startup/lifecycle, window ownership/z-order, drag/drop, sync, update, or native/AOT behavior, read the relevant architecture document and matching tests. Architecture documents have mixed status (implemented contracts, proposals, and dated reports); inspect their status header and current implementation before treating one as shipped behavior.
- Preserve both regular managed behavior and the conditional Native AOT path. `PublishAot` defines `DESKBOX_NATIVE_AOT`; AOT is architecture-specific and requires the Rust backend. Do not enable the smoke harness in retail publishing or remove AOT-specific compile guards without checking the distribution scripts and contract tests.
- Fork-specific behavior includes the search Double-Control hotkey and update publishing. Double-Control is reserved for search; a legacy main-hotkey Double-Control setting migrates to the saved main chord and enables search on Double-Control. `AppUpdateService` accepts HTTPS downloads from `deskbox.fun` and its subdomains, or from the exact GitHub path `/Ci-Tu/DeskBox/releases/download/`; keep the origin and checksum validation pinned instead of broadening trust to all of `github.com`. Inspect those services and their tests before changing them.
- Cloud backup is implemented separately from cloud sync. The sync protocol document is explicitly marked “contract, not implementation”; this tree has projection and local state/outbox stores, but do not assume the document's server transport/engine is implemented. Verify current wiring before describing or extending sync behavior.
- `src/DeskBox/Services/SettingsSearchCatalog.cs` is generated. If settings XAML changes affect searchable localized settings, update it with `pwsh -File .\scripts\update-settings-search-catalog.ps1` and verify with `-Check`; do not hand-edit the generated file.
- Preserve existing user changes and generated/release artifacts. The distribution script removes its selected output directory, after checking it is under `.artifacts`; inspect the exact output path before running it.
- Do not stop or launch DeskBox automatically after an edit. Only perform app smoke runs when the user asks for them or the task requires them. `scripts/start-debug.ps1` selects the architecture-specific output and uses a per-worktree development data root by default, but it stops an existing DeskBox process whose executable is under this repository unless `-NoStop` is supplied. Report the executable and data root actually used for an authorized run.

## Build and verification

- The SDK is pinned by `global.json` to .NET SDK `10.0.303`; the Rust toolchain is pinned in `rust-toolchain.toml` to `1.96.0` with Windows MSVC targets. Keep NuGet lock files in sync with intentional dependency changes; CI restores in locked mode.
- When the user asks for build/test verification, use the Windows CI shape below from the repository root (adjust `Platform` and RID together for ARM64):

  ```powershell
  dotnet restore .\DeskBox.sln --locked-mode -p:Platform=x64 -p:RuntimeIdentifier=win-x64
  dotnet build .\src\DeskBox\DeskBox.csproj -c Release --no-restore -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -v:minimal
  dotnet test .\tests\DeskBox.Tests\DeskBox.Tests.csproj -c Release --no-restore -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:WindowsAppSdkBootstrapInitialize=false --blame-hang --blame-hang-timeout 5m --logger "trx;LogFileName=test-results.trx" -v:minimal
  ```

- The tests are serialized (`CollectionBehavior(DisableTestParallelization = true)`) and include both behavior tests and source/build contract tests. Use the narrowest relevant test filter when appropriate; use the full CI command for a full verification request.
- The test project currently invokes `powershell.exe` from an MSBuild target to build the Rust test DLL. Running the outer command in PowerShell 7 does not change that child-process choice; do not silently rewrite it. Check both shell versions if that integration fails.
- `scripts/build-stage-7c1-distribution.ps1 -Platform x64` builds/audits the full Direct Native AOT installer and Store distribution and requires the Windows SDK and Inno Setup. It is a release gate, not the routine inner-loop build. ARM64 distribution/runtime gates use native ARM64 GitHub runners.
- If editing settings-search metadata, run the generator's `-Check` mode when the user asks for verification. Do not run unrelated long AOT, packaging, or device smoke suites for a small change.

## CI and repository policy

- `.github/workflows/ci.yml` is the ordinary build/test workflow. `.github/workflows/fork-build.yml` syncs upstream and publishes the fork; `distribution-audit.yml` and `arm64-runtime.yml` are specialized architecture/distribution gates. Check the workflow itself for current triggers and parameters before dispatching or diagnosing it.
- The commit-msg hook and PR CI reject line-anchored `Co-Authored-By`, `Generated with`, and `Generated-by` attribution trailers. Do not add these trailers to commits; on a fresh clone, activate `.githooks` with `git config core.hooksPath .githooks`.
- Do not infer current CI or release state from a previous run or a dated handover. Inspect the live run/release state when access permits and report permission failures as such.
