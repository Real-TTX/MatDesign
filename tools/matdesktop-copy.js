#!/usr/bin/env node
// Copies MatDesktop (src/MatDesktop, the toolkit for desktop programs: docs/DESKTOP.md) into a program (rule CODE-03: a program copies, nothing is loaded from elsewhere):
//   node tools/matdesktop-copy.js <folder of the program's project> [--dry] [--tokens-only | --winforms]
// It writes every .cs and .axaml file of src/MatDesktop into <folder>/MatDesktop/ (the folders kept: Controls, Setup, Themes, Tokens, Tray, Windows) with the first line
//   // MatDesktop <version> – a copy from MatDesign, do not edit here          (an XML comment in an .axaml file)
// and matdesktop.version next to them (the version, then the SHA-256 of each file as copied, like sha256sum), removes the files of an older copy that the package no longer has,
// and says which package references the program's project still needs.
// --tokens-only copies only the files that need nothing but System: Tokens/MatTokens.g.cs, Tokens/MatColor.cs and Tokens/MatPalette.cs, for a tray that stays WinForms (AGENT-05).
// --winforms copies every file that needs nothing but System (the PLAIN list): the tokens and the colours, and also the texts (MatText), the version (MatVersion), the states (MatState),
// the order of the tray menu (Tray/MatTrayMenu.cs) and the single instance, so that a WinForms tray or setup does not write them again. A test holds the list to the files that name no Avalonia.
// The version is MatDesktopVersion in src/MatDesktop/MatDesktop.csproj. Generated files are copied as they are in the repository (run node tools/desktop.js first, CI holds them fresh).
// Exit code: 0 copied, 64 wrong arguments, 66 the folder is not a project folder.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const args = process.argv.slice(2);
const dry = args.includes('--dry');
const tokensOnly = args.includes('--tokens-only');
const winforms = args.includes('--winforms');
if (tokensOnly && winforms) { console.error('--tokens-only or --winforms, not both'); process.exit(64); }
const part = tokensOnly ? ' --tokens-only' : winforms ? ' --winforms' : '';
const target = args.find(a => !a.startsWith('--'));
if (!target) { console.error('usage: node tools/matdesktop-copy.js <folder of the program\'s project> [--dry] [--tokens-only | --winforms]'); process.exit(64); }

const source = path.join(__dirname, '..', 'src', 'MatDesktop');
const project = fs.readFileSync(path.join(source, 'MatDesktop.csproj'), 'utf8');
const version = (/<MatDesktopVersion>([^<]+)<\/MatDesktopVersion>/.exec(project) || [])[1];
if (!version) { console.error('MatDesktopVersion is missing in src/MatDesktop/MatDesktop.csproj'); process.exit(70); }

const folder = path.resolve(target);
const projects = fs.existsSync(folder) ? fs.readdirSync(folder).filter(f => f.endsWith('.csproj')) : [];
if (projects.length === 0) { console.error(`${folder} is not the folder of a .NET project (no .csproj in it).`); process.exit(66); }

const DASH = '–';
const LF = String.fromCharCode(10);
const MARK_CS = '// MatDesktop ';
const MARK_XAML = '<!-- MatDesktop ';
const TOKENS = ['Tokens/MatTokens.g.cs', 'Tokens/MatColor.cs', 'Tokens/MatPalette.cs'];
const PLAIN = [...TOKENS, 'MatText.cs', 'MatVersion.cs', 'MatState.cs', 'MatDesktopInfo.cs', 'MatSingleInstance.cs', 'Tray/MatTrayMenu.cs'];
const destination = path.join(folder, 'MatDesktop');
const sha = text => crypto.createHash('sha256').update(text, 'utf8').digest('hex');

// the files of the package, relative with forward slashes, without what a build leaves
function walk(dir, prefix = '') {
  const list = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (entry.name !== 'bin' && entry.name !== 'obj') list.push(...walk(path.join(dir, entry.name), `${prefix}${entry.name}/`));
    } else if (/\.(cs|axaml)$/.test(entry.name)) {
      list.push(prefix + entry.name);
    }
  }
  return list;
}

const files = (tokensOnly ? TOKENS : winforms ? PLAIN : walk(source)).sort();
const header = file => file.endsWith('.axaml')
  ? `${MARK_XAML}${version} ${DASH} a copy from MatDesign, do not edit here -->`
  : `${MARK_CS}${version} ${DASH} a copy from MatDesign, do not edit here`;

if (!dry) { fs.mkdirSync(destination, { recursive: true }); }

const manifest = [`MatDesktop ${version} ${DASH} a copy from MatDesign src/MatDesktop (tools/matdesktop-copy.js${part})`];
for (const file of files) {
  const text = header(file) + LF + fs.readFileSync(path.join(source, file), 'utf8').split(String.fromCharCode(13) + LF).join(LF);
  manifest.push(`${sha(text)}  ${file}`);
  if (!dry) {
    fs.mkdirSync(path.dirname(path.join(destination, file)), { recursive: true });
    fs.writeFileSync(path.join(destination, file), text);
  }
  console.log(`${dry ? 'would copy' : 'copied'}  MatDesktop/${file}`);
}

// A file of an older copy (it has the mark) that the package does not have any more.
if (fs.existsSync(destination)) {
  for (const file of walk(destination).filter(f => !files.includes(f))) {
    const first = fs.readFileSync(path.join(destination, file), 'utf8').split(LF, 1)[0];
    if (first.startsWith(MARK_CS) || first.startsWith(MARK_XAML)) {
      if (!dry) { fs.unlinkSync(path.join(destination, file)); }
      console.log(`${dry ? 'would remove' : 'removed'} MatDesktop/${file} (no longer in the package)`);
    } else {
      console.warn(`kept    MatDesktop/${file}: not a file of the package (move it out of the folder)`);
    }
  }
}

if (!dry) { fs.writeFileSync(path.join(destination, 'matdesktop.version'), manifest.join(LF) + LF); }
console.log(`MatDesktop ${version}${tokensOnly ? ' (the tokens only)' : winforms ? ' (the part without Avalonia)' : ''} ${dry ? 'would be' : 'is'} in ${destination}`);

// What the program's project needs to compile the copy (the part without Avalonia needs nothing).
if (!tokensOnly && !winforms) {
  const app = projects.map(p => fs.readFileSync(path.join(folder, p), 'utf8')).join(LF);
  const avalonia = (/<AvaloniaVersion>([^<]+)<\/AvaloniaVersion>/.exec(project) || [])[1];
  for (const match of project.matchAll(/<PackageReference Include="([^"]+)" Version="([^"]+)"/g)) {
    if (!app.includes(`Include="${match[1]}"`)) {
      console.warn(`the project needs <PackageReference Include="${match[1]}" Version="${match[2].replace('$(AvaloniaVersion)', avalonia)}" />`);
    }
  }
}
