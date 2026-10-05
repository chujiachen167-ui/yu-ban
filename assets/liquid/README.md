# 默认皮肤液面

视觉参考：用户提供的 [React Bits Liquid Chrome](https://www.reactbits.dev/backgrounds/liquid-chrome)。

本目录的 HLSL 与原生 WPF 绑定为语伴独立编写：组合波面计算法线，再用两条虚拟灯带与环境明暗表现浅色流体。不包含 React Bits 组件、其着色器公式或 OGL 依赖，不以移植 React Bits 源码发布。

`LiquidSurface.hlsl` 是可编辑源文件；`LiquidSurface.ps` 是随构建生成并嵌入程序的 PS 3.0 字节码；`compile.ps1` 调用 Windows 的 d3dcompiler_47.dll。无需额外安装 React、浏览器内核或着色器 SDK。运行程序时不再编译着色器。

此材质只绘制设置窗口背景，不捕获桌面、不折射实际桌面内容，也不影响表单文字。半透明来自输出 alpha 和窗口合成；1.6 为瓷白主色和冰蓝反光。只在官方默认皮肤可见时订阅绘制，最多每秒更新 30 次；最小化、隐藏、切到插画皮肤、关闭窗口或系统关闭动画时停止更新。

需要硬件支持 PS 3.0。不能使用该效果的设备显示静态冰白背景，保留设置功能；这不代表所有远程桌面/显卡组合都已验证。WPF 软件渲染不执行 PS 3.0，实际画面应在目标桌面查看，不能仅靠离屏位图判断。

参考文档：[WPF ShaderEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect)、[D3DCompile](https://learn.microsoft.com/en-us/windows/win32/api/d3dcompiler/nf-d3dcompiler-d3dcompile)。
