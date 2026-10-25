# Formula prezzi (Price Calculator): analisi e decisioni

Documento di lavoro. Si aggiorna man mano che le decisioni vengono prese.
Stato: **funzionalità implementata** (analisi, decisioni e codice); restano i punti aperti elencati in "Stato implementazione".

## Stato implementazione

Implementato:

- Pagina **Formula prezzi** in Impostazioni > Formula prezzi (solo `Admin`): passi percentuali a cascata, passo di arrotondamento sempre per eccesso, simulatore dal costo.
- Endpoint: `GET` e `PUT /api/v1/settings/price-formula` (lettura e salvataggio della formula), `POST /api/v1/products/price-formula/calculate` (calcolo dal costo, senza salvare). Tutti con policy `Admin`.
- Pulsante "Calcola da costo" nelle schermate di prodotto e variante (solo `Admin`): propone il prezzo, non lo salva da solo.
- Ruoli lato server: policy per gruppo di endpoint (`Admin`, `Sales`, `Warehouse`, `Accounting`), con test 401/403 nella matrice di autorizzazione. `Cashier` non ha più accesso.
- Redazione dei costi: costi e valorizzazioni solo ad `Admin` e `Accounting`; margine e passi della formula solo ad `Admin` (endpoint con policy `Admin`).
- Movimenti di magazzino: `Sales` può registrare solo vendite showroom (quantità negativa) e resi cliente (quantità positiva); gli altri motivi richiedono `Admin` o `Warehouse`.
- Rimozione del legacy (`DefaultMarkupRate`, `MarkupRate`, `PricingEngine`, `calculate-selling-price`).
- Rimossa la regola di listino `CostMarkup` (codice, API, interfaccia e test): i listini mantengono `FixedPrice` e `PercentageDiscount`. Nessuna migrazione elimina le righe esistenti (punto G).
- Migrazioni `AddPriceFormula` e `RemoveLegacyMarkupRates`: poi riunite nella baseline `InitialCreate` (riga successiva).
- Le 42 migrazioni EF sono state riunite in un'unica baseline `InitialCreate` (schema identico, dati seed equivalenti). I database locali vanno ricreati (`dotnet ef database update` su un database vuoto) oppure va allineata la loro `__EFMigrationsHistory` (riga con l'id di `InitialCreate`). Le migrazioni sono ora due: `InitialCreate` e `AddVariantForeignKeys`, che aggiunge le FK su `variant_id` di `ddt_items`, `invoice_items`, `order_items` (Restrict) e `stock_movements` (Cascade), mancanti nello schema della baseline pur dichiarate dal modello.

Ancora aperto:

- Sconto sul totale in fattura (punto F).
- Accesso del magazziniere al documento di spedizione (punto H).
- Ruolo `Accounting` (punto I).
- Prezzi errati già fatturati (punto D).
- Passo manuale in Entra, **prima del rilascio**: rimuovere il ruolo dell'app `Cashier` (Cassiere Showroom) e riassegnare i cassieri a `Sales` (Commerciale).
- Applicare le migrazioni con `dotnet ef database update`.
- Configurare la formula nell'interfaccia prima del primo utilizzo: senza formula il calcolo risponde "Formula prezzi non configurata".
- Per decisione, `Warehouse` può ancora leggere il prezzo delle righe del DDT fornitore e degli ordini di acquisto.
- Ruoli dell'app mobile per le immagini non verificati: `ImagesController` e `CatalogController` richiedono ora un ruolo (lettura: qualunque ruolo; caricamento, categoria ed eliminazione: `Admin` e `Warehouse`).

## Obiettivo

Il prezzo di vendita è un valore **fisso salvato** su prodotto e variante (`SellingPrice`).
Un modulo `PriceCalculator` lo propone partendo dal costo di acquisto, con una catena di
ricarichi configurabili da database e da UI, e un arrotondamento finale. L'amministratore
può sempre scrivere il prezzo a mano, senza alcun calcolo.

Il calcolo avviene **solo su richiesta** (mai in fase di vendita). Il documento emesso
conserva il proprio prezzo.

Nome in interfaccia: **Formula prezzi** (Impostazioni > Formula prezzi).
Nome nel codice: `PriceFormula` e `PriceFormulaStep`.

