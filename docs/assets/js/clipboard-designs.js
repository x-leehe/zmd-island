/* clipboard-designs.js — 由 docs/designs/clipboard-all.json 生成（导出稿与画布默认稿保持同源）
 * 重新生成：node %TEMP%\opencode\gen-designs.js docs/designs/clipboard-all.json clipboard docs/assets/js/clipboard-designs.js
 * 自定义状态（如「唤起态」）不在此文件里，随导出 JSON 的 states 一起导入。
 */
window.IslandDesigns = window.IslandDesigns || {};
window.IslandDesigns.clipboard = {
    /* 等待态 560×60 */
    wait:     {
      "spec": {
        "width": 560,
        "height": 60,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "cw1",
          "type": "icon",
          "x": 24,
          "y": 10,
          "w": 40,
          "h": 40,
          "z": 1,
          "opacity": 1,
          "props": {
            "name": "assignment",
            "ratio": 0.8
          }
        },
        {
          "id": "cw2",
          "type": "text",
          "x": 72,
          "y": 10,
          "w": 412,
          "h": 40,
          "z": 2,
          "opacity": 1,
          "props": {
            "text": "图片 2.4MB",
            "fontSize": 20
          }
        },
        {
          "id": "cw5",
          "type": "expand",
          "x": 496,
          "y": 10,
          "w": 40,
          "h": 40,
          "z": 5,
          "opacity": 1,
          "props": {
            "expanded": false
          }
        },
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
            "text": "等待态：类型图标（文本 assignment / 图片 image / 音频 audiotrack / 链接 link）+「类型 大小」+ 展开按钮。\n左 / 右内边距统一 24（与收缩态、实机电池岛一致）。不设删除键——点整条即复制，删除放在展开态里。\n只在剪贴板内容变化时出现一次，随后按状态生命周期自行回落。"
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
          "id": "cu1",
          "type": "image",
          "x": 32,
          "y": 32,
          "w": 96,
          "h": 96,
          "z": 1,
          "opacity": 1,
          "props": {}
        },
        {
          "id": "cu2",
          "type": "text",
          "x": 144,
          "y": 32,
          "w": 200,
          "h": 32,
          "z": 2,
          "opacity": 1,
          "props": {
            "text": "图片 · PNG",
            "fontSize": 20
          }
        },
        {
          "id": "cu3",
          "type": "text",
          "x": 144,
          "y": 64,
          "w": 224,
          "h": 32,
          "z": 3,
          "opacity": 1,
          "props": {
            "text": "1920 × 1080 · 2.4 MB",
            "fontSize": 14
          }
        },
        {
          "id": "cu4",
          "type": "text",
          "x": 144,
          "y": 96,
          "w": 224,
          "h": 32,
          "z": 4,
          "opacity": 1,
          "props": {
            "text": "复制时间 14:18",
            "fontSize": 14
          }
        },
        {
          "id": "cu8",
          "type": "button",
          "x": 440,
          "y": 56,
          "w": 40,
          "h": 40,
          "z": 8,
          "opacity": 1,
          "props": {
            "name": "bookmark",
            "shape": "circle",
            "variant": "solid",
            "ratio": 0.6,
            "toggled": false
          }
        },
        {
          "id": "cu9",
          "type": "button",
          "x": 496,
          "y": 56,
          "w": 40,
          "h": 40,
          "z": 9,
          "opacity": 1,
          "props": {
            "name": "delete",
            "shape": "circle",
            "variant": "solid",
            "ratio": 0.6
          }
        },
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
            "text": "剪贴板展开态：\n左侧缩略预览（图片铺满 96×96；若为文本，则将预览换成assignment图标，内容换成三行 text：首行加粗、后两行浅色，行高 1.25，超出截断）+ 类型 / 规格，右侧两个「自定义按钮」（间距 16、右边距 24）。\n按钮图标：固定用 bookmark（toggled = 已固定）、清空用 delete；固定后的条目无法被自动清空，除非取消固定。\n三段文字 32px 一节正好与预览等高（32/64/96 → 各 32 高），按钮行与其垂直居中。"
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
          "id": "cc1",
          "type": "icon",
          "x": 24,
          "y": 10,
          "w": 40,
          "h": 40,
          "z": 1,
          "opacity": 1,
          "props": {
            "name": "assignment",
            "ratio": 0.8
          }
        },
        {
          "id": "cc2",
          "type": "text",
          "x": 72,
          "y": 16,
          "w": 104,
          "h": 28,
          "z": 2,
          "opacity": 1,
          "props": {
            "text": "图片 2.4MB",
            "fontSize": 20
          }
        },
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
            "text": "收缩态：类型图标 +「类型 大小」，够判断刚复制的是什么。"
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
          "z": 5,
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
          "z": 9,
          "opacity": 1,
          "props": {
            "name": "bookmark",
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
          "z": 11,
          "opacity": 1,
          "props": {
            "text": "小王，今晚八点讨论那个方案记得给个……",
            "fontSize": 20
          }
        },
        {
          "id": "c45bb5",
          "type": "expand",
          "x": 440,
          "y": 12,
          "w": 40,
          "h": 40,
          "z": 13,
          "opacity": 1,
          "props": {
            "expanded": false
          }
        },
        {
          "id": "c239ff",
          "type": "button",
          "x": 496,
          "y": 12,
          "w": 40,
          "h": 40,
          "z": 12,
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
          "z": 6,
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
          "z": 8,
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
          "z": 14,
          "opacity": 1,
          "props": {
            "text": "图片 2.4MB",
            "fontSize": 20
          }
        },
        {
          "id": "c682sw",
          "type": "expand",
          "x": 440,
          "y": 96,
          "w": 40,
          "h": 40,
          "z": 15,
          "opacity": 1,
          "props": {
            "expanded": false
          }
        },
        {
          "id": "c1fjzb",
          "type": "button",
          "x": 496,
          "y": 96,
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
          "id": "c2sjor",
          "type": "panelWait",
          "x": 0,
          "y": 168,
          "w": 560,
          "h": 64,
          "z": 7,
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
          "z": 10,
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
          "z": 17,
          "opacity": 1,
          "props": {
            "text": "文件 10MB",
            "fontSize": 20
          }
        },
        {
          "id": "c4hxb4",
          "type": "expand",
          "x": 440,
          "y": 180,
          "w": 40,
          "h": 40,
          "z": 18,
          "opacity": 1,
          "props": {
            "expanded": false
          }
        },
        {
          "id": "c5jhl3",
          "type": "button",
          "x": 496,
          "y": 180,
          "w": 40,
          "h": 40,
          "z": 19,
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
          "x": 24,
          "y": 268,
          "w": 56,
          "h": 56,
          "z": 20,
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
          "x": 480,
          "y": 268,
          "w": 56,
          "h": 56,
          "z": 21,
          "opacity": 1,
          "props": {
            "expanded": true
          }
        },
        {
          "id": "c9aaaa",
          "type": "note",
          "x": 96,
          "y": 268,
          "w": 372,
          "h": 56,
          "z": 22,
          "opacity": 1,
          "props": {
            "text": "规格样张（560×340）：\n三种类型的等待态并排（文本 / 图片 / 文件），行距 84。\n下方是本插件会用到的两种控件放大样——删除（圆 56）与「收起」状态的展开键。\n三个等待态样张的左右边距与真实稿一致（24 / 24）。\n声明的背景板和唤起态一样保持透明。\n如果项目超出3个，则允许鼠标滚轮滚动。\n（注，本面板拦截滚轮事件，直到以任何方式退出这个面板前，滚轮**永远只能负责内容滚动**，而不是切换面板）"
          }
        },
        {
          "id": "c3k7s0",
          "type": "panelUnfold",
          "x": 576,
          "y": 64,
          "w": 560,
          "h": 160,
          "z": 23,
          "opacity": 1,
          "props": {
            "label": "展开态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c4kb61",
          "type": "panelWait",
          "x": 596,
          "y": 184,
          "w": 520,
          "h": 64,
          "z": 4,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c5ki43",
          "type": "panelWait",
          "x": 620,
          "y": 196,
          "w": 472,
          "h": 64,
          "z": 3,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c6l6u9",
          "type": "icon",
          "x": 604,
          "y": 120,
          "w": 40,
          "h": 40,
          "z": 24,
          "opacity": 1,
          "props": {
            "name": "bookmark",
            "ratio": 0.72
          }
        },
        {
          "id": "c7lxm1",
          "type": "text",
          "x": 656,
          "y": 104,
          "w": 400,
          "h": 32,
          "z": 25,
          "opacity": 1,
          "props": {
            "text": "小王，今晚八点讨论的那个方案记得给个回复，人家小李等老长时间了，不能再拖了。",
            "fontSize": 20
          }
        },
        {
          "id": "c8o2ub",
          "type": "text",
          "x": 656,
          "y": 176,
          "w": 240,
          "h": 32,
          "z": 26,
          "opacity": 1,
          "props": {
            "text": "来自：微信 | 张总 14:48",
            "fontSize": 18
          }
        },
        {
          "id": "c9p4ko",
          "type": "button",
          "x": 976,
          "y": 168,
          "w": 40,
          "h": 40,
          "z": 27,
          "opacity": 1,
          "props": {
            "name": "assignment",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c11p7n9",
          "type": "button",
          "x": 1076,
          "y": 168,
          "w": 40,
          "h": 40,
          "z": 28,
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
          "id": "c12pjte",
          "type": "expand",
          "x": 1024,
          "y": 168,
          "w": 40,
          "h": 40,
          "z": 29,
          "opacity": 1,
          "props": {
            "expanded": true
          }
        },
        {
          "id": "c13r4n8",
          "type": "button",
          "x": 928,
          "y": 168,
          "w": 40,
          "h": 40,
          "z": 30,
          "opacity": 1,
          "props": {
            "name": "bookmark_remove",
            "shape": "circle",
            "variant": "solid",
            "toggled": false,
            "ratio": 0.62
          }
        },
        {
          "id": "c14sxhb",
          "type": "note",
          "x": 584,
          "y": 268,
          "w": 552,
          "h": 56,
          "z": 31,
          "opacity": 1,
          "props": {
            "text": "右侧为点击展开时显示的实例底部卡片堆叠效果视情况展现。我手动排版能力有限，实际上上下应满足轴对称。\nassignment为复制，将对应内容复制到剪贴板，以达到随用随取的效果。\n展开按钮点击后会过渡到如左侧面板的样式。\n删除按钮，就是它的本职工作。\n\n对于非文本内容，参考剪贴板展开态的表现，只需要改按钮。"
          }
        },
        {
          "id": "c15v679",
          "type": "panelWait",
          "x": 596,
          "y": 40,
          "w": 520,
          "h": 64,
          "z": 2,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c16vvnw",
          "type": "panelWait",
          "x": 620,
          "y": 28,
          "w": 472,
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
          "id": "c1mmmb",
          "type": "scrollbar",
          "x": 552,
          "y": 32,
          "w": 8,
          "h": 184,
          "z": 32,
          "opacity": 1,
          "props": {
            "thumb": 0.35,
            "horizontal": false
          }
        }
      ]
    }
};
