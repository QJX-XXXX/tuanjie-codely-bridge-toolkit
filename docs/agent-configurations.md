# 多 Agent 客户端参考与手动回退

普通接入请使用[安装与设置](setup-guide.md)和 `Window/Tuanjie Codely Agent Setup`。本页不再重复安装流程，只记录 EditorWindow 未能操作客户端 UI 时需要的路径、刷新方式和手动回退规则。

## 连接链路

每个客户端最终都按项目配置启动本地 stdio MCP：

```text
Agent 客户端
  → codely.cmd serve unity-mcp --stdio --unity-project-path <ProjectRoot>
  → Codely Bridge
  → 团结 Editor
```

`<ProjectRoot>` 必须是当前项目的规范化绝对路径。Windows 下通常使用 `cmd.exe /c` 调用 `codely.cmd`。不要把真实用户路径、token、端口或 descriptor 提交到仓库。

## 五客户端路径速查

EditorWindow 默认使用“用户级全局（单项目）”；多个团结项目并行时选择“当前项目”。Skill 始终安装到客户端用户级目录，与 MCP 配置范围无关。

| 客户端 | 用户级 Skill 根目录 | 用户级 MCP | 当前项目 MCP |
|---|---|---|---|
| Codex | `$CODEX_HOME/skills/`；未设置时 `~/.codex/skills/` | `$CODEX_HOME/config.toml`；未设置时 `~/.codex/config.toml` | `<ProjectRoot>/.codex/config.toml` |
| Claude Code | `~/.claude/skills/` | `~/.claude.json` 顶层 `mcpServers` | `~/.claude.json` 的 `projects[<ProjectRoot>].mcpServers` |
| Cursor | `~/.cursor/skills/` | `~/.cursor/mcp.json` | `<ProjectRoot>/.cursor/mcp.json` |
| Qoder | `~/.qoder/skills/` | `~/.qoder/settings.json` | `<ProjectRoot>/.qoder/settings.local.json` |
| WorkBuddy | `~/.codebuddy/skills/` | `~/.workbuddy/mcp.json` | `<ProjectRoot>/.workbuddy/mcp.json` |

WorkBuddy 的 Skills 兼容目录使用 `.codebuddy/skills/`，但 MCP 配置仍使用 `.workbuddy/mcp.json`。

EditorWindow UPM 包：

```text
https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit.git?path=/editor-package
```

## 客户端刷新与工作区信任

| 客户端 | 刷新/检查 | 打开和信任项目 |
|---|---|---|
| Codex | 重新打开任务；CLI 可运行 `codex mcp list` | 新建会话时选择当前项目并授予访问权限 |
| Claude Code | `claude mcp list`、`claude mcp get tuanjie`；会话内 `/mcp` | 在项目根启动 `claude`，按 workspace trust 提示确认 |
| Cursor | MCP 设置页刷新；有 CLI 时才运行 `cursor-agent mcp list` | **File → Open Folder**，选择 **Trust this Workspace** |
| Qoder | **Settings → MCP → My Servers** 查看工具列表 | 从项目根启动 Qoder，按 Directory Trust 提示确认 |
| WorkBuddy | **Plugins → MCP servers → Configure MCP** 刷新 | 创建任务时选择项目根，按目录权限提示确认 |

信任只表示客户端允许读取项目或加载项目配置，不等于 MCP 已连接。仍需检查 `tuanjie` 注册状态，并在当前会话暴露只读工具时核对 MCP 返回的项目根。

## 手动回退配置

只有 EditorWindow 不可用或需要脚本化回退时，才手动修改对应入口。已有 `tuanjie` 时只替换 `--unity-project-path` 后的路径；不要整体反序列化重写配置。修改前创建同目录 `.bak`，并保留其他 MCP server。

JSON 客户端的最小 STDIO 结构如下，路径必须替换为实际绝对路径：

```json
{
  "mcpServers": {
    "tuanjie": {
      "command": "cmd.exe",
      "args": [
        "/c",
        "C:\\Tools\\CodelyCLI\\codely.cmd",
        "serve",
        "unity-mcp",
        "--stdio",
        "--unity-project-path",
        "D:\\TuanjieProjects\\YourGame"
      ]
    }
  }
}
```

JSON 文件位置按上表选择；Claude Code 也可以使用：

```powershell
claude mcp add --transport stdio --scope project tuanjie -- cmd.exe /c "C:\Tools\CodelyCLI\codely.cmd" serve unity-mcp --stdio --unity-project-path "D:\TuanjieProjects\YourGame"
```

Codex 使用 TOML 模板 [templates/config.toml.example](../templates/config.toml.example)。批量项目、脚本化或 CI 才使用仓库 PowerShell 脚本；它只处理 Codex 项目级 `.codex/config.toml`。

## 手动验收

1. 五个 Skill 已安装到当前客户端正确的用户级目录，并按客户端规则重新加载。
2. EditorWindow 已导入，Bridge 已按官方流程随团结 Editor 加载和初始化。
3. `codely.cmd --version` 成功，客户端自己的 MCP 列表显示 `tuanjie`。
4. 如果会话提供实际只读 MCP 工具，核对 MCP 项目根与当前工作区一致；不一致时停止写入。

同一个团结 Editor 同时只允许一个 Agent 执行写入。切换客户端前先结束前一会话的写入，并等待 Editor 完成导入、编译、Domain Reload 和保存。

客户端官方参考：[Claude Code MCP](https://code.claude.com/docs/en/mcp)、[Cursor MCP](https://docs.cursor.com/context/model-context-protocol)、[Qoder MCP](https://docs.qoder.com/cli/mcp-reference)、[WorkBuddy MCP](https://www.workbuddy.ai/docs/zh/workbuddy/From-Beginner-to-Expert-Guide/Function-Description/MCP-Guide)。
