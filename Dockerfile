# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV BuildingInsideDocker=true
WORKDIR /src

COPY . .

RUN dotnet restore "BarRestPOS.csproj" --disable-parallel
RUN dotnet publish "BarRestPOS.csproj" -c Release -o /app/publish --no-restore 

# Server Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV TZ=America/Managua
ENV GENERIC_TIMEZONE=America/Managua

# Persist ASP.NET Core DataProtection keys across restarts/deploys
#RUN mkdir -p /app/dp-keys
#VOLUME ["/app/dp-keys"]


COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BarRestPOS.dll"]