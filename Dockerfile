# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TicketFlow.slnx ./
COPY src/ src/

RUN dotnet restore src/TicketFlow.Api/TicketFlow.Api.csproj

COPY . .
RUN dotnet publish src/TicketFlow.Api/TicketFlow.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TicketFlow.Api.dll"]
