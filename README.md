# EndfieldCharge · 插件驱动的桌面灵动岛

插上 / 拔掉充电器时，从屏幕顶部弹出一块"灵动岛"式 HUD，显示当前电量（mWh 与百分比）。
项目已从单体应用重构为**插件驱动**架构，HUD 本身是一个**四态灵动岛**，视觉与动画风格复刻《终末地》工业 / 超充模式 HUD。

- **插件驱动**：`src/EndfieldCharge.Contracts`（BCL-only）与 `src/EndfieldCharge.Contracts.Avalonia` 定义插件与皮肤契约，`Host/` 承载岛、插件注册表、外部插件加载器（每插件独立 `AssemblyLoadContext`）与菜单合成；电池充电检测降为一个不可卸载的元插件，其它插件只提供数据即可借用它的样式在岛上展示内容。音乐岛是**项目内的外部插件子项目**（`plugins/`），宿主只按契约认识它。
- **四态灵动岛**：响应态 → 等待态 → 收缩态 → 隐藏态；鼠标移到屏幕顶缘细条停留即可再次展开。

## 下载安装

从 [Releases](https://github.com/Lenkmat/endfield-charge/releases) 下载：

| 文件 | 说明 |
|------|------|
| `EndfieldCharge-x.y.z-setup.exe` | Inno Setup 安装版（中文/英文向导，可选桌面快捷方式与开机自启） |
| `EndfieldCharge-x.y.z-portable.zip` | 便携版，解压即用 |

## 功能

| 功能 | 说明 |
|------|------|
| 电量显示 | 剩余 / 满充容量（mWh，整数）与百分比，读取 `CallNtPowerInformation`，WMI 兜底 |
| 电源监听 | `RegisterPowerSettingNotification` 订阅 GUID_ACDC_POWER_SOURCE，2s 轮询兜底，400ms 双向去抖（过滤 Windows 满电瞬时抖动） |
| 插件驱动架构 | `src/EndfieldCharge.Contracts` 定义插件契约（`IPlugin` / `IPluginContext` / `IIslandContentProvider` / `IContextMenuContributor`），`src/EndfieldCharge.Contracts.Avalonia` 定义皮肤契约（`IIslandSkin` / `IIslandHost` / `IIslandExpandToggle`）；`Host/Plugins/PluginLoader` 从 exe 旁 `plugins/*.dll` 加载外部插件（每插件独立 ALC，契约共享默认上下文） |
| 音乐岛（外部插件） | `plugins/EndfieldCharge.Plugin.Music`：SMTC **事件驱动**（`SessionsChanged` / `CurrentSessionChanged` + 会话的媒体属性 / 播放态 / 时间线事件），不轮询；无会话显示空态、连接失败降级为「未连接 SMTC」并慢速自愈；歌词走**多源并行择优**（LRCLIB + 网易云同时探测，汇总取最相似的一条，并丢弃「纯音乐，请欣赏」这类占位），长句自动跑马灯；音乐开始播放自动以**展开态**弹出（左键点岛即隐藏；**受「音乐来源白名单」约束**，名单为空时不会自动弹岛），岛内按钮可播放/暂停与切歌 |
| 插件设置 | 设置窗口「插件」页由插件自绘（`IPluginSettingsPage` 契约）。音乐插件提供：展开态超时（最低 3s）、无歌词时显示歌名、显示可视化器、歌词来源（**并行择优**：三源同时检索取最匹配；偏好 LRCLIB / 偏好网易云 / 偏好本地：串行回退；仅 LRCLIB / 仅网易云 / 仅本地；关闭歌词）、音乐来源白名单（按 AUMID，可手动添加或点选「见过的来源」，列表每秒刷新；**默认空 ⇒ 不采集频谱也不自动弹岛**）；设置持久化在 `%APPDATA%\EndfieldCharge\plugins\<id>\settings.json`，改动即生效 |
| 滚轮切换岛 | 按「翻页」语义：每滚一档 = 翻一页 = 切一个岛（上滚上一页 / 下滚下一页），不做手势判定；边界行为可在设置里配置（循环 / 到边界即停 / 禁用）；皮肤把 `ParticipatesInWheelSwitch` 覆写为 `false` 即被排除出轮转 |
| 电池元插件 | 电池充电检测实现为不可卸载的元插件（`CanUnload=false`），同时提供岛样式（`IIslandSkin`）与电池数据（`IIslandContentProvider`）；其它插件只提供数据即可借用该样式在岛上展示内容，无需接触 Avalonia |
| 四态灵动岛 | 响应态（完整「超充模式」入场动画，固定时长）→ 等待态（电量胶囊）→ 收缩态（宽度约 200，仅电池环 + 百分比）→ 隐藏态；鼠标移到屏幕顶缘细条停留约 300ms 展开回等待态 |
| 低电量变色 | 电量 < 20% 时黄绿电量圈变红（#FF4D4F） |
| 提醒通知 | 低电量提醒（阈值可调 5–40%）与充满提醒（≥99%），卡牌风格弹窗，4s 自动消失 |
| 设置窗口 | 通用 / 动画 / 插件 / 通知 / 关于五个页签；全局缩放（0.4–1.2）、窗口置顶、滚轮切换岛行为（循环 / 到边界即停 / 禁用）、等待态超时、收缩态超时、HUD 位置（顶部居中/靠右/靠左）、显示器选择、语言、开机自启，保存即生效并持久化 |
| 托盘菜单 | 右键使用 Win32 原生菜单（显示 / 设置 / 置顶 / 插件▸ / 关于 / 退出） |
| 岛右键菜单 | 自绘菜单（illogical-impulse 风格：`#201F20`、圆角 12、1px 描边、阴影），窗口 `WS_EX_NOACTIVATE` 永不激活，配合 `WH_MOUSE_LL` 全局鼠标钩子检测外部点击关闭；菜单项 = 插件注入项 + 宿主固定项（窗口置顶 / 免打扰▸ / 插件▸ / 设置 / 退出）；**任意带子项的条目**都支持悬停展开子菜单 |
| 插件菜单注入 | 插件实现 `IContextMenuContributor` 即可向右键菜单注入条目，按 Target → Section → Priority 聚合；音乐插件即以此提供「上一曲 / 下一曲 / 暂停·播放」 |
| 点击穿透 | 窗口整窗 `WS_EX_TRANSPARENT` + 30ms 光标轮询；光标在岛区域时临时取消穿透使其可交互，其余区域点击穿透到桌面 / 其它窗口 |
| 状态生命周期超时 | **每个状态都有自己的超时，与唤醒方式无关**（托盘 / 插拔电 / 滚轮翻页 / 内容主动展开都一样）：展开态 → 等待态（**最低 3s**，时长由皮肤自报，音乐插件把它做成自己的设置）、等待态 → 收缩态、收缩态 → 隐藏态（后两者在设置→动画页配置）；鼠标悬停在岛区域内会重置计时 |
| 窗口置顶 | 设置窗口与岛右键菜单均可切换窗口置顶 |
| 动画微调 | 设置窗口「动画」页实时预览并微调时长 / 回弹 / 波纹参数，保存即生效并持久化 |
| 节能模式提示 | 开 / 关节能（省电）模式时弹出对应 HUD。24H2+（build 26100+）订阅 GUID_ENERGY_SAVER_STATUS 通知、轮询注册表 EnergySaverState；旧系统用 GUID_POWER_SAVING_STATUS + SystemStatusFlag。设置「通知」页可开关 |
| 检查更新 | 读取 GitHub Releases API，比较程序集版本，一键跳转下载页 |
| 多语言 | 中文 / 英文，默认跟随系统，可在设置中手动切换 |
| 开机自启 | 设置窗口「通用」页开关，写 `HKCU\...\CurrentVersion\Run`（当前用户级，无需管理员） |
| 统一图标 | 托盘 / 各窗口 / exe / 安装器 / 卸载器统一使用 `Assets\tray_bolt` 图标 |
| 日志 | `%TEMP%\EndfieldCharge\log-YYYYMMDD.txt`，方便排查托盘菜单定位等问题 |

## 运行要求

- Windows 10 1809+ / Windows 11
- .NET 8 运行时（Release 为框架依赖单文件发布，需安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)）
- x64

