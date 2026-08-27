FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend ./backend

WORKDIR /src/backend
RUN dotnet restore ./tools/LibrarySystem.Tools.CoverImport/LibrarySystem.Tools.CoverImport.csproj
RUN dotnet publish ./tools/LibrarySystem.Tools.CoverImport/LibrarySystem.Tools.CoverImport.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LibrarySystem.Tools.CoverImport.dll"]
