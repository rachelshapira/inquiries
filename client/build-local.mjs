import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, copyFileSync, mkdirSync, cpSync } from 'node:fs';
import { createHash } from 'node:crypto';
execFileSync(
  process.execPath,
  ['node_modules/@angular/compiler-cli/bundles/src/bin/ngc.js', '-p', 'tsconfig.app.json'],
  { stdio: 'inherit' },
);
execFileSync(process.execPath, ['link-local.mjs'], { stdio: 'inherit' });
mkdirSync('../server/wwwroot', { recursive: true });
execFileSync(
  'node_modules/@esbuild/win32-x64/esbuild.exe',
  [
    'out-tsc/app/main.js',
    '--bundle',
    '--format=esm',
    '--splitting',
    '--minify',
    '--outdir=../server/wwwroot',
    '--entry-names=main',
    '--chunk-names=chunk-[hash]',
  ],
  { stdio: 'inherit' },
);
copyFileSync('src/styles.css', '../server/wwwroot/styles.css');
cpSync('public/fonts', '../server/wwwroot/fonts', { recursive: true });
const hash = (path) => createHash('sha256').update(readFileSync(path)).digest('hex').slice(0, 12);
const html = readFileSync('src/index.html', 'utf8')
  .replace(
    '</head>',
    `<link rel="stylesheet" href="styles.css?v=${hash('src/styles.css')}"></head>`,
  )
  .replace(
    '</body>',
    `<script type="module" src="main.js?v=${hash('../server/wwwroot/main.js')}"></script></body>`,
  );
writeFileSync('../server/wwwroot/index.html', html);
