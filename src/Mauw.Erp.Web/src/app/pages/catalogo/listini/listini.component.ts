import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { PriceList, PriceListRequest, PriceRule, PriceCalculationType } from '../../../models/price-list.model';
import { PriceListService, priceError } from '../../../services/price-list.service';
import { decimal8, priceRuleValidator } from '../../../services/price-list.validators';
import { PriceRuleTargetComponent } from './price-rule-target.component';

function localDate(value?: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, -1);
}

@Component({
  selector: 'app-listini',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PriceRuleTargetComponent],
  template: `
    <div class="p-6 space-y-4">
      <div class="flex justify-between"><h1 class="text-3xl font-bold">Listini prezzi</h1>
        <button class="bg-blue-600 text-white rounded px-4 py-2" [disabled]="busy" (click)="edit()">Nuovo listino</button></div>
      <p class="text-sm text-gray-600">Valuta EUR. Specificità: SKU → prodotto → categoria → globale; poi priorità maggiore.
        Parità fra regole applicabili: errore, verificato dal server. I prezzi risolti non sono totali fiscali.</p>
      <p *ngIf="error" role="alert" class="bg-red-50 text-red-700 p-3">{{ error }}</p>
      <p *ngIf="loading">Caricamento…</p>
      <div class="bg-white rounded shadow overflow-auto">
        <table class="w-full text-left"><thead><tr class="border-b"><th class="p-3">Nome</th><th>Valuta</th><th>Regole</th><th>Stato</th><th>Azioni</th></tr></thead>
          <tbody><tr *ngFor="let list of lists" class="border-b">
            <td class="p-3">{{ list.name }}</td><td>{{ list.currency }}</td><td>{{ list.rules.length }}</td>
            <td><button [disabled]="busy" (click)="activate(list)" class="border rounded px-2 py-1"
              [attr.aria-label]="(list.isActive ? 'Disattiva ' : 'Attiva ') + list.name">{{ list.isActive ? 'Attivo' : 'Inattivo' }}</button></td>
            <td class="space-x-3"><button [disabled]="busy" (click)="open(list.id)" class="text-blue-600">Dettaglio</button>
              <button [disabled]="busy" (click)="deleteId = list.id" class="text-red-600">Elimina</button></td>
          </tr><tr *ngIf="!loading && lists.length === 0"><td colspan="5" class="p-4">Nessun listino.</td></tr></tbody>
        </table>
      </div>
      <div *ngIf="deleteId" class="bg-red-50 p-4 rounded">
        Eliminare il listino? I riferimenti esistenti potrebbero impedirne la rimozione.
        <button [disabled]="busy" (click)="remove()" class="text-red-700 px-3">Conferma</button>
        <button [disabled]="busy" (click)="deleteId = null">Annulla</button>
      </div>
      <form *ngIf="editing" [formGroup]="form" (ngSubmit)="save()" class="bg-white rounded shadow p-5 space-y-4">
        <h2 class="text-xl font-bold">{{ editId ? 'Modifica listino' : 'Nuovo listino' }}</h2>
        <label class="block">Nome <input formControlName="name" maxlength="255" class="border rounded p-2 w-full"></label>
        <label class="block"><input type="checkbox" formControlName="isActive"> Attivo · EUR</label>
        <div formArrayName="rules" class="space-y-4">
          <fieldset *ngFor="let rule of rules.controls; let i = index" [formGroupName]="i" class="border rounded p-4">
            <legend class="font-semibold">Regola {{ i + 1 }}</legend>
            <div class="grid md:grid-cols-3 gap-3">
              <label>Bersaglio<select formControlName="target" (change)="rule.controls.targetId.setValue('')" class="border p-2 w-full">
                <option value="global">Globale</option><option value="variantId">SKU (variante)</option>
                <option value="productId">Prodotto</option><option value="categoryId">Categoria</option></select></label>
              <label *ngIf="rule.controls.target.value !== 'global'">ID bersaglio (GUID)
                <input formControlName="targetId" class="border p-2 w-full" placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"></label>
              <app-price-rule-target *ngIf="rule.controls.target.value !== 'global'"
                [target]="rule.controls.target.value" [targetId]="rule.controls.targetId.value"
                (selected)="rule.controls.targetId.setValue($event)"></app-price-rule-target>
              <label>Tipo<select formControlName="calculationType" class="border p-2 w-full">
                <option value="FixedPrice">Prezzo fisso netto EUR</option><option value="PercentageDiscount">Sconto %</option>
                <option value="CostMarkup">Ricarico sul costo %</option></select></label>
              <label>Valore<input type="number" min="0" step="0.00000001" formControlName="value" class="border p-2 w-full"></label>
              <label>Quantità minima<input type="number" min="0.00000001" step="0.00000001" formControlName="minimumQuantity" class="border p-2 w-full"></label>
              <label>Priorità<input type="number" step="1" formControlName="priority" class="border p-2 w-full"></label>
              <label>Dal (incluso, ora locale)<input type="datetime-local" step="0.001" formControlName="validFrom" class="border p-2 w-full"></label>
              <label>Al (incluso, ora locale)<input type="datetime-local" step="0.001" formControlName="validTo" class="border p-2 w-full"></label>
            </div>
            <p *ngIf="rule.invalid && rule.touched" class="text-red-600 text-sm" role="alert">
              Verificare bersaglio GUID, valore non negativo (sconto ≤100), massimo 8 decimali, quantità positiva, priorità intera e intervallo date coerente.
            </p>
            <button type="button" (click)="rules.removeAt(i)" class="text-red-600 mt-3">Rimuovi regola</button>
          </fieldset>
        </div>
        <button type="button" (click)="addRule()" class="text-blue-600">+ Aggiungi regola</button>
        <p *ngIf="form.controls.name.invalid && form.controls.name.touched" class="text-red-600">Nome obbligatorio (max 255 caratteri).</p>
        <div class="flex gap-3"><button type="submit" [disabled]="busy" class="bg-blue-600 text-white rounded px-4 py-2">Salva</button>
          <button type="button" [disabled]="busy" (click)="editing = false">Annulla</button></div>
      </form>
    </div>
  `
})
export class ListiniComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(PriceListService);
  lists: PriceList[] = [];
  error = '';
  busy = false;
  loading = false;
  editing = false;
  editId: string | null = null;
  deleteId: string | null = null;
  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(255), Validators.pattern(/\S/)]],
    isActive: true,
    rules: new FormArray<ReturnType<ListiniComponent['ruleForm']>>([])
  });
  get rules() { return this.form.controls.rules; }
  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading = true;
    this.service.getAll().pipe(finalize(() => this.loading = false)).subscribe({
      next: lists => this.lists = lists, error: e => this.error = priceError(e)
    });
  }
  ruleForm(rule?: PriceRule) {
    const target = rule?.variantId ? 'variantId' : rule?.productId ? 'productId' : rule?.categoryId ? 'categoryId' : 'global';
    return this.fb.nonNullable.group({
      id: rule?.id ?? '',
      target,
      targetId: rule?.variantId ?? rule?.productId ?? rule?.categoryId ?? '',
      calculationType: this.fb.nonNullable.control<PriceCalculationType>(rule?.calculationType ?? 'FixedPrice'),
      value: [rule?.value ?? 0, [Validators.required, Validators.min(0), decimal8]],
      minimumQuantity: [rule?.minimumQuantity ?? 1, [Validators.required, Validators.min(0.00000001), decimal8]],
      priority: [rule?.priority ?? 0, [Validators.required, Validators.min(-2147483648), Validators.max(2147483647), Validators.pattern(/^-?\d+$/)]],
      validFrom: localDate(rule?.validFrom), validTo: localDate(rule?.validTo)
    }, { validators: priceRuleValidator });
  }
  addRule(rule?: PriceRule): void { this.rules.push(this.ruleForm(rule)); }
  edit(list?: PriceList): void {
    this.error = ''; this.editId = list?.id ?? null; this.editing = true; this.rules.clear();
    this.form.reset({ name: list?.name ?? '', isActive: list?.isActive ?? true });
    list?.rules.forEach(rule => this.addRule(rule));
  }
  open(id: string): void {
    this.busy = true;
    this.service.get(id).pipe(finalize(() => this.busy = false)).subscribe({
      next: list => this.edit(list), error: e => this.error = priceError(e)
    });
  }
  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy) return;
    const value = this.form.getRawValue();
    const body: PriceListRequest = {
      name: value.name.trim(), currency: 'EUR', isActive: value.isActive,
      rules: value.rules.map(rule => ({
        ...(rule.id ? { id: rule.id } : {}),
        calculationType: rule.calculationType, value: rule.value, minimumQuantity: rule.minimumQuantity,
        priority: rule.priority,
        variantId: rule.target === 'variantId' ? rule.targetId : null,
        productId: rule.target === 'productId' ? rule.targetId : null,
        categoryId: rule.target === 'categoryId' ? rule.targetId : null,
        validFrom: rule.validFrom ? new Date(rule.validFrom).toISOString() : null,
        validTo: rule.validTo ? new Date(rule.validTo).toISOString() : null
      }))
    };
    this.busy = true; this.error = '';
    (this.editId ? this.service.update(this.editId, body) : this.service.create(body))
      .pipe(finalize(() => this.busy = false)).subscribe({
        next: () => { this.editing = false; this.load(); }, error: e => this.error = priceError(e)
      });
  }
  activate(list: PriceList): void {
    this.busy = true; this.error = '';
    this.service.activate(list.id, !list.isActive).pipe(finalize(() => this.busy = false)).subscribe({
      next: () => { list.isActive = !list.isActive; }, error: e => this.error = priceError(e)
    });
  }
  remove(): void {
    if (!this.deleteId) return;
    this.busy = true; this.error = '';
    this.service.delete(this.deleteId).pipe(finalize(() => this.busy = false)).subscribe({
      next: () => { this.deleteId = null; this.editing = false; this.load(); }, error: e => this.error = priceError(e)
    });
  }
}
