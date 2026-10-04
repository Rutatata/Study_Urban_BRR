(function () {
'use strict';

var OL = '#1a1020', PI = Math.PI;

window.ROSTER = [
  { id: 'naruto', name: 'Наруто', anime: 'Naruto',
    colors: { hair: '#ffd23a', skin: '#f6c9a0', outfit: '#ff7a1a', accent: '#2a4a9a', eyes: '#3a9ae8' },
    hairStyle: 'spiky', stats: { speed: 8, shot: 7, pass: 6, tackle: 6, keeper: 5 },
    special: { name: 'Расенган-удар', color: '#3fa9ff', color2: '#c4ecff' }, quote: 'Даттебайо! Я не сдамся!' },
  { id: 'goku', name: 'Гоку', anime: 'Dragon Ball',
    colors: { hair: '#15121c', skin: '#f6c9a0', outfit: '#ff7a00', accent: '#1e50c8', eyes: '#2a2230' },
    hairStyle: 'tall', stats: { speed: 8, shot: 10, pass: 4, tackle: 6, keeper: 4 },
    special: { name: 'Камехамеха', color: '#4fc3ff', color2: '#ffffff' }, quote: 'Кто-то сильнее? Отлично!' },
  { id: 'luffy', name: 'Луффи', anime: 'One Piece',
    colors: { hair: '#1a1420', skin: '#f2c196', outfit: '#2f5fcf', accent: '#d62828', eyes: '#2a1a14' },
    hairStyle: 'messy', stats: { speed: 7, shot: 8, pass: 5, tackle: 7, keeper: 6 },
    special: { name: 'Гому-Гому Пистолет', color: '#ff5a3c', color2: '#ffd54a' }, quote: 'Я стану Королём пиратов!' },
  { id: 'ichigo', name: 'Ичиго', anime: 'Bleach',
    colors: { hair: '#ff8a1f', skin: '#f4c7a0', outfit: '#1a1a22', accent: '#e8e8e8', eyes: '#8a4a1a' },
    hairStyle: 'spiky', stats: { speed: 7, shot: 8, pass: 5, tackle: 8, keeper: 4 },
    special: { name: 'Гецуга Тенчо', color: '#ff2d2d', color2: '#2a1030' }, quote: 'Я защищу всех!' },
  { id: 'gojo', name: 'Годжо', anime: 'Jujutsu Kaisen',
    colors: { hair: '#f4f6ff', skin: '#f8d8c0', outfit: '#1b2240', accent: '#14101c', eyes: '#4ab8ff' },
    hairStyle: 'spiky', stats: { speed: 8, shot: 8, pass: 7, tackle: 5, keeper: 6 },
    special: { name: 'Лиловая Пустота', color: '#b36bff', color2: '#ff4d6d' }, quote: 'Не волнуйся. Я сильнейший.' },
  { id: 'tanjiro', name: 'Тандзиро', anime: 'Demon Slayer',
    colors: { hair: '#4a1f2a', skin: '#f4c7a0', outfit: '#1f6f4a', accent: '#2f8f5a', eyes: '#b02a3a' },
    hairStyle: 'messy', stats: { speed: 7, shot: 6, pass: 7, tackle: 8, keeper: 5 },
    special: { name: 'Танец Бога Огня', color: '#ff7a1a', color2: '#ffd23f' }, quote: 'Я буду стараться изо всех сил!' },
  { id: 'saitama', name: 'Сайтама', anime: 'One Punch Man',
    colors: { hair: '#f6c9a0', skin: '#f6c9a0', outfit: '#f5d020', accent: '#d62828', eyes: '#1a1a1a' },
    hairStyle: 'bald', stats: { speed: 5, shot: 10, pass: 3, tackle: 7, keeper: 8 },
    special: { name: 'Обычный Удар', color: '#ffe14d', color2: '#ffffff' }, quote: 'Один удар — и всё.' },
  { id: 'levi', name: 'Леви', anime: 'Attack on Titan',
    colors: { hair: '#1c1a24', skin: '#f7d6bd', outfit: '#e9e4d4', accent: '#3f6b4a', eyes: '#5a6a7a' },
    hairStyle: 'short', stats: { speed: 10, shot: 6, pass: 6, tackle: 8, keeper: 4 },
    special: { name: 'Вихрь Клинков', color: '#9fd6c0', color2: '#e8f4ff' }, quote: 'Не пожалей о выборе.' },
  { id: 'deku', name: 'Дэку', anime: 'My Hero Academia',
    colors: { hair: '#2f9a4a', skin: '#f4c7a0', outfit: '#1f7a46', accent: '#d62828', eyes: '#2fa05a' },
    hairStyle: 'messy', stats: { speed: 7, shot: 8, pass: 5, tackle: 6, keeper: 5 },
    special: { name: 'Смэш: Один за Всех', color: '#59e07a', color2: '#d6ffd9' }, quote: 'Плюс Ультра!' },
  { id: 'isagi', name: 'Исаги', anime: 'Blue Lock',
    colors: { hair: '#1a1822', skin: '#f2c8a2', outfit: '#1d3fbf', accent: '#2fe0d0', eyes: '#2fe0d0' },
    hairStyle: 'messy', stats: { speed: 7, shot: 8, pass: 8, tackle: 5, keeper: 4 },
    special: { name: 'Прямой Гол', color: '#2fe0d0', color2: '#1a3cff' }, quote: 'Гол забью я!' },
  { id: 'tsubasa', name: 'Цубаса', anime: 'Captain Tsubasa',
    colors: { hair: '#1d1a22', skin: '#f0c49c', outfit: '#ffffff', accent: '#2a54c8', eyes: '#3a2a1a' },
    hairStyle: 'short', stats: { speed: 8, shot: 7, pass: 9, tackle: 5, keeper: 3 },
    special: { name: 'Драйв-Шот', color: '#3b82ff', color2: '#ffffff' }, quote: 'Мяч — мой друг!' },
  { id: 'usagi', name: 'Усаги', anime: 'Sailor Moon',
    colors: { hair: '#ffe36b', skin: '#fbd5bb', outfit: '#2a4fd0', accent: '#e83a4a', eyes: '#3a9ae8' },
    hairStyle: 'twintails', stats: { speed: 6, shot: 6, pass: 7, tackle: 5, keeper: 7 },
    special: { name: 'Лунная Тиара', color: '#ff9ecf', color2: '#ffe66b' }, quote: 'Во имя Луны!' }
];

/* ---------- helpers ---------- */

var shadeCache = {};
function shade(hex, a) {
  var key = hex + a;
  if (shadeCache[key]) return shadeCache[key];
  var h = hex.replace('#', '');
  if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
  var n = parseInt(h, 16), rgb = [n >> 16 & 255, n >> 8 & 255, n & 255], out = '#';
  for (var i = 0; i < 3; i++) {
    var v = a < 0 ? rgb[i] * (1 + a) : rgb[i] + (255 - rgb[i]) * a;
    v = Math.max(0, Math.min(255, Math.round(v)));
    out += (v < 16 ? '0' : '') + v.toString(16);
  }
  return (shadeCache[key] = out);
}

function outline(ctx, lw) {
  ctx.lineWidth = lw; ctx.strokeStyle = OL; ctx.lineJoin = 'round'; ctx.stroke();
}
function fillOut(ctx, fill, lw) {
  ctx.fillStyle = fill; ctx.fill(); outline(ctx, lw);
}
function rr(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.lineTo(x + w - r, y); ctx.quadraticCurveTo(x + w, y, x + w, y + r);
  ctx.lineTo(x + w, y + h - r); ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  ctx.lineTo(x + r, y + h); ctx.quadraticCurveTo(x, y + h, x, y + h - r);
  ctx.lineTo(x, y + r); ctx.quadraticCurveTo(x, y, x + r, y);
  ctx.closePath();
}
function ell(ctx, x, y, rx, ry, fill, lw) {
  ctx.beginPath(); ctx.ellipse(x, y, rx, ry, 0, 0, PI * 2);
  ctx.fillStyle = fill; ctx.fill();
  if (lw) outline(ctx, lw);
}
function limb(ctx, x1, y1, x2, y2, w, col, lw) {
  ctx.lineCap = 'round';
  ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2);
  ctx.lineWidth = w + lw * 2; ctx.strokeStyle = OL; ctx.stroke();
  ctx.lineWidth = w; ctx.strokeStyle = col; ctx.stroke();
}
function star(ctx, x, y, r, fill) {
  ctx.beginPath();
  for (var i = 0; i < 10; i++) {
    var a = -PI / 2 + i * PI / 5, rad = i % 2 ? r * 0.45 : r;
    ctx.lineTo(x + Math.cos(a) * rad, y + Math.sin(a) * rad);
  }
  ctx.closePath(); fillOut(ctx, fill, 1);
}

/* ---------- hair ---------- */

var SPIKE = {
  spiky: { n: 7, len: 0.6, up: 0.15, v: [1, 0.8, 1.1], tip: [0.1, 0.22, 0.15] },
  tall:  { n: 7, len: 0.95, up: 0.6, v: [1, 0.8, 1.2], tip: [0.1, 0.22, 0.15] },
  messy: { n: 9, len: 0.4, up: 0.1, v: [1, 0.7, 0.9, 0.6], tip: [0.12, 0.24, 0.1, 0.2, 0.16] },
  short: { n: 6, len: 0.2, up: 0, v: [1, 0.8], tip: [0.2, 0.26, 0.2] }
};
var STYLE_BASE = { twintails: 'short', long: 'short', tied: 'short' };
var SPIKE_ID = { ichigo: { len: 0.8 }, gojo: { len: 0.8, up: 0.45 }, naruto: { len: 0.55 }, deku: { len: 0.5 }, levi: { len: 0.12 } };

function spikeCfg(c) {
  var base = SPIKE[STYLE_BASE[c.hairStyle] || c.hairStyle];
  if (!base) return null;
  var ov = SPIKE_ID[c.id], o = {};
  for (var k in base) o[k] = base[k];
  if (ov) for (k in ov) o[k] = ov[k];
  return o;
}

function spikePath(ctx, cfg, cx, cy, r, front) {
  var a0 = front ? PI * 0.72 : PI * 0.8, a1 = front ? PI * 2.28 : PI * 2.12;
  var n = cfg.n + (front ? 2 : 0), br = r * 0.96, i;
  ctx.beginPath();
  ctx.moveTo(cx + Math.cos(a0) * br, cy + Math.sin(a0) * br);
  for (i = 0; i < n; i++) {
    var am = a0 + (a1 - a0) * (i + 0.5) / n, an = a0 + (a1 - a0) * (i + 1) / n;
    var dx = Math.cos(am) * (1 - cfg.up) - (front ? 0 : 0.3), dy = Math.sin(am) * (1 - cfg.up);
    if (Math.sin(am) < 0) dy -= cfg.up;
    var d = Math.sqrt(dx * dx + dy * dy) || 1, L = r * cfg.len * cfg.v[i % cfg.v.length];
    ctx.lineTo(cx + Math.cos(am) * br + dx / d * L, cy + Math.sin(am) * br + dy / d * L);
    ctx.lineTo(cx + Math.cos(an) * br, cy + Math.sin(an) * br);
  }
  ctx.lineTo(cx, cy); ctx.closePath();
}

function tail(ctx, tx, ty, w, L, fill, lw) {
  ctx.beginPath();
  ctx.moveTo(tx - w, ty);
  ctx.bezierCurveTo(tx - w * 1.5, ty + L * 0.5, tx - w * 0.6, ty + L * 0.9, tx, ty + L);
  ctx.bezierCurveTo(tx + w * 0.6, ty + L * 0.9, tx + w * 1.5, ty + L * 0.5, tx + w, ty);
  ctx.closePath(); fillOut(ctx, fill, lw);
}

function backHair(ctx, c, cx, cy, r, front, lw) {
  var hc = c.colors.hair, st = c.hairStyle;
  if (st === 'bald') return;
  var cfg = spikeCfg(c);
  if (st === 'long') {
    rr(ctx, front ? cx - r * 1.12 : cx - r * 1.12, cy - r * 0.6, front ? r * 2.24 : r * 1.5, r * 2.4, r * 0.6);
    fillOut(ctx, shade(hc, -0.15), lw);
  }
  if (st === 'twintails') {
    var sh = shade(hc, -0.12), ty = cy - r * 0.05;
    if (front) { tail(ctx, cx - r * 1.1, ty, r * 0.38, r * 2.0, hc, lw); tail(ctx, cx + r * 1.1, ty, r * 0.38, r * 2.0, hc, lw); }
    else { tail(ctx, cx - r * 1.0, ty, r * 0.4, r * 2.0, hc, lw); tail(ctx, cx - r * 0.35, ty - r * 0.1, r * 0.34, r * 1.7, sh, lw); }
    var bx = front ? [-0.6, 0.6] : [-0.6, 0.3];
    for (var i = 0; i < 2; i++) ell(ctx, cx + bx[i] * r, cy - r * 1.0, r * 0.36, r * 0.34, hc, lw);
  }
  if (cfg) { spikePath(ctx, cfg, cx, cy, r, front); fillOut(ctx, hc, lw); }
  if (st === 'twintails') {
    var tx = front ? [-1.1, 1.1] : [-1.0, -0.35];
    for (i = 0; i < tx.length; i++) ell(ctx, cx + tx[i] * r, cy - r * 0.05, r * 0.2, r * 0.2, c.colors.accent, lw * 0.8);
  }
}

function capPath(ctx, cfg, cx, cy, r, front) {
  var xr = cx + r * (front ? 0.8 : 0.98), xl = cx - r * (front ? 0.8 : 0.35), n = front ? 5 : 4, i;
  ctx.beginPath();
  ctx.moveTo(cx - r, cy);
  ctx.arc(cx, cy, r * 1.04, PI, PI * 2);
  if (front) { ctx.lineTo(cx + r, cy + r * 0.35); ctx.lineTo(xr, cy + r * 0.35); }
  ctx.lineTo(xr, cy - r * 0.15);
  for (i = 0; i < n; i++) {
    ctx.lineTo(xr + (xl - xr) * (i + 0.5) / n, cy - r * cfg.tip[i % cfg.tip.length]);
    ctx.lineTo(xr + (xl - xr) * (i + 1) / n, cy - r * 0.42);
  }
  if (front) { ctx.lineTo(xl, cy + r * 0.35); ctx.lineTo(cx - r, cy + r * 0.35); }
  else { ctx.lineTo(xl, cy + r * 0.5); ctx.lineTo(cx - r * 0.98, cy + r * 0.55); }
  ctx.closePath();
}

function frontHair(ctx, c, cx, cy, r, front, lw) {
  var hc = c.colors.hair, cfg = spikeCfg(c);
  if (!cfg) {
    ctx.beginPath(); ctx.arc(cx - r * 0.2, cy - r * 0.55, r * 0.5, PI * 1.15, PI * 1.6);
    ctx.lineWidth = r * 0.12; ctx.lineCap = 'round'; ctx.strokeStyle = 'rgba(255,255,255,0.65)'; ctx.stroke();
    return;
  }
  capPath(ctx, cfg, cx, cy, r, front);
  ctx.fillStyle = hc; ctx.fill();
  ctx.save(); ctx.clip();
  ctx.fillStyle = shade(hc, -0.2);
  ctx.beginPath(); ctx.ellipse(cx - r * (front ? 1.2 : 0.7), cy, r * 0.8, r * 1.2, 0, 0, PI * 2); ctx.fill();
  ctx.beginPath(); ctx.arc(cx, cy + r * 0.05, r * 0.78, PI * 1.12, PI * 1.5);
  ctx.lineWidth = r * 0.14; ctx.lineCap = 'round'; ctx.strokeStyle = shade(hc, 0.4); ctx.globalAlpha = 0.7; ctx.stroke();
  ctx.restore();
  capPath(ctx, cfg, cx, cy, r, front); outline(ctx, lw);
}

/* ---------- face ---------- */

function eye(ctx, x, y, w, h, col, lw, mood) {
  if (mood === 'stun') {
    ctx.lineCap = 'round'; ctx.lineWidth = lw * 1.3; ctx.strokeStyle = OL; ctx.beginPath();
    ctx.moveTo(x - w * 0.7, y - h * 0.5); ctx.lineTo(x + w * 0.7, y + h * 0.5);
    ctx.moveTo(x + w * 0.7, y - h * 0.5); ctx.lineTo(x - w * 0.7, y + h * 0.5); ctx.stroke();
    return;
  }
  ell(ctx, x, y, w, h, '#ffffff'); ctx.lineWidth = lw * 0.5; ctx.strokeStyle = OL; ctx.stroke();
  ell(ctx, x, y + h * 0.08, w * 0.82, h * 0.92, col);
  ctx.beginPath(); ctx.ellipse(x, y + h * 0.08, w * 0.82, h * 0.92, 0, PI, PI * 2);
  ctx.fillStyle = shade(col, -0.4); ctx.fill();
  ell(ctx, x, y + h * 0.12, w * 0.38, h * 0.5, OL);
  ell(ctx, x - w * 0.28, y - h * 0.3, w * 0.3, w * 0.3, '#fff');
  ell(ctx, x + w * 0.3, y + h * 0.4, w * 0.14, w * 0.14, 'rgba(255,255,255,0.85)');
  ctx.beginPath(); ctx.ellipse(x, y, w, h, 0, PI * 1.02, PI * 1.98);
  ctx.lineWidth = lw * 1.5; ctx.lineCap = 'round'; ctx.strokeStyle = OL; ctx.stroke();
}

function eyeSpots(cx, r, front) {
  return front ? [{ x: cx - r * 0.42, w: r * 0.26 }, { x: cx + r * 0.42, w: r * 0.26 }]
               : [{ x: cx + r * 0.2, w: r * 0.25 }, { x: cx + r * 0.7, w: r * 0.2 }];
}

function face(ctx, c, cx, cy, r, front, mood, lw) {
  var sp = eyeSpots(cx, r, front), ey = cy + r * 0.25, i, brow = shade(c.colors.hair === c.colors.skin ? '#8a6a50' : c.colors.hair, -0.3);
  if (c.id !== 'gojo') {
    for (i = 0; i < 2; i++) {
      var s = sp[i], h = s.w * 1.35;
      eye(ctx, s.x, ey, s.w, h, c.colors.eyes, lw, mood);
      ctx.beginPath(); ctx.lineCap = 'round'; ctx.lineWidth = lw * 1.1; ctx.strokeStyle = brow;
      ctx.moveTo(s.x - s.w * 0.9, ey - h * 1.3 + (mood === 'open' ? -r * 0.04 : 0));
      ctx.lineTo(s.x + s.w * 0.8, ey - h * 1.45); ctx.stroke();
    }
  }
  var mx = front ? cx : cx + r * 0.48, my = cy + r * 0.66;
  if (mood === 'open') {
    ctx.beginPath(); ctx.ellipse(mx, my, r * 0.15, r * 0.13, 0, 0, PI * 2);
    fillOut(ctx, '#7a1830', lw * 0.6);
  } else {
    ctx.beginPath(); ctx.arc(mx, my - r * 0.06, r * 0.13, PI * 0.15, PI * 0.85);
    ctx.lineWidth = lw * 0.9; ctx.lineCap = 'round'; ctx.strokeStyle = OL; ctx.stroke();
  }
  ctx.beginPath(); ctx.ellipse(front ? cx - r * 0.7 : cx + r * 0.35, cy + r * 0.52, r * 0.12, r * 0.07, 0, 0, PI * 2);
  ctx.fillStyle = 'rgba(255,90,110,0.35)'; ctx.fill();
  if (front) { ctx.beginPath(); ctx.ellipse(cx + r * 0.7, cy + r * 0.52, r * 0.12, r * 0.07, 0, 0, PI * 2); ctx.fill(); }
}

function skinMarks(ctx, c, cx, cy, r, front, lw) {
  var sp = eyeSpots(cx, r, front), id = c.id, i, j;
  ctx.lineCap = 'round';
  if (id === 'naruto') {
    ctx.strokeStyle = 'rgba(26,16,32,0.75)'; ctx.lineWidth = lw * 0.6; ctx.beginPath();
    var xs = front ? [[-0.95, -0.55], [0.55, 0.95]] : [[0.3, 0.72]];
    for (i = 0; i < xs.length; i++) for (j = 0; j < 3; j++) {
      ctx.moveTo(cx + r * xs[i][0], cy + r * (0.42 + j * 0.12));
      ctx.lineTo(cx + r * xs[i][1], cy + r * (0.38 + j * 0.12));
    }
    ctx.stroke();
  } else if (id === 'deku') {
    ctx.fillStyle = '#b8643a';
    for (i = 0; i < sp.length; i++) for (j = 0; j < 3; j++) {
      ctx.beginPath(); ctx.arc(sp[i].x + (j - 1) * r * 0.1, cy + r * (0.5 + (j % 2) * 0.06), lw * 0.4, 0, PI * 2); ctx.fill();
    }
  } else if (id === 'luffy') {
    ctx.strokeStyle = '#7a2a2a'; ctx.lineWidth = lw * 0.9; ctx.beginPath();
    ctx.moveTo(sp[0].x - sp[0].w * 0.2, cy + r * 0.55); ctx.lineTo(sp[0].x - sp[0].w * 0.2, cy + r * 0.78); ctx.stroke();
  } else if (id === 'isagi') {
    var s = sp[1];
    ctx.strokeStyle = 'rgba(47,224,208,0.9)'; ctx.lineWidth = lw * 0.9; ctx.beginPath();
    ctx.ellipse(s.x, cy + r * 0.25, s.w * 1.2, s.w * 1.7, 0, 0, PI * 2); ctx.stroke();
  }
}

function accessories(ctx, c, cx, cy, r, front, lw) {
  var id = c.id, px = front ? cx : cx + r * 0.45, acc = c.colors.accent;
  if (id === 'naruto' || id === 'tanjiro') {
    ctx.beginPath();
    ctx.moveTo(cx - r * 1.0, cy - r * 0.5); ctx.quadraticCurveTo(cx, cy - r * 0.68, cx + r * 1.0, cy - r * 0.5);
    ctx.lineTo(cx + r * 1.0, cy - r * 0.24); ctx.quadraticCurveTo(cx, cy - r * 0.4, cx - r * 1.0, cy - r * 0.24);
    ctx.closePath(); fillOut(ctx, id === 'naruto' ? acc : '#f0f0f0', lw);
    if (id === 'naruto') {
      rr(ctx, px - r * 0.34, cy - r * 0.55, r * 0.68, r * 0.3, r * 0.05); fillOut(ctx, '#cdd7e4', lw * 0.8);
      ctx.beginPath(); ctx.arc(px, cy - r * 0.4, r * 0.08, 0, PI * 1.5); ctx.lineWidth = lw * 0.5; ctx.strokeStyle = OL; ctx.stroke();
    } else {
      ctx.beginPath(); ctx.moveTo(px - r * 0.1, cy - r * 0.2); ctx.lineTo(px + r * 0.25, cy - r * 0.05); ctx.lineTo(px + r * 0.15, cy - r * 0.2);
      ctx.fillStyle = '#c8202e'; ctx.fill();
      ctx.beginPath(); ctx.ellipse(cx - r * 1.02, cy + r * 0.3, r * 0.07, r * 0.16, 0, 0, PI * 2); fillOut(ctx, '#e8e8e8', lw * 0.6);
    }
  } else if (id === 'luffy') {
    var hx = front ? cx : cx + r * 0.05, gold = '#f4d77a';
    ctx.beginPath(); ctx.ellipse(hx, cy - r * 0.7, r * 1.5, r * 0.34, 0, 0, PI * 2); fillOut(ctx, gold, lw);
    ctx.beginPath(); ctx.ellipse(hx + r * 0.5, cy - r * 0.62, r * 0.9, r * 0.2, 0, 0, PI); ctx.fillStyle = shade(gold, -0.2); ctx.fill();
    ctx.beginPath(); ctx.moveTo(hx - r * 0.8, cy - r * 0.72);
    ctx.bezierCurveTo(hx - r * 0.85, cy - r * 1.85, hx + r * 0.85, cy - r * 1.85, hx + r * 0.8, cy - r * 0.72);
    ctx.closePath(); fillOut(ctx, gold, lw);
    ctx.beginPath(); ctx.moveTo(hx - r * 0.8, cy - r * 0.78); ctx.lineTo(hx + r * 0.8, cy - r * 0.78);
    ctx.lineTo(hx + r * 0.83, cy - r * 1.0); ctx.lineTo(hx - r * 0.83, cy - r * 1.0); ctx.closePath(); fillOut(ctx, acc, lw * 0.9);
  } else if (id === 'tsubasa') {
    ctx.beginPath(); ctx.arc(cx, cy - r * 0.1, r * 1.08, PI, PI * 2); ctx.closePath(); fillOut(ctx, '#ffffff', lw);
    ctx.beginPath(); ctx.arc(cx, cy - r * 0.1, r * 1.08, PI * 1.12, PI * 1.5); ctx.lineWidth = r * 0.12; ctx.strokeStyle = acc; ctx.stroke();
    ctx.beginPath(); ctx.ellipse(front ? cx : cx + r * 0.95, cy - r * 0.2, r * (front ? 0.95 : 0.55), r * 0.16, 0, 0, PI * 2); fillOut(ctx, acc, lw);
  } else if (id === 'usagi') {
    ctx.beginPath(); ctx.moveTo(px - r * 0.2, cy - r * 0.42); ctx.lineTo(px, cy - r * 0.78); ctx.lineTo(px + r * 0.2, cy - r * 0.42);
    ctx.closePath(); fillOut(ctx, '#ffd54a', lw * 0.8);
    ell(ctx, px, cy - r * 0.5, r * 0.09, r * 0.09, acc, lw * 0.5);
  } else if (id === 'gojo') {
    rr(ctx, cx - r * 1.0, cy - r * 0.02, r * 2.0, r * 0.52, r * 0.1); fillOut(ctx, acc, lw);
    ctx.beginPath(); ctx.moveTo(cx - r * 0.6, cy + r * 0.08); ctx.lineTo(cx + r * 0.2, cy + r * 0.08);
    ctx.lineWidth = lw * 0.6; ctx.strokeStyle = 'rgba(255,255,255,0.25)'; ctx.stroke();
  }
}

function drawHead(ctx, c, cx, cy, r, front, mood, lw) {
  backHair(ctx, c, cx, cy, r, front, lw);
  ctx.beginPath(); ctx.ellipse(cx, cy, r, r * 0.96, 0, 0, PI * 2);
  fillOut(ctx, c.colors.skin, lw);
  ctx.beginPath(); ctx.arc(cx, cy, r * 0.96, PI * 0.55, PI * 1.05); ctx.fillStyle = shade(c.colors.skin, -0.1); ctx.fill();
  if (!front) {
    ell(ctx, cx - r * 0.2, cy + r * 0.2, r * 0.17, r * 0.22, shade(c.colors.skin, -0.05), lw * 0.7);
  }
  face(ctx, c, cx, cy, r, front, mood, lw);
  skinMarks(ctx, c, cx, cy, r, front, lw);
  frontHair(ctx, c, cx, cy, r, front, lw);
  accessories(ctx, c, cx, cy, r, front, lw);
  if (c.hairStyle === 'bald') {
    ctx.beginPath(); ctx.arc(cx, cy, r * 0.8, PI * 1.15, PI * 1.5);
    ctx.lineWidth = r * 0.12; ctx.lineCap = 'round'; ctx.strokeStyle = 'rgba(255,255,255,0.6)'; ctx.stroke();
  }
}

/* ---------- body ---------- */

function leg(ctx, hx, hy, a, bend, col, sock, shoe, lw) {
  var kx = hx + Math.sin(a) * 8, ky = hy + Math.cos(a) * 8, b = a - bend;
  var fx = kx + Math.sin(b) * 8, fy = ky + Math.cos(b) * 8;
  limb(ctx, hx, hy, kx, ky, 5.5, col, lw);
  limb(ctx, kx, ky, fx, fy, 5.5, col, lw);
  ctx.lineCap = 'round'; ctx.beginPath(); ctx.moveTo((kx + fx) / 2, (ky + fy) / 2); ctx.lineTo(fx, fy);
  ctx.lineWidth = 5.5; ctx.strokeStyle = sock; ctx.stroke();
  ctx.save(); ctx.translate(fx + Math.cos(b) * 2.2, fy - Math.sin(b) * 2.2); ctx.rotate(-b + 0.0);
  ctx.beginPath(); ctx.ellipse(1.5, 0.5, 4.8, 3, 0, 0, PI * 2); fillOut(ctx, shoe, lw);
  ctx.restore();
}

function arm(ctx, sx, sy, a, skin, sleeve, hand, lw) {
  var ex = sx + Math.sin(a) * 10, ey = sy + Math.cos(a) * 10;
  limb(ctx, sx, sy, ex, ey, 4.2, skin, lw);
  ctx.lineCap = 'round'; ctx.beginPath(); ctx.moveTo(sx, sy);
  ctx.lineTo(sx + Math.sin(a) * 4, sy + Math.cos(a) * 4); ctx.lineWidth = 5.2; ctx.strokeStyle = sleeve; ctx.stroke();
  ell(ctx, ex, ey, 2.9, 2.9, hand, lw * 0.8);
}

function pose(st, p, t, moving) {
  var s = Math.sin(p), P = { lean: 0, rot: 0, px: 0, py: 0, bob: 0,
    lN: [0.1, 0.05], lF: [-0.1, 0.05], aN: 0.2 + Math.sin(t * 2) * 0.05, aF: -0.2, mood: 'normal', sway: Math.sin(t * 3) * 1.2 };
  if (st === 'run') {
    P.lN = [s * 0.95, Math.max(0, -s) * 1.3 + 0.1]; P.lF = [-s * 0.95, Math.max(0, s) * 1.3 + 0.1];
    P.aN = -s * 0.9; P.aF = s * 0.9; P.lean = 0.12; P.bob = -Math.abs(Math.cos(p)) * 1.8; P.sway = s * 2.5;
  } else if (st === 'kick') {
    P.lN = [1.25, -0.3]; P.lF = [-0.35, 0.3]; P.aN = -0.9; P.aF = 1.1; P.lean = -0.12; P.mood = 'open'; P.sway = 3;
  } else if (st === 'slide') {
    P.rot = -1.05; P.px = -6; P.py = -6; P.lN = [2.5, -0.1]; P.lF = [1.8, 1.4]; P.aN = -0.3; P.aF = 2.2; P.mood = 'open';
  } else if (st === 'dive') {
    P.rot = 1.5; P.px = -2; P.py = -27; P.lN = [0.2, 0.2]; P.lF = [-0.15, 0.5]; P.aN = 2.9; P.aF = 2.7; P.mood = 'open';
  } else if (st === 'stun') {
    P.rot = Math.sin(t * 6) * 0.06; P.lean = Math.sin(t * 6) * 0.08; P.aN = 0.5; P.aF = -0.5; P.mood = 'stun';
  } else if (st === 'celebrate') {
    var j = Math.abs(Math.sin(p || t * 8));
    P.lN = [0.35, 0.3]; P.lF = [-0.3, 0.2]; P.aN = 2.7 + Math.sin(t * 12) * 0.15; P.aF = -2.7 - Math.sin(t * 12) * 0.15;
    P.bob = -j * 5; P.mood = 'open'; P.sway = 2;
  } else {
    P.bob = Math.sin(t * 3) * 0.5;
  }
  return P;
}

function aura(ctx, c, t) {
  var col = c.special.color, col2 = c.special.color2, i;
  var g = ctx.createRadialGradient(0, -30, 4, 0, -30, 40);
  g.addColorStop(0, col2); g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = 0.35;
  ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, -30, 28, 42, 0, 0, PI * 2); ctx.fill();
  ctx.globalAlpha = 0.6;
  for (i = 0; i < 9; i++) {
    var bx = -20 + i * 5, fl = Math.sin(t * 14 + i * 1.9), H = 34 + (i % 3) * 10 + fl * 7, W = 5 + (i % 2) * 2;
    var cxx = bx + Math.sin(t * 9 + i) * 2;
    ctx.fillStyle = i % 2 ? col : col2;
    ctx.beginPath(); ctx.moveTo(bx - W, 0);
    ctx.quadraticCurveTo(bx - W * 1.2, -H * 0.5, cxx, -H);
    ctx.quadraticCurveTo(bx + W * 1.2, -H * 0.5, bx + W, 0); ctx.closePath(); ctx.fill();
  }
  ctx.restore();
}

