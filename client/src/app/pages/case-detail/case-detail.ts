import { ProcessGraph } from '../../shared/process-graph/process-graph';
import { businessTitle, stateLabel } from '../../shared/presentation';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  viewChild,
  ElementRef,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { Action, BusinessOperation, CaseDetail, ReviewTask, Definition, Process } from '../../core/models';
@Component({
  selector: 'app-case-detail',
  imports: [ProcessGraph, RouterLink, DatePipe, ReactiveFormsModule],
  templateUrl: './case-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaseDetailPage implements OnInit {
  readonly businessTitle = businessTitle;
  readonly api = inject(Api);
  readonly route = inject(ActivatedRoute);
  readonly graphState = signal('');
  readonly processDefinition = signal<Definition | null>(null);
  readonly visualDefinition = computed(() => {
    const definition = this.processDefinition();
    return definition ? { ...definition, states: definition.states.map((state) => ({ ...state, label: this.stateLabel(state.label) })) } : null;
  });
  readonly item = signal<CaseDetail | null>(null);
  readonly businessOperations = signal<BusinessOperation[]>([]);
  readonly pendingBusiness = computed(() => this.businessOperations().find(operation => operation.status !== 'sent'));
  refresh() { void this.api.run(() => this.load()); }
  readonly response = computed(() => this.item()?.response ?? null);
  readonly reviewers = signal<{ id: string; name: string; unit: string }[]>([]);
  readonly reason = new FormControl('', { nonNullable: true });
  readonly shareNote = new FormControl(false, { nonNullable: true });
  readonly assignee = new FormControl('', { nonNullable: true });
  readonly completedDocuments = computed(() => this.item()?.requiredDocuments.filter(kind => this.documentFor(kind)?.valid).length ?? 0);
  documentFor(kind: string) { return this.item()?.documents.find(d => d.kind === kind && d.current); }
  readonly editable = computed(() => this.item()?.fields.some((f) => f.editable));
  readonly requiresActionNote = computed(() => this.item()?.actions.find((a) => a.key !== 'cancel')?.needsReason);
  readonly documentsHeading = viewChild<ElementRef<HTMLElement>>('documentsHeading');
  jumpToDocuments() {
    const heading = this.documentsHeading()?.nativeElement;
    heading?.scrollIntoView({ block: 'start' });
    heading?.focus({ preventScroll: true });
  }
  readonly actionsHeading = viewChild<ElementRef<HTMLElement>>('actionsHeading');
  jumpToActions() {
    const heading = this.actionsHeading()?.nativeElement;
    heading?.scrollIntoView({ block: 'start' });
    heading?.focus({ preventScroll: true });
  }
  readonly activeTasks = computed(
    () => this.item()?.tasks.filter((t) => t.round === this.item()?.round) ?? [],
  );
  ngOnInit() {
    void this.api.run(async () => {
      await this.load();
      const processes = await this.api.request<Process[]>('/processes');
      this.processDefinition.set(
        processes.find((p) => p.id === this.item()?.processVersionId)?.definition ?? null,
      );
      if (
        this.item()?.canWrite &&
        ['Admin', 'Reviewer'].includes(this.api.session()?.user?.role ?? '')
      )
        this.reviewers.set(await this.api.request('/cases/' + this.item()!.id + '/reviewers'));
    });
  }
  async load() {
    this.setItem(
      await this.api.request<CaseDetail>('/cases/' + this.route.snapshot.paramMap.get('id')),
    );
    await this.loadBusinessOperations();
  }
  async loadBusinessOperations() {
    this.businessOperations.set(await this.api.request<BusinessOperation[]>('/cases/' + this.item()!.id + '/business-actions'));
  }
  setItem(item: CaseDetail) {
    this.item.set(item);
    this.shareNote.setValue(!!this.requiresActionNote());
    this.assignee.setValue(item.assigneeId ?? '');

  }
  act(action: Action) {
    void this.api.run(async () => {
      this.setItem(
        await this.api.request('/cases/' + this.item()!.id + '/actions/' + action.key, 'POST', {
          version: this.item()!.version,
          note: this.reason.value,
          shareNote: this.shareNote.value,
        }),
      );
      await this.loadBusinessOperations();
      this.reason.setValue('');
      this.api.notice.set('הפעולה בוצעה ותועדה');
    });
  }
  review(task: ReviewTask, result: string, note: string) {
    void this.api.run(async () => {
      this.setItem(
        await this.api.request('/cases/' + this.item()!.id + '/tasks/' + task.id, 'POST', {
          version: this.item()!.version,
          result,
          note,
        }),
      );
    });
  }
  assign() {
    void this.api.run(async () => {
      this.setItem(
        await this.api.request('/cases/' + this.item()!.id + '/assign', 'POST', {
          version: this.item()!.version,
          assigneeId: this.assignee.value,
        }),
      );
    });
  }
  stateName(key: string) {
    return this.stateLabel(this.processDefinition()?.states.find((s) => s.key === key)?.label ?? key);
  }
  stateLabel(label: string) {
    return stateLabel(label);
  }
  outgoing(key: string) {
    return this.processDefinition()?.transitions.filter((t) => t.from === key) ?? [];
  }
  documentName(id: number) {
    const d = this.item()?.documents.find((d) => d.id === id);
    return d ? `${d.kind} · גרסה ${d.number}` : 'מסמך';
  }
  mayReview() {
    return (
      ['Admin', 'Reviewer'].includes(this.api.session()?.user?.role ?? '') &&
      this.item()?.actions.some((a) => a.key === 'recommend')
    );
  }
}
