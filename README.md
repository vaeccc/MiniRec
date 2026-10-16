# MiniRec

**轻量级 Windows 托盘录音工具。**

MiniRec 支持录制麦克风、电脑播放的声音，或将两路声音混合录制。启动后驻留系统托盘，无需一直打开主窗口，可通过托盘菜单或全局快捷键快速开始与停止录音。

[![Windows Build](https://github.com/vaeccc/MiniRec/actions/workflows/build.yml/badge.svg)](https://github.com/vaeccc/MiniRec/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> **当前为开发预览版**：Windows 自动编译、打包和启动检查已接入，但真实音频设备、混音同步及长期运行仍需进一步测试。暂未提供正式安装程序。

## 功能

- **三种录音模式**：麦克风、电脑声音（WASAPI Loopback）、混合录音。
- **快捷操作**：托盘左键可快速开始/停止；右键可切换录音模式、打开保存目录和设置。
- **全局快捷键**：分别设置三种录音模式及“停止并保存”，支持冲突提示和清除。
- **音频输出**：支持 MP3 和 WAV，可选录音后音量标准化。
- **剪贴板**：录音结束后可将音频文件复制到剪贴板，也可选用临时文件方式。
- **系统集成**：支持开机自启动、中英文界面和单实例运行。
- **状态提示**：托盘图标根据空闲/录音状态切换，录音时显示已录制时长。
- **轻量实现**：C#、WinForms 和 NAudio，使用流式文件处理，无额外后台服务或主窗口。

## 下载与运行

**系统要求**：Windows 10 / 11（x64），具备 .NET Framework 4.8 或兼容版本。

1. 前往 **[GitHub Actions · MiniRec Windows Preview](https://github.com/vaeccc/MiniRec/actions/workflows/build.yml)**。
2. 打开最新的成功构建，在页面底部 **Artifacts** 下载 `MiniRec-preview-win-x64`。
3. 将下载的压缩包**完整解压**；若其中还有 `MiniRec-preview-win-x64.zip`，请再解压一次。
4. 运行 `MiniRec.exe`。软件不会弹出主窗口，请在任务栏右下角的**通知区域 / 隐藏图标**中找到 MiniRec。

这是**便携版**，不是安装程序。请将 `MiniRec.exe` 与压缩包内的 DLL 等文件放在同一个目录，不要只复制 EXE。普通用户无需安装 Visual Studio 或 .NET SDK。

## 使用方法

### 开始和停止录音

| 操作 | 方法 |
| --- | --- |
| 录制麦克风 | 右键托盘图标 → **录制麦克风** |
| 录制电脑声音 | 右键托盘图标 → **录制电脑声音** |
| 混合录音 | 右键托盘图标 → **混合录音** |
| 停止并保存 | 右键托盘图标 → **停止并保存** |
| 快速开始 / 停止 | 左键点击托盘图标（录音来源可在设置中指定） |
| 打开录音目录 | 右键托盘图标 → **打开录音文件夹** |

首次使用建议先右键打开 **设置**，确认麦克风设备、保存位置与录音格式，再进行录音测试。

### 快捷键

在 **设置 → 快捷键与常规** 中，点击快捷键输入框后直接按键录入；使用 `Delete` / `Backspace` 清除，`Esc` 取消本次录入。留空即禁用对应快捷键。

**兼容旧版数字小键盘快捷键：**

| 按键 | 常见用途 |
| --- | --- |
| 数字小键盘 `+` | 开始录制麦克风 |
| 数字小键盘 `-` | 停止并保存 |

这里的 `+`、`-` 指**键盘右侧数字小键盘**，并非主键盘的加减号。它们是兼容旧版配置的用法，**全新安装默认不会自动启用**；你可以自行设置。若某个快捷键已被其他程序占用，请更换组合键。

### 录音保存

默认保存目录是用户“文档”下的 `MiniRec` 文件夹，可在设置中修改。右键托盘图标选择“打开录音文件夹”，会直接打开当前配置的保存位置。

录音完成时程序会生成 MP3 或 WAV 文件。长时间录音、混合音频及格式转换涉及磁盘临时文件；设备中断或转换失败时，程序会尽可能在保存目录的 `Recovery` 子目录保留可恢复的 WAV 数据，但无法保证所有异常情况均能完整恢复。

## 从 QuickAudioRecorder 迁移

MiniRec 是独立的 GitHub 项目，与旧版 Python / PyQt6 程序互不覆盖。

首次启动时，如果新配置不存在，MiniRec 会尝试读取旧配置：

```text
旧配置：%APPDATA%\QuickAudioRecorder\settings.json
新配置：%APPDATA%\MiniRec\settings.json
日志目录：%LOCALAPPDATA%\MiniRec\Logs\
```

成功导入后，新版将使用自己的配置文件，**不会删除或修改旧版配置**。迁移内容包括录音目录、录音格式、设备及原有快捷键等。

升级前建议退出旧程序并备份设置文件。如果旧版启用了开机启动，请关闭旧版的启动项，再到 MiniRec 中启用“登录 Windows 时自动启动”，避免两个版本同时运行。

## 开发与构建

**技术栈**：C# · .NET Framework 4.8 · WinForms · NAudio 2.4.0 · Windows WASAPI。

在 Windows 上安装 Visual Studio 2022 及 **.NET Framework 4.8 Developer Pack / Targeting Pack**，然后打开 `MiniRec.sln`，或执行：

```powershell
msbuild MiniRec/MiniRec.csproj /restore /p:Configuration=Release /p:Platform=x64
```

自动构建工作流见 [`.github/workflows/build.yml`](.github/workflows/build.yml)，会执行 Windows 编译、解压后的启动检查、模拟音频 WAV/MP3 编解码及恢复测试，并上传便携 ZIP 和 SHA256 校验文件。

正式发布前必须完成 [发布验收清单](RELEASE_CHECKLIST.md) 中的实机测试。通过版本号标签触发 [草稿 Release 工作流](.github/workflows/release.yml)，自动检查、打包后仍需手动审核和发布。

> 自动化测试不等同于实机音频验收；录音设备切换、蓝牙耳机、长录音和混录同步仍需在目标环境中验证。

## 问题反馈

遇到问题可通过 [GitHub Issues](https://github.com/vaeccc/MiniRec/issues) 反馈，建议附上 Windows 版本、MiniRec 构建编号、复现步骤和相关日志。上传日志前请检查并遮盖其中可能包含的用户路径或设备名称。
