FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/StatusPage.Api/StatusPage.Api.csproj", "src/StatusPage.Api/"]
RUN dotnet restore "src/StatusPage.Api/StatusPage.Api.csproj"

COPY ["tools/Seed.ConsoleApp/Seed.ConsoleApp.csproj", "tools/Seed.ConsoleApp/"]
RUN dotnet restore "tools/Seed.ConsoleApp/Seed.ConsoleApp.csproj"

COPY . .
RUN dotnet publish "src/StatusPage.Api/StatusPage.Api.csproj" -c Release -o /app/publish
RUN dotnet publish "tools/Seed.ConsoleApp/Seed.ConsoleApp.csproj" -c Release -o /app/seed

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/seed ./seed
COPY --from=build /src/database/scripts ./database-scripts
COPY --from=build /src/docs ./docs-root/docs
COPY --from=build /src/README.md ./docs-root/README.md

ENTRYPOINT ["dotnet", "StatusPage.Api.dll"]
