FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CircDesk.sln .
COPY src/CircDesk.Api/CircDesk.Api.csproj src/CircDesk.Api/
COPY tests/CircDesk.Api.Tests/CircDesk.Api.Tests.csproj tests/CircDesk.Api.Tests/
RUN dotnet restore src/CircDesk.Api/CircDesk.Api.csproj

COPY src/ src/
RUN dotnet publish src/CircDesk.Api/CircDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN useradd --uid 1000 appuser
USER appuser

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CircDesk.Api.dll"]
