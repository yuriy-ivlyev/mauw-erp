import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-fatture',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Fatture</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Elenco delle fatture</p>
      </div>
    </div>
  `
})
export class FattureComponent {}
