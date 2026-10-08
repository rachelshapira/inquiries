import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { ReviewTask, CaseSummary } from '../../core/models';
import { businessTitle } from '../../shared/presentation';
@Component({selector:'app-tasks',imports:[RouterLink,DatePipe],templateUrl:'./tasks.html',changeDetection:ChangeDetectionStrategy.OnPush})
export class Tasks implements OnInit{
 readonly api=inject(Api);readonly items=signal<ReviewTask[]>([]);readonly cases=signal<CaseSummary[]>([]);readonly onlyOpen=signal(true);readonly filtered=computed(()=>this.items().filter(t=>!this.onlyOpen()||t.status==='open'));
 ngOnInit(){void this.api.run(async()=>{const [tasks,cases]=await Promise.all([this.api.request<ReviewTask[]>('/tasks'),this.api.request<CaseSummary[]>('/cases')]);this.items.set(tasks);this.cases.set(cases);});}
 title(id:number){return businessTitle(this.cases().find(c=>c.id===id)?.title??'פנייה #'+id);}
 overdue(t:ReviewTask){return t.status==='open'&&new Date(t.dueAt)<new Date();}
}
