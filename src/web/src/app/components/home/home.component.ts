import { Component } from '@angular/core';

@Component({
  selector: 'app-home',
  standalone: true,
  template: `
    <div class="container">
      <h1>Mauw ERP</h1>
      <p>Welcome to Mauw Enterprise Resource Planning System</p>
      <p>Built with Angular and .NET API</p>
    </div>
  `,
  styles: [`
    .container {
      padding: 20px;
      text-align: center;
    }
    h1 {
      color: #333;
      margin-bottom: 20px;
    }
    p {
      color: #666;
      font-size: 16px;
    }
  `]
})
export class HomeComponent { }
