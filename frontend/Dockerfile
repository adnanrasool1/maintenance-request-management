# Multi-stage build for the web container: build the Angular app with Node,
# then serve the static output (and proxy /api) with nginx.

# --- Build stage ---------------------------------------------------------------
FROM node:24.21.0-alpine3.24 AS build
WORKDIR /app

# Install the exact dependency versions from the lock file first, so this layer
# stays cached until package.json / package-lock.json change.
COPY package.json package-lock.json .npmrc ./
RUN npm ci --no-audit --no-fund

COPY . .
# Production configuration (the default): real API through /api, no mock backend.
RUN npm run build

# --- Runtime stage -------------------------------------------------------------
FROM nginx:1.30.5-alpine3.24

COPY nginx.conf /etc/nginx/conf.d/default.conf
RUN rm -rf /usr/share/nginx/html/*
COPY --from=build /app/dist/mra-web/browser /usr/share/nginx/html

EXPOSE 80
