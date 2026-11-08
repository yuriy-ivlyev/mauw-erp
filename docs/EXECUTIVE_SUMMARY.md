# Mauw ERP — Sintesi esecutiva

Stato rilevato al **2026-11-05**. Per dettagli architetturali, flussi, test e
attività aperte, fare riferimento allo [stato architetturale e funzionale](./STATO_AVANZAMENTO_ARCHITETTURALE.md).

## Prodotto

Mauw ERP è un gestionale B2B per il commercio wholesale, con frontend Angular,
API ASP.NET Core e database PostgreSQL. La soluzione è organizzata in progetti
separati per Domain, Contracts, Application, Infrastructure e API; i test
xUnit e il DataGenerator opzionale sono mantenuti in progetti distinti.

## Funzioni operative

- Anagrafiche clienti, fornitori e agenti; catalogo prodotti e varianti, listini
  e formule di prezzo.
- Ordini, vendita al banco, magazzino, spedizioni/DDT e ritiro in showroom.
- Fatturazione da ordini completati, pagamenti, partitario cliente e note di
  credito.
- Acquisti e carico merce tramite ordini fornitore e DDT, con riconciliazione.
- Ciclo passivo operativo per fatture fornitori, spese e rimborsi.
- Gestione aziendale Admin-only: impostazioni e loghi aziendali, anagrafica
  dipendenti, documenti HR, cartellini e costi mensili con cedolino.
- PDF e generazione/validazione XML FatturaPA, con calcolo fiscale centralizzato.

## Limiti da considerare

- Il flusso SDI valida e archivia l'XML; in Development usa un provider
  simulato. Negli altri ambienti il provider predefinito non è configurato:
  l'invio a un trasportatore SDI reale e la ricezione automatica degli esiti
  non sono disponibili.
- Il ciclo passivo e i dati HR supportano il controllo operativo, non la
  contabilità generale, la liquidazione IVA, il calcolo paghe o gli
  adempimenti contributivi.
- I DDT fornitore possono ricevere dati OCR/IA già strutturati, ma non è
  integrato un motore OCR.
- Il DataGenerator è facoltativo e va usato esclusivamente su database dedicati
  `*_loadtest`.

## Documentazione

- [Stato architetturale e funzionale](./STATO_AVANZAMENTO_ARCHITETTURALE.md)
- [Frontend Angular e flussi web](./README-ANGULAR.md)
- [Decisioni sul calcolo prezzi](./PRICE_CALCULATOR_DECISIONS.md)
- [Generatore dati di volume](../src/Mauw.Erp.DataGenerator/README.txt)
