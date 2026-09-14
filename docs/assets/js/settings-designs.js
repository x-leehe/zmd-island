/* settings-designs.js — 由 docs/designs/settings-all.json 生成（导出稿与画布默认稿保持同源）
 * 重新生成：node %TEMP%\opencode\gen-designs.js docs/designs/settings-all.json settings docs/assets/js/settings-designs.js
 * 自定义状态（如「唤起态」）不在此文件里，随导出 JSON 的 states 一起导入。
 */
window.IslandDesigns = window.IslandDesigns || {};
window.IslandDesigns.settings = {
    /* 等待态 560×750 */
    wait:     {
      "spec": {
        "width": 560,
        "height": 750,
        "panelOpacity": 0
      },
      "components": [
        {
          "id": "c15rx7",
          "type": "panelWait",
          "x": 0,
          "y": 0,
          "w": 560,
          "h": 60,
          "z": 1,
          "opacity": 1,
          "props": {
            "label": "等待态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c25wt3",
          "type": "panelUnfold",
          "x": 0,
          "y": 74,
          "w": 560,
          "h": 676,
          "z": 2,
          "opacity": 1,
          "props": {
            "label": "展开态",
            "showLabel": false,
            "radius": 0
          }
        },
        {
          "id": "c36jnz",
          "type": "text",
          "x": 28,
          "y": 0,
          "w": 504,
          "h": 60,
          "z": 4,
          "opacity": 1,
          "props": {
            "text": "//灵动岛内容（如电池充电界面等）",
            "fontSize": 25
          }
        },
        {
          "id": "c599hh",
          "type": "text",
          "x": 236,
          "y": 92,
          "w": 296,
          "h": 28,
          "z": 6,
          "opacity": 1,
          "props": {
            "text": "OVER THE FRONTIER | INTO THE FRONT",
            "fontSize": 16
          }
        },
        {
          "id": "c15jgh1",
          "type": "segmented",
          "x": 28,
          "y": 132,
          "w": 504,
          "h": 40,
          "z": 7,
          "opacity": 1,
          "props": {
            "text": "通用/动画/通知/插件/开发者/关于",
            "count": 6,
            "active": 0,
            "fontSize": 18
          }
        },
        {
          "id": "c16maru",
          "type": "scrollbar",
          "x": 552,
          "y": 172,
          "w": 8,
          "h": 548,
          "z": 8,
          "opacity": 1,
          "props": {
            "thumb": 0.35,
            "horizontal": false
          }
        },
        {
          "id": "c17n4br",
          "type": "text",
          "x": 28,
          "y": 196,
          "w": 36,
          "h": 20,
          "z": 9,
          "opacity": 1,
          "props": {
            "text": "显示",
            "fontSize": 18
          }
        },
        {
          "id": "c18o52p",
          "type": "text",
          "x": 48,
          "y": 220,
          "w": 80,
          "h": 32,
          "z": 10,
          "opacity": 1,
          "props": {
            "text": "全局缩放",
            "fontSize": 16
          }
        },
        {
          "id": "c20pmas",
          "type": "slider",
          "x": 48,
          "y": 264,
          "w": 484,
          "h": 24,
          "z": 11,
          "opacity": 1,
          "props": {
            "progress": 0.55
          }
        },
        {
          "id": "c21pyt7",
          "type": "stepper",
          "x": 392,
          "y": 216,
          "w": 140,
          "h": 36,
          "z": 12,
          "opacity": 1,
          "props": {
            "text": "0.8"
          }
        },
        {
          "id": "c22reaj",
          "type": "text",
          "x": 48,
          "y": 300,
          "w": 80,
          "h": 32,
          "z": 13,
          "opacity": 1,
          "props": {
            "text": "窗口置顶",
            "fontSize": 16
          }
        },
        {
          "id": "c23tzog",
          "type": "text",
          "x": 48,
          "y": 332,
          "w": 224,
          "h": 20,
          "z": 14,
          "opacity": 1,
          "props": {
            "text": "灵动岛始终展示在其他窗口上方",
            "fontSize": 14
          }
        },
        {
          "id": "c24vd32",
          "type": "toggle",
          "x": 488,
          "y": 304,
          "w": 44,
          "h": 24,
          "z": 15,
          "opacity": 1,
          "props": {
            "on": true
          }
        },
        {
          "id": "c25vrdo",
          "type": "text",
          "x": 48,
          "y": 364,
          "w": 124,
          "h": 32,
          "z": 16,
          "opacity": 1,
          "props": {
            "text": "灵动岛切换方式",
            "fontSize": 16
          }
        },
        {
          "id": "c26wl2x",
          "type": "text",
          "x": 48,
          "y": 396,
          "w": 228,
          "h": 20,
          "z": 17,
          "opacity": 1,
          "props": {
            "text": "鼠标滚轮滚动时，如何切换灵动岛",
            "fontSize": 14
          }
        },
        {
          "id": "c27xysq",
          "type": "select",
          "x": 392,
          "y": 362,
          "w": 140,
          "h": 36,
          "z": 18,
          "opacity": 1,
          "props": {
            "text": "循环切换",
            "fontSize": 16
          }
        },
        {
          "id": "c320luf",
          "type": "note",
          "x": 592,
          "y": 20,
          "w": 220,
          "h": 56,
          "z": 19,
          "opacity": 1,
          "props": {
            "text": "重构设置界面（560×750）：右键-设置后弹出的面板，整窗＝「灵动岛本体 + 展开的设置面板」——\n顶栏 0..60 是岛本体（沿用等待态那块胶囊，显示当前岛内容，如电池充电界面）；74 起是设置窗体。声明面板不透明度为 0（透明），画面里可见的两块胶囊由 panel 组件自己画。\n分页：通用 / 动画 / 通知 / 插件 / 开发者 / 关于（6 页；**相对现版刻意调整**：通知与插件换序，并新增「开发者」页。分段控件 x28 宽 504 高 40 字号 18）。\n分组标题版式：标题（fs18）+ 其下一段 36×8 的强调条（rect，非 divider）。\n设置行版式（全窗统一）：左标签 fs16（x48）+ 次行说明 fs14（同 x）+ 右侧控件右对齐到 x532（= 560−28）；控件与其标签垂直居中；行与行纵向留 12（实测 216→264→300 为 12 节奏）。\n底部动作行（y682..722）：左侧保存键（40×40，图标应为 save / check 一类，表示保存设置），右侧收起键（expand expanded=true）。\n内容超出时由滚动条承担滚动：滚动条贴窗口右缘 552..560，本窗体内滚轮只滚内容。\n整窗对齐基准：左右内边距 28，内容右缘 532。"
          }
        },
        {
          "id": "c337k37",
          "type": "button",
          "x": 28,
          "y": 682,
          "w": 40,
          "h": 40,
          "z": 20,
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
          "id": "c35a5f8",
          "type": "expand",
          "x": 492,
          "y": 682,
          "w": 40,
          "h": 40,
          "z": 21,
          "opacity": 1,
          "props": {
            "expanded": true
          }
        },
        {
          "id": "c36g8n9",
          "type": "rect",
          "x": 28,
          "y": 212,
          "w": 36,
          "h": 8,
          "z": 3,
          "opacity": 1,
          "props": {
            "accent": "#504e4f"
          }
        },
        {
          "id": "c1hewq",
          "type": "icon",
          "x": 28,
          "y": 86,
          "w": 40,
          "h": 40,
          "z": 22,
          "opacity": 1,
          "props": {
            "name": "settings",
            "ratio": 0.72
          }
        },
        {
          "id": "c2hr76",
          "type": "text",
          "x": 76,
          "y": 96,
          "w": 44,
          "h": 20,
          "z": 23,
          "opacity": 1,
          "props": {
            "text": "设置",
            "fontSize": 20
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
          "id": "c28zm4c",
          "type": "note",
          "x": 80,
          "y": 180,
          "w": 220,
          "h": 56,
          "z": 1,
          "opacity": 1,
          "props": {
            "text": "此页面不设计"
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
          "id": "c29zvc7",
          "type": "note",
          "x": 4,
          "y": 104,
          "w": 220,
          "h": 56,
          "z": 1,
          "opacity": 1,
          "props": {
            "text": "此页面不设计"
          }
        }
      ]
    },

    /* 自定义 / 入场态 560×340 */
    custom:     {
      "spec": {
        "width": 560,
        "height": 340,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "c310d0u",
          "type": "note",
          "x": 40,
          "y": 392,
          "w": 220,
          "h": 56,
          "z": 1,
          "opacity": 1,
          "props": {
            "text": "此页面不设计"
          }
        }
      ]
    }
};
