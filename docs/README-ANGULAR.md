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

## Loghi aziendali

In **Impostazioni → Azienda** si possono sostituire separatamente il logo
orizzontale (intestazione applicativo) e quello compatto (menu, favicon e PDF), senza
redeploy. Il caricamento richiede il ruolo **Admin** e accetta SVG o PNG fino a
2 MiB. Gli SVG vengono normalizzati nel browser in PNG trasparenti fino a
1600 pixel sul lato maggiore: nel database vengono memorizzati solo i pixel,
non markup SVG eseguibile. Il backend valida formato, dimensioni e leggibilità.

I due loghi sono campi binari dell'anagrafica **Nostra Azienda**
(`contacts`, record `IsInternalCompany`), non URL e non una tabella separata.
Il seed inizializza esclusivamente i campi mancanti con i loghi forniti in
[`media/`](../media/); non sovrascrive le sostituzioni ai riavvii.
I PNG iniziali sono incorporati nell'assembly Infrastructure, così sono
disponibili anche nel container senza accesso al filesystem sorgente.
Le risorse iniziali sono state esportate con Inkscape, area del disegno,
larghezza 1200 px (orizzontale) e 800 px (compatto).
Il compatto usa il simbolo senza testo di
[`mauw-separated-logo-only.svg`](../media/mauw-separated-logo-only.svg);
la variante quadrata con scritta resta disponibile come sorgente alternativa.

La lettura usa `GET /api/v1/settings/company/logos/{horizontal|compact}` e la
sostituzione `PUT` sullo stesso percorso, con JSON `{ "pngData": "<base64>" }`.
Sono risorse locali all'applicativo: non occorrono servizi immagini esterni.
I PDF leggono direttamente i byte dal database tramite DI, senza richieste HTTP.
I PDF già scaricati non vengono modificati; quelli rigenerati usano il logo corrente.
Nelle stampe il logo compatto occupa una colonna a sinistra, entro 1,2 cm di
larghezza e 1,35 cm di altezza. Il blocco aziendale e allineato in alto nella
colonna accanto, senza immagini sulla baseline del testo; i dati documento restano a destra.
Il campo legacy `LogoUrl` resta per compatibilità, ma non viene usato per i loghi.

La migrazione `AddCompanyLogos` aggiunge soltanto le due colonne nullable.
All'avvio l'API applica le migrazioni e poi il seed. Per applicarle manualmente:

```powershell
dotnet ef database update --project src\Mauw.Erp.Infrastructure
```

## Dati aziendali nelle stampe

In **Impostazioni → Azienda** il flag **Capitale sociale interamente versato**
aggiunge `I.V.` al capitale nell'intestazione di ordini, fatture, DDT e note di
credito. Richiede un capitale positivo; non modifica importi o calcoli fiscali.

La sezione **Coordinate bancarie e note fatture** salva banca, IBAN, BIC/SWIFT
e note modificabili nell'anagrafica aziendale. IBAN e BIC vengono normalizzati
e validati prima del salvataggio. I dati sono riportati solo nelle fatture,
non in ordini, DDT o note di credito. Il pulsante per le condizioni proposte
compila il testo di trasporto, che va verificato e salvato dall'utente.
Non viene dichiarata automaticamente emissione XML o trasmissione a SdI.

Il riquadro agente usa l'agente predefinito associato al cliente del documento
(`DefaultAgentId`), non un nome aziendale fisso. Non esiste attualmente una
selezione agente distinta sul singolo ordine. Se assegnato, viene stampata anche
la dicitura sulla provvigione a carico del venditore; un collegamento agente
non valido restituisce un errore esplicito.

La migrazione `AddCompanyInvoiceDetails` e solo additiva: nuovi campi facoltativi
e flag inizialmente disattivato, senza riscrivere documenti o inventare dati bancari.
Le modifiche si vedono nei PDF rigenerati; quelli gia scaricati restano invariati.

## 📁 Struttura

```
src/Mauw.Erp.Web/
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
cd src/Mauw.Erp.Web
npm install
npm start  # Dev server http://localhost:4200
```

### Build per produzione

```bash
cd src/Mauw.Erp.Web
npm run build  # Output in dist/web/browser
```

### Docker

Da PowerShell, genera prima il certificato HTTPS locale (una volta per
macchina; richiede .NET SDK e PowerShell come amministratore):

```powershell
.\generate-dev-cert.ps1
docker compose up --build -d
```

L'applicazione è disponibile su `https://localhost:5019` (o sulla porta
configurata con `HTTPS_PORT`). Il Compose espone il sito tramite il reverse
proxy HTTPS; API e frontend restano sulla rete Docker.

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
- Sezioni espandibili con stato mantenuto dal `MenuService`; il filtro dei ruoli
  nasconde le voci senza clonare le sezioni, per non perdere lo stato al clic.

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
- **Dev e prod**: `/api` (da `environment.ts` e `environment.prod.ts`; il
  reverse proxy Docker inoltra le richieste all'API).

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
