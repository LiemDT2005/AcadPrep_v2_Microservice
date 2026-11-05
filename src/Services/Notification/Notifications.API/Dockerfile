FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.EventBus/BuildingBlocks.EventBus.csproj src/BuildingBlocks/BuildingBlocks.EventBus/
COPY src/Services/Notification/Notifications.API/Notifications.API.csproj src/Services/Notification/Notifications.API/
RUN dotnet restore src/Services/Notification/Notifications.API/Notifications.API.csproj
# Copy only Notification and its shared Result/event-bus dependencies; never copy Identity keys.
COPY src/BuildingBlocks/BuildingBlocks.Common/ src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.EventBus/ src/BuildingBlocks/BuildingBlocks.EventBus/
COPY src/Services/Notification/ src/Services/Notification/
RUN dotnet publish src/Services/Notification/Notifications.API/Notifications.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
# Mount the RSA public key read-only and set Jwt__PublicKeyPath at deployment.
# Supply ConnectionStrings__Default and Smtp__* securely; no database initialization runs here.
ENTRYPOINT ["dotnet", "Notifications.API.dll"]
