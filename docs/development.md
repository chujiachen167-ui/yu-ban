# 开发说明

## 从源码构建

Windows 10/11 x64，.NET Framework 4.8。无需安装 Node、Rust 或浏览器运行时。使用系统 C# 编译器与 Windows 自带 d3dcompiler_47.dll。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

产物在 dist：主程序 EnglishCompanion.exe、读取输入框的 CompanionProbe.exe、隔离播放的 CompanionVoice.exe，以及开发用 CompanionTests.exe。发布给用户时前三个需要放在同一文件夹，测试程序无需分发。

## 检查

基础测试包含新增平台的离线 HTTP 请求检查，不调用真实模型。覆盖各家鉴权、请求文本与模型、译文解析、WAV/PCM/十六进制音频、错误提示、取消、Key 和配置隔离。

```powershell
.\dist\CompanionTests.exe --panel-check --skin-check --pet-check --liquid-check
```

这些检查会打开本项目的测试窗口，需要交互式 Windows 桌面。--liquid-check 要求当前显卡支持 WPF PS 3.0。--desktop-check 额外检查托盘与真实 HWND，受限命令环境可能无法注册托盘。

--playback-check 会播放本机已经缓存的固定英文示例，缺少缓存时直接失败，绝不回退为收费网络请求。--speech-ui-check 只检查试听偏好窗口的采用与取消。--voice-smoke、--cloud-check 等诊断入口可能调用真实 API；不要把它们放进默认测试或 CI。

本轮基础与接口检查 214 项通过，包含界面和固定缓存播放的 334 项通过。托盘注册在受限命令环境未通过；直接桌面验收启动授权超时，尚未形成新的托盘视觉验收。新增平台没有真实 Key 验证，详细范围见 providers.md。

## 维护入口

- src/ProviderProfiles.cs：平台、默认模型、网址、分开的 Key 和模型配置。
- src/Services.cs：请求协议、返回解析、超时、取消和长度限制。
- src/SettingsWindow.cs、Settings.xaml：两行设置页；ProviderHelp.cs、ProviderOptionsWindow.cs：帮助和模型调整。
- src/TranslationPanel.cs：浮窗排版、查词、选择复制及动画。
- src/VoiceWorker.cs：独立播放进程，避免音频驱动异常带走主程序。
- tests/ProviderTests.cs：用假凭据和模拟 HTTP 返回检查协议，无需账户。

改动不要记录用户原文、译文或 Key。上传 Issue 时不要附 settings.json、语音缓存或个人快捷方式。

## 已知待办

微信等自绘输入框的自动读取、更多真实账户兼容验证、词典遗漏和多语言设置入口，欢迎社区贡献。当前默认面向英语；配置文件里的 Language 可由开发者调整，但设置页还没有目标语言选择器。

## 版本与日志

公开首版 1.0.0。Windows 文件版本写为 1.0.0.0，显示版本写为 1.0.0。开发更新日志放在 changelog，应用设置页不提供日志入口。
