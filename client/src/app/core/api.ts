import { computed, Injectable, signal } from '@angular/core';
import { CreationOptions, Session } from './models';
@Injectable({ providedIn: 'root' })
export class Api {
  readonly creationOptions = signal<CreationOptions | null>(null);
  readonly canCreate = computed(() => !!this.creationOptions()?.processes.length);
  async loadCreationOptions() {
    const options = await this.request<CreationOptions>('/cases/creation-options');
    this.creationOptions.set(options);
    return options;
  }
  readonly session = signal<Session | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly pending = signal(0);
  async request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
    this.pending.update(n => n + 1); this.error.set('');
    try {
      const headers: Record<string, string> = { 'X-Workflow-Client': 'portal' };
      if (body !== undefined && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
      const response = await fetch('/api' + path, { method, headers, credentials: 'same-origin', body: body instanceof FormData ? body : body === undefined ? undefined : JSON.stringify(body) });
      if (!response.ok) {
        const value: { error?: string } = await response.json().catch(() => ({}));
        throw new Error(value.error ?? (response.status === 401 ? 'נדרשת כניסה למערכת' : 'לא ניתן להשלים את הפעולה כרגע'));
      }
      return response.status === 204 ? undefined as T : await response.json() as T;
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'שגיאת תקשורת'); throw error; }
    finally { this.pending.update(n => n - 1); }
  }
  async loadSession() { this.creationOptions.set(null); this.session.set(await this.request<Session>('/session')); }
  async login(id: string) { await this.request('/demo/login/' + encodeURIComponent(id), 'POST'); await this.loadSession(); }
  role(role: string) { return ({ Admin: 'מנהל מערכת', Provider: 'נותן שירות', Reviewer: 'גורם מטפל', Approver: 'גורם מאשר' } as Record<string, string>)[role] ?? role; }
  async run(action: () => Promise<void>) { try { await action(); } catch { /* request() exposes errors to the user. */ } }
}
