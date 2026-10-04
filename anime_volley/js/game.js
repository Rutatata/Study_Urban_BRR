'use strict';
(function () {
  // ===== constants (meters / seconds) =====
  const CW = 9, CL = 18, NY = 9, NH = 2.43, NB = 1.43, R = 0.11, GB = 9.8, GP = 14;
  // formation slots (x across, distance from net): FL, FM, FR(setter), BL, BM(libero), BR
  const SLOTS = [[1.7, 1.8], [4.5, 1.4], [7.3, 1.8], [1.7, 6.2], [4.5, 7.0], [7.3, 6.2]];
  const DIFF = {
    easy:   { acc: 0.55, rec: -0.14, block: 0.55, speed: 0.9,  blockers: 1 },
    normal: { acc: 0.78, rec: 0,     block: 1,    speed: 1,    blockers: 2 },
    hard:   { acc: 0.93, rec: 0.08,  block: 1.35, speed: 1.06, blockers: 2 },
  };
  const MATE = { acc: 0.82, rec: 0.04, block: 1, speed: 1, blockers: 2 };

  const $ = (id) => document.getElementById(id);
  const cv = $('game'), ctx = cv.getContext('2d');
  const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
  const lerp = (a, b, t) => a + (b - a) * t;
  const rnd = (a, b) => a + Math.random() * (b - a);
  const hyp = Math.hypot;

  // ===== canvas & camera =====
  let cw = 0, chh = 0, dpr = 1, F = 900;
  function resize() {
    dpr = Math.min(2, devicePixelRatio || 1);
    cw = innerWidth; chh = innerHeight;
    cv.width = cw * dpr; cv.height = chh * dpr;
    cv.style.width = cw + 'px'; cv.style.height = chh + 'px';
    F = Math.min(chh * 1.2, cw * 0.95);
  }
  addEventListener('resize', resize);
  resize();

  const BASE_CAM = { x: 4.5, y: -7.2, z: 6.4, a: 0.4 };
  const cam = { ...BASE_CAM };
  let ca = Math.cos(cam.a), sa = Math.sin(cam.a), shX = 0, shY = 0;
  const depthOf = (x, y, z) => (y - cam.y) * ca - (z - cam.z) * sa;
  function proj(x, y, z) {
    const dy = y - cam.y, dz = z - cam.z, d = dy * ca - dz * sa;
    if (d < 0.15) return null;
    const s = F / d;
    return [cw / 2 + (x - cam.x) * s + shX, chh * 0.47 - (dy * sa + dz * ca) * s + shY, s, d];
  }
  function toFloor(sx, sy) {
    const rx = (sx - cw / 2 - shX) / F, ry = (chh * 0.47 - sy + shY) / F;
    const dy = ca + ry * sa, dz = ry * ca - sa;
    if (dz > -1e-3) return null;
    const t = -cam.z / dz;
    return { x: cam.x + rx * t, y: cam.y + dy * t };
  }
  function clipPoly(pts) {
    const out = [], near = 0.2;
    for (let i = 0; i < pts.length; i++) {
      const a = pts[i], b = pts[(i + 1) % pts.length];
      const da = depthOf(a[0], a[1], a[2]), db = depthOf(b[0], b[1], b[2]);
      if (da >= near) out.push(a);
      if ((da >= near) !== (db >= near)) {
        const t = (near - da) / (db - da);
        out.push([lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)]);
      }
    }
    return out;
  }
  function poly3(pts, fill, stroke, lw) {
    const c = clipPoly(pts);
    if (c.length < 3) return;
    ctx.beginPath();
    c.forEach((p, i) => { const s = proj(p[0], p[1], p[2]); if (s) (i ? ctx.lineTo(s[0], s[1]) : ctx.moveTo(s[0], s[1])); });
    ctx.closePath();
    if (fill) { ctx.fillStyle = fill; ctx.fill(); }
    if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = lw || 1; ctx.stroke(); }
  }
  function seg3(a, b, stroke, lw) {
    const da = depthOf(a[0], a[1], a[2]), db = depthOf(b[0], b[1], b[2]);
    if (da < 0.2 && db < 0.2) return;
    if (da < 0.2) { const t = (0.2 - da) / (db - da); a = [lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)]; }
    if (db < 0.2) { const t = (0.2 - db) / (da - db); b = [lerp(b[0], a[0], t), lerp(b[1], a[1], t), lerp(b[2], a[2], t)]; }
    const p = proj(a[0], a[1], a[2]), q = proj(b[0], b[1], b[2]);
    if (!p || !q) return;
    ctx.strokeStyle = stroke; ctx.lineWidth = lw; ctx.beginPath(); ctx.moveTo(p[0], p[1]); ctx.lineTo(q[0], q[1]); ctx.stroke();
  }
  function floorLine(x1, y1, x2, y2, w, col) {
    const dx = x2 - x1, dy = y2 - y1, l = hyp(dx, dy) || 1, nx = -dy / l * w / 2, ny = dx / l * w / 2;
    poly3([[x1 + nx, y1 + ny, 0], [x2 + nx, y2 + ny, 0], [x2 - nx, y2 - ny, 0], [x1 - nx, y1 - ny, 0]], col);
  }
  function floorCircle(x, y, r, n = 28) {
    const o = [];
    for (let i = 0; i < n; i++) { const a = i / n * Math.PI * 2; o.push([x + Math.cos(a) * r, y + Math.sin(a) * r, 0.01]); }
    return o;
  }

  // ===== audio =====
  let AC = null;
  function audioInit() {
    if (!AC) { try { AC = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { /* no audio */ } }
    if (AC && AC.state === 'suspended') AC.resume();
  }
  function tone(f, d, type = 'square', v = 0.08, slide = 0) {
    if (!AC) return;
    const t = AC.currentTime, o = AC.createOscillator(), g = AC.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t);
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, f + slide), t + d);
    g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + d);
    o.connect(g).connect(AC.destination); o.start(t); o.stop(t + d);
  }
  function noise(d, v = 0.15, hp = 800) {
    if (!AC) return;
    const n = (AC.sampleRate * d) | 0, buf = AC.createBuffer(1, n, AC.sampleRate), a = buf.getChannelData(0);
    for (let i = 0; i < n; i++) a[i] = (Math.random() * 2 - 1) * (1 - i / n);
    const s = AC.createBufferSource(); s.buffer = buf;
    const f = AC.createBiquadFilter(); f.type = 'highpass'; f.frequency.value = hp;
    const g = AC.createGain(); g.gain.value = v;
    s.connect(f).connect(g).connect(AC.destination); s.start();
  }
  const SFX = {
    bump: () => { noise(0.06, 0.12, 400); tone(260, 0.07, 'sine', 0.15, -80); },
    set: () => { noise(0.04, 0.08, 1200); tone(420, 0.05, 'sine', 0.1); },
    spike: (p) => { noise(0.14, 0.2 + p * 0.2, 200); tone(110, 0.18, 'sawtooth', 0.12, -60); },
    floor: () => { noise(0.2, 0.25, 150); tone(70, 0.2, 'sine', 0.25, -30); },
    net: () => noise(0.25, 0.12, 900),
    block: () => { noise(0.12, 0.25, 300); tone(180, 0.12, 'square', 0.08, -100); },
    whistle: () => tone(2100, 0.35, 'sine', 0.07),
    point: () => { noise(1.4, 0.1, 600); [523, 659, 784].forEach((f, i) => setTimeout(() => tone(f, 0.18, 'square', 0.05), i * 90)); },
    superS: () => { tone(160, 1, 'sawtooth', 0.06, 1000); noise(0.8, 0.12, 1500); },
    lose: () => [392, 330, 262].forEach((f, i) => setTimeout(() => tone(f, 0.2, 'triangle', 0.06), i * 120)),
  };

  // ===== state =====
  const G = {
    mode: 'menu', t: 0, teams: [], players: [], user: null, target: 15, diff: 'normal', oppId: 'aoba',
    modeT: 0, slowT: 0, cine: null, cutin: null, popups: [], particles: [], rings: [], cracks: [], shake: 0,
    serving: 0, prompt: '', flash: 0, menuT: 0, userCharId: 'hinata', log: [],
  };
  const ball = { x: 4.5, y: 4, z: 1, vx: 0, vy: 0, vz: 0, held: null, live: false, lastP: null, lastTeam: null,
    super: null, spin: 0, blockChecked: false, blockTouch: false, kind: '', hitT: 0, power: 0 };

  const crowd = [];
  for (let r = 0; r < 7; r++) for (let i = 0; i < 46; i++) crowd.push({ x: -7 + i * 0.5 + (r % 2) * 0.25, r, c: `hsl(${(Math.random() * 360) | 0},65%,${40 + Math.random() * 25}%)`, ph: Math.random() * 6 });

  // ===== geometry per team =====
  const wy = (team, uy) => (team.i === 0 ? NY - uy : NY + uy);           // distance-from-net -> world y
  const uyOf = (team, y) => (team.i === 0 ? NY - y : y - NY);
  const onSide = (team, y) => (team.i === 0 ? y < NY : y > NY);
  const fwd = (team) => (team.i === 0 ? 1 : -1);                         // direction toward the net
  const other = (team) => G.teams[1 - team.i];
  const inCourt = (x, y, m = 0) => x >= -m && x <= CW + m && y >= -m && y <= CL + m;

  // ===== setup =====
  function teamById(id) { return VB_TEAMS.find((t) => t.id === id); }
  function arrange(list) {
    const left = [...list];
    const take = (f) => { const i = left.findIndex(f); return i >= 0 ? left.splice(i, 1)[0] : null; };
    const S = take((c) => c.role === 'S'), L = take((c) => c.role === 'L'), MB = take((c) => c.role === 'MB');
    const WS = take((c) => c.role === 'WS' || c.role === 'OP');
    const order = [WS, MB, S, null, L, null];
    for (let i = 0; i < 6; i++) if (!order[i]) order[i] = left.shift();
    return order;
  }
  function mkPlayer(ch, team, slot) {
    const s = ch.stats, h = ch.h || 1;
    return { ch, team, slot, x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, dvx: 0, dvy: 0, air: false, sprint: false, run: 0,
      pose: 'idle', poseT: 0, diveT: 0, diveDir: 1, recT: 0, stunT: 0, energy: 0, stam: 100, callT: 0, armed: 0,
      isUser: false, blockJump: false, blockTask: null, celebrate: 0,
      reach: 2.15 * h, jumpH: 0.5 + s.jump * 0.065, spd: 3.6 + s.speed * 0.28 };
  }
  function mkTeam(i, data, userCharId) {
    const t = { i, data, score: 0, touches: 0, plan: null, serverIdx: 0, D: i === 0 ? MATE : DIFF[G.diff] };
    t.players = arrange(data.players).map((c, k) => mkPlayer(c, t, k));
    t.serveOrder = t.players.filter((p) => p.ch.role !== 'L');
    if (userCharId) {
      G.user = t.players.find((p) => p.ch.id === userCharId);
      G.user.isUser = true;
      const k = t.serveOrder.indexOf(G.user);
      t.serverIdx = k >= 0 ? (k + 1) % t.serveOrder.length : 0;
    }
    return t;
  }
  function setupMatch() {
    G.teams = [mkTeam(0, teamById('karasuno'), G.userCharId), mkTeam(1, teamById(G.oppId), null)];
    G.players = [...G.teams[0].players, ...G.teams[1].players];
    G.popups = []; G.particles = []; G.rings = []; G.cracks = []; G.log = [];
    G.serving = Math.random() < 0.5 ? 0 : 1;
    setupServe();
  }
  function setupServe() {
    const st = G.teams[G.serving];
    G.mode = 'serve'; G.modeT = 1.8; G.cine = null; G.slowT = 0;
    for (const t of G.teams) {
      t.touches = 0; t.plan = null;
      for (const p of t.players) {
        const [ux, uy] = SLOTS[p.slot];
        const recv = t !== st && p.slot !== 2 ? 1.3 : 0;
        p.x = ux; p.y = wy(t, uy + recv); p.z = 0; p.vx = p.vy = p.vz = 0; p.air = false;
        p.diveT = p.recT = p.stunT = 0; p.blockTask = null; p.celebrate = 0; p.pose = 'ready';
      }
    }
    const server = st.serveOrder[st.serverIdx % st.serveOrder.length];
    st.server = server;
    server.x = 6.8; server.y = wy(st, 9.9);
    Object.assign(ball, { held: server, live: false, super: null, lastP: null, lastTeam: null, vx: 0, vy: 0, vz: 0, blockTouch: false, kind: 'serve' });
    G.charging = false; G.chargeT = 0;
    G.prompt = server.isUser ? 'ПОДАЧА — зажми ЛКМ и отпусти (мышь — куда). Q — супер-подача' : '';
  }

  // ===== effects =====
  function popup(text, color = '#ffd23f', size = 1, sub = '') {
    G.popups = G.popups.filter((p) => p.size >= 1.5 && size < 1.5);
    G.popups.push({ text, sub, color, size, t: size >= 1.5 ? 2 : 1.1, max: size >= 1.5 ? 2 : 1.1 });
  }
  function addP(x, y, z, vx, vy, vz, life, color, size, type) {
    if (G.particles.length > 700) return;
    G.particles.push({ x, y, z, vx, vy, vz, life, max: life, color, size, type });
  }
  function burst(x, y, z, color, n, sp, type = 'spark') {
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, s = rnd(0.3, 1) * sp;
      addP(x, y, z, Math.cos(a) * s, Math.sin(a) * s, rnd(0.2, 1) * sp, rnd(0.3, 0.9), color, rnd(0.04, 0.09), type);
    }
  }
  function impact(x, y, big, c1, c2) {
    G.rings.push({ x, y, r: 0.1, life: big ? 0.9 : 0.5, max: big ? 0.9 : 0.5, col: c1 || '#fff', speed: big ? 9 : 5 });
    if (big) {
      G.rings.push({ x, y, r: 0.1, life: 0.7, max: 0.7, col: c2 || '#ffd23f', speed: 5 });
      const lines = [];
      for (let i = 0; i < 9; i++) {
        let a = rnd(0, Math.PI * 2), px = x, py = y; const pts = [[px, py]];
        for (let k = 0; k < 4; k++) { a += rnd(-0.5, 0.5); const l = rnd(0.25, 0.6); px += Math.cos(a) * l; py += Math.sin(a) * l; pts.push([px, py]); }
        lines.push(pts);
      }
      G.cracks.push({ lines, life: 6 });
      burst(x, y, 0.1, '#c9a26b', 30, 3.5, 'dust');
      burst(x, y, 0.3, c1 || '#fff', 25, 6);
      G.shake = 22; G.flash = 0.35;
    } else {
      burst(x, y, 0.05, '#e8c08a', 8, 1.5, 'dust');
    }
  }

  // ===== ball physics helpers =====
  function launch(tx, ty, tz, T) {
    ball.vx = (tx - ball.x) / T; ball.vy = (ty - ball.y) / T;
    ball.vz = (tz - ball.z + 0.5 * GB * T * T) / T;
    ball.held = null; ball.live = true; ball.blockChecked = false; ball.hitT = 0;
  }
  function clearsNet(tx, ty, tz, T) {
    const vy = (ty - ball.y) / T;
    if (Math.abs(vy) < 1e-3 || (NY - ball.y) / vy < 0 || (ty - NY) * (ball.y - NY) > 0) return true;
    const tn = (NY - ball.y) / vy;
    if (tn > T) return true;
    const vz = (tz - ball.z + 0.5 * GB * T * T) / T;
    return ball.z + vz * tn - 0.5 * GB * tn * tn > NH + R + 0.12;
  }
  function safeT(tx, ty, tz, T) { let k = 0; while (!clearsNet(tx, ty, tz, T) && k++ < 14) T *= 1.12; return T; }
  // time/position when the ball (no drag) descends through height zt
  function predictZ(zt) {
    const b = ball, disc = b.vz * b.vz + 2 * GB * (b.z - zt);
    if (disc < 0) return { x: b.x, y: b.y, t: 0 };
    const t = Math.max(0, (b.vz + Math.sqrt(disc)) / GB);
    return { x: b.x + b.vx * t, y: b.y + b.vy * t, t };
  }

  // ===== touches / rules =====
  function registerTouch(p, isBlock) {
    const t = p.team;
    if (isBlock) { t.touches = 0; other(t).touches = 0; }
    else if (ball.lastTeam !== t) { t.touches = 1; other(t).touches = 0; }
    else {
      if (ball.lastP === p && !ball.blockTouch) { fault(t, 'ДВОЙНОЕ КАСАНИЕ'); return false; }
      t.touches++;
      if (t.touches > 3) { fault(t, 'ЧЕТЫРЕ КАСАНИЯ'); return false; }
    }
    ball.lastP = p; ball.lastTeam = t; ball.blockTouch = !!isBlock; ball.super = null;
    t.plan = null;
    p.energy = Math.min(100, p.energy + (p.isUser ? 7 : 3));
    return true;
  }
  function fault(team, why) { awardPoint(other(team), why, null); }

  function awardPoint(team, why, hero) {
    if (G.mode !== 'rally') return;
    G.mode = 'point'; G.modeT = 2.6; ball.live = false;
    team.score++;
    G.log.push({ team: team.i, why, who: hero ? hero.ch.name : '' });
    const ours = team.i === 0;
    popup(why, ours ? '#ffd23f' : '#ff5a7a', 1.6, `очко — ${team.data.name}${hero ? ' · ' + hero.ch.name : ''}`);
    ours ? SFX.point() : SFX.lose();
    for (const p of G.players) { p.celebrate = p.team === team ? 1 : -1; p.blockTask = null; }
    if (hero) hero.energy = Math.min(100, hero.energy + 12);
    if (G.serving !== team.i) {
      G.serving = team.i;
      team.serverIdx = (team.serverIdx + 1) % team.serveOrder.length;
    }
  }
  function checkWin() {
    const [a, b] = G.teams;
    if ((a.score >= G.target || b.score >= G.target) && Math.abs(a.score - b.score) >= 2) return true;
    return false;
  }

  // ===== planning (AI brain) =====
  function nearestFree(team, x, y, exclude) {
    let best = null, bt = 1e9;
    for (const p of team.players) {
      if (p === exclude || p.z > 0.3 || p.stunT > 0) continue;
      let tt = hyp(p.x - x, p.y - y) / p.spd;
      if (p.isUser) tt -= 0.12;
      if (tt < bt) { bt = tt; best = p; }
    }
    return best;
  }
  function setterOf(team, exclude) {
    const s = team.players.find((p) => p.ch.role === 'S' && p !== exclude && p.stunT <= 0 && !(p.isUser && p.callT > 0));
    if (s) return s;
    return nearestFree(team, 5.8, wy(team, 1), exclude);
  }
  // ball is (or will be) on team's side: decide who plays it next
  function replan() {
    if (!ball.live) return;
    const L = predictZ(R);
    for (const t of G.teams) if (t.plan && t.plan.kind !== 'attack' && t.plan.kind !== 'set') t.plan = null;
    const team = L.y < NY ? G.teams[0] : G.teams[1];
    if (team.plan) return;
    const n = ball.lastTeam === team ? team.touches : 0;
    if (n >= 3) return;
    const C = predictZ(n === 0 ? 0.95 : 1.2);
    if (!onSide(team, C.y)) return;
    if (!inCourt(L.x, L.y, 0.15) && n === 0 && Math.random() < 0.85) { team.plan = { kind: 'leave', p: null }; return; }
    const p = nearestFree(team, C.x, C.y, ball.lastP && ball.lastP.team === team && !ball.blockTouch ? ball.lastP : null);
    if (!p) return;
    team.plan = { kind: n === 0 ? 'receive' : n === 1 ? 'set' : 'over', p, x: C.x, y: C.y, z: 1, t: G.t + C.t };
  }
  function chooseAttacker(team, setter) {
    const u = G.user;
    const ok = (p) => p !== setter && p.ch.role !== 'L' && p.stunT <= 0;
    if (u && u.team === team && ok(u) && (u.callT > 0 || Math.random() < 0.3)) return u;
    const c = team.players.filter((p) => ok(p) && !p.isUser);
    let best = c[0], bs = -1e9;
    for (const p of c) {
      const sc = p.ch.stats.spike * 0.6 + (p.slot <= 2 ? 3 : 0) + rnd(0, 5);
      if (sc > bs) { bs = sc; best = p; }
    }
    return best;
  }
  function attackHeight(p) { return p.reach + p.jumpH - 0.08; }

  // ===== hits =====
  function contactQuality(d, rad) { return clamp(1 - d / rad, 0, 1); }

  function doPass(p, q, toward) {   // first touch: bump to setter (or toward a point)
    const t = p.team;
    q = clamp(q, 0.05, 1);
    if (toward && !onSide(t, toward.y)) { return doOver(p, toward, q); }
    const setter = toward ? nearestFree(t, toward.x, toward.y, p) || setterOf(t, p) : setterOf(t, p);
    const sx = toward ? toward.x : 5.8, sy = toward ? toward.y : wy(t, 0.9);
    const err = (1 - q) * 2.6;
    const tx = clamp(sx + rnd(-err, err), -1, CW + 1), ty = sy - fwd(t) * rnd(0, err);
    const T = 1.15 + (1 - q) * 0.3;
    launch(tx, ty, 2.35, T);
    p.pose = 'bump'; p.poseT = 0.35;
    SFX.bump();
    if (setter) t.plan = { kind: 'set', p: setter, x: tx, y: ty, z: 2.35, t: G.t + T };
  }
  function doSet(p, q, attacker, special) {
    const t = p.team;
    q = clamp(q, 0.05, 1);
    attacker = attacker || chooseAttacker(t, p);
    if (!attacker) return doOver(p, null, q);
    let tx, ty, tz = attackHeight(attacker), T;
    const freak = attacker.isUser && attacker.air && attacker.vz > 0 && (p.ch.stats.set >= 9 || special);
    if (freak) {
      T = Math.max(0.2, attacker.vz / GP);
      tx = attacker.x + attacker.vx * T; ty = attacker.y + attacker.vy * T + fwd(t) * 0.15;
      tz = attacker.z + attacker.vz * T - 0.5 * GP * T * T + attacker.reach;
      popup('СТРАННАЯ БЫСТРАЯ!', '#ff8a1a', 1.1);
      G.slowT = 0.5;
    } else if (attacker.isUser) {
      tx = clamp(attacker.x, 0.8, CW - 0.8);
      ty = wy(t, uyOf(t, attacker.y) < 3.6 ? 0.6 : 3.3);
      T = 1.0;
    } else {
      const s = attacker.slot;
      if (s === 0) { tx = 0.9; ty = wy(t, 0.7); T = 1.15; }
      else if (s === 1) { tx = 4.0; ty = wy(t, 0.55); T = 0.62; }
      else if (s === 2) { tx = 8.1; ty = wy(t, 0.7); T = 0.95; }
      else { tx = attacker.x; ty = wy(t, 3.3); T = 1.0; }
    }
    const err = freak ? 0 : (1 - q) * 1.3;
    tx += rnd(-err, err); ty -= fwd(t) * rnd(0, err * 0.6);
    launch(tx, ty, tz, T);
    p.pose = ball.z > 1.6 ? 'set' : 'bump'; p.poseT = 0.35;
    SFX.set();
    t.plan = { kind: 'attack', p: attacker, x: tx, y: ty, z: tz, t: G.t + T, freak };
    planBlock(other(t), tx, G.t + T);
  }
  function planBlock(team, x, tHit) {
    const n = team.D.blockers;
    const front = team.players.filter((p) => p.slot <= 2 && !p.isUser).sort((a, b) => Math.abs(a.x - x) - Math.abs(b.x - x)).slice(0, n);
    front.forEach((p, k) => {
      const tPeak = Math.sqrt(2 * GP * p.jumpH * 0.85) / GP;
      p.blockTask = { x: clamp(x + (k ? 0.5 : -0.25) * (x < 4.5 ? 1 : -1), 0.3, CW - 0.3), jumpAt: tHit - tPeak + 0.04 + rnd(-0.06, 0.08) / team.D.block };
    });
  }
  function doSpike(p, q, aim, special) {
    const t = p.team, o = other(t), s = p.ch.stats;
    let tx, ty;
    if (aim && onSide(o, aim.y)) { tx = aim.x; ty = aim.y; }
    else {
      const cands = [[0.6, 8.3], [8.4, 8.3], [0.6, 4.5], [8.4, 4.5], [4.5, 8.5], [2.2, 2.4], [6.8, 2.4]];
      let bs = -1e9;
      for (const [ux, uy] of cands) {
        const cx = ux, cy = wy(o, uy);
        let md = 1e9; for (const d of o.players) if (d.z < 0.3) md = Math.min(md, hyp(d.x - cx, d.y - cy));
        const sc = md + rnd(0, 1.5);
        if (sc > bs) { bs = sc; tx = cx; ty = cy; }
      }
      const e = (1 - t.D.acc) * 1.6;
      tx += rnd(-e, e); ty += rnd(-e, e);
    }
    let S = (12.5 + s.spike * 1.25) * (0.7 + 0.35 * q) * (special ? 1.35 : 1);
    const tip = !p.isUser && !special && Math.random() < 0.08;
    if (tip) { tx = clamp(p.x + rnd(-2, 2), 0.5, 8.5); ty = wy(o, rnd(1.2, 2.2)); S = 6; }
    const d = hyp(tx - ball.x, ty - ball.y);
    const T = safeT(tx, ty, R, Math.max(0.12, d / S));
    launch(tx, ty, R, T);
    ball.power = S; ball.kind = tip ? 'tip' : 'spike';
    p.pose = 'spike'; p.poseT = 0.4;
    SFX.spike(q);
    G.shake = Math.max(G.shake, tip ? 2 : 6 + q * 6);
    burst(ball.x, ball.y, ball.z, '#fff', 10, 3);
    if (tip) popup('ОБМАНКА!', '#29e6ff', 0.7);
    if (special) {
      ball.super = { c1: p.ch.special.color, c2: p.ch.special.color2, who: p };
    } else if (q > 0.82 && !tip) {
      popup('ИДЕАЛЬНО!', '#ffd23f', 0.9);
      G.slowT = 0.45; G.cine = { t: 0.9, x: p.x, y: p.y, z: p.z + 2 };
      ball.super = { c1: '#ffffff', c2: t.data.colors.trim, who: p, mini: true };
    }
  }
  function doOver(p, aim, q) {
    const o = other(p.team);
    const tx = aim && onSide(o, aim.y) ? aim.x : rnd(1.5, 7.5), ty = aim && onSide(o, aim.y) ? aim.y : wy(o, rnd(5, 8));
    const T = safeT(tx, ty, R, 1.25);
    launch(tx, ty, R, T);
    ball.kind = 'free';
    p.pose = ball.z > 1.6 ? 'set' : 'bump'; p.poseT = 0.35;
    SFX.bump();
    void q;
  }
  function doServe(p, power, aim, special) {
    const o = other(p.team);
    let tx, ty;
    if (aim) { tx = aim.x; ty = aim.y; }
    else { tx = rnd(0.8, 8.2); ty = wy(o, rnd(3, 8.3)); }
    const sv = p.ch.stats.serve;
    let T = special ? 0.62 : lerp(1.75, 0.85, power) - (p.isUser ? 0 : sv * 0.02);
    T = safeT(tx, ty, R, T);
    launch(tx, ty, R, T);
    ball.kind = 'serve'; ball.power = hyp(tx - ball.x, ty - ball.y) / T;
    ball.lastP = p; ball.lastTeam = p.team; p.team.touches = 0; o.touches = 0;
    p.pose = 'serve'; p.poseT = 0.4;
    SFX.spike(power * 0.6);
    if (special) ball.super = { c1: p.ch.special.color, c2: p.ch.special.color2, who: p };
    G.mode = 'rally'; G.t = 0; G.prompt = '';
    replan();
  }

  // receive success model (AI and user)
  function receiveRoll(p, posQ) {
    const sp = hyp(ball.vx, ball.vy, ball.vz);
    let q = 0.45 + p.ch.stats.receive * 0.05 - Math.max(0, sp - 11) * 0.03 + p.team.D.rec + posQ * 0.25;
    if (ball.super) q -= ball.super.mini ? 0.25 : 0.55;
    return q;
  }
  function tryReceive(p, d, rad, toward, autoHit) {
    if (!registerTouch(p)) return;
    const posQ = contactQuality(d, rad);
    let q = receiveRoll(p, posQ) + (autoHit ? -0.1 : 0);
    const special = p.armed > 0 && p.energy >= 100 && p.ch.special.kind === 'dig';
    if (special) { useSpecial(p, () => { doPass(p, 1, toward); popup(p.ch.special.name.toUpperCase(), p.ch.special.color, 1); }); return; }
    if (ball.super && !ball.super.mini && Math.random() > q) {
      // super spike blasts through
      p.stunT = 0.8; ball.vz = Math.abs(ball.vz) * 0.3 + 2; ball.vx *= 0.5; ball.vy *= 0.5;
      popup('СНЕСЛО!', '#ff3b8d', 0.9); return;
    }
    if (Math.random() > clamp(q + 0.3, 0.05, 0.97)) {
      // shank
      ball.vx = rnd(-4, 4); ball.vy = -fwd(p.team) * rnd(0, 3) + rnd(-1, 1); ball.vz = rnd(2.5, 5.5);
      ball.held = null; ball.blockChecked = false;
      p.pose = 'bump'; p.poseT = 0.3; SFX.bump();
      if (p.isUser) popup('КРИВОЙ ПРИЁМ', '#aaa', 0.7);
      replan();
      return;
    }
    if (p.diveT > 0 && Math.random() < 0.5) popup('ВЫТАЩИЛ!', '#3dffb0', 0.8);
    doPass(p, clamp(q, 0.1, 1), toward);
  }

  function useSpecial(p, fn) {
    p.energy = 0; p.armed = 0;
    SFX.superS();
    G.cutin = { p, t: 1.25, max: 1.25, lines: Array.from({ length: 26 }, () => [Math.random(), rnd(0.2, 1)]), done: fn };
  }

  // AI hit execution when the ball is reachable
  function reachInfo(p) {
    const b = ball;
    if (p.air) {
      const hx = p.x, hy = p.y + fwd(p.team) * 0.15, hz = p.z + p.reach;
      const d = hyp(b.x - hx, b.y - hy, b.z - hz);
      return d < 0.85 ? { ok: true, air: true, d, rad: 0.85 } : { ok: false };
    }
    const hd = hyp(p.x - b.x, p.y - b.y), rad = p.diveT > 0 ? 1.6 : 1.0;
    if (hd < rad && b.z > 0.12 && b.z < p.reach + 0.35 && (p.diveT <= 0 || b.z < 1)) return { ok: true, air: false, d: hd, rad };
    return { ok: false };
  }
  function legalFor(p) {
    const b = ball;
    if (!b.live || b.held) return false;
    if (!(onSide(p.team, b.y) || Math.abs(b.y - NY) < 0.25)) return false;
    if (b.hitT < 0.12 && b.lastP === p) return false;
    return true;
  }
  function aiHit(p, plan, ri) {
    const t = p.team;
    const n = ball.lastTeam === t && !ball.blockTouch ? t.touches + 1 : 1;
    if (n === 1) { tryReceive(p, ri.d, ri.rad, null, false); return; }
    if (!registerTouch(p)) return;
    if (n === 2) {
      const q = clamp(0.55 + p.ch.stats.set * 0.045 + contactQuality(ri.d, ri.rad) * 0.2 - (ball.z < 1.5 ? 0.25 : 0), 0.1, 1);
      if (p.ch.stats.set >= 9 && p.energy >= 100 && Math.random() < 0.5) {
        const atk = chooseAttacker(t, p);
        useSpecial(p, () => doSet(p, 1, atk, true));
        return;
      }
      doSet(p, q, plan && plan.kind === 'set' ? null : null, false);
      return;
    }
    if (ri.air) {
      const q = clamp(contactQuality(ri.d, ri.rad) * 0.7 + 0.3 * clamp(1 - Math.abs(p.vz) / 3, 0, 1) + rnd(-0.1, 0.1), 0, 1);
      if (p.energy >= 100 && p.ch.special.kind === 'spike' && Math.random() < 0.7) { useSpecial(p, () => doSpike(p, 1, null, true)); return; }
      doSpike(p, q, null, false);
    } else doOver(p, null, 0.5);
  }

  function aiPlayer(p, dt) {
    const t = p.team, f = fwd(t), plan = t.plan;
    p.sprint = false;
    if (p.stunT > 0 || p.recT > 0 || p.diveT > 0) { p.dvx = p.dvy = 0; aiTryContact(p, plan); return; }
    // blocking
    if (p.blockTask && (!plan || plan.p !== p)) {
      const bt = p.blockTask;
      seek(p, bt.x, NY - f * 0.45, 0.4); p.sprint = true;
      if (!p.air && G.t >= bt.jumpAt) { jump(p, 0.85); p.blockJump = true; p.blockTask = null; }
      if (G.t > bt.jumpAt + 0.8) p.blockTask = null;
      return;
    }
    if (plan && plan.p === p && plan.kind !== 'leave') {
      if (plan.kind === 'attack') {
        const tPeak = Math.sqrt(2 * GP * p.jumpH) / GP;
        const standX = plan.x, standY = plan.y - f * 0.25;
        const tl = plan.t - G.t;
        if (!p.air) {
          if (tl > tPeak + 0.45) seek(p, standX, standY - f * 1.6, 0.3);
          else seek(p, standX, standY, 0.2);
          p.sprint = true;
          if (tl <= tPeak + 0.01 && hyp(p.x - standX, p.y - standY) < 1.7) jump(p, 1);
        }
      } else {
        seek(p, plan.x, plan.y - f * 0.25, 0.25); p.sprint = true;
        const d = hyp(p.x - ball.x, p.y - ball.y);
        if (ball.live && !ball.held && ball.z < 0.9 && ball.vz < 0 && d > 0.95 && d < 2.6 && p.diveT <= 0 && onSide(t, ball.y)) dive(p, ball.x - p.x, ball.y - p.y);
      }
      aiTryContact(p, plan);
      return;
    }
    // formation
    const [ux, uy] = SLOTS[p.slot];
    let hx = ux, hu = uy;
    const ourBall = ball.lastTeam === t && onSide(t, predictZ(R).y);
    if (ourBall && p.slot <= 2) hu = 2.6;                    // front row: get ready to approach
    if (!ourBall && p.slot <= 2 && onSide(other(t), ball.y)) hu = 1.0; // wait for block
    if (p.ch.role === 'S' && ourBall) { hx = 6; hu = 1.2; }
    seek(p, hx, wy(t, hu), 0.6);
    void dt;
  }
  function aiTryContact(p, plan) {
    if (!legalFor(p) || !plan || plan.p !== p) return;
    const ri = reachInfo(p);
    if (ri.ok) aiHit(p, plan, ri);
  }

  // ===== movement =====
  function seek(p, tx, ty, arrive) {
    const dx = tx - p.x, dy = ty - p.y, d = hyp(dx, dy);
    const m = d < 0.05 ? 0 : clamp(d / arrive, 0, 1);
    p.dvx = d ? dx / d * m : 0; p.dvy = d ? dy / d * m : 0;
  }
  function jump(p, k) {
    if (p.air || p.recT > 0 || p.stunT > 0) return;
    const run = hyp(p.vx, p.vy);
    p.vz = Math.sqrt(2 * GP * p.jumpH * k) * (1 + Math.min(run, 6) * 0.012);
    p.air = true; p.blockJump = false;
    burst(p.x, p.y, 0.05, '#e8c08a', 5, 1.2, 'dust');
  }
  function dive(p, dx, dy) {
    const d = hyp(dx, dy) || 1;
    p.diveT = 0.5; p.vx = dx / d * 6.5; p.vy = dy / d * 6.5; p.diveDir = dx >= 0 ? 1 : -1;
    burst(p.x, p.y, 0.05, '#e8c08a', 6, 1.5, 'dust');
  }
  function physP(p, dt) {
    for (const k of ['recT', 'stunT', 'callT', 'armed', 'poseT']) if (p[k] > 0) p[k] -= dt;
    if (p.diveT > 0) { p.diveT -= dt; p.vx *= Math.exp(-3 * dt); p.vy *= Math.exp(-3 * dt); if (p.diveT <= 0) p.recT = 0.45; }
    else if (p.air) { /* keep horizontal momentum */ p.vx *= Math.exp(-0.6 * dt); p.vy *= Math.exp(-0.6 * dt); }
    else if (p.recT > 0 || p.stunT > 0) { p.vx *= Math.exp(-10 * dt); p.vy *= Math.exp(-10 * dt); }
    else {
      const ms = p.spd * (p.sprint ? 1.3 : 1) * p.team.D.speed, a = Math.min(1, dt * 10);
      p.vx += (p.dvx * ms - p.vx) * a; p.vy += (p.dvy * ms - p.vy) * a;
    }
    p.x += p.vx * dt; p.y += p.vy * dt;
    if (p.air) {
      p.vz -= GP * dt; p.z += p.vz * dt;
      if (p.z <= 0) { p.z = 0; p.vz = 0; p.air = false; p.recT = Math.max(p.recT, 0.18); p.blockJump = false; }
    }
    // stay on own side
    const t = p.team;
    if (t.i === 0) p.y = clamp(p.y, -4, NY - 0.3); else p.y = clamp(p.y, NY + 0.3, CL + 4);
    p.x = clamp(p.x, -3, CW + 3);
    p.run += hyp(p.vx, p.vy) * dt * 2.2;
  }

  // ===== ball update =====
  function updateBall(dt) {
    const b = ball;
    if (b.held) {
      const p = b.held;
      b.x = p.x + 0.25; b.y = p.y + fwd(p.team) * 0.2; b.z = 1.1 + p.z;
      return;
    }
    const py = b.y;
    b.hitT += dt;
    b.vz -= GB * dt;
    b.x += b.vx * dt; b.y += b.vy * dt; b.z += b.vz * dt;
    b.spin += hyp(b.vx, b.vy, b.vz) * dt * 3;
    const sp = hyp(b.vx, b.vy, b.vz);
    if (b.super && b.live) addP(b.x, b.y, b.z, 0, 0, 0, b.super.mini ? 0.25 : 0.45, Math.random() < 0.5 ? b.super.c1 : b.super.c2, b.super.mini ? 0.1 : 0.17, 'trail');
    else if (sp > 13 && b.live) addP(b.x, b.y, b.z, 0, 0, 0, 0.15, 'rgba(255,255,255,0.8)', 0.07, 'trail');

    // net plane
    if (b.live && (py - NY) * (b.y - NY) <= 0 && py !== b.y) {
      if (b.z < NH + R && b.z > -1 && b.x > -0.6 && b.x < CW + 0.6) {
        b.y = py < NY ? NY - R - 0.01 : NY + R + 0.01;
        b.vy = -b.vy * 0.2; b.vx *= 0.5; b.vz = Math.min(b.vz, 0) * 0.3;
        SFX.net();
        popup('В СЕТКУ!', '#aaa', 0.7);
        replan();
      } else {
        for (const t of G.teams) t.touches = 0;
      }
    }
    // blocks
    if (b.live && !b.blockChecked && Math.abs(b.y - NY) < 0.45 && b.lastTeam) {
      const def = b.vy > 0 ? G.teams[1] : G.teams[0];
      if (def !== b.lastTeam) {
        for (const p of def.players) {
          if (!p.air || Math.abs(p.y - NY) > 0.9) continue;
          const top = p.z + p.reach + 0.3, bot = p.z + p.reach - 0.55;
          if (Math.abs(b.x - p.x) < 0.65 && b.z > bot && b.z < top) { b.blockChecked = true; blockHit(p); break; }
        }
      }
    }
    // floor
    if (b.z <= R && b.vz < 0) {
      b.z = R;
      if (b.live) land();
      b.vz = -b.vz * 0.45; b.vx *= 0.7; b.vy *= 0.7;
    }
    // walls of the hall
    if (b.x < -7 || b.x > CW + 7) b.vx *= -0.5;
    if (b.y < -6 || b.y > CL + 6) b.vy *= -0.5;
  }
  function blockHit(p) {
    const b = ball, attacker = b.lastP, sp = hyp(b.vx, b.vy);
    p.pose = 'block'; p.poseT = 0.4;
    const special = p.armed > 0 && p.energy >= 100 && p.ch.special.kind === 'block';
    if (b.super && !b.super.mini && !special) {
      p.stunT = 0.6; b.vx *= 0.85; b.vy *= 0.85;
      popup('ПРОБИЛ БЛОК!', '#ff3b8d', 1); G.shake = 12; SFX.block();
      return;
    }
    registerTouch(p, true);
    const pk = special ? 1 : clamp(0.32 + p.ch.stats.block * 0.045 * p.team.D.block - (sp - 14) * 0.02, 0.08, 0.85);
    const r = Math.random();
    SFX.block();
    burst(b.x, b.y, b.z, '#fff', 14, 3, 'star');
    if (r < pk) {
      const tx = clamp(b.x + rnd(-1.5, 1.5), 0.3, CW - 0.3), ty = attacker ? wy(attacker.team, rnd(0.6, 2.6)) : b.y - Math.sign(b.vy) * 2;
      const T = 0.32;
      b.vx = (tx - b.x) / T; b.vy = (ty - b.y) / T; b.vz = (R - b.z + 0.5 * GB * T * T) / T;
      if (special) { useSpecialFx(p); }
      popup(special ? p.ch.special.name.toUpperCase() : 'БЛОК!', p.team.i === 0 ? '#29e6ff' : '#ff5a7a', 1);
      G.shake = 10; G.slowT = 0.35;
    } else if (r < pk + 0.35) {
      b.vx *= 0.25; b.vy *= 0.2; b.vz = 3.8;
      popup('СМЯГЧИЛ', '#ddd', 0.7);
    } else {
      b.vx = (b.x < 4.5 ? -1 : 1) * rnd(5, 8); b.vy *= 0.5; b.vz = 3;
      popup('ОТ БЛОКА В АУТ', '#ff5a7a', 0.7);
    }
    replan();
  }
  function useSpecialFx(p) {
    p.energy = 0; p.armed = 0; SFX.superS();
    G.cutin = { p, t: 1.0, max: 1.0, lines: Array.from({ length: 26 }, () => [Math.random(), rnd(0.2, 1)]), done() {} };
  }
  function land() {
    const b = ball;
    const inside = inCourt(b.x, b.y, R);
    const sideTeam = b.y < NY ? G.teams[0] : G.teams[1];
    const big = !!b.super && !b.super.mini, hero = b.super ? b.super.who : null;
    impact(b.x, b.y, big || (b.super && b.super.mini), b.super && b.super.c1, b.super && b.super.c2);
    SFX.floor();
    b.live = false; b.super = null;
    if (inside) {
      const win = other(sideTeam);
      let why = 'ОЧКО!';
      if (b.kind === 'serve' && b.lastTeam === win) why = 'ЭЙС!';
      else if (b.blockTouch && b.lastTeam === win) why = 'БЛОК!';
      else if (b.kind === 'spike') why = big ? 'ДОБИВАНИЕ!!' : 'УБОЙНЫЙ!';
      else if (b.kind === 'tip') why = 'ОБМАНКА!';
      if (big || why === 'УБОЙНЫЙ!') { G.cine = { t: 1.1, x: b.x, y: b.y, z: 0.5 }; G.slowT = 0.8; }
      if (big) popup('ドン!!', '#fff', 1.2);
      awardPoint(win, why, b.lastTeam === win ? b.lastP : hero);
    } else {
      const loser = b.lastTeam || sideTeam;
      awardPoint(other(loser), b.blockTouch && b.lastTeam === other(loser) ? 'ОТ БЛОКА!' : 'АУТ!', null);
    }
  }

  // ===== input =====
  const keys = {}, mouse = { x: 0, y: 0, used: false, fx: 4.5, fy: 14 };
  let hitBuf = 0;
  addEventListener('keydown', (e) => {
    keys[e.code] = true;
    if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault();
    if (e.repeat) return;
    if (e.code === 'Escape' || e.code === 'KeyP') { togglePause(); return; }
    const u = G.user;
    if (!u || G.cutin) return;
    if (e.code === 'Space') { if (G.mode === 'rally') { jump(u, 1); } else if (G.mode === 'serve' && ball.held !== u && G.charging) jump(u, 1); }
    else if (e.code === 'KeyJ') pressHit();
    else if (e.code === 'KeyQ') armSpecial();
    else if (e.code === 'KeyC') { u.callT = 2.5; popup('ДАЙ МНЕ!', '#ff8a1a', 0.7); }
    else if (e.code === 'KeyE' && G.mode === 'rally' && !u.air && u.diveT <= 0 && u.recT <= 0) {
      const dx = u.dvx || (ball.x - u.x), dy = u.dvy || (ball.y - u.y); dive(u, dx, dy);
    }
  });
  addEventListener('keyup', (e) => { keys[e.code] = false; if (e.code === 'KeyJ') releaseHit(); });
  cv.addEventListener('mousemove', (e) => { mouse.x = e.clientX; mouse.y = e.clientY; mouse.used = true; });
  cv.addEventListener('mousedown', (e) => { audioInit(); if (e.button === 0) pressHit(); else if (e.button === 2) { keys.Space = false; if (G.user && G.mode === 'rally') jump(G.user, 1); } });
  addEventListener('mouseup', (e) => { if (e.button === 0) releaseHit(); });
  cv.addEventListener('contextmenu', (e) => e.preventDefault());
  addEventListener('blur', () => { for (const k in keys) keys[k] = false; if (G.mode === 'rally' || G.mode === 'serve') togglePause(); });

  function armSpecial() {
    const u = G.user;
    if (u.energy < 100) { popup('ЭНЕРГИЯ НЕ ПОЛНАЯ', '#999', 0.6); return; }
    u.armed = 4;
    popup(`${u.ch.special.name}: ГОТОВ!`, u.ch.special.color, 0.8);
  }
  function pressHit() {
    const u = G.user;
    if (!u) return;
    if (G.mode === 'serve' && ball.held === u) {      // toss
      ball.held = null; ball.live = false; ball.vx = ball.vy = 0; ball.vz = 4.6; ball.z = 1.8;
      G.charging = true; G.chargeT = 0; u.pose = 'serve'; u.poseT = 1;
      return;
    }
    if (G.mode === 'rally') hitBuf = 0.28;
  }
  function releaseHit() {
    const u = G.user;
    if (G.mode === 'serve' && G.charging) serveRelease(u);
  }
  function serveRelease(u) {
    G.charging = false;
    const power = clamp(G.chargeT / 0.9, 0.1, 1);
    const special = u.armed > 0 && u.energy >= 100 && u.ch.special.kind === 'serve';
    const aim = mouse.used ? aimFloor() : null;
    const target = aim && onSide(other(u.team), aim.y) ? aim : null;
    if (special) { useSpecial(u, () => doServe(u, 1, target, true)); G.mode = 'rally'; G.t = 0; ball.vz = 0; ball.z = Math.max(ball.z, 2.6); return; }
    doServe(u, power, target, false);
  }
  function aimFloor() { const f = toFloor(mouse.x, mouse.y); return f || { x: 4.5, y: 14 }; }

  function userInput(dt) {
    const u = G.user;
    const ix = (keys.KeyD || keys.ArrowRight ? 1 : 0) - (keys.KeyA || keys.ArrowLeft ? 1 : 0);
    const iy = (keys.KeyW || keys.ArrowUp ? 1 : 0) - (keys.KeyS || keys.ArrowDown ? 1 : 0);
    const l = hyp(ix, iy) || 1;
    u.dvx = ix / l; u.dvy = iy / l;
    const want = (keys.ShiftLeft || keys.ShiftRight) && (ix || iy);
    if (want && u.stam > 2) { u.sprint = true; u.stam -= 28 * dt; } else { u.sprint = false; u.stam = Math.min(100, u.stam + 14 * dt); }
    if (G.mode !== 'rally') return;
    if (hitBuf > 0) {
      hitBuf -= dt;
      if (legalFor(u)) {
        const ri = reachInfo(u);
        if (ri.ok) { hitBuf = 0; userHit(u, ri); }
      }
    }
    // auto-receive assist: well positioned under a ball you're responsible for
    const plan = u.team.plan;
    if (plan && plan.p === u && plan.kind === 'receive' && legalFor(u) && !u.air) {
      const hd = hyp(u.x - ball.x, u.y - ball.y);
      if (hd < 0.55 && ball.z < 1.0 && ball.vz < 0) { const ri = reachInfo(u); if (ri.ok) { userHit(u, ri, true); popup('авто-приём', '#999', 0.5); } }
    }
  }
  function userHit(u, ri, auto) {
    const t = u.team;
    const n = ball.lastTeam === t && !ball.blockTouch ? t.touches + 1 : 1;
    const aim = mouse.used ? aimFloor() : null;
    const aimOwn = aim && onSide(t, aim.y);
    const special = u.armed > 0 && u.energy >= 100;
    if (n === 1 && !ri.air) { tryReceive(u, ri.d, ri.rad, aimOwn ? aim : null, auto); return; }
    if (!registerTouch(u)) return;
    if (ri.air) {
      if (aimOwn && n < 3) { // jump-set toward teammate
        const mate = nearestFree(t, aim.x, aim.y, u);
        doSet(u, 0.8, mate && mate !== u ? mate : null, false); return;
      }
      const q = clamp(contactQuality(ri.d, ri.rad) * 0.7 + 0.3 * clamp(1 - Math.abs(u.vz) / 3, 0, 1), 0, 1);
      if (special && u.ch.special.kind === 'spike') { useSpecial(u, () => doSpike(u, 1, aim, true)); return; }
      doSpike(u, q, aim, false);
      return;
    }
    if (n === 2 && (!aim || aimOwn)) {
      const q = clamp(0.5 + u.ch.stats.set * 0.05 + contactQuality(ri.d, ri.rad) * 0.2, 0.1, 1);
      let mate = null;
      if (aimOwn) {
        let bd = 1e9;
        for (const p of t.players) if (p !== u && p.ch.role !== 'L') { const d = hyp(p.x - aim.x, p.y - aim.y); if (d < bd) { bd = d; mate = p; } }
      }
      if (special && u.ch.special.kind === 'set') { useSpecial(u, () => doSet(u, 1, mate, true)); return; }
      doSet(u, q, mate, false);
      return;
    }
    doOver(u, aim, 0.6);
  }

  // ===== main update =====
  function update(rdt) {
    if (G.cutin) {
      G.cutin.t -= rdt;
      if (G.cutin.t <= 0) { const c = G.cutin; G.cutin = null; c.done(); }
      return;
    }
    const slow = G.slowT > 0 ? 0.2 : 1;
    if (G.slowT > 0) G.slowT -= rdt;
    const dt = rdt * slow;
    G.modeT -= dt;
    for (const p of G.popups) p.t -= rdt;
    G.popups = G.popups.filter((p) => p.t > 0);
    G.shake *= Math.exp(-7 * rdt);
    G.flash = Math.max(0, G.flash - rdt);
    if (G.cine) { G.cine.t -= rdt; if (G.cine.t <= 0) G.cine = null; }

    if (G.mode === 'serve') {
      const st = G.teams[G.serving], server = st.server;
      if (G.user) userInput(dt);
      for (const p of G.players) {
        if (p.isUser) {
          if (p === server) { p.dvy = 0; p.y = wy(st, 9.9); }
        } else if (p === server) { p.dvx = p.dvy = 0; }
        else { const [ux, uy] = SLOTS[p.slot]; seek(p, ux, wy(p.team, uy + (p.team !== st && p.slot !== 2 ? 1.3 : 0)), 0.5); }
        physP(p, dt);
      }
      if (G.charging) G.chargeT += dt;
      if (!server.isUser && G.modeT <= 0.6 && ball.held) { ball.held = null; ball.vx = ball.vy = 0; ball.vz = 4.6; ball.z = 1.8; server.pose = 'serve'; server.poseT = 1; if (server.ch.stats.serve >= 8) jump(server, 0.8); }
      if (!ball.held) {
        updateBall(dt);
        if (!server.isUser && ball.vz <= 0.2) {
          const special = server.energy >= 100 && server.ch.special.kind === 'serve' && Math.random() < 0.6;
          if (special) { useSpecial(server, () => doServe(server, 1, null, true)); G.mode = 'rally'; G.t = 0; }
          else doServe(server, rnd(0.45, 0.9), null, false);
        } else if (server.isUser && G.charging && ball.z < 1.3 && ball.vz < 0) serveRelease(server);
      } else updateBall(dt);
      updateFx(dt); updateCamera(rdt);
      return;
    }
    if (G.mode === 'point') {
      for (const p of G.players) { p.dvx = p.dvy = 0; if (p.celebrate > 0 && !p.air && Math.random() < dt * 1.5) { p.vz = 4; p.air = true; } physP(p, dt); }
      updateBall(dt); updateFx(dt); updateCamera(rdt);
      if (G.modeT <= 0) { if (checkWin()) endMatch(); else setupServe(); }
      return;
    }
    if (G.mode !== 'rally') return;
    G.t += dt;
    userInput(dt);
    for (const p of G.players) if (!p.isUser) aiPlayer(p, dt);
    for (const p of G.players) { physP(p, dt); if (!p.isUser) p.energy = Math.min(100, p.energy + dt * 0.4); }
    G.user.energy = Math.min(100, G.user.energy + dt * 1.0);
    updateBall(dt);
    // safety: ball dead far away
    if (ball.live && (ball.z < -2 || G.t > 40)) { ball.live = false; awardPoint(other(ball.lastTeam || G.teams[0]), 'АУТ!', null); }
    updateFx(dt);
    updateCamera(rdt);
  }
  function updateFx(dt) {
    for (const p of G.particles) {
      p.life -= dt;
      if (p.type === 'trail') continue;
      p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt; p.vz -= (p.type === 'confetti' ? 2 : 9) * dt;
      if (p.z < 0) { p.z = 0; p.vz = 0; p.vx *= 0.7; p.vy *= 0.7; }
    }
    G.particles = G.particles.filter((p) => p.life > 0);
    for (const r of G.rings) { r.life -= dt; r.r += r.speed * dt; }
    G.rings = G.rings.filter((r) => r.life > 0);
    for (const c of G.cracks) c.life -= dt;
    G.cracks = G.cracks.filter((c) => c.life > 0);
  }
  function updateCamera(rdt) {
    let tx = BASE_CAM.x, ty = BASE_CAM.y, tz = BASE_CAM.z, ta = BASE_CAM.a;
    if (G.user && G.mode !== 'menu') {
      tx = clamp(lerp(4.5, (G.user.x + ball.x) / 2, 0.45), 2.5, 6.5);
      if (ball.y > NY + 2 && ball.live) { ty += 0.8; }
    }
    if (G.cine) { tx = G.cine.x; ty = G.cine.y - 5.2; tz = G.cine.z + 2.2; ta = 0.3; }
    const k = 1 - Math.exp(-(G.cine ? 6 : 3) * rdt);
    cam.x = lerp(cam.x, tx, k); cam.y = lerp(cam.y, ty, k); cam.z = lerp(cam.z, tz, k); cam.a = lerp(cam.a, ta, k);
    ca = Math.cos(cam.a); sa = Math.sin(cam.a);
    shX = rnd(-1, 1) * G.shake; shY = rnd(-1, 1) * G.shake;
  }

  // ===== rendering =====
  function drawHall() {
    const g = ctx.createLinearGradient(0, 0, 0, chh);
    g.addColorStop(0, '#0b0f2a'); g.addColorStop(0.45, '#1c1446'); g.addColorStop(1, '#0a0716');
    ctx.fillStyle = g; ctx.fillRect(0, 0, cw, chh);
    const tm = performance.now() / 1000;
    // far wall + stands
    poly3([[-9, 25, 0], [18, 25, 0], [18, 25, 9], [-9, 25, 9]], '#151033');
    for (let r = 0; r < 4; r++) poly3([[-9, 25 - r * 0.01, 1.2 + r * 1.3], [18, 25, 1.2 + r * 1.3], [18, 25, 1.3 + r * 1.3], [-9, 25, 1.3 + r * 1.3]], '#2a1f5c');
    const jump = G.mode === 'point' ? 1 : 0;
    for (const c of crowd) {
      const s = proj(c.x * 1.35 + 0, 24.9, 1.4 + c.r * 0.62 + (jump ? Math.abs(Math.sin(tm * 9 + c.ph)) * 0.25 : Math.abs(Math.sin(tm * 2 + c.ph)) * 0.03));
      if (!s) continue;
      const r = 0.2 * s[2];
      ctx.fillStyle = c.c; ctx.fillRect(s[0] - r, s[1], r * 2, r * 2.4);
      ctx.fillStyle = '#f0c8a0'; ctx.beginPath(); ctx.arc(s[0], s[1] - r * 0.1, r * 0.8, 0, 7); ctx.fill();
    }
    // banners
    const ban = (x, txt, col, txtCol) => {
      poly3([[x - 1.6, 24.8, 8.6], [x + 1.6, 24.8, 8.6], [x + 1.6, 24.8, 5.8], [x - 1.6, 24.8, 5.8]], col, '#000', 2);
      const s = proj(x, 24.8, 7.2);
      if (s) { ctx.fillStyle = txtCol; ctx.font = `bold ${1.7 * s[2]}px serif`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(txt, s[0], s[1]); }
    };
    ban(1, '飛べ', '#111', '#fff');
    ban(8, '繋げ', '#c0262d', '#fff');
    // side walls
    poly3([[-9, -8, 0], [-9, 25, 0], [-9, 25, 9], [-9, -8, 9]], '#120e2c');
    poly3([[18, -8, 0], [18, 25, 0], [18, 25, 9], [18, -8, 9]], '#120e2c');
    // neon strips (Rematch-like arena)
    seg3([-9, -8, 2.2], [-9, 25, 2.2], '#29e6ff', 3);
    seg3([18, -8, 2.2], [18, 25, 2.2], '#ff3b8d', 3);
    seg3([-9, 25, 9], [18, 25, 9], '#ffd23f', 3);
  }
  function drawFloor() {
    poly3([[-9, -8, 0], [18, -8, 0], [18, 25, 0], [-9, 25, 0]], '#1f6c74');
    poly3([[-0.2, -0.2, 0], [CW + 0.2, -0.2, 0], [CW + 0.2, CL + 0.2, 0], [-0.2, CL + 0.2, 0]], '#e39a4a');
    // wood planks
    for (let i = 1; i < 18; i++) floorLine(i * 0.5, 0, i * 0.5, CL, 0.015, 'rgba(120,60,20,0.18)');
    // attack zones tint
    poly3([[0, NY - 3, 0], [CW, NY - 3, 0], [CW, NY + 3, 0], [0, NY + 3, 0]], 'rgba(255,120,40,0.18)');
    const lc = '#fff', w = 0.06;
    floorLine(0, 0, CW, 0, w, lc); floorLine(0, CL, CW, CL, w, lc);
    floorLine(0, 0, 0, CL, w, lc); floorLine(CW, 0, CW, CL, w, lc);
    floorLine(0, NY, CW, NY, w, lc);
    floorLine(0, NY - 3, CW, NY - 3, w, lc); floorLine(0, NY + 3, CW, NY + 3, w, lc);
    // cracks & rings
    for (const c of G.cracks) {
      ctx.globalAlpha = clamp(c.life / 2, 0, 1);
      for (const l of c.lines) for (let i = 1; i < l.length; i++) seg3([l[i - 1][0], l[i - 1][1], 0.01], [l[i][0], l[i][1], 0.01], '#2a1206', 3 - i * 0.5);
      ctx.globalAlpha = 1;
    }
    for (const r of G.rings) {
      ctx.globalAlpha = clamp(r.life / r.max, 0, 1);
      poly3(floorCircle(r.x, r.y, r.r, 36), null, r.col, 4);
      ctx.globalAlpha = 1;
    }
  }
  function drawShadows() {
    for (const p of G.players) {
      const s = proj(p.x, p.y, 0);
      if (!s) continue;
      const k = 1 / (1 + p.z * 0.5);
      ctx.fillStyle = `rgba(0,0,0,${0.3 * k})`;
      ctx.beginPath(); ctx.ellipse(s[0], s[1], 0.42 * s[2] * k, 0.42 * s[2] * k * sa * 0.9, 0, 0, 7); ctx.fill();
      if (p.isUser) {
        poly3(floorCircle(p.x, p.y, 0.55), null, '#ffd23f', 3);
      }
    }
    // ball shadow + landing marker
    if (!ball.held) {
      const s = proj(ball.x, ball.y, 0);
      if (s) { ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.beginPath(); ctx.ellipse(s[0], s[1], 0.16 * s[2], 0.16 * s[2] * sa, 0, 0, 7); ctx.fill(); }
      if (ball.live && ball.vz < 6 || (ball.live && ball.z > 2)) {
        const L = predictZ(R);
        const ours = L.y < NY;
        const pulse = 0.4 + 0.2 * Math.sin(performance.now() / 90);
        poly3(floorCircle(L.x, L.y, 0.35), `rgba(${ours ? '255,80,80' : '80,220,255'},${pulse * 0.5})`, ours ? '#ff5050' : '#50dcff', 2);
      }
    }
    // responsibility marker
    const u = G.user, plan = u && u.team.plan;
    if (plan && plan.p === u && G.mode === 'rally') {
      poly3(floorCircle(plan.x, plan.y, 0.7), 'rgba(255,210,63,0.15)', '#ffd23f', 3);
      const s = proj(plan.x, plan.y, plan.kind === 'attack' ? plan.z : 0.2);
      if (s) {
        const lab = { receive: 'ПРИЁМ!', set: 'ПАС!', attack: 'АТАКА!', over: 'ПЕРЕБРОСЬ!' }[plan.kind] || '';
        txt(lab, s[0], s[1] - 18, 16, '#ffd23f');
        if (plan.kind === 'attack') {
          const tl = plan.t - G.t, tPeak = Math.sqrt(2 * GP * u.jumpH) / GP;
          if (!u.air && tl < tPeak + 0.25 && tl > 0) txt('ПРЫГАЙ! (Пробел)', cw / 2, chh * 0.7, 26, '#ff8a1a');
          if (u.air) txt('БЕЙ! (ЛКМ)', cw / 2, chh * 0.7, 26, '#ff3b8d');
        }
      }
    }
    // aim reticle
    if (u && mouse.used && (G.mode === 'rally' || (G.mode === 'serve' && G.teams[G.serving].server === u))) {
      const a = aimFloor();
      poly3(floorCircle(a.x, a.y, 0.3, 16), null, 'rgba(255,255,255,0.7)', 2);
      floorLine(a.x - 0.45, a.y, a.x + 0.45, a.y, 0.04, 'rgba(255,255,255,0.7)');
      floorLine(a.x, a.y - 0.45, a.x, a.y + 0.45, 0.04, 'rgba(255,255,255,0.7)');
    }
  }
  function drawNet() {
    // posts
    for (const x of [-0.7, CW + 0.7]) {
      poly3([[x - 0.05, NY, 0], [x + 0.05, NY, 0], [x + 0.05, NY, NH + 0.15], [x - 0.05, NY, NH + 0.15]], '#d8d8e8', '#333', 1);
    }
    poly3([[-0.7, NY, NB], [CW + 0.7, NY, NB], [CW + 0.7, NY, NH], [-0.7, NY, NH]], 'rgba(10,10,20,0.35)');
    ctx.globalAlpha = 0.55;
    for (let x = -0.6; x <= CW + 0.6; x += 0.3) seg3([x, NY, NB], [x, NY, NH], '#111', 1);
    for (let z = NB; z <= NH; z += 0.2) seg3([-0.7, NY, z], [CW + 0.7, NY, z], '#111', 1);
    ctx.globalAlpha = 1;
    seg3([-0.7, NY, NH], [CW + 0.7, NY, NH], '#fff', 4);
    seg3([-0.7, NY, NB], [CW + 0.7, NY, NB], '#eee', 2);
    for (const x of [0, CW]) for (let k = 0; k < 9; k++) seg3([x, NY, NB + k * 0.2], [x, NY, NB + k * 0.2 + 0.2], k % 2 ? '#fff' : '#e22', 3);
  }
  function poseOf(p) {
    if (G.mode === 'point' && p.celebrate) return p.celebrate > 0 ? 'celebrate' : 'sad';
    if (p.diveT > 0) return 'dive';
    if (p.poseT > 0 && p.pose) return p.pose;
    if (p.air) {
      if (p.blockJump || p.blockTask) return 'block';
      const pl = p.team.plan;
      if (pl && pl.p === p && pl.kind === 'attack') return 'spikeWind';
      if (p.isUser && ball.live && hyp(ball.x - p.x, ball.y - p.y) < 2.5) return 'spikeWind';
      if (Math.abs(p.y - NY) < 1.2) return 'block';
      return 'jump';
    }
    if (hyp(p.vx, p.vy) > 0.6) return 'run';
    return G.mode === 'rally' ? 'ready' : 'idle';
  }
  function drawPlayer(p) {
    const s = proj(p.x, p.y, p.z);
    if (!s) return;
    const c = p.team.data.colors, lib = p.ch.role === 'L';
    const scale = s[2] * 1.85 / 64;
    const o = {
      scale, view: p.team.i === 0 ? 'back' : 'front', pose: poseOf(p), runPhase: p.run, moving: hyp(p.vx, p.vy) > 0.6,
      dir: p.diveT > 0 ? p.diveDir : (p.vx >= 0 ? 1 : -1), shirt: lib ? c.liberoShirt : c.shirt, trim: c.trim, shorts: c.shorts,
      numColor: c.num, number: p.ch.num, aura: p.energy >= 100 || p.armed > 0 || (G.cutin && G.cutin.p === p),
    };
    if (p.stunT > 0) { ctx.save(); ctx.globalAlpha = 0.6 + 0.4 * Math.sin(performance.now() / 40); window.drawVB(ctx, s[0], s[1], p.ch, o); ctx.restore(); }
    else window.drawVB(ctx, s[0], s[1], p.ch, o);
    if (p.isUser) {
      const hy = s[1] - 64 * scale * (p.ch.h || 1) - 14;
      ctx.fillStyle = '#ffd23f'; ctx.strokeStyle = '#000'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(s[0] - 8, hy - 10); ctx.lineTo(s[0] + 8, hy - 10); ctx.lineTo(s[0], hy); ctx.closePath(); ctx.fill(); ctx.stroke();
      if (p.callT > 0) txt('ДАЙ!', s[0], hy - 22, 14, '#ff8a1a');
    }
  }
  function drawBall() {
    const s = proj(ball.x, ball.y, ball.z);
    if (!s) return;
    const r = 0.17 * s[2];
    if (ball.super) {
      const g = ctx.createRadialGradient(s[0], s[1], 0, s[0], s[1], r * 4);
      g.addColorStop(0, ball.super.c2); g.addColorStop(0.4, ball.super.c1); g.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = g; ctx.beginPath(); ctx.arc(s[0], s[1], r * 4, 0, 7); ctx.fill();
    }
    ctx.fillStyle = '#f6f1e0'; ctx.strokeStyle = '#1a1020'; ctx.lineWidth = Math.max(1, r * 0.15);
    ctx.beginPath(); ctx.arc(s[0], s[1], r, 0, 7); ctx.fill(); ctx.stroke();
    // volleyball panels (yellow / blue)
    ctx.save(); ctx.beginPath(); ctx.arc(s[0], s[1], r, 0, 7); ctx.clip();
    for (let k = 0; k < 3; k++) {
      const a = ball.spin + k * 2.094;
      ctx.strokeStyle = k === 0 ? '#2a5bd7' : k === 1 ? '#f5c518' : '#2a5bd7';
      ctx.lineWidth = r * 0.45;
      ctx.beginPath(); ctx.arc(s[0] + Math.cos(a) * r * 1.1, s[1] + Math.sin(a) * r * 1.1, r * 1.05, 0, 7); ctx.stroke();
    }
    ctx.restore();
    ctx.beginPath(); ctx.arc(s[0], s[1], r, 0, 7); ctx.stroke();
  }
  function drawParticle(p) {
    const s = proj(p.x, p.y, p.z);
    if (!s) return;
    const a = clamp(p.life / p.max, 0, 1), sz = p.size * s[2];
    ctx.globalAlpha = a; ctx.fillStyle = p.color;
    if (p.type === 'star') {
      ctx.beginPath();
      for (let i = 0; i < 8; i++) { const rr = i % 2 ? sz * 0.4 : sz * 1.4, an = i * Math.PI / 4 + p.life * 6; ctx.lineTo(s[0] + Math.cos(an) * rr, s[1] + Math.sin(an) * rr); }
      ctx.fill();
    } else { ctx.beginPath(); ctx.arc(s[0], s[1], Math.max(0.8, sz * (p.type === 'trail' ? a : 1)), 0, 7); ctx.fill(); }
    ctx.globalAlpha = 1;
  }
  function speedLines(cx, cy, color, alpha, n) {
    ctx.save(); ctx.globalAlpha = alpha; ctx.strokeStyle = color;
    const Rr = hyp(cw, chh);
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, r0 = Rr * rnd(0.22, 0.42);
      ctx.lineWidth = rnd(1, 4);
      ctx.beginPath(); ctx.moveTo(cx + Math.cos(a) * r0, cy + Math.sin(a) * r0); ctx.lineTo(cx + Math.cos(a) * Rr, cy + Math.sin(a) * Rr); ctx.stroke();
    }
    ctx.restore();
  }
  function txt(s, x, y, size, fill, align = 'center') {
    ctx.font = `${size}px "Russo One", sans-serif`; ctx.textAlign = align; ctx.textBaseline = 'middle';
    ctx.lineJoin = 'round'; ctx.lineWidth = Math.max(3, size / 6); ctx.strokeStyle = '#000'; ctx.strokeText(s, x, y);
    ctx.fillStyle = fill; ctx.fillText(s, x, y);
  }
  function skewBox(x, y, w, h, fill) {
    ctx.fillStyle = fill; ctx.beginPath();
    ctx.moveTo(x + 10, y); ctx.lineTo(x + w + 10, y); ctx.lineTo(x + w - 10, y + h); ctx.lineTo(x - 10, y + h); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = '#000'; ctx.lineWidth = 3; ctx.stroke();
  }
  function drawHUD() {
    const [A, B] = G.teams, cx = cw / 2, small = cw < 720, nw = small ? 0 : 190;
    if (nw) { skewBox(cx - 70 - nw, 12, nw, 40, '#16161c'); skewBox(cx + 70, 12, nw, 40, B.data.colors.shirt); }
    skewBox(cx - 70, 8, 140, 48, '#120b2a');
    if (nw) { txt(A.data.name, cx - 70 - nw / 2, 33, 16, A.data.colors.trim); txt(B.data.name, cx + 70 + nw / 2, 33, 16, B.data.colors.num === '#ffffff' ? '#fff' : '#fff'); }
    txt(`${A.score}`, cx - 38, 32, 30, '#fff'); txt(`${B.score}`, cx + 38, 32, 30, '#fff');
    txt('🏐', G.serving === 0 ? cx - 70 - (nw ? nw + 18 : 18) : cx + 70 + (nw ? nw + 18 : 18), 33, 18, '#fff');
    txt(`до ${G.target}`, cx, 32, 12, '#ffd23f');
    const u = G.user, px = 70, py = chh - 70;
    ctx.save();
    ctx.beginPath(); ctx.arc(px, py, 48, 0, 7);
    const g = ctx.createRadialGradient(px, py, 5, px, py, 48); g.addColorStop(0, u.ch.special.color); g.addColorStop(1, '#120b2a');
    ctx.fillStyle = g; ctx.fill(); ctx.lineWidth = 4; ctx.strokeStyle = '#000'; ctx.stroke(); ctx.clip();
    window.drawVBPortrait(ctx, px, py + 6, 92, u.ch);
    ctx.restore();
    txt(`${u.ch.name} #${u.ch.num}`, px + 58, py - 34, 18, '#fff', 'left');
    const bar = (y, v, col, label) => {
      ctx.fillStyle = '#000'; ctx.fillRect(px + 56, y, 180, 12);
      ctx.fillStyle = col; ctx.fillRect(px + 58, y + 2, 176 * clamp(v / 100, 0, 1), 8);
      if (label) txt(label, px + 242, y + 6, 11, '#fff', 'left');
    };
    bar(py - 16, u.stam, '#3dffb0', 'РЫВОК');
    const full = u.energy >= 100, pulse = 0.5 + 0.5 * Math.sin(performance.now() / 120);
    bar(py + 4, u.energy, full ? `hsl(${(performance.now() / 5) % 360},100%,60%)` : u.ch.special.color, full ? '' : 'ЭНЕРГИЯ');
    if (u.armed > 0) txt(`${u.ch.special.name} — ГОТОВ!`, px + 58, py + 34, 15, u.ch.special.color, 'left');
    else if (full) txt(`Q — ${u.ch.special.name}`, px + 58, py + 34, 14 + pulse * 2, '#ffd23f', 'left');
    if (G.prompt) txt(G.prompt, cx, chh - 26, 15, '#fff');
    if (G.charging) {
      const c = clamp(G.chargeT / 0.9, 0, 1);
      ctx.fillStyle = '#000'; ctx.fillRect(cx - 100, chh * 0.78, 200, 14);
      ctx.fillStyle = `hsl(${120 - c * 120},100%,55%)`; ctx.fillRect(cx - 98, chh * 0.78 + 2, 196 * c, 10);
      txt('СИЛА ПОДАЧИ', cx, chh * 0.78 - 12, 13, '#fff');
    }
    for (const p of G.popups) {
      const k = 1 - p.t / p.max, big = p.size >= 1.5;
      const sc = k < 0.12 ? lerp(2.2, 1, k / 0.12) : 1;
      ctx.save(); ctx.globalAlpha = p.t < 0.3 ? p.t / 0.3 : 1;
      const y = big ? chh * 0.36 : chh * 0.24 - k * 30;
      ctx.translate(cx, y); ctx.scale(sc, sc); ctx.rotate(big ? -0.06 : 0);
      txt(p.text, 0, 0, big ? Math.min(110, cw / 9) : 30 * p.size, p.color);
      if (p.sub) txt(p.sub, 0, big ? Math.min(70, cw / 13) : 26, big ? 18 : 14, '#fff');
      ctx.restore();
    }
  }
  function drawCutin() {
    const c = G.cutin, k = 1 - c.t / c.max, ch = c.p.ch, cy = chh / 2;
    ctx.fillStyle = 'rgba(5,0,15,0.6)'; ctx.fillRect(0, 0, cw, chh);
    const bh = Math.min(270, chh * 0.44), slide = k < 0.15 ? 1 - k / 0.15 : 0;
    ctx.save();
    ctx.translate(-slide * cw, 0);
    ctx.beginPath(); ctx.moveTo(0, cy - bh / 2 + 34); ctx.lineTo(cw, cy - bh / 2 - 34); ctx.lineTo(cw, cy + bh / 2 - 34); ctx.lineTo(0, cy + bh / 2 + 34); ctx.closePath();
    const g = ctx.createLinearGradient(0, 0, cw, 0); g.addColorStop(0, ch.special.color); g.addColorStop(1, ch.special.color2);
    ctx.fillStyle = g; ctx.fill(); ctx.lineWidth = 6; ctx.strokeStyle = '#000'; ctx.stroke();
    ctx.save(); ctx.clip();
    ctx.strokeStyle = 'rgba(255,255,255,0.6)';
    for (const [ly, len] of c.lines) {
      const y = cy - bh / 2 + ly * bh, x = ((k * 3 + ly) % 1) * cw * 1.5 - cw * 0.25;
      ctx.lineWidth = 2 + len * 4; ctx.beginPath(); ctx.moveTo(cw - x, y); ctx.lineTo(cw - x + len * 300, y); ctx.stroke();
    }
    window.drawVBPortrait(ctx, cw * 0.22 + (1 - Math.min(1, k * 4)) * -200, cy + 10, bh * 1.25, ch);
    ctx.restore();
    const fs = Math.min(60, cw / 15);
    txt(ch.special.name.toUpperCase(), cw * 0.62, cy - 12, fs, '#fff');
    txt(`${ch.name} #${ch.num} · ${c.p.team.data.name}`, cw * 0.62, cy + fs * 0.75, 18, '#ffd23f');
    txt(`«${ch.quote}»`, cw * 0.62, cy + fs * 0.75 + 26, 14, '#fff');
    ctx.restore();
    if (k > 0.88) { ctx.fillStyle = `rgba(255,255,255,${(k - 0.88) / 0.12})`; ctx.fillRect(0, 0, cw, chh); }
  }

  function render() {
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    drawHall();
    drawFloor();
    if (!G.teams.length) return;
    drawShadows();
    const ents = G.players.map((p) => ({ d: depthOf(p.x, p.y, 0), p }));
    ents.push({ d: depthOf(ball.x, ball.y, 0) - 0.01, ball: true });
    ents.push({ d: depthOf(4.5, NY, 0), net: true });
    ents.sort((a, b) => b.d - a.d);
    for (const e of ents) e.ball ? drawBall() : e.net ? drawNet() : drawPlayer(e.p);
    for (const p of G.particles) drawParticle(p);
    if (ball.super && !ball.super.mini && ball.live) { const s = proj(ball.x, ball.y, ball.z); if (s) speedLines(s[0], s[1], ball.super.c2, 0.4, 34); }
    if (G.slowT > 0) { ctx.fillStyle = 'rgba(255,255,255,0.05)'; ctx.fillRect(0, 0, cw, chh); speedLines(cw / 2, chh / 2, '#fff', 0.25, 24); }
    if (G.flash > 0) { ctx.fillStyle = `rgba(255,255,255,${G.flash})`; ctx.fillRect(0, 0, cw, chh); }
    drawHUD();
    if (G.cutin) drawCutin();
  }

  // ===== loop =====
  let last = performance.now();
  function frame(now) {
    const rdt = Math.min(0.05, (now - last) / 1000); last = now;
    if (G.mode === 'menu' || G.mode === 'end') {
      G.menuT += rdt;
      cam.x = 4.5 + Math.sin(G.menuT * 0.25) * 2; cam.y = BASE_CAM.y; cam.z = BASE_CAM.z; cam.a = BASE_CAM.a;
      ca = Math.cos(cam.a); sa = Math.sin(cam.a); shX = shY = 0;
    } else if (G.mode !== 'paused') update(rdt);
    render();
    requestAnimationFrame(frame);
  }

  // ===== UI =====
  const screens = ['title', 'select', 'pause', 'end'];
  function show(id) { for (const s of screens) $(s).classList.toggle('show', s === id); }
  const howHTML = $('howTpl').innerHTML;
  $('how').innerHTML = howHTML;
  document.querySelector('#pause .how-copy').innerHTML = howHTML;
  $('btnHow').onclick = () => $('how').classList.toggle('hidden');
  $('btnPlay').onclick = () => { audioInit(); buildSelect(); show('select'); };
  $('btnBack').onclick = () => show('title');
  $('btnGo').onclick = startGame;
  $('btnResume').onclick = togglePause;
  $('btnQuit').onclick = () => { G.mode = 'menu'; G.teams = []; G.players = []; G.user = null; show('title'); };
  $('btnRematch').onclick = () => { show(null); setupMatch(); };
  $('btnPick').onclick = () => { G.mode = 'menu'; G.teams = []; G.players = []; G.user = null; buildSelect(); show('select'); };

  function portraitCanvas(c, size, bg) {
    const cvs = document.createElement('canvas'); cvs.width = size * 2; cvs.height = size * 2;
    const x = cvs.getContext('2d'); x.scale(2, 2);
    const g = x.createLinearGradient(0, 0, 0, size); g.addColorStop(0, bg); g.addColorStop(1, '#170d38');
    x.fillStyle = g; x.fillRect(0, 0, size, size);
    window.drawVBPortrait(x, size / 2, size * 0.54, size * 0.92, c);
    return cvs;
  }
  let built = false;
  function buildSelect() {
    if (!built) {
      built = true;
      const k = teamById('karasuno');
      for (const c of k.players) {
        const d = document.createElement('div'); d.className = 'card'; d.dataset.id = c.id;
        const nm = document.createElement('div'); nm.className = 'nm'; nm.textContent = `${c.name} #${c.num}`;
        d.append(portraitCanvas(c, 120, c.special.color), nm);
        d.onclick = () => pick(c);
        $('grid').append(d);
      }
      for (const t of VB_TEAMS) {
        if (t.id === 'karasuno') continue;
        const d = document.createElement('div'); d.className = 'opp'; d.dataset.id = t.id;
        const star = t.players[0];
        const cvs = portraitCanvas(star, 64, t.colors.trim);
        const nm = document.createElement('span'); nm.textContent = t.name;
        d.append(cvs, nm);
        d.onclick = () => pickOpp(t.id);
        $('opps').append(d);
      }
    }
    pick(teamById('karasuno').players.find((c) => c.id === G.userCharId) || teamById('karasuno').players[0]);
    pickOpp(G.oppId);
  }
  const ROLE = { S: 'Связующий', WS: 'Доигровщик', MB: 'Центральный блокирующий', OP: 'Диагональный', L: 'Либеро' };
  function pick(c) {
    G.userCharId = c.id;
    for (const n of $('grid').children) n.classList.toggle('sel', n.dataset.id === c.id);
    const x = $('infoPortrait').getContext('2d');
    x.clearRect(0, 0, 220, 220);
    window.drawVBPortrait(x, 110, 120, 200, c);
    $('infoName').textContent = `${c.name} #${c.num}`;
    $('infoRole').textContent = ROLE[c.role] || c.role;
    const L = { spike: 'Атака', jump: 'Прыжок', receive: 'Приём', set: 'Пас', block: 'Блок', serve: 'Подача', speed: 'Скорость' };
    $('infoStats').innerHTML = Object.keys(L).map((k) => `<div class="stat"><span>${L[k]}</span><div class="bar"><i style="width:${c.stats[k] * 10}%"></i></div></div>`).join('');
    const kindRu = { spike: 'атака', serve: 'подача', block: 'блок', dig: 'приём', set: 'пас' };
    $('infoSpecial').innerHTML = `<b>Приём-добивание: ${c.special.name}</b> <i>(${kindRu[c.special.kind] || c.special.kind})</i><br>«${c.quote}»`;
  }
  function pickOpp(id) {
    G.oppId = id;
    for (const n of $('opps').children) n.classList.toggle('sel', n.dataset.id === id);
  }
  function startGame() {
    audioInit();
    G.diff = document.querySelector('input[name=diff]:checked').value;
    G.target = +document.querySelector('input[name=len]:checked').value;
    show(null);
    setupMatch();
  }
  function togglePause() {
    if (G.mode === 'paused') { G.mode = G.prevMode; show(null); last = performance.now(); }
    else if (['rally', 'serve', 'point'].includes(G.mode)) { G.charging = false; G.prevMode = G.mode; G.mode = 'paused'; show('pause'); }
  }
  function endMatch() {
    G.mode = 'end';
    const [A, B] = G.teams, win = A.score > B.score;
    $('endTitle').textContent = win ? 'ПОБЕДА!' : 'ПОРАЖЕНИЕ...';
    $('endTitle').style.color = win ? '#ffd23f' : '#ff5a7a';
    $('endScore').innerHTML = `<span style="color:#ff8a1a">${A.score}</span> : <span style="color:#fff">${B.score}</span>`;
    const cnt = {};
    for (const l of G.log) if (l.team === 0) cnt[l.why] = (cnt[l.why] || 0) + 1;
    $('endList').innerHTML = `<b>${A.data.name} vs ${B.data.name}</b><br>` +
      Object.entries(cnt).map(([k, v]) => `${k} — ${v}`).join('<br>') +
      `<br><br><i>${win ? '«Мы — Карасуно! Летим дальше — к национальным!»' : '«Проигрыш — это не конец. Это повод стать сильнее.»'}</i>`;
    show('end');
  }

  window.__VB = { G, ball, update, keys, mouse, setupMatch, setupServe };
  requestAnimationFrame(frame);
})();
