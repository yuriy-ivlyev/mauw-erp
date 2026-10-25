# Product Variant Management Implementation Summary

## Listini prezzi EUR

Il modulo usa un aggregato `PriceList`, regole condizionali e un risolutore puro
registrato tramite DI. La cascata e SKU, prodotto, categoria, globale, prezzo
base del catalogo. Il prezzo base del catalogo e il prezzo di vendita salvato
sulla variante, mai ricalcolato dal costo. Le regole possono fissare un
prezzo, scontare il prezzo base oppure applicare un ricarico al costo variante.
Non si cumulano regole: entro il livello piu specifico vince la priorita numerica
maggiore; una parita applicabile restituisce un errore esplicito. Le soglie
quantita si applicano alla singola riga quotata, non alla somma del documento.
Le finestre temporali sono inclusive e memorizzate in UTC.

- [API e richieste di esempio](../src/Mauw.Erp.Api/PriceLists.http):
  CRUD `/api/v1/price-lists`, azione `/activation` e quotazione batch `/resolve`.
  La configurazione richiede Admin/Sales; lettura e quotazione sono disponibili
  anche ai ruoli operativi Accounting/Warehouse (il ruolo `Cashier` e stato unificato in `Sales`).
  Il mapping OIDC Entra usa `MapInboundClaims=true` e `ClaimTypes.Role` sia per
  l'autorizzazione API sia per i ruoli restituiti da `/api/auth/user`.
  Dopo aggiornamenti del mapping o delle assegnazioni Entra occorre uscire e
  rientrare per rinnovare il cookie BFF; un utente senza ruolo ammesso resta 403.
- La pagina **Catalogo > Listini** gestisce listini e regole con ricerca bersagli,
  date, soglie e priorita; il profilo cliente ha il listino predefinito.
  Per un bersaglio SKU ricerca direttamente le varianti per barcode, nome prodotto,
  colore o taglia, mostrando barcode/colore/taglia e salvando l'ID della variante.
  Usa il lookup anagrafico, non la matrice prezzi: anche SKU con prezzo catalogo
  ancora da completare possono essere selezionate per configurare una regola.
  La cassa carica quotazioni batch; ogni sconto, di listino o di cassa, ha la propria causale.
- Il riferimento cliente usa `ON DELETE SET NULL`. Le regole hanno indici
  separati `(listino, variante)`, `(listino, prodotto)`, `(listino, categoria)`.
  I listini inattivi o inesistenti generano errori; non si ripiega silenziosamente
  su un prezzo diverso.
- Prezzi e soglie sono `decimal`, con precisione persistita `(22,8)`.
  La quotazione non calcola IVA di riga. `ItalianAccountingCalculator` resta
  l'unica fonte fiscale: sconto applicato una volta, totali riga a due decimali,
  IVA per gruppo aliquota/Natura e arrotondamento `AwayFromZero`.
- Ordini/sessioni cassa, fatture e DDT conservano scalari fiscali e uno snapshot
  immutabile della quotazione. Gli ID listino/regola nello snapshot sono solo
  informativi, senza FK: modifiche o cancellazioni non riprezzano lo storico.
  Il prezzo unitario fiscale e pre-sconto; `AgreedUnitNetPrice` nello snapshot
  e un dato informativo scontato a otto decimali, non la base da moltiplicare per
  ricalcolare i totali. I PDF usano i valori fiscali persistiti della riga.
- Le richieste item accettano `priceListId`, `overrideReason`,
  `isCustomerReturn`, `sourceLineId` e `sourceOrderItemId`.
  Il reso deve partire dalla riga della fattura originale: copia prezzo unitario,
  sconto, aliquota e Natura gia persistiti, verificando cliente e variante,
  senza invocare il motore listini e senza ricalcolare il prezzo.
  Non sono ammessi resi senza fattura o prezzi forzati. Lo snapshot informativo
  del prezzo scontato viene copiato se presente; sulle vecchie fatture che ne
  sono prive resta null, senza ricostruire un prezzo storico. Solo gli importi
  fiscali del reso vengono calcolati sulle quantita restituite.
  `sourceOrderItemId` copia invece lo snapshot dell'ordine salvato per emettere
  fattura/DDT senza riquotare. Non sono accettati prezzi/sconti alternativi.
  I documenti storici privi di snapshot richiedono revisione esplicita.
