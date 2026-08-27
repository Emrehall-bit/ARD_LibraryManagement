FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend ./backend

WORKDIR /src/backend
RUN dotnet restore ./src/LibrarySystem.Api/LibrarySystem.Api.csproj
RUN dotnet publish ./src/LibrarySystem.Api/LibrarySystem.Api.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LibrarySystem.Api.dll"]
