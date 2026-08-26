# macOS Codex 页面内状态栏：CDP 可行性测试提示词

> 状态：**实验性、尚未经过实体 Mac 验证**。这不是当前 macOS 安装包已经支持的功能。

## 调研结论

存在一条值得在实体 Mac 上验证的候选路径：如果 macOS Codex 的实际应用包包含 Electron Framework，并且 Codex 在启动时接受 Electron 的 `--remote-debugging-port` 参数，就可以通过仅监听回环地址的 Chrome DevTools Protocol（CDP）枚举页面目标，再对已确认属于 Codex 的 `app://` 页面执行临时 DOM 状态栏注入。

目前没有找到 OpenAI 官方公开的“macOS Codex 页面注入接口”，也不能仅凭 Windows 版行为推断 Mac 版一定相同。OpenAI 官方只确认 Codex app 已在 macOS 提供；Electron、目标 URL、启动参数是否可用，都必须由实体 Mac 的只读证据确认。若任一前置条件失败，应继续使用当前的外部置顶悬浮窗，而不是修改签名应用包或绕过系统保护。

## 使用方式

在 macOS Codex 中打开本项目的干净 checkout，把下面整段提示词粘贴到一个新任务。测试会要求一次由用户手动完成的 Codex 退出和重启；这是为了避免运行提示词的 Codex 自行终止宿主进程。不要在包含未保存工作的 Codex 窗口中开始重启阶段。

## 可直接粘贴给 macOS Codex 的提示词

```text
你正在一台实体 macOS 机器上，为 CodexTokenOverlay 项目验证“通过 Electron CDP 把状态栏临时插入 Codex 页面”的可行性。你的任务只是检测、做一次可逆的固定文本探针并汇报证据，不是宣布 macOS 已获正式支持，也不是安装常驻组件。

必须遵守以下边界：

1. 先检测，后变更。第一阶段只能读取应用包元数据、签名信息、进程和监听状态。
2. 不读取、打印、复制或上传 Token、Cookie、Authorization header、账号信息、会话 JSONL 内容、环境变量全集、浏览器存储、请求/响应正文或聊天内容。
3. 不修改 Codex.app 内任何文件，不重新签名，不关闭 SIP，不改 Gatekeeper，不申请 Accessibility 权限，不使用 DYLD 注入、调试器附加、进程内存修改、Electron fuse 修改或 `--no-sandbox`。
4. 不使用公网监听。CDP 只能接受 `127.0.0.1` 或 `::1` 连接；若 `lsof` 显示 `*`、`0.0.0.0`、非回环 IP 或归属无法确认，立即停止测试并让用户正常重启 Codex。
5. 不安装依赖、不使用 `sudo`、不执行 `pip install`/`npm install`/Homebrew 安装。允许在 `mktemp -d` 创建一次性脚本和结果文件，完成后必须删除。
6. 不接管鼠标、键盘或窗口焦点。需要退出 Codex 时先保存阶段结果，给用户一条明确命令，由用户手动退出和重启；不要由当前任务杀死自己的宿主进程。
7. 只连接本机经过归属验证的 Codex CDP 端口，只向唯一确认的 Codex 主页面发送 `Runtime.evaluate`。不得调用 Network、Storage、Cookies、Fetch、Page.navigate、Target.createTarget 或下载相关 CDP 方法。
8. 输出必须脱敏：用户目录统一写成 `$HOME`，不输出 CDP WebSocket 的随机 ID，不输出 URL 查询串或 fragment，不输出进程完整参数中可能出现的其他值。只保留系统架构、Codex 版本、Bundle ID、是否发现 Electron Framework、端口是否为回环、脱敏后的目标 scheme/host/path 和探针结果。

按以下阶段执行，不要跳步。

阶段 A：只读检测

- 确认当前系统是 macOS，记录 `sw_vers -productVersion` 与 `uname -m`。
- 在 `/Applications/Codex.app`、`$HOME/Applications/Codex.app` 中查找 Codex；若都不存在，可用 Spotlight 只查找名为 `Codex.app` 的应用包。不要扫描整个磁盘内容。
- 解析应用包的 `Contents/Info.plist`，只记录 `CFBundleIdentifier`、`CFBundleExecutable`、`CFBundleShortVersionString` 和 `CFBundleVersion`。
- 检查主可执行文件存在，使用 `file` 记录其架构；检查 `Contents/Frameworks/Electron Framework.framework` 是否存在。可以列出 `Contents/Frameworks` 的第一层名称，但不要递归复制应用资源。
- 运行只读的 `codesign --verify --deep --strict --verbose=2 <Codex.app>`，并用 `codesign -dv --verbose=4 <Codex.app>` 只提取 Identifier、TeamIdentifier、Runtime Version 等非敏感摘要。不要修改签名。
- 检查当前是否已有 Codex 进程，以及是否已经带 `--remote-debugging-port`。输出时不要原样打印完整命令行，只报告布尔值和已脱敏端口。

阶段 A 的停止条件：

- 不是 macOS、找不到有效 Codex.app、主可执行文件无效，或没有 Electron Framework：输出 `MACOS_CDP_CANDIDATE=NO`，说明具体证据，停止。不要尝试 Accessibility、DYLD 或应用包修改作为替代。
- 条件满足：输出 `MACOS_CDP_CANDIDATE=YES_UNVERIFIED`，继续准备阶段 B，但仍不得宣称 CDP 参数已生效。

阶段 B：准备安全重启，不自行退出 Codex

- 用 `mktemp -d` 创建唯一临时目录，权限设为当前用户可读写。
- 用 Python 或系统工具先绑定 `127.0.0.1:0` 获取一个当前空闲的随机高位端口，立即释放；记录端口。承认这一步存在极小的端口竞争窗口，后续必须重新验证监听归属。
- 在临时目录保存一份不含凭据的状态文件：应用路径、Bundle ID、主可执行名、随机端口和测试阶段。路径中的用户名在显示给用户时替换为 `$HOME`。
- 给用户展示将要执行的命令，其语义应等价于：

    open -a "Codex" --args --remote-debugging-port=<随机端口>

- 说明必须先从 Codex 菜单正常退出所有 Codex 窗口，确认进程结束，再在 Terminal 执行该命令。不要建议 `kill -9`。如果应用显示名不是 `Codex`，使用阶段 A 已验证的 `.app` 路径。
- 明确提醒：这会中断当前 Codex 工作流；保存未提交内容后再操作。此时暂停并等待用户回复“已按测试命令重启”。不要在同一阶段自行关闭 Codex。

阶段 C：重启后的端点与归属验证

- 从阶段 B 的状态文件恢复端口和应用路径；若状态文件不存在或有多个候选，停止并让用户重新从阶段 A 开始，不猜端口。
- 最多等待 15 秒，然后用 `lsof -nP -iTCP:<端口> -sTCP:LISTEN` 检查监听。验证监听地址只能是 `127.0.0.1` 或 `[::1]`。
- 取得监听 PID，用 `ps` 和应用包的主可执行信息验证它属于刚才确认的 Codex.app。只输出 PID、已脱敏的可执行归属结论和端口，不输出完整命令行。
- 只有监听地址和进程归属都通过，才允许用 `curl --max-time 2` 请求 `http://127.0.0.1:<端口>/json/version` 和 `/json/list`；若只监听 IPv6，则使用 `[::1]`。
- 解析 JSON 时不得整段打印。`/json/version` 只保留 Browser/Protocol-Version 是否存在；`/json/list` 只保留每个目标的 type 以及去掉 query/fragment 后的 scheme、host、path。永远不要输出 `webSocketDebuggerUrl`。
- 目标必须同时满足：`type == "page"`、WebSocket host 为回环、WebSocket 端口等于已验证端口，并且 URL 是 `app://-/index.html` 或同一路径带 query/fragment。若没有唯一目标，输出 `CDP_TARGET_VERIFIED=NO` 并停止，不对“看起来像 Codex”的其他网页猜测注入。

