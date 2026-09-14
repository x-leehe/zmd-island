/* file-designs.js — 由 docs/designs/file-all.json 生成（导出稿与画布默认稿保持同源）
 * 重新生成：node %TEMP%\opencode\gen-designs.js docs/designs/file-all.json file docs/assets/js/file-designs.js
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
            "text": "不设计等待态。"
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
          "x": 16,
          "y": 168,
          "w": 528,
          "h": 96,
          "z": 30,
          "opacity": 1,
          "props": {
            "text": "文件展开态（560×160）——同一状态的两个瞬间，用户拖动文件时出现。拥有和“充电”一致的动画。实机只呈现其一，并排只是对照：\n左画板「待接收」：64×64 云图标 + 「//FILE DETECTED」小字 + 主文案「可将文件拖至此处」+ 副文案「进行「协议传输」或更多操作……」\n右画板「已接收」：图标换成check（此处图标占位） + 「//ADDITION COMPLETE」+「文件已添加」+「请在相关面板继续操作」\n两板同构，统一规则：内容左内边距 40、图标 64、文字自图标右侧 32 起、三行文字 y36 / y60 / y100（fs14 / fs20 / fs14），整块在 160 高里上下各留 36。\n「//」开头的小字是 HUD 风格的批注行，实现时用浅色等宽感字体。\n命名借自《明日方舟：终末地》的「协议核心」体系：把文件传出去＝「协议传输」（底层走 LocalSend），把文件归类整理＝「协议整理」；两者都收在「执行：」这个动作选择里。\n协议传输的容量上限：**待传文件总体积 2GB，且最多 20 个文件**（两者任一超出即视为超限）。\n协议整理不受此限制；超限只影响传输——文件照常收下、照常列在卡上，只保留删除等操作，界面上以「协议超载！」提示（示例里的 Windows-11.iso 7.5GB 单条就已超限）。"
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
          "x": 592,
          "y": 0,
          "w": 560,
          "h": 160,
          "z": 35,
          "opacity": 1,
          "props": {
            "label": "展开态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c61fo5",
          "type": "icon",
          "x": 632,
          "y": 48,
          "w": 64,
          "h": 64,
          "z": 36,
          "opacity": 1,
          "props": {
            "name": "favorite",
            "ratio": 0.72,
            "accent": "#ffffff"
          }
        },
        {
          "id": "c72igg",
          "type": "text",
          "x": 720,
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
          "x": 720,
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
          "x": 720,
          "y": 100,
          "w": 288,
          "h": 24,
          "z": 39,
          "opacity": 1,
          "props": {
            "text": "请在相关面板继续操作",
            "fontSize": 14
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
          "x": 440,
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
          "h": 155,
          "z": 19,
          "opacity": 1,
          "props": {
            "text": "规格样张（560×340）：本模块会用到的条目与控件的放大对照。\n上方三条是文件条目样张（.iso / .png / .pptx），行距 84：类型图标 + 「文件名 大小」+ 右侧删除键，左右边距与真实稿一致（24 / 24）。\n第一条 .iso 是 7.5GB，**故意取超限样本**（传输上限＝待传总量 2GB / 20 个文件），用来对照「协议超载！」的呈现；.png 2.4MB 与 .pptx 10MB 两条在限内。\n右侧那根 8×184 的竖条表示：条目超过 3 条时列表可滚动——在本面板内滚轮**只负责滚动内容，不切换面板**，直到以任何方式退出该面板。\n下方是动作条的放大样：「执行：」+ 动作选择（协议传输 / 协议整理）+ 状态徽标 + 执行（借用图标，实际应为check） / 删除 / 收起；底部那条进度条＝传输容量占用。\n放大样里进度拉到 100%（画\"占满 / 超限\"的样子），唤起态实稿是 45%（画常规占用），两处不是矛盾。\n声明的背景板与唤起态一样保持透明。\n在未超过限制时，进度条颜色取rgb(198,202,76)；超过60%即取当前警告颜色。\n在“协议整理”时，不展示进度条。"
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
          "x": 388,
          "y": 276,
          "w": 40,
          "h": 40,
          "z": 23,
          "opacity": 1,
          "props": {
            "name": "settings",
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
          "w": 500,
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
          "x": 271,
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
        }
      ]
    }
};
