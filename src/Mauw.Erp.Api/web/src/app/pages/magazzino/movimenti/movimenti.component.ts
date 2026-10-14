import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-movimenti',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Movimenti Magazzino</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Registra entrate e uscite dal magazzino</p>
      </div>
    </div>
  `
})
export class MovimentiComponent {}