## 构建

```bash
# 调试
dotnet build -c Debug

# 发布（单文件 exe，输出到 publish/）
dotnet publish -c Release -o publish

# 本地打安装包（需安装 Inno Setup，iscc 在 PATH 中）
iscc installer\EndfieldCharge.iss
```

> 注意：`PublishSingleFile` 只把托管 dll 打进 exe，SkiaSharp 的 native dll
> （libSkiaSharp / libHarfBuzzSharp / av_libglesv2）仍需与 exe 同目录 ——
> 便携分发请打包整个 `publish/` 目录，不要只拷 exe。
>
> 外部插件由 `CopyPluginsToPublish` 产出到 `publish/plugins/`，**必须与 exe 一同分发**
> （宿主从 exe 旁的 `plugins/` 目录加载）；只拷 exe 会没有音乐岛。
>
> 纯逻辑单元测试与格式基线（CI 都会跑）：`dotnet test tests\EndfieldCharge.Tests`、
> `dotnet format EndfieldCharge.csproj --verify-no-changes`。

### CI / 发布（GitHub Actions）

**CI**：推送到 `main`（以及 PR）会自动构建安装包与便携版 zip，Actions 页面可下载 artifact。
包内**已包含外部插件**（`plugins/EndfieldCharge.Plugin.Music.dll`，由
`EndfieldCharge.csproj` 的 `CopyPluginsToPublish` 放进 `publish/plugins/`）。

