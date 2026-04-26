FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["App_Gateway/App_Gateway.csproj", "App_Gateway/"]
RUN dotnet restore "App_Gateway/App_Gateway.csproj"
COPY . .
WORKDIR "/src/App_Gateway"
RUN dotnet build "App_Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App_Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "App_Gateway.dll"]
