# ---------------------------
# Base runtime image
# ---------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5100

# ---------------------------
# Build image
# ---------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StudentApp.Shared/StudentApp.Shared.csproj StudentApp.Shared/
COPY StudentApp.API/StudentApp.API.csproj StudentApp.API/

RUN dotnet restore "StudentApp.API/StudentApp.API.csproj"

COPY StudentApp.Shared/ StudentApp.Shared/
COPY StudentApp.API/ StudentApp.API/

WORKDIR "/src/StudentApp.API"

RUN dotnet build "StudentApp.API.csproj" -c Debug -o /app/build
RUN dotnet publish "StudentApp.API.csproj" -c Debug -o /app/publish

# ---------------------------
# Final runtime image
# ---------------------------
FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

# ---------------------------
# Install debug tools (cached layer)
# ---------------------------
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl unzip procps \
    && curl -sSL https://aka.ms/getvsdbgsh -o /tmp/getvsdbg.sh \
    && chmod +x /tmp/getvsdbg.sh \
    && bash /tmp/getvsdbg.sh -v vs2022 -l /vsdbg \
    && rm /tmp/getvsdbg.sh \
    && rm -rf /var/lib/apt/lists/*

# ---------------------------
# Environment
# ---------------------------
ENV ASPNETCORE_URLS=http://0.0.0.0:5100
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "StudentApp.API.dll"]