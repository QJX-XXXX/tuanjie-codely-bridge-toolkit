# Tuanjie + Codely Skill Suite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有单体 `tuanjie-codely-bridge` 重构为五个可独立安装、可自动路由且能安全操作团结 Editor 的 Tuanjie + Codely Skills。

**Architecture:** 新增 `tuanjie-workflows` 作为纯路由入口，保留并收窄 `tuanjie-codely-bridge` 的连接职责，再新增 Editor automation、包管理和 Bridge 自定义工具三个专项 Skill。每个专项 Skill 都自带最小安全闸门，详细流程放入自身 `references/`，不依赖跨 Skill 的相对文件。

**Tech Stack:** Markdown Skills、YAML Agent 元数据、Codex skill-creator 验证脚本、PowerShell 本地评测、Git、CodelyCLI/tuanjie MCP 只读验收。

**Spec:** `docs/design/2026-08-21-tuanjie-codely-skill-suite.md`

## Global Constraints

- 只支持团结 Editor 项目；Unity 官方 Editor 必须停止 Tuanjie 路由。
- 保留公开名称和路径 `skills/tuanjie-codely-bridge`，不改变 EditorWindow 包名 `cn.qjx.codex-codely-setup`。
- 不复制 Unity Technologies Skill 的正文、命令或 Unity 6、Unity Pipeline、UGS 平台假设。
- 只调用当前会话实际暴露的 `tuanjie` MCP 工具和参数，不编造工具能力。
- 不自动安装或替换团结 Editor、Codely Bridge、CodelyCLI、Node.js 或 Codex。
- 不读取、输出或提交 token、端口、descriptor、临时认证信息或真实用户凭据。
- 不提交 `tests/`、`editor-package/Tests/`、`docs/superpowers/`、本地评测产物或具体业务项目内容。
- 所有本地评测文件写入已忽略的 `artifacts/`。
- 修改仓库文件使用 `apply_patch`；保留用户已有和无关改动。
- 每个 Skill 目录名必须等于 frontmatter `name`，并包含独立 `agents/openai.yaml`。

---

### Task 1: 建立本地 Skill 套件评测基线

**Files:**
- Create locally, do not commit: `artifacts/skill-suite-evals.ps1`
- Read: `docs/design/2026-08-21-tuanjie-codely-skill-suite.md`
- Read: `skills/tuanjie-codely-bridge/SKILL.md`

**Interfaces:**
- Consumes: 设计文档中的五个 Skill 名称、共用安全闸门和不提交测试约束。
- Produces: `artifacts/skill-suite-evals.ps1`，退出码 `0` 表示套件结构、frontmatter、关键路由、文档清单和敏感内容扫描全部通过；退出码 `1` 表示至少一项失败。

- [ ] **Step 1: 编写当前实现必然失败的本地评测脚本**

使用 `apply_patch` 创建下列脚本；它只读仓库文件，不写项目或 Editor：

