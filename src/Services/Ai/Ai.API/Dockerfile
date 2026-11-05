FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Ai/Ai.API/Ai.API.csproj src/Services/Ai/Ai.API/
RUN dotnet restore src/Services/Ai/Ai.API/Ai.API.csproj
# Copy only the AI service and the shared JWT helper; never copy Identity keys or API keys.
COPY src/BuildingBlocks/BuildingBlocks.Common/ src/BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Ai/Ai.API/ src/Services/Ai/Ai.API/
RUN dotnet publish src/Services/Ai/Ai.API/Ai.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ai.API.dll"]
