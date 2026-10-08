import { ChangeDetectionStrategy, Component, input } from '@angular/core';
const paths: Record<string, string> = {
  grid: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
  inbox: 'M4 4h16v16H4z M4 14h5l2 3h2l2-3h5 M8 8h8',
  building: 'M4 21V5h10v16 M14 10h6v11 M2 21h20 M8 9h2 M8 13h2 M8 17h2 M16 14h1 M16 17h1',
  tasks: 'M9 5h11 M9 12h11 M9 19h11 M3 5l1 1 2-3 M3 12l1 1 2-3 M3 19l1 1 2-3',
  workflow: 'M9 3h6v6H9z M3 15h6v6H3z M15 15h6v6h-6z M12 9v3 M6 15v-3h12v3',
  route: 'M5 4v11a4 4 0 0 0 4 4h10 M15 15l4 4-4 4 M5 4h12 M13 1l4 3-4 3',
  menu: 'M4 6h16 M4 12h16 M4 18h16',
  close: 'M6 6l12 12 M6 18L18 6',
  plus: 'M12 5v14 M5 12h14',
  arrow: 'M19 12H5 M11 6l-6 6 6 6',
  search: 'M20 20l-5-5 M17 10a7 7 0 1 1-14 0 7 7 0 0 1 14 0',
  clock: 'M12 8v4l3 2 M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0',
  check: 'M5 12l4 4L19 6',
  shield: 'M12 3l8 3v6c0 5-8 9-8 9s-8-4-8-9V6z M8 12l3 3 5-6',
  logout: 'M9 4H4v16h5 M10 12h10 M16 8l4 4-4 4',
};
@Component({
  selector: 'app-icon',
  template:
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="path()"/></svg>',
  styles:
    ':host{display:inline-flex;flex-shrink:0;width:20px;height:20px}svg{width:100%;height:100%}',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Icon {
  readonly name = input('grid');
  path() {
    return paths[this.name()] ?? paths['grid'];
  }
}
