# 参与开发

这份文档写给想改语伴的人。**先看"能改什么"，再看"怎么验证"**——语伴最怕的是界面看着对、实际用不了。

## 环境

Windows 10/11 x64 与 .NET Framework 4.8。不需要装 Node、Rust 或浏览器运行时；构建脚本调用系统自带的 C# 编译器。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

产物在 `dist`：`EnglishCompanion.exe`（主程序）、`CompanionProbe.exe`（读输入框）、`CompanionVoice.exe`（隔离播放音频）、`CompanionTests.exe`（检查用，不随发布分发）。

**装到本机并核对**（推荐，避免你在旧版本上验证）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Deploy
```

`-Deploy` 会把产物装到本机安装目录，并用 SHA-256 核对安装版与构建一致，不一致就直接报错。**这一步曾经漏过**，导致用户验收的是近一小时前的旧版本。不要再靠人工记得做。

## 检查

```powershell
.\dist\CompanionTests.exe                                                    # 基础与接口，不需要桌面
.\dist\CompanionTests.exe --panel-check --skin-check --pet-check --liquid-check
.\dist\CompanionTests.exe --qwen-ui-check --speech-ui-check
```

后两类会打开本项目的测试窗口，需要交互式 Windows 桌面。`--liquid-check` 要求显卡支持 WPF PS 3.0。

**不要把这些放进默认检查**：`--voice-smoke`、`--cloud-check`、`--voice-compare` 会调用真实 API 并消耗额度。默认检查一律不联网、不读个人 Key。

## 改界面时请自己先截图

```powershell
.\dist\CompanionTests.exe --panel-shot <目录>
```

它用内置演示数据把浮窗与设置页渲染成 PNG，并打印**量化结果**：窗口高度、内容需要的高度、是否溢出、插画实际尺寸、液面是否铺满、下拉文字的垂直偏心量。这些数字是给改动者看的，不要只看图。

**为什么强调这一条**：语伴曾经连续出过几类只有渲染才能发现的缺陷——液面被父容器卡成顶部一条导致桌面文字透进界面、设置页卡片压在角色脸上、插画被行高拉扁。单元测试全绿，问题全在布局。**改了界面就渲染一次，并读那些数字。**

## 加一套皮肤

皮肤不需要改设置页。步骤：

1. **颜色**：在 `src/Themes.cs` 的 `Skin.Get` 里加一个分支，给出 `Id`、`Name`、`Accent`、`Ink`、`Muted`、`Surface`、`Edge`、`Panel`。`Name` 是下拉里显示给用户的名字。
2. **插画（可选）**：放一张 `assets/<id>.png`，并在 `build.ps1` 里加一行 `/resource:`，资源名必须是 `EnglishCompanion.<id>.png`。没有插画就返回 `null`，不算错。
3. **注册**：把 id 加进 `src/Themes.cs` 的 `Skin.Ids`。**只需改这一处**——设置页的下拉、默认值回退都读它。曾经那里是按位置写死的，加皮肤会连带改错别的项。
4. **特例**：如果新皮肤的按钮需要深色文字（像小奶蛙那样亮底色），在 `Skin.LightButtons` 里加上它的 id；不要在各处散写 `Id=="baby"`。

**必须保证可读性**：墨水色要能在你的 `Surface`、`Panel` 和半透明字段上达到 4.5:1 对比度。玻璃皮肤的特殊之处在于字段是透明的，文字实际压在液面与桌面之上——`--panel-shot` 会在文字背景上渲染一次并给出平均色差，背景文字透上来就会被看出来。

## 加一个翻译或语音平台

1. 在 `src/ProviderProfiles.cs` 的 `Translation` 或 `Speech` 数组里加一条：名称、默认地址、默认模型、协议类型、取 Key 的官方页面、官方文档，语音再加默认音色。
2. 如果这家不是 Chat Completions 协议，在 `src/Services.cs` 里补请求体构造与返回解析两个分支。
3. 在 `tests/ProviderTests.cs` 里加模拟 HTTP 检查：鉴权头、请求体字段、译文或音频解析、错误与取消。**不需要账户**。
4. 在 `docs/providers.md` 里如实登记：写清"离线验证"还是"真实账户验证"。**没有真实调用过就不要写已验证**——这两件事不能互相替代。

语音还要确认返回的音频能通过 `WaveAudio.Validate`：WAV/PCM、单或双声道、8 或 16 位、采样率 8000–96000。千问返回的流式占位头由代码内已知的哨兵值修复，不要放宽成"任意长度都接受"。

## 改朗读偏好（读法、语速、音色）

各家的参数名与取值范围不同，所以**能力判断与参数换算必须分开**：

- `SpeechProfiles.SupportsSpeed` / `CapabilityOf` 决定**界面给不给这一项**；
- `SpeechProfiles.SpeedFor` 决定**发什么值**，返回 `null` 表示这家不吃这个参数。

不支持时**不显示或置灰并写明原因**，不要发出对方会拒绝的参数。新平台要加进这两个方法，并在 `tests/Tests.cs` 的 `SpeedChecks` 里补一条。

朗读的音频缓存键取自**请求体本身**（`Services.SpeechBody`），所以任何会改变合成结果的设置都会自然失效。**不要改成只按文本比较**——那会导致换了音色还播旧录音。

## 改词典

英文来自 ECDICT（MIT），中文来自 CC-CEDICT（CC BY-SA 4.0）。两者都在 `assets/dictionary/`，各有生成脚本与说明。

**数据处理必须在生成脚本里做，不要在界面里打补丁。** 中文的词条带有 `CL:頭|头[tou2]` 这类内部标记，生成时就要转成简体写法并存进独立字段，界面负责排版。**不要把词典的内部格式原样显示给用户**——曾经把 `CL:頭|头[tou2]` 直接显示在释义里，用户看不懂。

查询侧的容错（空格、大小写、弯引号、单行损坏不拖垮整本词典）在 `src/WordDictionary.cs`，中文悬停分词在 `src/WordAt.cs`。改这两处都要补测试。

## 提交与反馈

- 改动尽量小、单一主题，说明**为什么**这么改，不只是改了什么。
- 带上你跑过的检查与结果。**没验证过的事情说明"未验证"**，不要写成已通过。
- **不要提交** `settings.json`、任何 Key、`dist/`、`artifacts/`、语音缓存或个人快捷方式。`.gitignore` 已覆盖大部分，提交前仍请 `git status` 看一眼。
- 报告问题时写清：Windows 版本、软件或输入法名称、怎么复现、期望与实际结果。截图里不要带 Key 或私人聊天内容。

## 已知边界

- 微信、QQ 这类自绘输入框**不向 Windows 公开内容**，读不到不是本项目可以绕过的缺陷。详见 README 与 `docs/providers.md`。
- 除 DeepSeek 与千问外，其余平台**只做过离线验证**。
- 中文朗读的听感只在维护者本机验收过；其他平台的中文音色没有听过。
