# Mauw ERP — Stato architetturale e funzionale

ERP B2B Pronto Moda Wholesale «mauw». Rilevazione aggiornata al **2026-11-04**,
basata sull'implementazione presente e sui test mirati recenti; non descrive un
particolare branch o stato Git.

## Sintesi esecutiva

- Vendite, banco cassa, ordini, magazzino, DDT e acquisti sono moduli
  implementati; il ritiro in showroom completa un ordine confermato senza
  prelievo, imballaggio o DDT, ma registra lo scarico delle giacenze.
- Fatture, pagamenti, partitario cliente, note di credito, PDF e generazione
  XML FatturaPA/TD04 sono integrati nei flussi applicativi.
- Il ciclo passivo operativo registra fatture fornitori, spese e rimborsi con
  allegati, categorie, approvazione e pagamenti parziali. Non è contabilità
  generale né liquidazione IVA.
- Il workflow SDI valida e archivia l'XML e usa `ISdiTransmissionProvider`.
  Development usa un provider simulato; negli altri ambienti il provider
  predefinito è non configurato. Non è quindi disponibile un collegamento SDI
  reale pronto per la produzione, né l'acquisizione automatica degli esiti.
- Il DataGenerator rimane opzionale e produce dati solo su database dedicati
  `*_loadtest`; include sia ordini spediti sia ritiri in showroom.

---

## 1. Stack tecnologico e principi architetturali

### 1.1 Stack

| Livello | Tecnologia |
|---|---|
| Backend | C# / **.NET 10** (`net10.0`), ASP.NET Core **Minimal API** + endpoint raggruppati (`MapGroup`), MediatR 14.2 per i comandi del flusso ordini |
| Persistenza | **EF Core 10.0.14** + Npgsql 10.0.3, **PostgreSQL 16**, naming `snake_case`, `Database.Migrate()` all'avvio |
| PDF | PDFsharp-MigraDoc 6.2.4 (fattura, DDT, ricevuta, report giacenze) con font embedded |
| Storage file | S3-compatibile (MinIO in compose) per immagini prodotto, loghi e DDT fornitore |
| Autenticazione | **Microsoft Entra ID** (OIDC, pattern BFF con cookie HttpOnly). Ruoli app: `Admin`, `Sales`, `Warehouse`, `Accounting` (il ruolo `Cashier` è stato assorbito da `Sales`) |
| Osservabilità | OpenTelemetry 1.20.0 (tracing, metriche, log) → `otel-collector`; sorgente `Npgsql` tracciata; decorator `Tracing*Service` |
| Integrazioni esterne | VIES (validazione P.IVA UE), Google Places con fallback Photon/OSM (autocomplete indirizzi) |
| Frontend | **Angular 18.2** (componenti standalone), Tailwind 3.4, RxJS 7.8, ApexCharts; test Karma/Jasmine |
| Infrastruttura | Docker Compose: `postgres`, `otel-collector`, `minio` (+ init), `api`, `web`, `proxy` (TLS) |
| Test | xUnit 2.9.3, Moq, Testcontainers.PostgreSql 4.16, PdfPig, coverlet |

Soluzione a livelli (`MauwErp`): `Mauw.Erp.Domain` → `Contracts` → `Application` → `Infrastructure` → `Api`, più `Mauw.Erp.Web` (Angular), `Mauw.Erp.UnitTests` e `Mauw.Erp.DataGenerator` (eseguibile opzionale, nessun riferimento dagli altri progetti).

### 1.2 Principi architetturali

1. **DDD / monolite modulare.** Aggregati con invarianti nel dominio (`Order`, `Invoice`, `Shipment`, `StockItem`, `SupplierPurchaseOrder`). Le transizioni di stato sono metodi dell'aggregato (`Order.Confirm`, `Ship`, `BeginAdjustment`, `Invoice.SyncFromOrder`…), non assegnazioni di proprietà.
2. **Persistenza transazionale.** I flussi locali che modificano documenti e magazzino usano `IUnitOfWork` e transazioni PostgreSQL. Le integrazioni esterne non partecipano a una transazione distribuita; i flussi SDI compensano gli errori con rollback e pulizia dello storage dove possibile. Isolamento:
   - `Serializable`: conferma ordine, vendita cassa, spedizione, carico DDT fornitore;
   - `ReadCommitted` + **advisory lock** PostgreSQL (`pg_advisory_xact_lock`): generazione documenti, rettifica pre-SDI e numerazioni concorrenti.
