// Files for the browser checks of the brand assets (run.js, "Brand assets"): they are built here, so that no binary file lies in the repository.
//   png(w, h, rgb)      a real PNG (a flat colour)
//   woff(family)        a real WOFF font that a browser draws: every character from space to tilde is a solid block 0.7 em wide, so a page set in it is easy to see and to measure
//   svg(...)            plain SVGs, and the hostile ones the Studio has to refuse
const zlib = require('zlib');

function crc32(buf) {
  let c, crc = 0xFFFFFFFF;
  for (let i = 0; i < buf.length; i++) {
    c = (crc ^ buf[i]) & 0xFF;
    for (let k = 0; k < 8; k++) { c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1); }
    crc = (crc >>> 8) ^ c;
  }
  return (crc ^ 0xFFFFFFFF) >>> 0;
}

function chunk(type, data) {
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, 'ascii');
  data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
}

function png(width, height, rgb) {
  const [r, g, b] = rgb || [15, 118, 110];
  const row = Buffer.alloc(1 + width * 3);
  for (let x = 0; x < width; x++) { row[1 + x * 3] = r; row[2 + x * 3] = g; row[3 + x * 3] = b; }
  const raw = Buffer.concat(Array.from({ length: height }, () => row));
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 2;
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), chunk('IHDR', header), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]);
}

// ---- a font ------------------------------------------------------------------------------------------------------------------------------------

const u16 = v => { const b = Buffer.alloc(2); b.writeUInt16BE(v & 0xFFFF); return b; };
const i16 = v => { const b = Buffer.alloc(2); b.writeInt16BE(v); return b; };
const u32 = v => { const b = Buffer.alloc(4); b.writeUInt32BE(v >>> 0); return b; };
const pad4 = buf => (buf.length % 4 === 0 ? buf : Buffer.concat([buf, Buffer.alloc(4 - (buf.length % 4))]));
const sum32 = buf => { const p = pad4(buf); let s = 0; for (let i = 0; i < p.length; i += 4) { s = (s + p.readUInt32BE(i)) >>> 0; } return s; };
const utf16 = text => { const b = Buffer.alloc(text.length * 2); for (let i = 0; i < text.length; i++) { b.writeUInt16BE(text.charCodeAt(i), i * 2); } return b; };

function tables(family) {
  const unitsPerEm = 1000;
  // glyph 0 is empty, glyph 1 is a solid block from x = 50 to 650 and from the baseline to 700
  const glyph = pad4(Buffer.concat([
    i16(1), i16(50), i16(0), i16(650), i16(700), u16(3), u16(0), Buffer.from([1, 1, 1, 1]),
    i16(50), i16(600), i16(0), i16(-600), i16(0), i16(0), i16(700), i16(0),
  ]));
  const head = Buffer.concat([u32(0x00010000), u32(0x00010000), u32(0), u32(0x5F0F3CF5), u16(3), u16(unitsPerEm), Buffer.alloc(16), i16(0), i16(0), i16(650), i16(700), u16(0), u16(8), i16(2), i16(0), i16(0)]);
  const hhea = Buffer.concat([u32(0x00010000), i16(800), i16(-200), i16(0), u16(700), i16(0), i16(0), i16(650), i16(1), i16(0), i16(0), Buffer.alloc(8), i16(0), u16(2)]);
  const maxp = Buffer.concat([u32(0x00010000), u16(2), u16(4), u16(1), u16(0), u16(0), u16(2), u16(0), u16(0), u16(0), u16(0), u16(0), u16(0), u16(0), u16(0)]);
  const hmtx = Buffer.concat([u16(500), i16(0), u16(700), i16(50)]);
  const loca = Buffer.concat([u16(0), u16(0), u16(glyph.length / 2)]);
  // cmap: format 4, one segment 0x20 to 0x7E in which every character is glyph 1 (a glyph id array of 95 times 1), and the closing segment
  const glyphIds = Buffer.concat(Array.from({ length: 95 }, () => u16(1)));
  const subtable = Buffer.concat([u16(4), u16(32 + glyphIds.length), u16(0), u16(4), u16(4), u16(1), u16(0), u16(0x7E), u16(0xFFFF), u16(0), u16(0x20), u16(0xFFFF), i16(0), i16(1), u16(4), u16(0), glyphIds]);
  const cmap = Buffer.concat([u16(0), u16(1), u16(3), u16(1), u32(12), subtable]);
  const names = [[1, family], [2, 'Regular'], [4, family], [6, family.replace(/ /g, '') + '-Regular']];
  const strings = names.map(([, text]) => utf16(text));
  let offset = 0;
  const records = names.map(([id], i) => { const r = Buffer.concat([u16(3), u16(1), u16(0x409), u16(id), u16(strings[i].length), u16(offset)]); offset += strings[i].length; return r; });
  const name = Buffer.concat([u16(0), u16(names.length), u16(6 + names.length * 12), ...records, ...strings]);
  const os2 = Buffer.concat([
    u16(4), i16(600), u16(400), u16(5), u16(0), i16(650), i16(600), i16(0), i16(75), i16(650), i16(600), i16(0), i16(350), i16(50), i16(300), i16(0), Buffer.alloc(10),
    u32(1), u32(0), u32(0), u32(0), Buffer.from('NONE'), u16(0x40), u16(0x20), u16(0x7E), i16(800), i16(-200), i16(0), u16(800), u16(200), u32(1), u32(0), i16(500), i16(700), u16(0), u16(0x20), u16(1),
  ]);
  const post = Buffer.concat([u32(0x00030000), u32(0), i16(-100), i16(50), u32(0), u32(0), u32(0), u32(0), u32(0)]);
  return { 'OS/2': os2, cmap, glyf: glyph, head, hhea, hmtx, loca, maxp, name, post };
}

