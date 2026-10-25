Mauw.Erp.DataGenerator — dati sintetici per prove di volume
==========================================================

Scopo e isolamento
------------------
Eseguibile console .NET 10 opzionale, non un servizio e non un benchmark.
Usa Bogus (locale it), il modello EF e ItalianAccountingCalculator condivisi.
Nessun progetto di produzione dipende dal generatore; nessun seed viene
aggiunto all'avvio dell'API, al deploy o alle migrazioni.
Non invia email, non chiama API esterne, non usa dati personali reali.
I dati non rappresentano documenti fiscali da trasmettere allo SDI.

Due database sono consigliati:
  mauw_erp          sviluppo quotidiano, pochi dati;
  mauw_erp_loadtest prove di volume, eliminabile e ripopolabile.
Preferire anche un'istanza PostgreSQL separata per misure attendibili:
il popolamento consuma comunque CPU, memoria e disco del server.
Usare un ruolo PostgreSQL dedicato, proprietario SOLO del database loadtest,
senza privilegi sui database applicativi. Il reset richiede di poterlo eliminare;
la creazione tramite EF richiede CREATEDB o un database vuoto creato dal DBA.
Il ruolo deve poter connettersi anche al database di manutenzione postgres:
da quella connessione mantiene l'advisory lock ed esegue DROP DATABASE sul
solo database loadtest di cui e proprietario, senza modificare tabelle o dati
del database di manutenzione.
Un suffisso non sostituisce l'isolamento dei permessi.
Non collegare questo strumento a produzione, neppure rinominandone il database.

Il generatore legge SOLO MAUW_LOADTEST_CONNECTION, mai DefaultConnection,
appsettings dell'API o .env. Richiede host esplicito, nome terminante in
_loadtest e --confirm-database con il nome esatto. Non stampa credenziali.
seed rifiuta database con anagrafiche/documenti/movimenti gia presenti,
anche se sono stati inseriti da una generazione interrotta. Non e un upsert.
Un advisory lock PostgreSQL nel database di manutenzione coordina seed e reset
per il medesimo database, anche durante l'eliminazione del database stesso.
Fermare API e altri generatori prima del reset.

