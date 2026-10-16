import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { MenuItem } from '../models/menu.model';

const ICONS = {
  dashboard:  'M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z',
  cart:       'M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49c.08-.14.12-.31.12-.48 0-.55-.45-1-1-1H5.21l-.94-2H1zm6 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z',
  cash:       'M20 4H4c-1.11 0-2 .89-2 2v12c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z',
  orders:     'M10 10.5h4v3h-4zm-6 0h4v3H4zm6-6h4v3h-4zM4 4.5h4v3H4zm6 6h4v3h-4zm-6 0h4v3H4zm12-6h4v3h-4zm0 6h4v3h-4zm0 6h4v3h-4z',
  quote:      'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14zM7 10h10v2H7zm0-4h10v2H7zm0 8h7v2H7z',
  contacts:   'M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z',
  warehouse:  'M20 6H10v14h10V6zm-4 10h-2v-4h2v4zm4 0h-2v-4h2v4zM4 12H2v8h2v-8zm4-6H6v14h2V6zm0-4H6v2h2V2z',
  products:   'M17 12h-5v5h5v-5zM16 1v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-1V1h-2zm3 18H5V8h14v11z',
  inventory:  'M10 10.5h4v3h-4zm-6 0h4v3H4zm6-6h4v3h-4zM4 4.5h4v3H4zm6 6h4v3h-4zm-6 0h4v3H4zm12-6h4v3h-4zm0 6h4v3h-4zm0 6h4v3h-4z',
  movements:  'M8 5v14l11-7z',
  ddt:        'M18.92 6.01C18.72 5.42 18.16 5 17.5 5h-11c-.66 0-1.22.42-1.42 1.01L3 12v8c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h12v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-8l-2.08-5.99zM6.5 16c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5zm11 0c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z',
  invoice:    'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z',
  creditNote: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z',
  settings:   'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.49.49 0 0 0-.59-.22l-2.39.96a7.16 7.16 0 0 0-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96a.47.47 0 0 0-.59.22L2.74 8.87a.47.47 0 0 0 .12.61l2.03 1.58c-.05.3-.07.63-.07.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.37 1.04.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.57 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32a.47.47 0 0 0-.12-.61l-2.01-1.58zM12 15.6a3.6 3.6 0 1 1 0-7.2 3.6 3.6 0 0 1 0 7.2z',
  categories: 'M12 2l-5.5 9h11L12 2zm0 3.84L13.93 9h-3.87L12 5.84zM17.5 13c-2.49 0-4.5 2.01-4.5 4.5S15.01 22 17.5 22s4.5-2.01 4.5-4.5-2.01-4.5-4.5-4.5zm0 7a2.5 2.5 0 0 1 0-5 2.5 2.5 0 0 1 0 5zM3 21.5h8v-8H3v8zm2-6h4v4H5v-4z',
  sizes:      'M21 6.5l-4-4-8.5 8.5-2 4.5 4.5-2L21 6.5zm-14 7l-1.5.67.83-1.83L17.67 1.33 18.83 2.5 7 13.5z',
  colors:     'M12 22C6.49 22 2 17.51 2 12S6.49 2 12 2s10 4.04 10 9c0 3.31-2.69 6-6 6h-1.77c-.28 0-.5.22-.5.5 0 .12.05.23.13.33.41.47.64 1.06.64 1.67A2.5 2.5 0 0 1 12 22zm0-18c-4.41 0-8 3.59-8 8s3.59 8 8 8c.28 0 .5-.22.5-.5a.54.54 0 0 0-.14-.35c-.41-.46-.63-1.05-.63-1.65a2.5 2.5 0 0 1 2.5-2.5H16c2.21 0 4-1.79 4-4 0-3.86-3.59-7-8-7z',
  materials:  'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z',
};

@Injectable({ providedIn: 'root' })
export class MenuService {
  private menuItems: MenuItem[] = [
    {
      label: 'Dashboard',
      route: '/dashboard',
      icon: ICONS.dashboard,
      children: []
    },
    {
      label: 'Vendite & Showroom',
      icon: ICONS.cart,
      isCollapsed: true,
      children: [
        { label: 'Banco / Cassa', route: '/vendite/cassa',          icon: ICONS.cash      },
        { label: 'Ordini',        route: '/ordini/elenco',          icon: ICONS.orders    },
        { label: 'Preventivi',    route: '/fatturazione/preventivi', icon: ICONS.quote     },
        { label: 'Anagrafiche',   route: '/anagrafiche/elenco',     icon: ICONS.contacts  }
      ]
    },
    {
      label: 'Magazzino & Logistica',
      icon: ICONS.warehouse,
      isCollapsed: false,
      children: [
        { label: 'Capi & Articoli',      route: '/magazzino/prodotti',   icon: ICONS.products   },
        { label: 'Giacenze & Inventario', route: '/magazzino/inventario', icon: ICONS.inventory  },
        { label: 'Movimenti',            route: '/magazzino/movimenti',  icon: ICONS.movements  },
        { label: 'DDT',                  route: '/ddt/elenco',           icon: ICONS.ddt        }
      ]
    },
    {
      label: 'Fatturazione & Fiscale',
      icon: ICONS.invoice,
      isCollapsed: true,
      children: [
        { label: 'Fatture',        route: '/fatturazione/fatture',      icon: ICONS.invoice    },
        { label: 'Note di Credito', route: '/fatturazione/nota-credito', icon: ICONS.creditNote }
      ]
    },
    {
      label: 'Configurazione Catalogo',
      icon: ICONS.settings,
      isCollapsed: true,
      children: [
        { label: 'Categorie & Macro', route: '/magazzino/categorie',     icon: ICONS.categories },
        { label: 'Taglie & Curve',    route: '/magazzino/taglie',        icon: ICONS.sizes      },
        { label: 'Colori',            route: '/magazzino/colori',        icon: ICONS.colors     },
        { label: 'Materiali',         route: '/magazzino/materiali',     icon: ICONS.materials  }
      ]
    },
    {
      label: 'Impostazioni',
      icon: ICONS.settings,
      isCollapsed: true,
      children: [
        { label: 'Nostra Azienda', route: '/impostazioni/azienda', icon: ICONS.contacts }
      ]
    }
  ];

  private menuSubject = new BehaviorSubject<MenuItem[]>(this.menuItems);
  public menu$: Observable<MenuItem[]> = this.menuSubject.asObservable();

  constructor() {}

  getMenuItems(): MenuItem[] {
    return this.menuItems;
  }

  toggleCollapse(item: MenuItem): void {
    if (item.children && item.children.length > 0) {
      item.isCollapsed = !item.isCollapsed;
      this.menuSubject.next([...this.menuItems]);
    }
  }
}
