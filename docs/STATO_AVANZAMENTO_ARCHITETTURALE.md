# Mauw ERP — Stato di avanzamento architetturale e funzionale

ERP B2B Pronto Moda Wholesale «mauw». Documento di riferimento (*single source of truth*) per riprendere lo sviluppo in una nuova sessione.

| | |
|---|---|
| Data rilevazione | 2026-10-31 |
| Branch / ultimo commit | `develop` / `0541704` |
| Stato working tree | **Non committato**: 41 file modificati + 22 elementi non tracciati (rettifica pre-SDI, XML FatturaPA, audit, token `xmin`, relative migrazioni e test). Vedi §6. |
| Metodo | Dati ricavati dal codice e da test/build eseguiti il 2026-10-31, non da documentazione precedente. |

## Sintesi esecutiva

- Il ciclo **ordine → evasione/DDT → fattura → rettifica pre-SDI → XML FatturaPA** è implementato end-to-end, transazionale e coperto da test (anche su PostgreSQL reale via Testcontainers).
- Il modulo **Acquisti è già implementato** (ordini fornitore, DDT fornitore con upload su S3, carico magazzino, riconciliazione, resi): non è un work item da zero (vedi §2.7 e §5).
- **Manca il collegamento con lo SDI**: il dominio modella `SdiStatus`, ma nessun codice trasmette o fa avanzare lo stato oltre `NotTransmitted`. Oggi ogni fattura è quindi sempre rettificabile.
- Test backend: **1244 verdi su 1246** (1 ignorato, 1 timeout infrastrutturale transitorio che passa in isolamento). Test frontend: **257 verdi su 260**, 3 falliti per spec obsolete preesistenti. Build Angular riuscita con warning di budget.

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
2. **Persistenza transazionale atomica.** Ogni flusso che tocca documento, magazzino e PDF/XML è un'unica transazione (`IUnitOfWork.BeginTransactionAsync` … `CommitAsync`/`RollbackAsync`). Isolamento:
   - `Serializable`: conferma ordine, vendita cassa, spedizione, carico DDT fornitore;
   - `ReadCommitted` + **advisory lock** PostgreSQL (`pg_advisory_xact_lock`): generazione fattura, rettifica pre-SDI, numerazione ordini/fatture.
3. **Flussi sincroni.** Nessun worker asincrono né outbox: la tabella `outbox_messages` introdotta da `AddOrderFulfillmentAndOutbox` è stata **rimossa** da `RemoveOrderOutboxAndAddPaymentInfo`. PDF e XML sono **derivati on-demand dallo stato persistito** (non sono salvati); nei flussi critici la loro generazione è verificata *dentro* la transazione e un errore provoca il rollback completo.
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

Flusso `StandardShipping` (ordine B2B con evasione):

```text
Draft ─Confirm─▶ Confirmed ─StartPicking─▶ Picking ─Ship─▶ Shipped | PartiallyShipped ─Complete─▶ Completed
  │ credito oltre fido ▶ PendingApproval ─Approve─▶ Confirmed
  └ Cancel / OnHold / Resume / CancelRemainingLines
```

- **Creazione bozza** (`ordini-nuovo.component.ts`): ricerca varianti (`products.searchVariants` con `availableOnly=true`, solo varianti con giacenza disponibile), scelta **tipologia evasione** `StandardShipping` vs `CashDesk`, **note interne persistite** (`orders.internal_notes`), spedizione con aliquota/Natura espliciti, anteprima totali dal backend.
- **Numerazione ordine**: `ORD-MMyyyy-NNN` (`PRV-`/`WEB-` per preventivi/ordini web), calcolata sotto advisory lock sul prefisso.
- **Conferma**: `ConfirmOrderAsync` (Serializable) imposta il magazzino di evasione predefinito, applica `CustomerCreditPolicy` (eventuale `PendingApproval`), **riserva** la giacenza.
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

