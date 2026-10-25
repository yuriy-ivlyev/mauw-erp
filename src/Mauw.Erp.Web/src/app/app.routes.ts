import { Routes } from '@angular/router';
import { AdminLayoutComponent } from './shared/layout/admin-layout.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { BancoCassaComponent } from './pages/vendite/cassa/banco-cassa.component';
import { authGuard } from './guards/auth.guard';
import { roleGuard } from './guards/role.guard';

// Ordini
import { OrdiniElencoComponent } from './pages/ordini/elenco/ordini-elenco.component';
import { OrdiniNuovoComponent } from './pages/ordini/nuovo/ordini-nuovo.component';

// Fatturazione
import { FattureComponent } from './pages/fatturazione/fatture/fatture.component';
import { PreventiviComponent } from './pages/fatturazione/preventivi/preventivi.component';
import { NotaCreditoComponent } from './pages/fatturazione/nota-credito/nota-credito.component';

// DDT
import { DdtElencoComponent } from './pages/ddt/elenco/ddt-elenco.component';
import { DdtNuovoComponent } from './pages/ddt/nuovo/ddt-nuovo.component';

// Magazzino
import { MagazziniComponent } from './pages/magazzino/magazzini/magazzini.component';
import { ProdottiComponent } from './pages/magazzino/prodotti/prodotti.component';
import { MovimentiComponent } from './pages/magazzino/movimenti/movimenti.component';
import { CategorieComponent } from './pages/magazzino/categorie/categorie.component';
import { ColoriComponent } from './pages/magazzino/colori/colori.component';
import { TaglieComponent } from './pages/magazzino/taglie/taglie.component';
import { MmaterialiComponent } from './pages/magazzino/materiali/materiali.component';
import { MacroCategorieComponent } from './pages/magazzino/macro-categorie/macro-categorie.component';

// Anagrafiche
import { AnagraficheElencoComponent } from './pages/anagrafiche/elenco/anagrafiche-elenco.component';
import { AnagraficaModificaComponent } from './pages/anagrafiche/modifica/anagrafica-modifica.component';

// Impostazioni
import { ImpostazioniAziendaComponent } from './pages/impostazioni/azienda/impostazioni-azienda.component';

// Catalogo
import { ListiniComponent } from './pages/catalogo/listini/listini.component';

