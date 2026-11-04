# Mauw ERP

ERP B2B per il commercio wholesale, composto da API ASP.NET Core, applicazione
web Angular e database PostgreSQL.

## Funzionalità

- Anagrafiche clienti, fornitori e agenti; catalogo con varianti, listini e
  formule di prezzo.
- Ordini, banco cassa, magazzino, DDT e acquisti con riconciliazione dei carichi.
- Fatture, registrazione pagamenti, partitario cliente e note di credito.
- Ciclo passivo operativo per fatture fornitori, spese e rimborsi con allegati,
  approvazione e pagamenti parziali.
- Anagrafica dipendenti Admin-only con matricola, inquadramento, dati annui e
  registrazione dei costi aziendali mensili con cedolino allegato.
- Calcolo fiscale centralizzato e generazione PDF/XML FatturaPA.
- Ritiro in showroom: completa un ordine confermato senza prelievo, imballaggio
  o DDT e registra comunque lo scarico della giacenza.

La trasmissione SDI ha un flusso applicativo con validazione XML e archiviazione
del documento. In Development usa un provider simulato; negli altri ambienti il
provider predefinito non è configurato. Per l’invio fiscale reale serve quindi
integrare e configurare un provider SDI di produzione.

## Architettura

La soluzione è organizzata in progetti .NET separati per Domain, Contracts,
Application, Infrastructure e API. Il frontend è in `src/Mauw.Erp.Web`; i test
xUnit e il generatore opzionale di dati di volume sono in progetti distinti.

- [Stato architetturale e funzionale](docs/STATO_AVANZAMENTO_ARCHITETTURALE.md)
- [Documentazione Angular e flussi web](docs/README-ANGULAR.md)
- [Decisioni sul calcolo prezzi](docs/PRICE_CALCULATOR_DECISIONS.md)
- [Generatore dati di volume](src/Mauw.Erp.DataGenerator/README.txt)

## Build e test

Dalla radice del repository:

```powershell
dotnet build Mauw.Erp.sln
dotnet test src\Mauw.Erp.UnitTests\Mauw.Erp.UnitTests.csproj
```

I test PostgreSQL usano Testcontainers e richiedono Docker. Per il frontend,
dalla cartella `src\Mauw.Erp.Web`:

```powershell
npm ci
npm run build
npm run test -- --watch=false --browsers=ChromeHeadless
```

Le migrazioni si creano e applicano con EF Core CLI:

```powershell
dotnet ef migrations add NomeMigrazione --project src\Mauw.Erp.Infrastructure --startup-project src\Mauw.Erp.Api
dotnet ef database update --project src\Mauw.Erp.Infrastructure --startup-project src\Mauw.Erp.Api
```

Per configurazione locale, avvio Docker e dettagli dei moduli web, consultare
[`docs/README-ANGULAR.md`](docs/README-ANGULAR.md). Il DataGenerator è
facoltativo: usare esclusivamente un database dedicato `*_loadtest`, come
descritto nel suo README.

Il ciclo passivo è operativo, ma non costituisce contabilità generale né
liquidazione IVA. Rimborsi, anagrafica dipendenti, costi mensili e cedolini sono
riservati all'Admin; il gestionale non calcola paghe, ammortamenti o
adempimenti fiscali. I documenti e le spese servono al controllo interno e al
passaggio dei dati al commercialista.
