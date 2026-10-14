import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-prodotti',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Prodotti</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Gestione del catalogo prodotti</p>
      </div>
    </div>
  `
})
export class ProdottiComponent {}
