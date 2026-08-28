# The playground is a static site with a compiler inside it, so the image is a web server and a
# folder. The two build stages exist because producing that folder needs Node for the editor and
# the .NET SDK for everything else, and neither is wanted in the image that ships.
#
# Build from the repository root, so that the Lens projects this one references are in context:
#
#     docker build -f Lens.Playground/Dockerfile -t lens-playground .
#     docker run --rm -p 8080:8080 lens-playground

# ---------------------------------------------------------------- editor assets

FROM node:22-alpine AS editor

WORKDIR /src/Lens.Playground
COPY Lens.Playground/package.json Lens.Playground/package-lock.json* ./
RUN npm ci --no-audit --no-fund

# ------------------------------------------------------------------- publishing

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# the project files first, so that a change to a script does not cost a restore
COPY Lens/Lens.csproj Lens/
COPY Lens.LanguageServer.Core/Lens.LanguageServer.Core.csproj Lens.LanguageServer.Core/
COPY Lens.Playground/Lens.Playground.csproj Lens.Playground/
RUN dotnet restore Lens.Playground/Lens.Playground.csproj

COPY Lens/ Lens/
COPY Lens.LanguageServer.Core/ Lens.LanguageServer.Core/
COPY Lens.Playground/ Lens.Playground/

# brought in rather than installed: the build must not reach for npm, and the editor is the only
# thing it would have reached for
COPY --from=editor /src/Lens.Playground/node_modules Lens.Playground/node_modules

RUN dotnet publish Lens.Playground/Lens.Playground.csproj \
        -c Release \
        -o /publish \
        --no-restore

# ----------------------------------------------------------------------- server

FROM caddy:2-alpine

COPY --from=build /publish/wwwroot /srv
COPY Lens.Playground/Caddyfile /etc/caddy/Caddyfile

# Railway sets PORT; the default matches what the Caddyfile falls back to.
ENV PORT=8080
EXPOSE 8080

# fails the container rather than serving a half-built folder
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
    CMD wget --spider -q http://127.0.0.1:${PORT}/index.html || exit 1

CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