### 2.5 Fatturazione e rettifica pre-SDI (FatturaPA 1.2.3 FPR12)

**Generazione.** `POST /api/v1/invoices/from-order/{orderId}` (policy `Billing`) richiede un ordine `Completed`; genera **una fattura per ordine** (garantito da advisory lock `invoice-order:{id}` + controllo applicativo; l'indice su `OrderId` **non** è univoco). Numero `FAT-{yyyyMM}-{NNNNN}` con verifica di contiguità; fattura già confermata (`ConfirmedAt`). Pagamenti (`RecordPayment`, metodo/transazione), note di credito, righe «solo fatturazione» e spese di spedizione esplicite nel castelletto.

**Stato SDI** (`SdiStatus`): `NotTransmitted`, `Pending`, `Transmitted`, `Accepted`. Guardie: `IsSdiSubmitted` (≥ `Transmitted` ⇒ contenuto congelato), `Invoice.CanBeAdjustedBeforeSdi` (solo `NotTransmitted`). Le righe legacy con `SdiSubmittedAt` valorizzato risultano `Transmitted`.

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

**Audit trail** (`document_audit_entries`): `DocumentType`, `DocumentId`, `Action`, `Note` (≤ 500), `Details` (≤ 4000), `UserId`, `UserName`, `OccurredAtUtc`; append-only.

### 2.6 Anagrafiche e impostazioni

Contatti con profili cliente/fornitore/agente (provvigioni), indirizzi, persone, blocco cliente, assegnazione cliente–agente, paesi, **validazione P.IVA via VIES**, **autocomplete indirizzi** (Google Places → fallback Photon), impostazioni azienda (sedi, loghi), dashboard analitiche (`Admin`).

### 2.7 Acquisti e carico fornitori (già implementato)

| Componente | Contenuto |
|---|---|
| Ordini fornitore | `SupplierPurchaseOrder`/`Line`; stati `Draft → Confirmed → PartiallyReceived → Completed / ClosedWithDiscrepancies`; numerazione `OF-{yyyyMMdd}-{seq:D6}` da `supplier_purchase_order_number_seq`; controllo di versione ottimistico |
| Riconciliazione | endpoint `reconciliation` e `shortages` (mancanze), chiusura con discrepanze |
| Resi fornitore | `SupplierReturnDraft` (`Draft → Issued`) |
| DDT fornitore | upload del file su S3, stati `Draft/Parsing/Parsed/Confirmed/Cancelled`, `parse` che **accetta un payload OCR/IA già strutturato** (nessun motore OCR integrato), abbinamento articoli (`Matched`/`NewArticleNeeded`/`Error`), `SupplierArticleMapping` appreso alla conferma |
| Conferma carico | `SupplierDeliveryNoteService.ConfirmAsync` (Serializable): valida l'intero documento, incrementa la giacenza, imposta il costo unitario, registra `SupplierReceipt`, aggiorna l'ordine fornitore |
| Frontend | `acquisti/ordini-fornitore`, `acquisti/ddt-fornitore`, wizard di carico (ruoli `Admin`/`Warehouse`) |

### 2.8 Mappa API principale

`/api/v1/`: `orders` (incl. `fulfillment-queue`, `ship`, `reopen-adjustment`, `adjustment-lines`, `sync-invoice`), `cassa/sessions`, `invoices`, `ddts`, `inventory`, `products`, `contacts`, `warehouses`, `price-lists`, `settings/price-formula`, `settings/company`, `supplier-delivery-notes`, `geo`, `lookups`, `dashboard`. Eccezioni al prefisso `v1`: gli ordini fornitore sono solo su `/api/supplier-purchase-orders`; i DDT fornitore e le giacenze sono esposti sia su `/api/...` sia su `/api/v1/...`.

---

## 3. Stato del database e migrazioni EF Core

PostgreSQL 16, **43 tabelle** + `__EFMigrationsHistory`, 2 sequenze (`shipment_document_number_seq`, `supplier_purchase_order_number_seq`).

| Area | Tabelle |
|---|---|
| Anagrafiche | `contacts`, `contact_addresses`, `contact_persons`, `customer_profiles`, `supplier_profiles`, `agent_profiles`, `agent_commissions`, `countries` |
| Catalogo e prezzi | `products`, `product_variants`, `product_variant_images`, `categories`, `macro_categories`, `collections`, `colors`, `sizes`, `materials`, `material_compositions`, `price_lists`, `price_list_rules`, `price_formulas`, `price_formula_steps` |
| Magazzino | `warehouses`, `stock_items` (token `xmin`), `stock_movements` |
| Ordini e spedizioni | `orders`, `order_items`, `shipments`, `shipment_lines`, `ddt_documents`, `ddt_items` |
| Fatturazione e audit | `invoices`, `invoice_items`, `payments`, `credit_notes`, `document_audit_entries` |
| Acquisti | `supplier_purchase_orders`, `supplier_purchase_order_lines`, `supplier_delivery_notes`, `supplier_delivery_note_lines`, `supplier_article_mappings`, `supplier_return_drafts`, `supplier_return_draft_lines` |

### Migrazioni (14, in ordine)

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
| 13 | `20261030232428_AddStockItemXminConcurrencyToken` | Token di concorrenza `xmin` su `stock_items` (nessuna operazione DDL: colonna di sistema) — **non tracciata in git** |
| 14 | `20261031114600_AddPreSdiOrderAdjustment` | `invoices.sdi_status`, `orders.is_under_adjustment/adjustment_started_at/adjustment_started_by`, tabella `document_audit_entries` con indice — **non tracciata in git** |

Note operative: i database locali creati prima del baseline vanno ricreati o riallineati nella `__EFMigrationsHistory`. Le migrazioni si creano/applicano **solo** con `dotnet ef migrations add` / `dotnet ef database update`; non riscrivono importi o classificazioni fiscali storiche (le righe storiche nascono `NotTransmitted` e non in rettifica).

---

## 4. Stato della suite di test e delle verifiche

Esecuzione del 2026-10-31 (Docker disponibile per Testcontainers).

### 4.1 Backend (.NET)

`dotnet test src\Mauw.Erp.UnitTests\Mauw.Erp.UnitTests.csproj`

| Esito | Numero |
|---|---|
| Totale | 1246 |
| Superati | **1244** |
| Ignorati | 1 — `SupplierDocumentStorageTests.OriginalBytesAndContentTypeRoundTripThroughS3AndAreDeleted` (richiede `MAUW_TEST_S3_ENDPOINT/ACCESS_KEY/SECRET_KEY`) |
| Falliti | 1 — `OrderNumberPostgreSqlTests.ConcurrentStandardAndCassaOrderNumberAssignmentsAreUniqueAndSequential`: `NpgsqlException: The operation has timed out` durante `EnsureCreated` (connessione al container, 33 min). **Non riproducibile**: riesecuzione isolata 2/2 verdi. Da trattare come flakiness infrastrutturale (il run completo ha richiesto 43 min). |

Copertura per area (classi principali):

- **Contabilità** — `ItalianAccountingCalculatorTests`, `DiscountCompositionTests`, `DocumentAccountingServiceTests`, `CommercialDocumentAccountingTests`: scorpori (23,00×5; 94,18×5), due righe da 0,03 al 22%, quantità frazionarie, sconti composti, midpoint, overflow, immutabilità.
- **FatturaPA** — `FatturaPaInvoiceXmlBuilderTests` (20): validazione contro l'**XSD ufficiale**, fattura mista 22/10/0%, cliente tedesco (N3.2), svizzero (N3.1, CAP segnaposto), USA senza P.IVA, UK con CAP alfanumerico, **Grecia `EL`**, determinismo, errori espliciti; `FatturaPaXmlMapperTests`.
- **Flussi su PostgreSQL reale** — `OrderPreSdiAdjustmentPostgreSqlTests` (13: blocco con fattura `Pending`/`Transmitted`, audit, delta di stock, giacenza insufficiente, sync con stesso id/numero, cliente estero, rollback su errore XML, spedizione residua non riapribile), `DdtAndCassaStockMovementPostgreSqlTests` (14: DDT con PDF, **spedizione parziale con DDT sequenziali**, vendita cassa, rollback su errori di salvataggio/PDF, fido), `InvoiceGenerationPostgreSqlTests`, `OrderNumberPostgreSqlTests`, `InventoryStockQueryPostgreSqlTests`, `PriceListPostgreSql/PriceFormulaPostgreSql`, `SupplierDeliveryNote/PurchaseOrderPostgreSqlTests`, `OrderDraftItemRemovalPostgreSqlTests`.
- **Dominio** — `OrderAdjustmentTests` (15), `OrderLifecycleTests`, `OrderShippingChargeTests`, `StockMovementTests`, `PriceFormula*`, `PriceListResolverTests`.
- **Sicurezza** — `EndpointAuthorizationMatrixTests`, `CostVisibility*`, `EntraRoleClaimsTests`, `StockMovementRoleRestrictionTests`.
- **Policy UI** — `OrderPermittedActionsPolicyTests`.
- **Migrazioni** — `InitialCreateBaselineMigrationTests`, `AddVariantForeignKeysMigrationTests`.
- **Generatore dati** — `DataGenerator/SimulationTests`, `GeneratorSafetyTests` (solo database `*_loadtest`, conferme esplicite, credenziali dedicate); `README.txt` aggiornato fino a `AddPreSdiOrderAdjustment`.
- **PDF** — `InvoicePdfGeneratorTests`, `InventoryStockPdfGeneratorTests`.

### 4.2 Frontend (Angular)

`ng test --watch=false --browsers=ChromeHeadless`: **260 specifiche, 257 verdi, 3 fallite** (tutte **preesistenti e non correlate** al lavoro pre-SDI: i file non risultano modificati):

| Spec | Causa |
|---|---|
| `listini.component.spec.ts` — «creates a global rule without inventing an ID or a target» | La spec usa `minimumQuantity: 0.00000001`, ma il componente impone quantità minima intera ≥ 1 (coerente col vincolo DB `whole_minimum_quantity`): spec obsoleta |
| `app.component.spec.ts` — «should have the 'web' title» | Spec scaffold di default (si aspetta `web`, il titolo è `Mauw ERP`) |
| `app.component.spec.ts` — «should render title» | Spec scaffold di default (si aspetta `Hello, web`) |

**Build**: `ng build` (produzione) completata con successo; unico warning: **bundle iniziale 981,08 kB oltre il budget di 819,20 kB (+161,88 kB)**. Componenti caricati in lazy: acquisti, inventario, dashboard, formula prezzi.

---

## 5. Roadmap e prossimi passi

Priorità proposta, in ordine.

| P | Attività | Stato / motivazione |
|---|---|---|
| **P0** | **Consolidare il working tree** | 41 file modificati e 22 non tracciati (incluse 2 migrazioni) non sono committati: committare, poi valutare la PR. |
| **P0** | **Connettore trasmissione SDI** | Il dominio ha `MarkSdiPending/Transmitted/Accepted`, ma **nessun chiamante**: tutte le fatture restano `NotTransmitted` e rettificabili. Servono: provider/canale di invio, endpoint o servizio di transizione, ricezione notifiche (ricevuta di consegna, scarto, mancata consegna), **persistenza del file XML effettivamente inviato**, e blocco definitivo della rettifica dopo `Pending`/`Transmitted`. |
| **P1** | **Automatismi fiscali anagrafica estera nel form ordini** | Oggi `ordini-nuovo`, `banco-cassa` e `order-detail` offrono tutta la lista di Nature **a scelta manuale**; l'incoerenza con il paese del cliente (N3.2 intra-UE / N3.1 extra-UE) emerge solo alla generazione dell'XML. Proporre/imporre la Natura in base al paese del cliente senza mai inventarla per documenti storici. |
| **P1** | **XML per note di credito** | `GetInvoiceXmlAsync` gestisce solo fatture TD01; le note di credito hanno il PDF ma non l'XML (TD04). Servono righe e classificazioni esplicite (niente aliquota media su note multi-aliquota). |
| **P1** | **Stabilizzare i test** | Correggere le 3 spec Angular obsolete; rendere robusti i test Testcontainers (timeout di connessione/`EnsureCreated`, durata della suite 43 min); rieseguire la suite completa. |
| **P2** | **Estendere il modulo acquisti (già presente)** | Integrare un motore OCR/IA reale (oggi `parse` riceve un payload già strutturato); gestire le fatture passive; eseguire il test di round-trip S3 in CI (oggi saltato senza variabili d'ambiente). Estendere `SupplierPurchaseOrderPostgreSqlTests` (formato, prefisso, progressione, unicità sotto concorrenza). |
| **P2** | **Hardening operativo** | Rendere `Program.cs` minimale (HttpClient, Swagger, CORS e migrazione all'avvio sono ancora inline); sostituire il CORS `AllowAll`; valutare un indice univoco su `invoices.order_id` (oggi solo lock + controllo applicativo); verificare la numerazione ordini `count + 1` in presenza di eliminazioni di bozze; ridurre il bundle iniziale Angular. |
| **P3** | **Punti aperti dalle decisioni sui prezzi** | Sconto sul totale in fattura, accesso del magazziniere al documento di spedizione, ruolo `Accounting`, prezzi errati già fatturati (`docs/PRICE_CALCULATOR_DECISIONS.md`); rimozione del ruolo `Cashier` in Entra prima del rilascio. |
| **P3** | **Regimi speciali** | Bollo, ritenute, casse previdenziali, split payment, esigibilità IVA: non implementati per scelta di perimetro. Un esportatore XML definitivo dovrà validare anche i controlli SDI aggiornati (i test aritmetici da soli non certificano la conformità). |

---

## 6. Avvertenze e guida alla ripresa

- **Working tree sporco**: la rettifica pre-SDI, l'`XML builder`, l'audit, `SdiStatus` e le migrazioni 13–14 esistono solo localmente. Verificare `git status` prima di creare nuove migrazioni.
- **Regole non negoziabili** per qualunque modifica contabile: usare solo `ItalianAccountingCalculator`; `decimal` + `AwayFromZero`; IVA per gruppo `(aliquota, Natura)`; importi 2 decimali, prezzi unitari 8; valutare l'intero documento prima di mutare righe/totali; nessuna Natura di default su documenti storici.
- **Invarianti da proteggere con test** quando si tocca il codice: formato e sequenza `OF-…` degli ordini fornitore; riconciliazione ristretta alle coppie `(WarehouseId, VariantId)`; propagazione della cancellazione e fallback di geocoding solo per guasti attesi (`AddressAndVatProviderTests`); un solo percorso canonico di validazione per request.
- **UI**: tabelle sempre paginate (`<app-pagination>`), azioni con `TableActionButtonComponent`, pulsanti Nuovo con `AddActionButtonComponent`, eliminazioni sempre con conferma, ritorni con `BackButtonComponent` (`<button>`, mai link).
- **Strumenti**: migrazioni con `dotnet ef migrations add` / `dotnet ef database update`; generatore di volume solo su database `*_loadtest` e da aggiornare (codice, test, `README.txt`) a ogni modifica di entità o flussi documentali.
- **Comandi di verifica**: backend `dotnet test src\Mauw.Erp.UnitTests\Mauw.Erp.UnitTests.csproj` (richiede Docker); frontend da `src\Mauw.Erp.Web`: `npx ng test --watch=false --browsers=ChromeHeadless` e `npx ng build`.
