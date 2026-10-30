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

## Fatturazione da ordine

Non si creano fatture indipendenti o in bozza: da un ordine `Completed` un utente
con permessi di fatturazione usa **Genera fattura**. Il numero assegnato segue
`FAT-AAAAmm-NNNNN`; il progressivo mensile è serializzato in transazione e non
viene consumato se la generazione fallisce. Le fatture definitive non si
eliminano e i numeri storici non vengono rinumerati.

La generazione copia le righe e l'eventuale spesa di spedizione classificata
dall'ordine, usando il calcolatore fiscale condiviso. Prima dell'invio a SdI,
aggiunte/rimozioni contabili e variazioni della spedizione sull'ordine
completato aggiornano la fattura nello stesso salvataggio; le righe rimosse
restano nello storico fisico e nei DDT. Dopo l'invio, il timestamp SdI blocca
le modifiche. Invio XML e trasmissione a SdI non sono ancora implementati.

## Acquisti / Carico Merce (DDT fornitore)

La voce **Acquisti > Carico Merce**, subito sotto Vendite, e disponibile ai ruoli
`Admin` e `Warehouse`. La rotta `/acquisti/ddt-fornitore` mostra esclusivamente
l'elenco paginato con ricerca su numero DDT/fornitore/magazzino, fornitore,
stato e intervallo date. **Nuovo carico** apre
`/acquisti/ddt-fornitore/nuovo`; selezionare un documento apre
`/acquisti/ddt-fornitore/:id`. Il vecchio percorso `/acquisti/carico-merce`
reindirizza all'elenco. Il wizard separa i tre step Documento & Fornitore,
Riconciliazione righe e Riepilogo & Conferma; una bozza esistente apre lo step
di riconciliazione, mentre documenti finalizzati sono consultabili nel riepilogo.

Il flusso consente di:

1. Archiviare l'originale PDF, PNG o JPEG (massimo 20 MB) su S3 usando la
   configurazione `S3Storage` gia presente. Il numero DDT puo essere completato dopo.
2. Aggiungere, modificare, eliminare e associare le righe in bozza. Le quantita
   sono **pezzi interi positivi**, rappresentati da `int` nel modello e nei DTO;
   JSON con quantita frazionarie viene rifiutato, mai arrotondato.
3. Importare un payload OCR/IA strutturato tramite il pannello JSON o l'API
   `/parse`. Non e incluso un motore OCR e nessun documento viene inviato
   automaticamente a fornitori IA esterni.
4. Salvare e confermare il carico: testata, movimenti `SupplierStockIn`,
   mapping fornitore/variante e giacenze del magazzino sono aggiornati in una
   transazione PostgreSQL serializzabile. Una versione obsoleta o una doppia
   conferma restituisce `409`; ricaricare il documento prima di riprovare.

Ogni variante puo avere il proprio **Codice fornitore variante** (massimo 100
caratteri), distinto dal codice fornitore generale del prodotto. In fase di
riconciliazione il matching cerca prima il codice sulla variante, limitatamente
al fornitore del DDT e solo quando identifica una variante univoca; poi usa i
mapping appresi alla conferma e, per compatibilita con il catalogo esistente,
il codice generale del prodotto solo se identifica una variante univoca.
I codici duplicati non producono associazioni automatiche e taglie/colori non
vengono indovinati. Una variante selezionata manualmente deve appartenere allo
stesso fornitore. Un codice non puo essere riassociato silenziosamente a una
variante diversa. I prodotti mancanti si creano nel catalogo prima di associare
e confermare le righe.
La migrazione `AddSupplierCodeToProductVariant` aggiunge la colonna nullable
senza riscrivere i codici storici; valorizzare i codici mancanti dal catalogo
prodotti prima di affidarsi al matching diretto per variante.

`Products` ha gia l'indice univoco `(SupplierId, SupplierCode)`. Anche le
varianti applicano il vincolo `(SupplierId, SupplierCode)`, con `SupplierId`
copiato dal prodotto padre e aggiornato quando cambia il fornitore. La migrazione
`AddSupplierVariantSupplierCodeUniqueIndex` crea questo vincolo senza modificare
i codici variante. Prima di applicarla, verificare e risolvere eventuali codici
variante duplicati per lo stesso fornitore:

```sql
SELECT p.supplier_id, v.supplier_code, COUNT(*)
FROM product_variants v
JOIN products p ON p.id = v.product_id
WHERE v.supplier_code IS NOT NULL
GROUP BY p.supplier_id, v.supplier_code
HAVING COUNT(*) > 1;
```

Le modifiche non salvate disabilitano la conferma; documenti confermati o
annullati sono in sola lettura. L'annullamento e ammesso solo prima del carico.
Il documento originale rimane scaricabile attraverso l'API autenticata.
La giacenza del vecchio schema e ancora persistita come `numeric`: questo
flusso accetta e genera esclusivamente interi e rifiuta giacenze storiche
frazionarie, senza convertirle automaticamente.

API principale: `/api/supplier-delivery-notes`, con alias `/api/v1/supplier-delivery-notes`:
`GET /` (parametri opzionali `search`, `supplierId`, `status`, `dateFrom`,
`dateTo`, oltre a `page`/`pageSize`), `GET /{id}`, `POST /` multipart, `GET /{id}/file`,
`PUT /{id}/lines` (sostituzione completa), `POST /{id}/parse`,
`POST /{id}/confirm`, `POST /{id}/cancel`. Le modifiche richiedono `version`.
I costi unitari mantengono 8 decimali e vengono trasmessi come stringhe decimali
per evitare perdite nel frontend; questo flusso non calcola IVA o totali fiscali.
Gli esempi sono in
[SupplierDeliveryNotes.http](../src/Mauw.Erp.Api/SupplierDeliveryNotes.http).

Applicare la migrazione additiva prima dell'uso:

```powershell
dotnet ef database update --project src\Mauw.Erp.Infrastructure --startup-project src\Mauw.Erp.Api
```

La migrazione non riclassifica documenti storici e amplia la precisione dei
costi di magazzino preservando anche la precedente capacita delle cifre intere.
I proxy consentono 21 MB per includere l'overhead multipart del file da 20 MB.

I test PostgreSQL si attivano con `MAUW_TEST_POSTGRES_CONNECTION`, indirizzata a
un database di test gia migrato. Verificano rollback anche dopo `SaveChanges`,
doppia conferma, concorrenza su documenti diversi, overflow e tracciabilita.
Il roundtrip S3 opzionale usa `MAUW_TEST_S3_ENDPOINT`, `MAUW_TEST_S3_ACCESS_KEY`
e `MAUW_TEST_S3_SECRET_KEY`: crea e rimuove un bucket temporaneo dedicato.

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
