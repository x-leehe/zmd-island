# EndfieldCharge · 插件驱动的桌面灵动岛

插上 / 拔掉充电器时，从屏幕顶部弹出一块"灵动岛"式 HUD，显示当前电量（mWh 与百分比）。
项目已从单体应用重构为**插件驱动**架构，HUD 本身是一个**四态灵动岛**，视觉与动画风格复刻《终末地》工业 / 超充模式 HUD。

- **插件驱动**：`Contracts/` 定义插件契约（BCL-only，不引用 Avalonia），`Host/` 承载岛、插件注册表与菜单合成；电池充电检测降为一个不可卸载的元插件，其它插件只提供数据即可借用它的样式在岛上展示内容。
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
| 插件驱动架构 | `Contracts/` 定义插件契约（`IPlugin` / `IPluginContext` / `IIslandContentProvider` / `IContextMenuContributor`），`Host/` 承载岛与插件运行时；插件可提供岛内容、注入右键菜单 |
| 电池元插件 | 电池充电检测实现为不可卸载的元插件（`CanUnload=false`），同时提供岛样式（`IIslandSkin`）与电池数据（`IIslandContentProvider`）；其它插件只提供数据即可借用该样式在岛上展示内容，无需接触 Avalonia |
| 四态灵动岛 | 响应态（完整「超充模式」入场动画，固定时长）→ 等待态（电量胶囊）→ 收缩态（宽度约 200，仅电池环 + 百分比）→ 隐藏态；鼠标移到屏幕顶缘细条停留约 300ms 展开回等待态 |
| 低电量变色 | 电量 < 20% 时黄绿电量圈变红（#FF4D4F） |
| 提醒通知 | 低电量提醒（阈值可调 5–40%）与充满提醒（≥99%），卡牌风格弹窗，4s 自动消失 |
| 设置窗口 | 全局缩放（0.4–1.2）、等待态超时、收缩态超时、HUD 位置（顶部居中/靠右/靠左）、显示器选择、语言、开机自启、窗口置顶，保存即生效并持久化 |
| 托盘菜单 | 右键使用 Win32 原生菜单（显示 / 设置 / 置顶 / 插件▸ / 关于 / 退出） |
| 岛右键菜单 | 自绘菜单（illogical-impulse 风格：`#201F20`、圆角 12、1px 描边、阴影），窗口 `WS_EX_NOACTIVATE` 永不激活，配合 `WH_MOUSE_LL` 全局鼠标钩子检测外部点击关闭；菜单项 = 插件注入项 + 宿主固定项（窗口置顶 / 插件▸ / 设置） |
| 插件菜单注入 | 插件实现 `IContextMenuContributor` 即可向右键菜单注入条目，按 Target → Section → Priority 聚合；演示插件 `MusicDemoPlugin` 演示该能力 |
| 点击穿透 | 窗口整窗 `WS_EX_TRANSPARENT` + 30ms 光标轮询；光标在岛区域时临时取消穿透使其可交互，其余区域点击穿透到桌面 / 其它窗口 |
| 等待态 / 收缩态超时 | 等待态与收缩态分别有可配置超时 T1 / T2，超时后依次进入收缩态与隐藏态；鼠标悬停在岛区域内始终保持等待态，不会在悬停时收缩 |
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
| `--power-log` | 输出电源事件日志到 `%TEMP%\power-log.txt` |

> 注意：这几个参数现在会停在等待态（不再自动消失）；参数互斥，按 `--demo` → `--preview-unplug` → `--preview` 的优先级生效。

## 项目结构

