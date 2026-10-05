# 视觉资产

用户提供「大肥鱼皮肤.png」「小奶娃皮肤.png」作为设计参考。主题插画以 OpenAI 内置 image_gen 工具的图片编辑模式生成，2026-10-04；transparent_background=true。原始概念图不含在发布包中。

- ocean.png：蓝发女仆、蓝鲸、波浪、贝壳与星星，中心透明，供真实控件排版。
- baby.png：黄色大眼小生物、嫩叶与雏菊，中心透明。
- yuban-icon.png / companion.ico：1.6 原创 E 键帽标志，浅色键面、深蓝 E 与轻微立体侧边。keycap-icon.ps1 用可编辑几何路径生成，不依赖字体或外部图片；pack-icon.ps1 打包 16、20、24、32、40、48、64、128、256 像素 ICO。原 1.2 红蓝引号图标已替换。

## 历史 1.2 图标生成提示（已弃用，仅保留来源记录）

Use case: logo-brand. Create a premium Windows app icon for a Chinese language learning companion named Yuban (语伴). Do not put ANY letters, words, Chinese characters or numbers in the image. A distinctive compact emblem of two facing quotation-mark speech shapes, suggesting two friendly conversational partners. Main shape vivid cobalt blue with a lighter blue inner facet, smaller counter-shape coral red. Crisp meticulously balanced geometry, subtly sculpted enamel depth, clean negative space, elegant friendly contemporary product brand. Avoid the ordinary overlapping round chat bubbles and avoid wave bars. No outer square tile, no enclosing circle, no background plate. True transparent alpha background, centered square composition, emblem fills 82 percent of canvas, generous clean padding. Strong thick silhouettes legible when reduced to 24 pixels, only two main shapes, no thin decorative details, no cast shadow outside emblem. Single finished icon, no mockup, no variants, no text.

## 插画编辑提示

Ocean: Edit target: supplied blue ocean desktop settings concept. Produce a single wide 3:2 transparent PNG decorative illustration asset for a real desktop app, no UI mockup. Preserve the exact recognizable blue-haired chibi maid girl with blue bow, leaning over with both hands, at the far upper left; a small smiling blue whale and gentle blue waves to the upper right. Sea bubbles, tiny shells and stars along bottom corners. Center and lower-middle 65% fully transparent and EMPTY for real form controls. No text, no lettering, no buttons, no rectangles, no app logo, no speech bubbles, no border enclosing the entire canvas. Keep original cute polished anime/watercolor rendering and blue cream colors. Decorative elements only occupying upper 25% and bottom 10%; transparent cutout alpha everywhere else. The girl can be larger than the other decorations. Do not add new characters.

Baby: Preserve the yellow baby creature with huge glossy eyes and tiny mouth at upper left, with hands leaning over the garden edge. Warm yellow and cream palette, green leaves and white daisies. Decorative art only, empty transparent center for real form controls; no text, logo, buttons, panels or border. Keep the character recognizable from the supplied concept. Upper decorations and bottom corner garden accents, wide 3:2 transparent PNG.

发布者应自行确认参考图中角色与素材的使用权；生成过程记录不代表第三方角色权利已获许可。

## 1.7 动态浮窗素材

`pet-atlas.png`：参考本项目已有 ocean.png、baby.png，通过内置 image_gen 编辑模式生成，透明背景，2 列 × 2 行；第一行蓝发角色睁眼/闭眼，第二行黄色小奶蛙睁眼/闭眼。当前皮肤名已更正为“小奶蛙”，前文“小奶娃皮肤.png”仅是用户原始文件名。

提示摘要：保持参考角色，四格等分、透明留白、同一角色两帧位置和尺寸相同，仅改变眼睛；禁止文字、网格、风景、UI、外部光晕。角色双手搭在不可见边缘，供实际 UI 合成。原始生成文件保留，发布包只使用项目内图集。

`PetAdornment.cs` 在原生 WPF 中读取图集，呼吸和轻摆仅改变角色图层；眨眼交叉淡入两帧。默认每秒最多更新 30 次。参考项目 https://github.com/PC2005-cloud/dsh-pet 的待机动画与透明素材分层思路，该项目根 LICENSE 为 MIT；此处没有复制它的源码、算法或角色素材，不依赖其运行时。

1.7 设置页的字标使用系统自带 KaiTi，缺失时依次回退 STKaiti、Microsoft YaHei UI；不分发字体文件。桌面与托盘仍使用原创 E 键帽图标。
