FROM node:22-alpine AS deps

WORKDIR /src/frontend/widget

COPY frontend/widget/package.json frontend/widget/package-lock.json ./

RUN npm config set fetch-retries 5 \
	&& npm config set fetch-retry-factor 2 \
	&& npm config set fetch-retry-mintimeout 20000 \
	&& npm config set fetch-retry-maxtimeout 120000 \
	&& npm ci --no-audit

FROM node:22-alpine AS build

WORKDIR /src/frontend/widget

COPY --from=deps /src/frontend/widget/node_modules ./node_modules
COPY frontend/widget/ ./

RUN npm run build

FROM nginx:1.27-alpine AS runner

COPY frontend/widget/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/frontend/widget/dist /usr/share/nginx/html

EXPOSE 8080
