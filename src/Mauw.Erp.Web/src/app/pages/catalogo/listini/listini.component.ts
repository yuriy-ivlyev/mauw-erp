import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-listini',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-4">Listini Prezzi</h1>
      <div class="bg-white rounded-lg shadow p-8 text-center text-gray-400">
        <svg class="w-16 h-16 mx-auto mb-4 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
            d="M20 12V22H4V12M22 7H2v5h20V7zM12 22V7M12 7H7.5a2.5 2.5 0 010-5C11 2 12 7 12 7zM12 7h4.5a2.5 2.5 0 000-5C13 2 12 7 12 7z"/>
        </svg>
        <p class="text-lg font-medium text-gray-500">Modulo Listini in sviluppo</p>
        <p class="text-sm text-gray-400 mt-1">Qui potrai creare e gestire i listini prezzi per clienti e canali di vendita.</p>
      </div>
    </div>
  `
})
export class ListiniComponent {}
