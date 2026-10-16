#!/usr/bin/env node
// Copies MatPlatform (src/MatPlatform, the platform contract for .NET apps: docs/PLATFORM.md) into an app (rule CODE-03: an app copies, nothing is loaded from elsewhere):
//   node tools/matplatform-copy.js <folder of the app's web project> [--dry]
//   (or tools/matplatform-copy.ps1 -Target <folder>)
// It writes every .cs file of src/MatPlatform into <folder>/MatPlatform/ with the first line
//   // MatPlatform <version> – a copy from MatDesign, do not edit here
// and matplatform.version next to them (the version, then the SHA-256 of each file as copied, like sha256sum; not a .json, so a web project does not publish it),
// removes the files of an older copy that the package no longer has, and says which package references the app's project still needs.
// The version is MatPlatformVersion in src/MatPlatform/MatPlatform.csproj.
// Exit code: 0 copied, 64 wrong arguments, 66 the folder is not a project folder.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const args = process.argv.slice(2);
const dry = args.includes('--dry');
const target = args.find(a => !a.startsWith('--'));
if (!target) { console.error('usage: node tools/matplatform-copy.js <folder of the app\'s web project> [--dry]'); process.exit(64); }

const source = path.join(__dirname, '..', 'src', 'MatPlatform');
const project = fs.readFileSync(path.join(source, 'MatPlatform.csproj'), 'utf8');
const version = (/<MatPlatformVersion>([^<]+)<\/MatPlatformVersion>/.exec(project) || [])[1];
if (!version) { console.error('MatPlatformVersion is missing in src/MatPlatform/MatPlatform.csproj'); process.exit(70); }

const folder = path.resolve(target);
const projects = fs.existsSync(folder) ? fs.readdirSync(folder).filter(f => f.endsWith('.csproj')) : [];
if (projects.length === 0) { console.error(`${folder} is not the folder of a .NET project (no .csproj in it).`); process.exit(66); }

const MARK = '// MatPlatform ';
const DASH = '–';
const header = `${MARK}${version} ${DASH} a copy from MatDesign, do not edit here`;
const destination = path.join(folder, 'MatPlatform');
const files = fs.readdirSync(source).filter(f => f.endsWith('.cs')).sort();
const sha = text => crypto.createHash('sha256').update(text, 'utf8').digest('hex');
const LF = String.fromCharCode(10);

if (!dry) { fs.mkdirSync(destination, { recursive: true }); }

const manifest = [`MatPlatform ${version} ${DASH} a copy from MatDesign src/MatPlatform (tools/matplatform-copy.js)`];
for (const file of files) {
  const text = header + LF + fs.readFileSync(path.join(source, file), 'utf8').split(String.fromCharCode(13) + LF).join(LF);
  manifest.push(`${sha(text)}  ${file}`);
  if (!dry) { fs.writeFileSync(path.join(destination, file), text); }
  console.log(`${dry ? 'would copy' : 'copied'}  MatPlatform/${file}`);
}

// A file of an older copy (it has the mark) that the package does not have any more.
if (fs.existsSync(destination)) {
  for (const file of fs.readdirSync(destination).filter(f => f.endsWith('.cs') && !files.includes(f))) {
    const first = fs.readFileSync(path.join(destination, file), 'utf8').split(LF, 1)[0];
    if (first.startsWith(MARK)) {
      if (!dry) { fs.unlinkSync(path.join(destination, file)); }
      console.log(`${dry ? 'would remove' : 'removed'} MatPlatform/${file} (no longer in the package)`);
    } else {
      console.warn(`kept    MatPlatform/${file}: not a file of the package (move it out of the folder)`);
    }
  }
}

if (!dry) { fs.writeFileSync(path.join(destination, 'matplatform.version'), manifest.join(LF) + LF); }
console.log(`MatPlatform ${version} ${dry ? 'would be' : 'is'} in ${destination}`);

// What the app's project needs to compile the copy.
const app = projects.map(p => fs.readFileSync(path.join(folder, p), 'utf8')).join(LF);
for (const match of project.matchAll(/<PackageReference Include="([^"]+)" Version="([^"]+)"/g)) {
  if (!app.includes(`Include="${match[1]}"`)) {
    console.warn(`the project needs <PackageReference Include="${match[1]}" Version="${match[2]}" />`);
  }
}
