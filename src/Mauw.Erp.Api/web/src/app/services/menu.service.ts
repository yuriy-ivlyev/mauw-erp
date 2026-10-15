import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { MenuItem } from '../models/menu.model';

@Injectable({
  providedIn: 'root'
})
export class MenuService {
  private menuItems: MenuItem[] = [
    {
      label: 'Dashboard',
      route: '/dashboard',
      icon: 'M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z',
      children: []
    },
    {
      label: 'Ordini',
      icon: 'M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49c.08-.14.12-.31.12-.48 0-.55-.45-1-1-1H5.21l-.94-2H1zm6 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z',
      children: [
        {
          label: 'Elenco Ordini',
          route: '/ordini/elenco',
          icon: 'M10 10.5h4v3h-4zm-6 0h4v3H4zm6-6h4v3h-4zM4 4.5h4v3H4zm6 6h4v3h-4zm-6 0h4v3H4zm12-6h4v3h-4zm0 6h4v3h-4zm0 6h4v3h-4z'
        },
        {
          label: 'Nuovo Ordine',
          route: '/ordini/nuovo',
          icon: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z'
        }
      ]
    },
    {
      label: 'Fatturazione',
      icon: 'M11 5.5h2v6h-2zm4 0h2v6h-2zm-8 0h2v6H7z',
      children: [
        {
          label: 'Fatture',
          route: '/fatturazione/fatture',
          icon: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6z'
        },
        {
          label: 'Preventivi',
          route: '/fatturazione/preventivi',
          icon: 'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14z'
        },
        {
          label: 'Nota Credito',
          route: '/fatturazione/nota-credito',
          icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z'
        }
      ]
    },
    {
      label: 'Documenti Di Trasporto',
      icon: 'M18.92 6.01C18.72 5.42 18.16 5 17.5 5h-11c-.66 0-1.22.42-1.42 1.01L3 12v8c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h12v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-8l-2.08-5.99zM6.5 16c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5zm11 0c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z',
      children: [
        {
          label: 'Elenco DDT',
          route: '/ddt/elenco',
          icon: 'M10 10.5h4v3h-4zm-6 0h4v3H4zm6-6h4v3h-4zM4 4.5h4v3H4zm6 6h4v3h-4zm-6 0h4v3H4zm12-6h4v3h-4zm0 6h4v3h-4zm0 6h4v3h-4z'
        },
        {
          label: 'Nuovo DDT',
          route: '/ddt/nuovo',
          icon: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z'
        }
      ]
    },
    {
      label: 'Gestione Magazzino',
      icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z',
      children: [
        {
          label: 'Prodotti',
          route: '/magazzino/prodotti',
          icon: 'M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49c.08-.14.12-.31.12-.48 0-.55-.45-1-1-1H5.21l-.94-2H1zm6 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z'
        },
        {
          label: 'Movimenti',
          route: '/magazzino/movimenti',
          icon: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z'
        },
        {
          label: 'Inventario',
          route: '/magazzino/inventario',
          icon: 'M10 10.5h4v3h-4zm-6 0h4v3H4zm6-6h4v3h-4zM4 4.5h4v3H4zm6 6h4v3h-4zm-6 0h4v3H4zm12-6h4v3h-4zm0 6h4v3h-4zm0 6h4v3h-4z'
        },
        {
          label: 'Categorie',
          route: '/magazzino/categorie',
          icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z'
        },
        {
          label: 'Colori',
          route: '/magazzino/colori',
          icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z'
        },
        {
          label: 'Taglie',
          route: '/magazzino/taglie',
          icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z'
        },
        {
          label: 'Materiali',
          route: '/magazzino/materiali',
          icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z'
        },
        {
          label: 'Macro Categorie',
          route: '/magazzino/macro-categorie',
          icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z'
        }
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
