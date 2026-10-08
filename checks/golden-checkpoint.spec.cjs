const { test } = require('playwright/test');
const fs = require('node:fs/promises');
const path = require('node:path');
test('existing-config required documents and initiation authorization', async ({page}) => {
  const config = JSON.parse(await fs.readFile(path.resolve(__dirname,'../docs/golden-checkpoint/preflight.json'),'utf8'));
  const {runGoldenCheckpoint} = await import('./golden-checkpoint.mjs');
  const result = await runGoldenCheckpoint(page,process.env.INQUIRY_E2E_URL || 'http://127.0.0.1:5081',config,{
    invoice:path.resolve(__dirname,'fixtures/demo-invoice.pdf'),service:path.resolve(__dirname,'fixtures/demo-service.pdf')});
  await fs.writeFile(path.resolve(__dirname,'../docs/golden-checkpoint/browser-result.json'),JSON.stringify(result,null,2));
});
