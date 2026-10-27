# Regole permanenti di sviluppo

Rispondere brevemente; fornire dettagli estesi solo se richiesti.
Preferire dependency injection, configurazioni DI in metodi di estensione e
`Program.cs` minimale. Per errori di validazione usare il Result pattern.

## Invarianti per refactoring e validazione

- Per gli ordini fornitore mantenere il formato `OF-{yyyyMMdd}-{sequenza:D6}`
  e la progressione concorrente basata su `supplier_purchase_order_number_seq`;
  non sostituirla con un conteggio delle righe. Estendere
  `SupplierPurchaseOrderPostgreSqlTests` per verificare formato, prefisso,
  progressione e unicita sotto concorrenza.
- Le query di riconciliazione devono restringere i dati alle coppie effettive
  `(WarehouseId, VariantId)`. Ogni ottimizzazione deve conservare risultati,
  ordinamento e stati; estendere
  `InventoryStockQueryPostgreSqlTests.GetAmbiguousMovementsAsync_ProposesOnlySingleExactDeltaAndDoesNotModifyHistory`
  per coprire coppie di magazzino-variante incrociate.
- Propagare la cancellazione richiesta dal chiamante. Il fallback geocoding e
  ammesso solo per guasti attesi del provider, non per cancellazione o errori
  estranei. Estendere `AddressAndVatProviderTests` con casi di cancellazione e
  di errore non recuperabile, oltre ai casi di fallback esistenti.
- Ogni request deve avere un solo percorso canonico di validazione: coprire con
  test le regole effettivamente applicate a runtime e non duplicarle tra
  endpoint, validator e servizio. Aggiornare i test del validator/servizio
  interessato.
- Ottimizzazioni e refactoring devono preservare output, ordinamento e
  comportamento business. Aggiungere test di regressione nel test pertinente
  prima di cambiare il percorso di calcolo o di query.

## Contabilita italiana: invarianti obbligatori

Queste regole si applicano a fatture, preventivi, ordini, DDT e relativi
refactoring. Il riferimento unico per il calcolo e
`Mauw.Erp.Domain.Services.ItalianAccountingCalculator`, registrato tramite DI.
Non duplicare formule in servizi applicativi, entita, mapper, PDF o frontend.
Il calcolatore deve restare puro, deterministico e senza I/O; input e risultati
fiscali sono record immutabili. Gli errori restituiscono `AccountingResult<T>`:
non usare eccezioni per input invalidi, non ignorare errori o inventare valori.

### Precisione e arrotondamento

- Utilizzare esclusivamente `decimal` e letterali `m` per importi, prezzi,
  quantita fiscali, percentuali e aliquote: mai `double`, `float` o conversioni
  attraverso tipi binari floating-point.
- Ogni arrotondamento monetario deve specificare
  `MidpointRounding.AwayFromZero`, inclusi importi negativi.
- Prezzi unitari netti: da 2 a 8 decimali, senza arrotondamento preventivo al
  centesimo. Conservare la precisione nel database, nelle API e nel PDF.
- Aliquote IVA: da 0 a 100, al massimo 2 decimali. Sconti: da 0 a 100.
  Quantita fiscali `decimal`, anche frazionarie; zero non valido. Le quantita
  intere delle entita di magazzino non limitano il calcolatore condiviso.
- Importi di riga e totali monetari del progetto hanno esattamente 2 decimali,
  anche quando nulli (zero monetario: `0.00m`).

### DettaglioLinee: 2.2.1

Il prezzo unitario e al netto dell'IVA e prima dello sconto. Applicare lo sconto
percentuale una sola volta, senza arrotondare il prezzo unitario scontato:

```csharp
decimal prezzoTotale = Math.Round(
    quantita * prezzoUnitario * (1m - scontoPercentuale / 100m),
    2, MidpointRounding.AwayFromZero);
```

