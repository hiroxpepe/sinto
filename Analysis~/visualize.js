// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
//
// visualize.js - honest LINE charts of the real Filter.cs sweep at 5 speeds.
// No flattering. Instantaneous frequency via local zero-crossings (validated to
// 0.0% on an accelerating chirp). Driven through the real 60Hz cutoff smoother
// (Voices.cs path), RESO=100, E1 (41.2Hz) saw note + oscillation = the MIX heard.
//
// Three LINE charts, fixed order, five speed curves each:
//   1. Pitch (semitone)        2. Loudness (dB)        3. Timbre (oscillation Hz)
//
// Zero dependencies (own SVG). Usage: node visualize.js <data_dir> <kind>

const fs = require("fs");
const path = require("path");
const SR = 44100;
const SPEEDS = ["0p3", "0p7", "1p5", "3p0", "6p0"];
const LBL = { "0p3": "0.3s byao!", "0p7": "0.7s", "1p5": "1.5s", "3p0": "3s", "6p0": "6s" };
const COL = { "0p3": "#d62728", "0p7": "#ff7f0e", "1p5": "#2ca02c", "3p0": "#1f77b4", "6p0": "#9467bd" };

function zcInst(buf, centerIdx, halfWin) {
  const lo = Math.max(1, centerIdx - halfWin), hi = Math.min(buf.length - 1, centerIdx + halfWin);
  const cross = [];
  for (let i = lo; i <= hi; i++) if (buf[i - 1] < 0 && buf[i] >= 0) { const f = -buf[i - 1] / (buf[i] - buf[i - 1]); cross.push(i - 1 + f); }
  if (cross.length < 2) return 0;
  let sum = 0; for (let k = 1; k < cross.length; k++) sum += cross[k] - cross[k - 1];
  return SR / (sum / (cross.length - 1));
}
function localRms(buf, centerIdx, halfWin) {
  const lo = Math.max(0, centerIdx - halfWin), hi = Math.min(buf.length, centerIdx + halfWin);
  let sq = 0, n = 0; for (let i = lo; i < hi; i++) { sq += buf[i] * buf[i]; n++; }
  return n > 0 ? Math.sqrt(sq / n) : 0;
}
function loadSweep(file) {
  const meta = {}, v = [];
  for (const line of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    if (!line) continue;
    if (line.startsWith("#")) { for (const kv of line.slice(1).trim().split(/\s+/)) { const [k, x] = kv.split("="); meta[k] = x; } continue; }
    v.push(parseFloat(line));
  }
  return { s: Float64Array.from(v), meta };
}
function track(file) {
  const { s, meta } = loadSweep(file);
  const start = parseFloat(meta.start), end = parseFloat(meta.end);
  const halfWin = Math.max(150, Math.floor(s.length * 0.008));
  const rmsWin = Math.max(256, Math.floor(s.length * 0.01));
  const pts = [];
  for (let knob = 2; knob <= 100; knob += 1) {
    const frac = (knob / 100 - start) / (end - start);
    if (frac < 0 || frac > 1) continue;
    const idx = Math.floor(frac * (s.length - 1));
    const osc = zcInst(s, idx, halfWin);
    pts.push({ knob, osc, semi: osc > 0 ? 69 + 12 * Math.log2(osc / 440) : 0, db: 20 * Math.log10(localRms(s, idx, rmsWin) + 1e-9) });
  }
  return pts;
}
function selftest() {
  const n = Math.floor(SR * 0.3); const buf = new Float64Array(n); let ph = 0;
  for (let i = 0; i < n; i++) { const f = 1000 + 10000 * (i / (n - 1)); ph += 2 * Math.PI * f / SR; buf[i] = Math.sin(ph); }
  const idx = Math.floor(0.95 * (n - 1));
  const est = zcInst(buf, idx, Math.max(150, Math.floor(n * 0.008)));
  console.log(`selftest chirp 0.3s @95%: meas=${Math.round(est)}Hz true=10500Hz err=${(Math.abs(est - 10500) / 10500 * 100).toFixed(1)}%`);
}
function chart(series, o) {
  const { width, height, top, title, yLabel, get } = o;
  const padL = 78, padR = 120, padT = 24, padB = 24;
  const pw = width - padL - padR, ph = height - padT - padB;
  let ymin = Infinity, ymax = -Infinity;
  for (const s of series) for (const p of s.pts) { const y = get(p); if (y < ymin) ymin = y; if (y > ymax) ymax = y; }
  if (ymin === ymax) ymax = ymin + 1;
  const padY = (ymax - ymin) * 0.08; ymin -= padY; ymax += padY;
  const sx = v => padL + (v / 100) * pw;
  const sy = v => top + padT + ph - ((v - ymin) / (ymax - ymin)) * ph;
  let s = `<rect x="${padL}" y="${top + padT}" width="${pw}" height="${ph}" fill="#fafafa" stroke="#ccc"/>`;
  for (let t = 0; t <= 4; t++) { const yv = ymin + (ymax - ymin) * t / 4, yy = sy(yv); s += `<line x1="${padL}" y1="${yy}" x2="${padL + pw}" y2="${yy}" stroke="#eee"/><text x="${padL - 6}" y="${yy + 3}" font-size="9" text-anchor="end" fill="#555">${fmt(yv)}</text>`; }
  for (let xv = 0; xv <= 100; xv += 10) { const xx = sx(xv); s += `<text x="${xx}" y="${top + padT + ph + 14}" font-size="9" text-anchor="middle" fill="#555">${xv}</text>`; }
  for (const ser of series) { let pts = ""; for (const p of ser.pts) pts += `${sx(p.knob).toFixed(1)},${sy(get(p)).toFixed(1)} `; s += `<polyline points="${pts.trim()}" fill="none" stroke="${ser.color}" stroke-width="1.3" opacity="0.9"/>`; }
  let ly = top + padT + 4;
  for (const ser of series) { s += `<line x1="${padL + pw + 10}" y1="${ly}" x2="${padL + pw + 28}" y2="${ly}" stroke="${ser.color}" stroke-width="2.5"/><text x="${padL + pw + 32}" y="${ly + 3}" font-size="9" fill="#333">${ser.label}</text>`; ly += 15; }
  s += `<text x="${padL}" y="${top + 15}" font-size="11" font-weight="bold" fill="#222">${title}</text>`;
  s += `<text x="13" y="${top + padT + ph / 2}" font-size="9" fill="#666" transform="rotate(-90 13 ${top + padT + ph / 2})" text-anchor="middle">${yLabel}</text>`;
  return s;
}
function fmt(v) { const a = Math.abs(v); if (a >= 1000) return Math.round(v).toString(); if (a >= 10) return v.toFixed(0); if (a >= 1) return v.toFixed(1); return v.toFixed(2); }
function main() {
  const dataDir = process.argv[2] || "data";
  const kind = process.argv[3] || "moog";
  selftest();
  const series = SPEEDS.map(sp => ({ pts: track(path.join(dataDir, `sweep_${kind}_${sp}.csv`)), color: COL[sp], label: LBL[sp] }));
  const W = 1060, ch = 235, H = ch * 3 + 46;
  let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" font-family="sans-serif">`;
  svg += `<rect width="${W}" height="${H}" fill="white"/>`;
  svg += `<text x="${W / 2}" y="20" font-size="13" font-weight="bold" text-anchor="middle">${kind}: sweep at 5 speeds (instantaneous freq, RESO=100, 60Hz smoother, E1 saw MIX)</text>`;
  const b = 32;
  svg += chart(series, { width: W, height: ch, top: b + ch * 0, title: "1. Pitch (semitone) vs CUTOFF", yLabel: "MIDI note", get: p => p.semi });
  svg += chart(series, { width: W, height: ch, top: b + ch * 1, title: "2. Loudness (dB) vs CUTOFF", yLabel: "dB", get: p => p.db });
  svg += chart(series, { width: W, height: ch, top: b + ch * 2, title: "3. Timbre (oscillation Hz) vs CUTOFF", yLabel: "Hz", get: p => p.osc });
  svg += `<text x="${W / 2}" y="${H - 4}" font-size="10" text-anchor="middle" fill="#444">CUTOFF (0-100)</text>`;
  svg += `</svg>`;
  const out = path.join(dataDir, `sweep_speed_${kind}.svg`);
  fs.writeFileSync(out, svg);
  console.log(`saved ${out}`);
}
main();
