FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Lancamentos.Api/Lancamentos.Api.csproj", "src/Lancamentos.Api/"]
COPY ["src/Lancamentos.Domain/Lancamentos.Domain.csproj", "src/Lancamentos.Domain/"]
COPY ["src/Lancamentos.Infrastructure/Lancamentos.Infrastructure.csproj", "src/Lancamentos.Infrastructure/"]
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
COPY ["src/Shared.Security/Shared.Security.csproj", "src/Shared.Security/"]
RUN dotnet restore "src/Lancamentos.Api/Lancamentos.Api.csproj"

COPY . .
RUN dotnet publish "src/Lancamentos.Api/Lancamentos.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Lancamentos.Api.dll"]
