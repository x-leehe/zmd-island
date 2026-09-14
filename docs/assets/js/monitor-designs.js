/* monitor-designs.js — 系统监控插件的三态默认设计稿
 * 展开态用「量规」组件（环 + 数值）做主体；等待态用「图标 + 数值」紧凑排布（60px 下更好读）。
 * 背景 spark 是实现时替换为真实时间序列折线的占位节奏。
 */
window.IslandDesigns = window.IslandDesigns || {};
window.IslandDesigns.monitor = {
  /* 等待态 560×60 */
  wait: {
    spec: { width: 560, height: 60 },
    components: [
      { id: 'sw1', type: 'stat',   x: 24,  y: 20, w: 110, h: 20, z: 1, opacity: 1, props: { name: 'memory', label: 'CPU', value: '42', unit: '%', fontSize: 14 } },
      { id: 'sw2', type: 'stat',   x: 150, y: 20, w: 110, h: 20, z: 2, opacity: 1, props: { name: 'dashboard', label: 'GPU', value: '31', unit: '%', fontSize: 14 } },
      { id: 'sw3', type: 'stat',   x: 276, y: 20, w: 110, h: 20, z: 3, opacity: 1, props: { name: 'storage', label: 'RAM', value: '63', unit: '%', fontSize: 14 } },
      { id: 'sw4', type: 'stat',   x: 386, y: 20, w: 62,  h: 20, z: 4, opacity: 1, props: { name: 'thermostat', label: '', value: '68', unit: '°C', showLabel: false, fontSize: 14 } },
      { id: 'sw5', type: 'expand', x: 454, y: 11, w: 40,  h: 40, z: 5, opacity: 1, props: { expanded: false } },
      { id: 'sw9', type: 'note',   x: 16,  y: 72, w: 528, h: 72, z: 20, opacity: 1, props: { text: '等待态：CPU / GPU / RAM / 温度四组「图标 + 数值」——60px 高度下比四个环更好读，展开按钮收尾。\n任一项越过阈值时该组改用 danger 色（组件支持 props.accent），阈值由插件设置提供。' } }
    ]
  },

  /* 监控展开态 560×160 */
  unfold: {
    spec: { width: 560, height: 160 },
    components: [
      { id: 'su1', type: 'spark', x: 0, y: 0, w: 560, h: 160, z: 1, opacity: 0.14, props: { mode: 'area', points: 36 } },

      { id: 'su2',  type: 'gauge',   x: 40,  y: 30,  w: 72, h: 72, z: 2,  opacity: 1, props: { progress: 0.42, value: '42', unit: '%' } },
      { id: 'su3',  type: 'text',    x: 48,  y: 108, w: 56, h: 18, z: 3,  opacity: 1, props: { text: 'CPU', fontSize: 13 } },
      { id: 'su4',  type: 'gauge',   x: 180, y: 30,  w: 72, h: 72, z: 4,  opacity: 1, props: { progress: 0.31, value: '31', unit: '%' } },
      { id: 'su5',  type: 'text',    x: 188, y: 108, w: 56, h: 18, z: 5,  opacity: 1, props: { text: 'GPU', fontSize: 13 } },
      { id: 'su6',  type: 'gauge',   x: 320, y: 30,  w: 72, h: 72, z: 6,  opacity: 1, props: { progress: 0.63, value: '63', unit: '%' } },
      { id: 'su7',  type: 'text',    x: 328, y: 108, w: 56, h: 18, z: 7,  opacity: 1, props: { text: 'RAM', fontSize: 13 } },
      { id: 'su8',  type: 'gauge',   x: 460, y: 30,  w: 72, h: 72, z: 8,  opacity: 1, props: { progress: 0.68, value: '68', unit: '°C' } },
      { id: 'su9',  type: 'text',    x: 468, y: 108, w: 64, h: 18, z: 9,  opacity: 1, props: { text: '温度', fontSize: 13 } },

      { id: 'su10', type: 'divider', x: 40,  y: 132, w: 504, h: 1,  z: 10, opacity: 1, props: {} },
      { id: 'su11', type: 'stat',    x: 40,  y: 138, w: 240, h: 18, z: 11, opacity: 1, props: { name: 'wifi', label: '网络', value: '↑12.4 ↓48.2', unit: 'MB/s', fontSize: 12 } },
      { id: 'su12', type: 'stat',    x: 320, y: 138, w: 224, h: 18, z: 12, opacity: 1, props: { name: 'timeline', label: 'FPS', value: '60', unit: '· 16.6ms', fontSize: 12 } },

      { id: 'su99', type: 'note', x: 16, y: 172, w: 528, h: 96, z: 30, opacity: 1, props: { text: '监控展开态：CPU / GPU / RAM / 温度四个量规（环 + 数值，无中心按钮）+ 分隔线 + 网络速率与 FPS。\n背景 spark 是负载曲线占位——换成真实时间序列即可；要双序列（CPU + GPU 叠图）就再复制一个 spark 并给不同 props.accent。' } }
    ]
  },

  /* 收缩态 200×60 */
  contract: {
    spec: { width: 200, height: 60 },
    components: [
      { id: 'sc1', type: 'gauge', x: 24, y: 10, w: 40,  h: 40, z: 1, opacity: 1, props: { progress: 0.42, value: '42', unit: '%', fontSize: 13 } },
      { id: 'sc2', type: 'stat',  x: 76, y: 20, w: 108, h: 20, z: 2, opacity: 1, props: { name: 'thermostat', label: '', value: '68', unit: '°C', showLabel: false, fontSize: 16 } },
      { id: 'sc9', type: 'note',  x: 16, y: 72, w: 168, h: 80, z: 20, opacity: 1, props: { text: '收缩态：一个 CPU 量规 + 温度，够一眼判断机器忙不忙 / 热不热；其余指标进展开态。' } }
    ]
  }
};
