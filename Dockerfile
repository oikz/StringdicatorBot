FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Stringdicator.csproj", "./"]
RUN dotnet restore "Stringdicator.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Stringdicator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Stringdicator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
LABEL org.opencontainers.image.source=https://github.com/oikz/stringdicatorbot
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Stringdicator.dll"]
