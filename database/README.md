# Scripts de Banco de Dados

Scripts SQL para provisionar manualmente as tabelas do sistema contra uma instância de SQL Server local, sem depender de `docker-compose up` ou da aplicação `.NET` aplicar as migrations automaticamente no startup.

Os scripts de criação de tabela (`001` e `002`) foram gerados diretamente a partir das migrations reais do Entity Framework Core (`dotnet ef migrations script --idempotent`), garantindo que o schema criado é idêntico ao que a aplicação espera encontrar — não é um DDL escrito à mão, é o SQL que o próprio `DatabaseInitializer` de cada serviço executa automaticamente no container.

## Ordem de execução

Execute os scripts nesta ordem, contra o SQL Server de destino (`localhost`, porta `1433` por padrão):

1. **`001_lancamentos_create_tables.sql`** — cria o banco `LancamentosDb` (tabelas `Lancamentos` e `OutboxMessages`). Execute com o contexto de conexão apontando para `LancamentosDb` (crie o banco antes se ele ainda não existir: `CREATE DATABASE LancamentosDb;`).
2. **`002_consolidado_create_tables.sql`** — cria o banco `ConsolidadoDb` (tabelas `SaldosDiarios` e `EventosProcessados`). Execute com o contexto de conexão apontando para `ConsolidadoDb` (crie o banco antes: `CREATE DATABASE ConsolidadoDb;`).
3. **`003_lancamentos_row_level_security.sql`** — aplica a política de Row-Level Security sobre `LancamentosDb` (ver [ADR-006](../docs/adr/ADR-006-multi-tenancy.md)). Execute depois do passo 1, no mesmo banco.
4. **`004_consolidado_row_level_security.sql`** — aplica a política de Row-Level Security sobre `ConsolidadoDb`. Execute depois do passo 2, no mesmo banco.

Todos os scripts são idempotentes — podem ser executados mais de uma vez sem erro, pois verificam a existência de objetos antes de criá-los.

## Exemplo com sqlcmd

```bash
sqlcmd -S localhost -U sa -P "<senha>" -Q "CREATE DATABASE LancamentosDb;"
sqlcmd -S localhost -U sa -P "<senha>" -d LancamentosDb -i database/scripts/001_lancamentos_create_tables.sql
sqlcmd -S localhost -U sa -P "<senha>" -d LancamentosDb -i database/scripts/003_lancamentos_row_level_security.sql

sqlcmd -S localhost -U sa -P "<senha>" -Q "CREATE DATABASE ConsolidadoDb;"
sqlcmd -S localhost -U sa -P "<senha>" -d ConsolidadoDb -i database/scripts/002_consolidado_create_tables.sql
sqlcmd -S localhost -U sa -P "<senha>" -d ConsolidadoDb -i database/scripts/004_consolidado_row_level_security.sql
```

## Quando isso é necessário

No fluxo normal via `docker-compose up` (ver [README principal](../README.md)), nenhum desses scripts precisa ser executado manualmente — cada serviço aplica suas próprias migrations e o script de RLS correspondente automaticamente no startup (`DatabaseInitializer`). Esta pasta existe para quem quiser inspecionar o schema exato sem subir a aplicação, provisionar um ambiente de banco gerenciado separadamente (ex: Azure SQL) antes do deploy dos serviços, ou auditar o schema como parte da documentação de arquitetura.

## Dados de exemplo (seed)

Para popular o ambiente com dados de demonstração (carteira de comerciantes, lançamentos distribuídos ao longo de 30 dias, incluindo dias de pico de volume para visualizar o card de saturação no Grafana), veja [`tools/Seed.ConsoleApp`](../tools/Seed.ConsoleApp/) — um programa que usa as APIs reais (não inserção direta no banco), exercitando o fluxo completo de Outbox → RabbitMQ → projeção de saldo consolidado.
