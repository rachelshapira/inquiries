"""Real API privacy/regression check. Run only against an isolated demo copy.
Creates two inquiries using existing published processes; does not change definitions.
"""
import http.cookiejar, json, sys, urllib.request, urllib.error, uuid
from pathlib import Path

base = sys.argv[1].rstrip('/')
assert base == 'http://127.0.0.1:5081', 'Use the isolated QA demo, never the working application'
class Client:
    def __init__(self, identity):
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.call('/demo/login/' + identity, 'POST', {})
    def call(self, path, method='GET', body=None, status=200, content_type='application/json'):
        data = body if isinstance(body, bytes) else None if body is None else json.dumps(body).encode()
        request = urllib.request.Request(base + '/api' + path, data=data, method=method,
            headers={'X-Workflow-Client': 'portal', 'Content-Type': content_type})
        try:
            with self.opener.open(request, timeout=20) as response: code, value = response.status, response.read()
        except urllib.error.HTTPError as response: code, value = response.code, response.read()
        assert code == status, (path, code, value[:500])
        return json.loads(value) if value else None

owner, employee, outsider = Client('provider'), Client('reviewer'), Client('other-provider')
processes = owner.call('/processes')
provider = owner.call('/providers')[0]
private = 'INTERNAL-' + uuid.uuid4().hex

def create(process):
    fields = process['definition']['fields']
    values = {'number': '1250', 'date': '2026-09-30', 'email': 'demo@example.org'}
    c = owner.call('/cases', 'POST', {'providerId': provider['id'], 'processVersionId': process['id'],
        'title': 'בדיקת הפרדת מידע למגיש ולעובד', 'data': {f['key']: values.get(f['type'], 'בירור שירות והשלמת מסמכים') for f in fields}})
    assert c['tasks'] == [] and c['routing'] is None and c['history'][0]['note'] == ''
    pdf = Path(__file__).parent.joinpath('fixtures/demo-invoice.pdf').read_bytes()
    for kind in process['definition']['documents']:
        boundary = uuid.uuid4().hex
        body = b''
        for name, value in [('version', str(c['version'])), ('kind', kind)]:
            body += (f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n').encode()
        body += (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="demo-invoice.pdf"\r\nContent-Type: application/pdf\r\n\r\n').encode() + pdf + (f'\r\n--{boundary}--\r\n').encode()
        c = owner.call(f"/cases/{c['id']}/documents", 'POST', body, content_type='multipart/form-data; boundary=' + boundary)
    submission = next(t for t in process['definition']['transitions'] if t['from'] == c['state'] and t['guard'] == 'submission')
    return owner.call(f"/cases/{c['id']}/actions/{submission['key']}", 'POST', {'version': c['version'], 'note': ''})

def external_view(cid):
    c = owner.call(f'/cases/{cid}')
    assert c['tasks'] == [] and c['routing'] is None and c['assigneeId'] is None
    assert private not in json.dumps(c) and all(h['isPublic'] and h['actor'] == '' for h in c['history'])
    outsider.call(f'/cases/{cid}', status=403)
    return c

owner.call('/tasks', status=403)
assert owner.call('/reports')['openTasks'] == owner.call('/reports')['overdueTasks'] == 0
payment = next(p for p in processes if p['key'] == 'payment-inquiry' and p['number'] == 2)
c = create(payment); cid = c['id']
c = employee.call(f'/cases/{cid}')
assert c['tasks']
start = next(a for a in c['actions'] if not a['needsReason'])
c = employee.call(f"/cases/{cid}/actions/{start['key']}", 'POST', {'version': c['version'], 'note': private})
assert any(h['note'] == private for h in c['history'])
assert external_view(cid)['state'] == c['state']
closing = next(a for a in c['actions'] if a['needsReason'])
reply = 'הבקשה נבדקה והטיפול הושלם.'
c = employee.call(f"/cases/{cid}/actions/{closing['key']}", 'POST', {'version': c['version'], 'note': reply, 'shareNote': True})
c = external_view(cid)
assert c['response']['note'] == reply and c['response']['actor'] == ''
assert any(h['note'] == reply for h in c['history']) and len(c['documents']) == len(payment['definition']['documents'])

review_process = next(p for p in processes if p['key'] == 'document-submission')
r = create(review_process); rid = r['id']; r = employee.call(f'/cases/{rid}')
task = r['tasks'][0]
r = employee.call(f"/cases/{rid}/tasks/{task['id']}", 'POST', {'version': r['version'], 'result': 'failed', 'note': private})
assert any(t['note'] == private for t in r['tasks'])
external_view(rid)
request = next(a for a in r['actions'] if a['needsReason'])
message = 'יש לצרף מסמך מעודכן וקריא.'
r = employee.call(f"/cases/{rid}/actions/{request['key']}", 'POST', {'version': r['version'], 'note': message, 'shareNote': True})
assert any(h['note'] == message for h in external_view(rid)['history'])
assert external_view(rid)['response']['note'] == message and not external_view(rid)['response']['final']
assert any(private in h['note'] for h in employee.call(f'/cases/{rid}')['history'])
legacy = owner.call('/cases/1')
assert legacy['history'] == [], 'Unclassified legacy history must stay internal'
print(json.dumps({'passed': True, 'closedInquiryId': cid, 'correctionsInquiryId': rid,
    'checks': ['internal task notes and history blocked', 'direct tasks API denied', 'public completion request and response persist',
        'employee retains audit', 'unclassified history private', 'ownership and required-document workflow retained']}, ensure_ascii=False))
