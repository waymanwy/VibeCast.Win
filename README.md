# VibeCast for Windows

把安卓手机变成 Windows 电脑的**远程输入板**。用**手机自带输入法的语音键**（Gboard 等）口述，文字实时同步、**自动敲进电脑上当前聚焦的输入框**（Notion / Obsidian / 聊天框 / 任意文本框）。

这是 [Pls-1q43/VibeCast](https://github.com/Pls-1q43/VibeCast)（macOS + iOS）思路的 **Windows + Android** 重写版：
- 桌面端由 Swift 菜单栏程序 → **C# / .NET 系统托盘程序**。
- macOS 辅助功能注入文字 → **Windows SendInput 按键注入**（对 Electron 类应用也可靠）。
- 手机端是一个极简网页：**只有一个文本框**，任意浏览器打开即用，**无需装 App、无需麦克风权限**。

---

## 工作原理

```
 ┌─────────────┐  输入法语音键  ┌─────────────┐   ws(JSON) + 配对码  ┌──────────────────────┐
 │  安卓手机    │ ─────────────▶ │  网页文本框   │ ────────────────────▶ │  Windows 托盘程序      │
 │  任意浏览器   │                │  只负责转发   │                       │  Kestrel HTTP + WS    │
 └─────────────┘                └─────────────┘                       │  ↓                    │
                                                                       │  SendInput 按键注入     │
                                                                       │  → 电脑上聚焦的输入框   │
                                                                       └──────────────────────┘
```

**关键设计:语音识别这件事完全不归 VibeCast 管。** 手机上就是个文本框,用你手机输入法自带的麦克风按钮（Gboard 等,通常是端上识别、准确率高）口述,VibeCast 只负责把文本框里的字传到电脑、敲进去。好处很直接:
- 不需要浏览器的麦克风权限,也就**不需要 HTTPS**——纯 HTTP,没有自签名证书警告。
- 语音质量取决于你手机输入法本身，通常比网页调用的识别更准、更快、带标点。
- 任意手机、任意浏览器都能用，不再局限于安卓 Chrome。

- 手机和电脑需在**同一局域网**（同一 Wi-Fi）。
- 发送前，请先在电脑上**点好你想让文字落入的输入框**——手机页面会实时显示"→ 将输入到：XXX"，帮你确认。
- 首次连接需要**扫描 PC 托盘弹出的二维码完成配对**（见下方"配对与鉴权"）。

---

## 环境要求

- Windows 10 / 11
- **.NET 8 SDK**（用于构建）。检查是否已装：
  ```powershell
  dotnet --version
  ```
  没有就装（任选其一）：
  ```powershell
  winget install Microsoft.DotNet.SDK.8
  ```
  或从 https://dotnet.microsoft.com/download/dotnet/8.0 下载安装。
- 手机端：任意浏览器均可；语音质量取决于手机输入法（推荐 Gboard）。

---

## 构建与运行

```powershell
# 在仓库根目录
dotnet restore
dotnet build -c Release

# 直接运行
dotnet run -c Release --project src/VibeCast/VibeCast.csproj
```

打包成**单文件 exe**（免装运行时，方便拷走使用）：
```powershell
dotnet publish src/VibeCast/VibeCast.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
# 产物：publish\VibeCast.exe —— 真·单文件，约 74 MB
```

> 手机网页（`wwwroot`）已作为**嵌入式资源打进 exe**，所以产物就是**单独一个 `VibeCast.exe`**，
> 免装运行时、无需附带任何文件夹，拷到任意 Windows 10/11 双击即用。
>
> 体积主要来自**自包含发布打进去的完整 .NET 运行时**——这是 WinForms 自包含部署的固有特性：
> 只要用了 `UseWindowsForms`，SDK 就会把整个 WPF+WinForms 运行时包一起打进去（没有"只要 WinForms"
> 的运行时包可选），而 WinForms 应用又因为大量用反射被官方明确禁止裁剪（trim）。项目已开启
> `EnableCompressionInSingleFile`（单文件压缩，纯打包层面，不影响功能）把体积从 ~163 MB 压到
> ~74 MB；冷启动会多花一点时间做一次性解压（实测约 1.3 秒，后续启动无感）。

启动后程序**驻留系统托盘**（不弹主窗口）。托盘图标：
- **双击** → 打开"连接信息"窗口（含二维码和网址）。
- **右键** → 连接信息 / 打开配置页 / 开机自启 / 退出。

---

## 手机连接步骤

1. 确认手机和电脑连的是**同一个 Wi-Fi**。
2. 电脑托盘**双击**图标，弹出二维码 → 手机相机/浏览器扫码。
   （二维码里的链接自带**配对码**，扫码即完成配对，之后无需重复操作。）
3. 页面打开后：先在电脑上**点好目标输入框**（页面顶部会显示"→ 将输入到：XXX"确认）。
4. 点文本框，用**手机键盘自带的麦克风按钮**口述（或直接打字），点"发送到电脑"。文字即刻敲入——**不需要选目标软件**，写到哪个框全看你刚才点的是哪个输入框。
5. 敲错了？点**"撤销"**——会退格删掉刚刚敲入的内容（仅撤销 VibeCast 自己打的这一段，不保证恢复被覆盖的原内容，见下文限制）。

没有证书警告、没有麦克风授权弹窗——因为整站是纯 HTTP，语音也不经过这个网页。

首次打开会有一条"添加到主屏幕"的小提示条，建议照做——见下方"固定地址"。

---

## 配对与鉴权

纯 HTTP 意味着任何在同一 Wi-Fi 下的设备都能访问到这个端口，所以 VibeCast 用一个**配对码（pairing token）**做访问控制：

- 首次启动时随机生成，存在 `%APPDATA%\VibeCast\config.json` 里的 `PairingToken`。
- 托盘弹出的二维码 / 连接链接会自动带上 `?token=...`；手机扫码后 token 存入浏览器 `localStorage`，以后打开无需重新输入。
- 服务器对 **WebSocket 连接**和 **`/api/config` 接口**都校验这个 token，拿不到或不对会被拒绝（手机上会看到"配对码不正确"提示）。
- 想让某台已配对的手机失效：删除 `config.json` 重启程序会生成新 token（所有旧链接失效，需要重新扫码）。

> 这只是"同一局域网内的访问门槛"，不是端到端加密——请仍确保 Wi-Fi 本身可信（不要在完全公开、不设密码的网络上用）。

---

## 固定地址（不用每次扫码）

VibeCast 的连接链接里带着电脑当前的**局域网 IP**，如果路由器用 DHCP 重新分配了 IP，旧链接就会失效。两步让它一劳永逸：

1. **给电脑的 IP 做 DHCP 保留**（一次性，路由器上设置）：登录路由器管理页 → 找"DHCP 保留 / 静态地址分配 / Address Reservation"，把电脑的 MAC 地址绑定一个固定 IP。绑定后这台电脑的地址永远不变，链接自然也就固定了。不同品牌路由器叫法不同，具体参考路由器说明书。
2. **手机浏览器"添加到主屏幕"**：扫码打开页面后，用浏览器菜单的"添加到主屏幕"（或页面顶部的一次性提示条），存成一个图标。以后不用扫码，直接点图标打开——配对码已经存在 `localStorage` 里，图标点开即用。

> **代理/VPN 软件的影响**：DHCP 保留的 IP 本身不受代理软件影响（那是路由器层面的分配，和电脑/手机跑不跑代理无关）。真正可能出问题的是**连接这一步**——如果手机或电脑任一端开着 Clash、V2ray、Surge 这类工具的"系统代理/TUN 模式"，且没有把局域网网段（`192.168.0.0/16`、`10.0.0.0/8` 等）设为直连，访问局域网 IP 的请求会被一起塞进代理隧道，导致连不上或很慢。主流工具通常默认内置局域网直连规则，但如果你自定义过配置文件，遇到连不上时可以先检查这一项。

---

## 防火墙

若手机打不开网页，多半是 Windows 防火墙拦了入站。用**管理员** PowerShell 运行：
```powershell
powershell -ExecutionPolicy Bypass -File scripts\add-firewall-rule.ps1
# 端口非默认时： -Port 9000
```
（首次运行程序时，Windows 也可能弹出防火墙提示，勾选"专用网络"允许即可。）

---

## 发送行为：Replace / Enter 开关

手机页文本框下方有两个小开关（不是必选项，默认都关，直接发送即可）：

- **Replace（替换）**：关闭时在光标处**插入**文字（适合 Obsidian、Notion、笔记，默认）；打开则先全选**替换**整个输入框（适合已有旧内容的聊天框 / 提示词框）。
- **↵ Enter（回车）**：打开后发送成功会自动按一次回车，适合"打完直接发送"的聊天场景；关闭（默认）只插入文字，不提交。

这两个开关记在**这台手机这个浏览器**的本地存储里，跟哪个电脑无关，也不需要像"选目标应用"那样每次点选——设一次，之后照旧。

托盘"打开配置页"（会自动带上配对码）里只剩下 PC 侧的行为设置：发送后是否弹托盘提示、按键间隔（毫秒，卡字时调大）。配置保存在 `%APPDATA%\VibeCast\config.json`，改动即时生效。

---

## 目标可见 + 撤销

为了让"盲发"变成"看得见、敢信":

- **实时显示目标**：手机页面顶部会显示 PC 当前聚焦窗口的标题（"→ 将输入到：无标题 - 记事本"），电脑切换焦点时手机上会跟着更新。发送前先确认这一行，避免字打错地方。
- **注入结果里带真实目标**：发送成功后手机提示会附带"实际敲进了哪个窗口标题"，而不是简单的"发送成功"。
- **撤销**：发送成功后出现"撤销"按钮，点击会发送等量退格，删掉刚刚敲入的文字。
  - 限制：打开了"Replace"开关时会先全选再替换,撤销只能删掉 VibeCast 自己打的新内容,**不能恢复被替换掉的原内容**。
  - 限制：如果打开了"↵ Enter"开关（自动回车，比如聊天框已经发出消息），撤销无效——消息可能已经提交，此时不提供撤销按钮。

---

## 文字注入的实现

见 [`TextInjector`](src/VibeCast/Injection/TextInjector.cs)：

1. 默认策略 = **Unicode 按键注入**（`SendInput` + `KEYEVENTF_UNICODE`，所有按键打包成一次原子调用）。
   它确定性强、**不碰用户剪贴板**、完美支持中文与 emoji（代理对按 UTF-16 码元分别发送，由 SendInput 重组），
   对 Notepad / 聊天框 / Electron 应用都可靠。Dialog 模式先 `Ctrl+A` 全选再输入以整体替换。
2. 可选 **剪贴板粘贴**（配置项 `UseClipboard=true`）：适合超长文本或极少数忽略合成按键的控件；
   粘贴后**延迟**再还原剪贴板，避免"还原早于粘贴"的竞态。
3. `Submit` 时补发一次回车。
4. **撤销**：记录本次实际输入的字符数，收到 `undo` 消息时发送等量 Backspace。

> 实现要点：Win32 `INPUT` 结构体的 union 必须按最大成员 `MOUSEINPUT` 对齐，否则 x64 上
> `Marshal.SizeOf<INPUT>()` 偏小、`cbSize` 不匹配，`SendInput` 会**静默失败**（返回 0 却不报错）。
>
> 未来可增强：接入 UI Automation `ValuePattern.SetValue` 做更"干净"的写入（当控件支持时）。

---

## 项目结构

```
VibeCast.sln
src/VibeCast/
  Program.cs                 应用入口（单实例 + 启动服务器 + 托盘消息循环）
  app.manifest               asInvoker / DPI 感知
  Config/                    AppConfig（含 PairingToken）、ConfigStore（%APPDATA% JSON）
  Net/NetworkInfo.cs         局域网 IPv4 枚举
  Injection/                 NativeMethods(SendInput)、TextInjector(按键/剪贴板/撤销)、ForegroundWindowInfo(前台窗口标题)、InjectMode
  Server/                    Protocol、WebSocketHub(注入+撤销+目标广播+鉴权)、WebServer(Kestrel+静态站+REST+WS)
  UI/                        TrayApplicationContext(托盘)、ConnectionForm(二维码)
  Infrastructure/UiDispatcher.cs  后台线程 → STA UI 线程 的调度
  wwwroot/                   手机网页：index/app/pairing/style/i18n + config 页（编译时嵌入 exe，无目标下拉框）
shared/protocol.md           WebSocket / REST 协议
scripts/add-firewall-rule.ps1  一键放行防火墙
```

---

## 常见问题

- **手机扫码打不开**：多为防火墙（见上）或不在同一 Wi-Fi；有 AP 隔离的公共/访客网络会互相不可见；也可能是手机或电脑开着代理/VPN 的全局模式没有绕过局域网网段（见"固定地址"一节）。
- **添加到主屏幕后突然连不上了**：电脑的局域网 IP 变了——去路由器设置 DHCP 保留（见"固定地址"），或者重新扫一次码。
- **"配对码不正确" / "缺少配对码"**：链接过期或被手动改过——回到 PC 托盘重新双击弹出二维码，重新扫描。
- **点了发送但电脑没反应**：发送前要先用鼠标点中目标输入框；也可以看手机页面顶部"→ 将输入到"是否显示了预期窗口。
- **没有语音按钮**：VibeCast 页面本身没有语音按钮——用你手机输入法自带的麦克风键。如果输入法没有语音功能，直接打字。
- **中文变乱码或掉字**：把配置页里的"按键间隔"调大（如 40–60ms）。
- **端口 8443 被占用**：`config.json` 改 `Port` 后重启程序，并对应调整防火墙端口。

---

## 许可

参考原项目采用 MIT。本移植版按需自行选择许可协议。
