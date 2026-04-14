FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5200

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StudentApp.Gateway/StudentApp.Gateway.csproj   StudentApp.Gateway/
RUN dotnet restore "StudentApp.Gateway/StudentApp.Gateway.csproj"

COPY StudentApp.Gateway/ StudentApp.Gateway/
WORKDIR "/src/StudentApp.Gateway"
RUN dotnet publish "StudentApp.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StudentApp.Gateway.dll"]
