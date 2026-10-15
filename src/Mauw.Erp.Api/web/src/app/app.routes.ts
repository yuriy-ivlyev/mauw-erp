import { Routes } from '@angular/router';
import { AdminLayoutComponent } from './shared/layout/admin-layout.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';

// Import componenti ordini
import { OrdiniElencoComponent } from './pages/ordini/elenco/ordini-elenco.component';
import { OrdiniNuovoComponent } from './pages/ordini/nuovo/ordini-nuovo.component';

// Import componenti fatturazione
import { FattureComponent } from './pages/fatturazione/fatture/fatture.component';
import { PreventiviComponent } from './pages/fatturazione/preventivi/preventivi.component';
import { NotaCreditoComponent } from './pages/fatturazione/nota-credito/nota-credito.component';

// Import componenti DDT
import { DdtElencoComponent } from './pages/ddt/elenco/ddt-elenco.component';
import { DdtNuovoComponent } from './pages/ddt/nuovo/ddt-nuovo.component';

// Import componenti magazzino
import { ProdottiComponent } from './pages/magazzino/prodotti/prodotti.component';
import { MovimentiComponent } from './pages/magazzino/movimenti/movimenti.component';
import { InventarioComponent } from './pages/magazzino/inventario/inventario.component';
import { CategorieComponent } from './pages/magazzino/categorie/categorie.component';
import { ColoriComponent } from './pages/magazzino/colori/colori.component';
import { TaglieComponent } from './pages/magazzino/taglie/taglie.component';
import { MmaterialiComponent } from './pages/magazzino/materiali/materiali.component';
import { MacroCategorieComponent } from './pages/magazzino/macro-categorie/macro-categorie.component';

export const routes: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    children: [
      {
        path: '',
        component: DashboardComponent,
        pathMatch: 'full',
        title: 'Dashboard | Mauw ERP'
      },
      {
        path: 'dashboard',
        component: DashboardComponent,
        title: 'Dashboard | Mauw ERP'
      },
      // Ordini
      {
        path: 'ordini/elenco',
        component: OrdiniElencoComponent,
        title: 'Elenco Ordini | Mauw ERP'
      },
      {
        path: 'ordini/nuovo',
        component: OrdiniNuovoComponent,
        title: 'Nuovo Ordine | Mauw ERP'
      },
      // Fatturazione
      {
        path: 'fatturazione/fatture',
        component: FattureComponent,
        title: 'Fatture | Mauw ERP'
      },
      {
        path: 'fatturazione/preventivi',
        component: PreventiviComponent,
        title: 'Preventivi | Mauw ERP'
      },
      {
        path: 'fatturazione/nota-credito',
        component: NotaCreditoComponent,
        title: 'Nota Credito | Mauw ERP'
      },
      // DDT
      {
        path: 'ddt/elenco',
        component: DdtElencoComponent,
        title: 'Elenco DDT | Mauw ERP'
      },
      {
        path: 'ddt/nuovo',
        component: DdtNuovoComponent,
        title: 'Nuovo DDT | Mauw ERP'
      },
      // Magazzino
      {
        path: 'magazzino/prodotti',
        component: ProdottiComponent,
        title: 'Prodotti | Mauw ERP'
      },
      {
        path: 'magazzino/movimenti',
        component: MovimentiComponent,
        title: 'Movimenti | Mauw ERP'
      },
      {
        path: 'magazzino/inventario',
        component: InventarioComponent,
        title: 'Inventario | Mauw ERP'
      },
      {
        path: 'magazzino/categorie',
        component: CategorieComponent,
        title: 'Categorie | Mauw ERP'
      },
      {
        path: 'magazzino/colori',
        component: ColoriComponent,
        title: 'Colori | Mauw ERP'
      },
      {
        path: 'magazzino/taglie',
        component: TaglieComponent,
        title: 'Taglie | Mauw ERP'
      },
      {
        path: 'magazzino/materiali',
        component: MmaterialiComponent,
        title: 'Materiali | Mauw ERP'
      },
      {
        path: 'magazzino/macro-categorie',
        component: MacroCategorieComponent,
        title: 'Macro Categorie | Mauw ERP'
      }
    ]
  }
];
