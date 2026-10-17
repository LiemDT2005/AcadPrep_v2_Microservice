# Multi-stage build for Exam.API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for caching dotnet restore
COPY ["src/Services/Exam/Exam.API/Exam.API.csproj", "src/Services/Exam/Exam.API/"]
COPY ["src/Services/Exam/Exam.Application/Exam.Application.csproj", "src/Services/Exam/Exam.Application/"]
COPY ["src/Services/Exam/Exam.Domain/Exam.Domain.csproj", "src/Services/Exam/Exam.Domain/"]
COPY ["src/Services/Exam/Exam.Infrastructure/Exam.Infrastructure.csproj", "src/Services/Exam/Exam.Infrastructure/"]
COPY ["src/BuildingBlocks/BuildingBlocks.EventBus/BuildingBlocks.EventBus.csproj", "src/BuildingBlocks/BuildingBlocks.EventBus/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj", "src/BuildingBlocks/BuildingBlocks.Common/"]

RUN dotnet restore "src/Services/Exam/Exam.API/Exam.API.csproj"

# Copy source code and build
COPY src/ src/
WORKDIR "/src/src/Services/Exam/Exam.API"
RUN dotnet build "Exam.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Exam.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Exam.API.dll"]
