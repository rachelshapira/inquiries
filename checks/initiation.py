"""Real API authorization regression; run ONLY against an isolated local QA instance."""
import http.cookiejar,json,sys,urllib.request,urllib.error,uuid
BASE=sys.argv[1].rstrip('/')
assert BASE.endswith(':5081'), 'Use the isolated QA server'
class Client:
 def __init__(self,account=None):
  self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  if account:self.call('/demo/login/'+account,'POST',{})
 def call(self,path,method='GET',body=None,status=200):
  req=urllib.request.Request(BASE+'/api'+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json','X-Workflow-Client':'portal'})
  try:
   with self.opener.open(req) as r:code,data=r.status,r.read()
  except urllib.error.HTTPError as r:code,data=r.code,r.read()
  assert code==status,(path,code,data[:350])
  return json.loads(data) if data else None
checks=[]
def passed(name):checks.append(name);print('PASS',name)
admin,provider,reviewer,approver=Client('admin'),Client('provider'),Client('reviewer'),Client('approver')
Client().call('/cases/creation-options',status=401);passed('Unauthenticated options rejected')
options=provider.call('/cases/creation-options');payment=next(p for p in options['processes'] if p['key']=='payment-inquiry');pid=options['providers'][0]['id']
assert payment['definition']['documents'] and len(options['providers'])==1;passed('Provider receives latest eligible processes and own provider only')
payload=dict(providerId=pid,processVersionId=payment['id'],title='בדיקת הרשאות יצירה',data={})
assert admin.call('/cases/creation-options')['processes']==[]
admin.call('/cases','POST',payload,403)
passed('Administrative role cannot initiate inquiries even when workflow transitions include Admin')
for client,role in [(reviewer,'Reviewer'),(approver,'Approver')]:
 assert payment['id'] not in {p['id'] for p in client.call('/cases/creation-options')['processes']}
 client.call('/cases','POST',payload,403);passed(role+' cannot initiate baseline processes via API')
stamp=uuid.uuid4().hex[:8];readonly='readonly-'+stamp
admin.call('/organization/accounts','POST',dict(id=readonly,name='קורא בלבד לבדיקת הרשאות',role='Reviewer',unit='care',providerId=None,active=True,readAccess='subtree',writeAccess='none',version=0))
ro=Client(readonly);assert ro.call('/cases/creation-options')['processes']==[];ro.call('/cases','POST',payload,403);passed('Read-only user has no eligibility and POST is rejected')
def publish(roles,name):
 definition=dict(name=name,initialState='draft',fields=[],states=[dict(key='draft',label='טיוטה',terminal=False),dict(key='done',label='הושלמה',terminal=True)],transitions=[dict(key='continue',label='השלמת הפנייה',**{'from':'draft'},to='done',roles=roles,guard='none',effects=[])],documents=[])
 return admin.call('/processes','POST',dict(key='initiation-'+uuid.uuid4().hex[:8],baseNumber=0,definition=definition))['id']
forbidden=publish(['Admin'],'תהליך מוגבל לבדיקת הרשאות')
provider.call('/cases','POST',{**payload,'processVersionId':forbidden},403)
assert forbidden not in {p['id'] for p in provider.call('/cases/creation-options')['processes']};passed('Partial process access: permitted process visible, forbidden process hidden and API rejected')
no_docs=publish(['Admin','Provider'],'בקשה ללא מסמכים')
c=provider.call('/cases','POST',{**payload,'processVersionId':no_docs});assert c['requiredDocuments']==[] and c['documents']==[] and c['actions'][0]['blockedReason'] is None
no_documents_inquiry=c['id']
c=provider.call('/cases/'+str(c['id'])+'/actions/continue','POST',dict(version=c['version'],note=''));assert c['state']=='done';passed('No-document workflow creates and executes existing transition')
review_process=publish(['Admin','Reviewer'],'תהליך המאפשר פתיחה לעובד')
assert review_process in {p['id'] for p in reviewer.call('/cases/creation-options')['processes']}
c=reviewer.call('/cases','POST',{**payload,'processVersionId':review_process});assert c['state']=='draft';passed('Workflow configuration grants Reviewer initiation without role-specific code')
provider.call('/cases','POST',{**payload,'providerId':2},403);passed('Cross-provider scope remains enforced')
result=dict(passed=True,checks=checks,readonlyAccount=readonly,paymentProcess=payment['id'],forbiddenProcess=forbidden,noDocumentsProcess=no_docs,noDocumentsInquiry=no_documents_inquiry,reviewerProcess=review_process)
from pathlib import Path
folder=Path(__file__).resolve().parents[1]/'docs/gov-ui/initiation-fix';folder.mkdir(parents=True,exist_ok=True);(folder/'authorization-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(checks),'authorization checks passed')
