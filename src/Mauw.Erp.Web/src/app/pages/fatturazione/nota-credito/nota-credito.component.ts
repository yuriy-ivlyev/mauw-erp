import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-nota-credito',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Nota Credito</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Gestione note credito</p>
      </div>
    </div>
  `
})
export class NotaCreditoComponent {}
