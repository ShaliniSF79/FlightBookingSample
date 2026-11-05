import { build } from 'esbuild';
import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';

const outdir = 'dist';

await rm(outdir, { recursive: true, force: true });
await mkdir(outdir, { recursive: true });

const result = await build({
  entryPoints: ['src/main.tsx'],
  bundle: true,
  splitting: true,
  format: 'esm',
  outdir: `${outdir}/assets`,
  entryNames: '[name]-[hash]',
  chunkNames: 'chunks/[name]-[hash]',
  assetNames: '[name]-[hash]',
  loader: { '.css': 'css' },
  define: { 'import.meta.env': JSON.stringify({ BASE_URL: '/' }) },
  jsx: 'automatic',
  minify: true,
  target: 'es2020',
  metafile: true,
});

const entryOutput = Object.entries(result.metafile.outputs).find(([, output]) => output.entryPoint === 'src/main.tsx');
if (!entryOutput) throw new Error('Unable to locate the client entry bundle.');
const entryFile = `/${entryOutput[0].replace(`${outdir}/`, '')}`;
const cssFile = entryOutput[1].cssBundle ? `/${entryOutput[1].cssBundle.replace(`${outdir}/`, '')}` : undefined;
const indexHtml = await readFile('index.html', 'utf8');
const withCss = cssFile ? indexHtml.replace('</head>', `  <link rel="stylesheet" href="${cssFile}">\n</head>`) : indexHtml;
await writeFile(`${outdir}/index.html`, withCss.replace('/src/main.tsx', entryFile));
await cp('public', outdir, { recursive: true });
