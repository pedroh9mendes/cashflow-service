FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Consolidado.Api/Consolidado.Api.csproj", "src/Consolidado.Api/"]
COPY ["src/Consolidado.Domain/Consolidado.Domain.csproj", "src/Consolidado.Domain/"]
COPY ["src/Consolidado.Infrastructure/Consolidado.Infrastructure.csproj", "src/Consolidado.Infrastructure/"]
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
COPY ["src/Shared.Security/Shared.Security.csproj", "src/Shared.Security/"]
RUN dotnet restore "src/Consolidado.Api/Consolidado.Api.csproj"

COPY . .
RUN dotnet publish "src/Consolidado.Api/Consolidado.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Consolidado.Api.dll"]
