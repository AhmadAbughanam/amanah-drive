FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/AmanahDrive.Api/AmanahDrive.Api.csproj src/AmanahDrive.Api/
RUN dotnet restore src/AmanahDrive.Api/AmanahDrive.Api.csproj

COPY . .
RUN dotnet publish src/AmanahDrive.Api/AmanahDrive.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AmanahDrive.Api.dll"]