```powershell
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$skills = @(
    'tuanjie-workflows',
    'tuanjie-codely-bridge',
    'tuanjie-editor-automation',
    'tuanjie-package-management',
    'tuanjie-codely-custom-tools'
)
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($name in $skills) {
    $root = Join-Path $repo "skills/$name"
    $skillFile = Join-Path $root 'SKILL.md'
    $agentFile = Join-Path $root 'agents/openai.yaml'
    if (-not (Test-Path -LiteralPath $skillFile)) {
        $failures.Add("missing Skill: $name")
        continue
    }
    if (-not (Test-Path -LiteralPath $agentFile)) {
        $failures.Add("missing metadata: $name")
    }
    $text = Get-Content -Raw -LiteralPath $skillFile
    if ($text -notmatch "(?m)^name:\s+$([regex]::Escape($name))$") {
        $failures.Add("frontmatter name mismatch: $name")
    }
    if ($text -notmatch 'Unity 官方') {
        $failures.Add("missing Unity boundary: $name")
    }
}

$routerPath = Join-Path $repo 'skills/tuanjie-workflows/SKILL.md'
if (Test-Path -LiteralPath $routerPath) {
    $router = Get-Content -Raw -LiteralPath $routerPath
    foreach ($target in $skills[1..4]) {
        if ($router -notmatch [regex]::Escape($target)) {
            $failures.Add("router target missing: $target")
        }
    }
}

$bridgePath = Join-Path $repo 'skills/tuanjie-codely-bridge/SKILL.md'
if (Test-Path -LiteralPath $bridgePath) {
    $bridge = Get-Content -Raw -LiteralPath $bridgePath
    if ($bridge -notmatch '项目根') { $failures.Add('bridge root check missing') }
    if ($bridge -notmatch 'config\.toml') { $failures.Add('bridge config workflow missing') }
}

$editorPath = Join-Path $repo 'skills/tuanjie-editor-automation/SKILL.md'
if (Test-Path -LiteralPath $editorPath) {
    $editor = Get-Content -Raw -LiteralPath $editorPath
    foreach ($term in @('重新读取', 'Domain Reload', '重复组件', '保存')) {
        if ($editor -notmatch [regex]::Escape($term)) {
            $failures.Add("editor contract missing: $term")
        }
    }
}

$packagePath = Join-Path $repo 'skills/tuanjie-package-management/SKILL.md'
if (Test-Path -LiteralPath $packagePath) {
    $package = Get-Content -Raw -LiteralPath $packagePath
    if ($package -notmatch 'packages-lock\.json') { $failures.Add('package lock boundary missing') }
    if ($package -notmatch '实际解析版本') { $failures.Add('resolved version verification missing') }
}

$customPath = Join-Path $repo 'skills/tuanjie-codely-custom-tools/SKILL.md'
if (Test-Path -LiteralPath $customPath) {
    $custom = Get-Content -Raw -LiteralPath $customPath
    if ($custom -notmatch 'schema') { $failures.Add('custom tool schema proof missing') }
    if ($custom -notmatch '未暴露') { $failures.Add('custom tool refusal missing') }
}

$tracked = git -C $repo ls-files
$forbiddenTracked = $tracked | Where-Object {
    $_ -match '^(tests/|editor-package/Tests/|docs/superpowers/|artifacts/)'
}
if ($forbiddenTracked) {
    $failures.Add('forbidden tracked paths: ' + ($forbiddenTracked -join ', '))
}

$scanFiles = $tracked | Where-Object { $_ -match '\.(md|yaml|yml|toml|ps1|psm1|cs|json)$' }
foreach ($relative in $scanFiles) {
    $path = Join-Path $repo $relative
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $text = Get-Content -Raw -LiteralPath $path
    if ($text -match '2DTower Defense|TowerDefenseDemo|SampleScene') {
        $failures.Add("business project content: $relative")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Skill suite evaluations passed.'
```

- [ ] **Step 2: 运行基线并确认因为四个新 Skill 不存在而失败**

Run:

```powershell
& .\artifacts\skill-suite-evals.ps1
```

Expected: exit code `1`，至少包含 `missing Skill: tuanjie-workflows`、`missing Skill: tuanjie-editor-automation`、`missing Skill: tuanjie-package-management` 和 `missing Skill: tuanjie-codely-custom-tools`。

- [ ] **Step 3: 确认评测文件未进入 Git 跟踪范围**

Run:

```powershell
git status --short --ignored artifacts
git check-ignore -v artifacts/skill-suite-evals.ps1
```

Expected: 文件显示为 ignored，`git status --short` 的正常未跟踪列表中不包含它。

- [ ] **Step 4: 不提交本任务产物**

本任务只建立红灯基线。不要 `git add -f artifacts`，也不要提交测试脚本。

### Task 2: 新增 `tuanjie-workflows` 路由 Skill

**Files:**
- Create: `skills/tuanjie-workflows/SKILL.md`
- Create: `skills/tuanjie-workflows/agents/openai.yaml`
- Test locally: `artifacts/skill-suite-evals.ps1`

**Interfaces:**
- Consumes: 工作区路径、项目标识、用户意图，以及四个专项 Skill 的稳定名称。
- Produces: 路由目标 `tuanjie-codely-bridge`、`tuanjie-editor-automation`、`tuanjie-package-management` 或 `tuanjie-codely-custom-tools`；没有合适目标时明确停止或使用文件级工具。

- [ ] **Step 1: 用四个真实提示做无 Skill 基线记录**

在 `artifacts/router-baseline.md` 记录未加载新路由 Skill 时 Agent 对以下提示的选择和缺陷，不提交该文件：

