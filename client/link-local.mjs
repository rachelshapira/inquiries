// Fallback for hosts where Node cannot launch esbuild's background service.
// Uses the Angular compiler and Babel linker, then the native esbuild CLI.
import { readdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { transformAsync } from '@babel/core';
import linker from '@angular/compiler-cli/linker/babel';
const root = path.resolve('node_modules/@angular');
let count = 0;
async function walk(dir) {
 for (const entry of await readdir(dir,{withFileTypes:true})) {
  const file=path.join(dir,entry.name);
  if(entry.isDirectory()) { if(entry.name!=='node_modules') await walk(file); }
  else if(entry.name.endsWith('.mjs')) {
   const source=await readFile(file,'utf8');
   if(!source.includes('ɵɵngDeclare')) continue;
   const result=await transformAsync(source,{filename:file,plugins:[linker],configFile:false,babelrc:false,compact:false});
   await writeFile(file,result.code); count++;
  }
 }
}
await walk(root);
console.log(`Linked ${count} Angular library modules.`);