3. **Flussi sincroni.** Nessun worker asincrono né outbox: la tabella `outbox_messages` introdotta da `AddOrderFulfillmentAndOutbox` è stata rimossa da `RemoveOrderOutboxAndAddPaymentInfo`. I PDF sono generati on-demand; gli XML trasmessi sono archiviati nello storage SDI e i relativi metadati persistiti.
4. **Backend-driven UI (HATEOAS).** `OrderDto` espone `PermittedActions` (azione, etichetta, endpoint, metodo, stile, dialogo richiesto), `IsReadOnly`, `StatusDescription`, `CanEditInvoice`, `IsUnderAdjustment`, `InvoiceSdiStatus`. La logica è in `OrderPermittedActionsPolicy`; il frontend esegue `postAction(action)` senza replicare le regole di stato.
5. **Result pattern.** `OperationResult<T>` (applicazione), `AccountingResult<T>` (contabilità), `OrderWorkflowResult` / `InvoiceWorkflowResult` (dominio): niente eccezioni per input invalidi o stati non ammessi.
6. **Contabilità italiana centralizzata.** `Mauw.Erp.Domain.Services.ItalianAccountingCalculator` (registrato via DI, puro, deterministico, senza I/O) è l'unica fonte di calcolo: `decimal`, `MidpointRounding.AwayFromZero`, `PrezzoTotale` di riga a 2 decimali, prezzi unitari fino a 8, **IVA calcolata una sola volta per gruppo `(AliquotaIVA, Natura)`**, mai per riga. Sconti composti solo in `CombineDiscounts`. UI e PDF non ricalcolano.
7. **Sicurezza lato server.** Policy per gruppo di endpoint (`AdminOnly`, `CatalogManagement`, `WarehouseOperations`, `SalesOperations`, `Billing`, `DdtDocuments`, `StockMovementWrite`, `InventoryStockRead`, `AnyRole`), matrice 401/403 testata; redazione dei costi per i ruoli senza visibilità (`ICostVisibility`).
8. **Auditabilità.** `document_audit_entries` append-only per le variazioni documentali sensibili; `stock_movements` come registro di magazzino con motivo (`StockMovementReason`).

---

## 2. Mappa dei moduli completati e flussi operativi

### 2.1 Catalogo e varianti

- **Entità**: `Product` (fornitore, codice fornitore, barcode, `VatRate`, costo base, categoria/collezione/materiale), `ProductVariant` (colore, taglia, **barcode/SKU**, `PurchaseCost`, `SellingPrice`, peso), `ProductVariantImage`, tabelle di anagrafica (`Category`, `MacroCategory`, `Collection`, `Color`, `Size`, `Material` + composizione).
- **Prezzi**: `SellingPrice` fisso a 8 decimali; **Formula prezzi** (`PriceFormula`/`PriceFormulaStep`, singleton) propone il prezzo dal costo con ricarichi a cascata e arrotondamento per eccesso (solo `Admin`, su richiesta, mai in vendita). **Listini** (`PriceList`/`PriceListRule`: `FixedPrice`, `PercentageDiscount`, risoluzione deterministica con errore sulle parità). Regola `CostMarkup` e motore legacy rimossi.
- **Vincoli**: quantità di magazzino intere (check `ck_stock_items_whole_quantities`, `ck_stock_movements_whole_quantity_delta`, `ck_price_list_rules_whole_minimum_quantity`).
- **Fiscalità**: aliquota 0–100 (max 2 decimali), Natura obbligatoria con aliquota 0 e vietata con aliquota > 0; elenco codici N1…N7 (senza i generici N2/N3/N6).
- **Frontend**: pagine prodotti, categorie, macro-categorie, colori, taglie, materiali, listini, formula prezzi.

### 2.2 Vendite e ordini

Flusso `StandardShipping` (ordine B2B con evasione) e chiusura alternativa per
ritiro diretto:

```text
Draft ─Confirm─▶ Confirmed ─StartPicking─▶ Picking ─Ship─▶ Shipped | PartiallyShipped ─Complete─▶ Completed
                         └─CompleteShowroomPickup───────────────────────────────────────────────▶ Completed
  │ credito oltre fido ▶ PendingApproval ─Approve─▶ Confirmed
  └ Cancel / OnHold / Resume / CancelRemainingLines
```

