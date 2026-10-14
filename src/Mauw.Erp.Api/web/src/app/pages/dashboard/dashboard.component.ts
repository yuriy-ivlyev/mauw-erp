import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  stats = [
    { title: 'Total Users', value: '1,234', icon: '👥', color: 'bg-blue-500' },
    { title: 'Total Sales', value: '$45,231', icon: '💰', color: 'bg-green-500' },
    { title: 'Orders', value: '342', icon: '📦', color: 'bg-orange-500' },
    { title: 'Revenue', value: '$83,021', icon: '📈', color: 'bg-purple-500' }
  ];
}
