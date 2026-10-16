# MiniRec

MiniRec 是一款轻量级 Windows 系统托盘录音工具，使用 **C# / .NET Framework 4.8 / WinForms / NAudio WASAPI** 实现，无主窗口。此仓库是独立项目，不属于任何 GitHub Fork 网络。

> **开发预览版。** 当前已实现 CI 编译和启动冒烟测试；实际录音质量、混音同步、热键行为及多种音频设备兼容性仍需要 Windows 实机验收，暂不视为正式 Release。

## 功能

- 录制麦克风、电脑声音（WASAPI Loopback），或两路混合录音
- 系统托盘左键快捷操作、右键切换录音模式 / 打开录音文件夹 / 设置 / 退出
- 支持 WAV、MP3，可选音量标准化及复制文件到剪贴板
- 全局快捷键，可单独配置和清除；兼容原来的**数字小键盘 `+` 开始、`-` 停止**
- 采用内嵌双状态 ICO 图标，设置窗口和 EXE 使用空闲图标，录音时托盘切换为红色图标
- 支持中文和英文设置界面、登录 Windows 后自动启动（默认不启用）
- 流式写入录音数据，长时间录音不需要把整段录音加载到内存

## 下载和使用

打开本仓库的 **Actions → MiniRec Windows Preview**，选择成功运行，在 **Artifacts** 下载 `MiniRec-preview-win-x64`。Artifact 可能嵌套一层 ZIP，请全部解压，然后运行 `MiniRec.exe`。注意 EXE 所在目录的依赖 DLL 和 EXE 配置文件需要一起保留。

软件启动后不会弹出主窗口；在 Windows 任务栏通知区域（含隐藏图标）右键 MiniRec 图标打开菜单。需要 Windows 10/11 x64、.NET Framework 4.8 或兼容版本；不需要安装 .NET SDK。

## 旧版设置迁移

MiniRec 第一次运行时，如 `%APPDATA%\MiniRec\settings.json` 不存在，会尝试读取：

```text
%APPDATA%\QuickAudioRecorder\settings.json
```

然后将其导入新目录，**不删除、不修改**旧设置。已有录音保存目录、设备、格式和快捷键保持原值。后续 MiniRec 只使用自己的设置文件。

- 新设置：`%APPDATA%\MiniRec\settings.json`
- 新日志：`%LOCALAPPDATA%\MiniRec\Logs\app.log`
- 新的开机启动项名称：`MiniRec`

原 QuickAudioRecorder 的开机启动注册表项不会自动删除。如果曾启用旧版开机启动，请在停用旧软件后自行关闭旧启动项，再在 MiniRec 设置中启用新启动项。首次迁移前建议先退出旧版并备份设置文件。

## 开发

通过 Visual Studio 2022 安装 .NET Framework 4.8 Targeting Pack 后，在 Windows 中打开 `MiniRec.sln`，或执行：

```powershell
msbuild MiniRec/MiniRec.csproj /restore /p:Configuration=Release /p:Platform=x64
```

GitHub Actions 使用干净目录中的 ZIP 做启动和回归检查，不需要在使用者电脑上安装开发工具。

## 版权与来源

MiniRec 是以 [QuickAudioRecorder](https://github.com/lukmay/QuickAudioRecorder) 的产品功能与使用体验为起点开发的 C# 重写项目。为尊重原项目的 MIT 许可证，本仓库保留原作者版权声明，相关归属见 [LICENSE](LICENSE) 和 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

此项目使用 NAudio（MIT License），不包含 Python/PyQt6 的旧实现、构建产物或历史测试文件。