```
EndfieldCharge/
├─ Contracts/                            # 插件契约（BCL-only，不引用 Avalonia）
│  ├─ IPlugin.cs                         # Id / DisplayName / CanUnload / Initialize / Shutdown
│  ├─ IPluginContext.cs
│  ├─ IIslandContentProvider.cs          # 岛内容数据契约
│  ├─ IslandContentDescriptor.cs         # 内容描述符（Title/TagLine/Value/Unit/Percent/RingFraction/Tone/PlayKind）
│  └─ MenuContribution.cs                # IContextMenuContributor + MenuContribution（插件注入右键菜单）
├─ Host/
│  ├─ Island/
│  │  ├─ IIslandSkin.cs                  # 皮肤契约：BindContent + PlayResponse / PlayWaiting / PlayContract / PlayDismiss + ApplyScale
│  │  ├─ IslandVisualState.cs            # 四态枚举（响应 / 等待 / 收缩 / 隐藏）
│  │  └─ IslandStateMachine.cs           # 四态状态机（纯逻辑、可测）
│  ├─ Plugins/
│  │  ├─ IPluginRegistry.cs  PluginRegistry.cs   # 进程内注册表（CanUnload==false 的插件拒绝移除）
│  │  ├─ Battery/
│  │  │  ├─ BatteryPlugin.cs             # 电池元插件（CanUnload=false，兼 IIslandSkin + IIslandContentProvider）
│  │  │  ├─ BatteryIslandView.axaml(.cs) # 岛的视觉树 + 四态动画播放
│  │  │  └─ BatteryAnimationTheme.cs     # 原 Animations/HudAnimations.cs
│  │  └─ Demo/
│  │     └─ MusicDemoPlugin.cs           # 演示插件：向右键菜单注入条目
│  └─ Menu/
│     └─ PluginMenuComposer.cs           # 按 Target → Section → Priority 聚合菜单
├─ Services/
│  ├─ AutoStart.cs               # 开机自启（HKCU Run 键读写）
│  ├─ BatteryService.cs          # 电池快照（剩余/满充 mWh、百分比、AC 状态）
│  ├─ Logger.cs                  # 文件日志（%TEMP%\EndfieldCharge\）
│  ├─ PowerNative.cs             # P/Invoke：powrprof、message-only 窗口
│  ├─ PowerWatcher.cs            # 电源变化监听 + 去抖确认
│  └─ UpdateChecker.cs           # GitHub Releases 更新检查
├─ Settings/
│  ├─ AppSettings.cs             # 设置模型（缩放/动画微调/超时/位置/显示器/语言/提醒）
│  ├─ SettingsManager.cs         # 设置加载与持久化
│  ├─ SettingsWindow.axaml       # 设置窗口（通用 / 动画 / 通知 / 关于）
│  └─ SettingsWindow.axaml.cs
├─ Views/
│  ├─ HudWindow.axaml(.cs)               # HUD 窗口外壳（透明 / 点击穿透 / 定位 / 状态机驱动）
│  ├─ IslandContextMenuWindow.axaml(.cs) # 岛自绘右键菜单（非激活 + 鼠标钩子）
│  └─ TrayMenuWindow.axaml(.cs)          # 自定义托盘菜单窗口（左键改为显示岛，保留备用）
├─ Styles/                       # 颜色主题与图标几何（StreamGeometry）
├─ Assets/                       # tray_bolt.png（运行时图标）+ tray_bolt.ico（exe/安装器图标）
├─ installer/
│  ├─ EndfieldCharge.iss         # Inno Setup 安装脚本
│  └─ Languages/                 # 中文本地化（随仓库分发）
└─ .github/workflows/            # CI：自动构建 + 打标签发 Release
```

## 动画实现要点

- HUD 是一个四态状态机（响应态 → 等待态 → 收缩态 → 隐藏态），流转与超时由纯逻辑的 `IslandStateMachine` 驱动，便于测试。
- 响应态入场动画**时长固定**，不再随设置伸缩。
- 收缩 / 隐藏时胶囊缩小并向上「吸」出屏幕；展开是收缩的**倒放**，并叠加从上方滑入（Y -110 → 0）。入场前先铺好起始态再显示，避免闪跳。
- Avalonia 11 的 `KeyFrame` 使用 **`KeySpline`（贝塞尔控制点）** 做逐段缓动，多关键帧下 `Animation.Easing` 不生效，每段必须显式指定 `KeySpline`，否则该段为线性。
- `Border.HeightProperty`（即 `Layoutable.HeightProperty`）可直接动画，因此胶囊高度的 `60 → 90 → 60` 用独立轨道驱动。
- 收尾「整体缩小关没」由外层 `ScaleHost` 的 `RenderTransform` 统一缩放，胶囊本身宽度不动。

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