Preparazione (Bash; dalla radice del repository)
----------------------------------------------
Richiesti SDK .NET 10, PostgreSQL e strumento dotnet-ef compatibile con la
versione EF di Directory.Packages.props. Non sono richieste nuove migrazioni.
Le migrazioni storiche sono riunite in un'unica baseline (InitialCreate), che
crea da zero l'intero schema: il ciclo reset -> database update -> seed e
percorribile su un database vuoto. Un database creato con le vecchie migrazioni
va ricreato oppure la sua tabella __EFMigrationsHistory va allineata (una sola
riga con l'id di InitialCreate). Il generatore non usa EnsureCreated e non
corregge migrazioni applicative.
Il generatore salva anche il delta firmato di ogni movimento e verifica
questa coerenza prima di dichiarare completato il seed. Se il database contiene
dati sintetici creati prima dell'introduzione di quantity_delta, database update
aggiunge la colonna nullable ma non puo ricostruire in sicurezza tutte le
rettifiche storiche: per riavere uno scenario coerente usare reset -> database
update -> seed, non migrare in-place il vecchio dataset.

Non conservare password nel repository o nella cronologia della shell.
Impostare MAUW_LOADTEST_CONNECTION nel proprio secret manager/ambiente:
  Host=localhost;Port=5432;Database=mauw_erp_loadtest;Username=<ruolo dedicato>;******

Per gli esempi, ROOT e il percorso assoluto del clone:
  ROOT=/home/runner/work/mauw-erp/mauw-erp

1. Verificare il piano senza aprire connessioni:
  dotnet run --project "$ROOT/src/Mauw.Erp.DataGenerator" -- plan

2. Creare/aggiornare lo schema SOLO sul database dedicato:
  ConnectionStrings__DefaultConnection="$MAUW_LOADTEST_CONNECTION" \
    dotnet ef database update \
      --project "$ROOT/src/Mauw.Erp.Infrastructure" \
      --startup-project "$ROOT/src/Mauw.Erp.Api"

   Il comando EF costruisce il modello, non avvia il server API.
   Non avviare l'API prima del primo seed: il suo inizializzatore potrebbe
   inserire dati dimostrativi e il generatore rifiuterebbe il database.

3. Popolare (la scala predefinita e grande: valutare prima lo spazio libero):
  dotnet run --configuration Release --project "$ROOT/src/Mauw.Erp.DataGenerator" -- \
    seed --confirm-database mauw_erp_loadtest

4. Dopo il popolamento, avviare l'API contro il database desiderato:
  ConnectionStrings__DefaultConnection="$MAUW_LOADTEST_CONNECTION" \
    dotnet run --project "$ROOT/src/Mauw.Erp.Api"
   Per tornare al DB sviluppo, fermare l'API e riavviarla con la normale
   connessione di sviluppo. Non cambiano configurazioni tracciate o codice.
   Non usare insieme due istanze API sulla stessa porta.

Ricreare e ripopolare
--------------------
Il reset e distruttivo e non fa backup. Fermare prima l'API: viene eseguito
DROP DATABASE senza FORCE, quindi sessioni ancora aperte fanno fallire il
reset anziche essere disconnesse. Sono necessarie due conferme:
  dotnet run --project "$ROOT/src/Mauw.Erp.DataGenerator" -- \
    reset --confirm-database mauw_erp_loadtest --allow-reset
Poi ripetere dotnet ef database update e seed come sopra.
Il generatore non usa EnsureCreated, non modifica migrazioni e non applica
automaticamente migrazioni: la ricreazione dello schema resta affidata a EF CLI.
Vale il blocco della catena di migrazioni descritto in Preparazione.
Ctrl+C interrompe il caricamento; i batch gia salvati rimangono. Non e prevista
la ripresa automatica: dopo errori o cancellazione ricreare il database.
Codici uscita: 0 successo, 2 input/precondizioni/generazione non validi,
1 errore infrastrutturale, 130 annullamento.

Scenario e parametri
--------------------
L'ultima richiesta usa CINQUE anni, non dieci. Fine predefinita: oggi UTC;
inizio: stessa data meno cinque anni. Per dieci anni usare --years 10.
Per confronti ripetibili fissare --end-date yyyy-MM-dd e --seed (default 42).
Gli identificativi tecnici e timestamp di creazione non sono un dump binario
ripetibile; il seed serve a ripetere le scelte dei dati e la distribuzione.

Default:
  --customers 5000             --active-customers 1000
  --suppliers 500              --agents 200
  --active-agents 50           --products 20000
  --initial-products 4000      --years 5
  --seed 42                   --batch-size 1000
  --weekly-pieces-factor 4     --sales-lag-days 60
  --sell-through-percent 75    --year-end-carryover-percent 50
  --end-date <oggi UTC>

Il 30% dei clienti e assegnato agli agenti attivi; il primo 10% dei fornitori
rappresenta l'80% dei pezzi ricevuti, non necessariamente l'80% del valore.
I clienti senza operazioni e gli agenti inattivi restano nelle anagrafiche.
Gli stocchisti fanno parte dei clienti operativi, non aumentano il totale.

Ogni contatto (cliente, fornitore, agente) ha dati anagrafici sintetici:
partita IVA di 11 cifre con cifra di controllo valida (univoca), codice fiscale
(= partita IVA per le societa; 16 caratteri con carattere di controllo per gli
agenti, persone fisiche), sede legale, 1-3 referenti (1 per gli agenti), PEC e
codice SDI (solo clienti). Valori fittizi, mai reali: email su example.com,
numeri non assegnati. IBAN, REA e regime fiscale non sono popolati.

20.000 prodotti significa MODELLI, non singole SKU: ogni modello ha 5 o 6
colori per 7 taglie (XXS, XS, S, M, L, XL, XXL), circa 770.000 varianti.
300–600 articoli settimanali (x --weekly-pieces-factor, default 4 = 1.200–2.400,
circa 573.000 pezzi e 760.000 movimenti in cinque anni; 1 = 300–600)
significa PEZZI ricevuti, non nuovi modelli
moltiplicati per tutte le varianti: molte SKU avranno stock zero.
Ogni consegna di un modello e un lotto di 24 pezzi di UN colore con curva
taglie XXS..XXL = 1-3-5-6-5-3-1: le varianti in stock hanno piu pezzi, non uno.
Il caricamento iniziale introduce 4.000 modelli (un lotto ciascuno, quindi
96.000 pezzi); i successivi introducono progressivamente gli altri e
riassortiscono quelli gia introdotti, di solito con un altro colore.
Il fornitore dominante vale l'80% dei pezzi con la tolleranza di un lotto.
Colori: Nero, Bianco, Blu, Rosso, Verde, Beige con valori HTML reali.
Barcode/SKU delle varianti: EAN-13 numerico con cifra di controllo valida,
prefisso 29 (uso interno GS1), univoco e deterministico.
Per ogni modello la prima variante e marcata VarianteVetrina (una sola).
Idempotenza: seed non e un upsert; rifiuta database gia popolati. A parita di
--seed, --end-date e opzioni, i dati anagrafici, i prezzi e la distribuzione
sono identici tra esecuzioni (verificato dai test); cambiano solo Id e timestamp.

Costi netti 5–40 EUR; prezzo di vendita netto dalla formula prezzi demo fissa
(+40% e +10% a cascata, arrotondato per eccesso a 0,10 EUR).
Esempio coerente: 20,2653 -> 20,30; 19,8679 -> 19,90.
L'esempio originale 19,8679 -> 20,00 e incompatibile con un passo fisso
di 0,10: non e stata inventata una soglia commerciale diversa sotto 20 EUR.
Prezzi persistiti con precisione a 8 decimali; importi documenti a 2 decimali.
Le imposte e i totali sono calcolati solo dal calcolatore fiscale condiviso.

Sono previsti 2–3 carichi per settimana (salvo l'ultima settimana incompleta),
oltre al carico iniziale, per un totale settimanale di 300–600 pezzi per il
fattore (--weekly-pieces-factor, default 4).
La vendita ordinaria punta al --sell-through-percent (default 75) dei pezzi
acquistati da almeno --sales-lag-days giorni (default 60): la merce resta a
scaffale prima di essere venduta. Con --sales-lag-days 0 si vende subito dopo
ogni carico. A fine anno si liquida (sconto 50%, agli stocchisti, merce piu
vecchia per prima) solo la quota di residuo non riportata: il
--year-end-carryover-percent (default 50) resta a magazzino nell'anno nuovo.
Con 0 il magazzino commerciale e vuoto a ogni 31 dicembre. Dopo la
liquidazione il ciclo ordinario riparte dalla merce ricevuta dopo (nei primi
giorni dell'anno non ci sono vendite ordinarie). Il riepilogo riporta i
valori reali.
Il 2% e reso al fornitore e l'1% e trasferito nel magazzino fallati.
Le quote sono applicate a pezzi interi; arrotondamenti e ultimo centinaio
incompleto possono produrre piccoli scostamenti, riportati nel riepilogo.
Il fallato non viene rivenduto come merce commerciale e resta separato.
I movimenti distinguono carichi, vendite, resi al fornitore e fallati.
Ogni vendita genera ordine, DDT e fattura con le stesse righe e totali.
Le vendite sono intestate solo ad anagrafiche con profilo cliente: agenti e
fornitori non ricevono mai ordini, DDT o fatture di vendita.
L'IVA ordinaria dello scenario e il 22%; non simula tutti i regimi fiscali,
la certificazione SDI, le commissioni o una contabilita completa.
Le giacenze derivano dai movimenti; non si vende merce non ancora ricevuta.
Le liquidazioni di fine anno cedono il residuo commerciale agli stocchisti;
l'anno corrente mantiene il residuo per provare l'operativita.
Il riepilogo finale del comando riporta le quantita generate.
Il programma crea i dati per le prove: misurare separatamente ricerche,
paginazione, disponibilita, ordini, fatture e report PDF nell'applicazione.
Non promette tempi di risposta o dimensioni del database a priori.

Sviluppi futuri e verifiche
--------------------------
Quando cambiano entita, costruttori, vincoli, flussi, classificazioni fiscali
o precisioni, aggiornare generatore, test dedicati e questo manuale nella
stessa modifica. La regola e anche in .github/copilot-instructions.md.
Non copiare formule contabili qui e non aggiungere riferimenti al generatore
da Api/Application/Infrastructure/Domain/Contracts o dai loro startup.

Compilazione e test esistenti:
  dotnet build "$ROOT/Mauw.Erp.sln"
  dotnet test "$ROOT/src/Mauw.Erp.UnitTests" --filter FullyQualifiedName~DataGenerator

Prima di una nuova prova completa, verificare il piano e la connessione,
fare una prova ridotta con parametri coerenti, quindi ricreare il database.
Esempio ridotto (stesso orizzonte, meno anagrafiche e SKU):
  dotnet run --configuration Release --project "$ROOT/src/Mauw.Erp.DataGenerator" -- \
    seed --confirm-database mauw_erp_loadtest \
    --customers 100 --active-customers 20 --suppliers 10 \
    --agents 10 --active-agents 5 --products 100 --initial-products 20 \
    --end-date 2026-10-19 --seed 42
I clienti (totali e attivi) devono essere multipli di 10; i modelli e il
carico iniziale multipli di 5. Gli agenti attivi non possono superare il
numero di clienti attivi rappresentati. Usare plan con le stesse opzioni
per verificare questi vincoli senza scrivere.
Conservare seed, data finale, anni, commit e parametri insieme ai risultati
dei benchmark; dopo un cambio schema ricreare il dataset.