## Stato attuale (sintesi)

- `Product.SellingPrice` e `ProductVariant.SellingPrice`: `numeric(22,8)`, già fissi.
- Rimossi (migrazione `RemoveLegacyMarkupRates`): `CatalogPriceCalculator.FromMarkup`,
  `POST /api/v1/products/calculate-selling-price`, `Product.DefaultMarkupRate`,
  `ProductVariant.MarkupRate` e `PricingEngine` (con la sua iniezione in `InventoryService`).
- `PurchaseCost` della variante è `numeric(18,4)` e cambia solo a mano.
- Non esistono provvigioni agenzia né sconti fornitore nel modello.
- Le API `products` e `settings/company` non hanno restrizioni di ruolo lato server.
- I ruoli arrivano dai claim di Entra e il codice li legge senza trasformarli. Non c'è una tabella utenti/ruoli nel database.

## Decisioni prese

| # | Tema | Decisione |
|---|------|-----------|
| 1 | Base dei prezzi | Tutti i prezzi e i movimenti interni sono **IVA esclusa** (imponibile): costo di acquisto, prezzo di vendita, valorizzazione di magazzino. Il listino fornitore è già netto. Il gestionale non è un programma di contabilità completa. |
| 1 | Quando si applica l'IVA | Solo in vendita, in fattura, in base al cliente. Italia: IVA applicata. Cessioni intracomunitarie ed estero: non imponibili (art. 41 e simili), con Natura e classificazione esplicita. |
| 2 | Ricarichi | A cascata: ogni percentuale si applica al risultato del passo precedente. Es. 40% poi 10% dà costo × 1,40 × 1,10. |
| 3 | Provvigione | È un passo della catena, applicato sempre. Non dipende dalla presenza di un agente: genera margine extra o compensa la provvigione che si pagherà all'agente. Non serve un legame con un agente. |
| 4 | Sconti fornitore | Non esistono: il prezzo di acquisto è complessivo. Nessun passo "sconto fornitore" nel calcolo. |
| 5 | Costo di partenza | `PurchaseCost` del prodotto o della variante. Non cambia nel tempo: un riassortimento con costo diverso è un articolo diverso. Il calcolo non usa i movimenti di magazzino. |
| 6 | Ricalcolo | Nessun ricalcolo automatico al cambio costo. Il calcolo è solo su richiesta. Un prezzo cambia solo per correggere un errore di inserimento. |
| 7 | Sicurezza | Protezione per ruolo **lato API** obbligatoria, anche sugli endpoint esistenti. Le nuove API di configurazione e calcolo richiedono `Admin`. Vedi matrice dei ruoli. |
| 8 | Legacy | Si rimuove ciò che non è usato. |
| A | Formula unica | **Una sola formula per tutto il catalogo** (nessuna formula per categoria o fornitore, per ora). Si chiama "Formula prezzi". La struttura dati resta pronta a crescere verso formule per categoria. |
| B | Arrotondamento | **Sempre per eccesso**, a passi di 0,10 € (dieci centesimi), sul netto, **solo nella generazione del prezzo**. Esempio: 45,5789 → 45,60; 45,60 resta 45,60. Mai in fattura né nel calcolo dell'IVA. |
| C | Precisione del costo | Il listino fornitore ha sempre 2 decimali: i costi di acquisto si inseriscono con 2 decimali (la UI lo impone in inserimento). Il calcolatore tollera fino a 4 decimali, la scala memorizzata di `PurchaseCost` (`numeric(18,4)`). Nessuna migrazione. |
| D | Prezzi errati già fatturati | Rinviato: se ne riparla. |
| E | Unificazione ruoli | `Cashier` e `Sales` sono lo stesso ruolo. Si **tiene `Sales`** e si elimina `Cashier`. `Sales` è il più radicato (preventivi, anagrafiche, listini, ordini, menu). Ruoli finali: `Admin`, `Sales`, `Warehouse`, `Accounting`. |
| E | Magazziniere e prezzi dei DDT | Se un DDT fornitore riporta il prezzo, il magazziniere lo vede. Va bene così per ora, da rivalutare. |
| E | Magazziniere e spedizioni | Spedendo la merce vede la fattura o il DDT allegato (sola lettura). Non vede costi né valorizzazioni. Non gli serve la giacenza storica, per ora. |
| E | Commercialista | Rinviato: per ora non ci si preoccupa del ruolo. Idea di partenza: sola lettura, con il costo della merce invenduta per il bilancio. |

