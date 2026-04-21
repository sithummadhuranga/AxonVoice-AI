FROM node:22-alpine AS deps

WORKDIR /src/frontend/dashboard

COPY frontend/dashboard/package.json frontend/dashboard/package-lock.json ./

RUN npm config set fetch-retries 5 \
	&& npm config set fetch-retry-factor 2 \
	&& npm config set fetch-retry-mintimeout 20000 \
	&& npm config set fetch-retry-maxtimeout 120000 \
	&& npm ci --no-audit

FROM node:22-alpine AS build

WORKDIR /src/frontend/dashboard

ENV NEXT_TELEMETRY_DISABLED=1

COPY --from=deps /src/frontend/dashboard/node_modules ./node_modules
COPY frontend/dashboard/ ./

RUN npm run build

FROM node:22-alpine AS runner

WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV HOSTNAME=0.0.0.0
ENV PORT=3000

COPY --from=build /src/frontend/dashboard/.next/standalone ./
COPY --from=build /src/frontend/dashboard/.next/static ./.next/static
COPY --from=build /src/frontend/dashboard/public ./public

EXPOSE 3000

CMD ["node", "server.js"]