// The tables of a TrueType font in the order of their tags, with the checksum of the whole file in head.
function sfnt(family) {
  const t = tables(family);
  const tags = Object.keys(t).sort();
  const header = (n) => { const p = Math.pow(2, Math.floor(Math.log2(n))); return Buffer.concat([u32(0x00010000), u16(n), u16(p * 16), u16(Math.log2(p)), u16(n * 16 - p * 16)]); };
  let offset = 12 + tags.length * 16;
  const directory = tags.map(tag => { const e = Buffer.concat([Buffer.from(tag, 'ascii'), u32(sum32(t[tag])), u32(offset), u32(t[tag].length)]); offset += pad4(t[tag]).length; return e; });
  const file = Buffer.concat([header(tags.length), ...directory, ...tags.map(tag => pad4(t[tag]))]);
  // head.checkSumAdjustment: 0xB1B0AFBA minus the sum of the whole file (it was 0 while the sum was made)
  const headOffset = 12 + tags.length * 16 + tags.slice(0, tags.indexOf('head')).reduce((s, tag) => s + pad4(t[tag]).length, 0);
  file.writeUInt32BE((0xB1B0AFBA - sum32(file)) >>> 0, headOffset + 8);
  return { file, tags, t };
}

// A WOFF (version 1) around those tables: every table zlib-compressed when that is smaller.
function woff(family) {
  const { file, tags, t } = sfnt(family || 'E2E Sans');
  const adjustment = file.readUInt32BE(12 + tags.length * 16 + tags.slice(0, tags.indexOf('head')).reduce((s, tag) => s + pad4(t[tag]).length, 0) + 8);
  t.head.writeUInt32BE(adjustment, 8);
  const parts = tags.map(tag => { const raw = t[tag]; const z = zlib.deflateSync(raw); return { tag, raw, data: z.length < raw.length ? z : raw }; });
  let offset = 44 + tags.length * 20;
  const directory = parts.map(p => { const e = Buffer.concat([Buffer.from(p.tag, 'ascii'), u32(offset), u32(p.data.length), u32(p.raw.length), u32(sum32(p.raw))]); offset += pad4(p.data).length; return e; });
  const body = Buffer.concat(parts.map(p => pad4(p.data)));
  const header = Buffer.concat([Buffer.from('wOFF'), u32(0x00010000), u32(44 + directory.length * 20 + body.length), u16(tags.length), u16(0), u32(file.length), u16(1), u16(0), u32(0), u32(0), u32(0), u32(0), u32(0)]);
  return Buffer.concat([header, ...directory, body]);
}

// ---- SVGs --------------------------------------------------------------------------------------------------------------------------------------

const NS = 'xmlns="http://www.w3.org/2000/svg"';
const svg = {
  mark: fill => Buffer.from(`<svg ${NS} viewBox="0 0 64 64"><title>Acme</title><rect width="64" height="64" rx="14" fill="${fill || '#b4235f'}"/><circle cx="32" cy="32" r="14" fill="#ffffff"/></svg>`),
  wide: fill => Buffer.from(`<svg ${NS} viewBox="0 0 240 40"><title>Acme Anvils</title><rect width="240" height="40" rx="8" fill="${fill || '#b4235f'}"/><circle cx="20" cy="20" r="10" fill="#ffffff"/></svg>`),
  onload: Buffer.from(`<svg ${NS} viewBox="0 0 10 10" onload="alert(1)"><rect width="10" height="10"/></svg>`),
  script: Buffer.from(`<svg ${NS} viewBox="0 0 10 10"><script>alert(document.cookie)</script></svg>`),
  external: Buffer.from(`<svg ${NS} viewBox="0 0 10 10"><use href="https://evil.example/x.svg#a"/></svg>`),
  bomb: Buffer.from(`<!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">]><svg ${NS} viewBox="0 0 10 10"><text>&lol2;</text></svg>`),
};

// a Windows program with the name of a picture
const exe = () => { const b = Buffer.alloc(4096); b.write('MZ'); b.write('This program cannot be run in DOS mode.', 78); return b; };

module.exports = { png, woff, svg, exe };
