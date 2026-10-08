"""Payload transition integration checks. Use an isolated local demo at port 5081."""
import copy, http.cookiejar, json, sys, urllib.request, urllib.error, uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5081').rstrip('/')
class Client:
 def __init__(self,account):
  self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
  self.call('/demo/login/'+account,'POST',{})
 def call(self,path,method='GET',body=None,status=200):
  data=None if body is None else json.dumps(body).encode()
  request=urllib.request.Request(BASE+'/api'+path,data=data,method=method,headers={'X-Workflow-Client':'portal','Content-Type':'application/json'})
  try:
   with self.opener.open(request,timeout=20) as response:code=response.status;value=response.read()
  except urllib.error.HTTPError as error:code=error.code;value=error.read()
  assert code==status,(path,code,value.decode()[:800])
  return json.loads(value) if value else None
admin=Client('admin');provider=Client('provider');checks=0
def passed(name):
 global checks
 checks+=1;print('PASS',name,flush=True)
def condition(field,operator,value):return dict(field=field,operator=operator,value=value)
def route(key,label,to,priority,conditions,match='all'):return dict(key=key,label=label,to=to,priority=priority,when=dict(match=match,conditions=conditions))
def field(key,type):return dict(key=key,label=key,type=type,required=False,viewRoles=['Admin','Provider'],editRoles=['Admin','Provider'],editStates=['draft'])
definition=dict(name='Payload checks',initialState='draft',fields=[field('amount','number'),field('urgency','text'),field('deadline','date'),field('email','email')],states=[dict(key='draft',label='Draft',terminal=False),dict(key='standard',label='Standard',terminal=True),dict(key='manager',label='Manager',terminal=True),dict(key='urgent',label='Urgent',terminal=True)],documents=['test document'],transitions=[dict(key='continue',label='Continue',from_='draft',to='standard',roles=['Admin'],guard='none',effects=[],routes=[route('large','Large amount','manager',10,[condition('amount','greaterOrEqual','10000')]),route('urgent','Urgent inquiry','urgent',20,[condition('urgency','contains','urgent')])])])
transition=definition['transitions'][0];transition['from']=transition.pop('from_')
def preview(data,d=None,status=200):return admin.call('/processes/preview-action','POST',dict(definition=d or definition,state='draft',action='continue',data=data),status)
assert preview({'amount':'9999.99'})['to']=='standard'
assert preview({'amount':'10000.00'})['to']=='manager'
assert preview({'amount':'10001','urgency':'URGENT'})['routeKey']=='large'
assert preview({'amount':'1','urgency':'very URGENT'})['to']=='urgent'
passed('typed decimal boundary, text comparison and first matching priority')
test=copy.deepcopy(definition);test['transitions'][0]['routes'][0]['when']['conditions']=[condition('amount','notEquals','10000')]
assert preview({},test)['to']=='standard'
assert preview({'amount':'10000.00'},test)['to']=='standard'
assert preview({'amount':'1'},test)['to']=='manager'
test['transitions'][0]['routes'][0]['when']['conditions']=[condition('amount','exists','false')]
assert preview({},test)['to']=='manager'
passed('missing fields do not match negative comparisons; exists is explicit')
test=copy.deepcopy(definition);test['transitions'][0]['routes'][0]['when']['conditions']=[condition('amount','greaterThan','10000'),condition('urgency','equals','urgent')]
assert preview({'amount':'20000','urgency':'normal'},test)['to']=='standard'
assert preview({'amount':'20000','urgency':'urgent'},test)['to']=='manager'
test['transitions'][0]['routes'][0]['when']['match']='any'
assert preview({'amount':'20000','urgency':'normal'},test)['to']=='manager'
passed('AND and OR over distinct payload fields')
test=copy.deepcopy(definition);test['transitions'][0]['routes'][0]['when']['conditions']=[condition('deadline','lessThan','2030-01-01')]
assert preview({'deadline':'2029-12-31'},test)['to']=='manager'
assert preview({'deadline':'2030-01-01'},test)['to']=='standard'
test['transitions'][0]['routes'][0]['when']['conditions']=[condition('email','contains','example.org')]
assert preview({'email':'test@example.org'},test)['to']=='manager'
passed('date ordering and email substring conditions')
for bad in [condition('unknown','equals','x'),condition('amount','contains','1'),condition('deadline','greaterThan','tomorrow')]:
 test=copy.deepcopy(definition);test['transitions'][0]['routes'][0]['when']['conditions']=[bad];preview({},test,400)
preview({'amount':'not a number'},status=400);preview({'deadline':'invalid'},status=400);preview({'unlisted':'value'},status=400)
passed('catalog, operator, constant and payload validation on the server')
test=copy.deepcopy(definition);test['transitions'][0]['routes'][1]['priority']=10;test['transitions'][0]['routes'][1]['key']='aaa'
assert preview({'amount':'20000','urgency':'urgent'},test)['to']=='urgent'
passed('equal priorities have a deterministic identifier tie-break')
stamp=uuid.uuid4().hex[:8];key='payload-check-'+stamp
version=admin.call('/processes','POST',dict(key=key,baseNumber=0,definition=definition))
p=next(p for p in admin.call('/providers') if p['registration']=='515001234')
case=admin.call('/cases','POST',dict(providerId=p['id'],processVersionId=version['id'],title='Payload check '+stamp,data={'amount':'1'}))
assert case['state']=='draft' and case['actions'][0]['to']=='standard'
case=admin.call('/cases/'+str(case['id'])+'/data','PUT',dict(version=case['version'],data={'amount':'20000'}))
assert case['state']=='draft' and case['actions'][0]['to']=='manager'
passed('saving payload stays in the same state and refreshes the predicted destination')
provider.call(f'/cases/{case["id"]}/actions/continue','POST',dict(version=case['version'],note=''),403)
case=admin.call(f'/cases/{case["id"]}/actions/continue','POST',dict(version=case['version'],note='test',to='standard',data={'amount':'1'}))
assert case['state']=='manager' and case['history'][0]['note'].startswith('test')
assert any('large' in h['note'] and 'payload' in h['note'] for h in case['history'])
passed('execution uses persisted payload, enforces roles and audits the selected route')
old=admin.call('/cases','POST',dict(providerId=p['id'],processVersionId=version['id'],title='Version check '+stamp,data={'amount':'20000'}))
changed=copy.deepcopy(definition);changed['transitions'][0]['routes'][0]['when']['conditions'][0]['value']='100000'
new_version=admin.call('/processes','POST',dict(key=key,baseNumber=1,definition=changed))
old=admin.call(f'/cases/{old["id"]}/actions/continue','POST',dict(version=old['version'],note=''))
new=admin.call('/cases','POST',dict(providerId=p['id'],processVersionId=new_version['id'],title='New version '+stamp,data={'amount':'20000'}))
new=admin.call(f'/cases/{new["id"]}/actions/continue','POST',dict(version=new['version'],note=''))
assert old['state']=='manager' and new['state']=='standard'
passed('existing inquiries keep the payload rules from their process version')
simulation=admin.call('/processes/simulate','POST',dict(definition=definition,actions=['continue'],data={'amount':'20000'}))
assert simulation['trace']==['draft','manager'] and simulation['decisions'][0]['routeKey']=='large'
passed('simulation and real execution agree on the chosen destination')
print(f'{checks} payload integration checks passed',flush=True)
