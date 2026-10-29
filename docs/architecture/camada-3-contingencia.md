# Persistência Local na Origem

Terceira camada de resiliência, cobrindo o cenário em que tanto o ambiente primário quanto o ambiente espelho do serviço de Lançamentos ficam inacessíveis simultaneamente para um determinado comerciante.

## Como funciona

1. **Registro local append-only.** O ponto de origem do lançamento mantém uma store local (SQLite na prova de conceito) onde cada lançamento é gravado imediatamente ao ser criado, antes de qualquer tentativa de envio ao backend.

2. **Identificador idempotente gerado na origem.** Cada lançamento recebe um GUID gerado no próprio ponto de origem, não pelo backend. Se o mesmo lançamento for enviado mais de uma vez, o backend reconhece o identificador já processado e descarta o duplicado.

3. **Tentativa de envio imediata, com fallback automático ao modo local.** Se o envio falhar (timeout, erro de conectividade, resposta 5xx sustentada), o lançamento permanece marcado como "pendente de sincronização" na store local, e o usuário recebe confirmação de que o lançamento foi registrado.

4. **Reconciliação ao reconectar.** Quando a conectividade é restabelecida, um processo de sincronização varre os lançamentos pendentes, em ordem cronológica, e os envia via `POST /api/lancamentos/reconciliar`, que aplica a regra de idempotência por `IdOrigem`.

5. **Sem acúmulo indefinido em fila remota.** Os lançamentos pendentes ficam retidos na origem, não em uma fila do backend — o crescimento fica limitado ao volume de um único comerciante durante o período de indisponibilidade.

## Uso durante migração de dados legados

Quando um comerciante possui um sistema anterior de controle de caixa, o mesmo componente de contingência local pode atuar como ponte durante a migração: alimentando tanto o sistema anterior quanto a nova plataforma a partir do mesmo registro local (ver `docs/transition-architecture.md`).

## Implementação

- Um cliente de exemplo (`src/Contingencia.ClienteExemplo`) grava lançamentos em SQLite local antes de tentar o envio.
- O endpoint de reconciliação (`POST /api/lancamentos/reconciliar`) no serviço de Lançamentos aceita lotes de lançamentos com identificador de origem e aplica verificação de idempotência antes de persistir.

Para testar:

```bash
cd src/Contingencia.ClienteExemplo
dotnet run -- http://localhost:5080 <token_jwt>
```

Com o backend indisponível, o lançamento é aceito e persistido localmente sem erro; ao reconectar, é sincronizado sem duplicação.
