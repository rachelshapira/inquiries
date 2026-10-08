import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { CaseSummary, Provider } from '../../core/models';
import { businessTitle, stateLabel } from '../../shared/presentation';
@Component({ selector:'app-providers', imports:[ReactiveFormsModule,RouterLink], templateUrl:'./providers.html', changeDetection:ChangeDetectionStrategy.OnPush })
export class Providers implements OnInit {
 readonly businessTitle=businessTitle; readonly stateLabel=stateLabel;
 readonly api=inject(Api); readonly items=signal<Provider[]>([]); readonly cases=signal<CaseSummary[]>([]); readonly selected=signal<Provider|null>(null); readonly editing=signal(false);
 readonly form=new FormGroup({name:new FormControl('',{nonNullable:true,validators:Validators.required}),registration:new FormControl('',{nonNullable:true,validators:Validators.pattern(/^[0-9]{5,12}$/)}),unit:new FormControl('care',{nonNullable:true,validators:Validators.required}),branches:new FormControl('',{nonNullable:true}),contactName:new FormControl('',{nonNullable:true}),email:new FormControl('',{nonNullable:true,validators:Validators.email}),phone:new FormControl('',{nonNullable:true}),agreement:new FormControl('',{nonNullable:true})});
 ngOnInit(){void this.api.run(async()=>{this.items.set(await this.api.request('/providers'));this.cases.set(await this.api.request('/cases'));});}
 open(p:Provider|null){this.selected.set(p);this.editing.set(true);this.form.reset({unit:'care',...p});if(this.api.session()?.user?.role!=='Admin')this.form.disable();}
 save(){if(this.form.invalid)return;void this.api.run(async()=>{const p=this.selected();await this.api.request(p?'/providers/'+p.id:'/providers',p?'PUT':'POST',{...this.form.getRawValue(),version:p?.version??0});this.editing.set(false);this.ngOnInit();this.api.notice.set('תיק נותן השירות נשמר');});}
 related(){return this.cases().filter(c=>c.providerId===this.selected()?.id);}
}
