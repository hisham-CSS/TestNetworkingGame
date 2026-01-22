# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution or project files
COPY src/Chronos.Core/Chronos.Core.csproj src/Chronos.Core/
COPY src/Chronos.Net/Chronos.Net.csproj src/Chronos.Net/
COPY src/Chronos.RelayServer/Chronos.RelayServer.csproj src/Chronos.RelayServer/

# Restore dependencies
RUN dotnet restore src/Chronos.RelayServer/Chronos.RelayServer.csproj

# Copy the rest of the source
COPY src/ src/

# Publish
WORKDIR /source/src/Chronos.RelayServer
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose UDP port
EXPOSE 7777/udp

ENTRYPOINT ["dotnet", "Chronos.RelayServer.dll"]