- **Creazione bozza** (`ordini-nuovo.component.ts`): ricerca varianti (`products.searchVariants` con `availableOnly=true`), scelta iniziale `StandardShipping` o `CashDesk`, note interne e spese di spedizione classificate, anteprima totali dal backend.
- **Numerazione ordine**: `ORD-MMyyyy-NNN` (`PRV-`/`WEB-` per preventivi/ordini web), calcolata sotto advisory lock sul prefisso.
- **Conferma**: `ConfirmOrderAsync` (Serializable) imposta il magazzino di evasione predefinito, applica `CustomerCreditPolicy` (eventuale `PendingApproval`), **riserva** la giacenza.
- **Ritiro in showroom**: da `Confirmed`, l'azione `CompleteShowroomPickup` consuma le riserve e registra i movimenti `ShowroomSale`; non crea `Shipment` o DDT e non richiede un operatore di magazzino. L'ordine salvato assume `FulfillmentType.ShowroomPickup`.
- **Dettaglio guidato dal backend** (`order-detail.component.ts`): bottoni dalle `PermittedActions`, `IsReadOnly`, badge di stato (`StatusDescription`), dialoghi dichiarati dal backend (`CancelReason`, `ShipmentDetails`, `PaymentDetails`).
- **Ruoli**: creazione ordine `Admin`/`Sales`; dettaglio anche `Warehouse` e `Accounting`.

### 2.3 Banco Cassa (POS)

- `FulfillmentType = CashDesk`; sessioni sospendibili (`/api/v1/cassa/sessions`: crea, `pause`, `resume`, `terminate`, elenco sospese).
- **Anteprima fiscale dal backend**: `POST /api/v1/cassa/sessions/preview` (calcolatore condiviso). La cassa invia sempre prezzo netto, sconto, aliquota e Natura, **mai il lordo**; il frontend non ricalcola imponibile/IVA/totali. Sconti di riga composti con causale (`discount-dialog`), applicati dal calcolatore backend.
- **Vendita immediata** (`FinalizeCashSaleCommandHandler`, Serializable): valida giacenza nel magazzino predefinito, `Order.FinalizeCashSale` (Draft → Completed), **scarico istantaneo** (`RecordCashSaleMovementsAsync`), **incasso sincrono** (metodo e dettaglio transazione persistiti), **PDF ricevuta** (`ricevuta-{numero}.pdf`). Errore di salvataggio o di generazione PDF ⇒ rollback di ordine e magazzino insieme (test dedicati).

### 2.4 Logistica, magazzino e spedizioni

- **Coda ordini da spedire**: `GET /api/v1/orders/fulfillment-queue` → pagina `/magazzino/spedizioni` (`shipment-queue.component.ts`, ruoli `Admin`/`Warehouse`), selezione quantità per riga (limitata al residuo), badge «Spedizione Totale / A Saldo» vs «Spedizione Parziale».
- **Spedizione** (`CreateShipmentCommandHandler`, `POST /orders/{id}/ship`, **Serializable**):
  1. da `Picking` esegue implicitamente `CompletePacking`; ammessi `Packing`/`PartiallyShipped`;
  2. calcola i residui per riga e valida le quantità richieste;
  3. numerazione univoca: sequenza PostgreSQL `shipment_document_number_seq` → `DDT-{yyyy}-{seq:D6}` e `SPED-{yyyy}-{seq:D6}`;
  4. crea `Shipment` + `ShipmentLine` (SKU, quantità) e `DdtDocument` collegato (`OrderId`, `ShipmentId`), `OrderItem.RecordShipment`;
  5. verifica giacenza fisica **e** riserva, registra i movimenti `OrderShipment` e scarica lo stock;
  6. transizione a `Shipped` (a saldo) o `PartiallyShipped` (residuo > 0);
  7. **genera in modo sincrono il PDF del DDT** (dati vettore, colli, peso, tracking) prima del commit: se fallisce, rollback di spedizione, stock e ordine.
