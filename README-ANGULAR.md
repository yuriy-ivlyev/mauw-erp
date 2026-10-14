# Mauw ERP - Angular Frontend

La parte web è stata migrata da Blazor a **Angular 18** con il framework standalone di Angular.

## Struttura

```
src/web/
├── src/
│   ├── app/
│   │   ├── components/       # Componenti Angular
│   │   ├── services/         # Servizi (ApiService)
│   │   ├── app.config.ts     # Configurazione app
│   │   ├── app.routes.ts     # Routing
│   │   └── app.component.ts  # Root component
│   ├── environments/         # Config ambienti (dev/prod)
│   ├── index.html
│   ├── main.ts
│   └── styles.scss
├── package.json
├── angular.json              # Config Angular CLI
└── tsconfig.json
```

## Sviluppo locale

```bash
cd src/web
npm install
npm start  # Avvia dev server su http://localhost:4200
```

## Build per produzione

```bash
cd src/web
npm run build  # Output in dist/web/browser
```

## Docker

Il Dockerfile.web è stato aggiornato per:
1. Buildare con Node.js
2. Servire con Nginx (porta 80)
3. Proxy `/api/*` verso il backend

```bash
docker-compose up  # Avvia tutto
```

## API Communication

- **Dev**: http://localhost:5019/api (da environment.ts)
- **Prod**: /api (from environment.prod.ts)

Usa `ApiService` per comunicare con il backend:

```typescript
constructor(private api: ApiService) {}

this.api.get<MyData>('endpoint').subscribe(data => {
  // Handle data
});
```

## Notes

- Angular 18 con standalone components
- SCSS per gli stili
- Routing con RouterModule
- HttpClient per le API calls
