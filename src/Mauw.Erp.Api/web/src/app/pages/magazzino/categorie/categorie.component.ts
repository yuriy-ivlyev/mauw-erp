import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-categorie',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Categorie Prodotti</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Gestisci le categorie di prodotti</p>
      </div>
    </div>
  `
})
export class CategorieComponent {}
