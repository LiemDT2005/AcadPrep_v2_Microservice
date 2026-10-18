# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj src/BuildingBlocks/BuildingBlocks.Common/
COPY src/BuildingBlocks/BuildingBlocks.EventBus/BuildingBlocks.EventBus.csproj src/BuildingBlocks/BuildingBlocks.EventBus/
COPY src/Services/Learning/Learning.Domain/Learning.Domain.csproj src/Services/Learning/Learning.Domain/
COPY src/Services/Learning/Learning.Application/Learning.Application.csproj src/Services/Learning/Learning.Application/
COPY src/Services/Learning/Learning.Infrastructure/Learning.Infrastructure.csproj src/Services/Learning/Learning.Infrastructure/
COPY src/Services/Learning/Learning.API/Learning.API.csproj src/Services/Learning/Learning.API/

# Restore dependencies
RUN dotnet restore src/Services/Learning/Learning.API/Learning.API.csproj

# Copy all source files
COPY src/ src/

# Publish
WORKDIR /src/src/Services/Learning/Learning.API
RUN dotnet publish -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 5005
ENV ASPNETCORE_URLS=http://+:5005

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Learning.API.dll"]