function behindBody(ctx, c, P, lw) {
  var id = c.id, sw = P.sway;
  if (id === 'saitama' || id === 'levi') {
    var col = id === 'saitama' ? '#f4f4f4' : c.colors.accent;
    ctx.beginPath(); ctx.moveTo(-3, -32); ctx.lineTo(3, -31);
    ctx.lineTo(-4, -10); ctx.quadraticCurveTo(-14 - sw, -6, -19 - sw, -4);
    ctx.quadraticCurveTo(-18 - sw, -20, -3, -32); ctx.closePath(); fillOut(ctx, col, lw);
    if (id === 'levi') { ctx.beginPath(); ctx.arc(-12 - sw * 0.5, -18, 2.5, 0, PI * 2); ctx.fillStyle = '#e8f0ff'; ctx.fill(); }
  } else if (id === 'ichigo') {
    ctx.beginPath(); ctx.moveTo(-6, -22); ctx.lineTo(-12, -60); ctx.lineTo(-7, -62); ctx.lineTo(-1, -26); ctx.closePath();
    fillOut(ctx, '#d8dde8', lw);
    ctx.beginPath(); ctx.moveTo(-7, -20); ctx.lineTo(-1, -24); ctx.lineTo(-2, -29); ctx.lineTo(-8, -25); ctx.closePath(); fillOut(ctx, '#1a1a22', lw);
  }
}

