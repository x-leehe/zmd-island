/* notify-designs.js — 由 docs/designs/notify-all.json 生成（导出稿与画布默认稿保持同源）
 * 重新生成：node %TEMP%\opencode\gen-designs.js docs/designs/notify-all.json notify docs/assets/js/notify-designs.js
 * 自定义状态（如「唤起态」）不在此文件里，随导出 JSON 的 states 一起导入。
 */
window.IslandDesigns = window.IslandDesigns || {};
window.IslandDesigns.notify = {
    /* 等待态 560×60 */
    wait:     {
      "spec": {
        "width": 560,
        "height": 60,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "nw9",
          "type": "note",
          "x": 16,
          "y": 72,
          "w": 528,
          "h": 80,
          "z": 20,
          "opacity": 1,
          "props": {
            "text": "不设计等待态。\n本插件所有面板都不能通过除通知外任何形式唤醒。"
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
          "id": "nu2",
          "type": "icon",
          "x": 16,
          "y": 64,
          "w": 40,
          "h": 40,
          "z": 4,
          "opacity": 1,
          "props": {
            "name": "chat",
            "ratio": 0.78,
            "accent": "#ffffff"
          }
        },
        {
          "id": "nu3",
          "type": "text",
          "x": 72,
          "y": 32,
          "w": 200,
          "h": 24,
          "z": 5,
          "opacity": 1,
          "props": {
            "text": "微信 | 刚刚",
            "fontSize": 22
          }
        },
        {
          "id": "c1glrv",
          "type": "text",
          "x": 72,
          "y": 72,
          "w": 336,
          "h": 24,
          "z": 6,
          "opacity": 1,
          "props": {
            "text": "张三",
            "fontSize": 20
          }
        },
        {
          "id": "nu6",
          "type": "text",
          "x": 72,
          "y": 104,
          "w": 360,
          "h": 32,
          "z": 7,
          "opacity": 1,
          "props": {
            "text": "晚点碰头？",
            "fontSize": 18
          }
        },
        {
          "id": "nu9",
          "type": "button",
          "x": 440,
          "y": 64,
          "w": 40,
          "h": 40,
          "z": 8,
          "opacity": 1,
          "props": {
            "name": "reply",
            "shape": "circle",
            "variant": "solid",
            "ratio": 0.62
          }
        },
        {
          "id": "nu10",
          "type": "button",
          "x": 496,
          "y": 64,
          "w": 40,
          "h": 40,
          "z": 9,
          "opacity": 1,
          "props": {
            "name": "close",
            "shape": "circle",
            "variant": "solid",
            "ratio": 0.62
          }
        },
        {
          "id": "c2oe3a",
          "type": "progress",
          "x": 24,
          "y": 152,
          "w": 512,
          "h": 8,
          "z": 11,
          "opacity": 1,
          "props": {
            "progress": 0.45,
            "showTime": false
          }
        },
        {
          "id": "nu99",
          "type": "note",
          "x": 16,
          "y": 224,
          "w": 528,
          "h": 96,
          "z": 10,
          "opacity": 1,
          "props": {
            "text": "通知展开态：图标 + 「应用名 | 时间」+ 发送者 → 正文 → 右侧两个「自定义按钮」：reply / close（props.name 可换图标，toggled 做激活态），默认隐藏，鼠标悬停时再淡入出现。\n一条通知弹出一次展开态；为防止打扰，来自同一来源的多个消息显示为：发送者改成「N 条消息」，正文置空（正文留白是本设计允许的正常状态）。\n底部进度条 = 通知超时进度（左右各内缩 24 以避开 30px 圆角），超时后**直接进入隐藏态**；当用户鼠标移到通知上时，倒计时暂停，进度条也停止走动。\n本插件所有面板都不能通过除通知外任何形式唤醒（滚轮 / 顶缘悬停都不应把它叫出来）。"
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
          "id": "nc9",
          "type": "note",
          "x": 16,
          "y": 72,
          "w": 168,
          "h": 80,
          "z": 20,
          "opacity": 1,
          "props": {
            "text": "不设计收缩态。\n本插件所有面板都不能通过除通知外任何形式唤醒。"
          }
        }
      ]
    },

    /* 自定义 / 入场态 560×160 */
    custom:     {
      "spec": {
        "width": 560,
        "height": 160,
        "panelOpacity": 1
      },
      "components": [
        {
          "id": "c3bsqs",
          "type": "text",
          "x": 80,
          "y": 48,
          "w": 216,
          "h": 24,
          "z": 1,
          "opacity": 0.5,
          "props": {
            "text": "//NEW NOTIFICATION",
            "fontSize": 14
          }
        },
        {
          "id": "c1b2iu",
          "type": "icon",
          "x": 32,
          "y": 64,
          "w": 32,
          "h": 32,
          "z": 2,
          "opacity": 1,
          "props": {
            "name": "notifications",
            "ratio": 0.72,
            "accent": "#ffffff"
          }
        },
        {
          "id": "c2b9kw",
          "type": "text",
          "x": 80,
          "y": 80,
          "w": 104,
          "h": 32,
          "z": 3,
          "opacity": 1,
          "props": {
            "text": "新通知",
            "fontSize": 20
          }
        },
        {
          "id": "c4cbql",
          "type": "note",
          "x": 32,
          "y": 168,
          "w": 496,
          "h": 72,
          "z": 4,
          "opacity": 1,
          "props": {
            "text": "入场态（收到通知的瞬间）：仿照电池充电的入场动画，展现这个面板，随后过渡到通知展开态；入场耗时固定，不随设置伸缩。"
          }
        }
      ]
    }
};