**Release 不随标签自动发布**，必须手动触发：

1. 打标签并推送（标签由维护者创建）：`git tag v1.0.0` + `git push origin v1.0.0`
2. GitHub → Actions → `Build Windows Installer` → **Run workflow**
3. 运行目标选该 `v*` 标签，并勾选 `publish_release`
4. 仅当「手动触发 + 勾选 + 运行在 `v*` 标签上」三者同时满足才创建 Release，
   并把标签版本号写入程序集版本与安装包文件名；`main` 的流水版本（`0.0.<run_number>`）
   只用于 Artifact，不会进入 Release

## 调试参数

启动时追加参数，无需真的插拔电源：

| 参数 | 作用 |
|------|------|
| `--demo` | 用示例数据播放响应动画后**停在等待态** |
| `--preview` | 用本机真实电池数据播放响应动画后**停在等待态** |
| `--preview-unplug` | 用示例数据播放响应动画后**停在等待态** |
| `--debug-ring` | 静态呈现电量态 1.5s |
| `--show-fps` | 右下角显示 FPS 计数器（排查动画性能） |
| `--power-log` | 输出电源事件日志到 `%TEMP%\power-log.txt` |
| `--demo-music` | 直接进入音乐岛等待态（外部插件 `plugins/EndfieldCharge.Plugin.Music`） |

> 注意：这几个参数现在会停在等待态（不再自动消失）；参数互斥，按 `--demo` → `--preview-unplug` → `--preview` → `--demo-music` 的优先级生效。

## 项目结构

