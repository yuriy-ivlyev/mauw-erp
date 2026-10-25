import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { MenuItem } from '../models/menu.model';

const ICONS = {
  // ── Sezioni ───────────────────────────────────────────────────────────────
  cart:       'M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49c.08-.14.12-.31.12-.48 0-.55-.45-1-1-1H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z',
  dashboard:  'M3 3h8v8H3zm10 0h8v5h-8zm0 7h8v11h-8zm-10 3h8v8H3z',
  tag:        'M17.63 5.84C17.27 5.33 16.67 5 16 5L5 5.01C3.9 5.01 3 5.9 3 7v10c0 1.1.9 1.99 2 1.99L16 19c.67 0 1.27-.33 1.63-.84L22 12l-4.37-6.16z',
  box:        'M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 002 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z',
  invoice:    'M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z',
  settings:   'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 00.12-.61l-1.92-3.32a.49.49 0 00-.59-.22l-2.39.96a7.16 7.16 0 00-1.62-.94l-.36-2.54A.484.484 0 0014 3h-3.84a.485.485 0 00-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96a.47.47 0 00-.59.22L2.74 8.87a.47.47 0 00.12.61l2.03 1.58c-.05.3-.07.63-.07.94s.02.64.07.94l-2.03 1.58a.49.49 0 00-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.37 1.04.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.57 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32a.47.47 0 00-.12-.61l-2.01-1.58zM12 15.6a3.6 3.6 0 110-7.2 3.6 3.6 0 010 7.2z',

  // ── Voci foglia ───────────────────────────────────────────────────────────
  cash:       'M20 4H4c-1.11 0-2 .89-2 2v12c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z',
  orders:     'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM7 7h2v2H7zm4 0h6v2h-6zm-4 4h2v2H7zm4 0h6v2h-6zm-4 4h2v2H7zm4 0h6v2h-6z',
  quote:      'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14zM7 10h10v2H7zm0-4h10v2H7zm0 8h7v2H7z',
  contacts:   'M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z',
  products:   'M17 12h-5v5h5v-5zM16 1v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-1V1h-2zm3 18H5V8h14v11z',
  priceList:  'M20 12V22H4V12M22 7H2v5h20V7zM12 22V7M12 7H7.5a2.5 2.5 0 010-5C11 2 12 7 12 7zM12 7h4.5a2.5 2.5 0 000-5C13 2 12 7 12 7z',
  attributes: 'M17 12h-5v5h5v-5zM16 1v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-1V1h-2zm3 18H5V8h14v11zm-8-3H7v-2h4v2zm4-4H7v-2h8v2z',
  inventory:  'M20 7H4a2 2 0 00-2 2v10a2 2 0 002 2h16a2 2 0 002-2V9a2 2 0 00-2-2zm-9 11H7v-2h4v2zm0-4H7v-2h4v2zm7 4h-5v-6h5v6zM4 5h16v2H4z',
  movements:  'M8 5v14l11-7z',
  ddt:        'M18.92 6.01C18.72 5.42 18.16 5 17.5 5h-11c-.66 0-1.22.42-1.42 1.01L3 12v8c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h12v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-8l-2.08-5.99zM6.5 16c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5zm11 0c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z',
  warehouse:  'M22 8.35V20a2 2 0 01-2 2H4a2 2 0 01-2-2V8.35A2 2 0 011.1 6.5l8-4.67a2 2 0 011.8 0l8 4.67A2 2 0 0122 8.35zM9 22h6v-6H9v6z',
  creditNote: 'M9 11H7v2h2v-2zm4 0h-2v2h2v-2zm4 0h-2v2h2v-2zm2-7h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V9h14v11z',
  company:    'M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z',
  categories: 'M12 2l-5.5 9h11L12 2zm0 3.84L13.93 9h-3.87L12 5.84zM17.5 13c-2.49 0-4.5 2.01-4.5 4.5S15.01 22 17.5 22s4.5-2.01 4.5-4.5-2.01-4.5-4.5-4.5zm0 7a2.5 2.5 0 010-5 2.5 2.5 0 010 5zM3 21.5h8v-8H3v8zm2-6h4v4H5v-4z',
  sizes:      'M21 6.5l-4-4-8.5 8.5-2 4.5 4.5-2L21 6.5zm-14 7l-1.5.67.83-1.83L17.67 1.33 18.83 2.5 7 13.5z',
  colors:     'M12 22C6.49 22 2 17.51 2 12S6.49 2 12 2s10 4.04 10 9c0 3.31-2.69 6-6 6h-1.77c-.28 0-.5.22-.5.5 0 .12.05.23.13.33.41.47.64 1.06.64 1.67A2.5 2.5 0 0112 22zm0-18c-4.41 0-8 3.59-8 8s3.59 8 8 8c.28 0 .5-.22.5-.5a.54.54 0 00-.14-.35c-.41-.46-.63-1.05-.63-1.65a2.5 2.5 0 012.5-2.5H16c2.21 0 4-1.79 4-4 0-3.86-3.59-7-8-7z',
  materials:  'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z',
};

