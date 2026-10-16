# MiniRec

**轻量、简洁的 Windows 托盘录音工具。**

MiniRec 可以录制麦克风声音、电脑播放的声音，或同时录制两者。无需保持主窗口打开，通过系统托盘和全局快捷键即可快速开始、停止并保存录音。

[![Windows Build](https://github.com/vaeccc/MiniRec/actions/workflows/build.yml/badge.svg)](https://github.com/vaeccc/MiniRec/actions/workflows/build.yml)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4)](https://github.com/vaeccc/MiniRec/releases)

## 功能特点

- **三种录音模式**：麦克风、电脑声音、混合录音。
- **系统托盘操作**：左键快速开始或停止，右键选择录音模式、打开保存目录及设置。
- **全局快捷键**：可分别为各录音模式和停止录音指定快捷键，支持冲突检测。
- **音频格式**：支持 MP3、WAV，可选音量标准化。
- **剪贴板功能**：录音后可将文件复制到剪贴板，方便粘贴分享。
- **实用设置**：自定义保存目录、麦克风设备、界面语言及开机自启动。
- **录音状态提示**：录制时托盘图标切换为红色，并显示录音时长。
- **录音恢复**：录音异常中断后，尝试保留并修复可恢复的 WAV 文件。

## 下载与运行

**系统要求：** Windows 10 / 11（x64），.NET Framework 4.8 或兼容版本。

- **正式版本：** [GitHub Releases](https://github.com/vaeccc/MiniRec/releases/latest)。
- **最新测试版：** [GitHub Actions](https://github.com/vaeccc/MiniRec/actions/workflows/build.yml)，进入成功的构建，在页面底部 **Artifacts** 下载 `MiniRec-preview-win-x64`。

下载后完整解压 ZIP，运行 `MiniRec.exe` 即可。MiniRec 为**便携版**，无需安装程序；请保留压缩包中的依赖 DLL，不要单独移动 EXE。

启动后软件驻留任务栏右下角的通知区域。如果没有直接看到图标，请展开隐藏图标区域。

## 使用方法

| 操作 | 方法 |
| --- | --- |
| 录制麦克风 | 右键托盘图标 → **录制麦克风** |
| 录制电脑声音 | 右键托盘图标 → **录制电脑声音** |
| 混合录音 | 右键托盘图标 → **混合录音** |
| 停止并保存 | 右键托盘图标 → **停止并保存** |
| 快速开始 / 停止 | 左键点击托盘图标 |
| 打开保存目录 | 右键托盘图标 → **打开录音文件夹** |
| 调整录音设置 | 右键托盘图标 → **设置…** |

左键启动录音时使用的模式，可以在设置中选择。

### 全局快捷键

进入 **设置 → 快捷键与常规**，分别设置麦克风、电脑声音、混合录音和停止保存的快捷键。支持常用组合键以及数字小键盘的 `+`、`-` 和数字键。

点击快捷键输入框后直接按下所需按键；使用 `Delete` / `Backspace` 清除，`Esc` 取消录入。快捷键留空表示不启用。

### 录音文件

默认保存在用户“文档”目录下的 `MiniRec` 文件夹，也可在设置中修改。文件可保存为 MP3 或 WAV。

录音出现中断或保存异常时，可检查录音目录中的 `Recovery` 文件夹。软件会尝试保留原始录音数据，但不能保证所有异常情况都可完全恢复。

### 配置与日志

```text
配置文件：%APPDATA%\MiniRec\settings.json
运行日志：%LOCALAPPDATA%\MiniRec\Logs\app.log
```

## 开发与构建

**技术栈：** C# · .NET Framework 4.8 · WinForms · NAudio · WASAPI。

在 Windows 上安装 Visual Studio 2022 和 .NET Framework 4.8 Targeting Pack，打开 `MiniRec.sln`，或使用以下命令构建：

```powershell
msbuild MiniRec/MiniRec.csproj /restore /p:Configuration=Release /p:Platform=x64
```

项目使用 [GitHub Actions](https://github.com/vaeccc/MiniRec/actions) 自动执行 Windows 构建和基础回归测试。

## 反馈问题

如遇到录音、快捷键或界面问题，请前往 [Issues](https://github.com/vaeccc/MiniRec/issues) 提交反馈。建议提供系统版本、软件版本、复现步骤及必要的日志；上传日志前注意检查个人路径等信息。
