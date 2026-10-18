import { Component } from '@angular/core';
import { CompanyBrandingService } from '../../services/company-branding.service';

@Component({
  selector: 'app-home',
  standalone: true,
  template: `
    <div class="container">
      <img [src]="branding.logoUrl('horizontal')" alt="Mauw" width="280" height="94"
        style="max-width: 100%; height: auto;">
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
export class HomeComponent {
  constructor(readonly branding: CompanyBrandingService) {}
}
