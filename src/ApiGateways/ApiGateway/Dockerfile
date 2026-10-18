FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/ApiGateways/ApiGateway/ApiGateway.csproj", "src/ApiGateways/ApiGateway/"]
RUN dotnet restore "src/ApiGateways/ApiGateway/ApiGateway.csproj"

COPY src/ApiGateways/ src/ApiGateways/
WORKDIR "/src/src/ApiGateways/ApiGateway"
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5080
ENV ASPNETCORE_URLS=http://+:5080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
