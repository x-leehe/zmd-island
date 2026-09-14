/* music-designs.js — 音乐插件的三态默认设计稿
 * 与 docs/designs/music-all.json 保持一致（按实机比例更新：字号/按钮尺寸为补偿 ×0.8 后的值）。
 * 频谱柱值省略 → 画布按 id 确定性生成。
 */
window.IslandDesigns = {
  music: {
    /* 等待态 560×60 */
    wait: {
      spec: { width: 560, height: 60 },
      components: [
        { id: 'mw1', type: 'cover',    x: 24,  y: 10, w: 40,  h: 40, z: 1,  opacity: 1, props: {} },
        { id: 'mw2', type: 'lyrics',   x: 72,  y: 15, w: 264, h: 32, z: 2,  opacity: 1, props: { lines: ['', '正在演唱的这一句', ''], fontSize: 20, align: 'left' } },
        { id: 'mw3', type: 'expand',   x: 454, y: 11, w: 40,  h: 40, z: 3,  opacity: 1, props: { expanded: false } },
        { id: 'mw4', type: 'ring',     x: 504, y: 10, w: 40,  h: 40, z: 4,  opacity: 1, props: { progress: 0.45, center: true, centerPlaying: false, disc: false } },
        { id: 'mw5', type: 'spectrum', x: 419, y: 18, w: 16,  h: 24, z: 5,  opacity: 1, props: { barCount: 3, mirrored: true, smooth: false } },
        { id: 'mw9', type: 'note',     x: 16,  y: 72, w: 528, h: 56, z: 20, opacity: 1, props: { text: '等待态：左侧封面 + 当前歌词（靠左，但不紧贴，保持设计呼吸感）；「展开按钮」不在圆环上；圆环上的按钮是播放/暂停；圆环显示实时进度；三根「上下轴对称」频谱。点击展开后，过渡到音乐展开态。' } }
      ]
    },

    /* 音乐展开态 560×160 */
    unfold: {
      spec: { width: 560, height: 160 },
      components: [
        { id: 'mu1', type: 'spectrum', x: 0,   y: 0,   w: 560, h: 160, z: 1,  opacity: 0.2, props: { barCount: 40, mirrored: false, smooth: true } },
        { id: 'mu2', type: 'cover',    x: 32,  y: 32,  w: 96,  h: 96,  z: 2,  opacity: 1,   props: {} },
        { id: 'mu3', type: 'title',    x: 144, y: 32,  w: 168, h: 48,  z: 3,  opacity: 1,   props: { title: '曲目名称', artist: '歌手 · 专辑', fontSize: 20 } },
        { id: 'mu4', type: 'lyrics',   x: 144, y: 80,  w: 200, h: 32,  z: 4,  opacity: 1,   props: { lines: ['', '正在演唱的这一句', ''], align: 'left', fontSize: 18 } },
        { id: 'mu5', type: 'progress', x: 144, y: 116, w: 200, h: 8,   z: 5,  opacity: 1,   props: { progress: 0.45, showTime: false } },
        { id: 'mu6', type: 'prev',     x: 392, y: 56,  w: 48,  h: 48,  z: 6,  opacity: 1,   props: {} },
        { id: 'mu7', type: 'play',     x: 448, y: 56,  w: 48,  h: 48,  z: 7,  opacity: 1,   props: { playing: false } },
        { id: 'mu8', type: 'next',     x: 504, y: 56,  w: 48,  h: 48,  z: 8,  opacity: 1,   props: {} },
        { id: 'mu9', type: 'note',     x: 16,  y: 172, w: 528, h: 56,  z: 20, opacity: 1,   props: { text: '展开态：封面 + 标题/歌词 + 进度 + 上/播/下；频谱为全屏铺底的淡背景，并且频谱条应该是平滑过度而不是单体条呈现的（参考 illogical-impulse 的实现）。' } }
      ]
    },

    /* 收缩态 200×60 */
    contract: {
      spec: { width: 200, height: 60 },
      components: [
        { id: 'mc1', type: 'cover',    x: 24,  y: 10, w: 40, h: 40, z: 1,  opacity: 1, props: {} },
        { id: 'mc2', type: 'spectrum', x: 72,  y: 18, w: 16, h: 24, z: 2,  opacity: 1, props: { barCount: 3, mirrored: true, smooth: false } },
        { id: 'mc3', type: 'ring',     x: 144, y: 10, w: 40, h: 40, z: 3,  opacity: 1, props: { progress: 0.62, center: false, disc: true, centerPlaying: false } },
        { id: 'mc9', type: 'note',     x: 16,  y: 72, w: 168, h: 72, z: 20, opacity: 1, props: { text: '收缩态：封面 + 三根「上下轴对称」频谱 + 进度圆环（复用元插件的进度圆环，但不添加笔记本图标）。' } }
      ]
    }
  }
};