```text
1. “检查当前团结项目为什么连接不上 Codely Bridge。”
2. “给当前 Scene 中已存在的对象增加组件并保存。”
3. “把当前项目的某个 UPM 包升级到已确认版本。”
4. “为重复的 Editor 操作创建一个 Codely Bridge 自定义工具。”
```

Expected: 至少一个场景无法稳定选择设计中的专项 Skill，因为这些 Skill 尚不存在。

- [ ] **Step 2: 创建路由 Skill 正文**

`skills/tuanjie-workflows/SKILL.md` 必须包含以下 frontmatter：

```yaml
---
name: tuanjie-workflows
description: Use when a Tuanjie Editor project needs routing among Codely Bridge setup, Editor automation, package management, or verified Bridge custom tools; do not use for Unity official Editor projects.
---
```

正文按顺序写明：项目识别、路由表、专项 Skill 未安装时的处理、纯文件任务的直接处理、Unity 官方项目停止条件、共同完成报告。路由表必须使用四个准确 Skill 名称，不复制专项流程，不承诺不存在的 MCP 工具。

- [ ] **Step 3: 创建 Codex 展示元数据**

`skills/tuanjie-workflows/agents/openai.yaml` 使用：

```yaml
interface:
  display_name: "Tuanjie Workflows"
  short_description: "Route Tuanjie Editor work to the right Codely skill"
  default_prompt: "Use $tuanjie-workflows to identify this Tuanjie task and route it to the correct verified workflow."
policy:
  allow_implicit_invocation: true
```

路由 Skill 不声明 MCP 依赖，因为纯文件任务和连接修复阶段可能尚无 `tuanjie` MCP。

- [ ] **Step 4: 验证新 Skill 结构与路由内容**

Run:

```powershell
python -X utf8 C:\Users\QJX\.codex\skills\.system\skill-creator\scripts\quick_validate.py skills/tuanjie-workflows
& .\artifacts\skill-suite-evals.ps1
```

Expected: `quick_validate.py` PASS；套件评测仍因另外三个专项 Skill 缺失而失败，但不再报告 `tuanjie-workflows` 或 router target 缺失。

- [ ] **Step 5: 提交路由 Skill**

```powershell
git add skills/tuanjie-workflows
git commit -m "feat: add Tuanjie workflow router skill"
```

### Task 3: 收窄并强化 `tuanjie-codely-bridge`

**Files:**
- Modify: `skills/tuanjie-codely-bridge/SKILL.md`
- Modify: `skills/tuanjie-codely-bridge/agents/openai.yaml`
- Modify: `skills/tuanjie-codely-bridge/references/codely-integration.md`
- Create: `skills/tuanjie-codely-bridge/references/setup-and-config.md`
- Create: `skills/tuanjie-codely-bridge/references/connection-diagnostics.md`
- Delete: `skills/tuanjie-codely-bridge/references/tool-routing.md`
- Delete: `skills/tuanjie-codely-bridge/references/verification-workflows.md`
- Test locally: `artifacts/skill-suite-evals.ps1`

**Interfaces:**
- Consumes: 规范化项目路径、团结标识、CodelyCLI 查找来源、项目 `.codex/config.toml`、Codex MCP 注册信息。
- Produces: 连接层状态报告，以及已经核对项目根的可用/不可用结论；对象修改任务委派给 `tuanjie-editor-automation`。

- [ ] **Step 1: 写连接诊断的压力场景**

在 `artifacts/bridge-scenarios.md` 记录以下期望，不提交：

```text
- config.toml 已注册 tuanjie，但 Editor 未打开：只能报告注册成功，不能声称实际连接成功。
- MCP 报告项目根与工作区不同：禁止任何 Editor 写入并报告两者的非敏感规范化路径。
- PATH 找不到 codely.cmd，但 CODELY_CLI_PATH 指向绝对文件：运行 --version 后接受该路径。
- 用户要求输出 descriptor 或端口排查：拒绝输出敏感连接内容，改做非敏感分层诊断。
```

- [ ] **Step 2: 重写主 Skill 为连接层**

更新 description 为：

```yaml
description: Use when a Tuanjie Editor project needs Codely Bridge, CodelyCLI, Codex MCP configuration, connection diagnostics, or read-only connection acceptance; do not use for Unity official Editor projects.
```

