#!/bin/sh
# Converte il PFX di sviluppo (certs/aspnetapp.pfx, generato da generate-dev-cert.ps1)
# in PEM per nginx. Eseguito dall'entrypoint dell'immagine prima dell'avvio.
set -eu

PFX=/certs/aspnetapp.pfx
OUT=/etc/nginx/ssl

if [ ! -f "$PFX" ]; then
    echo "ERRORE: $PFX non trovato. Esegui generate-dev-cert.ps1 e monta ./certs in /certs." >&2
    exit 1
fi

mkdir -p "$OUT"
PASS="${CERT_PASSWORD:-}"

# OpenSSL 3 puo' richiedere -legacy per PFX con cifrari datati
convert() {
    openssl pkcs12 -in "$PFX" -passin "pass:$PASS" "$@" -nodes
}

convert -clcerts -nokeys -out "$OUT/tls.crt" 2>/dev/null || convert -legacy -clcerts -nokeys -out "$OUT/tls.crt"
convert -nocerts -out "$OUT/tls.key" 2>/dev/null || convert -legacy -nocerts -out "$OUT/tls.key"
chmod 600 "$OUT/tls.key"
