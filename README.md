# QuickMix — 快捷音量合成器

Windows 上按应用调音量的小工具。打开就是一张列表，每个有声音的应用一行，拖滑条就改它自己的音量 —— 不用再右键托盘喇叭、进"音量合成器"、翻半天。

单文件 exe，零依赖，不装任何东西。

---

## 怎么用

双击 `QuickMix.exe`，窗口弹出来：

- 每一行 = 一个应用：图标、名字、当前百分比、滑条
- **拖滑条**调音量；拖到 0 就是没声，不会静音别的
- **右键**那一行 = 静音 / 取消静音
- **双击**整张卡片任意位置 = 直接拉满 100%
- 名字下面那条细光是**实时电平**，能看出它此刻在不在出声
- 你刚才在用的那个应用会排在列表第一行（只是排序，没有别的高亮）
- 调完点右上角 × 关掉就行，它不常驻

窗口默认置顶（右上角「置顶」可以取消），列表超过一屏时右侧会出现滚动条。

## 后台常驻模式（可选）

不想每次都点开，就带 `--tray` 启动：

```
QuickMix.exe --tray
```

它会缩进托盘，右键托盘图标：

- **打开音量面板**（全局快捷键，默认 `Ctrl + Alt + V`）
- **快捷键** —— 换成 `Ctrl+Alt+M` / `Ctrl+Alt+`` ` / `Win+Alt+V`，选完立刻生效，记在 `%APPDATA%\QuickMix\settings.ini`
- **开机自动启动** —— 写 `HKCU\...\Run`
- **退出**

常驻时窗口关掉只是收起来，不会退出进程；面板不显示时后台不刷新电平，基本不占 CPU。

> 快捷键若和别的软件冲突，注册会失败并弹气泡提示，换个组合就行。

## 命令行

脚本里也能用：

```
QuickMix.exe --dump                    # 列出所有会话和音量，写到 %TEMP%\quickmix-dump.txt
QuickMix.exe --set  msedge 35          # 把 msedge 调到 35%
QuickMix.exe --mute msedge 1           # 静音（0 = 取消）
```

`--set` / `--mute` 的目标可以写 pid、进程名（`msedge` 或 `msedge.exe`），也可以写窗口标题的一段。

## 实现

- **音频**：Core Audio，`MMDeviceEnumerator` → `IAudioSessionManager2` → 逐会话 `IAudioSessionControl2.GetProcessId` 聚合；音量读写走 `ISimpleAudioVolume`，电平走 `IAudioMeterInformation`。所有 COM 接口方法都带 `[PreserveSig]` 直接返回 HRESULT，不靠异常判错。
- **"你刚才在用的应用"**：启动时按 Z 序扫顶层窗口，跳过桌面/任务栏和 UWP 宿主壳，第一个命中音频会话的就是它；UWP 应用会从 `ApplicationFrameHost` 里挖出真正的 pid。
- **界面**：WinForms 自绘（`AudioRow` 一行一个控件，双缓冲），进程 DPI 感知，标题栏深色由 `DwmSetWindowAttribute` 设。
- **占用**：无窗口时不刷新、不轮询音频；窗口开着时 120ms 刷一次电平，约 1.4s 比对一次会话列表，变了才重建行。

## 目录

```
QuickMix/
  src/QuickMix.cs     单文件源码
  build.cmd           调 csc 编译（.NET Framework 4.x，无需 SDK）
  bin/QuickMix.exe    产物
```

重编：

```
cd QuickMix
build.cmd
```

## 已知边界

- 只跟**默认播放设备**（eMultimedia）上的会话打交道，和 Windows 自带合成器一个毛病；临时切了输出设备的话，那边的会话这里看不见。
- 系统声音（"Windows 音频"那一路）不列出来，本来也没人单独调它。
- 电平条是峰值，不做响度。
