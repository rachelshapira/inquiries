import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { Api } from './core/api';
export const routes: Routes = [
  {
    path: '',
    title: 'קשר | תמונת מצב',
    loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'cases',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    canActivate: [async (route) => {
      const api = inject(Api);
      const router = inject(Router);
      if (!route.queryParamMap.has('new')) return true;
      try { return (await api.loadCreationOptions()).processes.length > 0 || router.parseUrl('/cases'); }
      catch { return router.parseUrl('/cases'); }
    }],
    title: 'קשר | פניות',
    loadComponent: () => import('./pages/cases/cases').then((m) => m.Cases),
  },
  {
    path: 'cases/:id',
    title: 'קשר | פרטי פנייה',
    loadComponent: () => import('./pages/case-detail/case-detail').then((m) => m.CaseDetailPage),
  },
  {
    path: 'providers',
    title: 'קשר | נותני שירות',
    loadComponent: () => import('./pages/providers/providers').then((m) => m.Providers),
  },
  {
    path: 'tasks',
    canActivate: [() => inject(Api).session()?.user?.role !== 'Provider' || inject(Router).parseUrl('/cases')],
    title: 'קשר | משימות',
    loadComponent: () => import('./pages/tasks/tasks').then((m) => m.Tasks),
  },
  {
    path: 'processes',
    title: 'קשר | בונה התהליכים',
    canDeactivate: [
      (component: { canLeave: () => boolean | Promise<boolean> }) => component.canLeave(),
    ],
    loadComponent: () => import('./pages/processes/processes').then((m) => m.Processes),
  },
  {
    path: 'organization',
    title: 'קשר | הגדרות הארגון',
    loadComponent: () => import('./pages/routing/routing').then((m) => m.Routing),
  },
  {
    path: 'routing',
    redirectTo: ({ queryParams }) =>
      '/processes?section=routing' +
      (queryParams['process'] ? '&process=' + encodeURIComponent(queryParams['process']) : ''),
  },
  { path: '**', redirectTo: '' },
];