正文只保留：宿主与生效、入口闸门、CLI 定位、项目级配置、分层诊断、只读验收、向 `tuanjie-editor-automation`/`tuanjie-package-management`/`tuanjie-codely-custom-tools` 的明确委派。移除 Scene/Prefab 写入契约的详细说明。

- [ ] **Step 3: 拆分连接参考资料**

`setup-and-config.md` 写明：

```text
规范化项目路径 → 从 EditorPrefs/CODELY_CLI_PATH/PATH/用户路径定位 CLI
→ 运行 --version → 预览 config 合并 → 必要时备份
→ 只更新 [mcp_servers.tuanjie] → 重读配置 → codex mcp list
```

它必须强调 `codex mcp list` 只证明注册，不证明 Editor 实际连接；只修改目标 table，保留其他 MCP 配置。

`connection-diagnostics.md` 使用固定层级：项目标识 → Editor 进程/版本 → Bridge 随 Editor 加载前提 → CLI 路径/版本 → MCP 注册 → 首次只读调用 → 项目根比较。每层分别列出可证明的状态和不能据此声称的状态。

`codely-integration.md` 只保留 Codex/Codely Skills 宿主路径、reload 与安装边界；把自定义工具开发内容移交给新 Skill。

- [ ] **Step 4: 更新元数据并移除失效参考**

将 `agents/openai.yaml` 的短描述改为 `Set up and diagnose Codex connections to Tuanjie Editor`，默认提示只要求验证项目和连接。删除两个已被专项 Skill 取代的旧 reference，并确认主 Skill 没有死链。

- [ ] **Step 5: 验证连接 Skill**

Run:

```powershell
python -X utf8 C:\Users\QJX\.codex\skills\.system\skill-creator\scripts\quick_validate.py skills/tuanjie-codely-bridge
rg -n "tool-routing|verification-workflows" skills/tuanjie-codely-bridge
& .\artifacts\skill-suite-evals.ps1
```

Expected: quick validation PASS；`rg` 无结果；套件评测只因未实现的三个专项 Skill 失败。

- [ ] **Step 6: 提交连接 Skill 重构**

```powershell
git add skills/tuanjie-codely-bridge
git commit -m "refactor: focus Tuanjie bridge skill on connection setup"
```

### Task 4: 新增 `tuanjie-editor-automation`

**Files:**
- Create: `skills/tuanjie-editor-automation/SKILL.md`
- Create: `skills/tuanjie-editor-automation/agents/openai.yaml`
- Create: `skills/tuanjie-editor-automation/references/object-workflows.md`
- Create: `skills/tuanjie-editor-automation/references/script-compile-workflows.md`
- Create: `skills/tuanjie-editor-automation/references/failure-recovery.md`
- Test locally: `artifacts/skill-suite-evals.ps1`

**Interfaces:**
- Consumes: 已核对的工作区根、当前 `tuanjie` MCP schema、目标 Scene/Prefab/GameObject/资源、用户授权范围。
- Produces: 最小对象修改及重新读取证据；若修改代码，还产生编译、Domain Reload 和 Console 验证结论。

- [ ] **Step 1: 写 Editor automation 的失败优先场景**

在 `artifacts/editor-automation-scenarios.md` 写入并记录无新 Skill 时的响应：

```text
- 新 C# 类型文件已写入但 Editor 仍在编译，用户要求立即 AddComponent。
- 创建组件的 MCP 调用超时，重新读取后发现组件其实已经存在。
- Prefab Apply 返回失败，无法确定前一次是否部分成功。
- MCP 连接的是另一个同名项目目录。
```

Expected: 基线至少有一个响应遗漏“先重读再决定重试”或“编译失败停止对象操作”。

- [ ] **Step 2: 创建主 Skill**

使用 frontmatter：

```yaml
---
name: tuanjie-editor-automation
description: Use when a Tuanjie Editor project needs safe scene, prefab, GameObject, component, asset, or script-to-Editor automation through the verified tuanjie MCP; do not use for Unity official Editor projects.
---
```

正文包含：适用任务、六项入口闸门、文件级工具与 MCP 分工、对象写入契约、脚本编译闸门、保存与重新读取、完成报告。明确声明只从当前 schema 选择工具，不写任何假定工具名。

