import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
  viewChild,
  ElementRef,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  FormControl,
  FormGroup,
  FormRecord,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { CaseSummary, CaseDetail, Provider, Process, Field } from '../../core/models';
import { businessTitle, stateLabel } from '../../shared/presentation';
@Component({
  selector: 'app-cases',
  imports: [RouterLink, DatePipe, ReactiveFormsModule],
  templateUrl: './cases.html',
  styleUrl: './cases.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Cases implements OnInit {
  readonly businessTitle = businessTitle;
  readonly stateLabel = stateLabel;
  readonly api = inject(Api);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);
  readonly destroyRef = inject(DestroyRef);
  readonly items = signal<CaseSummary[]>([]);
  readonly providers = signal<Provider[]>([]);
  readonly processes = signal<Process[]>([]);
  readonly editing = signal(this.route.snapshot.queryParamMap.has('edit'));
  readonly showNew = signal(this.route.snapshot.queryParamMap.has('new') || this.editing());
  readonly search = signal('');
  readonly state = signal('');
  readonly creationFields = signal<Field[]>([]);
  readonly creationDocuments = signal<string[]>([]);
  readonly draft = signal<CaseDetail | null>(null);
  readonly step = signal(0);
  readonly documentStep = computed(() => this.creationDocuments().length > 0 && (!this.editing() || !!this.draft()?.canUpload));
  readonly steps = computed(() => ['פרטי הפנייה', ...(this.documentStep() ? ['מסמכים'] : []), this.editing() ? 'בדיקה וסיום' : 'בדיקה והגשה']);
  readonly stepHeading = viewChild<ElementRef<HTMLElement>>('stepHeading');
  readonly selectedFiles = signal<Record<string, File>>({});
  readonly completedDocuments = computed(() => this.creationDocuments().filter(kind => this.documentFor(kind)?.valid).length);
  readonly submissionActions = computed(() => {
    const c = this.draft();
    const definition = this.processes().find(p => p.id === c?.processVersionId)?.definition;
    return c?.actions.filter(a => definition?.transitions.some(t => t.key === a.key && t.from === c.state && t.guard === 'submission')) ?? [];
  });
  documentFor(kind: string) { return this.draft()?.documents.find(d => d.kind === kind && d.current); }
  stepDone(index: number) { return index === 0 ? !!this.draft() && !this.data.dirty : index === 1 && this.creationDocuments().length > 0 && this.completedDocuments() === this.creationDocuments().length; }
  beginNew() {
    this.editing.set(false);
    this.draft.set(null); this.selectedFiles.set({}); this.step.set(0);
    this.form.controls.title.reset(); this.prepareFields(); this.showNew.set(true);
    void this.router.navigate(['/cases'], { queryParams: { new: 1 }, replaceUrl: true });
  }
  closeEditor() {
    if (this.editing() && this.draft()) { void this.router.navigate(['/cases', this.draft()!.id]); return; }
    this.showNew.set(false);
    void this.router.navigate(['/cases'], { replaceUrl: true });
  }
  goStep(step: number) {
    this.step.set(step);
    setTimeout(() => this.stepHeading()?.nativeElement.focus());
  }
  readonly states = computed(() => [
    ...new Map(this.items().map((i) => [i.state, i.stateLabel])).entries(),
  ]);
  readonly filtered = computed(() =>
    this.items().filter(
      (i) =>
        (!this.state() || i.state === this.state()) &&
        `${i.id} ${i.title} ${i.providerName}`.includes(this.search()),
    ),
  );
  readonly data = new FormRecord<FormControl<string>>({});
  readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)],
    }),
    providerId: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
    processVersionId: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }),
  });
  ngOnInit() {
    this.form.controls.processVersionId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.prepareFields());
    let initialParams = true;
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      if (initialParams) { initialParams = false; return; }
      if (!params.has('new') && !params.has('edit') && this.showNew()) {
        this.showNew.set(false); this.editing.set(false); this.draft.set(null);
        void this.api.run(async () => {
          this.items.set(await this.api.request<CaseSummary[]>('/cases'));
          await this.api.loadCreationOptions();
        });
      }
    });
    void this.api.run(async () => {
      const editId = this.route.snapshot.queryParamMap.get('edit');
      if (editId) {
        const [c, processes] = await Promise.all([
          this.api.request<CaseDetail>('/cases/' + editId), this.api.request<Process[]>('/processes'),
        ]);
        if (!c.fields.some(f => f.editable) && !(c.canUpload && c.requiredDocuments.length)) {
          await this.router.navigate(['/cases', c.id]); return;
        }
        this.processes.set(processes);
        this.form.patchValue({ title: c.title, providerId: c.providerId, processVersionId: c.processVersionId });
        this.setDraft(c);
        this.goStep(this.route.snapshot.queryParamMap.get('step') === 'documents' && this.documentStep() ? 1 : 0);
        return;
      }
      const [items, options] = await Promise.all([
        this.api.request<CaseSummary[]>('/cases'),
        this.api.loadCreationOptions(),
      ]);
      this.items.set(items);
      this.processes.set(options.processes);
      this.form.patchValue({
        processVersionId: this.processes()[0]?.id ?? 0,
      });
      const draftId = this.route.snapshot.queryParamMap.get('draft');
      if (draftId && this.showNew()) {
        const c = await this.api.request<CaseDetail>('/cases/' + draftId);
        const process = this.processes().find(p => p.id === c.processVersionId);
        if (!process || c.state !== process.definition.initialState) {
          await this.router.navigate(['/cases', c.id]);
          return;
        }
        this.form.patchValue({ title: c.title, providerId: c.providerId, processVersionId: c.processVersionId });
        this.setDraft(c);
        this.goStep(this.creationDocuments().length ? 1 : this.steps().length - 1);
      }
    });
  }
  prepareFields() {
    if (this.draft() || this.editing()) return;
    const allowed = this.api.creationOptions()?.eligibility.find(e => e.processVersionId === this.form.controls.processVersionId.value)?.providerIds ?? [];
    this.providers.set(this.api.creationOptions()?.providers.filter(p => allowed.includes(p.id)) ?? []);
    if (!allowed.includes(this.form.controls.providerId.value)) this.form.controls.providerId.setValue(this.providers()[0]?.id ?? 0);
    const definition = this.processes().find(
      (p) => p.id === this.form.controls.processVersionId.value,
    )?.definition;
    const fields =
      definition?.fields.filter(
        (f) =>
          f.editRoles.includes(this.api.session()?.user?.role ?? '') &&
          f.editStates.includes(definition.initialState),
      ) ?? [];
    this.creationFields.set(fields);
    this.creationDocuments.set(definition?.documents ?? []);
    Object.keys(this.data.controls).forEach((k) => this.data.removeControl(k));
    fields.forEach((f) => this.data.addControl(f.key, new FormControl('', { nonNullable: true })));
  }
  create() {
    this.form.markAllAsTouched();
    if (this.form.invalid || (!this.editing() && !this.api.canCreate())) return;
    void this.api.run(async () => {
      if (this.draft()) {
        if (this.creationFields().length) this.setDraft(await this.api.request<CaseDetail>('/cases/' + this.draft()!.id + '/data', 'PUT', {
          version: this.draft()!.version, data: this.data.getRawValue(),
        }));
        this.goStep(1);
        return;
      }
      const item = await this.api.request<CaseDetail & { canView?: boolean; message?: string }>(
        '/cases',
        'POST',
        { ...this.form.getRawValue(), data: this.data.getRawValue() },
      );
      if (item.canView === false) {
        this.api.notice.set(item.message ?? 'הפנייה נוצרה ונותבה ליחידה המטפלת');
        this.showNew.set(false);
        this.items.set(await this.api.request('/cases'));
      } else {
        this.setDraft(item);
        await this.router.navigate(['/cases'], { queryParams: { new: 1, draft: item.id }, replaceUrl: true });
        this.goStep(1);
      }
    });
  }
  setDraft(c: CaseDetail) {
    this.draft.set(c);
    this.creationDocuments.set(c.requiredDocuments);
    this.creationFields.set(c.fields.filter(f => f.editable).map(f => ({ ...f, editRoles: [], viewRoles: [], editStates: [] })));
    Object.keys(this.data.controls).forEach(k => this.data.removeControl(k));
    this.creationFields().forEach(f => this.data.addControl(f.key, new FormControl(c.data[f.key] ?? '', { nonNullable: true })));
    this.data.markAsPristine();
  }
  pick(event: Event, kind: string) {
    const file = (event.target as HTMLInputElement).files?.[0];
    this.selectedFiles.update(files => { const next = { ...files }; if (file) next[kind] = file; else delete next[kind]; return next; });
  }
  upload(kind: string, input: HTMLInputElement, validUntil: string) {
    const file = this.selectedFiles()[kind];
    if (!file) return;
    void this.api.run(async () => {
      const data = new FormData();
      data.append('file', file); data.append('kind', kind);
      data.append('version', String(this.draft()!.version)); data.append('validUntil', validUntil);
      this.draft.set(await this.api.request<CaseDetail>('/cases/' + this.draft()!.id + '/documents', 'POST', data));
      this.selectedFiles.update(files => { const next = { ...files }; delete next[kind]; return next; });
      input.value = '';
    });
  }
  submit(key: string) {
    void this.api.run(async () => {
      const c = await this.api.request<CaseDetail>('/cases/' + this.draft()!.id + '/actions/' + key, 'POST', { version: this.draft()!.version, note: '' });
      await this.router.navigate(['/cases', c.id]);
    });
  }
}
