# Tuanjie + Codely Bridge Agent Integration

这个仓库的目的，是让使用 Codex、Claude Code、Qoder、Cursor、WorkBuddy 等 MCP Agent 的团队不依赖 TuanjieAI，直接通过 CodelyCLI 的 MCP stdio 入口和 Codely Bridge 连接团结 Editor。它提供可复用的团结 Editor 工作流，包含五客户端 EditorWindow、PowerShell 批处理脚本、Tuanjie + Codely Skill 套件、安全配置模板和验收指南。

## 项目定位

这是“多 Agent + 团结 Editor”的本地连接方案，作为 TuanjieAI 之外的工作路径；它不替换团结 Editor 或 Codely Bridge，也不适用于 Unity 官方 Editor 项目。

## 适用边界

- 目标是团结 Editor 项目，不是 Unity 官方 Editor 项目。
- Skill 可以全局安装并复用；EditorWindow 默认写用户级全局 MCP，适合一次只使用一个团结项目，多项目并行时改用当前项目范围。
- 当前版本只声明 Windows + PowerShell 验证，不宣称 macOS/Linux 兼容。
- 不自动安装 Node.js、任意 Agent、CodelyCLI 或 Codely Bridge；设置阶段不启动长期驻留的 MCP 服务，运行时由当前 Agent 按项目配置按需拉起 stdio 子进程，Bridge 则随团结 Editor 自动加载。
- 同一个团结 Editor 同时只允许一个 Agent 执行写入操作；切换客户端前先结束前一会话的写入并确认 Editor 稳定。

## 链路

任意支持本地 STDIO 的 Agent → CodelyCLI MCP/stdio → Codely Bridge → Tuanjie Editor

详见 [架构说明](docs/architecture.md)、[安装与设置](docs/setup-guide.md)、[客户端参考与手动回退](docs/agent-configurations.md) 和 [排错指南](docs/troubleshooting.md)。

## EditorWindow 支持的配置范围

| 客户端 | 用户级全局（单项目默认） | 当前项目（多项目并行推荐） |
|---|---|---|
| Codex | `$CODEX_HOME/config.toml` 或 `~/.codex/config.toml` | `<ProjectRoot>/.codex/config.toml` |
| Claude Code | `~/.claude.json` 的用户级 `mcpServers` | `~/.claude.json` 当前项目节点（local scope） |
| Cursor | `~/.cursor/mcp.json` | `<ProjectRoot>/.cursor/mcp.json` |
| Qoder | `~/.qoder/settings.json` | `<ProjectRoot>/.qoder/settings.local.json` |
| WorkBuddy | `~/.workbuddy/mcp.json` | `<ProjectRoot>/.workbuddy/mcp.json` |

打开 `Window/Tuanjie Codely Agent Setup` 选择客户端和范围。已有 `tuanjie` 条目时，窗口只替换 `--unity-project-path` 后面的路径字符串，其他配置、注释、顺序和空白逐字节保持；缺少条目时才新增最小配置。所有配置都必须指向当前项目的规范化绝对路径。手动回退和客户端路径见[客户端参考与手动回退](docs/agent-configurations.md)。

## 安装 EditorWindow UPM 包

在团结 Editor（Unity 风格界面）打开 `Window → Package Manager`，点击左上角 `+`，选择 **Add package from git URL**，粘贴下面的地址并点击 **Add**：

    https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit.git?path=/editor-package

等待包导入、编译和 Domain Reload 完成后，打开 `Window/Tuanjie Codely Agent Setup`。这个包只提供设置窗口，不会替你安装或替换 Codely Bridge。

Codely Bridge 本身按[官方安装流程](https://codely-docs.tuanjie.cn/using-codely/codely-bridge-installation-guide/)在 Package Manager 的 **Tuanjie Registry** 中搜索并安装；可搜索 `Codely Bridge` 或 `Tuanjie AI`。官方流程没有给出可通用复制的 Git URL，因此不要猜测或编造 Bridge 地址。安装后 Bridge 会随 Editor 加载和初始化。

## Skills 套件

Codex、Claude Code、Cursor、Qoder 和 WorkBuddy 都安装全部五个 Skill，让入口能够按任务自动分流：

| Skill | 负责内容 |
|---|---|
| `tuanjie-workflows` | 判断团结/Unity 边界并路由专项工作流 |
| `tuanjie-codely-bridge` | CodelyCLI、Bridge、MCP 配置和连接诊断 |
| `tuanjie-editor-automation` | Scene、Prefab、GameObject、组件、资源和脚本编译闭环 |
| `tuanjie-package-management` | 团结包查询、安装、升级、移除和解析版本验收 |
| `tuanjie-codely-custom-tools` | Bridge 自定义工具 API 核对、注册、发现和调用 |

Skill 分别安装到客户端的用户级目录：Codex 使用 `~/.codex/skills/`，Claude Code 使用 `~/.claude/skills/`，Cursor 使用 `~/.cursor/skills/`，Qoder 使用 `~/.qoder/skills/`，WorkBuddy 使用其官方兼容目录 `~/.codebuddy/skills/`。打开 EditorWindow 后，点击“安装/更新 Skills”即可安装或更新五个 Skill；手动路径和刷新规则见[客户端参考与手动回退](docs/agent-configurations.md)。

典型任务会自动路由：

- “连接不上 Bridge” → `tuanjie-codely-bridge`
- “给 Scene 对象加组件并保存” → `tuanjie-editor-automation`
- “升级 Tuanjie 包并确认实际版本” → `tuanjie-package-management`
- “创建并验证 Bridge 自定义工具” → `tuanjie-codely-custom-tools`

## 运行方式

安装并配置完成后，打开团结项目即可让 Codely Bridge 随 Editor 自动加载和初始化；当前 Agent 首次使用 `tuanjie` MCP 时，会按对应的项目配置自动启动 CodelyCLI 的 stdio 服务，不需要手动运行长期驻留的 MCP 服务。

## 前置条件

1. 安装与项目版本匹配的团结 Editor。
2. 按[官方 Codely Bridge 安装流程](https://codely-docs.tuanjie.cn/en/using-codely/codely-bridge-installation-guide/)安装与 Editor 版本匹配的 Codely Bridge，并打开目标团结项目。
3. 安装 Node.js LTS，并通过 npm 全局安装 CodelyCLI；确保 `codely.cmd` 可从 `PATH` 找到。接入时由 Agent 自动解析其绝对路径，只有自动解析失败时才需要用户提供。
4. 安装至少一个支持本地 MCP STDIO 的 Agent，并在其中打开并信任当前项目目录。

完整安装、EditorWindow、PowerShell、各客户端配置和验证步骤见[安装与设置](docs/setup-guide.md)。普通用户只需安装 `cn.qjx.codex-codely-setup` UPM 包，再在 EditorWindow 中安装 Skills 和配置客户端，不需要克隆或下载整个仓库。

## Agent 规则

任务路由、连接诊断、Editor 写入闭环和自定义工具验收已整合到五个 Skill；项目代理补充规则见[团结规则片段](templates/AGENTS.tuanjie-snippet.md)。

## 许可证

MIT，见 [LICENSE](LICENSE)。