function torso(ctx, c, o, team, lw, f) {
  var id = c.id;
  rr(ctx, -6.5, -32, 13, 16, 4); ctx.fillStyle = team; ctx.fill();
  ctx.save(); ctx.clip(); ctx.fillStyle = shade(team, -0.2); ctx.fillRect(-7, -33, 5, 18);
  ctx.restore();
  rr(ctx, -6.5, -32, 13, 16, 4); outline(ctx, lw);
  rr(ctx, -5, -33.5, 10, 3.5, 1.5); fillOut(ctx, c.colors.outfit === team ? c.colors.accent : c.colors.outfit, lw * 0.7);
  if (id === 'levi') {
    ctx.beginPath(); ctx.moveTo(-2, -31); ctx.lineTo(4, -31); ctx.lineTo(1, -23); ctx.closePath(); fillOut(ctx, '#ffffff', lw * 0.7);
  } else if (id === 'usagi') {
    ctx.beginPath(); ctx.moveTo(1, -27); ctx.lineTo(-3, -30); ctx.lineTo(-3, -24); ctx.closePath(); fillOut(ctx, c.colors.accent, lw * 0.7);
    ctx.beginPath(); ctx.moveTo(1, -27); ctx.lineTo(5, -30); ctx.lineTo(5, -24); ctx.closePath(); fillOut(ctx, c.colors.accent, lw * 0.7);
  }
  if (o.number != null) {
    ctx.save(); ctx.translate(0.5, -23.5); ctx.scale(f, 1);
    ctx.font = 'bold 8px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.lineWidth = 2; ctx.strokeStyle = 'rgba(26,16,32,0.8)'; ctx.strokeText(String(o.number), 0, 0);
    ctx.fillStyle = '#fff'; ctx.fillText(String(o.number), 0, 0);
    ctx.restore();
  }
}