Conseguenze delle decisioni:

- Il calcolatore lavora solo su importi netti. Non serve passare l'aliquota IVA né uno scorporo.
- In cassa, l'importo ivato è derivato dal netto in fase di vendita, dal calcolatore contabile condiviso.
  Poiché si arrotonda il netto, il lordo mostrato non sarà un valore tondo. Questo è accettato.
- L'aliquota IVA oggi vive su `Product.VatRate`, mentre la decisione dice che dipende dal cliente.
  Da verificare come il codice gestisce oggi l'aliquota per cliente: tema separato, fuori dal calcolo prezzi.
- Il prezzo generato ha al massimo 1 decimale. I prezzi inseriti a mano restano liberi (fino a 8 decimali, come oggi).
- Arrotondare il totale di una fattura non fa parte del calcolo: se serve, l'operatore inserisce a mano uno sconto
  sul totale. Il sistema può proporlo, ma non lo applica da solo. Vedi punto aperto F.

### Dettaglio sulla decisione 8 (rimozione eseguita)

Rimossi, con la migrazione `RemoveLegacyMarkupRates` (generata con `dotnet ef migrations add`):

- `Product.DefaultMarkupRate` e `ProductVariant.MarkupRate` (colonne e codice).
- `PricingEngine` e la sua iniezione in `InventoryService`.
- `POST /api/v1/products/calculate-selling-price` e `CatalogPriceCalculator.FromMarkup`, sostituiti dal nuovo modulo.

La migrazione elimina solo le due colonne e non riscrive importi o classificazioni di documenti storici.
Il `Down` le ricrea (`default_markup_rate` numeric(6,2) NOT NULL default 0, `markup_rate` numeric(6,2) NULL),
ma i valori eliminati non sono recuperabili.

La regola di listino `CostMarkup` è stata rimossa separatamente (vedi punto G).

## Matrice dei ruoli (bozza)

Oggi la restrizione per ruolo esiste quasi solo nel frontend (guardie delle rotte). Le API sono in gran parte
aperte a qualunque utente autenticato. L'obiettivo è che ogni gruppo di endpoint verifichi il ruolo lato server.

| Ruolo | Accesso previsto |
|-------|------------------|
| `Admin` | Tutto: formula prezzi, catalogo con costi, giacenza storica, riconciliazione, impostazioni. |
| `Sales` (commessa) | Cassa, fatture, note di credito, ordini, giacenza **attuale** (solo quantità), più quello che ha già oggi (preventivi, anagrafiche, listini). Vede solo il prezzo finito; l'IVA si aggiunge se dovuta. **Non vede** costo di acquisto, margine né calcoli della formula. |
| `Warehouse` (magazziniere) | Riceve e spedisce la merce: carichi, magazzino, movimenti, giacenze. Vede **solo le quantità**, senza costo unitario né valore. Vede in sola lettura la fattura o il DDT allegato alla spedizione. Vede il prezzo delle righe di un DDT fornitore, se presente. |
| `Accounting` (commercialista) | Rinviato. Mantiene l'accesso di oggi. In prospettiva: sola lettura, costo della merce invenduta per il bilancio. |

### Dati sensibili per ruolo (bozza)

| Dato | Admin | Accounting | Sales | Warehouse |
|------|-------|------------|-------|-----------|
| Costo di acquisto nell'anagrafica prodotto o variante | sì | sì (lettura) | no | no |
| Prezzo delle righe di un DDT fornitore | sì | sì (lettura) | no | sì, per ora |
| Margine e passi della formula | sì | rinviato | no | no |
| Valorizzazione di magazzino (costo unitario, valore totale) | sì | sì (lettura, per il bilancio) | no | no |
| Quantità attuali | sì | sì | sì | sì |
| Giacenza storica (alla data) | sì | sì (proposta, serve al bilancio) | no | no, per ora |
| Fattura o DDT collegato a una spedizione | sì | sì | sì (fatture) | sì, sola lettura |

