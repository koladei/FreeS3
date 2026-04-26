# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM node:22-alpine AS client-build
WORKDIR /client
COPY client-app/package*.json ./
RUN npm install
COPY client-app/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy project files and restore dependencies
COPY ["App_Storage/App_Storage.csproj", "App_Storage/"]
COPY ["Common/Data/Data.csproj", "Data/"]
COPY ["Common/Models/Models.csproj", "Models/"]
COPY ["Common/Services/Services.csproj", "Services/"]
RUN dotnet restore "App_Storage/App_Storage.csproj"

# Copy the remaining source code
COPY . .
WORKDIR "/src/App_Storage"
RUN dotnet build "App_Storage.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App_Storage.csproj" -c Release -o /app/publish /p:UseAppHost=false
COPY --from=client-build /client/dist /app/publish/wwwroot

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "App_Storage.dll"]
