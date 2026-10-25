import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  readonly isLoading = signal(false);

  private activeRequests = 0;
  private showTimer: ReturnType<typeof setTimeout> | undefined;

  beginRequest(): void {
    this.activeRequests += 1;
    if (this.activeRequests === 1) {
      this.showTimer = setTimeout(() => {
        if (this.activeRequests > 0) {
          this.isLoading.set(true);
        }
      }, 250);
    }
  }

  endRequest(): void {
    this.activeRequests -= 1;
    if (this.activeRequests === 0) {
      if (this.showTimer !== undefined) {
        clearTimeout(this.showTimer);
        this.showTimer = undefined;
      }
      this.isLoading.set(false);
    }
  }
}