- In cassa l'operatore non modifica mai il prezzo: il prezzo netto arriva sempre
  dal listino (o dal prezzo di vendita) e non esiste alcun override del prezzo.
  Puo solo aggiungere o modificare **lo sconto della cassa** (vedi
  [Sconti di riga con causale](#sconti-di-riga-con-causale)). Senza sconto di
  cassa, `isManualPriceOverride=false` con un listino fa ricalcolare al server
  i prezzi del lotto; senza listino rimangono compatibili i prezzi espliciti del
  flusso preesistente delle API documentali. La cassa autosalva prima
  dell'emissione e usa gli ID delle righe salvate. Il flusso preesistente di
  emissione documento e movimentazione magazzino resta multi-richiesta, non una
  nuova transazione distribuita. Le schermate autonome ordine/preventivo restano
  i segnaposto preesistenti: la compilazione operativa e nella cassa.

### Sconti di riga con causale

Ogni riga puo avere piu sconti, ciascuno con la propria **causale**, ad esempio
`1) Sconto magliette 5%`, `2) Sconto catalogo 3%`, `3) Sconto alla cassa 5%`.

- **Origine.** `PriceList`: deriva dalla regola di listino, la cui causale e il
  campo *Causale sconto* della regola (Catalogo > Listini); se manca si usa
  `Sconto listino <nome listino>`. `Cashier`: lo sconto deciso alla cassa.
  Gli sconti di listino sono in sola lettura in cassa.
- **Quanti sconti di listino.** Il risolutore sceglie una sola regola per riga
  (le regole non si cumulano, vedi sopra), quindi oggi la riga ha al piu uno
  sconto di listino piu quello della cassa. Modello, snapshot e interfaccia
  gestiscono un elenco: cumulare piu regole di listino e una decisione
  commerciale separata, non ancora presa.
- **Sconto della cassa.** Percentuale (maggiore di 0, al massimo 100) oppure
  valore fisso in euro (maggiore di 0, al massimo 2 decimali), con causale
  obbligatoria (massimo 500 caratteri). Il valore fisso e uno sconto sul
  *totale di riga* e non puo superare quanto resta dopo gli sconti precedenti.
  Non e ammesso sui resi da fattura ne insieme a un prezzo manuale.
- **Composizione, nel solo calcolatore.**
  `ItalianAccountingCalculator.CombineDiscounts` applica gli sconti nell'ordine
  (listino, poi cassa): le percentuali si compongono sul residuo, un importo
  si sottrae dal residuo. Restituisce **un'unica percentuale effettiva**,
  arrotondata una sola volta a 10 decimali (`AwayFromZero`), usata dalla formula
  di riga gia in vigore: lo sconto resta applicato una sola volta al totale di
  riga e il totale di riga resta a 2 decimali. Esempio: 5%, 3%, 5% su 100,00
  danno 12,4575% e 87,54. Un valore fisso di 4,20 su 5 x 18,84 da 90,00 esatti.
  Gli importi risparmiati per sconto servono solo alla presentazione e possono
  differire dal totale per arrotondamento. Frontend, PDF e mapper non ricalcolano.
- **Server autorevole.** Alla creazione/aggiornamento della sessione il server
  risolve di nuovo prezzo e sconto di listino e vi compone lo sconto di cassa:
  non si fida di prezzo o sconto inviati dal client. L'anteprima
  `POST /api/v1/cassa/sessions/preview` accetta `cashDiscount`
  (`percent` oppure `amount`, `reason`) e `priceListDiscountReason`, e
  restituisce per riga `discountedUnitPrice` e `discounts` con
  `source`, `reason`, `percent`/`amount` e `savedAmount`.
- **Persistenza.** La riga conserva la percentuale effettiva in `DiscountPercent`
  (numeric); lo snapshot conserva l'elenco in `pricing_discounts` (jsonb) su
  `order_items`, `ddt_items` e `invoice_items`, copiato all'emissione di DDT e
  fattura. `IsManualOverride` e vero se c'e uno sconto di cassa e
  `OverrideReason` ne contiene la causale (compatibilita). Le righe storiche
  senza elenco restano valide e non vengono riscritte; una sessione sospesa
  salvata con il vecchio sconto manuale unico viene ripresa come sconto di cassa
  percentuale con la sua causale.
- **Interfaccia di cassa.** Su ogni riga un piccolo pulsante con il glifo della
  percentuale apre il dialogo *Sconti sulla riga*: elenca gli sconti numerati con
  causale, mostra i prezzi e permette di aggiungere, modificare o togliere lo
  sconto della cassa scegliendo `%` o `€`. Se la riga e scontata (da listino o
  da cassa) il prezzo di listino e **barrato in rosso** e il nuovo prezzo e
  **in verde**, con la percentuale effettiva. Prezzi e percentuali mostrati
  arrivano dal backend. Il PDF stampa la percentuale effettiva con al massimo
  due decimali, solo per presentazione: il totale di riga e quello persistito.
- **Test.** `DiscountCompositionTests` (composizione, importi, arrotondamento,
  input invalidi, determinismo), `PriceListServiceTests` (sconto di cassa per
  percentuale e valore, composizione col listino, causali, rifiuti, persistenza
  dell'elenco), `DocumentAccountingServiceTests` (anteprima), oltre a
  `discount-dialog.component.spec.ts`, `banco-cassa.component.spec.ts`,
  `cassa-session.service.spec.ts` e `listini.component.spec.ts`.

### Migrazione e verifiche

La migrazione `AddPriceListsAndPricingSnapshots` e generata con
`dotnet ef migrations add`; aggiunge colonne snapshot nullable senza riscrivere
importi o classificazioni storiche. Per applicarla usare `dotnet ef database update`.
La migrazione `AddDiscountBreakdown` aggiunge, sempre nullable, `description`
(max 200) a `price_list_rules` e `pricing_discounts` (jsonb) a `order_items`,
`ddt_items` e `invoice_items`; non modifica righe esistenti. L'API applica le
migrazioni all'avvio.
La factory EF accetta `ConnectionStrings__DefaultConnection`, anche per database
di test isolati. Eventuali vecchi riferimenti cliente a listini inesistenti devono
essere revisionati prima di introdurre la FK: non vengono corretti automaticamente.

Il bootstrap di un database completamente vuoto incontra un problema precedente
al modulo: `RefactorCassaSessionToOrder` elimina `cassa_sessions`, che non viene
creata dalla catena di migrazioni presente. La verifica PostgreSQL del modulo usa
un database isolato con tale tabella legacy vuota come fixture; nessuna migrazione
storica e stata modificata.

I test xUnit coprono cascata, priorita/ambiguita, soglie e date, formule/precisione,
immutabilita, caricamento batch di 1000 righe, sconti di cassa, resi e copia degli
snapshot all'emissione, oltre alle regressioni fiscali e ai contratti HTTP.
Il test PostgreSQL esegue una transazione con rollback su un database gia migrato:
impostare `MAUW_TEST_POSTGRES_CONNECTION` per abilitarlo; altrimenti e
esplicitamente ignorato dal runner.

### Prezzo di vendita prodotto e variante

L'unico dato commerciale corrente e `SellingPrice`, un prezzo **unitario netto,
IVA esclusa e prima degli sconti**, persistito a otto decimali sulla SKU e,
solo per prodotti semplici, anche sul prodotto. Le API ricevono questo valore, non una formula,
una modalita o una percentuale. Anche zero e un prezzo esplicito valido.

- Il form permette l'inserimento diretto oppure l'azione **Applica ricarico al
  prezzo**. Quest'ultima usa `POST /api/v1/products/calculate-selling-price` con
  `purchaseCost` e `markupRate`, calcolati in `decimal` dal backend e arrotondati
  a otto decimali `AwayFromZero`. E un aiuto transitorio: non salva nulla.
  Successivamente si salva solo `sellingPrice`; il ricarico non viene ricordato.
- `HasVariants` distingue prodotti con colore/taglia (default) e articoli semplici.
  Anche colore singolo e taglia unica sono un prodotto con varianti.
  Sul padre con varianti il prezzo non e richiesto ne mostrato: le scritture
  salvano null, senza riprezzare le SKU e senza migrazioni dei dati storici.
  Ogni nuova variante richiede un proprio prezzo, senza copiarlo dal padre.
  Gli articoli semplici richiedono un prezzo e hanno una sola SKU tecnica,
  sincronizzata sul valore esplicitamente inserito. Non e consentito aggiungerne
  altre o cancellarla separatamente. Con SKU gia presenti non si puo cambiare
  tipologia per evitare conversioni e riprezzamenti impliciti.
- Nell'elenco prodotti la vendita netta mostra il prezzo comune alle varianti
  oppure **Da ... a ...** se differiscono; zero e valido. Le varianti mancanti
  sono segnalate come prezzi da completare, senza trasformarle in zero;
  in assenza di SKU si mostra **Nessuna variante**. Non viene usato un prezzo padre.
  Cambiare costo non ricalcola mai il prezzo di vendita salvato.
- Catalogo, barcode, matrice e fallback listini leggono il valore salvato.
  Le regole condizionali dei listini rimangono separate: sconti sul prezzo
  salvato, prezzi di listino espliciti o ricarichi di listino sul costo.
- Le colonne percentuali legacy restano solo per preservare dati preesistenti:
  non sono esposte nei contratti, applicate o aggiornate dal form. Il costo di
  acquisto non modifica mai automaticamente `SellingPrice`.
- La migrazione `PersistDefinitiveSellingPrices`, generata con `dotnet ef`,
  rinomina `fixed_selling_price` in `selling_price` preservando i valori.
  Non ricostruisce automaticamente prezzi mancanti da ricarichi legacy:
  i null sono visibili nel catalogo come **Prezzo da completare**, modificabili
  e bloccano la quotazione/vendita con errore esplicito fino alla revisione.
  I salvataggi delle SKU e dei prodotti semplici richiedono un prezzo valido.
- Documenti gia emessi, snapshot e resi da fattura restano invariati.

## Changes Made

### 1. **Component Updates** (`prodotti.component.ts`)

#### Imports
- Added: `ColorDto`, `SizeDto`, `CategoryDto`, `CollectionDto`, `MaterialDto`, `ProductVariantDto`
- Updated to support new variant management flow

#### Properties Added
- **Support Data**: `colors[]`, `sizes[]`, `categories[]`, `collections[]`, `materials[]`
- **Modal State**: `activeTab` (details | variants | gallery)
- **Variant Management**: 
  - `productVariants[]` - list of existing variants
  - `newVariantForm` - form state for adding variants
  - `loadingVariants`, `savingVariant`, `variantError`, `deletingVariantId` - state flags

#### Methods Added/Modified

**Data Loading:**
- `ngOnInit()` - now loads support data + products
- `loadSupportData()` - fetches colors, sizes, categories, collections, materials from API
- `loadVariants(productId)` - fetches variants for selected product

**Variant Management:**
- `addVariant()` - validates and creates new variant with colorId, sizeId, barcode, purchaseCost
- `deleteVariant(variantId)` - deletes variant after confirmation
- `getColorHtml(colorId)` - returns HTML color code for color indicator
- `getColorName(colorId)` - returns color name for display

**Form Updates:**
- `initializeProductForm()` - removed variants array
- `initializeVariantForm()` - new helper for variant form state
- `saveProduct()` - now only saves product without variants; after creation, loads variants tab
- `editProduct()` - loads product data and fetches variants

**Gallery Integration:**
- `openGalleryModal()` - now opens edit modal on gallery tab
- Gallery functionality preserved (add, remove, set main image)

### 2. **Template Updates**

#### Tab Navigation
Three tabs: Dettagli Prodotto | Varianti | Galleria (last two only show when editing)

#### Tab 1: Dettagli Prodotto (Product Details)
- Existing fields: Codice Interno, Nome Prodotto, Codice Fornitore, Markup Rate, VAT Rate
- New dropdowns:
  - Categoria (nullable)
  - Collezione (nullable)
  - Materiale (nullable)
- Form validation error display

#### Tab 2: Varianti (Variant Management)
**Existing Variants Table:**
- Columns: Colore (with HTML color dot), Taglia, Barcode, Costo Acquisto
- Color display with visual indicator using `[style.backgroundColor]`
- Delete action button per variant

**Add New Variant Form:**
- Color selector with live color preview (shows HTML color dot)
- Size selector dropdown
- Barcode input (required, must be unique)
- Purchase Cost input (number field)
- "Aggiungi Variante" button
- Variant error display

#### Tab 3: Galleria (Gallery - preserved from original)
- Image preview and thumbnails
- Add/remove/set main image functionality
- Unchanged from original

### 3. **Model Updates** (`product.model.ts`)

**Fixed:**
- Removed duplicate/malformed `CreateProductVariantRequest` definitions
- Removed orphaned properties

**Added:**
- `AddProductImageRequest` interface with `url` and `isMain` properties

**CreateProductVariantRequest now:**
```typescript
{
  productId: string;
  colorId: string;
  sizeId: string;
  barcode: string;
  purchaseCost: number;
}
```

### 4. **API Integration**

Uses existing ProductService methods:
- `getColors()`, `getSizes()`, `getCategories()`, `getCollections()`, `getMaterials()`
- `getProductVariants(productId)`
- `createProductVariant(request)` - sends colorId/sizeId instead of text
- `deleteProductVariant(productId, variantId)`
- `createProduct(request)` - no variants in request
- `updateProduct(id, request)` - no variants in request

## Workflow

### Create New Product
1. Click "+ Nuovo Prodotto"
2. Fill in product details tab (required: Codice Interno, Nome)
3. Optionally select Categoria, Collezione, Materiale
4. Click "Crea Prodotto"
5. After creation, automatically switches to Varianti tab
6. Add variants with color/size/barcode/cost
7. Can switch to Galleria tab to add product images

### Edit Existing Product
1. Click "Modifica" on product row
2. Details tab loads with current product data
3. Switch to Varianti tab to view/add/delete variants
4. Switch to Galleria tab to manage images
5. Click "Salva" to save any changes to product details

### Manage Variants
- **View**: Existing variants shown in table with color indicators
- **Add**: Fill form and click "Aggiungi Variante"
  - Validates required fields
  - Checks for duplicate barcodes
  - Refreshes table on success
- **Delete**: Click "Elimina" button, confirm, table refreshes

## Features

✓ Color selector with visual HTML color indicators  
✓ Barcode uniqueness validation  
✓ Separate product creation (without variants)  
✓ Separate variant management (add/delete only, no edit)  
✓ Support data dropdowns (Category, Collection, Material)  
✓ Image gallery integration in modal tab  
✓ Loading states and error messages  
✓ Preserved existing list view and functionality  
✓ Tab navigation for better UX  
✓ Responsive design with Tailwind CSS  

## Testing Scenarios

1. **Create Product without Variants**
   - Create a new product
   - Fill required fields
   - Click "Crea Prodotto"
   - Verify product appears in list
   - Edit to add variants

2. **Add Variants with Different Colors**
   - Edit product
   - Switch to Varianti tab
   - Add variant with Red color, Size L, Barcode ABC123, Cost 10.00
   - Add variant with Blue color, Size M, Barcode ABC124, Cost 12.00
   - Verify both appear in table with correct color dots

3. **Delete Variant**
   - In Varianti tab, click "Elimina" on a variant
   - Confirm deletion
   - Verify variant removed from table

4. **Validation**
   - Try to add variant with missing fields - should show error
   - Try to add variant with duplicate barcode - should show error
   - Try to create product without name - should show error

5. **Gallery Integration**
   - Edit product
   - Switch to Galleria tab
   - Add image with URL
   - Set as main
   - Verify changes persist

## No Breaking Changes

- Product list view unchanged
- Image gallery modal integrated into edit modal
- All existing filtering/search functionality preserved
- Existing styling and layout patterns maintained
- Image thumbnail, name, supplier columns preserved
