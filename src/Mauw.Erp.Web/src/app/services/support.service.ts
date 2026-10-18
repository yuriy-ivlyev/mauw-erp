import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface ColorDto {
  id: string;
  name: string;
  htmlColor: string;
  isVisible: boolean;
}

export interface UpdateColorRequest {
  name: string;
  htmlColor: string;
  isVisible: boolean;
}

export interface CreateColorRequest {
  name: string;
  htmlColor: string;
}

export interface SizeDto {
  id: string;
  name: string;
  isVisible: boolean;
}

export interface UpdateSizeRequest {
  name: string;
  isVisible: boolean;
}

export interface CategoryDto {
  id: string;
  name: string;
  macroCategoryId?: string;
  macroCategoryName?: string;
  isVisible: boolean;
}

export interface UpdateCategoryRequest {
  name: string;
  macroCategoryId?: string;
  isVisible: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  macroCategoryId?: string;
}

export interface MaterialCompositionDto {
  id: string;
  componentName: string;
  percentageValue: number;
}

export interface MaterialDto {
  id: string;
  name: string;
  isVisible: boolean;
  composition: MaterialCompositionDto[];
}

export interface UpdateMaterialRequest {
  name: string;
  composition?: MaterialCompositionData[];
  isVisible: boolean;
}

export interface CreateMaterialRequest {
  name: string;
  composition?: MaterialCompositionData[];
}

export interface MaterialCompositionData {
  componentName: string;
  percentageValue: number;
}

export interface MacroCategoryDto {
  id: string;
  name: string;
  isVisible: boolean;
}

export interface UpdateMacroCategoryRequest {
  name: string;
  isVisible: boolean;
}

export interface CreateMacroCategoryRequest {
  name: string;
}

@Injectable({
  providedIn: 'root'
})
export class SupportService {
  private apiUrl = environment.apiUrl;
  private baseEndpoint = '/v1/support';

  constructor(private http: HttpClient) { }

  // Colors
  getColors() {
    return this.http.get<ColorDto[]>(`${this.apiUrl}${this.baseEndpoint}/colors`);
  }

  getColor(id: string) {
    return this.http.get<ColorDto>(`${this.apiUrl}${this.baseEndpoint}/colors/${id}`);
  }

  createColor(data: CreateColorRequest) {
    return this.http.post<ColorDto>(`${this.apiUrl}${this.baseEndpoint}/colors`, data);
  }

  updateColor(id: string, data: UpdateColorRequest) {
    return this.http.put<ColorDto>(`${this.apiUrl}${this.baseEndpoint}/colors/${id}`, data);
  }

  deleteColor(id: string) {
    return this.http.delete(`${this.apiUrl}${this.baseEndpoint}/colors/${id}`);
  }

  // Sizes
  getSizes() {
    return this.http.get<SizeDto[]>(`${this.apiUrl}${this.baseEndpoint}/sizes`);
  }

  getSize(id: string) {
    return this.http.get<SizeDto>(`${this.apiUrl}${this.baseEndpoint}/sizes/${id}`);
  }

  createSize(data: { name: string }) {
    return this.http.post<SizeDto>(`${this.apiUrl}${this.baseEndpoint}/sizes`, data);
  }

  updateSize(id: string, data: UpdateSizeRequest) {
    return this.http.put<SizeDto>(`${this.apiUrl}${this.baseEndpoint}/sizes/${id}`, data);
  }

  deleteSize(id: string) {
    return this.http.delete(`${this.apiUrl}${this.baseEndpoint}/sizes/${id}`);
  }

  // Categories
  getCategories() {
    return this.http.get<CategoryDto[]>(`${this.apiUrl}${this.baseEndpoint}/categories`);
  }

  getCategory(id: string) {
    return this.http.get<CategoryDto>(`${this.apiUrl}${this.baseEndpoint}/categories/${id}`);
  }

  createCategory(data: CreateCategoryRequest) {
    return this.http.post<CategoryDto>(`${this.apiUrl}${this.baseEndpoint}/categories`, data);
  }

  updateCategory(id: string, data: UpdateCategoryRequest) {
    return this.http.put<CategoryDto>(`${this.apiUrl}${this.baseEndpoint}/categories/${id}`, data);
  }

  deleteCategory(id: string) {
    return this.http.delete(`${this.apiUrl}${this.baseEndpoint}/categories/${id}`);
  }

  // Materials
  getMaterials() {
    return this.http.get<MaterialDto[]>(`${this.apiUrl}${this.baseEndpoint}/materials`);
  }

  getMaterial(id: string) {
    return this.http.get<MaterialDto>(`${this.apiUrl}${this.baseEndpoint}/materials/${id}`);
  }

  createMaterial(data: CreateMaterialRequest) {
    return this.http.post<MaterialDto>(`${this.apiUrl}${this.baseEndpoint}/materials`, data);
  }

  updateMaterial(id: string, data: UpdateMaterialRequest) {
    return this.http.put<MaterialDto>(`${this.apiUrl}${this.baseEndpoint}/materials/${id}`, data);
  }

  deleteMaterial(id: string) {
    return this.http.delete(`${this.apiUrl}${this.baseEndpoint}/materials/${id}`);
  }

  // MacroCategories
  getMacroCategories() {
    return this.http.get<MacroCategoryDto[]>(`${this.apiUrl}${this.baseEndpoint}/macro-categories`);
  }

  getMacroCategory(id: string) {
    return this.http.get<MacroCategoryDto>(`${this.apiUrl}${this.baseEndpoint}/macro-categories/${id}`);
  }

  createMacroCategory(data: CreateMacroCategoryRequest) {
    return this.http.post<MacroCategoryDto>(`${this.apiUrl}${this.baseEndpoint}/macro-categories`, data);
  }

  updateMacroCategory(id: string, data: UpdateMacroCategoryRequest) {
    return this.http.put<MacroCategoryDto>(`${this.apiUrl}${this.baseEndpoint}/macro-categories/${id}`, data);
  }

  deleteMacroCategory(id: string) {
    return this.http.delete(`${this.apiUrl}${this.baseEndpoint}/macro-categories/${id}`);
  }
}
