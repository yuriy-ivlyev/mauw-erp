import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DashboardStatistics, DailyMovementStatistics, DailySalesStatistics } from '../../models/dashboard.model';
import { DashboardService } from '../../services/dashboard.service';

export type DashboardSectionKey = 'overview' | 'sales' | 'contacts' | 'warehouse';

interface SalesPoint {
  x: number;
  label: string;
  showLabel: boolean;
  orderY: number;
  revenueY: number;
  orderCount: number;
  grossRevenue: number;
}

interface MovementPoint {
  x: number;
  label: string;
  showLabel: boolean;
  inboundY: number;
  inboundHeight: number;
  outboundY: number;
  outboundHeight: number;
  adjustmentY: number;
  adjustmentHeight: number;
}

@Component({
  selector: 'app-dashboard-analytics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard-analytics.component.html',
  styleUrl: './dashboard-analytics.component.scss'
})
export class DashboardAnalyticsComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  section: DashboardSectionKey = 'overview';
  statistics: DashboardStatistics | null = null;
  statisticsError: string | null = null;
  loadingStatistics = false;
  salesPoints: SalesPoint[] = [];
  movementPoints: MovementPoint[] = [];
  revenueLine = '';
  salesGridLines: { y: number; orders: string; revenue: string }[] = [];
  movementGridLines: { y: number; count: string }[] = [];

  constructor(
    private readonly dashboardService: DashboardService,
    private readonly route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.route.data
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(data => {
        this.section = this.isDashboardSection(data['dashboardSection'])
          ? data['dashboardSection']
          : 'overview';
      });
    this.loadStatistics();
  }

  loadStatistics(): void {
    this.loadingStatistics = true;
    this.statisticsError = null;
    this.dashboardService.getStatistics()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: statistics => {
          this.statistics = statistics;
          this.updateCharts(statistics);
          this.loadingStatistics = false;
        },
        error: () => {
          this.statistics = null;
          this.statisticsError = 'Non è stato possibile caricare le statistiche. Riprova tra poco.';
          this.loadingStatistics = false;
        }
      });
  }

  get showSales(): boolean {
    return this.section === 'overview' || this.section === 'sales';
  }

  get showContacts(): boolean {
    return this.section === 'overview' || this.section === 'contacts';
  }

  get showWarehouse(): boolean {
    return this.section === 'overview' || this.section === 'warehouse';
  }

  get analyticsTitle(): string {
    const titles: Record<DashboardSectionKey, string> = {
      overview: 'Panoramica',
      sales: 'Andamento vendite',
      contacts: 'Statistiche anagrafiche',
      warehouse: 'Movimentazione magazzino'
    };
    return titles[this.section];
  }

  get largestLocationCount(): number {
    return Math.max(1, ...(this.statistics?.customerLocations.map(location => location.customerCount) ?? []));
  }

  formatNumber(value: number, maximumFractionDigits = 0): string {
    return new Intl.NumberFormat('it-IT', { maximumFractionDigits }).format(value);
  }

  formatCurrency(value: number, maximumFractionDigits = 2): string {
    return new Intl.NumberFormat('it-IT', {
      style: 'currency',
      currency: 'EUR',
      maximumFractionDigits
    }).format(value);
  }

  formatChartDate(value: string): string {
    return new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit' })
      .format(new Date(`${value}T00:00:00`));
  }

  private updateCharts(statistics: DashboardStatistics): void {
    const sales = statistics.sales.daily;
    const maxOrders = Math.max(1, ...sales.map(day => day.orderCount));
    const maxRevenue = Math.max(1, ...sales.map(day => day.grossRevenue));
    const left = 62;
    const width = 636;
    const top = 22;
    const bottom = 190;
    const height = bottom - top;

    this.salesPoints = this.createSalesPoints(sales, maxOrders, maxRevenue, left, width, top, height);
    this.revenueLine = this.salesPoints.map(point => `${point.x},${point.revenueY}`).join(' ');
    this.salesGridLines = Array.from({ length: 5 }, (_, index) => {
      const ratio = 1 - index / 4;
      return {
        y: top + height * index / 4,
        orders: this.formatNumber(Math.round(maxOrders * ratio)),
        revenue: this.formatCurrency(maxRevenue * ratio, 0)
      };
    });

    const movements = statistics.movements.daily;
    const maxMovements = Math.max(1, ...movements.map(day => day.totalCount));
    this.movementPoints = this.createMovementPoints(movements, maxMovements, left, width, top, height);
    this.movementGridLines = Array.from({ length: 5 }, (_, index) => ({
      y: top + height * index / 4,
      count: this.formatNumber(Math.round(maxMovements * (1 - index / 4)))
    }));
  }

  private isDashboardSection(section: unknown): section is DashboardSectionKey {
    return section === 'overview' || section === 'sales' ||
      section === 'contacts' || section === 'warehouse';
  }

  private createSalesPoints(
    daily: DailySalesStatistics[],
    maxOrders: number,
    maxRevenue: number,
    left: number,
    width: number,
    top: number,
    height: number
  ): SalesPoint[] {
    return daily.map((day, index) => {
      const ratio = daily.length > 1 ? index / (daily.length - 1) : 0;
      return {
        x: left + width * ratio,
        label: this.formatChartDate(day.date),
        showLabel: index % 6 === 0 || index === daily.length - 1,
        orderY: top + height * (1 - day.orderCount / maxOrders),
        revenueY: top + height * (1 - day.grossRevenue / maxRevenue),
        orderCount: day.orderCount,
        grossRevenue: day.grossRevenue
      };
    });
  }

  private createMovementPoints(
    daily: DailyMovementStatistics[],
    maxMovements: number,
    left: number,
    width: number,
    top: number,
    height: number
  ): MovementPoint[] {
    const bottom = top + height;
    return daily.map((day, index) => {
      const ratio = daily.length > 1 ? index / (daily.length - 1) : 0;
      const inboundHeight = height * day.inboundCount / maxMovements;
      const outboundHeight = height * day.outboundCount / maxMovements;
      const adjustmentHeight = height * day.adjustmentCount / maxMovements;
      return {
        x: left + width * ratio,
        label: this.formatChartDate(day.date),
        showLabel: index % 6 === 0 || index === daily.length - 1,
        inboundY: bottom - inboundHeight,
        inboundHeight,
        outboundY: bottom - inboundHeight - outboundHeight,
        outboundHeight,
        adjustmentY: bottom - inboundHeight - outboundHeight - adjustmentHeight,
        adjustmentHeight
      };
    });
  }
}
