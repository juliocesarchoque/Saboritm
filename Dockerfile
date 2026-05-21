# Imagen de SDK para compilar la aplicación (.NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar el archivo del proyecto y restaurar las dependencias
COPY ["Backend/AppRecetas.Api/AppRecetas.Api.csproj", "Backend/AppRecetas.Api/"]
RUN dotnet restore "Backend/AppRecetas.Api/AppRecetas.Api.csproj"

# Copiar el resto del código del Backend y compilarlo
COPY Backend/AppRecetas.Api/ Backend/AppRecetas.Api/
WORKDIR "/src/Backend/AppRecetas.Api"
RUN dotnet publish "AppRecetas.Api.csproj" -c Release -o /app/publish

# Imagen de Runtime para ejecutar la aplicación
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# ASP.NET Core 8/9 usa por defecto el puerto 8080
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AppRecetas.Api.dll"]
