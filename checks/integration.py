"""Run against the local demo: python checks/integration.py [base-url].
Creates clearly labelled demo inquiries and disables test routing rules afterwards.
"""
import http.cookiejar, json, sys, time, urllib.request, urllib.error, uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5080').rstrip('/')
class Client:
 def __init__(self,account=None):
  self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  if account:self.call('/demo/login/'+account,'POST',{})
 def call(self,path,method='GET',body=None,status=200,raw=False,content_type=None):
  data=body if isinstance(body,bytes) else None if body is None else json.dumps(body,ensure_ascii=False).encode()
  headers={'X-Workflow-Client':'portal'}
  if data is not None:headers['Content-Type']=content_type or 'application/json'
  request=urllib.request.Request(BASE+'/api'+path,data=data,headers=headers,method=method)
  try:
   with self.opener.open(request,timeout=20) as response:code=response.status;value=response.read()
  except urllib.error.HTTPError as error:code=error.code;value=error.read()
  assert code==status,(method,path,code,value.decode(errors='replace')[:700])
  if raw:return value
  return json.loads(value) if value else None
checks=0
def checked(message):
 global checks
 checks+=1;print('PASS',message,flush=True)
admin=Client('admin');north=Client('north-reviewer');haifa=Client('haifa-reviewer');care=Client('reviewer');branch=Client('branch-admin');hq=Client('hq-reviewer');provider=Client('provider');outsider=Client('other-provider')
Client().call('/cases',status=401);checked('authentication required')
assert {u['key'] for u in north.call('/org-units')}=={'north','north-haifa'}
assert {u['key'] for u in haifa.call('/org-units')}=={'north-haifa'}
branch.call('/routing/rules',status=403);north.call('/routing/rules',status=403);checked('branch subtree and central configuration permissions')
process=next(p for p in admin.call('/processes') if p['key']=='document-submission')
p=next(p for p in admin.call('/providers') if p['registration']=='515001234')
stamp=uuid.uuid4().hex[:8]
rules=[]
def rule(name,priority,conditions,target='north-haifa',match='all'):
 result=admin.call('/routing/rules','POST',{'processKey':process['key'],'name':'בדיקת '+stamp+' '+name,'priority':priority,'enabled':True,'targetUnit':target,'spec':{'match':match,'conditions':conditions},'version':0});rules.append(result);return result
