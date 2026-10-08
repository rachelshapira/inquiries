"""Organization access and process-specific routing. Run against isolated demo only."""
import http.cookiejar,json,sys,urllib.request,urllib.error,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5081').rstrip('/')
class Client:
 def __init__(self,id=None):
  self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  if id:self.call('/demo/login/'+id,'POST',{})
 def call(self,path,method='GET',body=None,status=200):
  req=urllib.request.Request(BASE+'/api'+path,data=None if body is None else json.dumps(body).encode(),method=method,headers={'X-Workflow-Client':'portal','Content-Type':'application/json'})
  try:
   with self.opener.open(req,timeout=20) as res:code=res.status;data=res.read()
  except urllib.error.HTTPError as res:code=res.code;data=res.read()
  assert code==status,(path,code,data[:600]);return json.loads(data) if data else None
admin=Client('admin');stamp=uuid.uuid4().hex[:10];checks=0

def passed(message):
 global checks
 checks+=1;print('PASS',message,flush=True)
accounts=admin.call('/organization/accounts');me=next(a for a in accounts if a['id']=='admin')
Client('north-reviewer').call('/organization/accounts',status=403)
admin.call('/organization/accounts/admin','PUT',{**me,'writeAccess':'none'},400)
passed('organization administration is HQ-only and self-lockout is prevented')
account=admin.call('/organization/accounts','POST',dict(id='org-check-'+stamp,name='Organization check',role='Admin',unit='north',providerId=None,active=True,readAccess='subtree',writeAccess='own',version=0))
branch=Client(account['id'])
assert {u['key'] for u in branch.call('/org-units')}=={'north','north-haifa'}
branch.call('/organization/accounts',status=403)
passed('membership and independent read/write scopes are persisted')
provider=next(p for p in admin.call('/providers') if p['unit']=='north')
other=next(p for p in admin.call('/providers') if p['unit']=='care')
definition=dict(name='Organization checks',initialState='draft',fields=[dict(key='notes',label='Notes',type='text',required=False,viewRoles=['Admin','Reviewer'],editRoles=['Admin'],editStates=['draft'])],states=[dict(key='draft',label='Draft',terminal=False),dict(key='done',label='Done',terminal=True)],transitions=[dict(key='finish',label='Finish',from_='draft',to='done',roles=['Admin'],guard='none',effects=[])],documents=["Proof"])
definition['transitions'][0]['from']=definition['transitions'][0].pop('from_')
key='org-'+stamp
process=admin.call('/processes','POST',dict(key=key,baseNumber=0,definition=definition))
key2=key+'-other';process2=admin.call('/processes','POST',dict(key=key2,baseNumber=0,definition=definition))
rule=dict(name='Org routing '+stamp,processKey=key,priority=1,enabled=True,targetUnit='north-haifa',spec=dict(match='all',conditions=[]),version=0)
r=admin.call('/routing/rules','POST',rule)
admin.call('/routing/rules','POST',{**rule,'processKey':None},400)
admin.call('/routing/rules','POST',{**rule,'spec':dict(match='all',conditions=[dict(field='data.unknown',operator='equals',value='x')])},400)
passed('new routing rules require an existing process and its field catalog')
payload=dict(providerId=provider['id'],processVersionId=process['id'],title='Organization check '+stamp,data={'notes':'test'})
assert admin.call('/routing/simulate','POST',payload)['unit']=='north-haifa'
assert admin.call('/routing/simulate','POST',{**payload,'processVersionId':process2['id']})['unit']=='north'
passed('routing rule applies only to its process, including simulation')
child=admin.call('/cases','POST',payload)
parent=admin.call('/cases','POST',{**payload,'processVersionId':process2['id']})
outside=admin.call('/cases','POST',{**payload,'processVersionId':process2['id'],'providerId':other['id']})
childview=branch.call('/cases/'+str(child['id']))
assert not childview['canWrite'] and childview['actions']==[] and all(not f['editable'] for f in childview['fields'])
branch.call('/cases/'+str(child['id'])+'/data','PUT',dict(version=child['version'],data={'notes':'forbidden'}),403)
branch.call('/cases/'+str(child['id'])+'/actions/finish','POST',dict(version=child['version']),403)
branch.call('/cases/'+str(outside['id']),status=403)
parent=branch.call('/cases/'+str(parent['id'])+'/data','PUT',dict(version=parent['version'],data={'notes':'allowed'}))
passed('own-unit write permits parent updates and blocks child writes and other branches')
account=admin.call('/organization/accounts/'+account['id'],'PUT',{**account,'writeAccess':'none'})
assert branch.call('/cases/'+str(parent['id']))['actions']==[]
branch.call('/cases/'+str(parent['id'])+'/data','PUT',dict(version=parent['version'],data={'notes':'forbidden'}),403)
branch.call('/cases','POST',{**payload,'processVersionId':process2['id']},403)
passed('read-only changes take effect on existing sessions and block all writes')
account=admin.call('/organization/accounts/'+account['id'],'PUT',{**account,'readAccess':'own','writeAccess':'own'})
branch.call('/cases/'+str(child['id']),status=403)
assert {u['key'] for u in branch.call('/org-units')}=={'north'}
admin.call('/organization/accounts/'+account['id'],'PUT',{**account,'readAccess':'own','writeAccess':'subtree'},400)
admin.call('/organization/accounts/'+account['id'],'PUT',{**account,'version':account['version']-1},409)
passed('own-unit read, invalid write expansion and concurrent edits are enforced')
account=admin.call('/organization/accounts/'+account['id'],'PUT',{**account,'readAccess':'subtree','writeAccess':'subtree'})
reviewers=branch.call('/cases/'+str(child['id'])+'/reviewers')
assert 'hq-reviewer' not in {a['id'] for a in reviewers}
branch.call('/cases/'+str(child['id'])+'/assign','POST',dict(version=child['version'],assigneeId='hq-reviewer'),400)
passed('handler assignment stays inside the caller subtree')
account=admin.call('/organization/accounts/'+account['id'],'PUT',{**account,'active':False})
branch.call('/cases',status=403)
passed('deactivated users lose access immediately')
units=admin.call('/org-units');north=next(u for u in units if u['key']=='north')
admin.call('/org-units/north','PUT',{**north,'parentKey':'north-haifa'},400)
passed('hierarchy cycles are rejected')
admin.call('/routing/rules/'+str(r['id']),'PUT',{**r,'enabled':False})
print(f'{checks} organization integration checks passed')

