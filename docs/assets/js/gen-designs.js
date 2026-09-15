#!/usr/bin/env node
/* gen-designs.js — 把归档的设计稿（docs/designs 下的 json，含 completed/）生成为画布模板（docs/assets/js/&lt;page&gt;-designs.js）。
 *
 * 用法：
 *   node docs/assets/js/gen-designs.js docs/designs/file-all.json
 *   node docs/assets/js/gen-designs.js docs/designs/*.json docs/designs/completed/*.json
 *
 * 约定：
 *   - 导出稿（JSON）是唯一真源：改设计请改 JSON，再重跑本脚本。
 *   - 模板只写四个内置状态（wait / unfold / contract / custom）；自定义状态（如「唤起态」）
 *     不在此文件里，随导出 JSON 的 states 一起导入。
 *   - 输出路径 = docs/assets/js/<json 的 page 字段>-designs.js。
 */
'use strict';
const fs = require('fs');
const path = require('path');

const ORDER = ['wait', 'unfold', 'contract', 'custom'];
const NAMES = { wait: '等待态', unfold: '展开态', contract: '收缩态', custom: '自定义 / 入场态' };

const args = process.argv.slice(2);
if (!args.length) {
  console.error('usage: node docs/assets/js/gen-designs.js <design.json> [more.json ...]');
  process.exit(1);
}

const rel = p => path.relative(process.cwd(), p).replace(/\\/g, '/');

args.forEach(function (src) {
  const abs = path.resolve(src);
  const data = JSON.parse(fs.readFileSync(abs, 'utf8'));
  const page = data.page || path.basename(abs).replace(/-all\.json$/, '').replace(/^completed_/, '');
  /* 模板始终写到本脚本所在的 docs/assets/js/，与设计稿放在 designs/ 还是 designs/completed/ 无关 */
  const out = path.resolve(__dirname, page + '-designs.js');

  const keys = ORDER.filter(function (id) { return !!data.layouts[id]; });
  const blocks = keys.map(function (id, i) {
    const L = data.layouts[id];
    const body = JSON.stringify(L, null, 2).split('\n');
    const rest = body.slice(1).map(function (l) { return '    ' + l; }).join('\n');
    const comma = (i === keys.length - 1) ? '' : ',';
    return '    /* ' + NAMES[id] + ' ' + L.spec.width + '×' + L.spec.height + ' */\n'
      + '    ' + id + ':     ' + body[0] + '\n' + rest + comma;
  });

  const header = [
    '/* ' + page + '-designs.js — 由 ' + rel(abs) + ' 生成（导出稿与画布默认稿保持同源）',
    ' * 重新生成：node ' + rel(__filename) + ' ' + rel(abs),
    ' * 自定义状态（如「唤起态」）不在此文件里，随导出 JSON 的 states 一起导入。',
    ' */',
    'window.IslandDesigns = window.IslandDesigns || {};',
    'window.IslandDesigns.' + page + ' = {'
  ];

  fs.writeFileSync(out, header.join('\n') + '\n' + blocks.join('\n\n') + '\n};\n', 'utf8');
  console.log('generated ' + rel(out) + '  [' + keys.join(', ') + ']');
});
