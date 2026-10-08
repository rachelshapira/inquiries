import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
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
import { businessTitle } from '../../shared/presentation';
import {
  Account,
  Field,
  OrgUnit,
  Process,
  Provider,
  RouteResult,
  RoutingCondition,
  RoutingRule,
} from '../../core/models';
@Component({
  selector: 'app-routing',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './routing.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Routing implements OnInit {
  readonly businessTitle = businessTitle;
  readonly embeddedProcess = input('');
  readonly previewProcesses = computed(() =>
    this.embeddedProcess()
      ? this.processes().filter((p) => p.key === this.embeddedProcess())
      : this.processes(),
  );
  readonly orgMode = inject(Router).url.startsWith('/organization');
  readonly processFilter = signal(
    inject(ActivatedRoute).snapshot.queryParamMap.get('process') ?? '',
  );
  readonly accounts = signal<Account[]>([]);
  readonly memberUnit = signal('hq');
  readonly editingAccount = signal(false);
  readonly selectedAccount = signal<Account | null>(null);
  readonly members = computed(() => this.accounts().filter((a) => a.unit === this.memberUnit()));
  readonly ruleFilter = signal('all');
  readonly visibleRules = computed(() =>
    this.rules().filter(
      (r) =>
        r.processKey === this.processFilter() &&
        (this.ruleFilter() === 'all' || r.enabled === (this.ruleFilter() === 'active')),
    ),
  );
  readonly section = signal<'rules' | 'units' | 'members' | 'preview'>(
    this.orgMode ? 'units' : 'rules',
  );
  readonly api = inject(Api);
  readonly destroy = inject(DestroyRef);
  readonly units = signal<OrgUnit[]>([]);
  readonly rules = signal<RoutingRule[]>([]);
  readonly processes = signal<Process[]>([]);
  readonly providers = signal<Provider[]>([]);
  readonly selectedRule = signal<RoutingRule | null>(null);
  readonly editingRule = signal(false);
  readonly conditions = signal<RoutingCondition[]>([]);
  readonly selectedUnit = signal<OrgUnit | null>(null);
  readonly editingUnit = signal(false);
  readonly preview = signal<RouteResult | null>(null);
  readonly previewFields = signal<Field[]>([]);
  readonly fields = computed(() => [
    { key: 'providerUnit', label: 'יחידת נותן השירות' },
    { key: 'providerId', label: 'מזהה נותן שירות' },
    { key: 'registration', label: 'מספר רישום' },
    { key: 'title', label: 'נושא הפנייה' },
    ...[
      ...new Map(
        this.processes()
          .filter((p) => p.key === this.processFilter())
          .flatMap((p) => p.definition.fields)
          .map((f) => [f.key, f]),
      ).values(),
    ].map((f) => ({ key: 'data.' + f.key, label: 'שדה בטופס: ' + f.label })),
  ]);
  readonly operators = [
    { key: 'equals', label: 'שווה ל־' },
    { key: 'notEquals', label: 'שונה מ־' },
    { key: 'contains', label: 'מכיל' },
    { key: 'startsWith', label: 'מתחיל ב־' },
    { key: 'in', label: 'אחד הערכים' },
    { key: 'exists', label: 'קיים ערך' },
    { key: 'greaterThan', label: 'גדול מ־' },
    { key: 'lessThan', label: 'קטן מ־' },
  ];
  readonly accountForm = new FormGroup({
    id: new FormControl('', { nonNullable: true, validators: Validators.required }),
    name: new FormControl('', { nonNullable: true, validators: Validators.required }),
    role: new FormControl('Reviewer', { nonNullable: true }),
    unit: new FormControl('hq', { nonNullable: true }),
    providerId: new FormControl<number | null>(null),
    active: new FormControl(true, { nonNullable: true }),
    readAccess: new FormControl('subtree', { nonNullable: true }),
    writeAccess: new FormControl('subtree', { nonNullable: true }),
  });
  readonly ruleForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: Validators.required }),
    priority: new FormControl(100, {
      nonNullable: true,
      validators: [Validators.min(0), Validators.max(100000)],
    }),
    enabled: new FormControl(true, { nonNullable: true }),
    targetUnit: new FormControl('', { nonNullable: true, validators: Validators.required }),
    match: new FormControl<'all' | 'any'>('all', { nonNullable: true }),
  });
  readonly unitForm = new FormGroup({
    key: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[a-z][a-z0-9-]{0,79}$/)],
    }),
    name: new FormControl('', { nonNullable: true, validators: Validators.required }),
    parentKey: new FormControl('hq', { nonNullable: true }),
  });
  readonly simulationData = new FormRecord<FormControl<string>>({});
  readonly simulationForm = new FormGroup({
    providerId: new FormControl(0, { nonNullable: true }),
    processVersionId: new FormControl(0, { nonNullable: true }),
    title: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });
  readonly sharedRules = computed(() => this.rules().filter((r) => !r.processKey));
  chooseProcess(key: string) {
    this.processFilter.set(key);
    this.editingRule.set(false);
  }
  processName() {
    return this.processes().find((p) => p.key === this.processFilter())?.definition.name ?? '';
  }
  parentOptions() {
    const key = this.selectedUnit()?.key;
    return this.units().filter((u) => !key || !this.subtree(key).includes(u.key));
  }
  subtree(key: string): string[] {
    const keys = [key];
    for (let i = 0; i < keys.length; i++)
      keys.push(
        ...this.units()
          .filter((u) => u.parentKey === keys[i])
          .map((u) => u.key),
      );
    return keys;
  }
  memberCount(key: string) {
    return this.accounts().filter((a) => a.unit === key && a.active).length;
  }
  showMembers(key: string) {
    this.memberUnit.set(key);
    this.section.set('members');
    this.editingAccount.set(false);
  }
  accessLabel(value: string) {
    return value === 'none'
      ? 'ללא הרשאה'
      : value === 'own'
        ? 'היחידה בלבד'
        : 'היחידה והיחידות שמתחתיה';
  }
  effectiveUnits(unit: string, access: string) {
    return access === 'none'
      ? 'אין יחידות'
      : (access === 'own' ? [unit] : this.subtree(unit)).map((k) => this.unitName(k)).join(' · ');
  }
  focusEditor(selector: string) {
    requestAnimationFrame(() => {
      const input = document.querySelector<HTMLInputElement>(selector + ' input');
      input?.focus({ preventScroll: true });
      input?.scrollIntoView({ block: 'center', behavior: 'auto' });
    });
  }
  alignAccess() {
    const read = this.accountForm.controls.readAccess.value;
    const write = this.accountForm.controls.writeAccess.value;
    if (read === 'none') this.accountForm.controls.writeAccess.setValue('none');
    else if (read === 'own' && write === 'subtree')
      this.accountForm.controls.writeAccess.setValue('own');
  }
  openAccount(account: Account | null) {
    this.selectedAccount.set(account);
    this.editingAccount.set(true);
    this.accountForm.reset({
      id: account?.id ?? '',
      name: account?.name ?? '',
      role: account?.role ?? 'Reviewer',
      unit: account?.unit ?? this.memberUnit(),
      providerId: account?.providerId ?? null,
      active: account?.active ?? true,
      readAccess: account?.readAccess ?? 'subtree',
      writeAccess: account?.writeAccess ?? 'subtree',
    });
    if (account) this.accountForm.controls.id.disable();
    else this.accountForm.controls.id.enable();
    this.focusEditor('[data-account-editor]');
  }
  saveAccount() {
    this.accountForm.markAllAsTouched();
    if (this.accountForm.invalid) return;
    void this.api.run(async () => {
      const account = this.selectedAccount();
      const value = this.accountForm.getRawValue();
      await this.api.request(
        account
          ? '/organization/accounts/' + encodeURIComponent(account.id)
          : '/organization/accounts',
        account ? 'PUT' : 'POST',
        { ...value, version: account?.version ?? 0 },
      );
      this.editingAccount.set(false);
      this.memberUnit.set(value.unit);
      await this.load();
      this.api.notice.set('שיוך המשתמש וההרשאות נשמרו. השינוי חל מיד בבקשה הבאה.');
    });
  }
  ngOnInit() {
    if (this.embeddedProcess()) this.processFilter.set(this.embeddedProcess());
    this.simulationForm.controls.processVersionId.valueChanges
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => this.preparePreview());
    if (this.api.session()?.user?.role === 'Admin' && this.api.session()?.user?.unit === 'hq')
      void this.api.run(() => this.load());
  }
  async load() {
    const [units, rules, processes, providers] = await Promise.all([
      this.api.request<OrgUnit[]>('/org-units'),
      this.api.request<RoutingRule[]>('/routing/rules'),
      this.api.request<Process[]>('/processes'),
      this.api.request<Provider[]>('/providers'),
    ]);
    this.accounts.set(await this.api.request<Account[]>('/organization/accounts'));
    this.units.set(units);
    this.rules.set(rules);
    this.processes.set(processes.filter((p, i, a) => a.findIndex((x) => x.key === p.key) === i));
    this.providers.set(providers);
    if (!this.processes().some((p) => p.key === this.processFilter()))
      this.processFilter.set(this.processes()[0]?.key ?? '');
    if (!this.simulationForm.controls.providerId.value)
      this.simulationForm.patchValue({
        providerId: providers[0]?.id ?? 0,
        processVersionId: this.previewProcesses()[0]?.id ?? 0,
      });
  }
  options(field: string) {
    if (field === 'processKey')
      return this.processes().map((p) => ({ value: p.key, label: p.definition.name }));
    if (field === 'providerUnit')
      return this.units().map((u) => ({ value: u.key, label: this.path(u) }));
    if (field === 'providerId')
      return this.providers().map((p) => ({ value: String(p.id), label: p.name }));
    return [];
  }
  readonly tree = computed(() => {
    const result: { unit: OrgUnit; depth: number }[] = [];
    const walk = (parent: string | null, depth: number) => {
      for (const unit of this.units().filter((u) => u.parentKey === parent)) {
        result.push({ unit, depth });
        walk(unit.key, depth + 1);
      }
    };
    walk(null, 0);
    return result;
  });
  unitName(key: string | null) {
    return this.units().find((u) => u.key === key)?.name ?? key ?? 'שורש';
  }
  path(unit: OrgUnit) {
    const names = [unit.name];
    let parent = unit.parentKey;
    const visited = new Set<string>([unit.key]);
    while (parent && !visited.has(parent)) {
      visited.add(parent);
      const node = this.units().find((u) => u.key === parent);
      if (!node) break;
      names.unshift(node.name);
      parent = node.parentKey;
    }
    return names.join(' / ');
  }
  openUnit(unit: OrgUnit | null) {
    this.selectedUnit.set(unit);
    this.editingUnit.set(true);
    this.unitForm.reset({
      key: unit?.key ?? '',
      name: unit?.name ?? '',
      parentKey: unit?.parentKey ?? 'hq',
    });
    if (unit) this.unitForm.controls.key.disable();
    else this.unitForm.controls.key.enable();
    this.focusEditor('[data-unit-editor]');
  }
  saveUnit() {
    this.unitForm.markAllAsTouched();
    if (this.unitForm.invalid) return;
    void this.api.run(async () => {
      const current = this.selectedUnit();
      const values = this.unitForm.getRawValue();
      await this.api.request(
        current ? '/org-units/' + current.key : '/org-units',
        current ? 'PUT' : 'POST',
        {
          ...values,
          parentKey: values.key === 'hq' ? null : values.parentKey,
          version: current?.version ?? 0,
        },
      );
      this.editingUnit.set(false);
      await this.load();
      this.api.notice.set('המבנה הארגוני נשמר. ההרשאות יחושבו מחדש בבקשה הבאה.');
    });
  }
  openRule(rule: RoutingRule | null) {
    this.selectedRule.set(rule);
    if (rule?.processKey) this.processFilter.set(rule.processKey);
    this.editingRule.set(true);
    this.ruleForm.reset({
      name: rule?.name ?? '',
      priority: rule?.priority ?? 100,
      enabled: rule?.enabled ?? true,
      targetUnit: rule?.targetUnit ?? '',
      match: rule?.spec.match ?? 'all',
    });
    this.conditions.set(structuredClone(rule?.spec.conditions ?? []));
    queueMicrotask(() =>
      document.querySelector<HTMLInputElement>('[data-rule-editor] input')?.focus(),
    );
  }
  condition(i: number, change: Partial<RoutingCondition>) {
    if (change.field) change.value = this.options(change.field)[0]?.value ?? '';
    if (change.operator === 'exists') change.value = 'true';
    this.conditions.update((list) => list.map((c, n) => (n === i ? { ...c, ...change } : c)));
  }
  addCondition() {
    this.conditions.update((list) => [
      ...list,
      { field: 'providerUnit', operator: 'equals', value: this.units()[0]?.key ?? '' },
    ]);
  }
  removeCondition(i: number) {
    this.conditions.update((list) => list.filter((_, n) => n !== i));
  }
  saveRule() {
    this.ruleForm.markAllAsTouched();
    if (this.ruleForm.invalid) {
      document.querySelector<HTMLInputElement>('[data-rule-editor] input.ng-invalid')?.focus();
      return;
    }
    void this.api.run(async () => {
      const r = this.selectedRule();
      const value = this.ruleForm.getRawValue();
      await this.api.request(r ? '/routing/rules/' + r.id : '/routing/rules', r ? 'PUT' : 'POST', {
        processKey: this.processFilter(),
        name: value.name,
        priority: value.priority,
        enabled: value.enabled,
        targetUnit: value.targetUnit,
        spec: { match: value.match, conditions: this.conditions() },
        version: r?.version ?? 0,
      });
      this.editingRule.set(false);
      this.preview.set(null);
      await this.load();
      this.api.notice.set('כלל הניתוב נשמר. הוא יחול על פניות חדשות.');
    });
  }
  preparePreview() {
    const definition = this.processes().find(
      (p) => p.id === this.simulationForm.controls.processVersionId.value,
    )?.definition;
    const fields =
      definition?.fields.filter(
        (f) => f.editRoles.includes('Admin') && f.editStates.includes(definition.initialState),
      ) ?? [];
    this.previewFields.set(fields);
    Object.keys(this.simulationData.controls).forEach((k) => this.simulationData.removeControl(k));
    fields.forEach((f) =>
      this.simulationData.addControl(f.key, new FormControl('', { nonNullable: true })),
    );
    this.preview.set(null);
  }
  simulate() {
    this.simulationForm.markAllAsTouched();
    if (this.simulationForm.invalid) return;
    void this.api.run(async () => {
      this.preview.set(
        await this.api.request<RouteResult>('/routing/simulate', 'POST', {
          ...this.simulationForm.getRawValue(),
          data: this.simulationData.getRawValue(),
        }),
      );
    });
  }
}
