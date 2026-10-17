FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Web/Storefront.Web/Storefront.Web.csproj", "src/Web/Storefront.Web/"]
RUN dotnet restore "src/Web/Storefront.Web/Storefront.Web.csproj"

COPY src/Web/ src/Web/
WORKDIR "/src/src/Web/Storefront.Web"
RUN dotnet build "Storefront.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Storefront.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5100
ENV ASPNETCORE_URLS=http://+:5100
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Storefront.Web.dll"]
