/* file-designs.js — 由 docs/designs/file-all.json 生成（导出稿与画布默认稿保持同源）
 * 重新生成：node docs/assets/js/gen-designs.js docs/designs/file-all.json
 * 自定义状态（如「唤起态」）不在此文件里，随导出 JSON 的 states 一起导入。
 */
window.IslandDesigns = window.IslandDesigns || {};
window.IslandDesigns.file = {
    /* 等待态 560×60 */
    wait:     {
      "spec": {
        "width": 560,
        "height": 60,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "cw9",
          "type": "note",
          "x": 16,
          "y": 80,
          "w": 528,
          "h": 80,
          "z": 20,
          "opacity": 1,
          "props": {
            "text": "等待态——路由到「唤起态」。"
          }
        }
      ]
    },

    /* 展开态 560×160 */
    unfold:     {
      "spec": {
        "width": 560,
        "height": 160,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "cu99",
          "type": "note",
          "x": 306,
          "y": 168,
          "w": 528,
          "h": 500,
          "z": 30,
          "opacity": 1,
          "props": {
            "text": "文件展开态（560×160）：四块样张按 2×2 分两组，对应文件的两个方向——上排 A/B 是「我把文件拖进岛」（发出：先待接收、后已接收），下排 C/D 是「别人把文件传给我」（接收：先收到、后看详情）；实机每次只呈现其中一块，动画与「充电」一致。\n左上画板「待接收」：64×64 云图标 + 「//FILE DETECTED」小字 + 主文案「可将文件拖至此处」+ 副文案「进行「协议传输」或更多操作……」\n右画板「已接收」：图标换成check（此处图标占位） + 「//ADDITION COMPLETE」+「文件已添加」+「请在相关面板继续操作」\n两板同构，统一规则：内容左内边距 40、图标 64、文字自图标右侧 32 起、三行文字 y36 / y60 / y100（fs14 / fs20 / fs14），整块在 160 高里上下各留 36。右板自 x600 起（= 左板 560 + 40 间距），板内偏移与左板一致。\n下排 C「收到新文件」：与 A/B 同构，内容换成 download 图标 +「//FILE RECEIVED」+「收到 20 个新文件！」+「来自 Xiaomi Mi 6」——文件由别的设备传来、一次性送达时的样子，右上角是 save / close / expand 三键。\n下排 D「文件详细信息」：把收下的文件逐条摊开——顶部一行标题（文件图标 +「收到 20 个新文件！」+ 来源设备），中间是可翻页的条目列表（类型图标 + 文件名 + 时间 | 大小），两侧 prev / next 翻页、底部页码点；右上是「自动整理」开关与 expand / save / close，表示收下之后可以就地整理。\n「//」开头的小字是 HUD 风格的批注行，实现时用浅色等宽感字体。\n命名借自《明日方舟：终末地》的「协议核心」体系：把文件传出去＝「协议传输」（底层走 LocalSend），把文件归类整理＝「协议整理」；两者都收在「执行：」这个动作选择里。\n协议传输的容量上限：**待传文件总体积 2GB，且最多 20 个文件**（两者任一超出即视为超限）。\n协议整理不受此限制；超限只影响传输——文件照常收下、照常列在卡上，只保留删除等操作，界面上以「协议超载！」提示（示例里的 Windows-11.iso 7.5GB 单条就已超限）。\n「协议传输」要先挑一台目标设备，那一步单开在「选择接收方」状态（560×160）里，见该状态的注释。"
          }
        },
        {
          "id": "c1w83n",
          "type": "icon",
          "x": 40,
          "y": 48,
          "w": 64,
          "h": 64,
          "z": 31,
          "opacity": 1,
          "props": {
            "name": "cloud",
            "ratio": 0.72,
            "accent": "#ffffff"
          }
        },
        {
          "id": "c2x1u2",
          "type": "text",
          "x": 136,
          "y": 60,
          "w": 208,
          "h": 40,
          "z": 32,
          "opacity": 1,
          "props": {
            "text": "可将文件拖至此处",
            "fontSize": 20
          }
        },
        {
          "id": "c3y2x8",
          "type": "text",
          "x": 136,
          "y": 100,
          "w": 216,
          "h": 24,
          "z": 33,
          "opacity": 1,
          "props": {
            "text": "进行「协议传输」或更多操作……",
            "fontSize": 14
          }
        },
        {
          "id": "c40hcn",
          "type": "text",
          "x": 136,
          "y": 36,
          "w": 264,
          "h": 24,
          "z": 34,
          "opacity": 1,
          "props": {
            "text": "//FILE DETECTED",
            "fontSize": 14
          }
        },
        {
          "id": "c518tk",
          "type": "panelUnfold",
          "x": 600,
          "y": 0,
          "w": 560,
          "h": 160,
          "z": 35,
          "opacity": 1,
          "props": {
            "label": "文件已添加",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c61fo5",
          "type": "icon",
          "x": 640,
          "y": 48,
          "w": 64,
          "h": 64,
          "z": 36,
          "opacity": 1,
          "props": {
            "name": "check",
            "ratio": 0.72,
            "accent": "#ffffff"
          }
        },
        {
          "id": "c72igg",
          "type": "text",
          "x": 736,
          "y": 60,
          "w": 128,
          "h": 40,
          "z": 37,
          "opacity": 1,
          "props": {
            "text": "文件已添加",
            "fontSize": 20
          }
        },
        {
          "id": "c830ng",
          "type": "text",
          "x": 736,
          "y": 36,
          "w": 264,
          "h": 24,
          "z": 38,
          "opacity": 1,
          "props": {
            "text": "//ADDITION COMPLETE",
            "fontSize": 14
          }
        },
        {
          "id": "c93tk4",
          "type": "text",
          "x": 736,
          "y": 100,
          "w": 288,
          "h": 24,
          "z": 39,
          "opacity": 1,
          "props": {
            "text": "请在相关面板继续操作",
            "fontSize": 14
          }
        },
        {
          "id": "c19ht3",
          "type": "panelUnfold",
          "x": 0,
          "y": 274,
          "w": 560,
          "h": 160,
          "z": 40,
          "opacity": 1,
          "props": {
            "label": "收到新文件",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c29oz4",
          "type": "icon",
          "x": 40,
          "y": 322,
          "w": 64,
          "h": 64,
          "z": 41,
          "opacity": 1,
          "props": {
            "name": "download",
            "ratio": 0.72,
            "accent": "#ffffff"
          }
        },
        {
          "id": "c3a3gh",
          "type": "text",
          "x": 136,
          "y": 334,
          "w": 208,
          "h": 40,
          "z": 42,
          "opacity": 1,
          "props": {
            "text": "收到 20 个新文件！",
            "fontSize": 20
          }
        },
        {
          "id": "c1calk",
          "type": "text",
          "x": 136,
          "y": 310,
          "w": 212,
          "h": 20,
          "z": 43,
          "opacity": 1,
          "props": {
            "text": "//FILE RECEIVED",
            "fontSize": 14
          }
        },
        {
          "id": "c2e30r",
          "type": "text",
          "x": 136,
          "y": 374,
          "w": 209,
          "h": 20,
          "z": 44,
          "opacity": 1,
          "props": {
            "text": "来自 Xiaomi Mi 6",
            "fontSize": 14
          }
        },
        {
          "id": "c3hslq",
          "type": "button",
          "x": 450,
          "y": 334,
          "w": 40,
          "h": 40,
          "z": 45,
          "opacity": 1,
          "props": {
            "name": "save",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c4ip3s",
          "type": "button",
          "x": 500,
          "y": 334,
          "w": 40,
          "h": 40,
          "z": 46,
          "opacity": 1,
          "props": {
            "name": "close",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c5kcb0",
          "type": "expand",
          "x": 400,
          "y": 334,
          "w": 40,
          "h": 40,
          "z": 47,
          "opacity": 1,
          "props": {
            "expanded": false
          }
        },
        {
          "id": "c6l25s",
          "type": "panelUnfold",
          "x": 600,
          "y": 274,
          "w": 560,
          "h": 260,
          "z": 48,
          "opacity": 1,
          "props": {
            "label": "文件详细信息",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c7nvbk",
          "type": "icon",
          "x": 624,
          "y": 296,
          "w": 32,
          "h": 32,
          "z": 49,
          "opacity": 1,
          "props": {
            "name": "download",
            "ratio": 0.72,
            "accent": "#ffffff"
          }
        },
        {
          "id": "c8ocmm",
          "type": "text",
          "x": 664,
          "y": 288,
          "w": 164,
          "h": 24,
          "z": 50,
          "opacity": 1,
          "props": {
            "text": "收到 20 个新文件！",
            "fontSize": 16
          }
        },
        {
          "id": "c9pdd8",
          "type": "text",
          "x": 664,
          "y": 312,
          "w": 220,
          "h": 20,
          "z": 51,
          "opacity": 1,
          "props": {
            "text": "来自 Xiaomi Mi 6",
            "fontSize": 14
          }
        },
        {
          "id": "c15y3aw",
          "type": "row",
          "x": 670,
          "y": 340,
          "w": 420,
          "h": 40,
          "z": 52,
          "opacity": 1,
          "props": {
            "icon": "assignment",
            "text": "方案修改稿.docx",
            "sub": "",
            "trailing": "14:48 | 3.8MB",
            "fontSize": 16
          }
        },
        {
          "id": "c16yum8",
          "type": "row",
          "x": 670,
          "y": 380,
          "w": 420,
          "h": 40,
          "z": 53,
          "opacity": 1,
          "props": {
            "icon": "assignment",
            "text": "主悬杆.stl",
            "sub": "",
            "trailing": "14:48 | 3.6MB",
            "fontSize": 16
          }
        },
        {
          "id": "c17z2vf",
          "type": "row",
          "x": 670,
          "y": 420,
          "w": 420,
          "h": 40,
          "z": 54,
          "opacity": 1,
          "props": {
            "icon": "assignment",
            "text": "photo_20260902_110636.png",
            "sub": "",
            "trailing": "14:48 | 5MB",
            "fontSize": 16
          }
        },
        {
          "id": "c181k4s",
          "type": "button",
          "x": 620,
          "y": 400,
          "w": 40,
          "h": 40,
          "z": 55,
          "opacity": 1,
          "props": {
            "name": "skip_previous",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c191zy1",
          "type": "button",
          "x": 1100,
          "y": 400,
          "w": 40,
          "h": 40,
          "z": 56,
          "opacity": 1,
          "props": {
            "name": "skip_next",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c2080jc",
          "type": "pager",
          "x": 840,
          "y": 510,
          "w": 72,
          "h": 12,
          "z": 57,
          "opacity": 1,
          "props": {
            "count": 5,
            "active": 0
          }
        },
        {
          "id": "c21gn1j",
          "type": "button",
          "x": 1100,
          "y": 292,
          "w": 40,
          "h": 40,
          "z": 58,
          "opacity": 1,
          "props": {
            "name": "close",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c22gqx4",
          "type": "button",
          "x": 1050,
          "y": 292,
          "w": 40,
          "h": 40,
          "z": 59,
          "opacity": 1,
          "props": {
            "name": "save",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c23gvzr",
          "type": "expand",
          "x": 1000,
          "y": 292,
          "w": 40,
          "h": 40,
          "z": 60,
          "opacity": 1,
          "props": {
            "expanded": true
          }
        },
        {
          "id": "c24iqy2",
          "type": "checkbox",
          "x": 892,
          "y": 300,
          "w": 100,
          "h": 24,
          "z": 61,
          "opacity": 1,
          "props": {
            "text": "自动整理",
            "checked": true,
            "fontSize": 16
          }
        },
        {
          "id": "c25mjx0",
          "type": "row",
          "x": 670,
          "y": 460,
          "w": 420,
          "h": 40,
          "z": 62,
          "opacity": 1,
          "props": {
            "icon": "assignment",
            "text": "别催了在做了.md",
            "sub": "",
            "trailing": "14:48 | 3KB",
            "fontSize": 16
          }
        }
      ]
    },

    /* 收缩态 200×60 */
    contract:     {
      "spec": {
        "width": 200,
        "height": 60,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "cc9",
          "type": "note",
          "x": 16,
          "y": 72,
          "w": 168,
          "h": 80,
          "z": 20,
          "opacity": 1,
          "props": {
            "text": "不设计收缩态"
          }
        }
      ]
    },

    /* 自定义 / 入场态 560×340 */
    custom:     {
      "spec": {
        "width": 560,
        "height": 340,
        "panelOpacity": 0
      },
      "components": [
        {
          "id": "c0aaaa",
          "type": "panelWait",
          "x": 0,
          "y": 0,
          "w": 560,
          "h": 64,
          "z": 2,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 30
          }
        },
        {
          "id": "c5vgvd",
          "type": "icon",
          "x": 24,
          "y": 12,
          "w": 40,
          "h": 40,
          "z": 6,
          "opacity": 1,
          "props": {
            "name": "assignment",
            "ratio": 0.72
          }
        },
        {
          "id": "c11rvn",
          "type": "text",
          "x": 72,
          "y": 12,
          "w": 356,
          "h": 40,
          "z": 8,
          "opacity": 1,
          "props": {
            "text": "Windows-11.iso 7.5GB",
            "fontSize": 20
          }
        },
        {
          "id": "c239ff",
          "type": "button",
          "x": 496,
          "y": 12,
          "w": 40,
          "h": 40,
          "z": 9,
          "opacity": 1,
          "props": {
            "name": "delete",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c1scbx",
          "type": "panelWait",
          "x": 0,
          "y": 84,
          "w": 560,
          "h": 64,
          "z": 3,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 30
          }
        },
        {
          "id": "c4ta7k",
          "type": "icon",
          "x": 24,
          "y": 96,
          "w": 40,
          "h": 40,
          "z": 5,
          "opacity": 1,
          "props": {
            "name": "assignment",
            "ratio": 0.72
          }
        },
        {
          "id": "c56vue",
          "type": "text",
          "x": 72,
          "y": 96,
          "w": 356,
          "h": 40,
          "z": 11,
          "opacity": 1,
          "props": {
            "text": "最终效果2.png 2.4MB",
            "fontSize": 20
          }
        },
        {
          "id": "c1fjzb",
          "type": "button",
          "x": 496,
          "y": 96,
          "w": 40,
          "h": 40,
          "z": 13,
          "opacity": 1,
          "props": {
            "name": "delete",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c2sjor",
          "type": "panelWait",
          "x": 0,
          "y": 168,
          "w": 560,
          "h": 64,
          "z": 4,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 30
          }
        },
        {
          "id": "c6w92m",
          "type": "icon",
          "x": 24,
          "y": 180,
          "w": 40,
          "h": 40,
          "z": 7,
          "opacity": 1,
          "props": {
            "name": "assignment",
            "ratio": 0.72
          }
        },
        {
          "id": "c2gow8",
          "type": "text",
          "x": 72,
          "y": 180,
          "w": 356,
          "h": 40,
          "z": 14,
          "opacity": 1,
          "props": {
            "text": "论AI如何赋能产业.pptx 10MB",
            "fontSize": 20
          }
        },
        {
          "id": "c5jhl3",
          "type": "button",
          "x": 496,
          "y": 180,
          "w": 40,
          "h": 40,
          "z": 16,
          "opacity": 1,
          "props": {
            "name": "delete",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c6mn6s",
          "type": "button",
          "x": 444,
          "y": 276,
          "w": 40,
          "h": 40,
          "z": 17,
          "opacity": 1,
          "props": {
            "name": "delete",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c8o7zb",
          "type": "expand",
          "x": 496,
          "y": 276,
          "w": 40,
          "h": 40,
          "z": 18,
          "opacity": 1,
          "props": {
            "expanded": true
          }
        },
        {
          "id": "c9aaaa",
          "type": "note",
          "x": 44,
          "y": 367,
          "w": 489,
          "h": 440,
          "z": 19,
          "opacity": 1,
          "props": {
            "text": "自定义展开态（560×340）：本模块由「等待态 / 唤起态」展开时出现，是文件列表 + 动作条的完整形态。\n上方是可滚动文件列表（样张三条：.iso / .png / .pptx），行距 84：类型图标 + 「文件名 大小」+ 右侧删除键，左右边距与真实稿一致（24 / 24）。\n第一条 .iso，这张卡是 Windows-11.iso 7.5GB —— 单条就超过了 2GB 的传输总量上限，所以动作条上挂着「协议超载！」徽标。**故意取超限样本**（传输上限＝待传总量 2GB / 20 个文件），用来对照「协议超载！」的呈现；.png 2.4MB 与 .pptx 10MB 两条在限内。\n右侧那根 8×184 的竖条表示：条目超过 3 条时列表可滚动——在本面板内滚轮**只负责滚动内容，不切换面板**，直到以任何方式退出该面板。\n下方是动作条：「执行：」+ 动作选择（协议传输 / 协议整理）+ 状态徽标 + 执行（check） / 删除 / 收起；底部那条进度条＝传输容量占用。\n列表与动作条之间的信息条是超限提示：只在超限时出现，用一句话说明怎么解除（「协议超载，请去除较大文件，或减小文件数量。」）。\n本态按「超限」样本绘制（进度 100%），唤起态按常规占用（45%）绘制，两处不是矛盾。\n声明的背景板与唤起态一样保持透明。\n在未超过限制时，进度条颜色取 rgb(198,202,76)；超过 60% 即取当前警告颜色（warnAt = 0.6）。\n在「协议整理」时，不展示进度条。"
          }
        },
        {
          "id": "c1mmmb",
          "type": "scrollbar",
          "x": 552,
          "y": 32,
          "w": 8,
          "h": 184,
          "z": 20,
          "opacity": 1,
          "props": {
            "thumb": 0.35,
            "horizontal": false
          }
        },
        {
          "id": "c106asb",
          "type": "panelWait",
          "x": 0,
          "y": 264,
          "w": 560,
          "h": 64,
          "z": 1,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c117552",
          "type": "select",
          "x": 92,
          "y": 280,
          "w": 160,
          "h": 32,
          "z": 21,
          "opacity": 1,
          "props": {
            "text": "协议传输",
            "fontSize": 18
          }
        },
        {
          "id": "c127hg4",
          "type": "text",
          "x": 32,
          "y": 284,
          "w": 40,
          "h": 24,
          "z": 22,
          "opacity": 1,
          "props": {
            "text": "执行：",
            "fontSize": 16
          }
        },
        {
          "id": "c138um6",
          "type": "button",
          "x": 392,
          "y": 276,
          "w": 40,
          "h": 40,
          "z": 23,
          "opacity": 1,
          "props": {
            "name": "rocket_launch",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c14d011",
          "type": "progress",
          "x": 32,
          "y": 320,
          "w": 504,
          "h": 8,
          "z": 24,
          "opacity": 1,
          "props": {
            "progress": 1,
            "showTime": false,
            "accent": "#ff4d4f",
            "warnAt": 0.6
          }
        },
        {
          "id": "c16i76h",
          "type": "badge",
          "x": 272,
          "y": 284,
          "w": 100,
          "h": 24,
          "z": 25,
          "opacity": 1,
          "props": {
            "text": "协议超载！",
            "fontSize": 16,
            "accent": "#ff4d4f"
          }
        },
        {
          "id": "c26yitt",
          "type": "banner",
          "x": 24,
          "y": 232,
          "w": 512,
          "h": 32,
          "z": 26,
          "opacity": 1,
          "props": {
            "text": "协议超载，请去除较大文件，或减小文件数量。",
            "tone": "error",
            "fontSize": 16
          }
        }
      ]
    }
};