```
EndfieldCharge/
├─ EndfieldCharge.csproj                 # 宿主唯一可执行项目（DefaultItemExcludes 排除 src/、plugins/、tests/ 等）
├─ .editorconfig                         # 格式基线（CI 用 dotnet format 校验）
├─ .githooks/commit-msg                  # Angular 提交规范钩子（git config core.hooksPath .githooks）
├─ CONTRIBUTING.md                       # 贡献指南（英文）
├─ src/
│  ├─ EndfieldCharge.Contracts/          # 插件契约（BCL-only，不引用 Avalonia）
│  │  ├─ IPlugin.cs                      # Id / DisplayName / CanUnload / Initialize / Shutdown
│  │  ├─ IPluginContext.cs               # DataDirectory + GetService<T>()
│  │  ├─ IIslandContentProvider.cs       # 岛内容数据契约
│  │  ├─ IslandContentDescriptor.cs      # 内容描述符（Title/TagLine/Value/Unit/Percent/RingFraction/Tone/PlayKind）
│  │  ├─ MenuContribution.cs             # IContextMenuContributor + MenuContribution（插件注入右键菜单）
│  │  ├─ Localization.cs                 # 多语言（运行时切换）
│  │  └─ Logger.cs                       # 文件日志（%TEMP%\EndfieldCharge\）
│  └─ EndfieldCharge.Contracts.Avalonia/ # 皮肤契约（唯一依赖 Avalonia 的契约程序集）
│     ├─ IIslandSkin.cs                  # 四态播放 + 尺寸 + 4 个可选特性（ParticipatesInWheelSwitch / UsesHostContent / ExpandedTimeoutSeconds / WindowHeight）
│     ├─ IIslandExpandToggle.cs          # 可选能力：岛内「展开 / 收起」按钮
│     ├─ IIslandHost.cs                  # 宿主服务：当前皮肤 / 轮转列表 / 切换 / ShowExpanded
│     ├─ IslandMetrics.cs                # 岛尺寸（命中区 / 菜单锚点）
│     ├─ DesignTokens.cs  AnimationPrimitives.cs  RingGeometry.cs
│     └─ Styles/Geometries.axaml         # Material 图标几何资源
├─ plugins/
│  └─ EndfieldCharge.Plugin.Music/       # 外部插件（项目内子项目，不单独建仓库）
│     ├─ MusicPlugin.cs                  # IPlugin + IIslandSkin + IIslandExpandToggle + IContextMenuContributor
│     ├─ MusicIslandView.axaml(.cs)      # 音乐岛三态视觉树与动画
│     ├─ SmtcMediaSource.cs              # SMTC 事件驱动数据源（连接重试 / 降级 / 本地进度插值）
│     └─ MusicFrame.cs                   # 一帧内容（含空态 / 降级态）
├─ tests/EndfieldCharge.Tests/           # xUnit：纯逻辑测试（IslandStateMachine / RingGeometry 等）
├─ Host/
│  ├─ Island/
│  │  ├─ IslandVisualState.cs            # 四态枚举（响应 / 等待 / 收缩 / 隐藏）
│  │  └─ IslandStateMachine.cs           # 四态状态机（纯逻辑、可测）
│  ├─ Plugins/
│  │  ├─ IPluginRegistry.cs  PluginRegistry.cs   # 进程内注册表（CanUnload==false 的插件拒绝移除）
│  │  ├─ PluginLoader.cs  PluginContext.cs       # 外部 DLL 加载（每插件独立 ALC）+ 每插件上下文
│  │  └─ Battery/
│  │     ├─ BatteryPlugin.cs             # 电池元插件（CanUnload=false，兼 IIslandSkin + IIslandContentProvider）
│  │     ├─ BatteryIslandView.axaml(.cs) # 岛的视觉树 + 四态动画播放
│  │     └─ BatteryAnimationTheme.cs     # 电池动画主题
│  └─ Menu/
│     └─ PluginMenuComposer.cs           # 按 Target → Section → Priority 聚合菜单
├─ Services/
│  ├─ AutoStart.cs               # 开机自启（HKCU Run 键读写）
│  ├─ BatteryService.cs          # 电池快照（剩余/满充 mWh、百分比、AC 状态）
│  ├─ PowerNative.cs             # P/Invoke：powrprof、message-only 窗口
│  ├─ PowerWatcher.cs            # 电源变化监听 + 去抖确认
│  └─ UpdateChecker.cs           # GitHub Releases 更新检查
├─ Settings/
│  ├─ AppSettings.cs             # 设置模型（缩放/动画微调/超时/位置/显示器/语言/提醒）
│  ├─ SettingsManager.cs         # 设置加载与持久化
│  └─ SettingsWindow.axaml(.cs)  # 设置窗口（通用 / 动画 / 通知 / 关于）
├─ Views/
│  ├─ HudWindow.axaml(.cs)               # HUD 窗口外壳（透明 / 点击穿透 / 定位 / 状态机驱动 / 皮肤轮转）
│  ├─ IslandContextMenuWindow.axaml(.cs) # 岛自绘右键菜单（非激活 + 鼠标钩子）
│  └─ TrayMenuWindow.axaml(.cs)          # 自定义托盘菜单窗口（左键改为显示岛，保留备用）
├─ Styles/HudTheme.axaml         # 颜色主题（图标几何已移入 Contracts.Avalonia）
├─ Assets/                       # tray_bolt.png（运行时图标）+ tray_bolt.ico（exe/安装器图标）
├─ installer/
│  ├─ EndfieldCharge.iss         # Inno Setup 安装脚本（打包整个 publish/，含 plugins/）
│  └─ Languages/                 # 中文本地化（随仓库分发）
└─ .github/                      # CI（构建 Artifact，含插件；Release 手动触发）+ PR / issue 模板
```