- **Da `PartiallyShipped`**: nuova spedizione sul residuo, oppure `CancelRemainingLines` (stralcio a saldo, rilascia le riserve residue).
- **Magazzino**: `StockItem` (`QuantityOnHand`, `QuantityReserved`, costo unitario, **token di concorrenza `xmin`**), `StockMovement` con motivi `SupplierReceipt`, `ShowroomSale`, `OrderShipment`, `InternalTransfer`, `CustomerReturn`, `SupplierReturn`, `InventoryAdjustment`, `OrderAdjustment`; tipi di magazzino Standard/Defective/Consignment/Subcontractor/Transit; trasferimenti; export giacenze CSV/PDF; **query di riconciliazione** sui movimenti ambigui (coppie `(WarehouseId, VariantId)`).
- **DDT manuali** (`/api/v1/ddts`, pagine `ddt/*`) restano disponibili, con movimentazione di stock e rollback atomico.

### 2.5 Fatturazione, rettifica e SDI (FatturaPA 1.2.3 FPR12)

**Generazione fatture.** `POST /api/v1/invoices/from-order/{orderId}` (policy
`Billing`) richiede un ordine `Completed`; genera una fattura per ordine con
numero `FAT-{yyyyMM}-{NNNNN}` e progressivo mensile contiguo. Il flusso usa
advisory lock e verifica applicativa. La fattura è confermata alla creazione.
Il PDF è generato alla richiesta; l'XML FPR12 TD01 è costruito dal backend e
validato contro lo schema XSD prima della trasmissione.

**Pagamenti.** Sono disponibili registrazione singola e batch (`/payments`,
`/payments/batch`), aggiornamento del residuo e maturazione delle provvigioni al
saldo. `CreditNoteOffset` collega il pagamento alla nota sorgente e ne scala il
credito residuo in modo atomico. `Payment.PaymentDate` viene normalizzata a UTC
anche quando l'API riceve un `DateTime` senza fuso, in coerenza con
`timestamp with time zone` di PostgreSQL.

**Estratto cliente.** `GET /api/v1/contacts/{id}/statement` aggrega on-demand
fatture confermate, note di credito e pagamenti con query EF Core `AsNoTracking`;
non esiste una tabella movimenti né una migrazione dedicata. Le righe NC
espongono il solo credito residuo non allocato: la quota già applicata è
rappresentata dal pagamento `CreditNoteOffset`, evitando il doppio conteggio nel
saldo progressivo.

**Note di credito.** Una TD04 può essere emessa solo per fatture `Transmitted`
o `Accepted`. La UI mostra la CTA in questi stati; il backend ripete il
controllo, valida righe e importi residui, permette il rientro fisico opzionale
e aggiorna lo stock nella stessa operazione. Sono supportati PDF, generazione
XML TD04 e archiviazione/trasmissione SDI; il credito allocato compare nel
partitario come pagamento `CreditNoteOffset`.

**Workflow SDI.** `SdiStatus` comprende `NotTransmitted`, `Pending`,
`Transmitted`, `Accepted`. La trasmissione di fatture e note di credito valida
l'XML, lo archivia via `ISdiDocumentStorage`, invoca
`ISdiTransmissionProvider` e persiste i metadati/audit dell'invio. In
Development viene registrato `LocalOrSimulatedSdiTransmissionProvider`, che
simula l'invio; fuori da Development il provider predefinito è
`UnconfiguredSdiTransmissionProvider` e rifiuta la trasmissione. Non è presente
un connettore SDI di produzione né l'acquisizione automatica delle ricevute e
degli stati successivi. `IsSdiSubmitted` congela i contenuti da `Transmitted`;
la rettifica pre-SDI è ammessa solo per fatture `NotTransmitted`. Le righe
legacy con `SdiSubmittedAt` valorizzato risultano `Transmitted`.

**Rettifica pre-SDI** (ordine resta `Completed` con `IsUnderAdjustment`):

