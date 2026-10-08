import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Api } from './core/api';
import { Icon } from './shared/icon/icon';
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet, Icon],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App implements OnInit {
  readonly api = inject(Api);
  readonly router = inject(Router);
  readonly destroy = inject(DestroyRef);
  readonly menuOpen = signal(false);
  readonly pageTitle = signal('תמונת מצב');
  ngOnInit() {
    this.router.events.pipe(takeUntilDestroyed(this.destroy)).subscribe((event) => {
      if (event instanceof NavigationEnd) {
        const key = event.urlAfterRedirects.split('/')[1]?.split('?')[0] ?? '';
        this.pageTitle.set(
          (
            {
              cases: 'פניות',
              providers: 'נותני שירות',
              tasks: 'משימות בדיקה',
              processes: 'טפסים ותהליכים',
              routing: 'ניתוב לפי תהליך',
              organization: 'הגדרות הארגון',
            } as Record<string, string>
          )[key] ?? 'תמונת מצב',
        );
        this.menuOpen.set(false);
      }
    });
    void this.api.run(() => this.api.loadSession());
  }
  login(id: string) {
    void this.api.run(() => this.api.login(id));
  }
  closeNavigation() {
    this.menuOpen.set(false);
    document.querySelector<HTMLButtonElement>('.mobile-toggle')?.focus();
  }
  logout() {
    void this.api.run(async () => {
      await this.api.request('/logout', 'POST');
      await this.api.loadSession();
    });
  }
}
