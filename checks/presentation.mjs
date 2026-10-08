// Run after client/build-local.mjs. No backend writes or new test dependencies.
import assert from 'node:assert/strict';
import { businessTitle, stateLabel } from '../client/out-tsc/app/app/shared/presentation.js';
assert.equal(businessTitle('E2E 1791285149641 – בירור תשלום'), 'בירור תשלום');
assert.equal(businessTitle('בירור תשלום – E2E 1791285149641'), 'בירור תשלום');
assert.equal(businessTitle('חשבונית 1791285149641'), 'חשבונית 1791285149641');
assert.equal(businessTitle('בדיקת מערכת 0c4526af'), 'בדיקת מערכת');
assert.equal(businessTitle('TEST 123456789', 'בירור שירות'), 'בירור שירות');
assert.equal(businessTitle('בירור תשלום – E2E 1791285149641: הגשת הפנייה'), 'בירור תשלום: הגשת הפנייה');
assert.equal(businessTitle('Organization check 4cbc720f1e: פתיחת פנייה'), 'Organization check: פתיחת פנייה');
assert.equal(businessTitle('בדיקת c6a90d81 שדה חסר'), 'בדיקת שדה חסר');
assert.equal(stateLabel('ClosedWithResponse'), 'נסגרה עם מענה');
assert.equal(stateLabel('אושרה'), 'אושרה');
console.log('10 presentation checks passed');
