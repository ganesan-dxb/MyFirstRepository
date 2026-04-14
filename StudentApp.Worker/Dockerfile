FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StudentApp.Shared/StudentApp.Shared.csproj             StudentApp.Shared/
COPY StudentApp.Worker/StudentApp.Worker.csproj             StudentApp.Worker/
RUN dotnet restore "StudentApp.Worker/StudentApp.Worker.csproj"

COPY StudentApp.Shared/ StudentApp.Shared/
COPY StudentApp.Worker/ StudentApp.Worker/
WORKDIR "/src/StudentApp.Worker"
RUN dotnet publish "StudentApp.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StudentApp.Worker.dll"]
