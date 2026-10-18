# Reverse proxy nginx che espone il frontend in HTTPS (certificato di sviluppo).

FROM nginx:alpine

RUN apk add --no-cache openssl

COPY default.conf.template /etc/nginx/templates/default.conf.template
COPY 10-pfx-to-pem.sh /docker-entrypoint.d/10-pfx-to-pem.sh
RUN sed -i 's/\r$//' /docker-entrypoint.d/10-pfx-to-pem.sh \
    && chmod +x /docker-entrypoint.d/10-pfx-to-pem.sh

# Container frontend interno
ENV WEB_UPSTREAM=http://web:80

EXPOSE 443