Impatti previsti rispetto a oggi:

- **Eliminare `Cashier`.** I ruoli arrivano dai claim di Entra: va tolto il ruolo dell'app in Entra e vanno riassegnate
  le commesse a `Sales` **prima del rilascio**, altrimenti perdono l'accesso alla cassa. Nel codice cambiano
  il tipo `AppRole` in `auth.model.ts`, la rotta `/vendite/cassa` (oggi solo `Admin` e `Cashier`), il menu e i controlli lato API.
  Un'analisi precedente indica che le API dei listini ammettono anche `Cashier`: da verificare in fase di implementazione.
- Il filtro dei dati sensibili va fatto **lato server**, nel mapping delle risposte, non nascondendo i campi nella UI.
  Oggi le risposte di prodotti, varianti, giacenze, listini e documenti contengono costi e valorizzazioni.
- Le fatture e le note di credito oggi sono visibili solo ad `Admin` e `Accounting` nel frontend: per `Sales` vanno aperte.
  Il magazziniere deve poter leggere solo il documento collegato alla spedizione, non l'elenco delle fatture.
- I listini sono visibili a `Sales`. La regola `CostMarkup`, che mostrava il margine, è stata rimossa (punto G):
  i listini non contengono più dati di margine.
- La giacenza storica (parametro data) e la riconciliazione dei movimenti ambigui vanno limitate ai ruoli previsti.
- Serve una policy per ruolo applicata a tutti i gruppi di endpoint, con test che verificano 401 e 403
  e test che verificano l'assenza dei campi sensibili nelle risposte dei ruoli senza permesso.

## Punti aperti

### F. Sconto sul totale per arrotondare in fattura

Funzione separata dal calcolo prezzi. L'operatore decide e inserisce lo sconto a mano, il sistema può solo
proporlo. Da progettare: come si rappresenta uno sconto sul totale nel documento, perché le regole contabili
richiedono righe esplicite con aliquota e Natura e un castelletto IVA coerente.

### G. Regola di listino `CostMarkup` (rimossa)

Rimossi il tipo `PriceCalculationType.CostMarkup`, il ramo del resolver (e `PriceResolutionInput.BaseCost`), la redazione e il
blocco dei valori nascosti nei listini, l'opzione "Ricarico sul costo %" e i test relativi. La "Formula prezzi" la sostituisce.

Nessuna migrazione elimina le righe esistenti (le migrazioni potrebbero essere riunite in una sola). Il tipo è salvato come stringa:
in un database di sviluppo con righe `CostMarkup` la lettura dei listini fallisce. Va ricreato il database oppure eseguito
`DELETE FROM price_list_rules WHERE calculation_type = 'CostMarkup';`.

### H. Accesso del magazziniere al documento di spedizione

Da progettare: come vede la fattura o il DDT allegato. Proposta: dalla schermata della spedizione, solo il documento
collegato, in sola lettura, senza accesso all'elenco delle fatture.

### I. Ruolo `Accounting`

Rinviato. Da definire in seguito quali dati servono al commercialista (inventario valorizzato per il bilancio,
documenti, anagrafiche), e se vede margine e formula.

### D. Prezzi errati già fatturati

Rinviato.

## Punti chiusi

- Netto o lordo: tutto netto, arrotondamento sul netto.
- Sconti fornitore: non ci sono.
- Modalità di arrotondamento: sempre per eccesso, passo 0,10 €.
- Precisione del costo di acquisto: inserimento a 2 decimali, il calcolatore accetta fino a 4 (scala memorizzata), nessuna migrazione.
- Quante formule: una sola, "Formula prezzi".
- `Cashier` e `Sales`: unificati in `Sales`.
- Magazziniere: vede solo quantità, più prezzi dei DDT fornitore se presenti, più il documento allegato alla spedizione.
- Protezione per ruolo: decisa, anche sulle API esistenti, con filtro dei dati sensibili lato server.

## Prossimi passi

1. Verificare G e decidere su F.
2. Preparare il piano a task (dominio, database, API, UI, ruoli) e i test obbligatori per le modifiche contabili.
