# 安装与设置

本指南是本仓库唯一的完整安装入口，用于让支持本地 MCP STDIO 的 Agent 连接团结 Editor，不要求使用 TuanjieAI。普通用户不需要克隆或下载整个仓库；前置条件由用户准备，随后为当前 Agent 安装五个 Skill、为团结项目安装 EditorWindow UPM 包，再选择用户级全局或当前项目配置。一次只使用一个团结项目时默认用户级全局，多项目并行时使用当前项目范围；PowerShell 只用于批量项目、脚本化或 CI。客户端路径、刷新方式和手动回退见[客户端参考与手动回退](agent-configurations.md)。

## 1. 前置条件

以下项目外部条件需要先准备好，Agent 不会替你安装或修复：

1. 安装与项目版本匹配的团结 Editor，并用它创建或打开目标项目；确认项目根包含 `Assets`、`Packages`、`ProjectSettings`，且 `ProjectVersion.txt` 包含团结版本字段。
2. 按[官方 Codely Bridge 安装流程](https://codely-docs.tuanjie.cn/en/using-codely/codely-bridge-installation-guide/)在团结 Package Manager 安装与 Editor 版本匹配的 `cn.tuanjie.codely.bridge`，然后打开目标团结项目；Bridge 会随 Editor 自动加载并初始化，无需单独启动 Bridge。
3. 安装 Node.js LTS（自带 npm），在 PowerShell 安装 CodelyCLI：

       npm install -g @unity-china/codely-cli

   这会把 CodelyCLI 安装为当前用户可用的全局命令。只要 npm 的全局命令目录已进入 `PATH`，不需要提前手工抄写路径；接入时让 Agent 解析 `codely.cmd` 的绝对路径并验证版本：

       $cli = (Get-Command codely.cmd -ErrorAction Stop).Source
       $cli
       & $cli --version

   如果项目使用 Bridge 1.0.81 或更高版本，请保持 CodelyCLI 为支持 `Temp/.com-unity-codely.json` 的最新版本；当前已验证 `1.0.0-rc.60` 可用。MCP 的 `--unity-project-path` 仍填写项目根目录。

   也可以参考 [Codely CLI 安装说明](https://codely-docs.tuanjie.cn/learn/ai-programming-environment-setup-guide/)。
4. 安装至少一个支持本地 MCP STDIO 和 Agent Skills 的客户端，在其中打开当前团结项目并完成工作区/目录信任。Agent 可以在具备桌面操作能力时帮你定位或打开项目；出现信任、访问权限或受保护操作弹窗时，由用户在客户端界面确认。常用客户端的打开和信任方法见[客户端参考与手动回退](agent-configurations.md)。Codex、Claude Code、Cursor、Qoder 和 WorkBuddy 都要安装本仓库的五个 Skill，再用 EditorWindow 配置各自的 MCP 入口。

Unity 官方 Editor 项目不要使用本仓库的 Codely Bridge Skill、EditorWindow 或 `tuanjie` MCP。

## 2. EditorWindow 主导接入（推荐）

普通用户不需要复制长提示词给 Agent。先在当前团结项目安装 `cn.qjx.codex-codely-setup` UPM 包，再打开 `Window/Tuanjie Codely Agent Setup`，窗口会集中显示客户端、Skill、Bridge、CodelyCLI 和 `tuanjie` 配置状态。

EditorWindow 安装示例：在团结 Editor（Unity 风格界面）打开 `Window → Package Manager`，点击左上角 `+`，选择 **Add package from git URL**，粘贴下面的地址并点击 **Add**：

       https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit.git?path=/editor-package

等待包导入、编译和 Domain Reload 完成，再打开 `Window/Tuanjie Codely Agent Setup`。这个包只提供设置窗口，不会自动安装或替换 Codely Bridge。

Codely Bridge 请按[官方安装流程](https://codely-docs.tuanjie.cn/using-codely/codely-bridge-installation-guide/)操作：打开 Package Manager，在 **Tuanjie Registry** 搜索 `Codely Bridge` 或 `Tuanjie AI`，安装 `Codely Bridge`，然后重新读取窗口状态。

按窗口顺序操作：

1. 选择 Codex、Claude Code、Cursor、Qoder 或 WorkBuddy；窗口会显示该客户端的 Skill 安装目录和 MCP 配置目标。
2. 点击“安装/更新 Skills”，从本仓库 `main` 分支获取五个独立 Skill。窗口只管理这五个 Skill 子目录，遇到无法确认归属的已有文件会拒绝覆盖。
3. 默认使用“用户级全局（单项目）”；需要同时连接多个团结项目时切换为“当前项目”。
4. 点击“重新读取”和“预览配置”，确认目标文件、旧路径、新路径和备份行为，再点击底部的“配置客户端”。
5. 按窗口提示重新加载当前 Agent，并在客户端自己的 MCP 列表或设置页确认 `tuanjie` 已注册；如果当前会话暴露实际 MCP 工具，再执行只读项目根核对。

窗口不会安装或替换 Codely Bridge，不会输出凭据，也不会启动长期驻留的 MCP 服务。Unity 官方 Editor 项目不要使用本窗口。

### 完成标准

Agent 必须分别说明：

- 五个 Skill 是否安装到当前客户端的正确用户级目录并被发现；
- `cn.qjx.codex-codely-setup` 是否已加入当前项目并完成导入；
- 当前客户端选择了用户级全局还是当前项目范围，MCP 配置是否新建或更新，是否创建备份；
- CodelyCLI 绝对路径和版本是否验证；
- `tuanjie` MCP 的注册状态、实际只读检查和项目根比较是否完成；
- 没有执行的验证或需要用户手动完成的步骤。

## 3. EditorWindow 操作说明

完成上一节的 Git URL 安装后，打开 `Window/Tuanjie Codely Agent Setup`。UPM 包 ID 是 `cn.qjx.codex-codely-setup`；也可以在仓库开发场景中将 `editor-package` 作为本地 UPM 包引用。

1. 选择 Client；保持默认“用户级全局（单项目）”，或者在需要多个团结项目同时使用时切换到“当前项目”。
2. 点击“重新读取”检查项目、Bridge、CodelyCLI、Skills 和现有 `tuanjie` 状态。
3. 点击“安装/更新 Skills”安装五个 Skill，再点击“预览配置”。
4. 确认目标路径和唯一变更后，点击底部宽按钮“配置客户端”。已有条目只改变 `--unity-project-path` 后面的路径；缺少条目时才新增最小配置。

窗口支持 Codex、Claude Code、Cursor、Qoder 和 WorkBuddy。它不安装 Bridge，不读取 descriptor 内容，也不启动 CodelyCLI 常驻服务。普通单项目接入不需要再运行 PowerShell。

## 4. 手动安装全局 Skills（回退）

正常流程使用 EditorWindow 的“安装/更新 Skills”。只有窗口无法访问 GitHub、客户端未识别或需要人工恢复时，才按下面的用户级目录手动安装；五个客户端都使用相同的五个 Skill：

| 客户端 | 用户级 Skill 根目录 |
|---|---|
| Codex | `$CODEX_HOME/skills/`，未设置时通常为 `~/.codex/skills/` |
| Claude Code | `~/.claude/skills/` |
| Cursor | `~/.cursor/skills/` |
| Qoder | `~/.qoder/skills/` |
| WorkBuddy | `~/.codebuddy/skills/` |

手动回退时优先使用当前客户端官方 Skill 安装器；否则从以下 GitHub 子路径逐个获取并放入对应根目录，不要克隆整个仓库，也不要把仓库根或整个 `skills` 目录当作一个 Skill：

    https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-workflows
    https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-codely-bridge
    https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-editor-automation
    https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-package-management
    https://github.com/QJX-XXXX/tuanjie-codely-bridge-toolkit/tree/main/skills/tuanjie-codely-custom-tools

安装后按当前客户端支持的 reload/重启方式确认五个 Skill 已被发现，再使用 `tuanjie-workflows` 作为入口或显式选择专项 Skill。Skill 全局安装与 MCP 配置范围互不等价：Skill 可以跨项目复用，而用户级全局 MCP 仍只指向最后配置的一个项目。

## 5. 批量/脚本化/CI：PowerShell（仅生成 Codex 配置）

只有需要批量处理多个团结项目、脚本化或 CI，且你已经取得仓库脚本文件时，才使用 PowerShell。脚本只生成/更新 Codex 的项目级 `.codex/config.toml`；Claude Code、Qoder、Cursor、WorkBuddy 不要用它替代 EditorWindow。它与 EditorWindow 是替代入口，不需要先后运行：

    .\scripts\setup-project.ps1 -ProjectPath "D:\TuanjieProjects\YourGame" -CodelyCliPath "C:\Tools\CodelyCLI\codely.cmd"

如果 `.codex/config.toml` 已存在且需要更新，增加 `-Force`。脚本只更新 `[mcp_servers.tuanjie]`，覆盖前会创建 `config.toml.bak`；首次创建不需要 `-Force`。

## 6. Codex `config.toml` 模板

将项目路径和 CodelyCLI 路径替换为实际绝对路径。Windows TOML 字符串中的反斜杠需要写成两个反斜杠：

    [mcp_servers.tuanjie]
    command = "cmd.exe"
    args = [
        "/c",
        "C:\\Tools\\CodelyCLI\\codely.cmd",
        "serve",
        "unity-mcp",
        "--stdio",
        "--unity-project-path",
        "D:\\TuanjieProjects\\YourGame"
    ]
    startup_timeout_sec = 30
    tool_timeout_sec = 120
    enabled = true

完整模板位于 [templates/config.toml.example](../templates/config.toml.example)。不要把 token、端口、descriptor 或真实用户凭据提交到仓库。EditorWindow 默认把这个 table 写入用户级 Codex 配置；它只能指向最后配置的一个项目。多个团结项目同时使用时，应切换到“当前项目”并分别维护 `.codex/config.toml`。

## 7. 连接与验证

安装和配置完成后：

1. 保持目标团结 Editor 打开，Bridge 会随 Editor 加载；不需要手动运行长期驻留的 CodelyCLI 服务。
2. Codex 在项目根执行 `codex mcp list`；Claude Code、Qoder、Cursor、WorkBuddy 使用各自的 MCP 列表或设置页确认 `tuanjie` 已注册。这只是配置检查，不等同于实际工具调用成功。
3. 确认当前客户端已经发现五个 Skill，再通过 `tuanjie-workflows` 路由到相应专项 Skill；连接任务必须先做只读项目根核对，Scene/Prefab/组件任务必须按读取 → 最小动作 → 重读 → 保存 → 再读闭环执行。
4. 如果 Editor 正在导入、编译、Domain Reload 或切换 Play Mode，先等待稳定，再进行 MCP 调用。
5. 在可回滚的测试场景中发送提示词：“使用 `tuanjie` MCP，在原点放一个立方体，并新建脚本让它自动旋转。”然后检查 Scene、脚本编译和运行结果。

当前 Agent 首次调用 `tuanjie` MCP 时，会按项目配置自动启动 `codely.cmd serve unity-mcp --stdio`；服务由 Agent 会话管理，不需要手动运行长期驻留服务。部分客户端仍需要在设置页刷新或启用条目，这只是客户端状态确认，不是启动 Bridge 的额外步骤。

## 8. 多项目使用

Skill 和 EditorWindow 包可以复用。用户级全局 `tuanjie` 只有一个静态 `--unity-project-path`，每次配置新项目都会替换旧路径，因此只适合一次使用一个团结项目；需要多个项目同时使用时，为每个项目选择“当前项目”范围。多个 Agent 连接同一 Editor 时，同时只允许一个 Agent 执行写入。
