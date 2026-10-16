import { Routes } from '@angular/router';
import { AdminLayoutComponent } from './shared/layout/admin-layout.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { BancoCassaComponent } from './pages/vendite/cassa/banco-cassa.component';

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
import { InventarioComponent } from './pages/magazzino/inventario/inventario.component';
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
    children: [
      { path: '',          component: DashboardComponent, pathMatch: 'full', title: 'Dashboard | Mauw ERP' },
      { path: 'dashboard', component: DashboardComponent, title: 'Dashboard | Mauw ERP' },

      // Vendite
      { path: 'vendite/cassa',         component: BancoCassaComponent,  title: 'Banco / Cassa | Mauw ERP' },

      // Ordini
      { path: 'ordini/elenco',         component: OrdiniElencoComponent, title: 'Elenco Ordini | Mauw ERP' },
      { path: 'ordini/nuovo',          component: OrdiniNuovoComponent,  title: 'Nuovo Ordine | Mauw ERP' },

      // Fatturazione
      { path: 'fatturazione/fatture',      component: FattureComponent,     title: 'Fatture | Mauw ERP' },
      { path: 'fatturazione/preventivi',   component: PreventiviComponent,  title: 'Preventivi | Mauw ERP' },
      { path: 'fatturazione/nota-credito', component: NotaCreditoComponent, title: 'Nota Credito | Mauw ERP' },

      // DDT
      { path: 'ddt/elenco', component: DdtElencoComponent, title: 'Elenco DDT | Mauw ERP' },
      { path: 'ddt/nuovo',  component: DdtNuovoComponent,  title: 'Nuovo DDT | Mauw ERP' },

      // Magazzino
      { path: 'magazzino/magazzini',      component: MagazziniComponent,     title: 'Magazzini | Mauw ERP' },
      { path: 'magazzino/prodotti',       component: ProdottiComponent,      title: 'Prodotti | Mauw ERP' },
      { path: 'magazzino/movimenti',      component: MovimentiComponent,     title: 'Movimenti | Mauw ERP' },
      { path: 'magazzino/inventario',     component: InventarioComponent,    title: 'Inventario | Mauw ERP' },
      { path: 'magazzino/categorie',      component: CategorieComponent,     title: 'Categorie | Mauw ERP' },
      { path: 'magazzino/colori',         component: ColoriComponent,        title: 'Colori | Mauw ERP' },
      { path: 'magazzino/taglie',         component: TaglieComponent,        title: 'Taglie | Mauw ERP' },
      { path: 'magazzino/materiali',      component: MmaterialiComponent,    title: 'Materiali | Mauw ERP' },
      { path: 'magazzino/macro-categorie',component: MacroCategorieComponent,title: 'Macro Categorie | Mauw ERP' },

      // Anagrafiche
      { path: 'anagrafiche/elenco',           component: AnagraficheElencoComponent,  title: 'Anagrafiche | Mauw ERP' },
      { path: 'anagrafiche/nuovo',            component: AnagraficaModificaComponent, title: 'Nuova Anagrafica | Mauw ERP' },
      { path: 'anagrafiche/:id/modifica',     component: AnagraficaModificaComponent, title: 'Modifica Anagrafica | Mauw ERP' },

      // Impostazioni
      { path: 'impostazioni/azienda', component: ImpostazioniAziendaComponent, title: 'Impostazioni Azienda | Mauw ERP' },

      // Catalogo
      { path: 'catalogo/listini', component: ListiniComponent, title: 'Listini Prezzi | Mauw ERP' }
    ]
  }
];
