# Referência de API

Contratos dos endpoints principais das APIs de negócio. Todos exigem um token JWT Bearer, exceto quando indicado.

## Autenticação

```bash
POST /dev/token
Content-Type: application/json

{"comercianteId": "11111111-1111-1111-1111-111111111111"}
```

Retorna um `token` JWT com o `ComercianteId` informado embutido como claim. Use-o no cabeçalho `Authorization: Bearer <token>` em todas as chamadas às APIs abaixo. Endpoint disponível apenas em ambiente de desenvolvimento (`http://localhost:5080/dev/token`).

## Lançamentos API (`http://localhost:5080`)

### Criar um lançamento

```bash
POST /api/lancamentos
Authorization: Bearer <token>
Content-Type: application/json

{
  "tipo": 1,
  "valor": 250.00,
  "descricao": "Venda do dia",
  "data": "2026-10-28"
}
```

`tipo: 1` = Crédito, `tipo: 2` = Débito. Retorna `201 Created` com o lançamento persistido.

### Listar lançamentos de um dia

```bash
GET /api/lancamentos?data=2026-10-28
Authorization: Bearer <token>
```

Retorna os lançamentos do comerciante autenticado (identificado pelo claim do token) na data informada.

### Reconciliar lançamentos pendentes (persistência local na origem)

```bash
POST /api/lancamentos/reconciliar
Authorization: Bearer <token>
Content-Type: application/json

{
  "lancamentos": [
    {
      "idOrigem": "guid-gerado-na-origem",
      "tipo": 1,
      "valor": 250.00,
      "descricao": "Venda registrada offline",
      "data": "2026-10-28"
    }
  ]
}
```

Aceita um lote de lançamentos identificados por `idOrigem` (gerado fora do servidor, tipicamente no dispositivo do comerciante). Reenviar o mesmo lote mais de uma vez não duplica os lançamentos já processados.

## Consolidado API (`http://localhost:5090`)

### Consultar saldo diário consolidado

```bash
GET /api/consolidado/saldo-diario?data=2026-10-28
Authorization: Bearer <token>
```

Retorna créditos, débitos e saldo final do comerciante autenticado na data informada. A leitura é direta sobre uma projeção pré-calculada, não recalcula nada em tempo de requisição.

Pode haver um atraso de alguns segundos entre o registro de um lançamento e seu reflexo nesta consulta, enquanto o evento correspondente é processado de forma assíncrona.

Requisições acima do limite de rate limiting por tenant retornam `429 Too Many Requests`.

## Status e saúde

```bash
GET /health
```

Disponível em cada serviço (`5080`, `5090`, `5100`), sem autenticação. Reflete estado agregado real (conectividade com banco, fila, estado do circuit breaker quando aplicável).

```bash
GET /status
```

Página pública de status, sem autenticação, disponível em `http://localhost:5100/status`.

## Isolamento entre comerciantes

Em todo endpoint acima, o `ComercianteId` usado é sempre extraído do claim do token validado no servidor, nunca de um parâmetro informado pelo cliente. Um comerciante autenticado não consegue acessar, listar ou modificar dados de outro comerciante, mesmo manipulando parâmetros da requisição.

Exemplo de verificação manual:

```bash
TOKEN_A=$(curl -s -X POST http://localhost:5080/dev/token \
  -H "Content-Type: application/json" \
  -d '{"comercianteId": "11111111-1111-1111-1111-111111111111"}' | jq -r .token)

TOKEN_B=$(curl -s -X POST http://localhost:5080/dev/token \
  -H "Content-Type: application/json" \
  -d '{"comercianteId": "22222222-2222-2222-2222-222222222222"}' | jq -r .token)

curl -X POST http://localhost:5080/api/lancamentos \
  -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d '{"tipo": 1, "valor": 500.00, "descricao": "Lançamento do comerciante A", "data": "2026-10-28"}'

# O comerciante B não vê nada do comerciante A
curl "http://localhost:5080/api/lancamentos?data=2026-10-28" -H "Authorization: Bearer $TOKEN_B"
```
