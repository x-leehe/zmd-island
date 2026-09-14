/* island-spec.js — 灵动岛规格常量
 * 数值取自 Host/Plugins/Battery/BatteryIslandView.axaml，仅供布局设计画布使用。
 * 经典 <script>（非 ES module），保证 file:// 双击即可运行。
 */
window.IslandSpec = (function () {
  'use strict';

  var COLORS = {
    pill: '#312F30',
    pillShadow: '0 1 6 rgba(0,0,0,0.25)',
    disc: '#262425',
    accent: '#C6CA4C',
    danger: '#FF4D4F',
    text: '#FFFFFF',
    textSecondary: 'rgba(255,255,255,0.55)',
    track: 'rgba(255,255,255,0.15)',
    btnBg: 'rgba(255,255,255,0.08)'
  };

  /* 一个设计的多个状态：每个状态有各自的岛尺寸与元素布局。
     画布不关心这是哪个插件的页面，只关心「状态 → 视觉」；插件名留给设计名（导出 JSON）去区分。 */
  var STATES = [
    { id: 'wait',     name: '等待态', w: 560, h: 60 },
    { id: 'unfold',   name: '展开态', w: 560, h: 160 },
    { id: 'contract', name: '收缩态', w: 200, h: 60 },
    { id: 'custom',   name: '自定义', w: 560, h: 160 }
  ];

  /* 真实应用渲染时整段 HUD 的全局缩放 */
  var RENDER_SCALE = 0.8;

  function pillRadius(h) {
    return Math.min(30, Math.round(h / 2));
  }

  return {
    COLORS: COLORS,
    STATES: STATES,
    RENDER_SCALE: RENDER_SCALE,
    pillRadius: pillRadius
  };
})();
