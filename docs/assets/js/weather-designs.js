/* weather-designs.js — 天气插件的三态默认设计稿
 * 图标用共享组件集的「图标」组件（路径已核验）；表内没有的字形（sunny / snowing / foggy）
 * 在实现时从 Material 官方集补，改 props.name 即可。
 */
window.IslandDesigns = window.IslandDesigns || {};
window.IslandDesigns.weather = {
  /* 等待态 560×60 */
  wait: {
    spec: { width: 560, height: 60 },
    components: [
      { id: 'ww1', type: 'icon',   x: 24,  y: 10, w: 40,  h: 40, z: 1,  opacity: 1, props: { name: 'cloud', ratio: 0.82 } },
      { id: 'ww2', type: 'text',   x: 72,  y: 10, w: 84,  h: 30, z: 2,  opacity: 1, props: { text: '23°', fontSize: 24 } },
      { id: 'ww3', type: 'badge',  x: 164, y: 19, w: 64,  h: 22, z: 3,  opacity: 1, props: { text: '多云', fontSize: 12 } },
      { id: 'ww4', type: 'text',   x: 240, y: 20, w: 140, h: 20, z: 4,  opacity: 1, props: { text: '上海 · 24°/18°', fontSize: 13 } },
      { id: 'ww5', type: 'stat',   x: 384, y: 20, w: 62,  h: 20, z: 5,  opacity: 1, props: { name: 'schedule', label: '', value: '14:20', unit: '', showLabel: false, fontSize: 12 } },
      { id: 'ww6', type: 'expand', x: 454, y: 11, w: 40,  h: 40, z: 6,  opacity: 1, props: { expanded: false } },
      { id: 'ww9', type: 'note',   x: 16,  y: 72, w: 528, h: 72, z: 20, opacity: 1, props: { text: '等待态：天气图标（cloud / grain 雨 / air 风 / opacity 湿度）+ 温度大字 + 天气徽标 +「城市 · 今日高低温」+ 图标化时间 + 展开按钮。\nOpen-Meteo 取数失败时整块退化为占位而非假数据。点击展开 → 天气展开态。' } }
    ]
  },

  /* 天气展开态 560×160 */
  unfold: {
    spec: { width: 560, height: 160 },
    components: [
      { id: 'wu1', type: 'spark',  x: 0,   y: 0,   w: 560, h: 160, z: 1,  opacity: 0.14, props: { mode: 'area', points: 28 } },
      { id: 'wu2', type: 'icon',   x: 32,  y: 32,  w: 96,  h: 96,  z: 2,  opacity: 1,    props: { name: 'cloud', ratio: 0.8 } },
      { id: 'wu3', type: 'text',   x: 144, y: 24,  w: 200, h: 24,  z: 3,  opacity: 1,    props: { text: '上海 · 徐汇', fontSize: 17 } },
      { id: 'wu4', type: 'text',   x: 144, y: 50,  w: 120, h: 48,  z: 4,  opacity: 1,    props: { text: '23°', fontSize: 40 } },
      { id: 'wu5', type: 'text',   x: 252, y: 66,  w: 150, h: 22,  z: 5,  opacity: 1,    props: { text: '多云 · 体感 21°', fontSize: 14 } },
      { id: 'wu6', type: 'text',   x: 144, y: 104, w: 240, h: 20,  z: 6,  opacity: 1,    props: { text: '最高 24° / 最低 18° · 湿度 62%', fontSize: 12 } },
      { id: 'wu7', type: 'stat',   x: 144, y: 126, w: 140, h: 18,  z: 7,  opacity: 1,    props: { name: 'air', label: '', value: '12', unit: 'km/h', showLabel: false, fontSize: 12 } },
      { id: 'wu8', type: 'stat',   x: 292, y: 126, w: 150, h: 18,  z: 8,  opacity: 1,    props: { name: 'schedule', label: '日落', value: '18:42', unit: '', fontSize: 12 } },

      { id: 'wu9',  type: 'text', x: 392, y: 26, w: 44, h: 16, z: 9,  opacity: 1, props: { text: '15:00', fontSize: 11 } },
      { id: 'wu10', type: 'icon', x: 400, y: 46, w: 28, h: 28, z: 10, opacity: 1, props: { name: 'cloud', ratio: 0.9 } },
      { id: 'wu11', type: 'text', x: 392, y: 80, w: 44, h: 16, z: 11, opacity: 1, props: { text: '24°', fontSize: 12 } },
      { id: 'wu12', type: 'text', x: 432, y: 26, w: 44, h: 16, z: 12, opacity: 1, props: { text: '16:00', fontSize: 11 } },
      { id: 'wu13', type: 'icon', x: 440, y: 46, w: 28, h: 28, z: 13, opacity: 1, props: { name: 'grain', ratio: 0.9 } },
      { id: 'wu14', type: 'text', x: 432, y: 80, w: 44, h: 16, z: 14, opacity: 1, props: { text: '23°', fontSize: 12 } },
      { id: 'wu15', type: 'text', x: 472, y: 26, w: 44, h: 16, z: 15, opacity: 1, props: { text: '17:00', fontSize: 11 } },
      { id: 'wu16', type: 'icon', x: 480, y: 46, w: 28, h: 28, z: 16, opacity: 1, props: { name: 'opacity', ratio: 0.9 } },
      { id: 'wu17', type: 'text', x: 472, y: 80, w: 44, h: 16, z: 17, opacity: 1, props: { text: '22°', fontSize: 12 } },
      { id: 'wu18', type: 'text', x: 512, y: 26, w: 44, h: 16, z: 18, opacity: 1, props: { text: '18:00', fontSize: 11 } },
      { id: 'wu19', type: 'icon', x: 520, y: 46, w: 28, h: 28, z: 19, opacity: 1, props: { name: 'air', ratio: 0.9 } },
      { id: 'wu20', type: 'text', x: 512, y: 80, w: 44, h: 16, z: 20, opacity: 1, props: { text: '20°', fontSize: 12 } },

      { id: 'wu99', type: 'note', x: 16, y: 172, w: 528, h: 96, z: 30, opacity: 1, props: { text: '天气展开态：大图标 +「城市 · 区」+ 温度大字 + 描述/体感 + 高低温/湿度 + 图标化的风速与日落；右侧一列「未来 4 小时」（时间 / 图标 / 温度）。\n背景 spark（面积图，透明度 0.14）是风或云量节奏的占位——实现时换成逐小时序列即可。' } }
    ]
  },

  /* 收缩态 200×60 */
  contract: {
    spec: { width: 200, height: 60 },
    components: [
      { id: 'wc1', type: 'icon', x: 24, y: 10, w: 40, h: 40, z: 1,  opacity: 1, props: { name: 'cloud', ratio: 0.82 } },
      { id: 'wc2', type: 'text', x: 72, y: 16, w: 104, h: 28, z: 2, opacity: 1, props: { text: '23° 多云', fontSize: 18 } },
      { id: 'wc9', type: 'note', x: 16, y: 72, w: 168, h: 80, z: 20, opacity: 1, props: { text: '收缩态：图标 +「温度 描述」，一行读完；不含城市与按钮，避免 200px 宽度下拥挤。' } }
    ]
  }
};
