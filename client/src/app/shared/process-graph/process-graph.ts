import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Definition } from '../../core/models';
let graphSequence = 0;
@Component({
  selector: 'app-process-graph',
  templateUrl: './process-graph.html',
  styleUrl: './process-graph.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProcessGraph {
  readonly arrowId = 'process-arrow-' + ++graphSequence;
  readonly definition = input.required<Definition>();
  readonly selected = input('');
  readonly selectedAction = input<number | null>(null);
  readonly stateSelected = output<string>();
  readonly actionSelected = output<number>();
  readonly graph = computed(() => {
    const d = this.definition();
    const ranks = new Map<string, number>([[d.initialState, 0]]);
    const pending = [d.initialState];
    for (let i = 0; i < pending.length; i++) {
      const from = pending[i];
      for (const t of d.transitions.filter((t) => t.from === from))
        for (const target of [t.to, ...(t.routes ?? []).map((r) => r.to)]) {
          if (!ranks.has(target)) {
            ranks.set(target, (ranks.get(from) ?? 0) + 1);
            pending.push(target);
          }
        }
    }
    const last = Math.max(0, ...ranks.values());
    for (const s of d.states) if (!ranks.has(s.key)) ranks.set(s.key, last + 1);
    const terminalRank =
      Math.max(0, ...d.states.filter((s) => !s.terminal).map((s) => ranks.get(s.key) ?? 0)) + 1;
    for (const state of d.states.filter((s) => s.terminal)) ranks.set(state.key, terminalRank);
    const columns = Math.max(0, ...ranks.values()) + 1;
    const width = Math.max(560, columns * 290);
    const occupied = new Map<number, number>();
    const nodes = d.states.map((s) => {
      const rank = ranks.get(s.key) ?? 0;
      const row = occupied.get(rank) ?? 0;
      occupied.set(rank, row + 1);
      return {
        ...s,
        x: width - 145 - rank * 290,
        y: 80 + row * 170,
        initial: s.key === d.initialState,
      };
    });
    const positions = new Map(nodes.map((n) => [n.key, n]));
    const terminalRows = new Set<number>();
    for (const node of nodes.filter((n) => n.terminal)) {
      const incoming = d.transitions
        .filter((t) => t.to === node.key || t.routes?.some((r) => r.to === node.key))
        .map((t) => positions.get(t.from))
        .filter((n) => n !== undefined)
        .sort((a, b) => (ranks.get(b.key) ?? 0) - (ranks.get(a.key) ?? 0));
      let row = incoming.length ? Math.round((incoming[0].y - 80) / 170) : 0;
      while (terminalRows.has(row)) row++;
      terminalRows.add(row);
      node.y = 80 + row * 170;
    }
    const bottom = Math.max(150, ...nodes.map((n) => n.y + 70));
    let lane = 0;
    const routes = d.transitions.flatMap((t, index) => [
      ...(t.routes ?? []).map((r) => ({
        index,
        from: t.from,
        to: r.to,
        label: r.label,
        condition: r.when.conditions
          .map(
            (c) =>
              `${d.fields.find((f) => f.key === c.field)?.label ?? c.field} ${c.operator} ${c.value}`,
          )
          .join(r.when.match === 'all' ? ' AND ' : ' OR '),
        fallback: false,
      })),
      {
        index,
        from: t.from,
        to: t.to,
        label: t.label + (t.routes?.length ? ' · אחרת' : ''),
        condition: '',
        fallback: !!t.routes?.length,
      },
    ]);
    const edges = routes
      .map((e, i) => {
        const a = positions.get(e.from);
        const b = positions.get(e.to);
        if (!a || !b) return null;
        const adjacent = (ranks.get(e.to) ?? 0) === (ranks.get(e.from) ?? 0) + 1;
        const outgoing = routes.filter((item) => item.from === e.from);
        const port = routes.slice(0, i).filter((item) => item.from === e.from).length;
        const incoming = routes.filter((item) => item.to === e.to);
        const targetPort = routes.slice(0, i).filter((item) => item.to === e.to).length;
        const sy = a.y - 18 + ((port + 1) * 42) / (outgoing.length + 1);
        const ty = b.y - 18 + ((targetPort + 1) * 42) / (incoming.length + 1);
        let path: string;
        let lx: number;
        let ly: number;
        if (adjacent) {
          const sx = a.x - 101;
          const tx = b.x + 103;
          const mid = (sx + tx) / 2;
          path = `M ${sx} ${sy} C ${mid} ${sy}, ${mid} ${ty}, ${tx} ${ty}`;
          lx = mid;
          ly = (sy + ty) / 2 - 9;
        } else {
          // Dedicated lanes outside the nodes keep returns, skips and self-loops clear.
          const y = bottom + 35 + lane++ * 34;
          // Side corridors avoid passing through states below either endpoint.
          const sourceSide = a.x - 118 - (i % 3) * 6;
          const targetSide = b.x + 118 + (i % 3) * 6;
          path = `M ${a.x - 101} ${sy} L ${sourceSide} ${sy} L ${sourceSide} ${y} L ${targetSide} ${y} L ${targetSide} ${ty} L ${b.x + 103} ${ty}`;
          lx = (sourceSide + targetSide) / 2;
          ly = y - 7;
        }
        return {
          ...e,
          fromLabel: a.label,
          toLabel: b.label,
          id: i,
          path,
          lx,
          ly,
        };
      })
      .filter((e) => e !== null);
    return { width, height: bottom + 70 + lane * 34, nodes, edges };
  });
  key(event: KeyboardEvent, value: string | number) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      typeof value === 'string' ? this.stateSelected.emit(value) : this.actionSelected.emit(value);
    }
  }
}
