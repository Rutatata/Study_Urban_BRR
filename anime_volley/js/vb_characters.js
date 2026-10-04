/* Volleyball chibi characters: data + drawing (fan tribute, Haikyuu!!-style). Plain script, no modules.
   API: window.VB_TEAMS, window.drawVB(ctx,x,y,ch,o), window.drawVBPortrait(ctx,x,y,size,ch[,mood]) */
(function () {
'use strict';

var OL = '#1a1020', PI = Math.PI;

/* ======================= DATA ======================= */

var TEAMS = [
  { id: 'karasuno', name: 'Карасуно',
    colors: { shirt: '#22222c', trim: '#ff7a1a', shorts: '#22222c', num: '#ff8a1f', liberoShirt: '#ff7a1a', liberoNum: '#22222c' } },
  { id: 'aoba', name: 'Аоба Джосай',
    colors: { shirt: '#f3f6f8', trim: '#2fb5a8', shorts: '#2fb5a8', num: '#1f9d94', liberoShirt: '#2fb5a8', liberoNum: '#ffffff' } },
  { id: 'nekoma', name: 'Некома',
    colors: { shirt: '#d6202c', trim: '#f4f4f4', shorts: '#1c1c22', num: '#ffffff', liberoShirt: '#f4f4f4', liberoNum: '#d6202c' } },
  { id: 'shiratorizawa', name: 'Шираторизава',
    colors: { shirt: '#f4f4f8', trim: '#7a3fb0', shorts: '#6a2fa0', num: '#6a2fa0', liberoShirt: '#6a2fa0', liberoNum: '#ffffff' } },
  { id: 'fukurodani', name: 'Фукуродани',
    colors: { shirt: '#34353d', trim: '#e8b923', shorts: '#202026', num: '#f2c230', liberoShirt: '#e6e6ee', liberoNum: '#2b2b33' } }
];

var SK = '#f6cfa8', SKT = '#e9b78c', SKP = '#f8dcc8';

/* p(id,name,team,num,role, hair,hair2,skin,eyes, hairStyle,extra, h, [spike,jump,receive,set,block,serve,speed], spName,kind,c1,c2, quote, hairK) */
function p(id, name, team, num, role, hair, hair2, skin, eyes, style, extra, h, st, sn, kind, c1, c2, quote, hk) {
  return { id: id, name: name, team: team, num: num, role: role,
    colors: { hair: hair, hair2: hair2, skin: skin, eyes: eyes },
    hairStyle: style, extra: extra, h: h, hairK: hk || 1,
    stats: { spike: st[0], jump: st[1], receive: st[2], set: st[3], block: st[4], serve: st[5], speed: st[6] },
    special: { name: sn, kind: kind, color: c1, color2: c2 }, quote: quote };
}

var PL = {
  karasuno: [
    p('hinata', 'Хината', 'karasuno', 10, 'MB', '#ff8a1f', '#d65a10', '#f4c49a', '#7a4a22', 'spiky', null, 0.9, [7, 10, 3, 2, 5, 4, 10], 'Странная быстрая', 'spike', '#ffa21f', '#fff1b0', 'Я могу взлететь!', 1.15),
    p('kageyama', 'Кагеяма', 'karasuno', 9, 'S', '#1c1822', '#34303c', SK, '#2a3a6a', 'straight', 'sharpEyes', 1.04, [6, 6, 5, 10, 6, 9, 6], 'Пас-гильотина', 'set', '#3a7bff', '#cfe3ff', 'Дай мне мяч, я дам лучший пас!'),
    p('nishinoya', 'Нишиноя', 'karasuno', 4, 'L', '#1c1822', '#f2d060', SKT, '#6a4a2a', 'spiky', 'streak', 0.9, [1, 5, 10, 4, 2, 5, 8], 'Роллинг Сандер', 'dig', '#ffd23a', '#fff6c0', 'Мяч не упадёт, пока я здесь!', 0.85),
    p('tanaka', 'Танака', 'karasuno', 5, 'WS', '#1e1a20', '#3a3440', SKT, '#4a3222', 'buzz', null, 0.98, [7, 5, 4, 2, 4, 5, 5], 'Пушка Танаки', 'spike', '#ff5a3c', '#ffd0a0', 'Я — гроза площадки!'),
    p('tsukishima', 'Цукишима', 'karasuno', 11, 'MB', '#f2d98a', '#c9ad55', '#f6d8bc', '#d8a82a', 'wavy', 'glasses', 1.12, [5, 7, 5, 4, 9, 5, 5], 'Читающий блок', 'block', '#8fd3ff', '#ffffff', 'Так себе. Но ладно.', 0.8),
    p('asahi', 'Асахи', 'karasuno', 3, 'WS', '#6a4428', '#4a2c18', SKT, '#5a3a22', 'long_bun', 'stubble', 1.08, [9, 6, 4, 2, 6, 6, 4], 'Удар аса', 'spike', '#ff7a1a', '#ffe0a0', 'Я — ас. Я не отступлю!')
  ],
  aoba: [
    p('oikawa', 'Ойкава', 'aoba', 1, 'S', '#6a4a30', '#8a6a48', SK, '#7a5a3a', 'fluffy', null, 1.06, [6, 6, 6, 9, 6, 10, 6], 'Убийственная подача', 'serve', '#3fd0c4', '#ffffff', 'Подача — моё оружие!'),
    p('iwaizumi', 'Ивайзуми', 'aoba', 4, 'WS', '#1c1a20', '#34303a', SKT, '#3a3a2a', 'spiky', null, 1.04, [8, 6, 6, 3, 6, 6, 7], 'Удар Ивачана', 'spike', '#2fb5a8', '#d0fff8', 'Соберись, Ойкава!', 0.7),
    p('kyotani', 'Кётани', 'aoba', 16, 'WS', '#e8c94a', '#1a1418', SK, '#6a3a2a', 'buzz', 'stripes', 1.03, [8, 6, 4, 2, 4, 6, 6], 'Бешеный пёс', 'spike', '#ff3a5a', '#2a1030', 'Заткнись и дай мне мяч!'),
    p('matsukawa', 'Мацукава', 'aoba', 2, 'MB', '#2a2024', '#3c3036', SK, '#3a2a22', 'short', null, 1.1, [6, 6, 5, 3, 8, 5, 5], 'Блок-стена', 'block', '#2fb5a8', '#e0fffa', 'Ага, поймал.'),
    p('hanamaki', 'Ханамаки', 'aoba', 3, 'WS', '#e9a3ae', '#cb7f8c', SK, '#8a5a4a', 'wavy', null, 1.06, [7, 6, 5, 3, 6, 5, 6], 'Хитрая финта', 'spike', '#ff9ec0', '#fff0f6', 'Ну давай, удиви.'),
    p('watari', 'Ватари', 'aoba', 7, 'L', '#6a4a2a', '#8a6a4a', SK, '#5a3a22', 'short', null, 0.96, [2, 4, 8, 4, 2, 4, 6], 'Страховка', 'dig', '#2fb5a8', '#d0fff8', 'Я прикрою!')
  ],
  nekoma: [
    p('kuroo', 'Куроо', 'nekoma', 1, 'MB', '#1a1820', '#34303c', SK, '#b8902a', 'bedhead', null, 1.08, [7, 7, 6, 5, 9, 6, 7], 'Блок-ловушка', 'block', '#ff3a3a', '#ffd0d0', 'Блок — это охота!'),
    p('kenma', 'Кенма', 'nekoma', 5, 'S', '#f0d878', '#3a2a22', SKP, '#d8b030', 'bob', 'catEyes,roots', 1.0, [4, 4, 6, 9, 3, 7, 4], 'Расчётливый пас', 'set', '#ffb52a', '#fff0c0', 'Мне всё равно... но интересно.'),
    p('yaku', 'Яку', 'nekoma', 3, 'L', '#7a5a3a', '#9a7a58', SK, '#5a3a22', 'short', null, 0.93, [2, 4, 9, 4, 2, 4, 7], 'Вездесущий приём', 'dig', '#ff4a4a', '#ffe0d0', 'Мяч не упадёт.', 0.9),
    p('lev', 'Льев', 'nekoma', 11, 'MB', '#d8dce8', '#9aa0b4', SKP, '#3ec878', 'short', null, 1.12, [7, 8, 2, 2, 6, 7, 6], 'Ракета Льва', 'spike', '#6fe08a', '#e8fff0', 'Я стану асом!', 1.5),
    p('yamamoto', 'Ямамото', 'nekoma', 4, 'WS', '#1c1620', '#2a2230', SKT, '#4a3222', 'mohawk', null, 1.0, [7, 6, 5, 2, 4, 7, 6], 'Паровой каток', 'spike', '#ff5030', '#ffd0b0', 'Покажу вам силу нападающего!'),
    p('kai', 'Кай', 'nekoma', 6, 'WS', '#2a2420', '#40382e', SK, '#4a3222', 'short', null, 1.0, [5, 5, 7, 4, 4, 5, 6], 'Стабильный приём', 'dig', '#ff4a4a', '#ffe0d0', 'Спокойно, всё под контролем.')
  ],
  shiratorizawa: [
    p('ushijima', 'Ушиджима', 'shiratorizawa', 1, 'OP', '#6a6638', '#4a4624', SK, '#6a6030', 'short', 'stoic', 1.1, [10, 8, 6, 3, 7, 9, 6], 'Левая пушка', 'spike', '#a05bff', '#f0e0ff', 'Сила решает всё.', 0.8),
    p('tendo', 'Тендо', 'shiratorizawa', 5, 'MB', '#e0402a', '#b02a1a', SKP, '#8a3a2a', 'spiky', 'wideEyes', 1.08, [7, 7, 5, 4, 8, 5, 7], 'Блок-угадайка', 'block', '#ff3a30', '#ffd0c8', 'Я вижу твой следующий ход!', 1.3),
    p('shirabu', 'Сирабу', 'shiratorizawa', 10, 'S', '#6a4a38', '#8a6a58', SK, '#3a3a2a', 'bob', null, 1.0, [5, 5, 6, 8, 4, 7, 5], 'Точный пас', 'set', '#a05bff', '#efe0ff', 'Не расслабляйся.'),
    p('goshiki', 'Гошики', 'shiratorizawa', 8, 'WS', '#7a3a28', '#5a2a1c', SK, '#2a3a5a', 'bowl', null, 1.0, [7, 6, 4, 2, 4, 6, 6], 'Бросок Гошики', 'spike', '#c04bff', '#f6e0ff', 'Я стану асом!'),
    p('ohira', 'Охира', 'shiratorizawa', 4, 'WS', '#2a2a30', '#40404a', SK, '#3a2a22', 'short', null, 1.04, [6, 5, 6, 3, 5, 5, 5], 'Тихий удар', 'spike', '#a05bff', '#efe0ff', 'Не дам упасть мячу.'),
    p('yamagata', 'Ямагата', 'shiratorizawa', 14, 'L', '#3a2a22', '#54423a', SK, '#4a3222', 'short', null, 0.96, [2, 4, 8, 4, 2, 4, 6], 'Плотный приём', 'dig', '#a05bff', '#efe0ff', 'Принял!')
  ],
  fukurodani: [
    p('bokuto', 'Бокуто', 'fukurodani', 4, 'WS', '#d8dce0', '#1a1820', SKT, '#f2c230', 'owl', null, 1.06, [9, 8, 5, 3, 6, 7, 7], 'Удар по линии!!', 'spike', '#ffd23a', '#ffffff', 'Хей-хей-хей!'),
    p('akaashi', 'Акааши', 'fukurodani', 5, 'S', '#2a2a38', '#46465a', SK, '#2a3a5a', 'wavy', null, 1.04, [5, 5, 6, 9, 5, 6, 6], 'Точный пас Акааши', 'set', '#e8b923', '#fff4c0', 'Бокуто-сан, соберитесь.'),
    p('konoha', 'Коноха', 'fukurodani', 7, 'WS', '#3a2a28', '#54423e', SK, '#4a3222', 'short', null, 1.02, [7, 6, 6, 3, 5, 5, 6], 'Пушка Коноха', 'spike', '#e8b923', '#fff4c0', 'Спокойно, я здесь.'),
    p('sarukui', 'Сарукуи', 'fukurodani', 3, 'WS', '#2a2a20', '#40402e', SKT, '#4a3a22', 'spiky', null, 1.06, [6, 6, 5, 3, 7, 5, 5], 'Крепкий блок', 'block', '#e8b923', '#fff4c0', 'Держу линию!', 0.5),
    p('washio', 'Вашио', 'fukurodani', 2, 'MB', '#201a20', '#382e38', SK, '#3a2a22', 'short', null, 1.1, [6, 6, 5, 3, 8, 5, 5], 'Стена Вашио', 'block', '#e8b923', '#fff4c0', 'Хм.'),
    p('komi', 'Коми', 'fukurodani', 19, 'L', '#3a3028', '#54463c', SK, '#4a3222', 'short', null, 0.97, [2, 4, 9, 4, 2, 4, 6], 'Вечный приём', 'dig', '#e8b923', '#fff4c0', 'Принял!')
  ]
};

var TEAM_BY_ID = {};
for (var ti = 0; ti < TEAMS.length; ti++) { TEAMS[ti].players = PL[TEAMS[ti].id]; TEAM_BY_ID[TEAMS[ti].id] = TEAMS[ti]; }
window.VB_TEAMS = TEAMS;

/* ======================= HELPERS ======================= */

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
function luma(hex) {
  var h = hex.replace('#', ''); if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
  var n = parseInt(h, 16); return ((n >> 16 & 255) * 0.3 + (n >> 8 & 255) * 0.59 + (n & 255) * 0.11) / 255;
}
function hasX(ch, t) { return !!(ch.extra && ch.extra.indexOf(t) >= 0); }
function outline(ctx, lw) { ctx.lineWidth = lw; ctx.strokeStyle = OL; ctx.lineJoin = 'round'; ctx.stroke(); }
function fillOut(ctx, fill, lw) { ctx.fillStyle = fill; ctx.fill(); outline(ctx, lw); }
function rr(g, x, y, w, h, r) {
  g.moveTo(x + r, y); g.lineTo(x + w - r, y); g.quadraticCurveTo(x + w, y, x + w, y + r);
  g.lineTo(x + w, y + h - r); g.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  g.lineTo(x + r, y + h); g.quadraticCurveTo(x, y + h, x, y + h - r);
  g.lineTo(x, y + r); g.quadraticCurveTo(x, y, x + r, y);
}
function ell(ctx, x, y, rx, ry, fill, lw) {
  ctx.beginPath(); ctx.ellipse(x, y, rx, ry, 0, 0, PI * 2);
  ctx.fillStyle = fill; ctx.fill(); if (lw) outline(ctx, lw);
}
function dirv(a, R) { a = a * PI / 180; return [Math.sin(a) * R, -Math.cos(a) * R]; }

/* two-bone joint: returns joint position. out = +1/-1 preferred x side of bend */
function bone(ax, ay, bx, by, L, out) {
  var dx = bx - ax, dy = by - ay, d = Math.sqrt(dx * dx + dy * dy) || 0.01;
  var l = Math.max(L, d * 0.5 * 1.002), h = Math.sqrt(Math.max(0, l * l - d * d / 4));
  var nx = -dy / d, ny = dx / d;
  if (nx * out < 0) { nx = -nx; ny = -ny; }
  return [(ax + bx) / 2 + nx * h, (ay + by) / 2 + ny * h];
}

/* ======================= HAIR ======================= */

var Z = [];
var HS = {
  spiky: { sp: [[-86, .3, -14, 9], [-64, .5, -12, 10], [-40, .62, -8, 10], [-16, .55, -4, 9], [8, .78, 3, 10], [32, .62, 8, 10], [56, .5, 12, 10], [80, .32, 16, 9]],
    fr: [[.74, -.52], [.56, -.2], [.32, -.58], [.14, -.14], [-.08, -.58], [-.3, -.2], [-.52, -.58], [-.72, -.22]], sd: -.02, nape: .52 },
  straight: { sm: 1, dome: 1.06, sp: [[-70, .1, 0, 18], [-35, .14, 0, 18], [0, .12, 0, 18], [35, .14, 0, 18], [70, .1, 0, 18]],
    fr: [[.78, -.1], [.5, -.3], [.2, -.5], [-.05, -.38], [-.4, -.18], [-.75, -.14]], sd: .45, nape: .9 },
  fluffy: { sm: 1, dome: 1.08, sp: [[-86, .3, -4, 12], [-64, .34, -3, 12], [-42, .3, -2, 12], [-20, .36, 0, 12], [2, .32, 2, 12], [24, .36, 4, 12], [46, .3, 6, 12], [68, .34, 8, 12], [88, .28, 8, 11]],
    fr: [[.84, -.12], [.6, -.4], [.3, -.28], [.05, -.5], [-.25, -.3], [-.55, -.46], [-.82, -.2]], sd: .25, nape: .72 },
  bedhead: { sp: [[-84, .4, -30, 9], [-62, .65, -22, 10], [-38, .8, -10, 10], [-14, 1.0, 4, 10], [10, .9, 12, 10], [34, .75, 22, 10], [58, .55, 28, 10], [80, .35, 30, 9]],
    fr: [[.7, -.5], [.5, -.15], [.25, -.55], [.0, -.2], [-.25, -.5], [-.5, -.15], [-.75, -.5]], sd: 0, nape: .55 },
  buzz: { dome: 1.02, sp: Z, fr: [[.82, -.4], [.45, -.6], [0, -.64], [-.45, -.6], [-.82, -.4]], sd: -.1, nape: .45 },
  bob: { sm: 1, dome: 1.08, sp: [[-70, .12, 0, 20], [-30, .14, 0, 20], [10, .16, 0, 20], [50, .12, 0, 20]],
    fr: [[.8, -.05], [.5, -.2], [.15, -.3], [-.2, -.22], [-.55, -.1]], sd: .85, nape: .95 },
  bowl: { dome: 1.1, sp: Z, fr: [[.92, -.2], [.6, -.3], [0, -.34], [-.6, -.3], [-.92, -.2]], sd: .3, nape: .55 },
  long_bun: { sm: 1, dome: 1.06, sp: [[-60, .08, 0, 22], [0, .08, 0, 22], [60, .08, 0, 22]],
    fr: [[.9, -.1], [.5, -.38], [.12, -.52], [-.12, -.52], [-.5, -.38], [-.9, -.1]], sd: 1.0, nape: 1.3, bun: 1 },
  wavy: { sm: 1, sp: [[-76, .22, -6, 14], [-48, .3, -5, 14], [-20, .28, -3, 13], [8, .34, 4, 13], [36, .3, 6, 14], [62, .26, 8, 14], [84, .2, 8, 12]],
    fr: [[.9, -.18], [.65, -.44], [.38, -.24], [.1, -.5], [-.18, -.28], [-.45, -.46], [-.72, -.24]], sd: .2, nape: .6 },
  owl: { sp: [[-82, .3, -10, 9], [-62, .5, -8, 10], [-40, 1.0, -18, 11], [-18, .55, -5, 9], [4, .85, 0, 10], [24, .55, 6, 9], [44, 1.0, 18, 11], [64, .5, 10, 10], [84, .3, 10, 9]],
    fr: [[.7, -.5], [.5, -.22], [.25, -.55], [0, -.2], [-.25, -.55], [-.5, -.22], [-.7, -.5]], sd: 0, nape: .5 },
  short: { sm: 1, sp: [[-70, .14, -4, 16], [-40, .2, -3, 14], [-10, .2, 0, 14], [20, .22, 3, 14], [50, .2, 4, 14], [78, .12, 6, 14]],
    fr: [[.88, -.3], [.55, -.46], [.2, -.38], [-.15, -.5], [-.5, -.4], [-.88, -.3]], sd: .05, nape: .5 },
  mohawk: { dome: 1.02, sp: Z, fr: [[.82, -.4], [.45, -.6], [0, -.64], [-.45, -.6], [-.82, -.4]], sd: -.1, nape: .45,
    ridge: [[-14, .85, -3, 8], [0, 1.1, 0, 8], [14, .85, 3, 8]] }
};

function domeSpikes(g, cx, cy, r, sp, R, sm, k) {
  var prev = (-102 - 90) * PI / 180, i, s, a0, a1, tp, c;
  for (i = 0; i < sp.length; i++) {
    s = sp[i]; a0 = (s[0] - s[3] - 90) * PI / 180; a1 = (s[0] + s[3] - 90) * PI / 180;
    if (a0 > prev) g.arc(cx, cy, R, prev, a0);
    tp = dirv(s[0] + s[2], r * (1 + s[1] * k * (sm ? 1.8 : 1)));
    if (sm) g.quadraticCurveTo(cx + tp[0], cy + tp[1], cx + Math.cos(a1) * R, cy + Math.sin(a1) * R);
    else { g.lineTo(cx + tp[0], cy + tp[1]); g.lineTo(cx + Math.cos(a1) * R, cy + Math.sin(a1) * R); }
    prev = Math.max(prev, a1);
  }
  g.arc(cx, cy, R, prev, (102 - 90) * PI / 180);
}

function fringe(g, pts, cx, cy, r, sm) {
  var i, n = pts.length, a, b;
  if (!sm) { for (i = 0; i < n; i++) g.lineTo(cx + pts[i][0] * r, cy + pts[i][1] * r); return; }
  g.lineTo(cx + pts[0][0] * r, cy + pts[0][1] * r);
  for (i = 1; i < n - 1; i++) {
    a = pts[i]; b = pts[i + 1];
    g.quadraticCurveTo(cx + a[0] * r, cy + a[1] * r, cx + (a[0] + b[0]) / 2 * r, cy + (a[1] + b[1]) / 2 * r);
  }
  g.lineTo(cx + pts[n - 1][0] * r, cy + pts[n - 1][1] * r);
}

function hairPath(g, hs, cx, cy, r, front, k, sp) {
  var R = r * (hs.dome || 1.04), st = (-102 - 90) * PI / 180;
  g.moveTo(cx + Math.cos(st) * R, cy + Math.sin(st) * R);
  domeSpikes(g, cx, cy, r, sp, R, hs.sm, k);
  if (front) {
    g.lineTo(cx + r * .98, cy + hs.sd * r);
    fringe(g, hs.fr, cx, cy, r, hs.sm);
    g.lineTo(cx - r * .98, cy + hs.sd * r);
  } else {
    var nb = hs.nape;
    g.lineTo(cx + r * .98, cy + .35 * r);
    g.quadraticCurveTo(cx + r * .92, cy + nb * r, cx, cy + nb * r);
    g.quadraticCurveTo(cx - r * .92, cy + nb * r, cx - r * .98, cy + .35 * r);
  }
  g.closePath();
}

function hairDecor(ctx, ch, st, cx, cy, r, front, hc) {
  var h2 = ch.colors.hair2, i, a;
  ctx.fillStyle = shade(hc, -0.2);
  ctx.beginPath(); ctx.ellipse(cx - r * (front ? 1.15 : 0.9), cy, r * 0.8, r * 1.5, 0, 0, PI * 2); ctx.fill();
  if (hasX(ch, 'roots')) {
    ctx.fillStyle = h2; ctx.beginPath(); ctx.moveTo(cx - r * 1.3, cy - r * 1.6);
    ctx.lineTo(cx + r * 1.3, cy - r * 1.6);
    var by = front ? -0.62 : -0.1;
    for (i = 0; i <= 8; i++) ctx.lineTo(cx + r * (1.2 - i * 0.3), cy + r * (by - (i % 2 ? 0.2 : 0)));
    ctx.closePath(); ctx.fill();
  }
  if (hasX(ch, 'streak')) {
    ctx.fillStyle = h2; ctx.beginPath();
    ctx.moveTo(cx - r * 0.5, cy - r * 1.6); ctx.lineTo(cx - r * 0.05, cy - r * 1.6);
    ctx.lineTo(cx + (front ? 0.02 : 0.1) * r, cy + (front ? -0.1 : -0.7) * r); ctx.lineTo(cx - r * 0.3, cy + (front ? -0.45 : -0.7) * r);
    ctx.closePath(); ctx.fill();
  }
  if (hasX(ch, 'stripes')) {
    ctx.strokeStyle = h2; ctx.lineWidth = r * 0.17; ctx.lineCap = 'butt';
    for (i = -2; i <= 2; i++) {
      ctx.beginPath(); ctx.moveTo(cx + i * r * 0.36 - r * 0.2, cy - r * 1.2); ctx.lineTo(cx + i * r * 0.36 + r * 0.1, cy + (front ? -0.15 : 0.3) * r); ctx.stroke();
    }
    ctx.lineCap = 'round';
  }
  if (st === 'owl') {
    ctx.strokeStyle = h2; ctx.lineWidth = r * 0.2; ctx.lineCap = 'butt';
    for (i = -1; i <= 1; i += 2) for (a = 0; a < 2; a++) {
      ctx.beginPath(); ctx.moveTo(cx + i * r * (0.2 + a * 0.28), cy - r * 0.95);
      ctx.lineTo(cx + i * r * (0.55 + a * 0.45), cy - r * 1.75); ctx.stroke();
    }
    ctx.lineCap = 'round';
  }
  if (!front && st !== 'buzz') {
    ctx.strokeStyle = shade(hc, -0.28); ctx.lineWidth = r * 0.07; ctx.lineCap = 'round';
    for (i = -2; i <= 2; i++) {
      ctx.beginPath(); ctx.moveTo(cx + i * r * 0.12, cy - r * 0.95); ctx.quadraticCurveTo(cx + i * r * 0.5, cy - r * 0.2, cx + i * r * 0.6, cy + r * 0.45); ctx.stroke();
    }
  }
  ctx.beginPath(); ctx.arc(cx, cy + r * 0.02, r * 0.8, PI * 1.14, PI * 1.46);
  ctx.lineWidth = r * 0.13; ctx.lineCap = 'round'; ctx.strokeStyle = shade(hc, 0.4); ctx.globalAlpha = 0.65; ctx.stroke(); ctx.globalAlpha = 1;
}

function drawHair(ctx, ch, cx, cy, r, front, lw) {
  var st = ch.hairStyle, hs = HS[st] || HS.short, hc = ch.colors.hair, k = ch.hairK || 1;
  var g, ridge = hs.ridge;
  if (st === 'long_bun' && front) { ctx.beginPath(); ctx.arc(cx, cy - r * 1.18, r * 0.4, 0, PI * 2); fillOut(ctx, hc, lw); }
  g = new Path2D(); hairPath(g, hs, cx, cy, r, front, k, hs.sp);
  ctx.fillStyle = (st === 'mohawk') ? shade(hc, -0.5) : hc; ctx.fill(g);
  ctx.save(); ctx.clip(g);
  if (st === 'mohawk') { ctx.fillStyle = shade(hc, -0.2); ctx.fillRect(cx - r * 1.3, cy - r * 0.4, r * 2.6, r * 0.3); }
  else hairDecor(ctx, ch, st, cx, cy, r, front, hc);
  ctx.restore();
  ctx.lineJoin = 'round'; ctx.lineWidth = lw; ctx.strokeStyle = OL; ctx.stroke(g);
  if (ridge) {
    var rg = new Path2D(), a0 = (ridge[0][0] - ridge[0][3] - 90) * PI / 180;
    var i, s, tp, a1;
    rg.moveTo(cx + Math.cos(a0) * r * 1.04, cy + Math.sin(a0) * r * 1.04);
    for (i = 0; i < ridge.length; i++) {
      s = ridge[i]; tp = dirv(s[0] + s[2], r * (1 + s[1] * k)); a1 = (s[0] + s[3] - 90) * PI / 180;
      rg.lineTo(cx + tp[0], cy + tp[1]); rg.lineTo(cx + Math.cos(a1) * r * 1.04, cy + Math.sin(a1) * r * 1.04);
    }
    if (front) { rg.lineTo(cx + r * 0.2, cy - r * 0.52); rg.lineTo(cx - r * 0.2, cy - r * 0.52); }
    else { rg.lineTo(cx + r * 0.22, cy + r * 0.42); rg.lineTo(cx - r * 0.22, cy + r * 0.42); }
    rg.closePath(); ctx.fillStyle = hc; ctx.fill(rg); ctx.lineWidth = lw; ctx.strokeStyle = OL; ctx.stroke(rg);
  }
  if (st === 'long_bun' && !front) {
    ctx.beginPath(); ctx.arc(cx, cy - r * 1.08, r * 0.42, 0, PI * 2); fillOut(ctx, hc, lw);
    ctx.beginPath(); ctx.moveTo(cx - r * 0.35, cy - r * 0.86); ctx.lineTo(cx + r * 0.35, cy - r * 0.86);
    ctx.lineWidth = lw * 1.6; ctx.strokeStyle = '#e8e0d0'; ctx.stroke();
  }
}

/* ======================= FACE ======================= */

function eye(ctx, x, y, w, h, col, lw, kind, side) {
  var pr = 0.38;
  ctx.save(); ctx.translate(x, y);
  if (kind === 'sharp') { ctx.rotate(-side * 0.16); h *= 0.66; w *= 1.08; }
  else if (kind === 'cat') { h *= 0.82; ctx.rotate(-side * 0.1); }
  else if (kind === 'wide') { h *= 1.12; w *= 1.12; pr = 0.22; }
  else if (kind === 'stoic') { h *= 0.8; }
  else if (kind === 'sad') { h *= 0.7; }
  ctx.beginPath(); ctx.ellipse(0, 0, w, h, 0, 0, PI * 2); ctx.fillStyle = '#fff'; ctx.fill();
  ctx.lineWidth = lw * 0.5; ctx.strokeStyle = OL; ctx.stroke();
  var iw = kind === 'wide' ? w * 0.62 : w * 0.84;
  ctx.beginPath(); ctx.ellipse(0, h * 0.06, iw, h * 0.94, 0, 0, PI * 2); ctx.fillStyle = col; ctx.fill();
  ctx.beginPath(); ctx.ellipse(0, h * 0.06, iw, h * 0.94, 0, PI, PI * 2); ctx.fillStyle = shade(col, -0.4); ctx.fill();
  if (kind === 'cat') { ctx.beginPath(); ctx.ellipse(0, h * 0.06, w * 0.14, h * 0.9, 0, 0, PI * 2); ctx.fillStyle = OL; ctx.fill(); }
  else { ctx.beginPath(); ctx.ellipse(0, h * 0.1, iw * pr / 0.38 * 0.38 * (kind === 'wide' ? 1.1 : 1), h * (kind === 'wide' ? 0.3 : 0.5), 0, 0, PI * 2); ctx.fillStyle = OL; ctx.fill(); }
  ell(ctx, -w * 0.28, -h * 0.3, w * 0.28, w * 0.28, '#fff');
  ell(ctx, w * 0.3, h * 0.4, w * 0.13, w * 0.13, 'rgba(255,255,255,0.85)');
  ctx.beginPath(); ctx.ellipse(0, 0, w, h, 0, PI * 1.02, PI * 1.98);
  ctx.lineWidth = lw * 1.5; ctx.lineCap = 'round'; ctx.strokeStyle = OL; ctx.stroke();
  ctx.restore();
}

function face(ctx, ch, cx, cy, r, mood, lw) {
  var c = ch.colors, ey = cy + r * 0.3, ex = r * 0.42, w = r * 0.23, h = r * 0.31, i, side, kind = 'normal';
  if (hasX(ch, 'catEyes')) kind = 'cat'; else if (hasX(ch, 'sharpEyes')) kind = 'sharp';
  else if (hasX(ch, 'wideEyes')) kind = 'wide'; else if (hasX(ch, 'stoic')) kind = 'stoic';
  if (mood === 'sad') kind = 'sad';
  var brow = shade(c.hair, -0.45), tilt = mood === 'determined' ? r * 0.11 : mood === 'sad' ? -r * 0.1 : 0;
  if (kind === 'sharp') tilt = r * 0.15; else if (kind === 'stoic') tilt = r * 0.03; else if (kind === 'wide') tilt = r * 0.04 + (mood === 'determined' ? r * 0.03 : 0);
  for (i = 0; i < 2; i++) {
    side = i ? 1 : -1;
    eye(ctx, cx + side * ex, ey, w, h, c.eyes, lw, kind, side);
    ctx.beginPath(); ctx.lineCap = 'round'; ctx.lineWidth = lw * (kind === 'stoic' ? 1.5 : 1.15); ctx.strokeStyle = brow;
    var by = ey - h * 1.45;
    ctx.moveTo(cx + side * (ex + w * 1.0), by - tilt * 0.4); ctx.lineTo(cx + side * (ex - w * 0.9), by + tilt); ctx.stroke();
  }
  var my = cy + r * 0.7;
  if (mood === 'open') {
    ctx.beginPath(); ctx.ellipse(cx, my, r * 0.2, r * 0.16, 0, 0, PI); ctx.closePath(); fillOut(ctx, '#7a1830', lw * 0.6);
    ctx.beginPath(); ctx.ellipse(cx, my + r * 0.09, r * 0.1, r * 0.05, 0, 0, PI * 2); ctx.fillStyle = '#e8506a'; ctx.fill();
  } else if (mood === 'sad') {
    ctx.beginPath(); ctx.arc(cx, my + r * 0.12, r * 0.12, PI * 1.15, PI * 1.85);
    ctx.lineWidth = lw * 0.9; ctx.strokeStyle = OL; ctx.stroke();
  } else if (mood === 'determined' || kind === 'stoic' || kind === 'sharp') {
    ctx.beginPath(); ctx.moveTo(cx - r * 0.15, my); ctx.lineTo(cx + r * 0.12, my); ctx.lineTo(cx + r * 0.2, my - r * 0.05);
    ctx.lineWidth = lw * 0.9; ctx.strokeStyle = OL; ctx.stroke();
  } else {
    ctx.beginPath(); ctx.arc(cx, my - r * 0.07, r * 0.13, PI * 0.15, PI * 0.85);
    ctx.lineWidth = lw * 0.9; ctx.strokeStyle = OL; ctx.stroke();
  }
  ctx.fillStyle = 'rgba(255,90,110,0.3)';
  ctx.beginPath(); ctx.ellipse(cx - r * 0.7, cy + r * 0.56, r * 0.12, r * 0.07, 0, 0, PI * 2); ctx.fill();
  ctx.beginPath(); ctx.ellipse(cx + r * 0.7, cy + r * 0.56, r * 0.12, r * 0.07, 0, 0, PI * 2); ctx.fill();
  if (hasX(ch, 'stubble')) {
    ctx.fillStyle = 'rgba(60,40,30,0.55)';
    for (i = 0; i < 12; i++) {
      var a = PI * (0.18 + i * 0.055); ctx.beginPath();
      ctx.arc(cx + Math.cos(a) * r * 0.78, cy + Math.sin(a) * r * 0.78 - r * 0.08, lw * 0.35, 0, PI * 2); ctx.fill();
    }
    ctx.beginPath(); ctx.arc(cx, my + r * 0.2, lw * 0.4, 0, PI * 2); ctx.fill();
  }
  if (hasX(ch, 'glasses')) {
    ctx.lineWidth = lw * 0.75; ctx.strokeStyle = '#2a2a34';
    for (i = 0; i < 2; i++) {
      side = i ? 1 : -1; ctx.beginPath(); rr(ctx, cx + side * ex - w * 1.35, ey - h * 1.2, w * 2.7, h * 2.4, w * 0.6);
      ctx.fillStyle = 'rgba(200,230,255,0.22)'; ctx.fill(); ctx.stroke();
    }
    ctx.beginPath(); ctx.moveTo(cx - ex + w * 1.35, ey - h * 0.3); ctx.lineTo(cx + ex - w * 1.35, ey - h * 0.3); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(cx - ex - w * 1.2, ey - h * 0.9); ctx.lineTo(cx - ex - w * 0.4, ey - h * 0.6);
    ctx.strokeStyle = 'rgba(255,255,255,0.85)'; ctx.lineWidth = lw * 0.6; ctx.stroke();
  }
}

function drawHead(ctx, ch, cx, cy, r, front, mood, lw) {
  var sk = ch.colors.skin, st = ch.hairStyle;
  ell(ctx, cx - r * 0.98, cy + r * 0.22, r * 0.14, r * 0.2, shade(sk, -0.06), lw * 0.7);
  ell(ctx, cx + r * 0.98, cy + r * 0.22, r * 0.14, r * 0.2, sk, lw * 0.7);
  if (st === 'long_bun' && front) { /* back hair strands behind shoulders */ }
  ctx.beginPath(); ctx.ellipse(cx, cy, r, r * 0.96, 0, 0, PI * 2); fillOut(ctx, sk, lw);
  ctx.beginPath(); ctx.arc(cx, cy, r * 0.95, PI * 0.55, PI * 1.05); ctx.fillStyle = shade(sk, -0.09); ctx.fill();
  if (front) face(ctx, ch, cx, cy, r, mood, lw);
  else if (hasX(ch, 'glasses')) {
    ctx.beginPath(); ctx.moveTo(cx - r * 0.97, cy + r * 0.1); ctx.lineTo(cx - r * 0.97, cy + r * 0.25);
    ctx.moveTo(cx + r * 0.97, cy + r * 0.1); ctx.lineTo(cx + r * 0.97, cy + r * 0.25);
    ctx.lineWidth = lw * 0.7; ctx.strokeStyle = '#2a2a34'; ctx.stroke();
  }
  drawHair(ctx, ch, cx, cy, r, front, lw);
}

/* ======================= BODY ======================= */

function poly(ctx, pts, w, col, lw) {
  var i;
  ctx.lineCap = 'round'; ctx.lineJoin = 'round';
  ctx.beginPath(); ctx.moveTo(pts[0], pts[1]); for (i = 2; i < pts.length; i += 2) ctx.lineTo(pts[i], pts[i + 1]);
  ctx.lineWidth = w + lw * 2; ctx.strokeStyle = OL; ctx.stroke();
  ctx.lineWidth = w; ctx.strokeStyle = col; ctx.stroke();
}

function legDraw(ctx, side, hx, hy, fx, fy, skin, sock, shoe, pad, trim, lw) {
  var k = bone(hx, hy, fx, fy, 8.4, side);
  poly(ctx, [hx, hy, k[0], k[1], fx, fy], 5.4, skin, lw);
  var mx = k[0] + (fx - k[0]) * 0.5, my = k[1] + (fy - k[1]) * 0.5;
  ctx.beginPath(); ctx.moveTo(mx, my); ctx.lineTo(fx, fy); ctx.lineWidth = 5.4; ctx.strokeStyle = sock; ctx.lineCap = 'butt'; ctx.stroke();
  ctx.lineCap = 'round';
  ctx.beginPath(); ctx.ellipse(k[0], k[1], 3.5, 3.1, 0, 0, PI * 2); fillOut(ctx, pad, lw * 0.7);
  var sx = fx + side * 0.9, sy = fy + 1.6;
  ctx.beginPath(); ctx.ellipse(sx, sy, 4.9, 2.7, 0, 0, PI * 2); fillOut(ctx, shoe, lw);
  ctx.save(); ctx.beginPath(); ctx.ellipse(sx, sy, 4.9, 2.7, 0, 0, PI * 2); ctx.clip();
  ctx.fillStyle = shade(shoe, -0.28); ctx.fillRect(sx - 6, sy + 0.9, 12, 3);
  ctx.fillStyle = trim; ctx.fillRect(sx + side * 1.4 - 1.3, sy - 2, 2.6, 2.4);
  ctx.restore();
  ctx.beginPath(); ctx.ellipse(sx, sy, 4.9, 2.7, 0, 0, PI * 2); outline(ctx, lw * 0.6);
}

function shortsDraw(ctx, dy, col, trim, lw) {
  var y0 = -19.5 + dy;
  ctx.beginPath(); ctx.moveTo(-7.4, y0); ctx.lineTo(7.4, y0); ctx.lineTo(8.6, y0 + 9.6); ctx.lineTo(1.0, y0 + 9.6); ctx.lineTo(0, y0 + 5.4);
  ctx.lineTo(-1.0, y0 + 9.6); ctx.lineTo(-8.6, y0 + 9.6); ctx.closePath();
  ctx.fillStyle = col; ctx.fill();
  ctx.save(); ctx.clip(); ctx.fillStyle = shade(col, -0.2); ctx.fillRect(-9, y0, 5, 11);
  ctx.strokeStyle = trim; ctx.lineWidth = 1.5; ctx.beginPath();
  ctx.moveTo(-8.2, y0 + 1); ctx.lineTo(-8.8, y0 + 10); ctx.moveTo(8.2, y0 + 1); ctx.lineTo(8.8, y0 + 10); ctx.stroke();
  ctx.restore();
  ctx.beginPath(); ctx.moveTo(-7.4, y0); ctx.lineTo(7.4, y0); ctx.lineTo(8.6, y0 + 9.6); ctx.lineTo(1.0, y0 + 9.6); ctx.lineTo(0, y0 + 5.4);
  ctx.lineTo(-1.0, y0 + 9.6); ctx.lineTo(-8.6, y0 + 9.6); ctx.closePath(); outline(ctx, lw);
}

function torsoDraw(ctx, front, shirt, trim, numCol, number, lw) {
  ctx.beginPath(); rr(ctx, -7.6, -34.5, 15.2, 19.5, 4.2); ctx.fillStyle = shirt; ctx.fill();
  ctx.save(); ctx.clip();
  ctx.fillStyle = shade(shirt, -0.16); ctx.fillRect(-8, -35, 5, 21);
  ctx.fillStyle = trim; ctx.fillRect(-8, -17.6, 16, 3); ctx.fillRect(-7.2, -34, 1.3, 17); ctx.fillRect(5.9, -34, 1.3, 17);
  ctx.restore();
  ctx.beginPath(); rr(ctx, -7.6, -34.5, 15.2, 19.5, 4.2); outline(ctx, lw);
  if (front) {
    ctx.beginPath(); ctx.moveTo(-3.2, -34.4); ctx.lineTo(0, -29.6); ctx.lineTo(3.2, -34.4); ctx.closePath();
    ctx.fillStyle = '#e8b890'; ctx.fill(); ctx.lineWidth = lw * 0.8; ctx.strokeStyle = trim; ctx.stroke();
  } else {
    ctx.beginPath(); ctx.moveTo(-3.6, -34.3); ctx.quadraticCurveTo(0, -32, 3.6, -34.3); ctx.lineWidth = lw * 1.1; ctx.strokeStyle = trim; ctx.stroke();
  }
  if (number != null) {
    var s = String(number), big = !front, fs = big ? (s.length > 1 ? 10 : 12.5) : (s.length > 1 ? 5.4 : 6.4);
    ctx.font = 'bold ' + fs + 'px Arial, Helvetica, sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    var ty = big ? -24.2 : -25.6;
    ctx.lineJoin = 'round'; ctx.lineWidth = big ? 2.4 : 1.6; ctx.strokeStyle = luma(numCol) > 0.6 && luma(shirt) > 0.6 ? OL : OL;
    ctx.strokeText(s, 0, ty); ctx.fillStyle = numCol; ctx.fillText(s, 0, ty);
  }
}

function armDraw(ctx, side, sx, sy, hx, hy, L, skin, shirt, trim, fist, fing, lw) {
  var k = bone(sx, sy, hx, hy, L, side);
  poly(ctx, [sx, sy, k[0], k[1], hx, hy], 4.3, skin, lw);
  var dx = k[0] - sx, dy = k[1] - sy, d = Math.sqrt(dx * dx + dy * dy) || 1, ux = dx / d, uy = dy / d;
  ctx.beginPath(); ctx.moveTo(sx - ux * 0.5, sy - uy * 0.5); ctx.lineTo(sx + ux * 4.4, sy + uy * 4.4);
  ctx.lineWidth = 6.6; ctx.strokeStyle = OL; ctx.stroke();
  ctx.lineWidth = 4.6 + 1.4; ctx.strokeStyle = shirt; ctx.stroke();
  ctx.beginPath(); ctx.moveTo(sx + ux * 3.4, sy + uy * 3.4); ctx.lineTo(sx + ux * 4.4, sy + uy * 4.4);
  ctx.lineWidth = 6.0; ctx.lineCap = 'butt'; ctx.strokeStyle = trim; ctx.stroke(); ctx.lineCap = 'round';
  if (fing) {
    var ex = hx - k[0], ey = hy - k[1], el = Math.sqrt(ex * ex + ey * ey) || 1, ang = Math.atan2(ey, ex), j, a;
    for (j = -2; j <= 2; j++) {
      a = ang + j * 0.34; if (j === 0 && false) continue;
      ctx.beginPath(); ctx.moveTo(hx, hy); ctx.lineTo(hx + Math.cos(a) * 4.4, hy + Math.sin(a) * 4.4);
      ctx.lineWidth = 3.6; ctx.strokeStyle = OL; ctx.stroke(); ctx.lineWidth = 1.8; ctx.strokeStyle = skin; ctx.stroke();
    }
  }
  ctx.beginPath(); ctx.arc(hx, hy, fist ? 3.3 : 3, 0, PI * 2); fillOut(ctx, skin, lw * 0.8);
}

/* ======================= POSES ======================= */

function getPose(name, t, ph, dir, moving) {
  var P = { dy: 0, lean: 0, rot: 0, px: 0, hd: 0, hr: 0, mood: 'normal', fL: [-3.8, -2.8], fR: [3.8, -2.8],
    hL: [-10.5, -17], hR: [10.5, -17], L: 8, armF: false, fist: false, fing: false, behind: 0, fx: 0 };
  var s = Math.sin(ph), c = Math.cos(ph), d = dir;
  function feet(a, b, y) { P.fL = [-a, y === undefined ? -2.8 : y]; P.fR = [b === undefined ? a : b, y === undefined ? -2.8 : y]; }
  switch (name) {
    case 'ready':
      P.dy = 5 + Math.sin(t * 4) * 0.4; feet(6.4); P.hL = [-4.8, -20]; P.hR = [4.8, -20]; P.hd = 1.5; P.mood = 'determined'; P.L = 8; break;
    case 'run':
      P.dy = -Math.abs(c) * 1.2 + 1; P.fR = [4.2, -2.8 - Math.max(0, s) * 6]; P.fL = [-4.2, -2.8 - Math.max(0, -s) * 6];
      P.hR = [10.5, -17 - s * 5]; P.hL = [-10.5, -17 + s * 5]; P.lean = d * 0.09; P.hd = 1; break;
    case 'bump':
      P.dy = 5; feet(6.4); P.hL = [-0.9, -19]; P.hR = [0.9, -19]; P.fist = true; P.hd = 2; P.mood = 'determined'; P.L = 7.5; break;
    case 'set':
      P.dy = 2; feet(5); P.hL = [-5.4, -60, 15.5]; P.hR = [5.4, -60, 15.5]; P.fing = true; P.armF = true; P.hd = -0.5; P.mood = 'determined'; break;
    case 'jump':
      feet(3.4, 3.4, -6.5); P.hL = [-13, -52, 12]; P.hR = [13, -52, 12]; P.armF = true; P.mood = 'open'; break;
    case 'spikeWind':
      feet(4, 4.4, -6); P.fL = [-4.5, -7]; P.fR = [4.5, -5];
      if (d > 0) { P.hR = [5, -58, 15.5]; P.hL = [-11, -64, 5]; } else { P.hL = [-5, -58, 15.5]; P.hR = [11, -64, 5]; }
      P.lean = -d * 0.1; P.behind = d > 0 ? 1 : -1; P.fing = true; P.armF = true; P.mood = 'determined'; P.hr = -d * 0.05; break;
    case 'spike':
      feet(4.5, 4.5, -5); P.fL = [-4.5, -4]; P.fR = [4.5, -6.5];
      if (d > 0) { P.hR = [4, -24, 12]; P.hL = [-10, -25, 8]; } else { P.hL = [-4, -24, 12]; P.hR = [10, -25, 8]; }
      P.lean = d * 0.17; P.armF = true; P.mood = 'determined'; P.fist = true; P.fx = 1; P.hd = 1; break;
    case 'block':
      P.dy = 2; feet(4.2); P.hL = [-9.5, -64, 16]; P.hR = [9.5, -64, 16]; P.fing = true; P.armF = true; P.mood = 'determined'; break;
    case 'dive':
      P.rot = d * 1.5; P.px = -d * 12; P.fL = [-3.2, -1]; P.fR = [3.8, -3.5];
      P.hL = [-8, -64, 16]; P.hR = [8, -64, 16]; P.fing = true; P.armF = true; P.mood = 'open'; break;
    case 'serve':
      feet(5.6); P.lean = -d * 0.06;
      if (d > 0) { P.hR = [9, -66, 16]; P.hL = [-10, -24, 8]; } else { P.hL = [-9, -66, 16]; P.hR = [10, -24, 8]; }
      P.armF = true; P.fing = true; P.mood = 'determined'; break;
    case 'celebrate':
      var j = Math.abs(Math.sin(t * 9)); P.dy = -j * 3; feet(4.6);
      P.hL = [-13 - Math.sin(t * 14), -58 + Math.sin(t * 12), 12]; P.hR = [13 + Math.sin(t * 14), -58 - Math.sin(t * 12), 12];
      P.fist = true; P.mood = 'open'; P.armF = true; break;
    case 'sad':
      P.dy = 2; P.hd = 4.5; P.hr = 0.07; P.hL = [-9, -15, 8]; P.hR = [9, -15, 8]; P.mood = 'sad'; break;
    default:
      P.dy = Math.sin(t * 3) * 0.5; P.hL = [-10.5, -17 + Math.sin(t * 3 + 1) * 0.6]; P.hR = [10.5, -17 + Math.sin(t * 3) * 0.6];
  }
  return P;
}

/* ======================= AURA ======================= */

function aura(ctx, ch, t) {
  var col = ch.special.color, col2 = ch.special.color2, i;
  var g = ctx.createRadialGradient(0, -32, 4, 0, -32, 44);
  g.addColorStop(0, col2); g.addColorStop(0.5, col); g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = 0.4;
  ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, -32, 30, 44, 0, 0, PI * 2); ctx.fill();
  ctx.globalAlpha = 0.65;
  for (i = 0; i < 9; i++) {
    var bx = -20 + i * 5, fl = Math.sin(t * 14 + i * 1.9), H = 40 + (i % 3) * 12 + fl * 8, W = 5 + (i % 2) * 2;
    var cxx = bx + Math.sin(t * 9 + i) * 3;
    ctx.fillStyle = i % 2 ? col : col2;
    ctx.beginPath(); ctx.moveTo(bx - W, 0);
    ctx.quadraticCurveTo(bx - W * 1.3, -H * 0.5, cxx, -H);
    ctx.quadraticCurveTo(bx + W * 1.3, -H * 0.5, bx + W, 0); ctx.closePath(); ctx.fill();
  }
  ctx.restore();
}

/* ======================= PUBLIC: drawVB ======================= */

window.drawVB = function (ctx, x, y, ch, o) {
  o = o || {};
  var S = (o.scale || 1) * (ch.h || 1), front = o.view === 'front', lw = 2;
  var t = Date.now() / 1000, dir = o.dir < 0 ? -1 : 1;
  var name = o.pose || (o.moving ? 'run' : 'idle');
  var P = getPose(name, t, o.runPhase || 0, dir, o.moving);
  var tm = TEAM_BY_ID[ch.team] || TEAMS[0], tc = tm.colors, lib = ch.role === 'L';
  var shirt = o.shirt || (lib ? tc.liberoShirt : tc.shirt), trim = o.trim || tc.trim, shorts = o.shorts || tc.shorts;
  var numCol = o.numColor || (lib ? tc.liberoNum : tc.num), number = o.number != null ? o.number : ch.num;
  var skin = ch.colors.skin, skinL = shade(skin, -0.1), pad = luma(shorts) > 0.5 ? '#2a2a32' : '#f4f4f8';
  var sock = '#f1f1f4', shoe = '#f6f6f8', dy = P.dy, hipY = -16 + dy;

  ctx.save(); ctx.translate(x, y); ctx.scale(S, S);
  if (o.aura) aura(ctx, ch, t);
  if (P.rot) { ctx.translate(P.px, -9); ctx.rotate(P.rot); ctx.translate(0, 16); }

  legDraw(ctx, -1, -3.8, hipY, P.fL[0], P.fL[1], skinL, sock, shoe, pad, trim, lw);
  legDraw(ctx, 1, 3.8, hipY, P.fR[0], P.fR[1], skin, sock, shoe, pad, trim, lw);
  shortsDraw(ctx, dy, shorts, trim, lw);

  ctx.save();
  ctx.translate(0, dy); ctx.translate(0, -16); ctx.rotate(P.lean); ctx.translate(0, 16);
  var SY = -31.5, SX = 7.4, frontL = P.armF, frontR = P.armF;
  if (front && P.behind) { if (P.behind > 0) frontR = false; else frontL = false; }
  function arms(wantFront) {
    if (frontL === wantFront) armDraw(ctx, -1, -SX, SY, P.hL[0], P.hL[1], P.hL[2] || P.L, skinL, shade(shirt, -0.16), trim, P.fist, P.fing, lw);
    if (frontR === wantFront) armDraw(ctx, 1, SX, SY, P.hR[0], P.hR[1], P.hR[2] || P.L, skin, shirt, trim, P.fist, P.fing, lw);
  }
  arms(false);
  ctx.beginPath(); rr(ctx, -2.8, -41, 5.6, 8, 2); fillOut(ctx, shade(skin, -0.08), lw);
  torsoDraw(ctx, front, shirt, trim, numCol, number, lw);
  ctx.save();
  ctx.translate(0, P.hd); ctx.translate(0, -49); ctx.rotate(P.hr); ctx.translate(0, 49);
  drawHead(ctx, ch, 0, -49, 11.5, front, P.mood, lw);
  ctx.restore();
  arms(true);
  if (P.fx) {
    ctx.beginPath(); ctx.arc(dir * 2, -50, 20, dir > 0 ? -0.2 : PI - 1.3 + 0.0, dir > 0 ? 1.3 : PI + 0.2, false);
    ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(255,255,255,0.75)'; ctx.stroke();
  }
  ctx.restore();
  ctx.restore();
};

/* ======================= PUBLIC: drawVBPortrait ======================= */

window.drawVBPortrait = function (ctx, x, y, size, ch, mood) {
  var tm = TEAM_BY_ID[ch.team] || TEAMS[0], tc = tm.colors, lib = ch.role === 'L', lw = 2.4;
  var shirt = lib ? tc.liberoShirt : tc.shirt, trim = tc.trim, numCol = lib ? tc.liberoNum : tc.num;
  var skin = ch.colors.skin;
  ctx.save(); ctx.translate(x, y); ctx.scale(size / 100, size / 100); ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  // neck
  ctx.beginPath(); rr(ctx, -6, 12, 12, 14, 4); fillOut(ctx, shade(skin, -0.1), lw);
  // shoulders / jersey
  ctx.beginPath(); ctx.moveTo(-50, 50); ctx.lineTo(-50, 38); ctx.quadraticCurveTo(-48, 26, -30, 23);
  ctx.lineTo(-9, 20); ctx.lineTo(9, 20); ctx.lineTo(30, 23); ctx.quadraticCurveTo(48, 26, 50, 38); ctx.lineTo(50, 50); ctx.closePath();
  ctx.fillStyle = shirt; ctx.fill();
  ctx.save(); ctx.clip();
  ctx.fillStyle = shade(shirt, -0.16); ctx.fillRect(-52, 18, 30, 34);
  ctx.fillStyle = trim; ctx.fillRect(-52, 41, 22, 4); ctx.fillRect(30, 41, 22, 4);
  ctx.restore();
  ctx.beginPath(); ctx.moveTo(-50, 50); ctx.lineTo(-50, 38); ctx.quadraticCurveTo(-48, 26, -30, 23);
  ctx.lineTo(-9, 20); ctx.lineTo(9, 20); ctx.lineTo(30, 23); ctx.quadraticCurveTo(48, 26, 50, 38); ctx.lineTo(50, 50); outline(ctx, lw);
  ctx.beginPath(); ctx.moveTo(-10, 20.4); ctx.lineTo(0, 35); ctx.lineTo(10, 20.4); ctx.closePath();
  ctx.fillStyle = shade(skin, -0.1); ctx.fill(); ctx.lineWidth = lw * 1.2; ctx.strokeStyle = trim; ctx.stroke();
  ctx.lineWidth = lw * 0.7; ctx.strokeStyle = OL; ctx.stroke();
  var s = String(ch.num);
  ctx.font = 'bold 15px Arial, Helvetica, sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.lineWidth = 3; ctx.strokeStyle = OL; ctx.strokeText(s, 24, 38); ctx.fillStyle = numCol; ctx.fillText(s, 24, 38);
  // head
  drawHead(ctx, ch, 0, -5, 22, true, mood || 'determined', lw);
  ctx.restore();
};

})();
