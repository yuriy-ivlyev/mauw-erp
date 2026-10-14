import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Inventario</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Consulta e gestisci i livelli di inventario</p>
      </div>
    </div>
  `
})
export class InventarioComponent {}