function shorts(ctx, c, lw) {
  var col = c.colors.outfit;
  if (c.id === 'usagi') {
    ctx.beginPath(); ctx.moveTo(-6.5, -19); ctx.lineTo(6.5, -19); ctx.lineTo(9.5, -10); ctx.lineTo(-9.5, -10); ctx.closePath();
  } else rr(ctx, -7, -19, 14, 8, 2.5);
  fillOut(ctx, col, lw);
  ctx.beginPath(); ctx.moveTo(-6, -18); ctx.lineTo(-3, -18); ctx.lineTo(-4, -11); ctx.lineTo(-8, -11); ctx.closePath();
  ctx.fillStyle = shade(col, -0.22); ctx.fill();
}

window.drawCharacter = function (ctx, x, y, ch, o) {
  o = o || {};
  var sc = o.scale || 1, f = o.facing < 0 ? -1 : 1, c = ch.colors, lw = 2;
  var st = o.state || (o.moving ? 'run' : 'idle'), p = o.runPhase || 0, t = Date.now() / 1000;
  var team = o.teamColor || c.outfit, P = pose(st, p, t, st === 'run');
  var skin = c.skin, skinF = shade(skin, -0.18), teamF = shade(team, -0.25);
  var hand = ch.id === 'saitama' ? c.accent : skin, handF = ch.id === 'saitama' ? shade(c.accent, -0.2) : skinF;
  var shoe = ch.id === 'deku' ? c.accent : '#f2f2f2', sock = shade(team, 0.1);

  ctx.save();
  ctx.translate(x, y); ctx.scale(sc * f, sc);
  ell(ctx, 0, 1, st === 'dive' || st === 'slide' ? 22 : 14, 4, 'rgba(0,0,0,0.28)');
  if (o.aura) aura(ctx, ch, t);
  ctx.translate(0, P.bob);
  if (P.rot) { ctx.translate(P.px, P.py); ctx.rotate(P.rot); ctx.translate(0, 17); }

  function upper(fn) {
    ctx.save(); ctx.translate(0, -17); ctx.rotate(P.lean); ctx.translate(0, 17); fn(); ctx.restore();
  }
  upper(function () { behindBody(ctx, ch, P, lw); });
  leg(ctx, 1, -17, P.lF[0], P.lF[1], skinF, shade(sock, -0.25), shade(shoe, -0.2), lw);
  upper(function () {
    arm(ctx, 0.5, -29.5, P.aF, skinF, teamF, handF, lw);
    torso(ctx, ch, o, team, lw, f);
  });
  shorts(ctx, ch, lw);
  leg(ctx, 0, -17, P.lN[0], P.lN[1], skin, sock, shoe, lw);
  upper(function () {
    drawHead(ctx, ch, 2, -45, 14, false, P.mood, lw);
    arm(ctx, 0.5, -29.5, P.aN, skin, team, hand, lw);
  });
  ctx.restore();

  if (st === 'stun') {
    ctx.save(); ctx.translate(x, y); ctx.scale(sc * f, sc);
    for (var i = 0; i < 3; i++) {
      var a = t * 5 + i * PI * 2 / 3;
      star(ctx, 2 + Math.cos(a) * 14, -66 + Math.sin(a) * 4, 3.5, '#ffe04a');
    }
    ctx.restore();
  }
};

