/* canvas.js — 灵动岛布局画布引擎（同插件多状态）
 * 状态切换（等待/展开/收缩/自定义，各自独立布局与尺寸）/ 拖拽 / 缩放 / 网格吸附 /
 * 对齐参考线 / 层级 / 属性面板 / 注释开关 / 撤销重做 / 持久化 / 导入导出。
 * 经典 <script>（非 ES module），保证 file:// 双击即可运行。
 */
window.IslandCanvas = (function () {
  'use strict';

  var Spec = window.IslandSpec;
  var Comp = window.IslandComponents;

  var DEFAULT_GRID = 8;   /* 网格密度的默认值（px）；可在工具栏改，吸附步长 = 可视网格间距 */
  var MIN = 8;        /* 最小尺寸 */
  var GUIDE_T = 4;    /* 对齐吸附阈值(px) */
  var PAD = 96;       /* 舞台内边距（给缩放手柄/注释留空间） */
  var HANDLES = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'];

  function roundTo(v, s) { return Math.round(v / s) * s; }
  function clone(o) { return JSON.parse(JSON.stringify(o)); }
  function intOr(v, d) { var n = parseFloat(v); return isFinite(n) ? Math.round(n) : d; }
  function clampInt(v, lo, hi, d) {
    var n = Math.round(parseFloat(v));
    if (!isFinite(n)) n = (d == null ? lo : d);
    return Math.min(hi, Math.max(lo, n));
  }
  function clampNum(v, lo, hi, d) {
    var n = parseFloat(v);
    if (!isFinite(n)) n = d;
    return Math.min(hi, Math.max(lo, n));
  }
  function el(id) { return document.getElementById(id); }

  /* 含文字组件的字号预设：[默认值, 上限]。
     必须放在模块作用域——属性面板的生成端（_renderPanel）与绑定端（_bindPanel）是两个函数、
     都要读它；写在任一函数内部，另一端就会拿不到，表现为「面板上显示得出字号，改了却毫无反应」。 */
  var FS_PRESET = {
    text: [14, 48], title: [14, 48], lyrics: [13, 48],
    stat: [16, 40], gauge: [16, 34], badge: [11, 24], progress: [10, 24],
    input: [13, 32], select: [13, 32], checkbox: [13, 28], radio: [13, 28], stepper: [14, 28],
    segmented: [12, 28], row: [14, 32], keyValue: [13, 28], avatar: [16, 40], chip: [11, 22],
    paragraph: [13, 32], banner: [12, 26], link: [12, 24]
  };

  /* 图标名列表：按 a–z 排序（原来的顺序是图标表的编写顺序，69 项根本找不到）。
     交给 <datalist>：浏览器原生支持「输入即过滤 + 可直接敲名字」，不用自己写过滤逻辑。
     选项只给 value、不给文字内容——否则一边显示值一边显示标签，看着像重复。 */
  function iconListOptions() {
    var names = Object.keys((window.IslandComponents && window.IslandComponents.icons) || {}).sort();
    var out = '';
    for (var i = 0; i < names.length; i++) out += '<option value="' + names[i] + '"></option>';
    return out;
  }

  function isIconName(n) {
    var bag = (window.IslandComponents && window.IslandComponents.icons) || {};
    return !!n && Object.prototype.hasOwnProperty.call(bag, n);
  }

  function fieldNum(id, label, val) {
    return '<label><span>' + label + '</span><input type="number" id="' + id + '" step="1" value="' + Math.round(val) + '"></label>';
  }
  function fieldText(id, label, val) {
    return '<label class="panel-field"><span>' + label + '</span><input type="text" id="' + id + '" value="' + Comp.util.esc(val) + '"></label>';
  }
  function fieldArea(id, label, val) {
    return '<label class="panel-field"><span>' + label + '</span><textarea id="' + id + '" rows="3">' + Comp.util.esc(val) + '</textarea></label>';
  }

  function resizeRect(o, dir, dx, dy) {
    var x = o.x, y = o.y, w = o.w, h = o.h;
    if (dir.indexOf('e') >= 0) w = o.w + dx;
    if (dir.indexOf('w') >= 0) { x = o.x + dx; w = o.w - dx; }
    if (dir.indexOf('s') >= 0) h = o.h + dy;
    if (dir.indexOf('n') >= 0) { y = o.y + dy; h = o.h - dy; }
    if (w < MIN) { if (dir.indexOf('w') >= 0) x -= (MIN - w); w = MIN; }
    if (h < MIN) { if (dir.indexOf('n') >= 0) y -= (MIN - h); h = MIN; }
    return { x: x, y: y, w: w, h: h };
  }

  /* ================= Engine ================= */
  function Engine(cfg) {
    this.cfg = cfg || {};
    this.defs = Comp.defs;
    this.states = this.cfg.states || Spec.STATES;
    this.storageKey = this.cfg.storageKey || 'endfield.layout';
    this.pageName = this.cfg.pageName || 'layout';
    /* 设计名：一个画布可以装多个设计，导出文件名与 JSON 的 page 字段都用它；
       导入时自动采用文件里的名字（导入 clipboard-all.json → 再导出仍是 clipboard-all.json） */
    this.designName = this.cfg.designName || this.pageName;

    /* 状态分两栏：内置态做按钮；其余（自定义 / 用户新增）收进「自定义 ▾」下拉 */
    this.builtinStates = [];
    this.customStates = [];
    var hasCustom = false;
    for (var si = 0; si < this.states.length; si++) {
      var sd = this.states[si];
      if (sd.id === 'custom') {
        hasCustom = true;
        this.customStates.push({ id: sd.id, name: sd.name || '自定义', w: sd.w, h: sd.h });
      } else {
        this.builtinStates.push({ id: sd.id, name: sd.name, w: sd.w, h: sd.h });
      }
    }
    if (!hasCustom) this.customStates.push({ id: 'custom', name: '自定义', w: 560, h: 160 });
    this.states = this.builtinStates.concat(this.customStates);

    this.el = {
      palette: el('palette'), workbench: el('workbench'), stage: el('stage'),
      island: el('island'), items: el('islandItems'), guides: el('guides'),
      props: el('propsPanel'), islandSize: el('islandSize'),
      stateBar: el('stateBar'), sizeW: el('sizeW'), sizeH: el('sizeH'), sizeBox: el('sizeBox'),
      gridToggle: el('tglGrid'), gridSizeInput: el('gridSize'), guidesToggle: el('tglGuides'),
      previewToggle: el('tglPreview'), notesToggle: el('tglNotes'),
      zoomLabel: el('zoomLabel'),
      importInput: el('importInput'), btnReset: el('btnReset'),
      btnExport: el('btnExport'), btnExportAll: el('btnExportAll'), btnImport: el('btnImport')
    };

    this.state = {
      active: null,
      spec: { width: 560, height: 160, panelOpacity: 1 },
      comps: [], selected: null,
      gridSnap: true, gridSize: DEFAULT_GRID, guidesOn: true, preview: false, showNotes: true,
      zoom: 1
    };

    this.layouts = {};
    this.history = [];
    this.hIndex = -1;
    this.drag = null;
    this.ghost = null;
    this._elMap = {};
    this._persistT = null;
    this._seq = 0;

    this._load();
    this._buildStateBar();
    this._bindToolbar();
    this._bindGlobal();
    this._bindStage();
    this._buildPalette();
    this.sync();
  }

  /* ---------- 状态 / 布局 ---------- */
  Engine.prototype._stateDef = function (id) {
    for (var i = 0; i < this.states.length; i++) if (this.states[i].id === id) return this.states[i];
    return null;
  };
  Engine.prototype._defaultStateId = function () {
    if (this.cfg.defaultState && this._stateDef(this.cfg.defaultState)) return this.cfg.defaultState;
    return (this.states[0] && this.states[0].id) || 'wait';
  };
  /* 兼容旧字段：把 preset 名（music / shrink / …）解析成状态 id */
  Engine.prototype._resolveStateId = function (raw) {
    if (typeof raw !== 'string') return null;
    if (this._stateDef(raw)) return raw;
    var alias = { music: 'unfold', shrink: 'contract', wait: 'wait', custom: 'custom' };
    return alias[raw] || null;
  };
  Engine.prototype._blankLayout = function (id) {
    var s = this._stateDef(id);
    return { spec: { width: s ? s.w : 560, height: s ? s.h : 160, panelOpacity: 1 }, components: [] };
  };
  Engine.prototype._capture = function () {
    var sp = this.state.spec;
    this.layouts[this.state.active] = {
      spec: {
        width: sp.width,
        height: sp.height,
        panelOpacity: (sp.panelOpacity == null ? 1 : sp.panelOpacity)
      },
      components: this.state.comps
    };
  };
  Engine.prototype._applyLayout = function (id) {
    if (!this.layouts[id]) {
      var d = this.cfg.defaultDesigns && this.cfg.defaultDesigns[id];
      this.layouts[id] = d ? this._sanitize(d) : this._blankLayout(id);
    }
    this.state.active = id;
    var spec = this.layouts[id].spec;
    this.state.spec = {
      width: spec.width,
      height: spec.height,
      panelOpacity: (spec.panelOpacity == null ? 1 : spec.panelOpacity)
    };
    this.state.comps = this.layouts[id].components;
    this.state.selected = null;
  };
  Engine.prototype._switchState = function (id) {
    if (id === this.state.active || !this._stateDef(id)) return;
    this._capture();
    this._applyLayout(id);
    this._pushHistory();
    this._renderIsland();
    this._renderItems();
    this._renderToolbar();
    this._renderPanel();
    this._persist();
  };

  /* ---------- 渲染 ---------- */
  Engine.prototype.sync = function (opts) {
    this._renderIsland();
    this._renderItems();
    this._renderToolbar();
    this._renderPanel();
    this._applyNotesVisibility();
    this._applyZoom();
    if (!opts || opts.persist !== false) this._persist();
  };

  /// 缩放：CSS zoom 同时缩放布局与渲染（滚动区自动适配），仅需换算指针坐标。
  Engine.prototype._applyZoom = function () {
    var z = this.state.zoom || 1;
    if (this.el.stage) this.el.stage.style.zoom = String(z);
    if (this.el.zoomLabel) this.el.zoomLabel.textContent = Math.round(z * 100) + '%';
  };

  Engine.prototype._setZoom = function (z) {
    this.state.zoom = Math.min(3, Math.max(0.25, Math.round(z * 100) / 100));
    this._applyZoom();
  };

  Engine.prototype._applyNotesVisibility = function () {
    if (this.el.workbench) this.el.workbench.classList.toggle('hide-notes', !this.state.showNotes);
  };

  Engine.prototype._renderIsland = function () {
    var w = this.state.spec.width, h = this.state.spec.height;
    var isl = this.el.island, st = this.el.stage;
    if (!isl || !st) return;
    isl.style.width = w + 'px';
    isl.style.height = h + 'px';
    isl.style.borderRadius = Spec.pillRadius(h) + 'px';
    isl.style.left = PAD + 'px';
    isl.style.top = PAD + 'px';
    /* 声明的面板本体不透明度（只作用于 ::before 面板层，不影响岛内元素） */
    var pa = (this.state.spec.panelOpacity == null ? 1 : this.state.spec.panelOpacity);
    isl.style.setProperty('--panel-alpha', String(pa));
    /* 面板被调透明后仍要看得见画的边界，否则画板会"消失"找不到 */
    isl.style.setProperty('--panel-outline', pa >= 0.999 ? '0' : '0.55');
    st.style.width = (w + PAD * 2) + 'px';
    st.style.height = (h + PAD * 2) + 'px';
    if (this.el.islandSize) this.el.islandSize.textContent = '岛 ' + w + ' × ' + h + ' px';
    this._applyGrid();
  };

  /* 网格密度：吸附步长 = 可视网格间距（Altium 式的「可指定网格」） */
  Engine.prototype._applyGrid = function () {
    var gs = clampInt(this.state.gridSize, 1, 100, DEFAULT_GRID);
    this.state.gridSize = gs;
    var isl = this.el.island;
    if (isl) {
      isl.style.setProperty('--grid', gs + 'px');
      isl.style.setProperty('--pill-radius', Spec.pillRadius(this.state.spec.height) + 'px');
    }
    var items = this.el.items;
    /* 密度小于 4px 时网格线会糊成一片，自动不显示网格（吸附仍然生效） */
    if (items) items.classList.toggle('show-grid', !!this.state.gridSnap && gs >= 4);
    if (this.el.gridSizeInput && document.activeElement !== this.el.gridSizeInput) this.el.gridSizeInput.value = gs;
  };

  Engine.prototype._styleItem = function (node, c) {
    node.style.left = c.x + 'px';
    node.style.top = c.y + 'px';
    node.style.width = c.w + 'px';
    node.style.height = c.h + 'px';
    node.style.zIndex = c.z;
    /* 不透明度只作用在 .ic-body 上：元素调到 0%（完全透明）时，
       选中框与缩放手柄仍然可见、可拖，不会变成"选中了就找不着" */
    node.style.opacity = 1;
    var body = node.querySelector('.ic-body');
    if (body) body.style.opacity = (c.opacity == null ? 1 : c.opacity);
  };

  Engine.prototype._renderItems = function () {
    var host = this.el.items;
    if (!host) return;
    host.innerHTML = '';
    this._elMap = {};
    var comps = this.state.comps.slice().sort(function (a, b) { return (a.z || 0) - (b.z || 0); });
    for (var i = 0; i < comps.length; i++) {
      var c = comps[i];
      var def = this.defs[c.type];
      if (!def) continue;
      var node = document.createElement('div');
      node.className = 'ic-item' + (c.id === this.state.selected ? ' selected' : '');
      node.setAttribute('data-id', c.id);
      node.setAttribute('data-type', c.type);
      node.setAttribute('tabindex', '0');
      node.setAttribute('role', 'button');
      node.setAttribute('aria-label', def.name);
      var body = document.createElement('div');
      body.className = 'ic-body';
      body.innerHTML = def.render(c);
      node.appendChild(body);
      for (var j = 0; j < HANDLES.length; j++) {
        var hd = document.createElement('div');
        hd.className = 'ic-handle h-' + HANDLES[j];
        hd.setAttribute('data-h', HANDLES[j]);
        node.appendChild(hd);
      }
      this._styleItem(node, c);
      this._elMap[c.id] = node;
      host.appendChild(node);
    }
  };

  Engine.prototype._applyItemStyle = function (c) {
    var node = this._elMap[c.id];
    if (node) this._styleItem(node, c);
  };

  Engine.prototype._updateContent = function (c) {
    var node = this._elMap[c.id];
    if (!node) return;
    var body = node.querySelector('.ic-body');
    var def = this.defs[c.type];
    if (body && def) body.innerHTML = def.render(c);
  };

  /* ---------- 模板：把已归档的设计当成数据载入，与"哪个插件的页面"彻底解耦 ---------- */
  Engine.prototype._templates = function () {
    var out = [];
    var bag = (typeof window !== 'undefined' && window.IslandDesigns) ? window.IslandDesigns : null;
    if (!bag) return out;
    for (var k in bag) {
      if (!Object.prototype.hasOwnProperty.call(bag, k)) continue;
      if (bag[k] && typeof bag[k] === 'object') out.push(k);
    }
    out.sort();
    return out;
  };

  Engine.prototype._hasContent = function () {
    for (var k in this.layouts) {
      if (!Object.prototype.hasOwnProperty.call(this.layouts, k)) continue;
      var l = this.layouts[k];
      if (l && l.components && l.components.length) return true;
    }
    return false;
  };

  Engine.prototype._loadTemplate = function (name) {
    var bag = (typeof window !== 'undefined') ? window.IslandDesigns : null;
    var tpl = bag && bag[name];
    if (!tpl) { alert('模板不存在：' + name); return; }
    /* 画布上已经有内容时才确认，避免首次点开就被弹窗挡住 */
    if (this._hasContent() && !window.confirm('载入模板「' + name + '」？\n当前画布内容会被替换（建议先「导出 JSON」备份，或用 Ctrl+Z 撤销）。')) return;
    this.designName = name;
    this.layouts = {};
    for (var k in tpl) {
      if (!Object.prototype.hasOwnProperty.call(tpl, k)) continue;
      if (this._stateDef(k)) this.layouts[k] = this._sanitize(tpl[k]);
    }
    var act = this._stateDef(this.state.active) ? this.state.active : this._defaultStateId();
    this._applyLayout(act);
    this._pushHistory();
    this._renderIsland();
    this._renderItems();
    this._renderToolbar();
    this._renderPanel();
    this._applyNotesVisibility();
    this._persist();
  };

  Engine.prototype._fillTemplateOptions = function () {
    if (!this.el.templateSel) return;
    var names = this._templates();
    var sig = names.join('|');
    if (this._tplSig === sig) return;
    this._tplSig = sig;
    var html = '<option value="">载入模板…</option>';
    for (var i = 0; i < names.length; i++) {
      html += '<option value="' + Comp.util.esc(names[i]) + '">' + Comp.util.esc(names[i]) + '</option>';
    }
    this.el.templateSel.innerHTML = html;
  };

  Engine.prototype._ensureTemplate = function () {
    if (this.el.templateSel) { this._fillTemplateOptions(); return; }
    if (!this._templates().length) return;   /* 没有任何模板就不显示这个控件 */
    var bar = document.querySelector('.toolbar-left');
    if (!bar) return;
    var wrap = document.createElement('label');
    wrap.className = 'template-pick';
    wrap.title = '载入模板：把某个已归档的设计原样载入当前画布';
    wrap.innerHTML = '<span class="dim">模板</span><select id="templateSel" aria-label="载入模板"></select>';
    bar.appendChild(wrap);
    this.el.templateSel = el('templateSel');
    if (!this.el.templateSel) return;
    this._fillTemplateOptions();
    var self = this;
    this.el.templateSel.addEventListener('change', function () {
      var v = this.value;
      this.value = '';
      if (v) self._loadTemplate(v);
    });
  };

  /* 设计名——工具栏控件由引擎生成；一个画布装多个设计，导出文件名与 JSON 的 page 都用它 */
  Engine.prototype._ensureDesignName = function () {
    if (this.el.designName) return;
    var bar = document.querySelector('.toolbar-left');
    if (!bar) return;
    var wrap = document.createElement('label');
    wrap.className = 'design-name';
    wrap.title = '设计名（导出文件名与 JSON 的 page 字段；导入时自动采用文件里的名字）';
    wrap.innerHTML = '<input type="text" id="designName" spellcheck="false" aria-label="设计名">';
    bar.appendChild(wrap);
    this.el.designName = el('designName');
    if (!this.el.designName) return;
    var self = this;
    this.el.designName.addEventListener('change', function () {
      var v = String(this.value || '').trim().replace(/[\\/:*?"<>|]+/g, '-').replace(/\s+/g, '-');
      self.designName = v || 'layout';
      this.value = self.designName;
      self._persist();
    });
  };

  /* 声明的面板（岛本体）不透明度——工具栏控件由引擎生成，五个画板共用一份实现 */
  Engine.prototype._ensurePanelAlpha = function () {
    if (this.el.panelAlpha) return;
    var box = this.el.sizeBox;
    if (!box || !box.parentNode) return;
    var wrap = document.createElement('label');
    wrap.className = 'panel-alpha';
    wrap.title = '声明的面板不透明度（只影响岛本体，不影响岛内元素）';
    wrap.innerHTML = '<input type="range" id="panelAlpha" min="0" max="1" step="0.05" value="1" aria-label="面板不透明度">'
      + '<b id="panelAlphaVal">100%</b>';
    box.parentNode.insertBefore(wrap, box.nextSibling);
    this.el.panelAlpha = el('panelAlpha');
    this.el.panelAlphaVal = el('panelAlphaVal');
    if (!this.el.panelAlpha) return;
    var self = this;
    this.el.panelAlpha.addEventListener('input', function () {
      self.state.spec.panelOpacity = clampNum(this.value, 0, 1, 1);
      if (self.el.panelAlphaVal) self.el.panelAlphaVal.textContent = Math.round(self.state.spec.panelOpacity * 100) + '%';
      self._renderIsland();
    });
    this.el.panelAlpha.addEventListener('change', function () { self.sync(); });
  };

  Engine.prototype._renderToolbar = function () {
    var st = this.state, bar = this.el.stateBar;
    this._ensureTemplate();
    this._ensureDesignName();
    this._ensurePanelAlpha();
    if (bar) {
      var btns = bar.querySelectorAll('[data-state]');
      for (var i = 0; i < btns.length; i++) {
        btns[i].classList.toggle('is-active', btns[i].getAttribute('data-state') === st.active);
      }
      var sel = bar.querySelector('#stateCustom');
      if (sel) {
        var isCustom = false;
        for (var ci = 0; ci < this.customStates.length; ci++) {
          if (this.customStates[ci].id === st.active) { isCustom = true; break; }
        }
        sel.value = isCustom ? st.active : '__custom__';
      }
    }
    if (this.el.sizeW) this.el.sizeW.value = st.spec.width;
    if (this.el.sizeH) this.el.sizeH.value = st.spec.height;
    if (this.el.panelAlpha) {
      var pa = (st.spec.panelOpacity == null ? 1 : st.spec.panelOpacity);
      this.el.panelAlpha.value = pa;
      if (this.el.panelAlphaVal) this.el.panelAlphaVal.textContent = Math.round(pa * 100) + '%';
    }
    /* 只在没在编辑时回写，避免打断正在输入的设计名 */
    if (this.el.designName && document.activeElement !== this.el.designName) this.el.designName.value = this.designName;
    if (this.el.gridToggle) this.el.gridToggle.checked = st.gridSnap;
    if (this.el.gridSizeInput && document.activeElement !== this.el.gridSizeInput) this.el.gridSizeInput.value = st.gridSize || DEFAULT_GRID;
    if (this.el.guidesToggle) this.el.guidesToggle.checked = st.guidesOn;
    if (this.el.previewToggle) this.el.previewToggle.checked = st.preview;
    if (this.el.notesToggle) this.el.notesToggle.checked = st.showNotes;
  };

  /* ---------- 属性面板 ---------- */
  Engine.prototype._renderPanel = function () {
    var host = this.el.props;
    if (!host) return;
    var c = this.state.selected ? this._byId(this.state.selected) : null;
    if (!c) {
      host.innerHTML = '<div class="panel-empty">选中岛上的元素<br>即可编辑属性<br><span class="dim">从左侧拖组件到岛上添加</span></div>';
      return;
    }
    var def = this.defs[c.type];
    var op = (c.opacity == null ? 1 : c.opacity);
    var h = [];
    h.push('<div class="panel-head"><span class="panel-type">' + def.name + '</span><span class="panel-id">' + c.id + '</span></div>');
    h.push('<div class="panel-grid">'
      + fieldNum('pX', 'X', c.x) + fieldNum('pY', 'Y', c.y)
      + fieldNum('pW', '宽', c.w) + fieldNum('pH', '高', c.h)
      + '</div>');
    h.push('<label class="panel-row"><span>不透明度</span><input type="range" id="pOpacity" min="0" max="1" step="0.05" value="' + op + '"><b id="pOpacityVal">' + Math.round(op * 100) + '%</b></label>');

    if (c.type === 'text') h.push(fieldText('pText', '文本内容', c.props.text || ''));
    if (c.type === 'note') h.push(fieldArea('pNoteText', '注释内容（可换行）', c.props.text || ''));
    if (c.type === 'title') {
      h.push(fieldText('pTitle', '标题', c.props.title || ''));
      h.push(fieldText('pArtist', '艺术家', c.props.artist || ''));
    }
    if (c.type === 'lyrics') {
      h.push(fieldArea('pLines', '歌词（每行一句，第 2 行高亮；留空则不显示）', (c.props.lines || []).join('\n')));
      h.push('<label class="panel-row"><span>对齐</span><select id="pAlign"><option value="center"' + (c.props.align === 'left' ? '' : ' selected') + '>居中</option><option value="left"' + (c.props.align === 'left' ? ' selected' : '') + '>靠左</option></select></label>');
    }
    /* 凡是含文字的组件都要能调字号：默认值按类型给（保持既有观感），上限也按类型收
       （FS_PRESET 定义在模块作用域，_bindPanel 也要读同一份） */
    var fsPreset = FS_PRESET[c.type];
    if (fsPreset) {
      h.push('<label class="panel-row"><span>字号</span><input type="number" id="pFontSize" min="8" max="' + fsPreset[1] + '" step="1" value="' + clampInt(c.props.fontSize, 8, fsPreset[1], fsPreset[0]) + '"></label>');
    }
    if (c.type === 'progress' || c.type === 'ring') {
      var pv = clampNum(c.props.progress, 0, 1, 0.45);
      h.push('<label class="panel-row"><span>进度</span><input type="range" id="pProgress" min="0" max="1" step="0.01" value="' + pv + '"><b id="pProgressVal">' + Math.round(pv * 100) + '%</b></label>');
    }
    if (c.type === 'progress') {
      h.push('<label class="panel-check"><input type="checkbox" id="pShowTime"' + (c.props.showTime ? ' checked' : '') + '><span>显示时间（高度自动 ≥ 26）</span></label>');
      h.push('<label class="panel-row"><span>警示阈值</span><input type="number" id="pWarnAt" min="0" max="100" step="5" value="' + Math.round(clampNum(c.props.warnAt, 0, 1, 1) * 100) + '"><b>%</b></label>');
    }
    if (c.type === 'ring') {
      h.push('<label class="panel-check"><input type="checkbox" id="pCenter"' + (c.props.center !== false ? ' checked' : '') + '><span>环内显示按钮</span></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pCenterPlaying"' + (c.props.centerPlaying ? ' checked' : '') + '><span>环内按钮为暂停态</span></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pDisc"' + (c.props.disc ? ' checked' : '') + '><span>暗盘底（电池环同款）</span></label>');
    }
    if (c.type === 'play') {
      h.push('<label class="panel-check"><input type="checkbox" id="pPlaying"' + (c.props.playing ? ' checked' : '') + '><span>暂停态（显示暂停图标）</span></label>');
    }
    if (c.type === 'expand') {
      h.push('<label class="panel-check"><input type="checkbox" id="pExpanded"' + (c.props.expanded ? ' checked' : '') + '><span>收起态（箭头朝上）</span></label>');
    }
    if (c.type === 'spectrum') {
      h.push('<label class="panel-row"><span>柱数</span><input type="number" id="pBarCount" min="1" max="64" step="1" value="' + clampInt(c.props.barCount, 1, 64, 14) + '"></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pMirrored"' + (c.props.mirrored ? ' checked' : '') + '><span>上下轴对称</span></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pSmooth"' + (c.props.smooth ? ' checked' : '') + '><span>平滑过渡（填充面）</span></label>');
    }
    /* 图标类组件：输入框 + datalist —— 可以直接敲名字，也会跟着输入弹出候选（候选按 a–z 排序） */
    if (c.type === 'icon' || c.type === 'stat' || c.type === 'gauge' || c.type === 'button') {
      h.push('<label class="panel-row"><span>图标</span>'
        + '<input type="text" id="pIconName" list="iconNameList" value="' + Comp.util.esc(c.props.name || '') + '"'
        + ' placeholder="输入或选择" autocomplete="off" spellcheck="false">'
        + '<datalist id="iconNameList">' + iconListOptions() + '</datalist></label>');
    }
    if (c.type === 'icon') {
      h.push('<label class="panel-row"><span>图标占比</span><input type="range" id="pIconRatio" min="0.3" max="1" step="0.02" value="' + clampNum(c.props.ratio, 0.3, 1, 0.72) + '"></label>');
    }
    if (c.type === 'stat') {
      h.push(fieldText('pStatLabel', '标签', c.props.label || ''));
      h.push(fieldText('pStatValue', '数值', c.props.value || ''));
      h.push(fieldText('pStatUnit', '单位', c.props.unit || ''));
      h.push('<label class="panel-check"><input type="checkbox" id="pStatNoLabel"' + (c.props.showLabel === false ? ' checked' : '') + '><span>隐藏标签</span></label>');
    }
    if (c.type === 'gauge') {
      h.push(fieldText('pGaugeValue', '数值', c.props.value || ''));
      h.push(fieldText('pGaugeUnit', '单位', c.props.unit || ''));
      h.push('<label class="panel-row"><span>进度</span><input type="range" id="pGaugeProgress" min="0" max="1" step="0.01" value="' + clampNum(c.props.progress, 0, 1, 0.6) + '"></label>');
    }
    if (c.type === 'button') {
      h.push('<label class="panel-row"><span>形状</span><select id="pBtnShape"><option value="circle"' + (c.props.shape === 'square' ? '' : ' selected') + '>圆形</option><option value="square"' + (c.props.shape === 'square' ? ' selected' : '') + '>方形</option></select></label>');
      h.push('<label class="panel-row"><span>样式</span><select id="pBtnVariant"><option value="solid"' + (c.props.variant === 'outline' ? '' : ' selected') + '>实心</option><option value="outline"' + (c.props.variant === 'outline' ? ' selected' : '') + '>描边</option></select></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pBtnToggled"' + (c.props.toggled ? ' checked' : '') + '><span>激活态（强调色填充 + 深色字形）</span></label>');
      h.push('<label class="panel-row"><span>图标占比</span><input type="range" id="pBtnRatio" min="0.3" max="0.95" step="0.02" value="' + clampNum(c.props.ratio, 0.3, 0.95, 0.62) + '"></label>');
    }
    if (c.type === 'badge') {
      h.push(fieldText('pBadgeText', '文字', c.props.text || ''));
      /* 字号统一走上方的「字号可调」块，这里不再单独出一份 */
    }
    if (c.type === 'card') {
      h.push('<label class="panel-row"><span>圆角</span><input type="number" id="pCardRadius" min="0" max="40" step="1" value="' + clampInt(c.props.radius, 0, 40, 12) + '"></label>');
    }
    if (c.type === 'spark') {
      h.push('<label class="panel-row"><span>点数</span><input type="number" id="pSparkPoints" min="2" max="120" step="1" value="' + clampInt(c.props.points, 2, 120, 28) + '"></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pSparkLine"' + (c.props.mode === 'line' ? ' checked' : '') + '><span>折线（不填充）</span></label>');
      h.push('<label class="panel-check"><input type="checkbox" id="pSparkMirror"' + (c.props.mirrored ? ' checked' : '') + '><span>上下轴对称</span></label>');
    }
    /* 空状态面板：状态名 / 是否显示 / 圆角（0 = 跟随高度；W/H 与不透明度用面板上方通用字段） */
    /* ---- 表单 / 内容类组件（Win32·WinUI 对照新增）的属性编辑 ---- */
    var M_TEXT = {
      input: '占位文本', select: '当前项', checkbox: '文字', radio: '文字', chip: '文字',
      avatar: '文字（无图标时取首字）', keyValue: '标签', row: '主文本', paragraph: '正文（自动换行 + 行数截断）',
      banner: '文字', link: '链接文字', segmented: '各段（用 / 分隔）', stepper: '数值'
    };
    if (M_TEXT[c.type]) {
      h.push(c.type === 'paragraph' ? fieldArea('pMText', M_TEXT[c.type], c.props.text || '') : fieldText('pMText', M_TEXT[c.type], c.props.text || ''));
    }
    if (c.type === 'row') {
      h.push(fieldText('pMSub', '副文本', c.props.sub || ''));
      h.push(fieldText('pMTrail', '右侧尾随文本', c.props.trailing || ''));
    }
    if (c.type === 'keyValue') h.push(fieldText('pMValue', '值', c.props.value || ''));
    if (c.type === 'row' || c.type === 'avatar') {
      /* 同样给候选：可以直接敲名字，也能从候选里挑；留空仍表示"用首字 / 默认" */
      h.push('<label class="panel-field"><span>图标名（avatar 留空则用首字）</span>'
        + '<input type="text" id="pMIcon" list="iconNameList" value="' + Comp.util.esc(c.props.icon || '') + '"'
        + ' autocomplete="off" spellcheck="false">'
        + '<datalist id="iconNameList">' + iconListOptions() + '</datalist></label>');
    }
    if (c.type === 'toggle') h.push('<label class="panel-check"><input type="checkbox" id="pMOn"' + (c.props.on !== false ? ' checked' : '') + '><span>打开</span></label>');
    if (c.type === 'checkbox' || c.type === 'radio') h.push('<label class="panel-check"><input type="checkbox" id="pMOn"' + (c.props.checked !== false ? ' checked' : '') + '><span>选中</span></label>');
    if (c.type === 'slider' || c.type === 'scrollbar') {
      var mpid = (c.type === 'slider') ? 'pMProgress' : 'pMThumb';
      var mplab = (c.type === 'slider') ? '进度' : '滑块占比';
      var mpv = (c.type === 'slider') ? clampNum(c.props.progress, 0, 1, 0.6) : clampNum(c.props.thumb, 0, 1, 0.35);
      h.push('<label class="panel-row"><span>' + mplab + '</span><input type="range" id="' + mpid + '" min="0" max="1" step="0.05" value="' + mpv + '"></label>');
    }
    if (c.type === 'scrollbar') h.push('<label class="panel-check"><input type="checkbox" id="pMHoriz"' + (c.props.horizontal ? ' checked' : '') + '><span>横向</span></label>');
    if (c.type === 'pager' || c.type === 'segmented') {
      h.push(fieldNum('pMCount', c.type === 'pager' ? '总页数' : '段数', c.props.count != null ? c.props.count : (c.type === 'pager' ? 5 : 3)));
      h.push(fieldNum('pMActive', '当前（从 0 起）', c.props.active != null ? c.props.active : 0));
    }
    if (c.type === 'paragraph') h.push(fieldNum('pMLines', '最多行数', c.props.lines != null ? c.props.lines : 3));
    if (c.type === 'dot' || c.type === 'banner') {
      h.push(fieldText('pMTone', c.type === 'dot' ? '色调（accent / danger / muted）' : '色调（info / success / warning / error）', c.props.tone || ''));
    }

    if (c.type === 'panelUnfold' || c.type === 'panelWait' || c.type === 'panelContract') {
      h.push(fieldText('pPanelLabel', '状态名（开启显示时贴在左侧）', c.props.label || ''));
      h.push('<label class="panel-check"><input type="checkbox" id="pPanelShowLabel"' + (c.props.showLabel ? ' checked' : '') + '><span>显示状态名</span></label>');
      h.push('<label class="panel-row"><span>圆角（0 = 跟随高度）</span><input type="number" id="pPanelRadius" min="0" max="120" step="1" value="' + clampInt(c.props.radius, 0, 120, 0) + '"></label>');
    }
    if (Comp.usesAccent(c.type)) {
      /* 进度条上的这个色块含义不同：它是「超过警示阈值后」用的颜色，即警告色本身 */
      var accentLabel = (c.type === 'progress') ? '警示色' : '强调色';
      h.push('<label class="panel-row"><span>' + accentLabel + '</span><input type="color" id="pAccent" value="' + (c.props.accent || '#C6CA4C') + '"></label>');
    }

    h.push('<div class="panel-section"><div class="panel-section-title">层级 / 操作</div>');
    h.push('<div class="panel-actions">'
      + '<button type="button" class="btn" id="zTop">置顶</button>'
      + '<button type="button" class="btn" id="zUp">上移</button>'
      + '<button type="button" class="btn" id="zDown">下移</button>'
      + '<button type="button" class="btn" id="zBottom">置底</button>'
      + '</div>');
    h.push('<div class="panel-actions" style="margin-top:8px;grid-template-columns:1fr"><button type="button" class="btn btn-danger" id="btnDelete">删除该元素</button></div>');
    h.push('</div>');

    host.innerHTML = h.join('');
    this._bindPanel(c);
  };

  Engine.prototype._bindPanel = function (c) {
    var self = this, host = this.el.props;
    function $id(id) { return host.querySelector('#' + id); }
    function on(id, ev, fn) { var n = $id(id); if (n) n.addEventListener(ev, fn); }
    function commit() { self._pushHistory(); self._persist(); }
    function geom() { self._applyItemStyle(c); self._syncPanelNumbers(c); self._updateContent(c); commit(); }
    function prop() { self._updateContent(c); commit(); }

    on('pX', 'change', function () { c.x = intOr(this.value, c.x); geom(); });
    on('pY', 'change', function () { c.y = intOr(this.value, c.y); geom(); });
    on('pW', 'change', function () { c.w = Math.max(MIN, intOr(this.value, c.w)); geom(); });
    on('pH', 'change', function () { c.h = Math.max(MIN, intOr(this.value, c.h)); geom(); });

    on('pOpacity', 'input', function () {
      c.opacity = clampNum(this.value, 0, 1, 1);
      var b = $id('pOpacityVal'); if (b) b.textContent = Math.round(c.opacity * 100) + '%';
      self._applyItemStyle(c); self._persist();
    });
    on('pOpacity', 'change', commit);

    /* 表单 / 内容类组件 */
    on('pMText', 'input', function () { c.props.text = this.value; self._updateContent(c); });
    on('pMText', 'change', commit);
    on('pMSub', 'input', function () { c.props.sub = this.value; self._updateContent(c); });
    on('pMSub', 'change', commit);
    on('pMTrail', 'input', function () { c.props.trailing = this.value; self._updateContent(c); });
    on('pMTrail', 'change', commit);
    on('pMValue', 'input', function () { c.props.value = this.value; self._updateContent(c); });
    on('pMValue', 'change', commit);
    on('pMIcon', 'input', function () { c.props.icon = this.value; self._updateContent(c); });
    on('pMIcon', 'change', commit);
    on('pMOn', 'change', function () {
      c.props.on = this.checked; c.props.checked = this.checked;
      self._updateContent(c); commit();
    });
    on('pMProgress', 'input', function () { c.props.progress = clampNum(this.value, 0, 1, 0.6); self._updateContent(c); });
    on('pMProgress', 'change', commit);
    on('pMThumb', 'input', function () { c.props.thumb = clampNum(this.value, 0.08, 1, 0.35); self._updateContent(c); });
    on('pMThumb', 'change', commit);
    on('pMHoriz', 'change', function () { c.props.horizontal = this.checked; self._updateContent(c); commit(); });
    on('pMCount', 'change', function () { c.props.count = clampInt(this.value, 1, 64, 5); self._updateContent(c); commit(); });
    on('pMActive', 'change', function () { c.props.active = clampInt(this.value, 0, 63, 0); self._updateContent(c); commit(); });
    on('pMLines', 'change', function () { c.props.lines = clampInt(this.value, 1, 12, 3); self._updateContent(c); commit(); });
    on('pMTone', 'change', function () { c.props.tone = this.value; self._updateContent(c); commit(); });

    on('pText', 'input', function () { c.props.text = this.value; self._updateContent(c); });
    on('pText', 'change', commit);
    on('pNoteText', 'input', function () { c.props.text = this.value; self._updateContent(c); });
    on('pNoteText', 'change', commit);
    on('pTitle', 'input', function () { c.props.title = this.value; self._updateContent(c); });
    on('pArtist', 'input', function () { c.props.artist = this.value; self._updateContent(c); });
    on('pTitle', 'change', commit);
    on('pArtist', 'change', commit);
    on('pLines', 'input', function () { c.props.lines = this.value.split('\n').slice(0, 3); self._updateContent(c); });
    on('pLines', 'change', commit);
    on('pAlign', 'change', function () { c.props.align = this.value; prop(); });
    on('pFontSize', 'change', function () {
      var mx = (FS_PRESET[c.type] || [14, 48])[1];
      c.props.fontSize = clampInt(this.value, 8, mx, 14);
      this.value = c.props.fontSize;
      prop();
    });

    on('pProgress', 'input', function () {
      c.props.progress = clampNum(this.value, 0, 1, 0);
      var b = $id('pProgressVal'); if (b) b.textContent = Math.round(c.props.progress * 100) + '%';
      self._updateContent(c); self._persist();
    });
    on('pProgress', 'change', commit);

    on('pShowTime', 'change', function () {
      c.props.showTime = this.checked;
      if (this.checked && c.h < 26) { c.h = 26; self._applyItemStyle(c); }
      self._updateContent(c);
      self._renderPanel();
      commit();
    });
    on('pWarnAt', 'change', function () {
      c.props.warnAt = clampInt(this.value, 0, 100, 100) / 100;
      this.value = Math.round(c.props.warnAt * 100);
      self._updateContent(c);
      commit();
    });
    on('pCenter', 'change', function () { c.props.center = this.checked; prop(); });
    on('pCenterPlaying', 'change', function () { c.props.centerPlaying = this.checked; prop(); });
    on('pDisc', 'change', function () { c.props.disc = this.checked; prop(); });
    on('pPlaying', 'change', function () { c.props.playing = this.checked; prop(); });
    on('pExpanded', 'change', function () { c.props.expanded = this.checked; prop(); });

    on('pBarCount', 'change', function () {
      c.props.barCount = clampInt(this.value, 1, 64, 14);
      c.props.bars = null;   /* 重新按柱数生成 */
      prop();
    });
    on('pMirrored', 'change', function () { c.props.mirrored = this.checked; prop(); });
    on('pSmooth', 'change', function () { c.props.smooth = this.checked; prop(); });

    /* 图标名：半截名字不生效（否则会一闪一闪地回退成 bolt）；失焦 / 回车时若名字无效则退回原值 */
    on('pIconName', 'input', function () {
      if (!isIconName(this.value)) return;
      c.props.name = this.value;
      self._updateContent(c);
    });
    on('pIconName', 'change', function () {
      if (!isIconName(this.value)) { this.value = c.props.name || ''; return; }
      c.props.name = this.value;
      prop();
    });
    on('pIconRatio', 'input', function () { c.props.ratio = clampNum(this.value, 0.3, 1, 0.72); self._updateContent(c); });
    on('pIconRatio', 'change', commit);
    on('pStatLabel', 'input', function () { c.props.label = this.value; self._updateContent(c); });
    on('pStatValue', 'input', function () { c.props.value = this.value; self._updateContent(c); });
    on('pStatUnit', 'input', function () { c.props.unit = this.value; self._updateContent(c); });
    on('pStatLabel', 'change', commit);
    on('pStatValue', 'change', commit);
    on('pStatUnit', 'change', commit);
    on('pStatNoLabel', 'change', function () { c.props.showLabel = !this.checked; prop(); });
    on('pGaugeValue', 'input', function () { c.props.value = this.value; self._updateContent(c); });
    on('pGaugeUnit', 'input', function () { c.props.unit = this.value; self._updateContent(c); });
    on('pGaugeValue', 'change', commit);
    on('pGaugeUnit', 'change', commit);
    on('pGaugeProgress', 'input', function () { c.props.progress = clampNum(this.value, 0, 1, 0.6); self._updateContent(c); });
    on('pGaugeProgress', 'change', commit);
    on('pBtnShape', 'change', function () { c.props.shape = this.value; prop(); });
    on('pBtnVariant', 'change', function () { c.props.variant = this.value; prop(); });
    on('pBtnToggled', 'change', function () { c.props.toggled = this.checked; prop(); });
    on('pBtnRatio', 'input', function () { c.props.ratio = clampNum(this.value, 0.3, 0.95, 0.62); self._updateContent(c); });
    on('pBtnRatio', 'change', commit);
    on('pBadgeText', 'input', function () { c.props.text = this.value; self._updateContent(c); });
    on('pBadgeText', 'change', commit);
    /* pBadgeFont 已废弃：字号统一走 pFontSize */
    on('pCardRadius', 'change', function () { c.props.radius = clampInt(this.value, 0, 40, 12); prop(); });
    on('pSparkPoints', 'change', function () { c.props.points = clampInt(this.value, 2, 120, 28); c.props.values = null; prop(); });
    on('pSparkLine', 'change', function () { c.props.mode = this.checked ? 'line' : 'area'; prop(); });
    on('pSparkMirror', 'change', function () { c.props.mirrored = this.checked; prop(); });

    on('pPanelLabel', 'input', function () { c.props.label = this.value; self._updateContent(c); });
    on('pPanelLabel', 'change', commit);
    on('pPanelShowLabel', 'change', function () { c.props.showLabel = this.checked; prop(); });
    on('pPanelRadius', 'change', function () { c.props.radius = clampInt(this.value, 0, 120, 0); prop(); });

    on('pAccent', 'input', function () { c.props.accent = this.value; self._updateContent(c); });
    on('pAccent', 'change', commit);
    /* pIconFilter 已废弃：改用 datalist 的原生过滤，不需要手写过滤框 */

    on('zTop', 'click', function () { self._zOrder(c, 'top'); });
    on('zUp', 'click', function () { self._zOrder(c, 'up'); });
    on('zDown', 'click', function () { self._zOrder(c, 'down'); });
    on('zBottom', 'click', function () { self._zOrder(c, 'bottom'); });
    on('btnDelete', 'click', function () { self._remove(c.id); });
  };

  Engine.prototype._syncPanelNumbers = function (c) {
    var host = this.el.props;
    if (!host) return;
    var map = { pX: c.x, pY: c.y, pW: c.w, pH: c.h };
    for (var id in map) {
      if (!Object.prototype.hasOwnProperty.call(map, id)) continue;
      var n = host.querySelector('#' + id);
      if (n && document.activeElement !== n) n.value = map[id];
    }
  };

  /* ---------- 选中 / 查询 ---------- */
  Engine.prototype._select = function (id) {
    this.state.selected = id;
    var nodes = this.el.items.querySelectorAll('.ic-item');
    for (var i = 0; i < nodes.length; i++) {
      nodes[i].classList.toggle('selected', nodes[i].getAttribute('data-id') === id);
    }
    this._renderPanel();
  };

  Engine.prototype._byId = function (id) {
    for (var i = 0; i < this.state.comps.length; i++) if (this.state.comps[i].id === id) return this.state.comps[i];
    return null;
  };

  /* ---------- 调色板 ---------- */
  Engine.prototype._buildPalette = function () {
    var self = this, host = this.el.palette;
    if (!host) return;
    host.innerHTML = '';
    Comp.groups.forEach(function (grp) {
      var sec = document.createElement('div');
      sec.className = 'palette-group';
      var title = document.createElement('div');
      title.className = 'palette-group-title';
      title.textContent = grp.name;
      sec.appendChild(title);
      var grid = document.createElement('div');
      grid.className = 'palette-grid';
      grp.items.forEach(function (type) {
        var def = self.defs[type];
        if (!def) return;
        var item = document.createElement('button');
        item.type = 'button';
        item.className = 'palette-item';
        item.setAttribute('data-type', type);
        item.setAttribute('title', '拖到岛上摆位，或按 Enter 加到中心');
        var prev = document.createElement('div');
        prev.className = 'palette-prev';
        prev.innerHTML = def.palette();
        var nm = document.createElement('div');
        nm.className = 'palette-name';
        nm.textContent = def.name;
        item.appendChild(prev);
        item.appendChild(nm);
        item.addEventListener('pointerdown', function (e) { self._paletteDrag(e, type); });
        item.addEventListener('keydown', function (e) {
          if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); self._addAtCenter(type); }
        });
        grid.appendChild(item);
      });
      sec.appendChild(grid);
      host.appendChild(sec);
    });
  };

  Engine.prototype._paletteDrag = function (e, type) {
    if (this.state.preview) return;
    if (e.button != null && e.button !== 0) return;
    var def = this.defs[type];
    if (!def) return;
    e.preventDefault();
    var self = this;
    var ghost = document.createElement('div');
    ghost.className = 'ic-ghost';
    var z = this.state.zoom || 1;
    ghost.style.width = (def.w * z) + 'px';
    ghost.style.height = (def.h * z) + 'px';
    var inner = document.createElement('div');
    inner.style.width = '100%';
    inner.style.height = '100%';
    inner.innerHTML = def.render({ id: 'ghost', type: type, w: def.w, h: def.h, props: clone(def.props || {}) });
    ghost.appendChild(inner);
    document.body.appendChild(ghost);
    this.ghost = ghost;

    function place(ev) {
      ghost.style.left = ev.clientX + 'px';
      ghost.style.top = ev.clientY + 'px';
      self.el.workbench.classList.toggle('drop-target', self._inArea(ev.clientX, ev.clientY));
    }
    function up(ev) {
      document.removeEventListener('pointermove', place);
      document.removeEventListener('pointerup', up);
      if (ghost.parentNode) ghost.parentNode.removeChild(ghost);
      self.ghost = null;
      self.el.workbench.classList.remove('drop-target');
      /* 只要落点在画布（workbench）内就生效——坐标换算仍以岛为原点，
         所以元素可以摆在岛外（负坐标 / 超出岛宽高都允许），注释就是这么放的。 */
      if (self._inArea(ev.clientX, ev.clientY)) {
        var p = self._toIsland(ev.clientX, ev.clientY);
        self.addComponent(type, p.x - def.w / 2, p.y - def.h / 2);
      }
    }
    place(e);
    document.addEventListener('pointermove', place);
    document.addEventListener('pointerup', up);
  };

  Engine.prototype._inIsland = function (cx, cy) {
    var r = this.el.island.getBoundingClientRect();
    return cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom;
  };
  /* 投放有效区 = 整个画布（workbench），而不是仅岛矩形：
     素材库里的东西应当能摆在画布任意位置（岛外、岛上方、注释区都行）。 */
  Engine.prototype._inArea = function (cx, cy) {
    var host = this.el.workbench || this.el.island;
    var r = host.getBoundingClientRect();
    return cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom;
  };
  Engine.prototype._toIsland = function (cx, cy) {
    var r = this.el.island.getBoundingClientRect();
    var z = this.state.zoom || 1;
    return { x: (cx - r.left) / z, y: (cy - r.top) / z };
  };

  /* ---------- 交互：岛内拖拽 / 缩放 ---------- */
  Engine.prototype._bindStage = function () {
    var self = this;
    this.el.items.addEventListener('pointerdown', function (e) {
      if (self.state.preview) return;
      var handle = e.target.closest ? e.target.closest('.ic-handle') : null;
      var node = e.target.closest ? e.target.closest('.ic-item') : null;
      if (!node) return;
      var c = self._byId(node.getAttribute('data-id'));
      if (!c) return;
      self._select(c.id);
      try { node.focus({ preventScroll: true }); } catch (err) { }
      e.preventDefault();
      self._startDrag(e, c, handle ? 'resize' : 'move', handle ? handle.getAttribute('data-h') : null);
    });
    this.el.workbench.addEventListener('pointerdown', function (e) {
      if (e.target.closest && e.target.closest('.ic-item')) return;
      self._select(null);
    });
  };

  Engine.prototype._startDrag = function (e, c, mode, dir) {
    var self = this;
    var d = {
      mode: mode, dir: dir,
      sx: e.clientX, sy: e.clientY,
      orig: { x: c.x, y: c.y, w: c.w, h: c.h },
      moved: false
    };
    this.drag = d;

    function move(ev) {
      var z = self.state.zoom || 1;
      var dx = (ev.clientX - d.sx) / z, dy = (ev.clientY - d.sy) / z;
      if (Math.abs(dx) > 1 || Math.abs(dy) > 1) d.moved = true;
      var rect = (d.mode === 'move')
        ? { x: d.orig.x + dx, y: d.orig.y + dy, w: d.orig.w, h: d.orig.h }
        : resizeRect(d.orig, d.dir, dx, dy);
      rect = self._snapAndGuide(rect, c.id);
      c.x = rect.x; c.y = rect.y; c.w = rect.w; c.h = rect.h;
      self._applyItemStyle(c);
      self._updateContent(c);
      self._syncPanelNumbers(c);
    }
    function up() {
      document.removeEventListener('pointermove', move);
      document.removeEventListener('pointerup', up);
      self._clearGuides();
      self.drag = null;
      if (d.moved) self._pushHistory();
      self._persist();
    }
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
  };

  Engine.prototype._snapAndGuide = function (rect, excludeId) {
    var r = {
      x: Math.round(rect.x), y: Math.round(rect.y),
      w: Math.max(MIN, Math.round(rect.w)), h: Math.max(MIN, Math.round(rect.h))
    };
    if (this.state.gridSnap) {
      var gs = this.state.gridSize || DEFAULT_GRID;
      r.x = roundTo(r.x, gs);
      r.y = roundTo(r.y, gs);
      r.w = Math.max(MIN, roundTo(r.w, gs));
      r.h = Math.max(MIN, roundTo(r.h, gs));
    }
    var guides = [];
    if (this.state.guidesOn) {
      var gx = this._align(r.x, r.w, 'x', excludeId);
      var gy = this._align(r.y, r.h, 'y', excludeId);
      r.x = gx.pos; r.y = gy.pos;
      if (gx.line != null) guides.push({ axis: 'v', pos: gx.line });
      if (gy.line != null) guides.push({ axis: 'h', pos: gy.line });
    }
    this._drawGuides(guides);
    return r;
  };

  Engine.prototype._lines = function (axis, excludeId) {
    var out = [], W = this.state.spec.width, H = this.state.spec.height;
    if (axis === 'x') { out.push(0, W / 2, W); } else { out.push(0, H / 2, H); }
    for (var i = 0; i < this.state.comps.length; i++) {
      var c = this.state.comps[i];
      if (c.id === excludeId) continue;
      if (axis === 'x') { out.push(c.x, c.x + c.w / 2, c.x + c.w); }
      else { out.push(c.y, c.y + c.h / 2, c.y + c.h); }
    }
    return out;
  };

  Engine.prototype._align = function (pos, size, axis, excludeId) {
    var lines = this._lines(axis, excludeId);
    var edges = [{ v: pos, off: 0 }, { v: pos + size / 2, off: size / 2 }, { v: pos + size, off: size }];
    var best = null;
    for (var i = 0; i < edges.length; i++) {
      for (var j = 0; j < lines.length; j++) {
        var d = Math.abs(edges[i].v - lines[j]);
        if (d <= GUIDE_T && (!best || d < best.d)) best = { d: d, line: lines[j], off: edges[i].off };
      }
    }
    if (best) return { pos: Math.round(best.line - best.off), line: Math.round(best.line) };
    return { pos: pos, line: null };
  };

  Engine.prototype._drawGuides = function (list) {
    var layer = this.el.guides;
    if (!layer) return;
    layer.innerHTML = '';
    for (var i = 0; i < list.length; i++) {
      var g = list[i], n = document.createElement('div');
      n.className = 'ic-guide ' + (g.axis === 'v' ? 'v' : 'h');
      if (g.axis === 'v') n.style.left = g.pos + 'px'; else n.style.top = g.pos + 'px';
      layer.appendChild(n);
    }
  };
  Engine.prototype._clearGuides = function () {
    if (this.el.guides) this.el.guides.innerHTML = '';
  };

  /* ---------- 增删 / 层级 ---------- */
  Engine.prototype._maxZ = function () {
    var m = 0;
    for (var i = 0; i < this.state.comps.length; i++) m = Math.max(m, this.state.comps[i].z || 0);
    return m;
  };

  Engine.prototype.addComponent = function (type, x, y, props) {
    var def = this.defs[type];
    if (!def) return null;
    x = intOr(x, 0); y = intOr(y, 0);
    if (this.state.gridSnap) { var gs2 = this.state.gridSize || DEFAULT_GRID; x = roundTo(x, gs2); y = roundTo(y, gs2); }
    var c = {
      id: 'c' + (++this._seq) + Date.now().toString(36).slice(-4),
      type: type, x: x, y: y,
      w: def.w, h: def.h,
      z: this._maxZ() + 1, opacity: 1,
      props: props ? clone(props) : clone(def.props || {})
    };
    this.state.comps.push(c);
    this.state.selected = c.id;
    this._pushHistory();
    this._renderItems();
    this._renderPanel();
    this._persist();
    return c;
  };

  Engine.prototype._addAtCenter = function (type) {
    var def = this.defs[type];
    if (!def) return;
    this.addComponent(type, Math.round((this.state.spec.width - def.w) / 2), Math.round((this.state.spec.height - def.h) / 2));
  };

  Engine.prototype._remove = function (id) {
    var idx = -1;
    for (var i = 0; i < this.state.comps.length; i++) {
      if (this.state.comps[i].id === id) { idx = i; break; }
    }
    if (idx < 0) return;
    this.state.comps.splice(idx, 1);
    if (this.state.selected === id) this.state.selected = null;
    this._pushHistory();
    this._renderItems();
    this._renderPanel();
    this._persist();
  };

  Engine.prototype._zOrder = function (c, dir) {
    var arr = this.state.comps.slice().sort(function (a, b) { return (a.z || 0) - (b.z || 0); });
    var i = arr.indexOf(c);
    if (i < 0) return;
    if (dir === 'top') { arr.splice(i, 1); arr.push(c); }
    else if (dir === 'bottom') { arr.splice(i, 1); arr.unshift(c); }
    else if (dir === 'up') { if (i >= arr.length - 1) return; arr.splice(i, 1); arr.splice(i + 1, 0, c); }
    else if (dir === 'down') { if (i <= 0) return; arr.splice(i, 1); arr.splice(i - 1, 0, c); }
    else return;
    for (var k = 0; k < arr.length; k++) arr[k].z = k + 1;
    this._pushHistory();
    this._renderItems();
    this._renderPanel();
    this._persist();
  };

  /* ---------- 撤销 / 重做 ---------- */
  Engine.prototype._snapshot = function () {
    this._capture();
    return JSON.stringify({
      ver: 3, page: this.designName,
      /* 视图偏好（网格密度 / 吸附 / 参考线 / 注释）随设计一起存，但**不进导出稿**——导出只写 active/states/layouts */
      view: {
        gridSnap: !!this.state.gridSnap, gridSize: this.state.gridSize || DEFAULT_GRID,
        guidesOn: !!this.state.guidesOn, showNotes: !!this.state.showNotes
      },
      active: this.state.active, states: this.customStates, layouts: this.layouts
    });
  };
  Engine.prototype._pushHistory = function () {
    var snap = this._snapshot();
    if (this.history[this.hIndex] === snap) return;
    this.history = this.history.slice(0, this.hIndex + 1);
    this.history.push(snap);
    if (this.history.length > 60) this.history.shift();
    this.hIndex = this.history.length - 1;
  };
  Engine.prototype._restore = function (snap) {
    var data = null;
    try { data = JSON.parse(snap); } catch (e) { return; }
    this.layouts = {};
    for (var k in (data.layouts || {})) {
      if (!Object.prototype.hasOwnProperty.call(data.layouts, k)) continue;
      if (this._stateDef(k)) this.layouts[k] = this._sanitize(data.layouts[k]);
    }
    var act = (typeof data.active === 'string' && this._stateDef(data.active)) ? data.active : this._defaultStateId();
    this._applyLayout(act);
    this._renderIsland();
    this._renderItems();
    this._renderToolbar();
    this._renderPanel();
    this._applyNotesVisibility();
    this._persist();
  };
  Engine.prototype._undo = function () {
    if (this.hIndex <= 0) return;
    this.hIndex--;
    this._restore(this.history[this.hIndex]);
  };
  Engine.prototype._redo = function () {
    if (this.hIndex >= this.history.length - 1) return;
    this.hIndex++;
    this._restore(this.history[this.hIndex]);
  };

  /* ---------- 持久化 / 校验 ---------- */
  Engine.prototype._persist = function () {
    var self = this;
    if (this._persistT) clearTimeout(this._persistT);
    this._persistT = setTimeout(function () {
      try { localStorage.setItem(self.storageKey, self._snapshot()); } catch (e) { }
    }, 220);
  };

  Engine.prototype._sanitize = function (data) {
    var self = this;
    var out = { spec: { width: 560, height: 160, panelOpacity: 1 }, components: [] };
    if (data && data.spec) {
      out.spec.width = clampInt(data.spec.width, 120, 2000, 560);
      out.spec.height = clampInt(data.spec.height, 40, 1200, 160);
      out.spec.panelOpacity = clampNum(data.spec.panelOpacity, 0, 1, 1);
    }
    var maxZ = 0;
    var list = (data && data.components) || [];
    for (var i = 0; i < list.length; i++) {
      var raw = list[i];
      if (!raw || typeof raw.type !== 'string') continue;
      var def = self.defs[raw.type];
      if (!def) continue;
      var c = {
        id: (typeof raw.id === 'string' && raw.id) ? raw.id : ('c' + (i + 1)),
        type: raw.type,
        x: intOr(raw.x, 0),
        y: intOr(raw.y, 0),
        w: Math.max(MIN, intOr(raw.w, def.w)),
        h: Math.max(MIN, intOr(raw.h, def.h)),
        z: intOr(raw.z, maxZ + 1),
        opacity: clampNum(raw.opacity, 0, 1, 1),
        props: (raw.props && typeof raw.props === 'object') ? raw.props : {}
      };
      maxZ = Math.max(maxZ, c.z);
      out.components.push(c);
    }
    return out;
  };

  Engine.prototype._load = function () {
    var raw = null;
    try { raw = localStorage.getItem(this.storageKey); } catch (e) { raw = null; }
    var data = null;
    if (raw) { try { data = JSON.parse(raw); } catch (e) { data = null; } }

    /* 先恢复用户新增的状态清单，再解析布局——否则这些状态的布局会被当成未知状态丢掉 */
    if (data && Array.isArray(data.states)) {
      this.customStates = [];
      for (var s = 0; s < data.states.length; s++) {
        var cs = data.states[s];
        if (!cs || typeof cs.id !== 'string' || !cs.id) continue;
        this.customStates.push({
          id: cs.id,
          name: (typeof cs.name === 'string' && cs.name) ? cs.name : cs.id,
          w: clampInt(cs.w, 120, 2000, 560),
          h: clampInt(cs.h, 40, 1200, 160)
        });
      }
      if (!this.customStates.length) this.customStates.push({ id: 'custom', name: '自定义', w: 560, h: 160 });
      this.states = this.builtinStates.concat(this.customStates);
    }

    this.layouts = {};
    if (data && data.layouts && typeof data.layouts === 'object') {
      for (var k in data.layouts) {
        if (!Object.prototype.hasOwnProperty.call(data.layouts, k)) continue;
        if (this._stateDef(k)) this.layouts[k] = this._sanitize(data.layouts[k]);
      }
    } else if (data && Array.isArray(data.components)) {
      var target = this._resolveStateId(data.state) || this._resolveStateId(data.spec && data.spec.preset) || this._defaultStateId();
      this.layouts[target] = this._sanitize(data);
    }
    if (data && typeof data.page === 'string' && data.page) this.designName = data.page;
    if (data && data.view) {
      this.state.gridSnap = data.view.gridSnap !== false;
      this.state.gridSize = clampInt(data.view.gridSize, 1, 100, DEFAULT_GRID);
      this.state.guidesOn = data.view.guidesOn !== false;
      this.state.showNotes = data.view.showNotes !== false;
    }
    var act = (data && typeof data.active === 'string' && this._stateDef(data.active)) ? data.active : this._defaultStateId();
    this._applyLayout(act);
    this._seq = 0;
    this.history = [];
    this.hIndex = -1;
    this._pushHistory();
  };

  /* ---------- 状态栏（内置按钮 + 「自定义 ▾」下拉） ---------- */

  Engine.prototype._buildStateBar = function () {
    var bar = this.el.stateBar;
    if (!bar) return;

    var self = this;
    var html = '';
    for (var i = 0; i < this.builtinStates.length; i++) {
      var s = this.builtinStates[i];
      html += '<button type="button" class="seg-btn" data-state="' + s.id + '">' + Comp.util.esc(s.name) + '</button>';
    }
    html += '<select id="stateCustom" title="自定义状态（可新增）"'
      + ' style="height:30px;padding:0 8px;border-radius:8px;background:#262628;color:#E8E8E8;'
      + 'border:1px solid #2A2A2C;font-size:12px;font-family:inherit;cursor:pointer">';
    html += '<option value="__custom__">自定义 ▾</option>';
    for (var j = 0; j < this.customStates.length; j++) {
      html += '<option value="' + this.customStates[j].id + '">' + Comp.util.esc(this.customStates[j].name) + '</option>';
    }
    html += '<option value="__add__">＋ 添加状态…</option></select>';
    bar.innerHTML = html;

    var sel = bar.querySelector('#stateCustom');
    if (sel) {
      sel.addEventListener('change', function () {
        var v = this.value;
        if (v === '__add__') { self._addCustomState(); return; }
        if (v === '__custom__') return;
        self._switchState(v);
      });
    }
  };

  /* 新增自定义状态：尺寸沿用当前状态，布局留空——从零开始排一个态 */
  Engine.prototype._addCustomState = function () {
    var name = window.prompt('新状态名称', '新状态');
    if (name == null) return;
    name = String(name).trim();
    if (!name) return;

    var id = 'c' + Date.now().toString(36);
    var w = this.state.spec.width;
    var h = this.state.spec.height;
    this.customStates.push({ id: id, name: name, w: w, h: h });
    this.states = this.builtinStates.concat(this.customStates);
    this.layouts[id] = { spec: { width: w, height: h, panelOpacity: this.state.spec.panelOpacity == null ? 1 : this.state.spec.panelOpacity }, components: [] };

    this._buildStateBar();
    this._switchState(id);
    this._pushHistory();
    this._persist();
  };

  /* ---------- 工具栏 / 全局快捷键 ---------- */
  Engine.prototype._bindToolbar = function () {
    var self = this;
    var bar = this.el.stateBar;
    if (bar) {
      /* 事件委托：状态条会被 _buildStateBar() 整体重建（导入 JSON / 新增自定义状态），
         逐个绑 click 会在重建后全部失效——委托给容器才不受影响 */
      bar.addEventListener('click', function (e) {
        var t = e.target;
        var btn = (t && typeof t.closest === 'function') ? t.closest('[data-state]') : null;
        if (!btn) return;
        self._switchState(btn.getAttribute('data-state'));
      });
    }
    if (this.el.sizeW) this.el.sizeW.addEventListener('change', function () { self._setSize(this.value, self.state.spec.height); });
    if (this.el.sizeH) this.el.sizeH.addEventListener('change', function () { self._setSize(self.state.spec.width, this.value); });
    if (this.el.gridToggle) this.el.gridToggle.addEventListener('change', function () {
      self.state.gridSnap = this.checked;
      self._applyGrid();
      self._persist();
    });
    if (this.el.gridSizeInput) this.el.gridSizeInput.addEventListener('change', function () {
      self.state.gridSize = clampInt(this.value, 1, 100, DEFAULT_GRID);
      this.value = self.state.gridSize;
      self._applyGrid();
      self._persist();
    });
    if (this.el.guidesToggle) this.el.guidesToggle.addEventListener('change', function () { self.state.guidesOn = this.checked; if (!this.checked) self._clearGuides(); self._persist(); });
    if (this.el.previewToggle) this.el.previewToggle.addEventListener('change', function () {
      self.state.preview = this.checked;
      self.el.workbench.classList.toggle('preview', this.checked);
      self._clearGuides();
    });
    if (this.el.notesToggle) this.el.notesToggle.addEventListener('change', function () {
      self.state.showNotes = this.checked;
      self._applyNotesVisibility();
      self._persist();
    });
    if (this.el.btnReset) this.el.btnReset.addEventListener('click', function () { self._reset(); });
    var zo = el('btnZoomOut'), zi = el('btnZoomIn'), z1 = el('btnZoom100'), za = el('btnZoomActual');
    if (zo) zo.addEventListener('click', function () { self._setZoom((self.state.zoom || 1) - 0.1); });
    if (zi) zi.addEventListener('click', function () { self._setZoom((self.state.zoom || 1) + 0.1); });
    if (z1) z1.addEventListener('click', function () { self._setZoom(1); });
    if (za) za.addEventListener('click', function () { self._setZoom(0.8); });
    if (this.el.btnExport) this.el.btnExport.addEventListener('click', function () { self._export(false); });
    if (this.el.btnExportAll) this.el.btnExportAll.addEventListener('click', function () { self._export(true); });
    if (this.el.btnImport) this.el.btnImport.addEventListener('click', function () { if (self.el.importInput) self.el.importInput.click(); });
    if (this.el.importInput) this.el.importInput.addEventListener('change', function () { self._import(this.files && this.files[0]); this.value = ''; });
  };

  /* 自定义状态的「声明尺寸」必须跟着画板走，否则导出稿里 states 与 layouts 会长期漂移 */
  Engine.prototype._syncStateDecl = function () {
    var id = this.state.active;
    for (var i = 0; i < this.customStates.length; i++) {
      if (this.customStates[i].id === id) {
        this.customStates[i].w = this.state.spec.width;
        this.customStates[i].h = this.state.spec.height;
        return;
      }
    }
  };

  Engine.prototype._setSize = function (w, h) {
    this.state.spec.width = clampInt(w, 120, 2000, this.state.spec.width);
    this.state.spec.height = clampInt(h, 40, 1200, this.state.spec.height);
    this._syncStateDecl();
    this._renderToolbar();
    this.sync();
  };

  Engine.prototype._bindGlobal = function () {
    var self = this;
    document.addEventListener('keydown', function (e) {
      var tag = (e.target && e.target.tagName ? e.target.tagName : '').toLowerCase();
      var typing = (tag === 'input' || tag === 'textarea' || tag === 'select');
      if ((e.ctrlKey || e.metaKey) && !e.altKey) {
        var k = (e.key || '').toLowerCase();
        if (k === 'z' && !e.shiftKey) { if (!typing) { e.preventDefault(); self._undo(); } return; }
        if ((k === 'z' && e.shiftKey) || k === 'y') { if (!typing) { e.preventDefault(); self._redo(); } return; }
      }
      if (typing) return;
      if (e.key === 'Escape') {
        self._select(null);
        if (document.activeElement && document.activeElement.blur) document.activeElement.blur();
        return;
      }
      var c = self.state.selected ? self._byId(self.state.selected) : null;
      if (!c) return;
      if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); self._remove(c.id); return; }
      var step = e.shiftKey ? 10 : 1, moved = false;
      if (e.key === 'ArrowLeft') { c.x -= step; moved = true; }
      else if (e.key === 'ArrowRight') { c.x += step; moved = true; }
      else if (e.key === 'ArrowUp') { c.y -= step; moved = true; }
      else if (e.key === 'ArrowDown') { c.y += step; moved = true; }
      if (moved) {
        e.preventDefault();
        self._applyItemStyle(c);
        self._syncPanelNumbers(c);
        self._pushHistory();
        self._persist();
      }
    });
  };

  /* ---------- 导入 / 导出 / 重置 ---------- */
  function download(name, obj) {
    var blob = new Blob([JSON.stringify(obj, null, 2)], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { try { URL.revokeObjectURL(url); } catch (e) { } }, 1500);
  }

  Engine.prototype._export = function (all) {
    this._capture();
    var name = this.designName || this.pageName;
    if (all) {
      download(name + '-all.json', { app: 'island-layout-canvas', version: 3, page: name, active: this.state.active, states: this.customStates, layouts: this.layouts });
      return;
    }
    var lay = this.layouts[this.state.active];
    download(name + '-' + this.state.active + '.json', {
      app: 'island-layout-canvas', version: 2, page: name, state: this.state.active,
      spec: lay.spec, components: lay.components
    });
  };

  Engine.prototype._import = function (file) {
    if (!file) return;
    var self = this;
    var reader = new FileReader();
    reader.onload = function () {
      var data = null;
      try { data = JSON.parse(String(reader.result)); } catch (e) { data = null; }
      if (!data) { alert('导入失败：不是有效的 JSON'); return; }
      /* 导入即采用文件里的设计名，这样「导入 clipboard-all.json → 再导出」仍是 clipboard-all.json */
      if (typeof data.page === 'string' && data.page) self.designName = data.page;

      if (data.layouts && typeof data.layouts === 'object') {
        /* 整包：替换全部状态。先恢复自定义状态清单，否则它们的布局会被当成未知状态丢掉 */
        if (Array.isArray(data.states)) {
          self.customStates = [];
          for (var si = 0; si < data.states.length; si++) {
            var cs = data.states[si];
            if (!cs || typeof cs.id !== 'string' || !cs.id) continue;
            self.customStates.push({
              id: cs.id,
              name: (typeof cs.name === 'string' && cs.name) ? cs.name : cs.id,
              w: clampInt(cs.w, 120, 2000, 560),
              h: clampInt(cs.h, 40, 1200, 160)
            });
          }
          if (!self.customStates.length) self.customStates.push({ id: 'custom', name: '自定义', w: 560, h: 160 });
          self.states = self.builtinStates.concat(self.customStates);
          self._buildStateBar();
        }
        var next = {};
        for (var k in data.layouts) {
          if (!Object.prototype.hasOwnProperty.call(data.layouts, k)) continue;
          if (self._stateDef(k)) next[k] = self._sanitize(data.layouts[k]);
        }
        if (!Object.keys(next).length) { alert('导入失败：没有任何可识别的状态'); return; }
        self.layouts = next;
        var act = (typeof data.active === 'string' && self._stateDef(data.active)) ? data.active : self._defaultStateId();
        self._applyLayout(act);
      } else if (Array.isArray(data.components)) {
        /* 单状态：落到 data.state / spec.preset 对应状态，否则当前状态 */
        var target = self._resolveStateId(data.state) || self._resolveStateId(data.spec && data.spec.preset) || self.state.active;
        self._capture();
        self.layouts[target] = self._sanitize(data);
        self._applyLayout(target);
      } else {
        alert('导入失败：不是有效的布局 JSON（缺少 components / layouts）');
        return;
      }
      self._pushHistory();
      self._renderIsland();
      self._renderItems();
      self._renderToolbar();
      self._renderPanel();
      self._applyNotesVisibility();
      self._persist();
    };
    reader.readAsText(file);
  };

  Engine.prototype._reset = function () {
    if (!window.confirm('重置「' + (this._stateDef(this.state.active) || {}).name + '」为默认设计？当前状态内容将被清除。')) return;
    this.layouts = {};
    this._applyLayout(this.state.active);
    this.history = [];
    this.hIndex = -1;
    this._pushHistory();
    this._renderIsland();
    this._renderItems();
    this._renderToolbar();
    this._renderPanel();
    this._applyNotesVisibility();
    this._persist();
  };

  return {
    init: function (cfg) {
      try {
        return new Engine(cfg);
      } catch (e) {
        /* 初始化异常必须看得见：否则页面只剩静态骨架，很难判断哪里坏了 */
        if (window.console && window.console.error) window.console.error('[island-canvas] 初始化失败', e);
        try { window.alert('画布初始化失败：' + (e && e.message ? e.message : e)); } catch (e2) { }
        throw e;
      }
    }
  };
})();
