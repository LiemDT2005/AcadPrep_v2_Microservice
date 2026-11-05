FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.EventBus/BuildingBlocks.EventBus.csproj src/BuildingBlocks/BuildingBlocks.EventBus/
COPY src/Services/Billing/Billing.API/Billing.API.csproj src/Services/Billing/Billing.API/
RUN dotnet restore src/Services/Billing/Billing.API/Billing.API.csproj
# Copy only Billing and the shared event contracts/bus it uses; never copy Identity keys.
COPY src/BuildingBlocks/BuildingBlocks.Common/ src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.EventBus/ src/BuildingBlocks/BuildingBlocks.EventBus/
COPY src/Services/Billing/Billing.API/ src/Services/Billing/Billing.API/
RUN dotnet publish src/Services/Billing/Billing.API/Billing.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Billing.API.dll"]
