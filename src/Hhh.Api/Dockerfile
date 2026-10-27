FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/Hhh.Domain/ src/Hhh.Domain/
COPY src/Hhh.Infrastructure/ src/Hhh.Infrastructure/
COPY src/Hhh.Api/ src/Hhh.Api/
RUN dotnet publish src/Hhh.Api/Hhh.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hhh.Api.dll"]
