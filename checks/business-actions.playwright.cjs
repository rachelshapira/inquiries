const path = require('node:path');
module.exports = {
  testDir: __dirname, testMatch: 'business-actions.e2e.spec.cjs', timeout: 120000, workers: 1, retries: 0,
  outputDir: path.resolve(__dirname, '../docs/business-actions/playwright-artifacts'),
  reporter: [['list'], ['json', { outputFile: path.resolve(__dirname, '../docs/business-actions/playwright-results.json') }]],
  use: { baseURL: 'http://127.0.0.1:5081', headless: true, viewport: { width: 1440, height: 1000 }, screenshot: 'only-on-failure', trace: 'retain-on-failure' },
};