try:
 r=rule('חיפה',10,[{'field':'data.notes','operator':'contains','value':stamp}])
 rule('עדיפות שנייה',20,[{'field':'data.notes','operator':'contains','value':stamp}],target='care-local')
 missing=rule('שדה חסר',0,[{'field':'data.notes','operator':'notEquals','value':'x'}])
 assert admin.call('/routing/simulate','POST',{'providerId':p['id'],'processVersionId':process['id'],'title':'missing field check','data':{}})['ruleId'] is None
 missing=admin.call('/routing/rules/'+str(missing['id']),'PUT',{**missing,'enabled':False})
 rules[2]=missing
 payload={'providerId':p['id'],'processVersionId':process['id'],'title':'בדיקת מערכת '+stamp,'data':{'contact':'בדיקה','email':'test@example.org','notes':'חיפה '+stamp}}
 preview=admin.call('/routing/simulate','POST',payload)
 assert preview['unit']=='north-haifa' and preview['ruleId']==r['id'];checked('dynamic field routing, priority and missing-field semantics')
 c=admin.call('/cases','POST',payload);cid=c['id']
 assert c['unit']=='north-haifa' and c['routing']['ruleVersion']==1
 assert any(h['note'].find('חיפה')>=0 for h in c['history']);checked('creation stores destination, rule version and audit atomically')
 assert north.call('/cases/'+str(cid))['id']==cid
 assert haifa.call('/cases/'+str(cid))['id']==cid
 assert hq.call('/cases/'+str(cid))['id']==cid
 assert provider.call('/cases/'+str(cid))['id']==cid
 for denied in [care,branch,outsider]:denied.call('/cases/'+str(cid),status=403)
 assert cid not in {c['id'] for c in care.call('/cases')}
 assert cid not in {c['id'] for c in branch.call('/cases')};checked('HQ, ancestor, leaf, sibling and branch-admin visibility')
 fallback=admin.call('/cases','POST',{**payload,'title':'בדיקת ברירת מחדל '+stamp,'data':{}})
 assert fallback['unit']=='care' and fallback['routing']['ruleId'] is None;checked('no-match fallback to provider unit')
 old={k:r[k] for k in ['processKey','name','priority','enabled','targetUnit','spec','version']}
 edited=admin.call('/routing/rules/'+str(r['id']),'PUT',{**old,'name':r['name']+' מעודכן'})
 rules[0]=edited
 admin.call('/routing/rules/'+str(r['id']),'PUT',old,status=409)
 assert admin.call('/cases/'+str(cid))['routing']['ruleVersion']==1;checked('rule concurrency and immutable existing routing decision')
 boundary='boundary'+stamp
 parts=[]
 for key,value in {'kind':'אישור ביטוח','version':str(c['version']),'validUntil':'2030-12-31'}.items():
  parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
 parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="test.pdf"\r\nContent-Type: application/pdf\r\n\r\n'.encode()+b'%PDF-1.4\n%Demo test document\n%%EOF\n\r\n')
 parts.append(f'--{boundary}--\r\n'.encode())
 c=admin.call('/cases/'+str(cid)+'/documents','POST',b''.join(parts),content_type='multipart/form-data; boundary='+boundary)
 did=c['documents'][0]['id']
 assert north.call(f'/cases/{cid}/documents/{did}',raw=True).startswith(b'%PDF-')
 care.call(f'/cases/{cid}/documents/{did}',status=403);checked('document download follows inquiry scope')
 c=admin.call(f'/cases/{cid}/actions/submit','POST',{'version':c['version'],'note':''})
 task=c['tasks'][0]
 assert task['id'] in {t['id'] for t in north.call('/tasks')}
 assert task['id'] not in {t['id'] for t in care.call('/tasks')}
 care.call(f'/cases/{cid}/tasks/{task["id"]}','POST',{'version':c['version'],'result':'passed','note':''},status=403)
 assert north.call('/reports')['total']==len(north.call('/cases'));checked('tasks, mutations and reports share server-side scope')
 admin.call(f'/cases/{cid}/actions/recommend','POST',{'version':c['version'],'note':''},status=400)
 c=north.call(f'/cases/{cid}/tasks/{task["id"]}','POST',{'version':c['version'],'result':'passed','note':'נבדק'})
 c=north.call(f'/cases/{cid}/actions/recommend','POST',{'version':c['version'],'note':''})
 assert c['state']=='approval';checked('workflow still enforces current document review')
 units={u['key']:u for u in admin.call('/org-units')}
 admin.call('/org-units/north','PUT',{**units['north'],'parentKey':'north-haifa'},status=400)
 moved=admin.call('/org-units/north-haifa','PUT',{**units['north-haifa'],'parentKey':'care'})
 try:
  north.call('/cases/'+str(cid),status=403)
  assert care.call('/cases/'+str(cid))['id']==cid
 finally:admin.call('/org-units/north-haifa','PUT',{**moved,'parentKey':'north'})
 checked('cycle prevention and immediate permission recalculation after reparenting')
 for _ in range(12):
  if any(n['caseId']==cid for n in north.call('/notifications')):break
  time.sleep(.5)
 assert cid in {n['caseId'] for n in north.call('/notifications')}
 assert cid not in {n['caseId'] for n in care.call('/notifications')};checked('notifications respect the same hierarchy')
finally:
 for saved in rules:
  latest=next(x for x in admin.call('/routing/rules') if x['id']==saved['id'])
  admin.call('/routing/rules/'+str(saved['id']),'PUT',{**latest,'enabled':False})
print(f'{checks} integration checks passed. Test rules disabled. Demo inquiry records retained.',flush=True)
