# Mauw ERP - Angular Admin Dashboard

La parte web è stata implementata come **sito amministrativo** usando **Angular 18** con **Tailwind CSS 3** e ispirato al template TailAdmin.

## 🎨 Caratteristiche

- ✅ Dashboard amministrativa moderna e responsive
- ✅ Sidebar con navigazione
- ✅ Header con user menu
- ✅ Tabelle dati con badge di stato
- ✅ Carte statistiche (KPI)
- ✅ Dark mode supportato
- ✅ Componenti standalone Angular
- ✅ Styling con Tailwind CSS

## 📁 Struttura

```
src/web/
├── src/
│   ├── app/
│   │   ├── pages/
│   │   │   └── dashboard/          # Dashboard principale
│   │   ├── shared/
│   │   │   └── layout/
│   │   │       ├── sidebar/        # Navigazione sidebar
│   │   │       ├── header/         # Header top bar
│   │   │       └── admin-layout.component.ts
│   │   ├── services/               # ApiService per backend
│   │   ├── app.routes.ts           # Routing
│   │   └── app.component.ts        # Root component
│   ├── environments/               # Dev/prod config
│   ├── index.html
│   ├── main.ts
│   └── styles.scss                 # Global Tailwind styles
├── tailwind.config.js              # Tailwind configuration
├── postcss.config.js               # PostCSS for Tailwind
├── package.json
└── angular.json
```

## 🚀 Quick Start

### Sviluppo locale

```bash
cd src/web
npm install
npm start  # Dev server http://localhost:4200
```

### Build per produzione

```bash
cd src/web
npm run build  # Output in dist/web/browser
```

### Docker

```bash
docker-compose up  # Tutto insieme (API + Web su Nginx)
```

## 🎯 Layout

### Admin Layout (`admin-layout.component.ts`)
- Sidebar a sinistra (fixed 256px)
- Header in alto
- Main content area che scorre
- Responsive: sidebar scompare su mobile, header rimane sempre visibile

### Sidebar
- Logo/Branding
- Menu con link ai moduli
- Highlight del route attivo
- Icon SVG per ogni voce

### Header
- Search bar (hidden su mobile)
- User profile avatar
- Spazio per notifiche/menu aggiuntive

### Dashboard
- 4 Stat Cards (Users, Sales, Orders, Revenue)
- Chart placeholder (ready per ApexCharts)
- Recent Orders table con status badges

## 🔌 API Communication

Usa `ApiService` in `src/app/services/api.service.ts`:

```typescript
import { ApiService } from '@app/services/api.service';

constructor(private api: ApiService) {}

// Esempio
this.api.get<Order[]>('orders').subscribe(orders => {
  console.log(orders);
});
```

**URLs:**
- **Dev**: `http://localhost:5019/api` (from `environment.ts`)
- **Prod**: `/api` (from `environment.prod.ts`)

## 🎨 Styling

- **Tailwind CSS 3**: Utility-first CSS framework
- **SCSS**: Per logica di styling complessa
- **Dark Mode**: Supportato via classe `dark` sul root
- **Colori**: Personalizzabili in `tailwind.config.js`

### Aggiungere una nuova pagina

1. Crea cartella in `src/app/pages/`
2. Genera componente standalone
3. Aggiungi route in `app.routes.ts`
4. Aggiungi link nel sidebar

Esempio:
```typescript
// src/app/pages/users/users.component.ts
import { Component } from '@angular/core';

@Component({
  selector: 'app-users',
  standalone: true,
  template: `<h1>Users Management</h1>`
})
export class UsersComponent {}
```

## 📦 Dipendenze principali

- `@angular/core` ^18.2.0
- `@angular/router` ^18.2.0
- `tailwindcss` ^3.4.0
- `autoprefixer` ^10.6.0
- `postcss` ^8.5.0

## 🔄 Prossimi passi

- [ ] Integrare ApexCharts per grafici
- [ ] Aggiungere pagina Utenti
- [ ] Aggiungere modulo Form (reattivi)
- [ ] Implementare autenticazione
- [ ] Aggiungere notifiche/toast
- [ ] Responsive mobile menu per sidebar

