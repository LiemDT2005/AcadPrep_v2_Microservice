# Multi-stage build for Exam
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for caching dotnet restore
COPY ["src/Services/Exam/Exam.csproj", "src/Services/Exam/"]
COPY ["src/BuildingBlocks/BuildingBlocks.EventBus/BuildingBlocks.EventBus.csproj", "src/BuildingBlocks/BuildingBlocks.EventBus/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj", "src/BuildingBlocks/BuildingBlocks.Common/"]

RUN dotnet restore "src/Services/Exam/Exam.csproj"

# Copy source code and build
COPY src/ src/
WORKDIR "/src/src/Services/Exam"
RUN dotnet build "Exam.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Exam.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Exam.dll"]
