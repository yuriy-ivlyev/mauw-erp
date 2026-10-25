import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { defer, finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';

export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  const loadingService = inject(LoadingService);

  return defer(() => {
    loadingService.beginRequest();
    return next(request).pipe(
      finalize(() => loadingService.endRequest())
    );
  });
};