阶段 D：一次性 DOM 探针

- 在临时目录创建一个最小 CDP 客户端。优先使用机器已经具备的能力；若没有现成 WebSocket 库，则可用 Python 标准库实现仅支持本次 `ws://` 回环连接的最小 RFC 6455 客户端。不得联网下载依赖。
- 客户端必须在连接前再次校验：WebSocket host 是 `127.0.0.1`/`::1`，端口等于已验证端口，路径以 `/devtools/page/` 开头。日志中隐藏其随机 ID。
- 只发送一条 `Runtime.evaluate`，执行一个常量、无外部输入、无网络访问的 IIFE。它应：
  - 仅创建 ID 为 `codex-token-overlay-macos-cdp-probe` 的节点和对应 style；
  - 文本固定为 `CodexTokenOverlay · macOS CDP probe`，不得包含账号、线程名或真实 Token 数据；
  - 使用与仓库 `CdpDomInjector` 同类的窄范围 composer 查找：可见的 `textarea`、`[contenteditable="true"]` 或 `[role="textbox"]`，向上查找 `[data-testid*="composer" i]`、`[class*="ComposerLayoutRoot" i]` 或 `form`；
  - 只在 composer 后插入节点；找不到 composer 就返回 `{mounted:false, reason:"composer-not-found"}`，不得退化成覆盖全窗口的悬浮元素；
  - 设置 `role="status"`、`aria-live="polite"`，使用不拦截主要交互的简单样式，不添加事件监听器，不访问 localStorage、IndexedDB、Cookie、网络或 Electron/Node API；
  - 去除同 ID 重复节点，并返回 `{mounted, count, url}`。输出时只保留 mounted/count，并对 url 去除 query/fragment。