/* ---------- portrait ---------- */

window.drawPortrait = function (ctx, x, y, size, ch) {
  var c = ch.colors, id = ch.id, lw = 2.2, s = size / 100, outfit = c.outfit;
  ctx.save();
  ctx.translate(x, y); ctx.scale(s, s);
  if (id === 'saitama' || id === 'levi') {
    rr(ctx, -42, 20, 84, 34, 14); fillOut(ctx, id === 'saitama' ? '#f4f4f4' : c.accent, lw);
  }
  ctx.save(); ctx.beginPath(); ctx.rect(-50, -50, 100, 100); ctx.clip();
  rr(ctx, -7, 14, 14, 16, 4); fillOut(ctx, shade(c.skin, -0.12), lw);
  rr(ctx, -34, 24, 68, 40, 16); ctx.fillStyle = outfit; ctx.fill();
  ctx.save(); ctx.clip(); ctx.fillStyle = shade(outfit, -0.2); ctx.fillRect(-36, 20, 20, 40);
  if (id === 'tanjiro') {
    for (var i = 0; i < 9; i++) for (var j = 0; j < 4; j++) {
      if ((i + j) % 2) { ctx.fillStyle = '#17181c'; ctx.fillRect(-36 + i * 8.5, 24 + j * 8.5, 8.5, 8.5); }
    }
  }
  ctx.restore();
  rr(ctx, -34, 24, 68, 40, 16); outline(ctx, lw);
  ctx.beginPath(); ctx.moveTo(-9, 21); ctx.lineTo(0, 36); ctx.lineTo(9, 21); ctx.closePath();
  fillOut(ctx, id === 'levi' ? '#ffffff' : (id === 'usagi' || id === 'saitama' ? shade(c.skin, -0.12) : c.accent), lw);
  if (id === 'usagi') {
    ctx.beginPath(); ctx.moveTo(0, 33); ctx.lineTo(-10, 27); ctx.lineTo(-10, 39); ctx.closePath(); fillOut(ctx, c.accent, lw);
    ctx.beginPath(); ctx.moveTo(0, 33); ctx.lineTo(10, 27); ctx.lineTo(10, 39); ctx.closePath(); fillOut(ctx, c.accent, lw);
    ell(ctx, 0, 33, 3.5, 3.5, '#ffd54a', lw * 0.7);
  }
  ctx.restore();
  drawHead(ctx, ch, 0, -3, 24, true, 'normal', lw);
  ctx.restore();
};

})();
