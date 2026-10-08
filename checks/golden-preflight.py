"""Checkpoint only: existing definitions/accounts; no configuration writes. Run against isolated QA."""
import json,urllib.request,urllib.error,http.cookiejar,sys
from pathlib import Path
base=sys.argv[1].rstrip('/');assert base.endswith(':5081')
class Client:
 def __init__(self,account=None):
  self.o=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  if account:self.call('/demo/login/'+account,'POST',{})
 def call(self,path,method='GET',body=None,status=200,raw=False):
  req=urllib.request.Request(base+'/api'+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'X-Workflow-Client':'portal','Content-Type':'application/json'})
  try:
   with self.o.open(req) as r:code,data=r.status,r.read()
  except urllib.error.HTTPError as r:code,data=r.code,r.read()
  assert code==status,(path,code,data[:200])
  return data if raw else json.loads(data) if data else None
if __name__=='__main__':
 external=Client('provider');all_versions=external.call('/processes');options=external.call('/cases/creation-options');required=next(p for p in options['processes'] if p['key']=='payment-inquiry');pid=next(e['providerIds'][0] for e in options['eligibility'] if e['processVersionId']==required['id'])
 simple=[p for p in options['processes'] if not p['definition']['documents']]
 assert not simple,'A no-document configuration now exists; inspect and run its real flow rather than treating it as missing'
 denied=Client('approver');assert denied.call('/cases/creation-options')['processes']==[]
 payload=dict(providerId=pid,processVersionId=required['id'],title='Golden authorization control',data={})
 denied.call('/cases','POST',payload,403);control=external.call('/cases','POST',payload);assert control['canUpload'] and control['requiredDocuments']==required['definition']['documents']
 partial=[]
 for a in Client().call('/session')['accounts']:
  c=Client(a['id']);o=c.call('/cases/creation-options');eligible={p['key'] for p in o['processes']};latest={p['key'] for p in all_versions}
  if eligible and eligible!=latest:partial.append(a['id'])
 assert not partial,'Existing partial eligibility found: run its UI/API control'
 result=dict(simple='SKIPPED — MISSING EXISTING TEST CONFIGURATION',simpleReason='No latest process eligible to the existing external user has documents=[]. Historical payment v1 is obsolete and does not permit that external role.',partial='SKIPPED — NO EXISTING PARTIAL-ELIGIBILITY CONFIGURATION',authorizationApi='PASS',authorizedControlInquiry=control['id'],process=required,providerId=pid,external='provider',employee='reviewer',denied='approver',configurationWrites=0)
 folder=Path(__file__).resolve().parents[1]/'docs/golden-checkpoint';folder.mkdir(parents=True,exist_ok=True);(folder/'preflight.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(result['simple']);print(result['partial']);print('Authorization API negative + authorized draft/upload-capability control PASS')
