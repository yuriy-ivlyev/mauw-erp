# Frontend Angular: build statico servito da nginx.
# Contesto di build: src/Mauw.Erp.Web

FROM node:20-alpine AS builder
WORKDIR /app

COPY package*.json ./
RUN npm ci --legacy-peer-deps

COPY . .
RUN npm run build

FROM nginx:alpine AS final

# Il template viene espanso all'avvio (envsubst) in /etc/nginx/conf.d/default.conf
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=builder /app/dist/web/browser /usr/share/nginx/html

# Container API interno a cui il frontend inoltra /api, /swagger e i callback OIDC
ENV API_UPSTREAM=http://api:8080

EXPOSE 80
