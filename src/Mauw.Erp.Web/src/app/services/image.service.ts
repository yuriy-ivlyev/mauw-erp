import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { ImageCategory, ImageDto } from '../models/product.model';

@Injectable({
  providedIn: 'root'
})
export class ImageService {
  private endpoint = 'images';

  constructor(private apiService: ApiService) { }

  list(targetId: string): Observable<ImageDto[]> {
    return this.apiService.get<ImageDto[]>(`${this.endpoint}/${targetId}`);
  }

  /** Senza categoria il backend assegna Main alla prima immagine e Zoom1 alle successive. */
  upload(targetId: string, file: File, category?: ImageCategory): Observable<ImageDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (category) form.append('category', category);
    return this.apiService.post<ImageDto>(`${this.endpoint}/${targetId}`, form);
  }

  remove(targetId: string, imageId: string): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${targetId}/${imageId}`);
  }

  /** Impostare Main scambia la categoria con l'attuale Principale. */
  setCategory(targetId: string, imageId: string, category: ImageCategory): Observable<ImageDto> {
    return this.apiService.put<ImageDto>(`${this.endpoint}/${targetId}/${imageId}/category`, { category });
  }
}
