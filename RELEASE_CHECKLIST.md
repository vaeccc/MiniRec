# MiniRec 正式发布验收清单

> 此文件是发布闸门。自动构建通过不等于已完成真实 Windows 音频设备验收。
> 在所有阻塞项实际验证通过前，仅使用 GitHub Actions 预览包，**不得发布公开 v1.0.0 正式版**。

## 自动化检查（每次修改必须通过）

- [ ] Windows x64 Release 编译无错误及 CS/NU 警告
- [ ] `--verify-hotkeys`：含数字小键盘 +、-、0–9 映射
- [ ] `--verify-settings-migration`：旧配置导入且旧文件保持不变
- [ ] `--verify-folder-path`：含中文、空格、斜杠和 UNC 路径
- [ ] `--verify-branding`：EXE、设置窗口名称与图标
- [ ] `--verify-audio-pipeline`：模拟音频混合、WAV、MP3 编码/解码与崩溃 WAV 文件头恢复
- [ ] 解压后的 MiniRec.exe 能正常启动；第二实例拒绝启动
- [ ] ZIP 及 SHA256SUMS.txt 已生成；解压后依赖 DLL、LICENSE、LICENSES/NAudio-MIT.txt 完整

## Windows 实机音频测试（发布前必须验证）

- [ ] Windows 10 x64：麦克风录制 WAV、MP3，停止后正常播放
- [ ] Windows 11 x64：麦克风录制 WAV、MP3，停止后正常播放
- [ ] Windows 10/11：电脑播放声音录制 WAV、MP3，监听回放结果
- [ ] Windows 10/11：混合录音 WAV、MP3，人声/电脑声同时存在且同步可接受
- [ ] 静音期间、播放设备改变、默认麦克风改变、蓝牙/耳机断开后的表现可接受
- [ ] 录音期间系统休眠/唤醒，能提示错误并保留可恢复的录音
- [ ] 录音 1 小时以上：内存无明显持续增长，最终音频长度和文件体积合理
- [ ] 录音过程中强制结束进程后重启：Recovery 下 WAV 文件仍在，文件头可以修复
- [ ] MP3 编码/磁盘写入失败时：未误提示“录音成功”，Recovery 保留原始录音
- [ ] 录音过程中退出：成功则保存后退出，失败则取消退出并提示
- [ ] 剪贴板复制成功/失败及“临时目录”模式不造成意外丢失
- [ ] 中文路径、空格、包含正斜杠的目录：实际保存及“打开录音文件夹”均正确
- [ ] 全局快捷键录入、冲突、重启保持及数字小键盘数字键均正确
- [ ] 设置窗口多次打开/关闭、刷新设备、开机自启动和退出均正常

## Release 流程

1. 完成上述手动测试，记录 Windows 版本、音频设备、测试人和通过日期。
2. 检查 `LICENSE`、`LICENSES/NAudio-MIT.txt` 和第三方依赖信息。
3. 将 `MiniRec/MiniRec.csproj` 的 `<Version>` 更新为目标版本（例如 `1.0.0`），提交并等待 main 的 CI 通过。
4. 生成对应 Git 标签（例如 `v1.0.0`），推送到 GitHub。
5. `.github/workflows/release.yml` 将重新编译/测试并创建**草稿 Release**，附带便携 ZIP 和 SHA256SUMS.txt。
6. 最后复核包内文件和 SHA256，检查更新说明，再**手动发布**草稿 Release。

不得在未经实机验证时以正式版名义发布。正式发布前建议备份已有录音和配置。