@Injectable({ providedIn: 'root' })
export class MenuService {
  private menuItems: MenuItem[] = [
    {
      label: 'Vendite',
      icon: ICONS.cart,
      isCollapsed: false,
      children: [
        { label: 'Cassa',      route: '/vendite/cassa',          icon: ICONS.cash,     roles: ['Admin', 'Sales'] },
        { label: 'Ordini',     route: '/ordini/elenco',          icon: ICONS.orders,   roles: ['Admin', 'Sales'] },
        { label: 'Preventivi', route: '/fatturazione/preventivi', icon: ICONS.quote,   roles: ['Admin', 'Sales', 'Accounting'] },
        { label: 'Contatti',   route: '/anagrafiche/elenco',     icon: ICONS.contacts, roles: ['Admin', 'Sales', 'Accounting'] }
      ]
    },
    {
      label: 'Acquisti',
      icon: ICONS.cart,
      isCollapsed: true,
      roles: ['Admin', 'Warehouse'],
      children: [
        { label: 'Ordini Fornitore', route: '/acquisti/ordini-fornitore', icon: ICONS.orders, roles: ['Admin', 'Warehouse'] },
        { label: 'Carico Merce', route: '/acquisti/ddt-fornitore', icon: ICONS.ddt, roles: ['Admin', 'Warehouse'] }
      ]
    },
    {
      label: 'Catalogo',
      icon: ICONS.tag,
      isCollapsed: true,
      roles: ['Admin', 'Sales', 'Warehouse'],
      children: [
        { label: 'Prodotti',        route: '/magazzino/prodotti',        icon: ICONS.products,   roles: ['Admin', 'Warehouse'] },
        { label: 'Listini',         route: '/catalogo/listini',          icon: ICONS.priceList,  roles: ['Admin', 'Sales'] },
        { label: 'Attributi', icon: '', isSeparator: true },
        { label: 'Categorie',       route: '/magazzino/categorie',       icon: ICONS.categories, roles: ['Admin', 'Warehouse'] },
        { label: 'Macro Categorie', route: '/magazzino/macro-categorie', icon: ICONS.categories, roles: ['Admin', 'Warehouse'] },
        { label: 'Taglie',          route: '/magazzino/taglie',          icon: ICONS.sizes,      roles: ['Admin', 'Warehouse'] },
        { label: 'Colori',          route: '/magazzino/colori',          icon: ICONS.colors,     roles: ['Admin', 'Warehouse'] },
        { label: 'Materiali',       route: '/magazzino/materiali',       icon: ICONS.materials,  roles: ['Admin', 'Warehouse'] }
      ]
    },
    {
      label: 'Magazzino',
      icon: ICONS.box,
      isCollapsed: true,
      roles: ['Admin', 'Sales', 'Warehouse'],
      children: [
        { label: 'Magazzini', route: '/magazzino/magazzini',  icon: ICONS.warehouse, roles: ['Admin', 'Warehouse'] },
        { label: 'Giacenze',  route: '/magazzino/inventario', icon: ICONS.inventory, roles: ['Admin', 'Sales', 'Warehouse'] },
        { label: 'Movimenti', route: '/magazzino/movimenti',  icon: ICONS.movements, roles: ['Admin', 'Warehouse'] },
        { label: 'DDT',       route: '/ddt/elenco',           icon: ICONS.ddt,       roles: ['Admin', 'Warehouse'] }
      ]
    },
    {
      label: 'Fiscale',
      icon: ICONS.invoice,
      isCollapsed: true,
      roles: ['Admin', 'Sales', 'Accounting'],
      children: [
        { label: 'Fatture',      route: '/fatturazione/fatture',      icon: ICONS.invoice,    roles: ['Admin', 'Sales', 'Accounting'] },
        { label: 'Note Credito', route: '/fatturazione/nota-credito', icon: ICONS.creditNote, roles: ['Admin', 'Sales', 'Accounting'] }
      ]
    },
    {
      label: 'Sistema',
      icon: ICONS.settings,
      isCollapsed: true,
      roles: ['Admin'],
      children: [
        { label: 'Dashboard', icon: '', isSeparator: true },
        { label: 'Panoramica', route: '/dashboard', icon: ICONS.dashboard, roles: ['Admin'] },
        { label: 'Vendite', route: '/dashboard/vendite', icon: ICONS.orders, roles: ['Admin'] },
        { label: 'Anagrafiche', route: '/dashboard/anagrafiche', icon: ICONS.contacts, roles: ['Admin'] },
        { label: 'Magazzino', route: '/dashboard/magazzino', icon: ICONS.movements, roles: ['Admin'] },
        { label: 'Impostazioni', icon: '', isSeparator: true },
        { label: 'Azienda', route: '/impostazioni/azienda', icon: ICONS.company, roles: ['Admin'] },
        { label: 'Formula prezzi', route: '/impostazioni/formula-prezzi', icon: ICONS.priceList, roles: ['Admin'] }
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