export const routes: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', component: DashboardComponent, pathMatch: 'full', title: 'Dashboard | Mauw ERP' },
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard-analytics.component').then(m => m.DashboardAnalyticsComponent), canActivate: [roleGuard], data: { roles: ['Admin'], dashboardSection: 'overview' }, title: 'Dashboard | Mauw ERP' },
      { path: 'dashboard/vendite', loadComponent: () => import('./pages/dashboard/dashboard-analytics.component').then(m => m.DashboardAnalyticsComponent), canActivate: [roleGuard], data: { roles: ['Admin'], dashboardSection: 'sales' }, title: 'Dashboard Vendite | Mauw ERP' },
      { path: 'dashboard/anagrafiche', loadComponent: () => import('./pages/dashboard/dashboard-analytics.component').then(m => m.DashboardAnalyticsComponent), canActivate: [roleGuard], data: { roles: ['Admin'], dashboardSection: 'contacts' }, title: 'Dashboard Anagrafiche | Mauw ERP' },
      { path: 'dashboard/magazzino', loadComponent: () => import('./pages/dashboard/dashboard-analytics.component').then(m => m.DashboardAnalyticsComponent), canActivate: [roleGuard], data: { roles: ['Admin'], dashboardSection: 'warehouse' }, title: 'Dashboard Magazzino | Mauw ERP' },

      // Vendite
      { path: 'vendite/cassa', component: BancoCassaComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Sales'] }, title: 'Banco / Cassa | Mauw ERP' },

      // Acquisti
      { path: 'acquisti/ordini-fornitore', loadComponent: () => import('./pages/acquisti/ordini-fornitore.component').then(m => m.OrdiniFornitoreComponent), canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Ordini Fornitore | Mauw ERP' },
      { path: 'acquisti/ddt-fornitore', loadComponent: () => import('./pages/acquisti/carico-merce.component').then(m => m.CaricoMerceComponent), canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Carico Merce | Mauw ERP' },
      { path: 'acquisti/ddt-fornitore/nuovo', loadComponent: () => import('./pages/acquisti/carico-merce-wizard.component').then(m => m.CaricoMerceWizardComponent), canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Nuovo Carico Fornitore | Mauw ERP' },
      { path: 'acquisti/ddt-fornitore/:id', loadComponent: () => import('./pages/acquisti/carico-merce-wizard.component').then(m => m.CaricoMerceWizardComponent), canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'DDT Fornitore | Mauw ERP' },
      { path: 'acquisti/carico-merce', redirectTo: 'acquisti/ddt-fornitore', pathMatch: 'full' },

      // Ordini
      { path: 'ordini/elenco', component: OrdiniElencoComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Sales'] }, title: 'Elenco Ordini | Mauw ERP' },
      { path: 'ordini/nuovo',  component: OrdiniNuovoComponent,  canActivate: [roleGuard], data: { roles: ['Admin', 'Sales'] }, title: 'Nuovo Ordine | Mauw ERP' },

      // Fatturazione
      { path: 'fatturazione/fatture',      component: FattureComponent,     canActivate: [roleGuard], data: { roles: ['Admin', 'Sales', 'Accounting'] }, title: 'Fatture | Mauw ERP' },
      { path: 'fatturazione/preventivi',   component: PreventiviComponent,  canActivate: [roleGuard], data: { roles: ['Admin', 'Sales', 'Accounting'] }, title: 'Preventivi | Mauw ERP' },
      { path: 'fatturazione/nota-credito', component: NotaCreditoComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Sales', 'Accounting'] }, title: 'Nota Credito | Mauw ERP' },

      // DDT
      { path: 'ddt/elenco', component: DdtElencoComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Elenco DDT | Mauw ERP' },
      { path: 'ddt/nuovo',  component: DdtNuovoComponent,  canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Nuovo DDT | Mauw ERP' },

      // Magazzino
      { path: 'magazzino/magazzini',       component: MagazziniComponent,      canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Magazzini | Mauw ERP' },
      { path: 'magazzino/prodotti',        component: ProdottiComponent,       canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Prodotti | Mauw ERP' },
      { path: 'magazzino/movimenti',       component: MovimentiComponent,      canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Movimenti | Mauw ERP' },
      { path: 'magazzino/inventario',      loadComponent: () => import('./pages/magazzino/inventario/inventario.component').then(m => m.InventarioComponent), canActivate: [roleGuard], data: { roles: ['Admin', 'Sales', 'Warehouse'] }, title: 'Inventario | Mauw ERP' },
      { path: 'magazzino/categorie',       component: CategorieComponent,      canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Categorie | Mauw ERP' },
      { path: 'magazzino/colori',          component: ColoriComponent,         canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Colori | Mauw ERP' },
      { path: 'magazzino/taglie',          component: TaglieComponent,         canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Taglie | Mauw ERP' },
      { path: 'magazzino/materiali',       component: MmaterialiComponent,     canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Materiali | Mauw ERP' },
      { path: 'magazzino/macro-categorie', component: MacroCategorieComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Warehouse'] }, title: 'Macro Categorie | Mauw ERP' },

      // Anagrafiche
      { path: 'anagrafiche/elenco',       component: AnagraficheElencoComponent,  canActivate: [roleGuard], data: { roles: ['Admin', 'Sales', 'Accounting'] }, title: 'Anagrafiche | Mauw ERP' },
      { path: 'anagrafiche/nuovo',        component: AnagraficaModificaComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Sales'] }, title: 'Nuova Anagrafica | Mauw ERP' },
      { path: 'anagrafiche/:id/modifica', component: AnagraficaModificaComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Sales'] }, title: 'Modifica Anagrafica | Mauw ERP' },

      // Impostazioni â€” solo Admin
      { path: 'impostazioni/azienda', component: ImpostazioniAziendaComponent, canActivate: [roleGuard], data: { roles: ['Admin'] }, title: 'Impostazioni Azienda | Mauw ERP' },

      // Catalogo
      { path: 'catalogo/listini', component: ListiniComponent, canActivate: [roleGuard], data: { roles: ['Admin', 'Sales'] }, title: 'Listini Prezzi | Mauw ERP' }
    ]
  }
];
