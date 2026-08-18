# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /app

# npm ci against the committed lockfile: a reproducible install, and one that fails loudly if the
# lockfile and manifest have drifted apart rather than silently resolving something else.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

# nginx's unprivileged image variant already runs as non-root; the stock image needs the directories
# it writes to be owned by the runtime user.
RUN touch /var/run/nginx.pid \
    && chown -R nginx:nginx /var/run/nginx.pid /var/cache/nginx /usr/share/nginx/html

USER nginx
EXPOSE 8080
