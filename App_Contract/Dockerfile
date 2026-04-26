FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["App_Contract/App_Contract.csproj", "App_Contract/"]
RUN dotnet restore "App_Contract/App_Contract.csproj"
COPY . .
WORKDIR "/src/App_Contract"
RUN dotnet build "App_Contract.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App_Contract.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "App_Contract.dll"]