- [ ] **Step 3: 创建三个专项 reference**

`object-workflows.md` 分别给出 Scene/GameObject/组件、Prefab、ScriptableObject/材质/Importer 的读取 → 最小动作 → 重读 → 保存 → 再读清单，并要求检查重复对象、重复组件、序列化引用和 Prefab Override。

`script-compile-workflows.md` 固定顺序为：文件修改 → 资源刷新 → 等待编译/Domain Reload → 读取本次 Console 错误 → 类型可用性确认 → 对象操作 → 重读/保存。有编译错误时停止。

`failure-recovery.md` 定义：先判断部分成功；幂等操作同参数重试一次；破坏性操作不盲重试；`tuanjie` MCP 失败不默认切 Unity MCP；保留安全文件修改但标记 Editor 验证未完成。

- [ ] **Step 4: 创建 Agent 元数据**

```yaml
interface:
  display_name: "Tuanjie Editor Automation"
  short_description: "Safely automate Tuanjie scenes, prefabs, assets, and scripts"
  default_prompt: "Use $tuanjie-editor-automation to inspect, modify, re-read, save, and verify this Tuanjie Editor task."
dependencies:
  tools:
    - type: "mcp"
      value: "tuanjie"
      description: "CodelyCLI MCP server connected to the target Tuanjie Editor project"
policy:
  allow_implicit_invocation: true
```

- [ ] **Step 5: 验证 Editor automation Skill**

Run:

```powershell
python -X utf8 C:\Users\QJX\.codex\skills\.system\skill-creator\scripts\quick_validate.py skills/tuanjie-editor-automation
& .\artifacts\skill-suite-evals.ps1
```

Expected: quick validation PASS；套件评测只剩包管理与自定义工具 Skill 缺失。

- [ ] **Step 6: 提交 Editor automation Skill**

```powershell
git add skills/tuanjie-editor-automation
git commit -m "feat: add Tuanjie Editor automation skill"
```

### Task 5: 新增 `tuanjie-package-management`

**Files:**
- Create: `skills/tuanjie-package-management/SKILL.md`
- Create: `skills/tuanjie-package-management/agents/openai.yaml`
- Create: `skills/tuanjie-package-management/references/package-workflows.md`
- Test locally: `artifacts/skill-suite-evals.ps1`

**Interfaces:**
- Consumes: 当前 manifest、Editor/Bridge schema 中实际存在的包管理能力、准确包名/来源/版本和用户授权。
- Produces: 包变更请求结果、资源导入/编译状态，以及 manifest 和实际解析版本的重新读取证据。

- [ ] **Step 1: 记录包管理压力场景**

在 `artifacts/package-scenarios.md` 记录以下期望：

```text
- 用户只说“安装最新版”：先发现可用版本并确认兼容性，不猜版本。
- Editor 包管理工具不可用：默认停止，不直接编辑 manifest。
- 用户明确授权安全合并 manifest：先说明影响与备份，只改目标依赖，不改 packages-lock.json。
- 包请求成功但 Editor 仍在解析：等待稳定后再读取实际解析版本。
```

- [ ] **Step 2: 创建包管理 Skill**

使用 frontmatter：

```yaml
---
name: tuanjie-package-management
description: Use when a Tuanjie Editor project needs package discovery, installation, upgrade, removal, or resolved-version verification through verified Editor or Codely Bridge capabilities; do not use for Unity official Editor projects.
---
```

正文必须区分读取/建议与实际修改；实际修改前确认包名、来源、版本、兼容性和授权。默认优先 Editor 包管理能力，明确无授权时禁止手改 manifest，始终禁止手工改 `packages-lock.json`。

- [ ] **Step 3: 创建包工作流 reference**

`package-workflows.md` 包含四条完整工作流：查询、安装、升级、移除。每条都以读取当前依赖开始，以等待解析/编译、重读 manifest、核对实际解析版本和 Console 结束。Git URL 包要保留现有依赖并报告来源；版本未知时先发现再确认。

- [ ] **Step 4: 创建 Agent 元数据**

