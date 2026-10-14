import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-ordini-elenco',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6">
      <h1 class="text-3xl font-bold mb-6">Elenco Ordini</h1>
      <div class="bg-white rounded-lg shadow p-6">
        <p class="text-gray-500">Contenuto della pagina ordini</p>
      </div>
    </div>
  `
})
export class OrdiniElencoComponent {}
