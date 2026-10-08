import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Processes } from './processes';

describe('Processes', () => {
  let component: Processes;
  let fixture: ComponentFixture<Processes>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Processes]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Processes);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
