FROM node:24-alpine AS web
WORKDIR /web
COPY apps/web/package*.json ./
RUN npm ci --no-fund
COPY apps/web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/Hhh.Domain/ src/Hhh.Domain/
COPY src/Hhh.Infrastructure/ src/Hhh.Infrastructure/
COPY src/Hhh.Api/ src/Hhh.Api/
RUN dotnet publish src/Hhh.Api/Hhh.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist/web/browser ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hhh.Api.dll"]
