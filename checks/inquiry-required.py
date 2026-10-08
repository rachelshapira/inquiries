"""Validate the editor-published payment-inquiry v2 using real API endpoints.
Run: python checks/inquiry-required.py [local-base-url]. Creates one labelled draft.
"""
import http.cookiejar, json, sys, urllib.request, urllib.error
from pathlib import Path

base = (sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:5080').rstrip('/')
class Client:
    def __init__(self, account):
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.call('/demo/login/' + account, 'POST', {})
    def call(self, path, method='GET', data=None, status=200, raw=False):
        request = urllib.request.Request(base + '/api' + path, method=method,
            data=None if data is None else json.dumps(data).encode(),
            headers={'Content-Type': 'application/json', 'X-Workflow-Client': 'portal'})
        try:
            with self.opener.open(request, timeout=20) as response:
                code, body = response.status, response.read()
        except urllib.error.HTTPError as response:
            code, body = response.code, response.read()
        assert code == status, (path, code, body[:400])
        return body if raw else json.loads(body) if body else None

external, employee, outsider = Client('provider'), Client('reviewer'), Client('other-provider')
process = next(p for p in external.call('/processes') if p['key'] == 'payment-inquiry' and p['number'] == 2)
definition = process['definition']
values = {'פרטי הבקשה': 'Synthetic required-field check', 'מספר חשבונית': 'DEMO-TEST',
    'סכום לתשלום': '1250', 'תאריך השירות': '2026-09-30', 'דואר אלקטרוני למענה': 'demo@example.org'}
data = {f['key']: values[f['label']] for f in definition['fields']}
provider = external.call('/providers')[0]
c = external.call('/cases', 'POST', {'providerId': provider['id'], 'processVersionId': process['id'],
    'title': 'בדיקת שדות חובה ומסמכים — טיוטת בדיקה', 'data': data})
cid, checks = c['id'], []
for field in definition['fields']:
    c = external.call(f'/cases/{cid}/data', 'PUT', {'version': c['version'], 'data': {field['key']: '   '}})
    error = external.call(f'/cases/{cid}/actions/submit', 'POST', {'version': c['version'], 'note': ''}, 400)
    assert field['label'] in error['error']
    unchanged = external.call(f'/cases/{cid}')
    assert (unchanged['state'], unchanged['version'], len(unchanged['history'])) == (c['state'], c['version'], len(c['history']))
    checks.append('Missing field rejected without transition: ' + field['label'])
    c = external.call(f'/cases/{cid}/data', 'PUT', {'version': c['version'], 'data': {field['key']: data[field['key']]}})
error = external.call(f'/cases/{cid}/actions/submit', 'POST', {'version': c['version'], 'note': ''}, 400)
assert 'חשבונית' in error['error']
checks.append('Missing document rejected by the server')
for field in definition['fields']:
    if field['type'] in ('number', 'date', 'email'):
        external.call(f'/cases/{cid}/data', 'PUT', {'version': c['version'], 'data': {field['key']: 'invalid-value'}}, 400)
        checks.append('Invalid typed value rejected: ' + field['type'])
completed = next(x for x in external.call('/cases') if x['title'].startswith('בירור תשלום עם מסמכים – E2E') and x['stateLabel'] == 'ClosedWithResponse')
inquiry = external.call('/cases/' + str(completed['id']))
persisted = {f['label']: inquiry['data'][f['key']] for f in definition['fields']}
assert persisted == {'פרטי הבקשה': 'בירור תשלום עבור שירות שסופק, עם חשבונית ואישור ביצוע.',
    'מספר חשבונית': 'DEMO-2026-1042', 'סכום לתשלום': '1250', 'תאריך השירות': '2026-09-30',
    'דואר אלקטרוני למענה': 'demo@example.org'}
checks.append('All five form values persist and are visible to the external owner')
for doc in inquiry['documents']:
    endpoint = f"/cases/{inquiry['id']}/documents/{doc['id']}"
    expected = Path(__file__).parent.joinpath('fixtures', doc['fileName']).read_bytes()
    assert external.call(endpoint, raw=True) == employee.call(endpoint, raw=True) == expected
    outsider.call(endpoint, status=403)
checks.append('External and employee downloads match actual PDFs; unrelated provider denied')
result = {'passed': True, 'draftInquiryId': cid, 'completedInquiryId': inquiry['id'], 'workflow': process, 'checks': checks}
Path(__file__).parent.parent.joinpath('docs/inquiry-attachments-runtime.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print('PASS:', len(checks), 'real server validation/access checks; draft', cid)
