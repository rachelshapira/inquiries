"""Golden #4: real workflow + outbox + separate fictional target. Isolated QA only."""
import http.cookiejar, json, sys, time, urllib.request, urllib.error
from pathlib import Path

BASE = sys.argv[1].rstrip('/')
assert BASE == 'http://127.0.0.1:5081', 'Mutating checks require the isolated QA instance'

class Client:
    def __init__(self, identity):
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.call('/demo/login/' + identity, 'POST', {})
    def call(self, path, method='GET', body=None, status=200):
        request = urllib.request.Request(BASE + '/api' + path, method=method,
            data=None if body is None else json.dumps(body).encode(),
            headers={'Content-Type': 'application/json', 'X-Workflow-Client': 'portal'})
        try:
            with self.opener.open(request, timeout=20) as response: code, data = response.status, response.read()
        except urllib.error.HTTPError as response: code, data = response.code, response.read()
        assert code == status, (path, code, data[:500])
        return json.loads(data) if data else None

admin, owner, employee, outsider = [Client(identity) for identity in ['admin', 'provider', 'reviewer', 'other-provider']]
provider = owner.call('/providers')[0]['id']
folder = Path(__file__).resolve().parents[1] / 'docs/business-actions'
folder.mkdir(parents=True, exist_ok=True)
if len(sys.argv) > 2 and sys.argv[2] == '--prepare':
    mode = sys.argv[3]
    before = admin.call('/demo/business-target/' + str(provider), 'POST', {'address': 'רחוב הדוגמה 10, עיר ניסוי', 'mode': mode})
    (folder / ('browser-' + mode + '-before.json')).write_text(json.dumps(before, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Prepared isolated fictional target', mode)
    sys.exit(0)
if len(sys.argv) > 2 and sys.argv[2] == '--verify-browser':
    mode = sys.argv[3]
    result = json.loads((folder / ('browser-' + mode + '.json')).read_text(encoding='utf-8'))
    before = json.loads((folder / ('browser-' + mode + '-before.json')).read_text(encoding='utf-8'))
    after = owner.call('/demo/business-target/' + str(provider))
    success = mode != 'reject'
    assert after['address'] == (result['after'] if success else before['address'])
    assert after['changes'] - before['changes'] == (1 if success else 0)
    case = owner.call('/cases/' + str(result['inquiryId']))
    jobs = employee.call('/cases/' + str(result['inquiryId']) + '/business-actions')
    assert case['state'] == result['expected'] and len(jobs) == 1 and jobs[0]['status'] == 'sent'
    assert jobs[0]['result']['success'] == success and jobs[0]['result']['data']['before'] == before['address']
    if mode == 'interruptOnce': assert jobs[0]['attempts'] == 1
    result.update({'targetVerified': True, 'targetBefore': before, 'targetAfter': after, 'operation': jobs[0]})
    (folder / ('browser-' + mode + '.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PASS browser + actual target', mode, result['inquiryId'])
    sys.exit(0)
roles = ['Provider', 'Reviewer', 'Approver', 'Admin']

def definition(field='requestedAddress'):
    return {'name': 'בקשה לשינוי כתובת — הדגמה מקומית', 'initialState': 'draft', 'documents': [],
        'fields': [{'key': field, 'label': 'כתובת חדשה (נתוני הדגמה בלבד)', 'type': 'textarea', 'required': True,
                    'viewRoles': roles, 'editRoles': ['Provider'], 'editStates': ['draft']}],
        'states': [{'key': key, 'label': label, 'terminal': terminal} for key, label, terminal in [
            ('draft', 'טיוטה', False), ('review', 'ממתינה לאישור', False), ('applying', 'עדכון הכתובת מתבצע', False),
            ('done', 'הכתובת עודכנה', True), ('failed', 'עדכון הכתובת לא בוצע', True)]],
        'transitions': [
            {'key': 'submit', 'label': 'הגשת הבקשה', 'from': 'draft', 'to': 'review', 'roles': ['Provider'], 'guard': 'submission', 'effects': ['newRound']},
            {'key': 'approve', 'label': 'אישור ועדכון הכתובת', 'from': 'review', 'to': 'applying', 'roles': ['Reviewer', 'Approver'], 'guard': 'reason', 'effects': [],
             'businessAction': {'key': 'changeAddress', 'inputs': {'address': field}, 'success': 'completed', 'failure': 'rejected'}},
            {'key': 'completed', 'label': 'הכתובת עודכנה', 'from': 'applying', 'to': 'done', 'roles': ['Admin'], 'guard': 'none', 'effects': [], 'trigger': 'businessSuccess'},
            {'key': 'rejected', 'label': 'עדכון הכתובת לא בוצע', 'from': 'applying', 'to': 'failed', 'roles': ['Admin'], 'guard': 'none', 'effects': [], 'trigger': 'businessFailure'}]}

key = 'local-address-demo'
existing = admin.call('/processes')
number = max([p['number'] for p in existing if p['key'] == key], default=0)
config = definition()
bad = definition(); bad['transitions'][1]['businessAction']['key'] = 'unknownAction'
admin.call('/processes', 'POST', {'key': key, 'baseNumber': number, 'definition': bad}, 400)
bad = definition(); bad['transitions'][2]['trigger'] = 'user'
admin.call('/processes', 'POST', {'key': key, 'baseNumber': number, 'definition': bad}, 400)
bad = definition(); bad['transitions'][1]['businessAction']['inputs']['address'] = 'missingField'
admin.call('/processes', 'POST', {'key': key, 'baseNumber': number, 'definition': bad}, 400)
process = admin.call('/processes', 'POST', {'key': key, 'baseNumber': number, 'definition': config})
assert any(h['key'] == 'changeAddress' for h in admin.call('/catalog')['businessActions'])
owner.call('/demo/business-target/' + str(provider), 'POST', {'address': 'אסור'}, 403)
outsider.call('/demo/business-target/' + str(provider), status=403)

def wait_final(cid):
    deadline = time.monotonic() + 40
    while time.monotonic() < deadline:
        inquiry = owner.call('/cases/' + str(cid))
        if inquiry['state'] in ['done', 'failed']: return inquiry
        time.sleep(.25)
    raise AssertionError(('Business action did not complete', cid, employee.call('/cases/' + str(cid))))

results = []
for mode in ['normal', 'reject', 'interruptOnce', 'slow']:
    before_address = 'רחוב הדוגמה 10, עיר ניסוי'
    after_address = 'רחוב הבדיקה 25, עיר דמיונית — ' + mode
    before = admin.call('/demo/business-target/' + str(provider), 'POST', {'address': before_address, 'mode': mode})
    inquiry = owner.call('/cases', 'POST', {'providerId': provider, 'processVersionId': process['id'],
        'title': 'בקשה לעדכון כתובת — הדגמה מקומית', 'data': {'requestedAddress': after_address}})
    cid = inquiry['id']
    assert inquiry['state'] == 'draft' and inquiry['processVersionId'] == process['id']
    inquiry = owner.call(f'/cases/{cid}/actions/submit', 'POST', {'version': inquiry['version']})
    assert inquiry['state'] == 'review'
    assert any(row['id'] == cid for row in employee.call('/cases'))
    owner.call(f'/cases/{cid}/actions/approve', 'POST', {'version': inquiry['version'], 'note': 'אסור'}, 403)
    inquiry = employee.call(f'/cases/{cid}')
    version = inquiry['version']
    employee.call(f'/cases/{cid}/actions/approve', 'POST', {'version': version}, 400)
    inquiry = employee.call(f'/cases/{cid}/actions/approve', 'POST', {'version': version, 'note': 'אושרה בקשת הדגמה'})
    assert inquiry['state'] == 'applying' and inquiry['actions'] == [] and not any(f['editable'] for f in inquiry['fields'])
    admin.call(f'/cases/{cid}/actions/completed', 'POST', {'version': inquiry['version']}, 403)
    employee.call(f'/cases/{cid}/actions/approve', 'POST', {'version': version, 'note': 'כפילות'}, 409)
    if mode == 'slow':
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            notifications = employee.call('/notifications')
            if any(n['caseId'] == cid and 'אישור ועדכון הכתובת' in n['message'] for n in notifications): break
            time.sleep(.1)
        else: raise AssertionError('Notification did not drain while local target was slow')
        assert owner.call(f'/cases/{cid}')['state'] == 'applying'
        assert owner.call('/demo/business-target/' + str(provider))['address'] == before_address
    # Observe the target commit while the platform still waits after simulated interruption.
    interrupted = False
    if mode == 'interruptOnce':
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            target = owner.call('/demo/business-target/' + str(provider))
            current = owner.call(f'/cases/{cid}')
            if target['address'] == after_address and current['state'] == 'applying': interrupted = True; break
            time.sleep(.1)
        assert interrupted, 'Did not observe target commit before acknowledgement'
    final = wait_final(cid)
    after = owner.call('/demo/business-target/' + str(provider))
    success = mode != 'reject'
    assert final['state'] == ('done' if success else 'failed')
    assert after['address'] == (after_address if success else before_address)
    assert after['changes'] - before['changes'] == (1 if success else 0)
    assert final['response']['final'] and ('הכתובת עודכנה' if success else 'לא השתנתה') in final['response']['note']
    assert sum(h['action'] == ('הכתובת עודכנה' if success else 'עדכון הכתובת לא בוצע') for h in final['history']) == 1
    jobs = employee.call(f'/cases/{cid}/business-actions')
    assert len(jobs) == 1 and jobs[0]['status'] == 'sent' and jobs[0]['result']['success'] == success
    assert jobs[0]['result']['data']['before'] == before_address
    if mode == 'interruptOnce': assert jobs[0]['attempts'] == 1
    outsiders = outsider.call(f'/cases/{cid}/business-actions', status=403)
    external_job = owner.call(f'/cases/{cid}/business-actions')[0]
    assert external_job['attempts'] is None
    results.append({'mode': mode, 'inquiryId': cid, 'before': before, 'after': after, 'job': jobs[0],
                    'finalState': final['state'], 'response': final['response'], 'history': final['history'], 'interruptionObserved': interrupted})
    print('PASS', mode, cid)

# Different process key and field key use the same generic binding/handler, no process-name branch.
alias_key = 'local-address-alias'
number = max([p['number'] for p in existing if p['key'] == alias_key], default=0)
alias = admin.call('/processes', 'POST', {'key': alias_key, 'baseNumber': number, 'definition': {**definition('postalLocation'), 'name': 'בדיקת מיפוי כתובת לשדה אחר'}})
admin.call('/demo/business-target/' + str(provider), 'POST', {'address': 'כתובת דמיונית קודמת', 'mode': 'normal'})
c = owner.call('/cases', 'POST', {'providerId': provider, 'processVersionId': alias['id'], 'title': 'הוכחת מיפוי קלט גנרי', 'data': {'postalLocation': 'כתובת דמיונית חדשה'}})
c = owner.call(f"/cases/{c['id']}/actions/submit", 'POST', {'version': c['version']})
c = employee.call(f"/cases/{c['id']}/actions/approve", 'POST', {'version': c['version'], 'note': 'בדיקת מיפוי'})
assert wait_final(c['id'])['state'] == 'done'
assert owner.call('/demo/business-target/' + str(provider))['address'] == 'כתובת דמיונית חדשה'
print('PASS generic mapping', c['id'])

# Ready, configured local demo for the separate real browser E2E.
admin.call('/demo/business-target/' + str(provider), 'POST', {'address': 'רחוב הדוגמה 10, עיר ניסוי', 'mode': 'normal'})
folder = Path(__file__).resolve().parents[1] / 'docs/business-actions'
folder.mkdir(parents=True, exist_ok=True)
(folder / 'api-results.json').write_text(json.dumps({'passed': True, 'processId': process['id'], 'processNumber': process['number'],
    'definition': config, 'results': results, 'genericMappingInquiry': c['id'], 'checks': ['success before/after', 'failure no target mutation',
    'commit-before-ack retry/idempotency', 'one completion/history/result', 'submission/approval guards', 'automatic completion cannot be called through API',
    'queue/ownership/target access', 'configuration validation', 'generic input mapping']}, ensure_ascii=False, indent=2), encoding='utf-8')