- 让用户只用肉眼确认固定探针文本是否出现在输入框下方；不要截图包含聊天内容。如确需截图，必须由用户自己裁剪到只剩探针区域并确认无敏感内容后再分享。
- 再发送一条仅查询表达式，确认节点数恰好为 1、文本完全匹配、位置紧邻 composer。
- 最后发送清理表达式，只删除 `#codex-token-overlay-macos-cdp-probe` 及其专用 style；查询确认两者都为 0。无论探针成功、失败还是异常，都执行清理。

阶段 E：关闭端口并恢复正常启动

- 保存脱敏结论后，请用户从 Codex 菜单正常退出，再从 Launchpad/Finder 正常启动 Codex，不带任何调试参数。
- 用户回复已正常重启后，确认原随机端口已无监听；删除临时目录。
- 如果端口仍在监听，先只读确认 PID。只有它仍明确属于 Codex 时，提示用户再次正常退出 Codex；不要终止不明进程。

最终只按下面模板汇报，不附原始 JSON、完整命令行、WebSocket URL、用户目录或会话内容：

MACOS_CDP_TEST_RESULT
- macOS: <版本>
- arch: <arm64|x86_64>
- Codex version: <版本>
- Bundle ID: <值>
- Electron Framework: <FOUND|NOT_FOUND>
- code signature verify: <PASS|FAIL>
- remote-debugging argument accepted: <YES|NO>
- listener ownership: <CODEX_CONFIRMED|FAILED>
- listener scope: <LOOPBACK_ONLY|UNSAFE|NONE>
- target: <脱敏后的 scheme/host/path 或 NONE>
- probe mount: <PASS|FAIL|NOT_RUN>
- composer adjacency: <PASS|FAIL|NOT_RUN>
- cleanup: <PASS|FAIL|NOT_RUN>
- normal restart / port closed: <PASS|FAIL|NOT_RUN>
- conclusion: <SUPPORTED_CANDIDATE|CDP_UNAVAILABLE|INCONCLUSIVE>
- blocker: <一句话，无则 NONE>
- sensitive data exposed: NO

只有以下条件全部满足，conclusion 才能是 `SUPPORTED_CANDIDATE`：Electron Framework 存在、启动参数生效、监听仅在回环地址、监听 PID 明确属于 Codex、唯一 `app://-/index.html` 页面目标通过、固定探针成功挂载且紧邻 composer、清理成功、正常重启后端口关闭。任何未知项都必须是 `INCONCLUSIVE`，不能推定成功。
```

## 方案边界

- CDP 注入是运行时、内存中的 DOM 变化；刷新页面、关闭窗口或正常退出 Codex 后不会保留。
- CDP 没有自身认证机制，测试时必须验证回环监听和进程归属，并尽快关闭调试端口。
- macOS Accessibility API 能表示界面元素的位置、属性和动作，但它不是 Codex DOM 插入接口。它可以支持未来的外部窗口定位研究，不能替代本测试对页面内状态栏的验证。
- 修改 `.app`、重签名、DYLD 注入或降低 Hardened Runtime/SIP 保护会改变安全边界，不属于普通用户可接受的适配方案。
- 即使一次测试成功，也只能证明该 Codex/macOS 版本组合具备候选能力；正式实现仍需版本门控、失败自动降级、移除逻辑和实体 Mac 回归测试。

## 一手资料

- [OpenAI：Introducing the Codex app](https://openai.com/index/introducing-the-codex-app/)：确认 Codex app 的 macOS 产品形态，但没有承诺公开的页面注入接口。
- [Electron：Supported Command Line Switches](https://www.electronjs.org/docs/latest/api/command-line-switches)：明确记录 `--remote-debugging-port=<port>` 会在指定端口启用 HTTP 远程调试。
- [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/)：定义 `/json/version`、`/json/list`、`webSocketDebuggerUrl` 和页面 WebSocket 端点。
- [Chromium source：remote debugging address](https://chromium.googlesource.com/chromium/src/+/HEAD/content/shell/common/shell_switches.h)：说明远程调试默认使用回环地址，并警告该协议没有认证；测试仍必须用 `lsof` 验证实际绑定。
- [Apple：Placing content in a bundle](https://developer.apple.com/documentation/bundleresources/placing-content-in-a-bundle)：说明 macOS 应用的 `Contents/Info.plist`、`Contents/MacOS/` 等标准布局。
- [Apple：AXUIElement](https://developer.apple.com/documentation/applicationservices/axuielement)：Accessibility 客户端可读取界面层级、位置、属性和动作；它并不提供 Web DOM 注入语义。
- [Apple：Hardened Runtime](https://developer.apple.com/documentation/security/hardened-runtime)：说明系统对代码注入、DYLD 环境变量和进程内存篡改的保护边界。
- [Electron：Security](https://www.electronjs.org/docs/latest/tutorial/security)：Electron 官方安全建议，包括沙箱、上下文隔离及不要向不受信内容暴露高权限 API。
