import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Ddt } from '../models/ddt.model';

@Injectable({ providedIn: 'root' })
export class DdtService {
  private readonly base = `${environment.apiUrl}/v1/ddts`;

  constructor(private readonly http: HttpClient) {}

  getById(id: string): Observable<Ddt> {
    return this.http.get<Ddt>(`${this.base}/${id}`);
  }
}