```yaml
interface:
  display_name: "Tuanjie Package Management"
  short_description: "Manage Tuanjie packages and verify resolved versions safely"
  default_prompt: "Use $tuanjie-package-management to inspect and safely apply this Tuanjie package change, then verify the resolved version."
dependencies:
  tools:
    - type: "mcp"
      value: "tuanjie"
      description: "CodelyCLI MCP server connected to the target Tuanjie Editor project"
policy:
  allow_implicit_invocation: true
```

- [ ] **Step 5: 验证包管理 Skill**

Run:

```powershell
python -X utf8 C:\Users\QJX\.codex\skills\.system\skill-creator\scripts\quick_validate.py skills/tuanjie-package-management
& .\artifacts\skill-suite-evals.ps1
```

Expected: quick validation PASS；套件评测只剩自定义工具 Skill 缺失。

- [ ] **Step 6: 提交包管理 Skill**

```powershell
git add skills/tuanjie-package-management
git commit -m "feat: add Tuanjie package management skill"
```

### Task 6: 新增 `tuanjie-codely-custom-tools`

**Files:**
- Create: `skills/tuanjie-codely-custom-tools/SKILL.md`
- Create: `skills/tuanjie-codely-custom-tools/agents/openai.yaml`
- Create: `skills/tuanjie-codely-custom-tools/references/custom-tool-contract.md`
- Test locally: `artifacts/skill-suite-evals.ps1`

**Interfaces:**
- Consumes: 当前 Bridge 版本、官方或本地程序集可验证的自定义工具 API、项目现有 Editor 代码边界和当前 MCP schema。
- Produces: 经过编译和 schema 发现证明的项目级自定义工具，或因 API/注册无法验证而安全停止的报告。

- [ ] **Step 1: 建立自定义工具基线场景**

在 `artifacts/custom-tool-scenarios.md` 记录：

```text
- 文档示例中出现工具名，但当前 MCP schema 没有：禁止调用。
- 当前 Bridge 版本的 attribute 或参数签名无法从文档/程序集确认：不生成 C# 模板。
- 工具编译成功但 schema 未出现：报告注册未完成，不声称可用。
- 工具调用修改 Scene：仍需重读、检查重复、保存和再读。
```

- [ ] **Step 2: 核对当前官方文档与本地 Bridge API**

读取官方 Codely Bridge 自定义文档和目标团结项目中已安装 Bridge 包可公开检查的程序集/示例。只记录类型名、attribute、参数容器和返回约定等公开 API，不读取 descriptor、端口或认证信息。若两者无法确认一致，不创建仓库级 C# 模板，并在 reference 中明确“先检查当前版本 API”。

- [ ] **Step 3: 创建自定义工具 Skill**

使用 frontmatter：

```yaml
---
name: tuanjie-codely-custom-tools
description: Use when a Tuanjie Editor project needs a Codely Bridge custom tool designed, registered, discovered, documented, or safely invoked through the current tuanjie MCP schema; do not use for Unity official Editor projects.
---
```

正文覆盖：何时应创建工具、何时直接使用现有 MCP、API 事实核对、项目 Editor 目录选择、编译/重载、schema 发现、调用验证、为项目工具编写配套 Skill。禁止用文档或历史对话替代 schema 证明。

- [ ] **Step 4: 创建工具契约 reference**

`custom-tool-contract.md` 定义以下检查清单：

```text
单一职责和准确名称
→ 当前 Bridge API 可验证
→ 显式输入和返回结构
→ 真实边界输入校验与中文错误
→ 放入现有 Editor 程序集边界
→ 编译与 Domain Reload 成功
→ 当前 MCP schema 出现准确名称和参数
→ 最小调用
→ 重读、保存、再读
→ 项目级 Skill 记录准确名称、schema 和返回字段
```

同时说明高风险批量删除、覆盖、Apply、资源重建必须单独取得用户授权。

- [ ] **Step 5: 创建 Agent 元数据**

```yaml
interface:
  display_name: "Tuanjie Codely Custom Tools"
  short_description: "Build and verify project-specific Codely Bridge tools"
  default_prompt: "Use $tuanjie-codely-custom-tools to design or verify this project-specific Bridge tool against the current Tuanjie MCP schema."
dependencies:
  tools:
    - type: "mcp"
      value: "tuanjie"
      description: "CodelyCLI MCP server used to discover and invoke verified Bridge custom tools"
policy:
  allow_implicit_invocation: true
```