| Passo | Endpoint | Effetto (transazione unica) |
|---|---|---|
| 1. Riapertura | `POST /orders/{id}/reopen-adjustment` | `Order.BeginAdjustment`; ammessa solo con fattura `NotTransmitted`; audit `OrderReopenedForAdjustment` |
| 2. Modifica righe | `PUT /orders/{id}/adjustment-lines` | `Order.AdjustLines` (pre-validazione pura: nulla cambia se fallisce), delta di magazzino con motivo **`OrderAdjustment`** (scarico aggiuntivo se la quantità cresce, rientro se cala; blocco se la giacenza non basta; magazzino di evasione dell'ordine), audit `OrderLinesAdjusted` |
| 3. Aggiornamento | `POST /orders/{id}/sync-invoice` | `Invoice.SyncFromOrder` (stessa fattura, **stesso id e numero**; rifiuta totali < importo già pagato), ricalcolo del **castelletto IVA multi-aliquota**, `CompleteAdjustment`, audit `InvoiceSynchronizedFromOrder` (con delta imponibile/IVA/totale e castelletto), **verifica di rigenerazione PDF e XML prima del commit** (rollback totale se falliscono) |

Le azioni `ReopenForAdjustment` / `SyncAndFinalizeInvoice` compaiono nelle `PermittedActions` solo per ordini `Completed` con fattura rettificabile; durante la rettifica le altre modifiche contabili sono bloccate.

**XML FatturaPA** (`FatturaPaInvoiceXmlBuilder`, `GET /invoices/{id}/xml`): tracciato **FPR12**, `TipoDocumento` TD01, `ProgressivoInvio` derivato dal numero, output deterministico.

- **Cliente Italia**: identificazione rigorosa, CAP a 5 cifre, `CodiceDestinatario` (codice SDI) normalizzato; la PEC destinatario non è gestita.
- **Cliente Intra-UE**: Natura **`N3.2`** richiesta per le righe a aliquota zero, `CodiceDestinatario = XXXXXXX`, prefisso P.IVA normalizzato (**Grecia: `EL` nel VAT, `IdPaese = GR`**).
- **Cliente Extra-UE**: Natura **`N3.1`**, `XXXXXXX`, identificativo fiscale estero se manca la P.IVA.
- **CAP estero**: se non è di 5 cifre si usa il segnaposto **`99999`** e il CAP reale confluisce nel comune (max 60 caratteri); CAP vuoto ⇒ `99999`.
- **Errori espliciti, nessuna Natura inventata**: aliquota 0 senza Natura, Natura con aliquota > 0, Natura incompatibile con l'area del cliente estero, dati parte mancanti (tutti i problemi vengono elencati).
- **Validazione XSD ufficiale** (`Schema_VFPR12_v1.2.3.xsd`, risorsa embedded nel progetto test; `xmldsig` sostituito da uno stub).

**XML note di credito**: il builder TD04 usa le righe e le classificazioni
esplicite della nota, senza ricavare aliquote medie. Il download è disponibile
da `/api/v1/credit-notes/{id}/xml`; per documenti trasmessi viene restituito
l'XML archiviato.

**Audit trail** (`document_audit_entries`): `DocumentType`, `DocumentId`, `Action`, `Note` (≤ 500), `Details` (≤ 4000), `UserId`, `UserName`, `OccurredAtUtc`; append-only.

### 2.6 Anagrafiche e impostazioni

Contatti con profili cliente/fornitore/agente (provvigioni), indirizzi, persone,
blocco cliente, assegnazione cliente–agente, paesi, validazione P.IVA via VIES,
autocomplete indirizzi (Google Places → fallback Photon) e impostazioni azienda
(sedi, loghi e dati fattura). Il partitario cliente è aggregato dall'API e
visualizzato tramite `CustomerStatementComponent`, condiviso tra dettaglio e
modifica anagrafica. Dashboard analitiche disponibili a `Admin`.

### 2.7 Acquisti e carico fornitori (già implementato)

| Componente | Contenuto |
|---|---|
| Ordini fornitore | `SupplierPurchaseOrder`/`Line`; stati `Draft → Confirmed → PartiallyReceived → Completed / ClosedWithDiscrepancies`; numerazione `OF-{yyyyMMdd}-{seq:D6}` da `supplier_purchase_order_number_seq`; controllo di versione ottimistico |
| Riconciliazione | endpoint `reconciliation` e `shortages` (mancanze), chiusura con discrepanze |
| Resi fornitore | `SupplierReturnDraft` (`Draft → Issued`) |
| DDT fornitore | upload del file su S3, stati `Draft/Parsing/Parsed/Confirmed/Cancelled`, `parse` che **accetta un payload OCR/IA già strutturato** (nessun motore OCR integrato), abbinamento articoli (`Matched`/`NewArticleNeeded`/`Error`), `SupplierArticleMapping` appreso alla conferma |
| Conferma carico | `SupplierDeliveryNoteService.ConfirmAsync` (Serializable): valida l'intero documento, incrementa la giacenza, imposta il costo unitario, registra `SupplierReceipt`, aggiorna l'ordine fornitore |
| Frontend | `acquisti/ordini-fornitore`, `acquisti/ddt-fornitore`, wizard di carico (ruoli `Admin`/`Warehouse`) |

### 2.8 Ciclo passivo operativo

- `PayableDocument` distingue fattura fornitore e rimborso; le righe conservano
  gli importi e la classificazione IVA riportati nel documento sorgente, oltre a
  categoria, centro di costo/progetto e segnalazione `AssetCandidate`.
- Gli allegati PDF/PNG/JPEG sono archiviati su S3. Bozza, invio in approvazione,
  approvazione/rifiuto e pagamenti parziali sono transizioni auditabili con
  controllo di concorrenza.
- Le liste di fatture passive e rimborsi sono separate dalle schermate di
  creazione/dettaglio; ricerca, filtri e pagina vengono preservati al ritorno
  dalla schermata documento.
- Le fatture passive richiedono un fornitore attivo e il numero documento;
  l'indice impedisce duplicati per fornitore, numero e data. I rimborsi sono
  associati all'utente autenticato e visibili al richiedente e ai ruoli
  `Admin`/`Accounting`.
- La registrazione non produce movimenti di magazzino: i DDT fornitore restano
  la fonte dell'entrata merce. Gli importi sono acquisiti dalla fattura, non
  ricalcolati. `AssetCandidate` è solo una segnalazione per il commercialista:
  non esistono registro cespiti o ammortamenti automatici.
- Perimetro non incluso: contabilità generale, registri/liquidazioni IVA,
  fatture passive abbinate automaticamente a ordini/DDT e paghe.

### 2.9 Mappa API principale

`/api/v1/`: `orders` (incl. `fulfillment-queue`, `ship`,
`complete-showroom-pickup`, rettifica pre-SDI), `cassa/sessions`, `invoices`
(pagamenti, PDF/XML, trasmissione singola e batch), `credit-notes` (preview,
emissione, dettaglio, XML e trasmissione; il PDF è servito dall'endpoint
`invoices/{id}/pdf`), `fiscal-documents` (invio batch), `ddts`,
`inventory`, `products`, `contacts` (incl. `/{id}/statement`), `warehouses`,
`price-lists`, `settings`, `supplier-delivery-notes`, `payables` e
`payables/my-reimbursements`, `geo`, `lookups` e `dashboard`. Le fatture passive
sono riservate ad `Admin`/`Accounting`; i rimborsi personali sono accessibili
ai quattro ruoli applicativi. Il ciclo passivo non genera movimenti stock.
Eccezioni al prefisso `v1`: gli ordini fornitore sono solo su
`/api/supplier-purchase-orders`; i DDT fornitore e le giacenze sono esposti sia
su `/api/...` sia su `/api/v1/...`.

---

## 3. Stato del database e migrazioni EF Core

PostgreSQL 16; schema gestito da EF Core e da migrazioni versionate in
`src/Mauw.Erp.Infrastructure/Migrations`. Le sequenze applicative includono
`shipment_document_number_seq` e `supplier_purchase_order_number_seq`.

| Area | Tabelle |
|---|---|
| Anagrafiche | `contacts`, `contact_addresses`, `contact_persons`, `customer_profiles`, `supplier_profiles`, `agent_profiles`, `agent_commissions`, `countries` |
| Catalogo e prezzi | `products`, `product_variants`, `product_variant_images`, `categories`, `macro_categories`, `collections`, `colors`, `sizes`, `materials`, `material_compositions`, `price_lists`, `price_list_rules`, `price_formulas`, `price_formula_steps` |
| Magazzino | `warehouses`, `stock_items` (token `xmin`), `stock_movements` |
| Ordini e spedizioni | `orders`, `order_items`, `shipments`, `shipment_lines`, `ddt_documents`, `ddt_items` |
| Fatturazione e audit | `invoices`, `invoice_items`, `payments`, `credit_notes`, `document_audit_entries` |
| Ciclo passivo | `payable_documents`, `payable_document_lines`, `payable_document_payments` |
| Acquisti | `supplier_purchase_orders`, `supplier_purchase_order_lines`, `supplier_delivery_notes`, `supplier_delivery_note_lines`, `supplier_article_mappings`, `supplier_return_drafts`, `supplier_return_draft_lines` |

### Migrazioni (20, inclusa la baseline)

| # | Migrazione | Contenuto |
|---|---|---|
| 1 | `20261025191133_InitialCreate` | **Baseline**: riunisce le 42 migrazioni storiche (schema e seed equivalenti) |
| 2 | `20261025200020_AddVariantForeignKeys` | FK su `variant_id` di `ddt_items`, `invoice_items`, `order_items`, `stock_movements` |
| 3 | `20261026073120_EnforceWholeMerchandiseQuantities` | Check di quantità intere (stock, movimenti, listini) |
| 4 | `20261028130535_AddOrderFulfillmentAndOutbox` | Campi di evasione su `orders` (tipo, magazzino, operatore, colli, peso, vettore, DDT, sospensione) + tabella outbox |
| 5 | `20261028183926_RemoveOrderOutboxAndAddPaymentInfo` | **Rimuove l'outbox**; aggiunge metodo/dettaglio pagamento all'ordine |
| 6 | `20261028233821_AddOrderInternalNotes` | `orders.internal_notes` |
| 7 | `20261029185405_AddOrderShippingCharge` | Costo, aliquota e Natura della spedizione sull'ordine |
| 8 | `20261029193709_AddShipmentFulfillment` | `shipments`, `shipment_lines`, `order_items.quantity_shipped`, `ddt_documents.shipment_id`, sequenza `shipment_document_number_seq` |
| 9 | `20261029231140_AlterInvoiceItemQuantityToDecimal` | Quantità fiscali frazionarie su `invoice_items` |
| 10 | `20261030072032_AddInvoicePaymentMetadata` | Metodo/riferimento sui pagamenti (nullable, storico preservato) |
| 11 | `20261030130229_AddInvoiceConfirmationWorkflow` | `invoices.confirmed_at` (valorizzato dalla data fattura per lo storico) |
| 12 | `20261030165538_AddInvoiceOrderSynchronization` | `is_billing_only`, `is_excluded_from_invoice`, `sdi_submitted_at`, spedizione sulla fattura |
| 13 | `20261030232428_AddStockItemXminConcurrencyToken` | Token di concorrenza `xmin` su `stock_items` (colonna di sistema) |
| 14 | `20261031114600_AddPreSdiOrderAdjustment` | `invoices.sdi_status`, `orders.is_under_adjustment/adjustment_started_at/adjustment_started_by`, tabella `document_audit_entries` con indice |
| 15 | `20261031144416_AddInvoiceSdiStorageMetadata` | Metadati nullable dell'XML fattura archiviato e della trasmissione |
| 16 | `20261031190947_AddCreditNoteIssuance` | Emissione e righe fiscali delle note di credito |
| 17 | `20261101063125_AddCreditNoteSdiStorageMetadata` | Metadati dell'XML nota di credito e della trasmissione |
| 18 | `20261101175138_AddCreditNoteAllocation` | `credit_notes.allocated_amount`, `payments.source_credit_note_id` nullable con FK `RESTRICT` |
| 19 | `20261104131620_AddPayableDocuments` | Documenti passivi, righe, pagamenti, allegati, stati e audit |
| 20 | `20261104133730_PayableDocumentInvoiceNumberScopedByDate` | Unicità numero fattura fornitore per fornitore e data documento |

Note operative: i database locali creati prima del baseline vanno ricreati o
riallineati nella `__EFMigrationsHistory`. Le migrazioni si creano/applicano
solo con `dotnet ef migrations add` / `dotnet ef database update`; non
riscrivono importi o classificazioni fiscali storiche. `ShowroomPickup` è un
nuovo valore dell'enum persistito come intero e non richiede una migrazione;
la normalizzazione UTC di `PaymentDate` non modifica lo schema.

---

## 4. Test e verifiche

La suite automatizzata copre dominio, servizi, API, persistenza PostgreSQL via
Testcontainers, PDF/XML e componenti Angular. Per una verifica completa:

```powershell
dotnet test src\Mauw.Erp.UnitTests\Mauw.Erp.UnitTests.csproj
Set-Location src\Mauw.Erp.Web
npm run test -- --watch=false --browsers=ChromeHeadless
npm run build
```

I test PostgreSQL richiedono Docker; il round-trip S3 è opzionale e richiede le
variabili `MAUW_TEST_S3_ENDPOINT`, `MAUW_TEST_S3_ACCESS_KEY` e
`MAUW_TEST_S3_SECRET_KEY`. I risultati aggregati cambiano tra esecuzioni e non
sono riportati come stato permanente.

Verifiche mirate del 2026-11-04 (non equivalenti a una suite completa):

- 403 test .NET superati, inclusi test di dominio e PostgreSQL del ciclo
  passivo, matrice autorizzazioni e presidio DataGenerator.
- 7 test Angular superati per la pagina passiva e la navigazione filtrata per
  ruolo.
- `dotnet build` e `npm run build` completati; la build Angular segnala il
  bundle iniziale oltre il budget configurato.
- `dotnet ef migrations has-pending-model-changes` conferma che il modello è
  allineato alle migrazioni.

Le aree principali comprendono calcolo contabile e sconti composti, validazione
XSD FatturaPA, rettifica pre-SDI, trasmissione simulata, note di credito,
movimenti/resi di magazzino, acquisti, autorizzazioni e sicurezza dei costi.

---

## 5. Attività aperte e limiti

| Priorità | Attività | Stato |
|---|---|---|
| P0 | Provider SDI reale e acquisizione esiti | Il workflow, la validazione XML, lo storage e il provider simulato sono presenti; gli ambienti non-Development usano il provider non configurato. Non è integrato un canale reale né la ricezione di accettazioni/scarti. |
| P1 | Hardening API | La policy CORS `AllowAll` e parte della configurazione DI/HTTP restano nel bootstrap API; definire origini per ambiente e continuare a spostare configurazione in extension methods. |
| P1 | Verifica full-suite continuativa | I test mirati recenti sono verdi; la suite completa va rieseguita nell'ambiente CI/Docker per misurarne lo stato aggregato aggiornato. |
| P1 | Abbinamento passivo a ordini/DDT | Le fatture passive sono registrabili, ma non hanno ancora riconciliazione automatica a ordini fornitore o DDT ricevuti. |
| P1 | Report gestionale | Aggiungere report/export operativo di vendite, IVA per aliquota/Natura e costi passivi; non è un registro IVA ufficiale né una liquidazione. |
| P2 | OCR documenti fornitori | Il DDT fornitore accetta payload OCR/IA già strutturati; non contiene un motore OCR né invia file a provider IA esterni. |
| P2 | Test storage S3 in CI | Il test di round-trip richiede endpoint e credenziali temporanei dedicati. |
| P2 | Cespiti e paghe | Le righe `AssetCandidate` sono solo segnalazioni. Registro cespiti/ammortamenti e gestione paghe restano fuori; i riepiloghi possono essere passati ai professionisti. |
| P3 | Regimi fiscali speciali | Bollo, ritenute, casse previdenziali, split payment ed esigibilità IVA non fanno parte del perimetro corrente. |

---

## 6. Invarianti operative

- Contabilità: usare `ItalianAccountingCalculator`, `decimal` e
  `MidpointRounding.AwayFromZero`; IVA per gruppo `(aliquota, Natura)`;
  importi a due decimali, prezzi unitari fino a otto; validare il documento
  prima di mutare righe e totali. Non inventare Nature per documenti storici.
- Ordini fornitore: mantenere `OF-{yyyyMMdd}-{sequenza:D6}` e
  `supplier_purchase_order_number_seq`.
- Riconciliazione: restringere le query alle coppie effettive
  `(WarehouseId, VariantId)`.
- Pagamenti: normalizzare le date a UTC prima della persistenza PostgreSQL.
- Ciclo passivo: conservare gli importi IVA e i riferimenti come riportati nei
  documenti ricevuti; la registrazione passiva non muove lo stock né calcola
  ammortamenti. Gli importi non sostituiscono i registri ufficiali del
  commercialista.
- UI: tabelle paginate, componenti condivisi per paginazione/azioni/aggiunta,
  conferma esplicita per eliminazioni e pulsante condiviso per tornare indietro.
- Migrazioni: usare `dotnet ef migrations add` e `dotnet ef database update`.
- DataGenerator: mantenerlo opzionale e isolato su database `*_loadtest`;
  aggiornare implementazione, test e README quando cambiano flussi documentali.
