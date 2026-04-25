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
COPY ["S3Server/S3Server.csproj", "S3Server/"]
COPY ["Common/Data/Data.csproj", "Data/"]
COPY ["Common/Models/Models.csproj", "Models/"]
COPY ["Common/Services/Services.csproj", "Services/"]
RUN dotnet restore "S3Server/S3Server.csproj"

# Copy the remaining source code
COPY . .
WORKDIR "/src/S3Server"
RUN dotnet build "S3Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "S3Server.csproj" -c Release -o /app/publish /p:UseAppHost=false
COPY --from=client-build /client/dist /app/publish/wwwroot

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "S3Server.dll"]