- [ ] **Step 6: 验证自定义工具 Skill 和完整套件结构**

Run:

```powershell
python -X utf8 C:\Users\QJX\.codex\skills\.system\skill-creator\scripts\quick_validate.py skills/tuanjie-codely-custom-tools
& .\artifacts\skill-suite-evals.ps1
```

Expected: quick validation PASS；首次获得 `Skill suite evaluations passed.` 和 exit code `0`。

- [ ] **Step 7: 提交自定义工具 Skill**

```powershell
git add skills/tuanjie-codely-custom-tools
git commit -m "feat: add Codely Bridge custom tools skill"
```

### Task 7: 更新安装、架构与使用文档

**Files:**
- Modify: `README.md`
- Modify: `docs/setup-guide.md`
- Modify: `docs/architecture.md`
- Modify: `docs/troubleshooting.md`
- Modify: `templates/AGENTS.tuanjie-snippet.md`
- Test locally: `artifacts/skill-suite-evals.ps1`

**Interfaces:**
- Consumes: 五个实际存在的 Skill 子路径、原 EditorWindow UPM URL 和现有项目接入流程。
- Produces: 无需克隆仓库的整套安装提示、单 Skill 安装说明、任务路由示例和与套件一致的 AGENTS 规则片段。

- [ ] **Step 1: 在 README 增加 Skills 套件入口**

在“链路”之后增加“Skills 套件”表格，逐项列出五个 Skill 的职责和 GitHub 子路径。明确推荐全部安装，允许只安装专项 Skill，并说明路由入口未安装专项 Skill 时只报告缺失能力，不自行模拟。

README 的 Agent 提示区增加四个自然语言例子：连接诊断、Scene/Prefab 修改、包管理、自定义工具；例子只提 Skill 名称，不包含具体业务项目名称。

- [ ] **Step 2: 将 Agent 主导接入提示改为安装整套 Skills**

在 `docs/setup-guide.md` 的公开来源中列出以下五个 URL：

```text
https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-workflows
https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-codely-bridge
https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-editor-automation
https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-package-management
https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-codely-custom-tools
```

提示要求 `skill-installer` 逐个安装到用户级目录；不要克隆整个仓库。保留 EditorWindow 包、CLI 定位、config 安全合并、备份和验收步骤。手动安装部分同时给出“整套推荐”和“只安装连接 Skill”的区别。

- [ ] **Step 3: 更新架构和排错文档**

`docs/architecture.md` 在 Codex 上方增加逻辑 Skill 路由层，但连接链路仍是 Codex → CodelyCLI → Bridge → Tuanjie Editor。列出四种任务委派和“纯文件任务不强制 MCP”。

`docs/troubleshooting.md` 新增：专项 Skill 未安装、Skill 已复制但当前会话未 reload、路由正确但 MCP schema 缺能力、package 解析未完成、自定义工具编译成功但未注册五个条目。

- [ ] **Step 4: 更新 AGENTS 规则片段**

`templates/AGENTS.tuanjie-snippet.md` 使用 `tuanjie-workflows` 作为默认入口，并列出四个专项 Skill 的职责。保留现有团结/Unity 边界、项目根一致性、schema 证明、编译闸门和写后重读规则。

- [ ] **Step 5: 验证文档链接和清单一致性**

Run:

```powershell
& .\artifacts\skill-suite-evals.ps1
$skillNames = Get-ChildItem skills -Directory | Select-Object -ExpandProperty Name
$missingFromReadme = $skillNames | Where-Object { (Get-Content -Raw README.md) -notmatch [regex]::Escape($_) }
if ($missingFromReadme) { throw "README missing: $($missingFromReadme -join ', ')" }
rg -n "2DTower Defense|TowerDefenseDemo|SampleScene|docs/superpowers" README.md docs/setup-guide.md docs/architecture.md docs/troubleshooting.md skills templates prompts
```

Expected: 套件评测 PASS；README 缺失列表为空；产品文档、Skills、模板和提示中没有业务项目或 `docs/superpowers` 引用。

- [ ] **Step 6: 提交文档更新**

```powershell
git add README.md docs/setup-guide.md docs/architecture.md docs/troubleshooting.md templates/AGENTS.tuanjie-snippet.md
git commit -m "docs: document Tuanjie Codely skill suite"
```

