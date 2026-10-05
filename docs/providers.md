# API 服务商说明

核对日期：2026-10-05。以下是本项目默认值，账户可用模型、套餐和地域仍以官方平台为准。

## 翻译

| 平台 | 默认模型 | 官方平台 | 官方接口文档 |
| --- | --- | --- | --- |
| DeepSeek | `deepseek-flash` | [获取 Key](https://platform.deepseek.com/api_keys) | [文档](https://api-docs.deepseek.com/) |
| 千问 | `qwen-plus` | [获取 Key](https://platform.qianwenai.com/home/api-keys) | [文档](https://help.aliyun.com/zh/model-studio/) |
| OpenAI | `gpt-4.1-mini` | [获取 Key](https://platform.openai.com/api-keys) | [文档](https://developers.openai.com/api/docs/) |
| Kimi | `kimi-k2.6` | [获取 Key](https://platform.moonshot.cn/console/api-keys) | [文档](https://platform.moonshot.cn/docs/guide/kimi-k2-quickstart) |
| 智谱 | `glm-4.7-flash` | [获取 Key](https://bigmodel.cn/usercenter/proj-mgmt/apikeys) | [文档](https://docs.bigmodel.cn/) |
| 豆包 / 火山方舟 | `doubao-seed-2-0-mini-260428` | [获取 Key](https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey) | [文档](https://docs.volcengine.com/docs/ark/chat-api?lang=zh) |
| 硅基流动 | `Qwen/Qwen3-8B` | [获取 Key](https://cloud.siliconflow.cn/account/ak) | [文档](https://docs.siliconflow.cn/docs/api/chat-completions-post) |
| Gemini | `gemini-2.5-flash-lite` | [获取 Key](https://aistudio.google.com/api-keys) | [文档](https://ai.google.dev/api/generate-content) |
| Claude | `claude-haiku-4-5` | [获取 Key](https://platform.claude.com/settings/keys) | [文档](https://platform.claude.com/docs/en/api/messages/create) |

## 云端语音

| 平台 | 默认模型 | 默认音色 / ID | 官方平台 | 官方接口文档 |
| --- | --- | --- | --- | --- |
| 千问 | `qwen3-tts-flash` | `Cherry` | [获取 Key](https://platform.qianwenai.com/home/api-keys) | [文档](https://help.aliyun.com/zh/model-studio/qwen-tts-api) |
| OpenAI | `gpt-4o-mini-tts` | `coral` | [获取 Key](https://platform.openai.com/api-keys) | [文档](https://developers.openai.com/api/docs/guides/text-to-speech) |
| 硅基流动 | `FunAudioLLM/CosyVoice2-0.5B` | `FunAudioLLM/CosyVoice2-0.5B:alex` | [获取 Key](https://cloud.siliconflow.cn/account/ak) | [文档](https://docs.siliconflow.cn/docs/api/audio-speech-post) |
| MiniMax | `speech-2.8-hd` | `English_expressive_narrator` | [获取 Key](https://platform.minimax.cn/user-center/basic-information/interface-key) | [文档](https://platform.minimax.cn/docs/api-reference/speech-t2a-http) |
| ElevenLabs | `eleven_multilingual_v2` | `JBFqnCBsd6RMkjVDRZzb` | [获取 Key](https://elevenlabs.io/app/settings/api-keys) | [文档](https://elevenlabs.io/docs/api-reference/text-to-speech/convert) |

## 如何调整

设置页选平台并填写 Key。点击 i 信息弹窗中的“模型与接口”，可改完整接口地址、模型和音色。确认此小窗口仅修改设置草稿，还需要回到主设置点“确认”才保存到本机。取消主设置不会写入。

千问普通百炼 Key 使用默认 Qwen3-TTS-Flash。工作空间 Key 使用该工作空间专属 HTTPS 地址、准确的 Audio 型号与对应音色；更换平台后再切回来保留原设置。国际地域地址也必须和 Key 的地域匹配。

Gemini 的接口地址填到 `/v1beta/models/`，程序附加模型和 `:generateContent`；ElevenLabs 地址填到 `/v1/text-to-speech/`，程序附加音色 ID 并请求 PCM 24000。不要在地址中放 Key 或查询参数。MiniMax 国内平台使用 api.minimax.cn，国际账户可改为 api.minimax.io；不能混用不同区域的 Key。

方舟需要先开通对应模型，可填可用的模型 ID 或接入点 ID。智谱使用普通按量 API，不等同于 Coding Plan 套餐。OpenAI API Key 不等同于 ChatGPT 订阅。其他厂商的聊天/会员订阅也不自动代表 API 额度。

## 协议与验证范围

DeepSeek、千问、OpenAI、Kimi、智谱、方舟、硅基流动使用 Chat Completions；Gemini 与 Claude 使用各自原生协议。语音分别处理千问 JSON 下载地址、OpenAI/硅基流动 WAV、MiniMax 十六进制 WAV、ElevenLabs PCM。下载千问音频时不附带 Key。

新增平台已通过模拟 HTTP 检查，覆盖鉴权、文本与模型参数、返回解析、音频可播放格式、失败和取消。它们暂未做真实账户调用，不能保证所有型号、音色、套餐都兼容，也没有完成声音质量对比。DeepSeek、千问是前期本机使用过的组合，本轮没有消耗额度重新调用。

默认型号可能被厂商更改或下线。新模型不一定接受相同参数；不要把下拉选项或离线测试当成厂商承诺。参数改动欢迎带官方文档和去除凭据后的复现提交 PR。

当前朗读偏好的读法/音色试听仅限千问 Audio 3.1 英语；其他平台可在模型与接口调整基础音色，不会自动套用千问的音色 ID。

百度、腾讯等需要多项凭据或独立签名的传统接口，以及火山语音的资源/应用 ID 接口，本版没有加入。后续适配需要相应的配置界面和协议验证，不把它们放入下拉框后假装一个 Key 就能调用。