`PrezzoUnitario` e il campo **2.2.1.9**; `PrezzoTotale` e **2.2.1.11**.
Verificare la coerenza aritmetica del controllo SDI **00423**.
Sulla singola riga NON calcolare, arrotondare o sommare IVA o lordo.
I campi legacy nullable `LineVatTotal` e `LineGrossTotal` restano solo per
compatibilita: le nuove righe li lasciano null e non sono fonti contabili.

Per prezzi iniziali ivati usare:

```csharp
decimal prezzoUnitarioNetto = Math.Round(
    prezzoLordo / (1m + aliquotaIva / 100m),
    8, MidpointRounding.AwayFromZero);
```

Conservare almeno 4 decimali dello scorporo quando necessari, fino a 8;
non ricalcolare il netto da un prezzo di visualizzazione a 2 decimali.
Trasmettere il lordo originale al backend per lo scorporo `decimal`.

### Sconti di riga composti

Una riga puo avere piu sconti, ciascuno con la propria causale obbligatoria
(listino, cassa). La composizione e solo in
`ItalianAccountingCalculator.CombineDiscounts`: le percentuali si compongono
sul residuo, un importo in euro (2 decimali, sul totale di riga) si sottrae dal
residuo e non puo superarlo. Il risultato e UNA percentuale effettiva, arrotondata
una sola volta a 10 decimali con `MidpointRounding.AwayFromZero`, che entra nella
formula `prezzoTotale` qui sopra: lo sconto resta applicato una sola volta e il
totale di riga resta a 2 decimali. Non applicare sconti in sequenza su importi
arrotondati e non ricalcolarli in servizi, mapper, PDF o frontend. L'elenco degli
sconti con causale e salvato nello snapshot della riga; gli importi risparmiati
per sconto servono solo alla presentazione. Alla cassa il prezzo non e
modificabile: si puo solo aggiungere uno sconto, sempre motivato.

### DatiRiepilogo: 2.2.2

Raggruppare SEMPRE per chiave composta `(AliquotaIVA, CodiceNatura)`,
mai per sola aliquota e mai derivando l'aliquota da imposte arrotondate.

Per ciascun gruppo:

```csharp
decimal imponibileImporto = righeDelGruppo.Sum(riga => riga.LineNetTotal);
decimal imposta = aliquotaIva == 0m
    ? 0.00m
    : Math.Round(imponibileImporto * aliquotaIva / 100m,
        2, MidpointRounding.AwayFromZero);
```

- `ImponibileImporto` (**2.2.2.5**, controllo **00422**) e la somma esatta
  dei totali di riga gia arrotondati: non sommare prodotti non arrotondati.
- `Imposta` (**2.2.2.6**, controllo **00421**) si calcola UNA volta
  sull'imponibile del gruppo, mai come somma di IVA unitaria o di riga.
- Con aliquota zero `Natura` (**2.2.2.2**) e obbligatoria.
  Codici ammessi: N1, N2.1, N2.2, N3.1-N3.6, N4, N5, N6.1-N6.9, N7.
  Non usare codici generici obsoleti N2, N3 o N6.
- Con aliquota positiva Natura deve essere null.
- Non attribuire Nature predefinite a documenti storici non classificati:
  restituire un errore esplicito e richiedere la corretta classificazione.

### Totali, persistenza e presentazione

`TotaleImponibile = somma degli imponibili dei gruppi`.
`TotaleImposta = somma delle imposte dei gruppi`.
`TotaleDocumento = TotaleImponibile + TotaleImposta`.
Le spese accessorie devono essere righe esplicite con aliquota/Natura e
partecipare al castelletto; non aggiungerle ciecamente dopo il calcolo IVA.
Questa politica non implementa automaticamente bollo, ritenute, casse,
split payment, esigibilita IVA o altri regimi speciali.