### Task 8: 全量验证、安装验收与发布准备

**Files:**
- Modify only if validation finds a defect: files created or modified in Tasks 2–7
- Read: all `skills/*/SKILL.md`
- Read: `README.md`, `docs/setup-guide.md`, `docs/architecture.md`, `docs/troubleshooting.md`
- Test locally, do not commit: `artifacts/skill-suite-evals.ps1` and scenario records

**Interfaces:**
- Consumes: 完整五 Skill 套件和更新后的文档。
- Produces: 静态验证结果、真实提示压力测试结果、可用时的团结 MCP 只读验收，以及干净且可推送的 Git 分支。

- [ ] **Step 1: 对每个 Skill 运行官方快速验证**

Run:

```powershell
Get-ChildItem skills -Directory | ForEach-Object {
    python -X utf8 C:\Users\QJX\.codex\skills\.system\skill-creator\scripts\quick_validate.py $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "quick_validate failed: $($_.Name)" }
}
& .\artifacts\skill-suite-evals.ps1
```

Expected: 五个 Skill 均 PASS，套件评测 PASS。

- [ ] **Step 2: 检查相对链接、frontmatter 和仓库边界**

使用 PowerShell 解析所有 `skills/**/*.md` 的相对 Markdown 链接，确认目标存在；核对每个目录名等于 `name`。运行：

```powershell
git ls-files
git status --short
rg -n "2DTower Defense|TowerDefenseDemo|SampleScene" README.md docs/setup-guide.md docs/architecture.md docs/troubleshooting.md skills templates prompts scripts editor-package
rg -n "token\s*=|descriptor|localhost:[0-9]{4,5}" README.md docs/setup-guide.md docs/architecture.md docs/troubleshooting.md skills templates prompts scripts editor-package
```

Expected: 不跟踪 `artifacts/`、`tests/`、`editor-package/Tests/` 或 `docs/superpowers/`；无业务项目内容；没有真实凭据、descriptor 内容或固定连接端口。普通说明中的单词 `token`/`descriptor` 可以出现，任何命中必须人工确认只是禁止规则而不是秘密值。

- [ ] **Step 3: 使用新会话做五类真实提示压力测试**

按照 `superpowers:writing-skills`，为路由、连接、Editor automation、包管理、自定义工具各使用一个隔离场景。每个场景记录：加载的 Skill、项目类型判断、选择的工具层、写入前检查、拒绝条件和完成报告。至少包含一次 Unity 官方项目拒绝、一次错项目根拒绝、一次编译错误停止和一次 schema 未暴露拒绝。

Expected: Agent 不需要读取所有 reference 就能正确选择专项 Skill；只有实际任务需要时才读取相应 reference。

- [ ] **Step 4: 在当前通用团结项目做只读验收**

只执行非敏感检查：确认规范化工作区、团结版本标识、`codex mcp list` 中 `tuanjie` 注册状态；如果当前会话实际暴露 `tuanjie` MCP，再调用一个真实只读工具并核对其项目根等于工作区。不要输出端口、descriptor 或 token，不修改 Scene/Prefab/资源。

Expected: 分别记录“已注册”和“已实际调用”两种状态。若 MCP 工具未暴露，只能报告注册检查完成、实际连接验收未完成。

- [ ] **Step 5: 审查差异和提交设计/计划文档**

Run:

```powershell
git diff --check
git status --short
git log --oneline -8
```

检查无意删除、死链、重复职责和被跟踪的本地产物。然后提交本次已确认设计和计划：

```powershell
git add docs/design/2026-08-21-tuanjie-codely-skill-suite.md docs/plans/2026-08-21-tuanjie-codely-skill-suite.md
git commit -m "docs: design Tuanjie Codely skill suite"
```

如果设计和计划已在执行前单独提交，则跳过重复提交，只确认工作区状态。

- [ ] **Step 6: 发布前报告并等待推送授权**

报告以下项目：五个 Skill 路径、每个 quick validation 结果、本地场景验证、实际团结 MCP 只读验证、未完成项、提交列表和远端差异。只有用户明确授权推送后才执行：

```powershell
git push origin HEAD:main
```

Expected: 推送后 `git rev-parse HEAD` 与 `git rev-parse origin/main` 一致，`git status --short --branch` 干净且无 ahead/behind。
