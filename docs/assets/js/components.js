/* components.js — 组件调色板定义（音乐 + 通用 + 标注）
 * 每个定义含：name / w / h / props / palette()(小样 HTML) / render(comp)(画布 HTML)。
 * 数据驱动：新增插件组件集只需往 groups / defs 里加条目。
 * 经典 <script>（非 ES module），保证 file:// 双击即可运行。
 */
window.IslandComponents = (function () {
  'use strict';

  var C = window.IslandSpec.COLORS;

  var PATHS = {
    // 与实机同源：Google Material Icons（Apache-2.0），统一 24×24 网格、纯填充。
    note:     '<path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z" fill="currentColor"/>',
    prev:     '<path d="M6 6h2v12H6zm3.5 6l8.5 6V6z" fill="currentColor"/>',
    next:     '<path d="M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z" fill="currentColor"/>',
    play:     '<path d="M8 5v14l11-7z" fill="currentColor"/>',
    pause:    '<path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z" fill="currentColor"/>',
    expand:   '<path d="M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41z" fill="currentColor"/>',
    collapse: '<path d="M7.41 15.41L12 10.83l4.59 4.58L18 14l-6-6-6 6z" fill="currentColor"/>',
    image:    '<rect x="3.5" y="3.5" width="17" height="17" rx="3"/><circle cx="9" cy="9.2" r="1.8"/><path d="M4.5 17l4.6-4.6 3.4 3.4 2.8-2.8 4 4"/>'
  };
  var STROKE = { image: 1 };

  function svg(name, px) {
    var s = STROKE[name] ? ' fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"' : '';
    return '<svg viewBox="0 0 24 24" width="' + px + '" height="' + px + '" style="display:block"' + s + '>' + PATHS[name] + '</svg>';
  }

  /* 图标表：全部取自 Google Material 图标（Apache-2.0）。
     24×24 正坐标的路径经 Iconify 的 ic 集核验；末尾 (MS) 标注的取自
     Material Symbols Outlined（fonts.gstatic.com，原生 viewBox="0 -960 960 960"），
     已用 <g transform="translate(0,24) scale(0.025)"> 换算到 24×24 网格，避免手写出错字形。
     标注说明里提到的字形若仍不在表内（如 weather 的 sunny / snowing），
     实现时从官方集补，画布只需替换名字。 */
  var ICONS = {
    /* 音乐（复用上面的 PATHS，字形与实机一致） */
    music_note: PATHS.note, play_arrow: PATHS.play, pause: PATHS.pause,
    skip_previous: PATHS.prev, skip_next: PATHS.next,
    expand_more: PATHS.expand, expand_less: PATHS.collapse,

    /* 天气 */
    cloud: '<path fill="currentColor" d="M19.35 10.04A7.49 7.49 0 0 0 12 4C9.11 4 6.6 5.64 5.35 8.04A5.994 5.994 0 0 0 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5c0-2.64-2.05-4.78-4.65-4.96"/>',
    grain: '<path fill="currentColor" d="M10 12c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2M6 8c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2m0 8c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2m12-8c1.1 0 2-.9 2-2s-.9-2-2-2s-2 .9-2 2s.9 2 2 2m-4 8c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2m4-4c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2m-4-4c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2m-4-4c-1.1 0-2 .9-2 2s.9 2 2 2s2-.9 2-2s-.9-2-2-2"/>',
    opacity: '<path fill="currentColor" d="M17.66 8L12 2.35L6.34 8A8.02 8.02 0 0 0 4 13.64c0 2 .78 4.11 2.34 5.67a7.99 7.99 0 0 0 11.32 0c1.56-1.56 2.34-3.67 2.34-5.67S19.22 9.56 17.66 8M6 14c.01-2 .62-3.27 1.76-4.4L12 5.27l4.24 4.38C17.38 10.77 17.99 12 18 14z"/>',
    air: '<path fill="currentColor" d="M14.5 17c0 1.65-1.35 3-3 3s-3-1.35-3-3h2c0 .55.45 1 1 1s1-.45 1-1s-.45-1-1-1H2v-2h9.5c1.65 0 3 1.35 3 3M19 6.5C19 4.57 17.43 3 15.5 3S12 4.57 12 6.5h2c0-.83.67-1.5 1.5-1.5s1.5.67 1.5 1.5S16.33 8 15.5 8H2v2h13.5c1.93 0 3.5-1.57 3.5-3.5m-.5 4.5H2v2h16.5c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5v2c1.93 0 3.5-1.57 3.5-3.5S20.43 11 18.5 11"/>',
    thermostat: '<path fill="currentColor" d="M15 13V5c0-1.66-1.34-3-3-3S9 3.34 9 5v8c-1.21.91-2 2.37-2 4c0 2.76 2.24 5 5 5s5-2.24 5-5c0-1.63-.79-3.09-2-4m-4-2V5c0-.55.45-1 1-1s1 .45 1 1v1h-1v1h1v2h-1v1h1v1z"/>',
    visibility: '<path fill="currentColor" d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5M12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5s5 2.24 5 5s-2.24 5-5 5m0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3s3-1.34 3-3s-1.34-3-3-3"/>',

    /* 系统监控 */
    memory: '<path fill="currentColor" d="M15 9H9v6h6zm-2 4h-2v-2h2zm8-2V9h-2V7c0-1.1-.9-2-2-2h-2V3h-2v2h-2V3H9v2H7c-1.1 0-2 .9-2 2v2H3v2h2v2H3v2h2v2c0 1.1.9 2 2 2h2v2h2v-2h2v2h2v-2h2c1.1 0 2-.9 2-2v-2h2v-2h-2v-2zm-4 6H7V7h10z"/>',
    dashboard: '<path fill="currentColor" d="M3 13h8V3H3zm0 8h8v-6H3zm10 0h8V11h-8zm0-18v6h8V3z"/>',
    storage: '<path fill="currentColor" d="M2 20h20v-4H2zm2-3h2v2H4zM2 4v4h20V4zm4 3H4V5h2zm-4 7h20v-4H2zm2-3h2v2H4z"/>',
    speed: '<path fill="currentColor" d="m20.38 8.57l-1.23 1.85a8 8 0 0 1-.22 7.58H5.07A8 8 0 0 1 15.58 6.85l1.85-1.23A10 10 0 0 0 3.35 19a2 2 0 0 0 1.72 1h13.85a2 2 0 0 0 1.74-1a10 10 0 0 0-.27-10.44zm-9.79 6.84a2 2 0 0 0 2.83 0l5.66-8.49l-8.49 5.66a2 2 0 0 0 0 2.83"/>',
    wifi: '<path fill="currentColor" d="m1 9l2 2c4.97-4.97 13.03-4.97 18 0l2-2C16.93 2.93 7.08 2.93 1 9m8 8l3 3l3-3a4.237 4.237 0 0 0-6 0m-4-4l2 2a7.074 7.074 0 0 1 10 0l2-2C15.14 9.14 8.87 9.14 5 13"/>',
    timeline: '<path fill="currentColor" d="M23 8c0 1.1-.9 2-2 2a1.7 1.7 0 0 1-.51-.07l-3.56 3.55c.05.16.07.34.07.52c0 1.1-.9 2-2 2s-2-.9-2-2c0-.18.02-.36.07-.52l-2.55-2.55c-.16.05-.34.07-.52.07s-.36-.02-.52-.07l-4.55 4.56c.05.16.07.33.07.51c0 1.1-.9 2-2 2s-2-.9-2-2s.9-2 2-2c.18 0 .35.02.51.07l4.56-4.55C8.02 9.36 8 9.18 8 9c0-1.1.9-2 2-2s2 .9 2 2c0 .18-.02.36-.07.52l2.55 2.55c.16-.05.34-.07.52-.07s.36.02.52.07l3.55-3.56A1.7 1.7 0 0 1 19 8c0-1.1.9-2 2-2s2 .9 2 2"/>',
    equalizer: '<path fill="currentColor" d="M10 20h4V4h-4zm-6 0h4v-8H4zM16 9v11h4V9z"/>',
    tune: '<path fill="currentColor" d="M3 17v2h6v-2zM3 5v2h10V5zm10 16v-2h8v-2h-8v-2h-2v6zM7 9v2H3v2h4v2h2V9zm14 4v-2H11v2zm-6-4h2V7h4V5h-4V3h-2z"/>',

    /* 通知 */
    notifications: '<path fill="currentColor" d="M12 22c1.1 0 2-.9 2-2h-4a2 2 0 0 0 2 2m6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1z"/>',
    chat: '<path fill="currentColor" d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2M6 9h12v2H6zm8 5H6v-2h8zm4-6H6V6h12z"/>',
    mail: '<path fill="currentColor" d="M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2m0 4l-8 5l-8-5V6l8 5l8-5z"/>',
    event: '<path fill="currentColor" d="M17 12h-5v5h5zM16 1v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19a2 2 0 0 0 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-1V1zm3 18H5V8h14z"/>',
    reply: '<path fill="currentColor" d="M10 9V5l-7 7l7 7v-4.1c5 0 8.5 1.6 11 5.1c-1-5-4-10-11-11"/>',
    close: '<path fill="currentColor" d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12z"/>',
    schedule: '<path fill="currentColor" d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2M12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8s8 3.58 8 8s-3.58 8-8 8"/><path fill="currentColor" d="M12.5 7H11v6l5.25 3.15l.75-1.23l-4.5-2.67z"/>',

    /* 剪贴板 / 文件 */
    assignment: '<path fill="currentColor" d="M19 3h-4.18C14.4 1.84 13.3 1 12 1s-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2m-7 0c.55 0 1 .45 1 1s-.45 1-1 1s-1-.45-1-1s.45-1 1-1m2 14H7v-2h7zm3-4H7v-2h10zm0-4H7V7h10z"/>',
    description: '<path fill="currentColor" d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8zm2 16H8v-2h8zm0-4H8v-2h8zm-3-5V3.5L18.5 9z"/>',
    folder: '<path fill="currentColor" d="M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8z"/>',
    audiotrack: '<path fill="currentColor" d="M12 3v9.28a4.4 4.4 0 0 0-1.5-.28C8.01 12 6 14.01 6 16.5S8.01 21 10.5 21c2.31 0 4.2-1.75 4.45-4H15V6h4V3z"/>',
    bookmark: '<path fill="currentColor" d="M17 3H7c-1.1 0-1.99.9-1.99 2L5 21l7-3l7 3V5c0-1.1-.9-2-2-2"/>',
    link: '<path fill="currentColor" d="M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1M8 13h8v-2H8zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5"/>',
    download: '<path fill="currentColor" d="M5 20h14v-2H5zM19 9h-4V3H9v6H5l7 7z"/>',
    delete: '<path fill="currentColor" d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6zM19 4h-3.5l-1-1h-5l-1 1H5v2h14z"/>',

    /* 文件传输 / 整理：文件类型图标、来源设备、协议动作（图纸 palettes 用） */
    image: PATHS.image,
    album: '<path fill="currentColor" d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10s10-4.48 10-10S17.52 2 12 2m0 14.5c-2.49 0-4.5-2.01-4.5-4.5S9.51 7.5 12 7.5s4.5 2.01 4.5 4.5s-2.01 4.5-4.5 4.5m0-5.5c-.55 0-1 .45-1 1s.45 1 1 1s1-.45 1-1s-.45-1-1-1"/>',
    slideshow: '<path fill="currentColor" d="M10 8v8l5-4zm9-5H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2m0 16H5V5h14z"/>',
    smartphone: '<path fill="currentColor" d="M17 1.01L7 1c-1.1 0-2 .9-2 2v18c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V3c0-1.1-.9-1.99-2-1.99M17 19H7V5h10z"/>',
    upload: '<path fill="currentColor" d="M5 20h14v-2H5zm0-10h4v6h6v-6h4l-7-7z"/>',
    folder_open: '<path fill="currentColor" d="M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2m0 12H4V8h16z"/>',

    /* 设备 / 动作：接收方设备选择、清理内存等（取自 Google material-design-icons 的 24px 图标） */
    devices: '<path fill="currentColor" d="M4 6h18V4H4c-1.1 0-2 .9-2 2v11H0v3h14v-3H4V6zm19 2h-6c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h6c.55 0 1-.45 1-1V9c0-.55-.45-1-1-1zm-1 9h-4v-7h4v7z"/>',
    laptop: '<path fill="currentColor" d="M20,18c1.1,0,2-0.9,2-2V6c0-1.1-0.9-2-2-2H4C2.9,4,2,4.9,2,6v10c0,1.1,0.9,2,2,2H0v2h24v-2H20z M4,6h16v10H4V6z"/>',
    tablet: '<path fill="currentColor" d="M21 4H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h18c1.1 0 1.99-.9 1.99-2L23 6c0-1.1-.9-2-2-2zm-2 14H5V6h14v12z"/>',
    tv: '<path fill="currentColor" d="M21 3H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h5v2h8v-2h5c1.1 0 1.99-.9 1.99-2L23 5c0-1.1-.9-2-2-2zm0 14H3V5h18v12z"/>',
    print: '<path fill="currentColor" d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>',
    rocket_launch: '<path fill="currentColor" d="M9.19,6.35c-2.04,2.29-3.44,5.58-3.57,5.89L2,10.69l4.05-4.05c0.47-0.47,1.15-0.68,1.81-0.55L9.19,6.35L9.19,6.35z M11.17,17c0,0,3.74-1.55,5.89-3.7c5.4-5.4,4.5-9.62,4.21-10.57c-0.95-0.3-5.17-1.19-10.57,4.21C8.55,9.09,7,12.83,7,12.83 L11.17,17z M17.65,14.81c-2.29,2.04-5.58,3.44-5.89,3.57L13.31,22l4.05-4.05c0.47-0.47,0.68-1.15,0.55-1.81L17.65,14.81 L17.65,14.81z M9,18c0,0.83-0.34,1.58-0.88,2.12C6.94,21.3,2,22,2,22s0.7-4.94,1.88-6.12C4.42,15.34,5.17,15,6,15 C7.66,15,9,16.34,9,18z M13,9c0-1.1,0.9-2,2-2s2,0.9,2,2s-0.9,2-2,2S13,10.1,13,9z"/>',

    /* 通用标记 */
    bolt: '<path fill="currentColor" d="M11 21h-1l1-7H7.5c-.58 0-.57-.32-.38-.66s.05-.08.07-.12C8.48 10.94 10.42 7.54 13 3h1l-1 7h3.5c.49 0 .56.33.47.51l-.07.15C12.96 17.55 11 21 11 21"/>',
    check: '<path fill="currentColor" d="M9 16.17L4.83 12l-1.42 1.41L9 19L21 7l-1.41-1.41z"/>',
    warning: '<path fill="currentColor" d="M1 21h22L12 2zm12-3h-2v-2h2zm0-4h-2v-4h2z"/>',
    error: '<path fill="currentColor" d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10s10-4.48 10-10S17.52 2 12 2m1 15h-2v-2h2zm0-4h-2V7h2z"/>',
    info: '<path fill="currentColor" d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10s10-4.48 10-10S17.52 2 12 2m1 15h-2v-6h2zm0-8h-2V7h2z"/>',
    settings: '<path fill="currentColor" d="M19.5 12c0-.23-.01-.45-.03-.68l1.86-1.41c.4-.3.51-.86.26-1.3l-1.87-3.23a.987.987 0 0 0-1.25-.42l-2.15.91c-.37-.26-.76-.49-1.17-.68l-.29-2.31c-.06-.5-.49-.88-.99-.88h-3.73c-.51 0-.94.38-1 .88l-.29 2.31c-.41.19-.8.42-1.17.68l-2.15-.91c-.46-.2-1-.02-1.25.42L2.41 8.62c-.25.44-.14.99.26 1.3l1.86 1.41a7.3 7.3 0 0 0 0 1.35l-1.86 1.41c-.4.3-.51.86-.26 1.3l1.87 3.23c.25.44.79.62 1.25.42l2.15-.91c.37.26.76.49 1.17.68l.29 2.31c.06.5.49.88.99.88h3.73c.5 0 .93-.38.99-.88l.29-2.31c.41-.19.8-.42 1.17-.68l2.15.91c.46.2 1 .02 1.25-.42l1.87-3.23c.25-.44.14-.99-.26-1.3l-1.86-1.41c.03-.23.04-.45.04-.68m-7.46 3.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5s3.5 1.57 3.5 3.5s-1.57 3.5-3.5 3.5"/>',
    favorite: '<path fill="currentColor" d="m12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5C2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3C19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54z"/>',
    flag: '<path fill="currentColor" d="M14.4 6L14 4H5v17h2v-7h5.6l.4 2h7V6z"/>',
    lock: '<path fill="currentColor" d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2m-6 9c-1.1 0-2-.9-2-2s.9-2 2-2s2 .9 2 2s-.9 2-2 2m3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1s3.1 1.39 3.1 3.1z"/>',
    videocam: '<path fill="currentColor" d="M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11z"/>',
    mic: '<path fill="currentColor" d="M12 14c1.66 0 2.99-1.34 2.99-3L15 5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3m5.3-3c0 3-2.54 5.1-5.3 5.1S6.7 14 6.7 11H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c3.28-.48 6-3.3 6-6.72z"/>',

    /* (MS) 剪贴板「固定 / 取消固定 / 复制」与「展开拉手」—— 见上方 960→24 网格换算说明 */
    bookmark_border: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M200-120v-640q0-33 23.5-56.5T280-840h400q33 0 56.5 23.5T760-760v640L480-240 200-120Zm80-122 200-86 200 86v-518H280v518Zm0-518h400-400Z"/></g>',
    bookmark_add: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M200-120v-640q0-33 23.5-56.5T280-840h240v80H280v518l200-86 200 86v-278h80v400L480-240 200-120Zm80-640h240-240Zm400 160v-80h-80v-80h80v-80h80v80h80v80h-80v80h-80Z"/></g>',
    bookmark_remove: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M840-680H600v-80h240v80ZM200-120v-640q0-33 23.5-56.5T280-840h240v80H280v518l200-86 200 86v-278h80v400L480-240 200-120Zm80-640h240-240Z"/></g>',
    content_copy: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M360-240q-33 0-56.5-23.5T280-320v-480q0-33 23.5-56.5T360-880h360q33 0 56.5 23.5T800-800v480q0 33-23.5 56.5T720-240H360Zm0-80h360v-480H360v480ZM200-80q-33 0-56.5-23.5T120-160v-560h80v560h440v80H200Zm160-240v-480 480Z"/></g>',
    unfold_more: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M480-120 300-300l58-58 122 122 122-122 58 58-180 180ZM358-598l-58-58 180-180 180 180-58 58-122-122-122 122Z"/></g>',
    unfold_less: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="m356-160-56-56 180-180 180 180-56 56-124-124-124 124Zm124-404L300-744l56-56 124 124 124-124 56 56-180 180Z"/></g>',
    keyboard_arrow_down: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M480-344 240-584l56-56 184 184 184-184 56 56-240 240Z"/></g>',
    drag_handle: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M160-360v-80h640v80H160Zm0-160v-80h640v80H160Z"/></g>',

    /* (MS) 第二批：设置界面 / 插件管理 / 通用操作 */
    save: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M840-680v480q0 33-23.5 56.5T760-120H200q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h480l160 160Zm-80 34L646-760H200v560h560v-446ZM565-275q35-35 35-85t-35-85q-35-35-85-35t-85 35q-35 35-35 85t35 85q35 35 85 35t85-35ZM240-560h360v-160H240v160Zm-40-86v446-560 114Z"/></g>',
    done_all: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M268-240 42-466l57-56 170 170 56 56-57 56Zm226 0L268-466l56-57 170 170 368-368 56 57-424 424Zm0-226-57-56 198-198 57 56-198 198Z"/></g>',
    palette: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M480-80q-82 0-155-31.5t-127.5-86Q143-252 111.5-325T80-480q0-83 32.5-156t88-127Q256-817 330-848.5T488-880q80 0 151 27.5t124.5 76q53.5 48.5 85 115T880-518q0 115-70 176.5T640-280h-74q-9 0-12.5 5t-3.5 11q0 12 15 34.5t15 51.5q0 50-27.5 74T480-80Zm0-400Zm-177 23q17-17 17-43t-17-43q-17-17-43-17t-43 17q-17 17-17 43t17 43q17 17 43 17t43-17Zm120-160q17-17 17-43t-17-43q-17-17-43-17t-43 17q-17 17-17 43t17 43q17 17 43 17t43-17Zm200 0q17-17 17-43t-17-43q-17-17-43-17t-43 17q-17 17-17 43t17 43q17 17 43 17t43-17Zm120 160q17-17 17-43t-17-43q-17-17-43-17t-43 17q-17 17-17 43t17 43q17 17 43 17t43-17ZM480-160q9 0 14.5-5t5.5-13q0-14-15-33t-15-57q0-42 29-67t71-25h70q66 0 113-38.5T800-518q0-121-92.5-201.5T488-800q-136 0-232 93t-96 227q0 133 93.5 226.5T480-160Z"/></g>',
    language: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M325-111.5q-73-31.5-127.5-86t-86-127.5Q80-398 80-480.5t31.5-155q31.5-72.5 86-127t127.5-86Q398-880 480.5-880t155 31.5q72.5 31.5 127 86t86 127Q880-563 880-480.5T848.5-325q-31.5 73-86 127.5t-127 86Q563-80 480.5-80T325-111.5ZM480-162q26-36 45-75t31-83H404q12 44 31 83t45 75Zm-104-16q-18-33-31.5-68.5T322-320H204q29 50 72.5 87t99.5 55Zm208 0q56-18 99.5-55t72.5-87H638q-9 38-22.5 73.5T584-178ZM170-400h136q-3-20-4.5-39.5T300-480q0-21 1.5-40.5T306-560H170q-5 20-7.5 39.5T160-480q0 21 2.5 40.5T170-400Zm216 0h188q3-20 4.5-39.5T580-480q0-21-1.5-40.5T574-560H386q-3 20-4.5 39.5T380-480q0 21 1.5 40.5T386-400Zm268 0h136q5-20 7.5-39.5T800-480q0-21-2.5-40.5T790-560H654q3 20 4.5 39.5T660-480q0 21-1.5 40.5T654-400Zm-16-240h118q-29-50-72.5-87T584-782q18 33 31.5 68.5T638-640Zm-234 0h152q-12-44-31-83t-45-75q-26 36-45 75t-31 83Zm-200 0h118q9-38 22.5-73.5T376-782q-56 18-99.5 55T204-640Z"/></g>',
    desktop_windows: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M320-120v-80h80v-80H160q-33 0-56.5-23.5T80-360v-400q0-33 23.5-56.5T160-840h640q33 0 56.5 23.5T880-760v400q0 33-23.5 56.5T800-280H560v80h80v80H320ZM160-360h640v-400H160v400Zm0 0v-400 400Z"/></g>',
    bug_report: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M480-200q66 0 113-47t47-113v-160q0-66-47-113t-113-47q-66 0-113 47t-47 113v160q0 66 47 113t113 47Zm-80-120h160v-80H400v80Zm0-160h160v-80H400v80Zm80 40Zm0 320q-65 0-120.5-32T272-240H160v-80h84q-3-20-3.5-40t-.5-40h-80v-80h80q0-20 .5-40t3.5-40h-84v-80h112q14-23 31.5-43t40.5-35l-64-66 56-56 86 86q28-9 57-9t57 9l88-86 56 56-66 66q23 15 41.5 34.5T688-640h112v80h-84q3 20 3.5 40t.5 40h80v80h-80q0 20-.5 40t-3.5 40h84v80H688q-32 56-87.5 88T480-120Z"/></g>',
    extension: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M200-200h520v-184l45-22q16-8 25.5-22t9.5-32q0-17-9.5-31.5T765-514l-45-21v-185H528l-10-68q-3-22-19.5-37T460-840q-23 0-39.5 15T401-788l-10 68H200v86q56 21 88 68t32 106q0 60-32 107t-88 68v85Zm0 80q-34 0-57-23t-23-57v-152q48 0 84-30.5t36-77.5q0-46-36-76t-84-32v-152q0-33 23.5-56.5T200-800h122q7-51 46-85.5t92-34.5q52 0 91 34.5t47 85.5h122q33 0 56.5 23.5T800-720v134q36 18 58 52t22 74q0 41-22 75t-58 51v134q0 34-23.5 57T720-120H200Zm300-340Z"/></g>',
    volume_up: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M560-131v-82q90-26 145-100t55-168q0-94-55-168T560-749v-82q124 28 202 125.5T840-481q0 127-78 224.5T560-131ZM120-360v-240h160l200-200v640L280-360H120Zm440 40v-322q47 22 73.5 66t26.5 96q0 51-26.5 94.5T560-320ZM400-606l-86 86H200v80h114l86 86v-252ZM300-480Z"/></g>',
    brightness_6: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M480-28 346-160H160v-186L28-480l132-134v-186h186l134-132 134 132h186v186l132 134-132 134v186H614L480-28Zm0-252q83 0 141.5-58.5T680-480q0-83-58.5-141.5T480-680v400Zm0 140 100-100h140v-140l100-100-100-100v-140H580L480-820 380-720H240v140L140-480l100 100v140h140l100 100Zm0-340Z"/></g>',
    keyboard: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M160-200q-33 0-56.5-23.5T80-280v-400q0-33 23.5-56.5T160-760h640q33 0 56.5 23.5T880-680v400q0 33-23.5 56.5T800-200H160Zm0-80h640v-400H160v400Zm160-40h320v-80H320v80ZM200-440h80v-80h-80v80Zm120 0h80v-80h-80v80Zm120 0h80v-80h-80v80Zm120 0h80v-80h-80v80Zm120 0h80v-80h-80v80ZM200-560h80v-80h-80v80Zm120 0h80v-80h-80v80Zm120 0h80v-80h-80v80Zm120 0h80v-80h-80v80Zm120 0h80v-80h-80v80ZM160-280v-400 400Z"/></g>',
    drag_indicator: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M360-160q-33 0-56.5-23.5T280-240q0-33 23.5-56.5T360-320q33 0 56.5 23.5T440-240q0 33-23.5 56.5T360-160Zm240 0q-33 0-56.5-23.5T520-240q0-33 23.5-56.5T600-320q33 0 56.5 23.5T680-240q0 33-23.5 56.5T600-160ZM360-400q-33 0-56.5-23.5T280-480q0-33 23.5-56.5T360-560q33 0 56.5 23.5T440-480q0 33-23.5 56.5T360-400Zm240 0q-33 0-56.5-23.5T520-480q0-33 23.5-56.5T600-560q33 0 56.5 23.5T680-480q0 33-23.5 56.5T600-400ZM360-640q-33 0-56.5-23.5T280-720q0-33 23.5-56.5T360-800q33 0 56.5 23.5T440-720q0 33-23.5 56.5T360-640Zm240 0q-33 0-56.5-23.5T520-720q0-33 23.5-56.5T600-800q33 0 56.5 23.5T680-720q0 33-23.5 56.5T600-640Z"/></g>',
    refresh: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M480-160q-134 0-227-93t-93-227q0-134 93-227t227-93q69 0 132 28.5T720-690v-110h80v280H520v-80h168q-32-56-87.5-88T480-720q-100 0-170 70t-70 170q0 100 70 170t170 70q77 0 139-44t87-116h84q-28 106-114 173t-196 67Z"/></g>',
    open_in_new: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M200-120q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h280v80H200v560h560v-280h80v280q0 33-23.5 56.5T760-120H200Zm188-212-56-56 372-372H560v-80h280v280h-80v-144L388-332Z"/></g>',
    search: '<g transform="translate(0,24) scale(0.025)"><path fill="currentColor" d="M784-120 532-372q-30 24-69 38t-83 14q-109 0-184.5-75.5T120-580q0-109 75.5-184.5T380-840q109 0 184.5 75.5T640-580q0 44-14 83t-38 69l252 252-56 56ZM380-400q75 0 127.5-52.5T560-580q0-75-52.5-127.5T380-760q-75 0-127.5 52.5T200-580q0 75 52.5 127.5T380-400Z"/></g>'
  };

  /* 图标名 → 24×24 内联 svg（名字不在表内时回退 bolt，画布上立刻能看出来） */
  function iconSvg(name, px) {
    var body = ICONS[name] || ICONS.bolt;
    return '<svg viewBox="0 0 24 24" width="' + px + '" height="' + px + '" style="display:block">' + body + '</svg>';
  }

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
  function num(v, d) { var n = parseFloat(v); return isFinite(n) ? n : d; }
  function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }
  function intOr(v, d, lo, hi) {
    var n = Math.round(parseFloat(v));
    if (!isFinite(n)) n = d;
    return Math.min(hi, Math.max(lo, n));
  }
  function accentOf(c) { return (c && c.props && c.props.accent) || C.accent; }
  function fmtTime(sec) {
    sec = Math.max(0, Math.round(sec));
    var m = Math.floor(sec / 60), s = sec % 60;
    return m + ':' + (s < 10 ? '0' : '') + s;
  }
  function circle(inner) {
    return '<div style="width:100%;height:100%;border-radius:50%;background:' + C.btnBg
      + ';display:flex;align-items:center;justify-content:center;color:#fff;overflow:hidden">' + inner + '</div>';
  }
  function iconPx(c, ratio) { return Math.max(10, Math.round(Math.min(c.w, c.h) * (ratio == null ? 0.5 : ratio))); }

  /* 进度圆环：外侧弧长 = 进度，可选内嵌播放/暂停键 */
  function ringHtml(size, ratio, playing, accent, withCenter, withDisc) {
    var p = clamp(ratio, 0, 1);
    var circ = 2 * Math.PI * 44;
    var s = '<svg viewBox="0 0 100 100" width="100%" height="100%" style="display:block">'
      + '<circle cx="50" cy="50" r="44" fill="' + (withDisc ? C.disc : 'none') + '" stroke="' + C.track + '" stroke-width="9"/>'
      + '<circle cx="50" cy="50" r="44" fill="none" stroke="' + accent + '" stroke-width="9" stroke-linecap="round"'
      + ' stroke-dasharray="' + (p * circ) + ' ' + circ + '" transform="rotate(-90 50 50)"/></svg>';
    var inner = '';
    if (withCenter !== false) {
      inner = '<div style="position:absolute;inset:17%;border-radius:50%;background:' + C.disc + '"></div>'
        + '<div style="position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);width:60%;height:60%;display:flex;align-items:center;justify-content:center;color:#fff">'
        + svg(playing ? 'pause' : 'play', '100%') + '</div>';
    }
    return '<div style="position:relative;width:100%;height:100%">' + s + inner + '</div>';
  }

  /* 频谱柱高：由组件 id 确定性地生成，保证刷新/重渲染后不变 */
  function hashSeed(str) {
    var h = 2166136261;
    for (var i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); }
    return h >>> 0;
  }
  function barsFromId(id, n) {
    var s = hashSeed(id), out = [];
    for (var i = 0; i < n; i++) {
      s = (Math.imul(s, 1664525) + 1013904223) >>> 0;
      out.push(0.22 + (s % 1000) / 1000 * 0.78);
    }
    return out;
  }

  /* 平滑频谱：用 Catmull-Rom 转三次贝塞尔，画一条平滑面积（而非离散柱） */
  function r2(v) { return Math.round(v * 100) / 100; }
  function curveThrough(pts) {
    if (!pts.length) return '';
    if (pts.length === 1) return 'M' + r2(pts[0][0]) + ',' + r2(pts[0][1]);
    var d = 'M' + r2(pts[0][0]) + ',' + r2(pts[0][1]);
    for (var i = 0; i < pts.length - 1; i++) {
      var p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2;
      var c1x = p1[0] + (p2[0] - p0[0]) / 6, c1y = p1[1] + (p2[1] - p0[1]) / 6;
      var c2x = p2[0] - (p3[0] - p1[0]) / 6, c2y = p2[1] - (p3[1] - p1[1]) / 6;
      d += 'C' + r2(c1x) + ',' + r2(c1y) + ' ' + r2(c2x) + ',' + r2(c2y) + ' ' + r2(p2[0]) + ',' + r2(p2[1]);
    }
    return d;
  }
  function smoothAreaPath(vals, mirrored) {
    var n = vals.length;
    if (!n) return '';
    function xAt(i) { return n === 1 ? 50 : (i / (n - 1)) * 100; }
    var pts = [];
    for (var i = 0; i < n; i++) pts.push([xAt(i), mirrored ? (50 - vals[i] * 50) : (100 - vals[i] * 100)]);
    if (mirrored) {
      var lower = [];
      for (var j = n - 1; j >= 0; j--) lower.push([xAt(j), 50 + vals[j] * 50]);
      return curveThrough(pts.concat(lower)) + 'Z';
    }
    return curveThrough(pts) + 'L100,100 L0,100 Z';
  }
  /* 平滑模式专用「光滑」柱值：由 id 决定的多正弦叠加，保证曲线平滑而非锯齿 */
  function smoothVals(id, n) {
    var s = hashSeed(id);
    var p1 = ((s % 997) / 997) * 6.2832;
    var p2 = (((s >> 8) % 991) / 991) * 6.2832;
    var p3 = (((s >> 16) % 983) / 983) * 6.2832;
    var out = [];
    for (var i = 0; i < n; i++) {
      var t = n === 1 ? 0.5 : i / (n - 1);
      var v = 0.44
        + 0.30 * Math.sin(t * 6.2832 * 1.15 + p1)
        + 0.14 * Math.sin(t * 6.2832 * 2.70 + p2)
        + 0.08 * Math.sin(t * 6.2832 * 5.30 + p3);
      out.push(Math.min(1, Math.max(0.08, v)));
    }
    return out;
  }

  /* 空状态面板：渲染成与主面板（岛）完全一致的胶囊——同一底色 / 阴影 / 圆角规则，
     所以设计时看到的就是实机底色，而不是一个透明占位框。
     默认尺寸即该状态的真实尺寸；W/H 与不透明度照常可改；圆角 0 = 跟随高度（h/2）。 */
  function emptyPanel(c, fallbackLabel) {
    var h = num(c.h, 60);
    var r = num(c.props.radius, 0);
    if (r <= 0) r = window.IslandSpec.pillRadius(h);
    var label = (c.props.label == null || c.props.label === '') ? fallbackLabel : String(c.props.label);
    var tag = c.props.showLabel
      ? '<div style="position:absolute;left:12px;top:50%;transform:translateY(-50%);font-size:10px;letter-spacing:1px;color:rgba(255,255,255,.38);white-space:nowrap">' + esc(label) + '</div>'
      : '';
    return '<div style="position:relative;width:100%;height:100%;border-radius:' + r + 'px;'
      + 'background:' + C.pill + ';box-shadow:0 1px 6px rgba(0,0,0,0.25);box-sizing:border-box">' + tag + '</div>';
  }

  var defs = {
    /* ================= 音乐 ================= */
    cover: {
      name: '封面', w: 56, h: 56, props: {},
      palette: function () {
        return '<div style="width:34px;height:34px;border-radius:7px;background:linear-gradient(135deg,' + C.accent + '80,' + C.disc + ' 72%);display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.85)">' + svg('note', 18) + '</div>';
      },
      render: function (c) {
        var a = accentOf(c);
        return '<div style="width:100%;height:100%;border-radius:8px;overflow:hidden;background:linear-gradient(135deg,' + a + '80,' + C.disc + ' 72%);display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.82)">' + svg('note', iconPx(c)) + '</div>';
      }
    },

    title: {
      name: '标题+艺术家', w: 170, h: 38, props: { title: '曲目名称', artist: '歌手 · 专辑' },
      palette: function () {
        return '<div style="display:flex;flex-direction:column;gap:4px;justify-content:center">'
          + '<div style="width:34px;height:6px;border-radius:3px;background:#fff;opacity:.9"></div>'
          + '<div style="width:24px;height:4px;border-radius:2px;background:' + C.textSecondary + '"></div></div>';
      },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 14), 8, 48);
        return '<div style="width:100%;height:100%;display:flex;flex-direction:column;justify-content:center;gap:3px;overflow:hidden;padding:0 1px">'
          + '<div style="font-size:' + fs + 'px;font-weight:700;line-height:1.2;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.title) + '</div>'
          + '<div style="font-size:' + Math.max(8, fs - 4) + 'px;line-height:1.2;color:' + C.textSecondary + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.artist) + '</div>'
          + '</div>';
      }
    },

    prev: {
      name: '上一曲', w: 30, h: 30, props: {},
      palette: function () { return '<div style="width:30px;height:30px;border-radius:50%;background:' + C.btnBg + ';display:flex;align-items:center;justify-content:center;color:#fff">' + svg('prev', 15) + '</div>'; },
      render: function (c) { return circle(svg('prev', iconPx(c, 2 / 3))); }
    },

    play: {
      name: '播放/暂停', w: 30, h: 30, props: { playing: false },
      palette: function () { return '<div style="width:30px;height:30px;border-radius:50%;background:' + C.btnBg + ';display:flex;align-items:center;justify-content:center;color:#fff">' + svg('play', 15) + '</div>'; },
      render: function (c) { return circle(svg(c.props.playing ? 'pause' : 'play', iconPx(c, 2 / 3))); }
    },

    next: {
      name: '下一曲', w: 30, h: 30, props: {},
      palette: function () { return '<div style="width:30px;height:30px;border-radius:50%;background:' + C.btnBg + ';display:flex;align-items:center;justify-content:center;color:#fff">' + svg('next', 15) + '</div>'; },
      render: function (c) { return circle(svg('next', iconPx(c, 2 / 3))); }
    },

    expand: {
      name: '展开按钮', w: 30, h: 30, props: { expanded: false },
      palette: function () { return '<div style="width:30px;height:30px;border-radius:50%;background:' + C.btnBg + ';display:flex;align-items:center;justify-content:center;color:#fff">' + svg('expand', 15) + '</div>'; },
      render: function (c) { return circle(svg(c.props.expanded ? 'collapse' : 'expand', iconPx(c, 0.6))); }
    },

    ring: {
      name: '进度圆环', w: 40, h: 40, props: { progress: 0.45, center: true, centerPlaying: false, disc: true },
      palette: function () { return '<div style="width:34px;height:34px">' + ringHtml(34, 0.45, false, C.accent, true, true) + '</div>'; },
      render: function (c) { return ringHtml(Math.min(c.w, c.h), num(c.props.progress, 0.45), !!c.props.centerPlaying, accentOf(c), c.props.center !== false, !!c.props.disc); }
    },

    progress: {
      name: '进度条', w: 200, h: 6, props: { progress: 0.45, showTime: false, warnAt: 1 },
      palette: function () {
        return '<div style="width:42px;height:6px;border-radius:3px;background:' + C.track + ';position:relative;overflow:hidden">'
          + '<div style="position:absolute;left:0;top:0;bottom:0;width:45%;border-radius:3px;background:' + C.accent + '"></div></div>';
      },
      render: function (c) {
        var p = clamp(num(c.props.progress, 0.45), 0, 1);
        /* 警示阈值：未超过时用标准强调色 rgb(198,202,76)；超过后切到「本组件设定的颜色」——
           也就是设计里约定的警告色（本组件不额外引入令牌，警告色＝这个进度条自己的颜色） */
        var warnAt = clamp(num(c.props.warnAt, 1), 0, 1);
        var a = (warnAt < 1 && p > warnAt) ? accentOf(c) : C.accent;
        var bar = '<div style="width:100%;height:6px;border-radius:3px;background:' + C.track + ';overflow:hidden">'
          + '<div style="height:100%;width:' + (p * 100) + '%;border-radius:3px;background:' + a + '"></div></div>';
        if (!c.props.showTime) {
          return '<div style="width:100%;height:100%;display:flex;align-items:center">' + bar + '</div>';
        }
        var total = 225;
        return '<div style="width:100%;height:100%;display:flex;flex-direction:column;justify-content:center;gap:3px">'
          + bar
          + '<div style="display:flex;justify-content:space-between;font-size:' + clamp(num(c.props.fontSize, 10), 8, 24) + 'px;line-height:1;color:' + C.textSecondary + ';font-variant-numeric:tabular-nums"><span>' + fmtTime(total * p) + '</span><span>' + fmtTime(total) + '</span></div>'
          + '</div>';
      }
    },

    lyrics: {
      name: '歌词', w: 260, h: 60, props: { lines: ['前一句歌词 · 浅色显示', '正在演唱的这一句', '下一句歌词 · 浅色显示'], align: 'center' },
      palette: function () {
        return '<div style="display:flex;flex-direction:column;gap:3px;align-items:center">'
          + '<div style="width:30px;height:3px;border-radius:2px;background:' + C.textSecondary + '"></div>'
          + '<div style="width:40px;height:4px;border-radius:2px;background:#fff"></div>'
          + '<div style="width:24px;height:3px;border-radius:2px;background:' + C.textSecondary + '"></div></div>';
      },
      render: function (c) {
        var raw = c.props.lines || [];
        var fs = clamp(num(c.props.fontSize, 13), 8, 48);
        var align = (c.props.align === 'left') ? 'left' : 'center';
        var out = '';
        for (var i = 0; i < 3; i++) {
          var t = raw[i] == null ? '' : String(raw[i]);
          if (!t) continue;   /* 空行不渲染：单行歌词即只显示当前句 */
          var st = 'line-height:1.25;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:100%';
          if (i === 1) out += '<div style="font-size:' + fs + 'px;font-weight:700;color:#fff;' + st + '">' + esc(t) + '</div>';
          else out += '<div style="font-size:' + Math.max(8, fs - 1) + 'px;color:' + C.textSecondary + ';' + st + '">' + esc(t) + '</div>';
        }
        return '<div style="width:100%;height:100%;display:flex;flex-direction:column;justify-content:center;align-items:' + (align === 'left' ? 'flex-start' : 'center') + ';gap:4px;overflow:hidden;text-align:' + align + '">' + out + '</div>';
      }
    },

    spectrum: {
      name: '频谱条', w: 120, h: 44, props: { barCount: 14, mirrored: false, smooth: false },
      palette: function () {
        var hs = [6, 12, 18, 9, 15], b = '';
        for (var i = 0; i < hs.length; i++) b += '<div style="width:3px;height:' + hs[i] + 'px;border-radius:1.5px;background:' + C.accent + '"></div>';
        return '<div style="display:flex;align-items:flex-end;gap:2px;height:20px">' + b + '</div>';
      },
      render: function (c) {
        var n = intOr(c.props.barCount, 14, 1, 64);
        if (!c.props.bars || c.props.bars.length !== n) c.props.bars = barsFromId((c.id || 'bars') + '#' + n, n);
        var a = accentOf(c), mir = !!c.props.mirrored, vals = [];
        for (var k = 0; k < n; k++) vals.push(clamp(num(c.props.bars[k], 0.4), 0.05, 1));

        /* 平滑模式：SVG 平滑填充面（上下渐变） */
        if (c.props.smooth) {
          var uid = 'sg-' + String(c.id || 'x').replace(/[^a-zA-Z0-9_-]/g, '');
          var stops = mir
            ? '<stop offset="0%" stop-color="' + a + '" stop-opacity="0.12"/><stop offset="50%" stop-color="' + a + '" stop-opacity="0.95"/><stop offset="100%" stop-color="' + a + '" stop-opacity="0.12"/>'
            : '<stop offset="0%" stop-color="' + a + '" stop-opacity="0.95"/><stop offset="100%" stop-color="' + a + '" stop-opacity="0.12"/>';
          return '<svg viewBox="0 0 100 100" preserveAspectRatio="none" width="100%" height="100%" style="display:block">'
            + '<defs><linearGradient id="' + uid + '" x1="0" y1="0" x2="0" y2="1">' + stops + '</linearGradient></defs>'
            + '<path d="' + smoothAreaPath(smoothVals((c.id || 'x') + '#s', n), mir) + '" fill="url(#' + uid + ')"/></svg>';
        }

        var out = '';
        for (var i = 0; i < n; i++) {
          out += '<div style="flex:1 1 0;min-width:1px;height:' + Math.round(vals[i] * 100) + '%;border-radius:1.5px;background:' + a + '"></div>';
        }
        return '<div style="width:100%;height:100%;display:flex;' + (mir ? 'align-items:center' : 'align-items:flex-end') + ';gap:3px;overflow:hidden">' + out + '</div>';
      }
    },

    /* ================= 通用 ================= */
    text: {
      name: '文本', w: 80, h: 20, props: { text: '文本内容', fontSize: 14 },
      palette: function () { return '<div style="font-size:15px;font-weight:700;color:#fff">Aa</div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 14), 8, 48);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;color:#fff;font-size:' + fs + 'px;font-weight:600;overflow:hidden;white-space:nowrap">' + esc(c.props.text) + '</div>';
      }
    },

    image: {
      name: '图片占位', w: 48, h: 48, props: {},
      palette: function () { return '<div style="width:30px;height:30px;border-radius:5px;border:1.5px dashed rgba(255,255,255,.35);display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.5)">' + svg('image', 16) + '</div>'; },
      render: function (c) {
        return '<div style="width:100%;height:100%;border-radius:5px;border:1.5px dashed rgba(255,255,255,.35);background:rgba(255,255,255,.04);display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,.5);overflow:hidden">' + svg('image', iconPx(c)) + '</div>';
      }
    },

    rect: {
      name: '矩形', w: 60, h: 40, props: {},
      palette: function () { return '<div style="width:30px;height:22px;border-radius:4px;background:' + C.accent + '"></div>'; },
      render: function (c) { return '<div style="width:100%;height:100%;border-radius:6px;background:' + accentOf(c) + '"></div>'; }
    },

    circle: {
      name: '圆形/环', w: 40, h: 40, props: {},
      palette: function () { return '<div style="width:26px;height:26px;border-radius:50%;border:3px solid ' + C.accent + '"></div>'; },
      render: function (c) {
        var bw = Math.max(2, Math.round(Math.min(c.w, c.h) * 0.14));
        return '<div style="width:100%;height:100%;border-radius:50%;border:' + bw + 'px solid ' + accentOf(c) + ';box-sizing:border-box"></div>';
      }
    },

    button: {
      name: '自定义按钮（可换图标）', w: 32, h: 32,
      props: { name: 'settings', shape: 'circle', variant: 'solid', toggled: false, ratio: 0.62 },
      palette: function () {
        return '<div style="width:26px;height:26px;border-radius:50%;background:' + C.btnBg + ';display:flex;align-items:center;justify-content:center;color:#fff">' + iconSvg('settings', 14) + '</div>';
      },
      render: function (c) {
        var radius = c.props.shape === 'square' ? Math.max(6, Math.round(Math.min(c.w, c.h) * 0.28)) + 'px' : '50%';
        var toggled = !!c.props.toggled;
        var outline = c.props.variant === 'outline';
        var bg = toggled ? accentOf(c) : (outline ? 'transparent' : 'rgba(255,255,255,.08)');
        var border = outline ? '1.5px solid rgba(255,255,255,.28)' : '1.5px solid transparent';
        var fg = toggled ? '#1A1A1C' : '#fff';
        return '<div style="width:100%;height:100%;border-radius:' + radius + ';background:' + bg + ';border:' + border
          + ';box-sizing:border-box;display:flex;align-items:center;justify-content:center;color:' + fg + ';overflow:hidden">'
          + iconSvg(String(c.props.name || 'bolt'), iconPx(c, num(c.props.ratio, 0.62))) + '</div>';
      }
    },

    /* ================= 标注 ================= */
    note: {
      name: '注释', w: 220, h: 56, props: { text: '在这里写设计说明…' },
      palette: function () { return '<div style="width:34px;height:24px;border:1.5px dashed rgba(198,202,76,.55);border-radius:4px;display:flex;align-items:center;justify-content:center;font-size:11px;color:rgba(198,202,76,.85)">注</div>'; },
      render: function (c) {
        return '<div style="width:100%;height:100%;border:1.5px dashed rgba(198,202,76,.5);background:#211F1E;border-radius:6px;padding:6px 8px;overflow:hidden;box-sizing:border-box">'
          + '<div style="font-size:11px;line-height:1.5;color:rgba(255,255,255,.8);white-space:pre-wrap;word-break:break-word;max-height:100%;overflow:hidden">' + esc(c.props.text) + '</div>'
          + '</div>';
      }
    },

    /* ================= 数据 / 图标 ================= */
    icon: {
      name: '图标', w: 32, h: 32, props: { name: 'bolt', ratio: 0.72 },
      palette: function () { return '<div style="width:26px;height:26px;color:#fff">' + iconSvg('bolt', 24) + '</div>'; },
      render: function (c) {
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;color:' + accentOf(c) + ';overflow:hidden">'
          + iconSvg(String(c.props.name || 'bolt'), iconPx(c, num(c.props.ratio, 0.72))) + '</div>';
      }
    },

    stat: {
      name: '图标 + 数值', w: 130, h: 26, props: { name: 'memory', label: 'CPU', value: '42', unit: '%' },
      palette: function () {
        return '<div style="display:flex;align-items:center;gap:5px;color:#fff">'
          + '<span style="width:16px;height:16px;display:flex;color:' + C.accent + '">' + iconSvg('memory', 16) + '</span>'
          + '<span style="font-size:11px;color:' + C.textSecondary + '">CPU</span>'
          + '<span style="font-size:13px;font-weight:700">42%</span></div>';
      },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 16), 8, 40);
        var sub = Math.max(8, fs - 5);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:6px;overflow:hidden">'
          + '<span style="display:flex;align-items:center;color:' + accentOf(c) + '">' + iconSvg(String(c.props.name || 'bolt'), Math.round(fs * 1.15)) + '</span>'
          + (c.props.showLabel === false ? ''
            : '<span style="font-size:' + sub + 'px;color:' + C.textSecondary + ';white-space:nowrap">' + esc(c.props.label) + '</span>')
          + '<span style="font-size:' + fs + 'px;font-weight:700;color:#fff;font-variant-numeric:tabular-nums;white-space:nowrap">' + esc(c.props.value) + '</span>'
          + '<span style="font-size:' + sub + 'px;color:' + C.textSecondary + ';white-space:nowrap">' + esc(c.props.unit) + '</span>'
          + '</div>';
      }
    },

    gauge: {
      name: '量规（环 + 数值）', w: 72, h: 72, props: { progress: 0.62, value: '62', unit: '%', label: 'CPU' },
      palette: function () { return '<div style="width:34px;height:34px">' + ringHtml(34, 0.62, false, C.accent, false, false) + '</div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, Math.min(c.w, c.h) * 0.26), 8, 34);
        return '<div style="position:relative;width:100%;height:100%">'
          + '<div style="position:absolute;inset:0">' + ringHtml(Math.min(c.w, c.h), num(c.props.progress, 0.6), false, accentOf(c), false, false) + '</div>'
          + '<div style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;line-height:1.05">'
          + '<div style="color:#fff;font-weight:700;font-size:' + fs + 'px;font-variant-numeric:tabular-nums">' + esc(c.props.value) + '</div>'
          + '<div style="color:' + C.textSecondary + ';font-size:' + Math.max(8, fs * 0.52) + 'px">' + esc(c.props.unit) + '</div>'
          + '</div></div>';
      }
    },

    spark: {
      name: '曲线 / 面积', w: 180, h: 52, props: { mode: 'area', points: 28, mirrored: false },
      palette: function () {
        return '<div style="width:40px;height:20px">'
          + '<svg viewBox="0 0 100 100" preserveAspectRatio="none" width="100%" height="100%"><path d="' + smoothAreaPath(smoothVals('palette-spark', 12), false) + '" fill="' + C.accent + '" fill-opacity="0.55"/></svg></div>';
      },
      render: function (c) {
        var n = intOr(c.props.points, 28, 2, 120);
        var vals = c.props.values && c.props.values.length === n ? c.props.values : smoothVals((c.id || 'spark') + '#spark', n);
        var mir = !!c.props.mirrored;
        var pts = [], i, x, y;
        for (i = 0; i < n; i++) {
          x = n === 1 ? 50 : (i / (n - 1)) * 100;
          y = mir ? 50 - vals[i] * 46 : 100 - vals[i] * 96;
          pts.push([x, y]);
        }
        var d = curveThrough(pts);
        if (c.props.mode === 'line') {
          return '<svg viewBox="0 0 100 100" preserveAspectRatio="none" width="100%" height="100%" style="display:block">'
            + '<path d="' + d + '" fill="none" stroke="' + accentOf(c) + '" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke"/></svg>';
        }
        var uid = 'sp-' + String(c.id || 'x').replace(/[^a-zA-Z0-9_-]/g, '');
        return '<svg viewBox="0 0 100 100" preserveAspectRatio="none" width="100%" height="100%" style="display:block">'
          + '<defs><linearGradient id="' + uid + '" x1="0" y1="0" x2="0" y2="1">'
          + '<stop offset="0%" stop-color="' + accentOf(c) + '" stop-opacity="0.85"/>'
          + '<stop offset="100%" stop-color="' + accentOf(c) + '" stop-opacity="0.06"/></linearGradient></defs>'
          + '<path d="' + smoothAreaPath(vals, mir) + '" fill="url(#' + uid + ')"/></svg>';
      }
    },

    badge: {
      name: '徽标', w: 76, h: 22, props: { text: '3 条未读' },
      palette: function () {
        return '<div style="padding:2px 8px;border-radius:999px;background:' + C.btnBg + ';color:' + C.accent + ';font-size:11px">3 条未读</div>';
      },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 11), 8, 24);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;border-radius:999px;'
          + 'background:rgba(255,255,255,.08);color:' + accentOf(c) + ';font-size:' + fs + 'px;font-weight:600;'
          + 'white-space:nowrap;overflow:hidden;box-sizing:border-box;padding:0 8px">' + esc(c.props.text) + '</div>';
      }
    },

    card: {
      name: '卡片面（面板）', w: 220, h: 100, props: { radius: 12 },
      palette: function () {
        return '<div style="width:34px;height:22px;border-radius:5px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.12)"></div>';
      },
      render: function (c) {
        var r = clamp(num(c.props.radius, 12), 0, 40);
        return '<div style="width:100%;height:100%;border-radius:' + r + 'px;background:rgba(255,255,255,.06);'
          + 'border:1px solid rgba(255,255,255,.12);box-sizing:border-box"></div>';
      }
    },

    divider: {
      name: '分隔线', w: 140, h: 1, props: {},
      palette: function () { return '<div style="width:34px;height:1px;background:' + C.track + '"></div>'; },
      render: function () {
        return '<div style="width:100%;height:100%;background:' + C.track + '"></div>';
      }
    },

    /* ================= 表单控件（Win32 / WinUI 对照：Edit·ComboBox·ToggleSwitch·CheckBox·RadioButton·Trackbar·UpDown·TabControl） ================= */
    input: {
      name: '文本框', w: 240, h: 40, props: { text: '搜索或输入…', caret: true },
      palette: function () { return '<div style="width:36px;height:16px;border-radius:5px;border:1px solid rgba(255,255,255,.18);background:rgba(255,255,255,.05)"></div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 13), 8, 32);
        var caret = c.props.caret === false ? '' : '<span style="display:inline-block;flex:0 0 auto;width:1px;height:' + Math.round(fs * 1.15) + 'px;background:' + accentOf(c) + '"></span>';
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:1px;padding:0 10px;box-sizing:border-box;border-radius:8px;'
          + 'background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.16);overflow:hidden">'
          + '<span style="min-width:0;font-size:' + fs + 'px;color:' + C.textSecondary + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span>' + caret + '</div>';
      }
    },

    select: {
      name: '下拉框', w: 240, h: 40, props: { text: '自动（跟随系统）' },
      palette: function () { return '<div style="width:36px;height:16px;border-radius:5px;border:1px solid rgba(255,255,255,.18);background:rgba(255,255,255,.05);display:flex;align-items:center;justify-content:flex-end;padding-right:2px;color:rgba(255,255,255,.5)">' + iconSvg('expand_more', 10) + '</div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 13), 8, 32);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:6px;padding:0 10px;box-sizing:border-box;border-radius:8px;'
          + 'background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.16);overflow:hidden">'
          + '<span style="flex:1 1 auto;min-width:0;font-size:' + fs + 'px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span>'
          + '<span style="flex:0 0 auto;display:flex;color:' + C.textSecondary + '">' + iconSvg('expand_more', Math.round(fs * 1.15)) + '</span></div>';
      }
    },

    toggle: {
      name: '开关', w: 44, h: 24, props: { on: true },
      palette: function () { return '<div style="width:30px;height:16px;border-radius:999px;background:' + C.accent + ';position:relative"><span style="position:absolute;right:2px;top:2px;bottom:2px;aspect-ratio:1/1;border-radius:50%;background:#fff"></span></div>'; },
      render: function (c) {
        var on = c.props.on !== false;
        return '<div style="position:relative;width:100%;height:100%;border-radius:999px;background:' + (on ? accentOf(c) : C.track) + '">'
          + '<span style="position:absolute;top:3px;bottom:3px;aspect-ratio:1/1;border-radius:50%;background:#fff;box-shadow:0 1px 3px rgba(0,0,0,.35);'
          + (on ? 'right:3px' : 'left:3px') + '"></span></div>';
      }
    },

    checkbox: {
      name: '复选框', w: 140, h: 24, props: { text: '启用', checked: true },
      palette: function () { return '<div style="width:16px;height:16px;border-radius:4px;background:' + C.accent + ';color:#201F20;display:flex;align-items:center;justify-content:center">' + iconSvg('check', 12) + '</div>'; },
      render: function (c) {
        var on = c.props.checked !== false;
        var fs = clamp(num(c.props.fontSize, 13), 8, 28);
        var d = Math.round(fs * 1.35);
        var box = '<span style="flex:0 0 auto;width:' + d + 'px;height:' + d + 'px;border-radius:4px;box-sizing:border-box;display:flex;align-items:center;justify-content:center;'
          + (on ? 'background:' + accentOf(c) + ';color:#201F20' : 'border:1.5px solid rgba(255,255,255,.35)') + '">'
          + (on ? iconSvg('check', Math.round(fs * 0.95)) : '') + '</span>';
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:8px">' + box
          + '<span style="font-size:' + fs + 'px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span></div>';
      }
    },

    radio: {
      name: '单选', w: 140, h: 24, props: { text: '跟随系统', checked: true },
      palette: function () { return '<div style="width:16px;height:16px;border-radius:50%;border:2px solid ' + C.accent + ';box-sizing:border-box;display:flex;align-items:center;justify-content:center"><span style="width:6px;height:6px;border-radius:50%;background:' + C.accent + '"></span></div>'; },
      render: function (c) {
        var on = c.props.checked !== false;
        var fs = clamp(num(c.props.fontSize, 13), 8, 28);
        var d = Math.round(fs * 1.35);
        var box = '<span style="flex:0 0 auto;width:' + d + 'px;height:' + d + 'px;border-radius:50%;box-sizing:border-box;display:flex;align-items:center;justify-content:center;'
          + (on ? 'border:2px solid ' + accentOf(c) : 'border:1.5px solid rgba(255,255,255,.35)') + '">'
          + (on ? '<span style="width:44%;height:44%;border-radius:50%;background:' + accentOf(c) + '"></span>' : '') + '</span>';
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:8px">' + box
          + '<span style="font-size:' + fs + 'px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span></div>';
      }
    },

    slider: {
      name: '滑块', w: 200, h: 24, props: { progress: 0.6 },
      palette: function () { return '<div style="width:34px;height:4px;border-radius:2px;background:' + C.track + ';position:relative"><span style="position:absolute;left:60%;top:50%;transform:translate(-50%,-50%);width:11px;height:11px;border-radius:50%;background:#fff"></span></div>'; },
      render: function (c) {
        var p = clamp(num(c.props.progress, 0.6), 0, 1);
        return '<div style="position:relative;width:100%;height:100%;display:flex;align-items:center">'
          + '<div style="width:100%;height:4px;border-radius:2px;background:' + C.track + ';overflow:hidden">'
          + '<div style="height:100%;width:' + (p * 100) + '%;background:' + accentOf(c) + '"></div></div>'
          + '<span style="position:absolute;left:' + (p * 100) + '%;top:50%;transform:translate(-50%,-50%);width:14px;height:14px;border-radius:50%;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.4)"></span></div>';
      }
    },

    stepper: {
      name: '步进器', w: 108, h: 32, props: { text: '1' },
      palette: function () { return '<div style="width:36px;height:16px;border-radius:5px;border:1px solid rgba(255,255,255,.18);display:flex;align-items:center;justify-content:space-between;padding:0 3px;font-size:9px;color:rgba(255,255,255,.7)"><span>−</span><span>1</span><span>＋</span></div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 14), 8, 28);
        var btn = '<span style="flex:0 0 auto;width:18px;height:18px;border-radius:5px;display:flex;align-items:center;justify-content:center;background:rgba(255,255,255,.1);color:#fff;font-size:12px;line-height:1">';
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:space-between;gap:6px;padding:0 7px;box-sizing:border-box;border-radius:8px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.16)">'
          + btn + '−</span><span style="flex:1 1 auto;text-align:center;font-size:' + fs + 'px;color:#fff;font-variant-numeric:tabular-nums">' + esc(c.props.text) + '</span>' + btn + '＋</span></div>';
      }
    },

    segmented: {
      name: '分段选择', w: 160, h: 32, props: { text: '音乐/天气/监控', count: 3, active: 0 },
      palette: function () { return '<div style="width:38px;height:16px;border-radius:5px;background:rgba(255,255,255,.05);display:flex;gap:2px;padding:2px;box-sizing:border-box"><span style="flex:1;border-radius:3px;background:rgba(255,255,255,.16)"></span><span style="flex:1;border-radius:3px"></span></div>'; },
      render: function (c) {
        var parts = String(c.props.text || '').split('/').map(function (t) { return t.trim(); }).filter(function (t) { return t.length; });
        if (!parts.length) parts = ['音乐', '天气', '监控'];
        var n = Math.max(1, Math.round(num(c.props.count, parts.length)));
        var act = Math.max(0, Math.min(n - 1, Math.round(num(c.props.active, 0))));
        var fs = clamp(num(c.props.fontSize, Math.max(8, Math.round(Math.min(c.h, 20) * 0.62))), 8, 28);
        var out = '';
        for (var i = 0; i < n; i++) {
          var on = (i === act);
          out += '<span style="flex:1 1 0;min-width:0;display:flex;align-items:center;justify-content:center;border-radius:6px;font-size:' + fs + 'px;'
            + (on ? 'background:rgba(255,255,255,.14);color:#fff;font-weight:600' : 'color:' + C.textSecondary)
            + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(parts[i] || ('第' + (i + 1) + '段')) + '</span>';
        }
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:2px;padding:3px;box-sizing:border-box;border-radius:9px;background:rgba(255,255,255,.05)">' + out + '</div>';
      }
    },

    /* ================= 内容 / 状态（Win32 / WinUI 对照：ListView 项·PersonPicture·InfoBadge·PipsPager·ScrollBar·TextBlock·InfoBar·SysLink） ================= */
    row: {
      name: '列表行', w: 360, h: 52, props: { icon: 'assignment', text: '主文本', sub: '副文本', trailing: '14:48' },
      palette: function () { return '<div style="width:36px;height:20px;border-radius:5px;background:rgba(255,255,255,.05);display:flex;align-items:center;gap:3px;padding:0 3px;box-sizing:border-box;color:rgba(255,255,255,.75)"><span style="flex:0 0 auto">' + iconSvg('assignment', 11) + '</span><span style="flex:1 1 auto;height:3px;border-radius:2px;background:rgba(255,255,255,.3)"></span></div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 14), 8, 32);
        var box = Math.round(fs * 2);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:10px;padding:0 12px;box-sizing:border-box;border-radius:10px;background:rgba(255,255,255,.05);overflow:hidden">'
          + '<span style="flex:0 0 auto;width:' + box + 'px;height:' + box + 'px;border-radius:8px;background:rgba(255,255,255,.08);color:#fff;display:flex;align-items:center;justify-content:center">'
          + iconSvg(String(c.props.icon || 'assignment'), Math.round(fs * 1.3)) + '</span>'
          + '<span style="flex:1 1 auto;min-width:0;display:flex;flex-direction:column;gap:2px">'
          + '<span style="font-size:' + fs + 'px;font-weight:600;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span>'
          + '<span style="font-size:' + Math.max(8, fs - 3) + 'px;color:' + C.textSecondary + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.sub) + '</span></span>'
          + '<span style="flex:0 0 auto;font-size:' + Math.max(8, fs - 3) + 'px;color:' + C.textSecondary + ';font-variant-numeric:tabular-nums">' + esc(c.props.trailing) + '</span></div>';
      }
    },

    keyValue: {
      name: '键值行', w: 220, h: 24, props: { text: '温度', value: '58 °C' },
      palette: function () { return '<div style="width:34px;height:10px;display:flex;align-items:center;justify-content:space-between"><span style="width:14px;height:3px;border-radius:2px;background:rgba(255,255,255,.3)"></span><span style="width:10px;height:4px;border-radius:2px;background:#fff"></span></div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 13), 8, 28);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:space-between;gap:12px;overflow:hidden">'
          + '<span style="font-size:' + Math.max(8, fs - 2) + 'px;color:' + C.textSecondary + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span>'
          + '<span style="flex:0 0 auto;font-size:' + fs + 'px;font-weight:600;color:#fff;font-variant-numeric:tabular-nums">' + esc(c.props.value) + '</span></div>';
      }
    },

    avatar: {
      name: '头像', w: 40, h: 40, props: { text: '张', icon: '' },
      palette: function () { return '<div style="width:26px;height:26px;border-radius:50%;background:linear-gradient(135deg,' + C.accent + '80,' + C.disc + ' 72%);display:flex;align-items:center;justify-content:center;font-size:12px;color:#fff">张</div>'; },
      render: function (c) {
        var s = Math.min(c.w, c.h);
        var inner = c.props.icon
          ? iconSvg(String(c.props.icon), Math.round(s * 0.55))
          : '<span style="font-size:' + clamp(num(c.props.fontSize, Math.round(s * 0.42)), 8, 40) + 'px;font-weight:700;color:#fff">' + esc(String(c.props.text || '').slice(0, 1)) + '</span>';
        return '<div style="width:100%;height:100%;border-radius:50%;overflow:hidden;display:flex;align-items:center;justify-content:center;'
          + 'background:linear-gradient(135deg,' + accentOf(c) + '80,' + C.disc + ' 72%)">' + inner + '</div>';
      }
    },

    chip: {
      name: '标签（描边）', w: 64, h: 24, props: { text: '工作' },
      palette: function () { return '<div style="padding:1px 7px;border-radius:999px;border:1px solid rgba(255,255,255,.22);color:rgba(255,255,255,.6);font-size:10px">工作</div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 11), 8, 22);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;box-sizing:border-box;padding:0 8px;'
          + 'border-radius:999px;border:1px solid rgba(255,255,255,.2);color:' + C.textSecondary + ';font-size:' + fs + 'px;white-space:nowrap;overflow:hidden">' + esc(c.props.text) + '</div>';
      }
    },

    dot: {
      name: '状态点', w: 10, h: 10, props: { tone: 'accent' },
      palette: function () { return '<div style="width:9px;height:9px;border-radius:50%;background:' + C.accent + '"></div>'; },
      render: function (c) {
        var tone = c.props.tone || 'accent';
        var col = tone === 'danger' ? C.danger : (tone === 'muted' ? C.textSecondary : accentOf(c));
        var s = Math.min(c.w, c.h);
        var glow = c.props.glow === false ? '' : 'box-shadow:0 0 ' + Math.max(3, s) + 'px ' + col + '99;';
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center">'
          + '<span style="width:' + s + 'px;height:' + s + 'px;border-radius:50%;background:' + col + ';' + glow + '"></span></div>';
      }
    },

    pager: {
      name: '页码点', w: 72, h: 10, props: { count: 5, active: 2 },
      palette: function () { return '<div style="display:flex;align-items:center;gap:3px"><span style="width:10px;height:6px;border-radius:3px;background:' + C.accent + '"></span><span style="width:6px;height:6px;border-radius:50%;background:rgba(255,255,255,.28)"></span><span style="width:6px;height:6px;border-radius:50%;background:rgba(255,255,255,.28)"></span></div>'; },
      render: function (c) {
        var n = Math.max(1, Math.round(num(c.props.count, 5)));
        var act = Math.max(0, Math.min(n - 1, Math.round(num(c.props.active, 0))));
        var s = Math.min(c.h, 10);
        var out = '';
        for (var i = 0; i < n; i++) {
          var on = (i === act);
          out += '<span style="height:' + s + 'px;width:' + (on ? Math.round(s * 2.4) : s) + 'px;border-radius:' + Math.round(s / 2) + 'px;background:'
            + (on ? accentOf(c) : 'rgba(255,255,255,.28)') + '"></span>';
        }
        return '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;gap:5px">' + out + '</div>';
      }
    },

    scrollbar: {
      name: '滚动条', w: 4, h: 64, props: { thumb: 0.35, horizontal: false },
      palette: function () { return '<div style="width:4px;height:28px;border-radius:2px;background:' + C.track + ';position:relative"><span style="position:absolute;left:0;right:0;top:0;height:40%;border-radius:2px;background:rgba(255,255,255,.45)"></span></div>'; },
      render: function (c) {
        var t = clamp(num(c.props.thumb, 0.35), 0.08, 1);
        var base = 'position:relative;width:100%;height:100%;border-radius:999px;background:' + C.track;
        var thumb = 'position:absolute;border-radius:999px;background:rgba(255,255,255,.45)';
        if (c.props.horizontal) {
          return '<div style="' + base + '"><span style="' + thumb + ';left:0;top:0;bottom:0;width:' + (t * 100) + '%"></span></div>';
        }
        return '<div style="' + base + '"><span style="' + thumb + ';left:0;right:0;top:0;height:' + (t * 100) + '%"></span></div>';
      }
    },

    paragraph: {
      name: '段落文本', w: 300, h: 60, props: { text: '这是一段会自动换行的正文。超过设定行数后截断并显示省略号，用来验证展开态里长文本的观感。', fontSize: 13, lines: 3 },
      palette: function () { return '<div style="display:flex;flex-direction:column;gap:3px;justify-content:center"><span style="width:34px;height:3px;border-radius:2px;background:rgba(255,255,255,.45)"></span><span style="width:30px;height:3px;border-radius:2px;background:rgba(255,255,255,.3)"></span><span style="width:20px;height:3px;border-radius:2px;background:rgba(255,255,255,.2)"></span></div>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 13), 8, 32);
        var ln = Math.max(1, Math.round(num(c.props.lines, 3)));
        return '<div style="width:100%;height:100%;overflow:hidden;font-size:' + fs + 'px;line-height:1.4;color:rgba(255,255,255,.86);'
          + 'display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:' + ln + ';word-break:break-word">' + esc(c.props.text) + '</div>';
      }
    },

    banner: {
      name: '信息条', w: 320, h: 40, props: { text: '未检测到网络，已切换到离线模式', tone: 'warning' },
      palette: function () { return '<div style="width:36px;height:16px;border-radius:5px;background:rgba(255,255,255,.05);border:1px solid #FFB45455;display:flex;align-items:center;gap:3px;padding:0 3px;box-sizing:border-box;color:#FFB454">' + iconSvg('warning', 11) + '<span style="flex:1;height:3px;border-radius:2px;background:rgba(255,255,255,.25)"></span></div>'; },
      render: function (c) {
        var tone = c.props.tone || 'info';
        var col = tone === 'error' ? C.danger : (tone === 'warning' ? '#FFB454' : (tone === 'success' ? accentOf(c) : C.textSecondary));
        var ico = tone === 'error' ? 'error' : (tone === 'warning' ? 'warning' : 'info');
        var fs = clamp(num(c.props.fontSize, 12), 8, 26);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;gap:8px;padding:0 10px;box-sizing:border-box;border-radius:8px;'
          + 'background:rgba(255,255,255,.05);border:1px solid ' + col + '55;overflow:hidden">'
          + '<span style="flex:0 0 auto;display:flex;color:' + col + '">' + iconSvg(ico, Math.round(fs * 1.2)) + '</span>'
          + '<span style="flex:1 1 auto;min-width:0;font-size:' + fs + 'px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">' + esc(c.props.text) + '</span></div>';
      }
    },

    link: {
      name: '超链接', w: 96, h: 22, props: { text: '查看全部' },
      palette: function () { return '<span style="color:' + C.accent + ';font-size:11px;text-decoration:underline;text-underline-offset:2px">查看全部</span>'; },
      render: function (c) {
        var fs = clamp(num(c.props.fontSize, 12), 8, 24);
        return '<div style="width:100%;height:100%;display:flex;align-items:center;color:' + accentOf(c) + ';font-size:' + fs + 'px;overflow:hidden">'
          + '<span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-decoration:underline;text-underline-offset:2px">' + esc(c.props.text) + '</span></div>';
      }
    },

    /* ================= 特殊：空状态面板（与主面板同款胶囊渲染） ================= */
    panelUnfold: {
      name: '空面板 · 展开态', w: 560, h: 160, props: { label: '展开态', showLabel: false, radius: 0 },
      palette: function () { return '<div style="width:38px;height:16px;border-radius:8px;background:' + C.pill + ';box-shadow:0 1px 6px rgba(0,0,0,0.25)"></div>'; },
      render: function (c) { return emptyPanel(c, '展开态'); }
    },

    panelWait: {
      name: '空面板 · 等待态', w: 560, h: 60, props: { label: '等待态', showLabel: false, radius: 0 },
      palette: function () { return '<div style="width:38px;height:14px;border-radius:7px;background:' + C.pill + ';box-shadow:0 1px 6px rgba(0,0,0,0.25)"></div>'; },
      render: function (c) { return emptyPanel(c, '等待态'); }
    },

    panelContract: {
      name: '空面板 · 收缩态', w: 200, h: 60, props: { label: '收缩态', showLabel: false, radius: 0 },
      palette: function () { return '<div style="width:26px;height:14px;border-radius:7px;background:' + C.pill + ';box-shadow:0 1px 6px rgba(0,0,0,0.25)"></div>'; },
      render: function (c) { return emptyPanel(c, '收缩态'); }
    }
  };

  return {
    defs: defs,
    groups: [
      { id: 'special', name: '特殊', items: ['panelUnfold', 'panelWait', 'panelContract'] },
      { id: 'music',  name: '音乐', items: ['cover', 'title', 'prev', 'play', 'next', 'expand', 'ring', 'progress', 'lyrics', 'spectrum'] },
      { id: 'form',   name: '表单', items: ['input', 'select', 'toggle', 'checkbox', 'radio', 'slider', 'stepper', 'segmented'] },
      { id: 'content', name: '内容 / 状态', items: ['row', 'keyValue', 'avatar', 'chip', 'dot', 'pager', 'scrollbar', 'paragraph', 'banner', 'link'] },
      { id: 'data',   name: '数据 / 图标', items: ['icon', 'stat', 'gauge', 'spark', 'badge'] },
      { id: 'common', name: '通用', items: ['text', 'image', 'rect', 'circle', 'button', 'card', 'divider'] },
      { id: 'aux',    name: '标注', items: ['note'] }
    ],
    usesAccent: function (type) {
      return ['cover', 'progress', 'rect', 'circle', 'spectrum', 'ring', 'icon', 'stat', 'gauge', 'spark', 'badge', 'button',
        'input', 'avatar', 'slider', 'dot', 'pager', 'link'].indexOf(type) >= 0;
    },
    icons: ICONS,
    util: { esc: esc, num: num, clamp: clamp, svg: svg, accentOf: accentOf, fmtTime: fmtTime }
  };
})();
