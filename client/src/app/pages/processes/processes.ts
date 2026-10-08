import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { FormControl, FormRecord, ReactiveFormsModule } from '@angular/forms';
import { ProcessGraph } from '../../shared/process-graph/process-graph';
import { Routing } from '../routing/routing';
import { Icon } from '../../shared/icon/icon';
import { Api } from '../../core/api';
import {
  Definition,
  Field,
  Process,
  State,
  Transition,
  PayloadRoute,
  PayloadCondition,
  PayloadDecision,
} from '../../core/models';
@Component({
  selector: 'app-processes',
  imports: [ReactiveFormsModule, Icon, NgTemplateOutlet, ProcessGraph, Routing],
  templateUrl: './processes.html',
  host: { '(window:beforeunload)': 'warnBeforeUnload($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Processes implements OnInit {
  readonly api = inject(Api);
  readonly route = inject(ActivatedRoute);
  readonly items = signal<Process[]>([]);
  readonly definition = signal<Definition | null>(null);
  readonly base = signal(0);
  readonly key = new FormControl('', { nonNullable: true });
  readonly simulation = new FormControl('submit,recommend,approve', { nonNullable: true });
  readonly result = signal('');
  readonly simulated = signal(false);
  readonly editorStep = signal(1);
  readonly selectedState = signal('');
  readonly selectedTransition = signal<number | null>(null);
  readonly selectedProcessId = signal(0);
  readonly publishedKey = computed(
    () => this.items().find((p) => p.id === this.selectedProcessId())?.key ?? '',
  );
  readonly dirty = signal(false);
  readonly attempted = signal(false);
  readonly sampleData = new FormRecord<FormControl<string>>({});
  readonly testDecision = signal<PayloadDecision | null>(null);
  readonly expandedRoute = signal('');
  readonly payloadOperators = [
    { key: 'equals', label: 'שווה ל־' },
    { key: 'notEquals', label: 'שונה מ־' },
    { key: 'contains', label: 'מכיל' },
    { key: 'startsWith', label: 'מתחיל ב־' },
    { key: 'in', label: 'אחד הערכים' },
    { key: 'greaterThan', label: 'גדול מ־ / אחרי' },
    { key: 'greaterOrEqual', label: 'גדול או שווה / בתאריך או אחריו' },
    { key: 'lessThan', label: 'קטן מ־ / לפני' },
    { key: 'lessOrEqual', label: 'קטן או שווה / בתאריך או לפניו' },
    { key: 'exists', label: 'קיים ערך' },
  ];
  payloadField(key: string) {
    return this.definition()?.fields.find((f) => f.key === key);
  }
  allowedOperators(key: string) {
    const type = this.payloadField(key)?.type;
    return this.payloadOperators.filter(
      (o) =>
        o.key === 'exists' ||
        ['equals', 'notEquals'].includes(o.key) ||
        (type === 'number' || type === 'date'
          ? ['greaterThan', 'greaterOrEqual', 'lessThan', 'lessOrEqual'].includes(o.key)
          : ['contains', 'startsWith', 'in'].includes(o.key)),
    );
  }
  sortedRoutes(t: Transition) {
    return [...(t.routes ?? [])].sort(
      (a, b) => a.priority - b.priority || (a.key < b.key ? -1 : a.key > b.key ? 1 : 0),
    );
  }
  routeDescription(route: PayloadRoute) {
    return route.when.conditions
      .map(
        (c) =>
          `${this.payloadField(c.field)?.label ?? c.field} ${this.payloadOperators.find((o) => o.key === c.operator)?.label ?? c.operator} ${c.operator === 'exists' ? (c.value === 'true' ? 'כן' : 'לא') : c.value || '…'}`,
      )
      .join(route.when.match === 'all' ? ' וגם ' : ' או ');
  }
  addPayloadRoute() {
    const t = this.currentTransition();
    const index = this.selectedTransition();
    const field = this.definition()?.fields[0];
    if (!t || index === null || !field) return;
    const key = 'route' + Date.now();
    this.transition(index, {
      routes: [
        ...(t.routes ?? []),
        {
          key,
          label: 'מסלול מותנה חדש',
          to: t.to,
          priority: ((t.routes?.length ?? 0) + 1) * 10,
          when: { match: 'all', conditions: [{ field: field.key, operator: 'equals', value: '' }] },
        },
      ],
    });
    this.expandedRoute.set(key);
  }
  patchPayloadRoute(key: string, change: Partial<PayloadRoute>) {
    const t = this.currentTransition();
    const index = this.selectedTransition();
    if (!t || index === null) return;
    this.transition(index, {
      routes: (t.routes ?? []).map((r) => (r.key === key ? { ...r, ...change } : r)),
    });
  }
  removePayloadRoute(key: string) {
    const t = this.currentTransition();
    const index = this.selectedTransition();
    if (t && index !== null)
      this.transition(index, { routes: (t.routes ?? []).filter((r) => r.key !== key) });
  }
  routeMatch(route: PayloadRoute, match: string) {
    if (match === 'all' || match === 'any')
      this.patchPayloadRoute(route.key, { when: { ...route.when, match } });
  }
  routeCondition(route: PayloadRoute, index: number, change: Partial<PayloadCondition>) {
    if (change.field) {
      change.operator = 'equals';
      change.value = '';
    }
    if (change.operator === 'exists') change.value = 'true';
    this.patchPayloadRoute(route.key, {
      when: {
        ...route.when,
        conditions: route.when.conditions.map((c, i) => (i === index ? { ...c, ...change } : c)),
      },
    });
  }
  addRouteCondition(route: PayloadRoute) {
    const field = this.definition()?.fields[0];
    if (field)
      this.patchPayloadRoute(route.key, {
        when: {
          ...route.when,
          conditions: [
            ...route.when.conditions,
            { field: field.key, operator: 'equals', value: '' },
          ],
        },
      });
  }
  removeRouteCondition(route: PayloadRoute, index: number) {
    this.patchPayloadRoute(route.key, {
      when: { ...route.when, conditions: route.when.conditions.filter((_, i) => i !== index) },
    });
  }
  prepareSamples() {
    const fields = this.definition()?.fields ?? [];
    for (const key of Object.keys(this.sampleData.controls))
      if (!fields.some((f) => f.key === key)) this.sampleData.removeControl(key);
    for (const f of fields)
      if (!this.sampleData.controls[f.key])
        this.sampleData.addControl(f.key, new FormControl('', { nonNullable: true }));
    this.testDecision.set(null);
  }
  sampleChanged() {
    this.testDecision.set(null);
    this.simulated.set(false);
    this.result.set('');
  }
  async previewAction() {
    const d = this.definition();
    const t = this.currentTransition();
    if (!d || !t || this.issues().length) return;
    await this.api.run(async () => {
      const fingerprint = JSON.stringify({ d, t, data: this.sampleData.getRawValue() });
      const result = await this.api.request<PayloadDecision>('/processes/preview-action', 'POST', {
        definition: d,
        state: t.from,
        action: t.key,
        data: this.sampleData.getRawValue(),
      });
      if (
        fingerprint ===
        JSON.stringify({
          d: this.definition(),
          t: this.currentTransition(),
          data: this.sampleData.getRawValue(),
        })
      )
        this.testDecision.set(result);
    });
  }

  readonly discardOpen = signal(false);
  private discardResolve: ((proceed: boolean) => void) | undefined;
  private discardFocus: HTMLElement | null = null;
  canLeave(): boolean | Promise<boolean> {
    return this.dirty() ? this.confirmDiscard() : true;
  }
  confirmDiscard(): Promise<boolean> {
    return new Promise((resolve) => {
      this.discardResolve = resolve;
      this.discardFocus = document.activeElement as HTMLElement;
      this.discardOpen.set(true);
      requestAnimationFrame(() =>
        document.querySelector<HTMLButtonElement>('.discard-dialog button')?.focus(),
      );
    });
  }
  resolveDiscard(proceed: boolean) {
    const resolve = this.discardResolve;
    this.discardResolve = undefined;
    this.discardOpen.set(false);
    resolve?.(proceed);
    if (!proceed) requestAnimationFrame(() => this.discardFocus?.focus());
  }
  discardKeys(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.resolveDiscard(false);
      return;
    }
    if (event.key !== 'Tab') return;
    const buttons = [...document.querySelectorAll<HTMLButtonElement>('.discard-dialog button')];
    const first = buttons[0],
      last = buttons.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }
  warnBeforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  readonly previousStep = (step: number) => Math.max(step - 1, 0);
  selectById(id: number) {
    const process = this.items().find((p) => p.id === id);
    if (process) this.select(process);
  }
  roleNames(roles: string[]) {
    return roles.map((role) => this.api.role(role)).join(' · ');
  }
  readonly steps = ['פרטי התהליך', 'מפת התהליך', 'טופס ומסמכים', 'ניתוב ליחידות', 'בדיקה ופרסום'];
  readonly currentState = computed(() =>
    this.definition()?.states.find((s) => s.key === this.selectedState()),
  );
  readonly currentTransition = computed(() => {
    const i = this.selectedTransition();
    return i === null ? null : (this.definition()?.transitions[i] ?? null);
  });
  readonly reachable = computed(() => {
    const d = this.definition();
    const reached = new Set<string>();
    if (!d) return reached;
    reached.add(d.initialState);
    let changed = true;
    while (changed) {
      changed = false;
      for (const t of d.transitions)
        if (reached.has(t.from)) {
          for (const target of [t.to, ...(t.routes ?? []).map((r) => r.to)])
            if (!reached.has(target)) {
              reached.add(target);
              changed = true;
            }
        }
    }
    return reached;
  });
  readonly roles = ['Provider', 'Reviewer', 'Approver', 'Admin'];
  readonly guards = [
    { key: 'none', label: 'ללא תנאי נוסף' },
    { key: 'submission', label: 'שדות חובה ומסמכים נדרשים מלאים ובתוקף' },
    { key: 'reason', label: 'חובה לציין סיבה לפעולה' },
    { key: 'reviewsPassed', label: 'בדיקות המסמכים בסבב הנוכחי עברו בהצלחה' },
  ];
  readonly effects = [
    { key: 'newRound', label: 'יצירת סבב בדיקה' },
    { key: 'closeTasks', label: 'סגירת משימות פתוחות' },
    { key: 'sendMail', label: 'שליחת דואר אלקטרוני' },
  ];
  ngOnInit() {
    void this.api.run(async () => {
      this.items.set(await this.api.request('/processes'));
      if (!this.definition() && this.items()[0]) {
        const params = this.route.snapshot.queryParamMap;
        await this.select(
          this.items().find((p) => p.key === params.get('process')) ?? this.items()[0],
        );
        if (params.get('section') === 'routing') this.editorStep.set(3);
      }
    });
  }
  async select(p: Process) {
    if (this.dirty() && !(await this.confirmDiscard())) return;
    this.selectedProcessId.set(p.id);
    this.simulation.setValue(this.defaultPath(p.definition).join(','));
    this.selectedState.set(p.definition.initialState);
    this.selectedTransition.set(null);
    this.editorStep.set(1);
    this.dirty.set(false);
    this.attempted.set(false);
    this.definition.set(structuredClone(p.definition));
    Object.keys(this.sampleData.controls).forEach((k) => this.sampleData.removeControl(k));
    this.prepareSamples();
    this.base.set(p.number);
    this.key.setValue(p.key);
    this.simulated.set(false);
    this.result.set('');
  }
  clone() {
    if (!this.definition()) return;
    this.selectedProcessId.set(0);
    this.key.setValue('process-' + Date.now());
    this.base.set(0);
    this.patch({ name: 'עותק של ' + this.definition()!.name });
    this.editorStep.set(0);
  }
  async startNew() {
    if (this.dirty() && !(await this.confirmDiscard())) return;
    this.selectedProcessId.set(0);
    this.base.set(0);
    this.key.setValue('process-' + Date.now());
    this.definition.set({
      name: '',
      initialState: 'draft',
      states: [
        { key: 'draft', label: 'טיוטה', terminal: false },
        { key: 'review', label: 'בבדיקה', terminal: false },
        { key: 'approval', label: 'ממתין לאישור', terminal: false },
        { key: 'approved', label: 'אושרה', terminal: true },
      ],
      transitions: [
        {
          key: 'submit',
          label: 'הגשת הפנייה',
          from: 'draft',
          to: 'review',
          roles: ['Provider', 'Admin'],
          guard: 'submission',
          effects: ['newRound'],
        },
        {
          key: 'recommend',
          label: 'העברה לאישור',
          from: 'review',
          to: 'approval',
          roles: ['Admin', 'Reviewer'],
          guard: 'reviewsPassed',
          effects: [],
        },
        {
          key: 'approve',
          label: 'אישור הפנייה',
          from: 'approval',
          to: 'approved',
          roles: ['Admin', 'Approver'],
          guard: 'none',
          effects: ['closeTasks'],
        },
      ],
      fields: [
        {
          key: 'details',
          label: 'פרטי הבקשה',
          type: 'textarea',
          required: false,
          viewRoles: [...this.roles],
          editRoles: ['Provider', 'Admin'],
          editStates: ['draft'],
        },
      ],
      documents: ['מסמך נדרש'],
    });
    Object.keys(this.sampleData.controls).forEach((k) => this.sampleData.removeControl(k));
    this.prepareSamples();
    this.selectedState.set('draft');
    this.selectedTransition.set(null);
    this.editorStep.set(0);
    this.simulation.setValue('submit,recommend,approve');
    this.simulated.set(false);
    this.result.set('');
    this.dirty.set(true);
    this.attempted.set(false);
  }
  focusInspector() {
    if (matchMedia('(max-width:900px)').matches)
      requestAnimationFrame(() => {
        const heading = document.querySelector<HTMLElement>('.workflow-inspector h2');
        heading?.focus({ preventScroll: true });
        heading?.scrollIntoView({ block: 'start' });
      });
  }
  backToMap() {
    const selected = document.querySelector<SVGElement>(
      '.graph-state.selected',
    );
    selected?.focus({ preventScroll: true });
    selected?.scrollIntoView({ block: 'start' });
  }
  chooseState(key: string) {
    this.selectedState.set(key);
    this.selectedTransition.set(null);
    this.focusInspector();
  }
  outgoing(key: string) {
    return (
      this.definition()
        ?.transitions.map((transition, index) => ({ transition, index }))
        .filter((x) => x.transition.from === key) ?? []
    );
  }
  chooseTransition(index: number) {
    const t = this.definition()?.transitions[index];
    if (!t) return;
    this.selectedState.set(t.from);
    this.testDecision.set(null);
    this.selectedTransition.set(index);
    this.focusInspector();
  }
  changeCurrentState(value: Partial<State>) {
    const i = this.definition()?.states.findIndex((s) => s.key === this.selectedState()) ?? -1;
    if (i >= 0) this.state(i, value);
  }
  removeState() {
    const d = this.definition();
    const state = this.currentState();
    if (
      !d ||
      !state ||
      state.key === d.initialState ||
      d.transitions.some(
        (t) =>
          t.from === state.key || t.to === state.key || t.routes?.some((r) => r.to === state.key),
      )
    )
      return;
    this.patch({
      states: d.states.filter((s) => s.key !== state.key),
      fields: d.fields.map((f) => ({
        ...f,
        editStates: f.editStates.filter((k) => k !== state.key),
      })),
    });
    this.chooseState(d.initialState);
  }
  canRemoveState() {
    const d = this.definition();
    return (
      !!d &&
      this.selectedState() !== d.initialState &&
      !d.transitions.some(
        (t) =>
          t.from === this.selectedState() ||
          t.to === this.selectedState() ||
          t.routes?.some((r) => r.to === this.selectedState()),
      )
    );
  }
  defaultPath(definition: Definition) {
    const queue: { state: string; actions: string[] }[] = [
      { state: definition.initialState, actions: [] },
    ];
    const visited = new Set<string>();
    while (queue.length) {
      const current = queue.shift()!;
      if (visited.has(current.state)) continue;
      visited.add(current.state);
      if (definition.states.find((s) => s.key === current.state)?.terminal) return current.actions;
      for (const t of definition.transitions.filter(
        (t) => t.from === current.state && t.key !== 'cancel',
      ))
        queue.push({ state: t.to, actions: [...current.actions, t.key] });
    }
    return [];
  }
  guardName(key: string) {
    return this.guards.find((g) => g.key === key)?.label ?? key;
  }
  nextStep() {
    this.attempted.set(true);
    if (this.editorStep() === 0 && (!this.definition()?.name.trim() || !this.key.value.trim()))
      return;
    this.editorStep.update((s) => Math.min(s + 1, 4));
    this.attempted.set(false);
  }
  basicChanged() {
    this.dirty.set(true);
    this.simulated.set(false);
    this.result.set('');
  }
  issues() {
    const d = this.definition();
    if (!d) return [];
    const issues: string[] = [];
    if (!d.name.trim()) issues.push('יש לתת שם לתהליך.');
    if (!/^[a-z][a-z0-9-]{0,59}$/.test(this.key.value))
      issues.push('מזהה התהליך צריך להתחיל באות ולהכיל אותיות קטנות באנגלית, מספרים או מקפים.');
    if (!d.states.some((s) => s.terminal)) issues.push('יש להגדיר לפחות שלב סיום אחד.');
    for (const s of d.states) {
      if (!s.label.trim()) issues.push('יש לתת שם לכל שלב.');
      if (!this.reachable().has(s.key)) issues.push('אין מסלול שמוביל לשלב ' + s.label + '.');
      if (s.terminal && this.outgoing(s.key).length)
        issues.push('שלב סיום אינו יכול להכיל פעולות יוצאות: ' + s.label + '.');
    }
    for (const t of d.transitions) {
      if (!t.label.trim()) issues.push('יש לתת שם לכל פעולה.');
      if (!t.roles.length) issues.push('יש לבחור מי רשאי לבצע את הפעולה ' + t.label + '.');
      if (t.guard === 'submission' && !t.effects.includes('newRound'))
        issues.push('הפעולה ' + t.label + ' בודקת הגשה ולכן צריכה ליצור סבב בדיקה.');
    }
    for (const t of d.transitions)
      for (const route of t.routes ?? []) {
        if (!route.label.trim()) issues.push('יש לתת שם למסלול המותנה.');
        if (!Number.isInteger(route.priority) || route.priority < 0 || route.priority > 100000)
          issues.push('עדיפות המסלול ' + route.label + ' חייבת להיות מספר שלם בין 0 ל־100000.');
        if (!route.when.conditions.length)
          issues.push('יש להוסיף תנאי למסלול ' + route.label + '.');
        for (const c of route.when.conditions) {
          if (!this.payloadField(c.field))
            issues.push('שדה התנאי במסלול ' + route.label + ' אינו קיים בטופס.');
          if (!this.allowedOperators(c.field).some((o) => o.key === c.operator))
            issues.push('התנאי במסלול ' + route.label + ' אינו מתאים לסוג השדה.');
          if (!c.value.trim()) issues.push('חסר ערך לתנאי במסלול ' + route.label + '.');
        }
      }
    return issues;
  }

  patch(value: Partial<Definition>) {
    this.testDecision.set(null);
    this.dirty.set(true);
    this.definition.update((d) => (d ? { ...d, ...value } : d));
    this.simulated.set(false);
    this.result.set('');
    this.prepareSamples();
  }
  transition(i: number, value: Partial<Transition>) {
    this.patch({
      transitions: this.definition()!.transitions.map((t, n) => (n === i ? { ...t, ...value } : t)),
    });
  }
  field(i: number, value: Partial<Field>) {
    this.patch({
      fields: this.definition()!.fields.map((f, n) => (n === i ? { ...f, ...value } : f)),
    });
  }
  state(i: number, value: Partial<State>) {
    this.patch({
      states: this.definition()!.states.map((s, n) => (n === i ? { ...s, ...value } : s)),
    });
  }
  toggleRole(i: number, role: string, checked: boolean) {
    const roles = this.definition()!.transitions[i].roles;
    this.transition(i, { roles: checked ? [...roles, role] : roles.filter((r) => r !== role) });
  }
  toggleEffect(i: number, effect: string, checked: boolean) {
    const effects = this.definition()!.transitions[i].effects;
    this.transition(i, {
      effects: checked ? [...effects, effect] : effects.filter((e) => e !== effect),
    });
  }
  addTransition() {
    const d = this.definition();
    const state = this.currentState();
    if (!d || !state || state.terminal) return;
    const index = d.transitions.length;
    this.patch({
      transitions: [
        ...d.transitions,
        {
          key: 'action' + Date.now(),
          label: 'פעולה חדשה',
          from: state.key,
          to: d.states.find((s) => s.key !== state.key)?.key ?? state.key,
          roles: ['Admin'],
          guard: 'none',
          effects: [],
        },
      ],
    });
    this.testDecision.set(null);
    this.selectedTransition.set(index);
    this.focusInspector();
  }
  addField() {
    const d = this.definition()!;
    this.patch({
      fields: [
        ...d.fields,
        {
          key: 'field' + Date.now(),
          label: 'שדה חדש',
          type: 'text',
          required: false,
          viewRoles: [...this.roles],
          editRoles: ['Provider', 'Admin'],
          editStates: [d.initialState, 'corrections'].filter((s) =>
            d.states.some((x) => x.key === s),
          ),
        },
      ],
    });
  }
  addState() {
    const key = 'state' + Date.now();
    this.patch({
      states: [...this.definition()!.states, { key, label: 'שלב חדש', terminal: false }],
    });
    this.chooseState(key);
  }
  removeTransition(i: number) {
    this.selectedTransition.set(null);
    this.patch({ transitions: this.definition()!.transitions.filter((_, n) => n !== i) });
  }
  removeField(i: number) {
    const key = this.definition()?.fields[i]?.key;
    if (
      this.definition()?.transitions.some((t) =>
        t.routes?.some((r) => r.when.conditions.some((c) => c.field === key)),
      )
    ) {
      this.api.error.set('השדה משמש בתנאי מעבר. יש להסיר את התנאים לפני מחיקת השדה.');
      return;
    }
    this.patch({ fields: this.definition()!.fields.filter((_, n) => n !== i) });
  }
  toggleFieldPermission(
    i: number,
    property: 'viewRoles' | 'editRoles' | 'editStates',
    value: string,
    checked: boolean,
  ) {
    const f = this.definition()?.fields[i];
    if (!f) return;
    const patch: Partial<Field> = {
      [property]: checked
        ? [...new Set([...f[property], value])]
        : f[property].filter((v) => v !== value),
    };
    if (property === 'editRoles' && checked)
      patch.viewRoles = [...new Set([...f.viewRoles, value])];
    if (property === 'viewRoles' && !checked)
      patch.editRoles = f.editRoles.filter((v) => v !== value);
    this.field(i, patch);
  }
  simulationStep(i: number, value: string) {
    const steps = this.list(this.simulation.value);
    steps[i] = value;
    this.simulation.setValue(steps.join(','));
    this.simulated.set(false);
    this.result.set('');
  }
  addSimulationStep() {
    const d = this.definition();
    if (!d) return;
    const steps = this.list(this.simulation.value);
    let current = d.initialState;
    for (const action of steps) {
      const transition = d.transitions.find((t) => t.key === action && t.from === current);
      if (transition) current = transition.to;
    }
    const next = d.transitions.find((t) => t.from === current);
    if (!next) {
      this.api.notice.set('אין מעבר נוסף מהשלב האחרון במסלול.');
      return;
    }
    this.simulation.setValue([...steps, next.key].join(','));
    this.simulated.set(false);
    this.result.set('');
  }
  removeSimulationStep() {
    this.simulation.setValue(this.list(this.simulation.value).slice(0, -1).join(','));
    this.simulated.set(false);
    this.result.set('');
  }
  documents(value: string) {
    this.patch({
      documents: value
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean),
    });
  }
  list(value: string) {
    return value
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
  }
  stateName(key: string) {
    return this.definition()?.states.find((s) => s.key === key)?.label ?? key;
  }
  simulate() {
    this.attempted.set(true);
    if (this.issues().length) return;
    void this.api.run(async () => {
      const checked = JSON.stringify({
        key: this.key.value,
        definition: this.definition(),
        actions: this.simulation.value,
        data: this.sampleData.getRawValue(),
      });
      const result = await this.api.request<{ trace: string[]; note: string }>(
        '/processes/simulate',
        'POST',
        {
          definition: this.definition(),
          actions: this.list(this.simulation.value),
          data: this.sampleData.getRawValue(),
        },
      );
      if (
        checked !==
        JSON.stringify({
          key: this.key.value,
          definition: this.definition(),
          actions: this.simulation.value,
          data: this.sampleData.getRawValue(),
        })
      ) {
        this.api.notice.set('ההגדרה השתנתה במהלך הבדיקה. יש לבדוק שוב לפני פרסום.');
        return;
      }
      this.result.set(result.trace.map((s) => this.stateName(s)).join(' ← ') + ' · ' + result.note);
      this.simulated.set(true);
    });
  }
  publish() {
    if (!this.simulated() || this.issues().length) return;
    void this.api.run(async () => {
      const submitted = JSON.stringify({ key: this.key.value, definition: this.definition() });
      const published = await this.api.request<{ id: number; number: number }>(
        '/processes',
        'POST',
        { key: this.key.value, baseNumber: this.base(), definition: this.definition() },
      );
      this.selectedProcessId.set(published.id);
      this.base.set(published.number);
      this.dirty.set(
        submitted !== JSON.stringify({ key: this.key.value, definition: this.definition() }),
      );
      this.simulated.set(false);
      this.api.notice.set(
        'גרסה ' + published.number + ' פורסמה. פניות קיימות נשארות בגרסה המקורית.',
      );
      this.ngOnInit();
    });
  }
}
