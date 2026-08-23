# 排错指南

## MCP 未出现在当前 Agent

1. 确认当前工作目录是目标项目根。
2. 在 EditorWindow 确认客户端、配置范围和目标路径。用户级全局只指向最后配置的单个项目；多项目并行应选择当前项目范围。
3. 确认 command、CodelyCLI 路径和 `--unity-project-path` 都是有效绝对路径，并运行 `codely.cmd --version`。
4. 使用当前客户端实际支持的 MCP 列表或状态检查，确认 server 名称为 `tuanjie`。
5. 保持目标团结 Editor 打开；Bridge 按官方流程随 Editor 加载和初始化。再用实际只读 MCP 调用核对项目根，不把窗口、包文件或连接图标存在当作“已连接”证明。

静态全局配置不能自动绑定所有项目；切换项目后点击“重新读取”，预览并更新 `--unity-project-path`，或者改用当前项目范围。

### Claude Code

运行 `claude mcp list` 和 `claude mcp get tuanjie`；会话内用 `/mcp` 查看实际工具。EditorWindow 的当前项目范围写入 `~/.claude.json` 对应项目的 local scope；团队共享 `.mcp.json` 发生变化时仍要按提示批准项目配置。

### Qoder

在 **Settings → MCP → My Servers** 中刷新条目，确认连接图标和工具列表。当前项目范围应检查 `.qoder/settings.local.json`；Qoder UI 未显示工具时，先检查 JSON 中 command/args 和项目根，再重新打开项目。

### Cursor

确认项目根的 `.cursor/mcp.json` 已被当前窗口加载，并在 MCP 设置页刷新工具列表。只有本机确实安装 Cursor Agent CLI 时才运行 `cursor-agent mcp list`；命令不存在时以 UI 为准。

### WorkBuddy

确认项目根的 `.workbuddy/mcp.json`，在 **Plugins → MCP servers → Configure MCP** 刷新。绿色状态表示客户端条目可用；仍需执行只读 MCP 调用并比较项目根。

## CodelyCLI 找不到或版本失败

优先顺序是 EditorWindow 中保存的路径、最新读取到的 `CODELY_CLI_PATH`、PATH 中的 `codely.cmd`。从 `0.3.2` 起，点击“重新读取”会重新查询当前进程、Windows 用户级和系统级环境变量，并展开 `%USERPROFILE%` 等变量，不再只使用团结 Editor 启动时继承的 PATH。不要扫描任意磁盘，也不要猜测文件名。先运行：

    & "C:\Tools\CodelyCLI\codely.cmd" --version

如果你使用的是旧版包，或环境变量是在 Editor 启动后才修改的，可以完全退出团结 Editor 和团结 Hub 后重新打开；也可以直接点击“选择 CodelyCLI”指定实际的 `codely.cmd`。Windows npm 全局安装常见路径是 `%APPDATA%\npm\codely.cmd`，但窗口只使用实际环境变量或用户选择的路径，不会假设该目录一定存在。命令不可执行时，修复路径或权限后再刷新窗口。

## 项目被识别为 Unity

检查 Editor 可执行文件是否为 Tuanjie.exe，以及 ProjectVersion.txt 是否包含 m_TuanjieEditorVersion。Unity 官方项目不得通过改名或手工添加字段伪装为团结项目。

## Bridge 缺失

窗口只会显示缺失并打开 Package Manager 入口。按项目实际团结版本安装 `cn.tuanjie.codely.bridge`，等待包解析、导入和 Domain Reload 完成，再重新读取状态。工具不会自动改 `Packages/manifest.json`。

## MCP 根路径不一致

如果当前 Agent 工作区和 MCP 状态报告的项目根不同，立即停止写入。关闭错误项目的连接，连接目标项目后重新读取根路径；不要把调用成功当作写入当前项目。

## 写入前拒绝覆盖

PowerShell 更新 Codex 项目 TOML 时需要 `-Force`。EditorWindow 对所有五个客户端都先预览再确认；已有文件会创建同目录 `.bak`。重复目标、缺少路径参数、结构不完整、预览过期或写入校验失败都应停止，不要改用整体 JSON/TOML 重写。

## C# 编译错误

先等待资源刷新、编译和 Domain Reload，再读取 Console。处理本次改动引入的错误后重新等待稳定；在程序集未加载前不要附加新组件、修改 Prefab 或保存依赖新类型的资源。

## 401 Unauthorized

这是连接链路的临时认证问题，不要读取或输出 token、端口或 descriptor。等待约两秒，仅对完全相同的安全命令重试一次；仍失败则停止并报告未完成的 Editor 验证。

## 专项 Skill 未安装

先确认请求属于哪个 Skill：`tuanjie-codely-bridge` 负责连接，`tuanjie-editor-automation` 负责对象/脚本，`tuanjie-package-management` 负责包，`tuanjie-codely-custom-tools` 负责自定义工具。缺少对应目录时，先在 EditorWindow 选择当前客户端并点击“安装/更新 Skills”；窗口不可用时再按[客户端参考与手动回退](agent-configurations.md)处理。不要让入口 Skill 猜测专项流程，也不要克隆整个仓库。

如果安装提示某个旧文件（例如已移除的 `references/tool-routing.md`）不属于远端版本，先确认目标目录的 `SKILL.md` 名称与目标 Skill 相同，再更新到包含旧版迁移逻辑的 EditorWindow 包。识别到同名旧 Skill 后，窗口会保留旁路备份并替换旧版；目录中存在无法归属的个人文件时仍会拒绝覆盖。

## Skill 已复制但当前会话没有新能力

文件复制或安装成功不等于当前会话已经加载。Codex 重新打开对话；Codely Skills 按宿主支持的 `/skills reload` 或新会话刷新。刷新后再次确认实际可用的 Skill 和 `tuanjie` MCP schema。

## 路由正确但 MCP 没有目标能力

文档、历史对话和用户口述都不能替代 schema。工具未暴露、参数不匹配或项目根未核对时只做只读诊断并停止；不要用相似工具替代，也不要默认切换到 Unity MCP。

## 包解析没有完成

包请求返回后仍可能处于解析、导入、编译或 Domain Reload。暂停后续包变更，等待 Editor 稳定，再重读 manifest、实际解析版本和 Console；不要手工编辑 `packages-lock.json`。

## 自定义工具编译成功但未注册

“方法编译成功”只证明程序集可编译；还要等 Domain Reload，重新发现当前 MCP schema，并确认准确工具名和参数。如果 schema 没有该工具，报告 Bridge 扫描/暴露未完成，不调用、不回退、不声称可用。

## 多 Agent 同时操作

同一个团结 Editor 不要同时让多个 Agent 执行写入。先结束前一客户端的写入会话，确认 Editor 不在导入、编译、Domain Reload、保存或切换 Play Mode，再刷新下一个客户端的 MCP 连接。这样可以避免两个 stdio 进程对同一 Scene、Prefab 或包状态产生交错修改。
