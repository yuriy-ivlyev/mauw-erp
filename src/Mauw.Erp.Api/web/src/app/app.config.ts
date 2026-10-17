import { ApplicationConfig, APP_INITIALIZER, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { routes } from './app.routes';
import { AuthService } from './services/auth.service';

function initAuth(auth: AuthService) {
  return () => auth.initialize();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(),
    {
      provide:    APP_INITIALIZER,
      useFactory: initAuth,
      deps:       [AuthService],
      multi:      true
    }
  ]
};
