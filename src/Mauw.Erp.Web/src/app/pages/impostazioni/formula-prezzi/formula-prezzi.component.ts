import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { PriceFormulaService, priceFormulaErrorMessage } from '../../../services/price-formula.service';
import {
  PRICE_FORMULA_DEFAULT_ROUNDING_STEP,
  PRICE_FORMULA_MAX_STEPS,
  PriceFormula,
  PriceFormulaCalculation,
  SavePriceFormulaRequest
} from '../../../models/price-formula.model';

interface EditableStep {
  id: number;
  label: string;
  percentage: number | null;
}

interface StepErrors {
  label: string | null;
  percentage: string | null;
}

const MAX_LABEL_LENGTH = 100;
const MAX_PERCENTAGE = 9999;
const MAX_ROUNDING_STEP = 100;

function hasMaxTwoDecimals(value: number): boolean {
  return /^\d+(?:\.\d{1,2})?$/.test(String(value));
}

// Il costo simulato puo avere la scala memorizzata (4 decimali); l'inserimento dei costi resta a 2.
function hasMaxFourDecimals(value: number): boolean {
  return /^\d+(?:\.\d{1,4})?$/.test(String(value));
}

@Component({
  selector: 'app-formula-prezzi',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './formula-prezzi.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FormulaPrezziComponent implements OnInit {
  private readonly service = inject(PriceFormulaService);
  private readonly destroyRef = inject(DestroyRef);
  private nextStepId = 1;

  readonly maxSteps = PRICE_FORMULA_MAX_STEPS;
  readonly maxLabelLength = MAX_LABEL_LENGTH;

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly isConfigured = signal(false);
  readonly updatedAt = signal<string | null>(null);

  readonly steps = signal<EditableStep[]>([]);
  readonly roundingStep = signal<number | null>(PRICE_FORMULA_DEFAULT_ROUNDING_STEP);
  private readonly savedSnapshot = signal(this.snapshotOf([], PRICE_FORMULA_DEFAULT_ROUNDING_STEP));

  readonly saving = signal(false);
  readonly saveError = signal('');
  readonly saveSuccess = signal('');

  readonly purchaseCost = signal<number | null>(null);
  readonly calculating = signal(false);
  readonly calculationError = signal('');
  readonly calculation = signal<PriceFormulaCalculation | null>(null);

  readonly hasUnsavedChanges = computed(
    () => this.snapshotOf(this.steps(), this.roundingStep()) !== this.savedSnapshot()
  );

  readonly stepErrors = computed<StepErrors[]>(() => this.steps().map(step => ({
    label: this.labelError(step.label),
    percentage: this.percentageError(step.percentage)
  })));

  readonly stepsError = computed(() => {
    const count = this.steps().length;
    if (count < 1) return 'Aggiungere almeno un passaggio.';
    if (count > PRICE_FORMULA_MAX_STEPS) return `Sono ammessi al massimo ${PRICE_FORMULA_MAX_STEPS} passaggi.`;
    return null;
  });

  readonly roundingError = computed(() => {
    const value = this.roundingStep();
    if (value == null || !Number.isFinite(value)) return 'Indicare il passo di arrotondamento.';
    if (value <= 0 || value > MAX_ROUNDING_STEP) return `Il passo deve essere maggiore di 0 e al massimo ${MAX_ROUNDING_STEP}.`;
    if (!hasMaxTwoDecimals(value)) return 'Il passo ammette al massimo 2 decimali.';
    return null;
  });

  readonly isValid = computed(() =>
    this.stepsError() === null &&
    this.roundingError() === null &&
    this.stepErrors().every(errors => errors.label === null && errors.percentage === null));

  readonly canSave = computed(() => this.isValid() && this.hasUnsavedChanges() && !this.saving());

  readonly costError = computed(() => {
    const cost = this.purchaseCost();
    if (cost == null || !Number.isFinite(cost)) return 'Indicare il costo di acquisto.';
    if (cost < 0) return 'Il costo di acquisto non può essere negativo.';
    if (!hasMaxFourDecimals(cost)) return 'Il costo di acquisto ammette al massimo 4 decimali.';
    return null;
  });

  readonly canSimulate = computed(() => this.costError() === null && !this.calculating());

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.service.get()
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: formula => this.applyFormula(formula),
        error: error => this.loadError.set(
          priceFormulaErrorMessage(error, 'Impossibile caricare la formula prezzi.'))
      });
  }

  addStep(): void {
    if (this.steps().length >= PRICE_FORMULA_MAX_STEPS) return;
    this.steps.update(steps => [...steps, { id: this.nextStepId++, label: '', percentage: null }]);
    this.clearFeedback();
  }

  removeStep(index: number): void {
    this.steps.update(steps => steps.filter((_, i) => i !== index));
    this.clearFeedback();
  }

  moveStep(index: number, direction: -1 | 1): void {
    const target = index + direction;
    if (target < 0 || target >= this.steps().length) return;
    this.steps.update(steps => {
      const copy = [...steps];
      [copy[index], copy[target]] = [copy[target], copy[index]];
      return copy;
    });
    this.clearFeedback();
  }

  updateLabel(index: number, label: string): void {
    this.patchStep(index, { label });
  }

  updatePercentage(index: number, percentage: number | null): void {
    this.patchStep(index, { percentage });
  }

  updateRoundingStep(value: number | null): void {
    this.roundingStep.set(value);
    this.clearFeedback();
  }

  save(): void {
    if (!this.canSave()) return;
    const request: SavePriceFormulaRequest = {
      steps: this.steps().map(step => ({ label: step.label.trim(), percentage: step.percentage as number })),
      roundingStep: this.roundingStep() as number
    };
    this.saving.set(true);
    this.saveError.set('');
    this.saveSuccess.set('');
    this.service.save(request)
      .pipe(finalize(() => this.saving.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: formula => {
          this.applyFormula(formula);
          this.calculation.set(null);
          this.calculationError.set('');
          this.saveSuccess.set('Formula prezzi salvata.');
        },
        error: error => this.saveError.set(
          priceFormulaErrorMessage(error, 'Impossibile salvare la formula prezzi.'))
      });
  }

  updatePurchaseCost(value: number | null): void {
    this.purchaseCost.set(value);
  }

  simulate(): void {
    const cost = this.purchaseCost();
    if (!this.canSimulate() || cost == null) return;
    this.calculating.set(true);
    this.calculationError.set('');
    this.calculation.set(null);
    this.service.calculate(cost)
      .pipe(finalize(() => this.calculating.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => this.calculation.set(result),
        error: error => this.calculationError.set(
          priceFormulaErrorMessage(error, 'Impossibile calcolare il prezzo di vendita.'))
      });
  }

  private patchStep(index: number, patch: Partial<EditableStep>): void {
    this.steps.update(steps => steps.map((step, i) => i === index ? { ...step, ...patch } : step));
    this.clearFeedback();
  }

  private clearFeedback(): void {
    this.saveSuccess.set('');
    this.saveError.set('');
  }

  private applyFormula(formula: PriceFormula): void {
    const steps = (formula.steps ?? []).map(step => ({
      id: this.nextStepId++,
      label: step.label,
      percentage: step.percentage as number | null
    }));
    const roundingStep = formula.roundingStep ?? PRICE_FORMULA_DEFAULT_ROUNDING_STEP;
    this.isConfigured.set(formula.isConfigured);
    this.updatedAt.set(formula.updatedAt ?? null);
    this.steps.set(steps);
    this.roundingStep.set(roundingStep);
    this.savedSnapshot.set(this.snapshotOf(steps, roundingStep));
  }

  private snapshotOf(steps: EditableStep[], roundingStep: number | null): string {
    return JSON.stringify({
      steps: steps.map(step => [step.label.trim(), step.percentage]),
      roundingStep
    });
  }

  private labelError(label: string): string | null {
    const trimmed = label.trim();
    if (!trimmed) return 'Indicare il nome del passaggio.';
    if (trimmed.length > MAX_LABEL_LENGTH) return `Il nome ammette al massimo ${MAX_LABEL_LENGTH} caratteri.`;
    return null;
  }

  private percentageError(value: number | null): string | null {
    if (value == null || !Number.isFinite(value)) return 'Indicare la percentuale.';
    if (value < 0 || value > MAX_PERCENTAGE) return `La percentuale deve essere tra 0 e ${MAX_PERCENTAGE}.`;
    if (!hasMaxTwoDecimals(value)) return 'La percentuale ammette al massimo 2 decimali.';
    return null;
  }
}
