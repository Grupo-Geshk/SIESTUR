# ============================
# BUILD STAGE (SDK 8)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos sólo los csproj para cachear el restore
# Si tienes una .sln en la raíz, puedes copiarla también y hacer restore de la .sln
COPY SIESTUR/*.csproj ./SIESTUR/
# (si tienes Directorios adicionales con csproj, repite COPY por cada uno)

RUN dotnet restore ./SIESTUR/SIESTUR.csproj

# Copiamos el resto del repo y publicamos
COPY . .
RUN dotnet publish ./SIESTUR/SIESTUR.csproj -c Release -o /app/publish

# ============================
# RUNTIME STAGE (ASP.NET 8)
# ============================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Puerto para Railway (usa la var PORT si está, si no 8080)
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}

COPY --from=build /app/publish ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "SIESTUR.dll"]
