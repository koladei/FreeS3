FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["App_Auth/App_Auth.csproj", "App_Auth/"]
COPY ["Common/Data/Data.csproj", "Common/Data/"]
COPY ["Common/Models/Models.csproj", "Common/Models/"]
RUN dotnet restore "App_Auth/App_Auth.csproj"
COPY . .
WORKDIR "/src/App_Auth"
RUN dotnet build "App_Auth.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App_Auth.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "App_Auth.dll"]