## 动画实现要点

- HUD 是一个四态状态机（响应态 → 等待态 → 收缩态 → 隐藏态），流转与超时由纯逻辑的 `IslandStateMachine` 驱动，便于测试。
- 响应态入场动画**时长固定**，不再随设置伸缩。
- 收缩 / 隐藏时胶囊缩小并向上「吸」出屏幕；展开是收缩的**倒放**，并叠加从上方滑入（Y -110 → 0）。入场前先铺好起始态再显示，避免闪跳。
- Avalonia 11 的 `KeyFrame` 使用 **`KeySpline`（贝塞尔控制点）** 做逐段缓动，多关键帧下 `Animation.Easing` 不生效，每段必须显式指定 `KeySpline`，否则该段为线性。
- `Border.HeightProperty`（即 `Layoutable.HeightProperty`）可直接动画，因此胶囊高度的 `60 → 90 → 60` 用独立轨道驱动。
- 收尾「整体缩小关没」由外层 `ScaleHost` 的 `RenderTransform` 统一缩放，胶囊本身宽度不动。

## 贡献

见 [CONTRIBUTING.md](CONTRIBUTING.md)：构建与验证流程、提交信息规范（Angular，英文）、PR 约定（squash）、代码与插件规范、AI 辅助贡献政策。

## 鸣谢

- [Avalonia](https://avaloniaui.net/) — 跨平台 .NET UI 框架。
- [Lenkmat/endfield-charge](https://github.com/Lenkmat/endfield-charge) — 原始的终末地风格电量 HUD（本项目的前身）。
- [end-4/dots-hyprland](https://github.com/end-4/dots-hyprland)（illogical-impulse）— 右键菜单与弹出面板的视觉/动效设计参考。
- 计划中参考 / 将来集成的开源项目：
  - [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — WMI 温度/硬件传感器
  - [cava](https://github.com/karlstav/cava) — 音频频谱可视化（MIT）
  - [localsend-go](https://github.com/meowrain/localsend-go) — LocalSend 命令行客户端
  - [LRCLIB](https://github.com/tranxuanthang/lrclib) — 开放式歌词服务（MIT）
  - [Lyricify-Lyrics-Helper](https://github.com/WXRIW/Lyricify-Lyrics-Helper) — LRC 解析参考（Apache-2.0）
  - [Waylyrics](https://github.com/waylyrics/waylyrics) — 桌面歌词参考（MIT）
  - [DropIt](https://sourceforge.net/projects/dropit/) — 规则化文件归档**概念**参考（GPL；仅借鉴概念，未引入其代码）

## 许可证

MIT
