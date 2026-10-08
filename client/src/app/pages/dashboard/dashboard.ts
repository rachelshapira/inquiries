import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { CaseSummary, Notification, Report } from '../../core/models';
import { businessTitle, stateLabel } from '../../shared/presentation';
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dashboard implements OnInit {
  readonly businessTitle = businessTitle;
  readonly stateLabel = stateLabel;
  readonly api = inject(Api);
  readonly report = signal<Report | null>(null);
  readonly cases = signal<CaseSummary[]>([]);
  readonly notifications = signal<Notification[]>([]);
  readonly labels: Record<string, string | undefined> = {
    draft: 'טיוטה',
    review: 'בבדיקה',
    corrections: 'ממתין להשלמות',
    approval: 'ממתין לאישור',
    approved: 'אושרה',
    cancelled: 'בוטלה',
  };
  ngOnInit() {
    void this.api.run(async () => {
      const [report, cases, notifications] = await Promise.all([
        this.api.request<Report>('/reports'),
        this.api.request<CaseSummary[]>('/cases'),
        this.api.request<Notification[]>('/notifications'),
        this.api.loadCreationOptions(),
      ]);
      this.report.set(report);
      this.cases.set(cases);
      this.notifications.set(notifications);
    });
  }
  count(state: string) {
    return this.report()?.states.find((s) => s.state === state)?.count ?? 0;
  }
  stateName(state: string) {
    return this.stateLabel(this.labels[state] ?? this.cases().find(c => c.state.toLowerCase() === state.toLowerCase())?.stateLabel ?? 'מצב נוסף');
  }
}