Il campo `ImportoTotaleDocumento` e **2.1.1.9**, NON 2.4.2.2.
Le API, i totali persistiti e i PDF devono usare il medesimo risultato fiscale.
Il frontend puo mostrare stime, chiaramente indicate, ma non costituisce
fonte contabile. UI e PDF mostrano il prezzo unitario arrotondato ai centesimi
(`N2`, `MidpointRounding.AwayFromZero`) solo per presentazione; la precisione
a 8 decimali resta in database, API e calcoli. Il PDF stampa soltanto i valori
gia calcolati (nessuno scorporo). La cassa invia sempre prezzo netto, sconto,
aliquota e Natura, mai il lordo; imponibile, IVA e totali mostrati in cassa
arrivano dall'endpoint `POST /api/v1/cassa/sessions/preview`, che usa il
calcolatore: il frontend non ricalcola nulla. Il PDF mostra inoltre importi a
2 decimali, sconto, aliquota e Natura.
Non ricavare un'aliquota media da una nota di credito multialiquota:
occorrono righe e classificazioni esplicite.

Mantenere i prezzi unitari persistiti a 8 decimali e gli importi a 2.
Validare l'intero documento prima di modificare o salvare righe e totali.
Le migrazioni non devono riscrivere automaticamente importi di documenti
storici o classificazioni fiscali; serve una revisione esplicita.
Usare gli strumenti .NET deterministici per migrazioni:
`dotnet ef migrations add` e `dotnet ef database update`.

### Generatore dati di volume

Quando cambiano entita, vincoli, contabilita o flussi documentali, adeguare anche
`src/Mauw.Erp.DataGenerator`, i suoi test e `README.txt`. Il generatore rimane un
eseguibile opzionale: nessun riferimento dai progetti applicativi, nessun hook
di avvio e nessun seed automatico nell'API. Usare esclusivamente un database
dedicato `_loadtest`, credenziali con privilegi limitati e conferme esplicite
prima di scritture o reset; non ereditare la connessione dell'applicazione.

### Test obbligatori per modifiche contabili

Usare xUnit con valori `decimal`, mai dati monetari `double` in `InlineData`.
Preservare e ampliare i test per:

- fattura standard al 22% e mista 22% / 10% / 0% con N3.2;
- gruppi con aliquota zero e Nature diverse;
- scorporo 23,00 x 5: imponibile 94,26, IVA 20,74, totale 115,00;
- scorporo 94,18 x 5: imponibile 385,98, IVA 84,92, totale 470,90;
- due righe nette da 0,03 al 22%: imponibile 0,06 e IVA 0,01, NON 0,02;
- quantita frazionarie, sconti, midpoint positivi e negativi;
- sconti composti: percentuali in sequenza (5%, 3%, 5% su 100,00 = 12,4575%,
  totale 87,54), importo in euro dopo una percentuale, importo oltre il residuo,
  causale mancante, percentuale effettiva a 10 decimali;
- precisione, input invalidi, Natura mancante/incompatibile e overflow;
- immutabilita, determinismo, nessuna mutazione dopo validazione fallita;
- coerenza tra aggregati, persistenza e presentazione PDF.

### Riferimenti ufficiali e limiti

- [Documentazione formato FatturaPA](https://www.fatturapa.gov.it/it/norme-e-regole/documentazione-fattura-elettronica/formato-fatturapa/)
- [Specifiche tecniche formato, versione 1.4](https://www.fatturapa.gov.it/export/documenti/fatturapa/v1.4/Specifiche_tecniche_del_formato_FatturaPA_V1.4.pdf)
- [Schema ordinario privati 1.2.3](https://www.fatturapa.gov.it/export/documenti/fatturapa/v1.4/Schema_VFPR12_v1.2.3.xsd)

La scelta di `PrezzoTotale` a esattamente 2 decimali e una politica tassativa
del progetto: non descriverla come unico formato consentito dallo schema,
che permette maggiore precisione per alcuni campi prezzo. I test aritmetici
non certificano da soli la conformita completa SDI: un futuro esportatore XML
deve anche validare XSD, vincoli documentali e controlli SDI aggiornati.
