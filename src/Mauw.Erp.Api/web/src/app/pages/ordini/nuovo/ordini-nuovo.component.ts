import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-ordini-nuovo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Nuovo Ordine</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Form per creare un nuovo ordine</p>
      </div>
    </div>
  `
})
export class OrdiniNuovoComponent {}
