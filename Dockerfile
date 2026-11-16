# ===== Stage 1: build the React client =====
FROM node:lts-alpine AS client-build
WORKDIR /src/client

# Install packages first, so this layer is cached when only the code changes.
COPY client/package.json client/package-lock.json ./
RUN npm ci

COPY client/ ./
RUN npm run build

# ===== Stage 2: build the .NET server =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src

COPY server/StudyPilot.Api.csproj server/
RUN dotnet restore server/StudyPilot.Api.csproj

COPY server/ server/
RUN dotnet publish server/StudyPilot.Api.csproj -c Release -o /app/publish --no-restore

# ===== Stage 3: the final, small runtime image =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=server-build /app/publish ./
COPY --from=client-build /src/client/dist ./wwwroot

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as a regular user instead of root.
USER $APP_UID

ENTRYPOINT ["dotnet", "StudyPilot.Api.dll"]