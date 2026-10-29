// ===== Autenticação do painel =====
//
// O painel inteiro (exceto /status, a página pública, e /api/status, sua fonte de dados) exige um
// token de administrador emitido por POST /api/auth/login (ver login.html). O token fica em
// localStorage e é anexado a toda chamada autenticada por fetchAutenticado. O backend também
// valida o token em cada endpoint protegido - esta checagem no cliente é só para não deixar o
// operador navegando numa tela vazia enquanto o backend devolve 401 chamada por chamada.

if (!localStorage.getItem('painel_token')) {
  window.location.replace('/login.html');
}

async function fetchAutenticado(url, opcoes = {}) {
  const token = localStorage.getItem('painel_token');
  const resposta = await fetch(url, {
    ...opcoes,
    headers: {
      ...(opcoes.headers || {}),
      Authorization: `Bearer ${token}`
    }
  });

  if (resposta.status === 401) {
    localStorage.removeItem('painel_token');
    localStorage.removeItem('painel_token_expira');
    window.location.replace('/login.html');
    throw new Error('Sessão expirada');
  }

  return resposta;
}

function sair() {
  localStorage.removeItem('painel_token');
  localStorage.removeItem('painel_token_expira');
  window.location.replace('/login.html');
}

// ===== Roteamento entre Painel e Documentação =====

const rotaBotoes = document.querySelectorAll('.rota-botao');
const rotas = document.querySelectorAll('.rota');
let docsCarregados = false;

rotaBotoes.forEach(botao => {
  botao.addEventListener('click', () => ativarRota(botao.dataset.rota));
});

function ativarRota(nome) {
  rotaBotoes.forEach(b => b.classList.toggle('ativa', b.dataset.rota === nome));
  rotas.forEach(r => r.classList.toggle('ativa', r.id === `rota-${nome}`));
  window.scrollTo({ top: 0, behavior: 'instant' });

  if (nome === 'docs' && !docsCarregados) {
    carregarListaDocumentos();
  }

  const url = new URL(window.location);
  if (nome === 'docs') {
    url.hash = 'docs';
  } else {
    url.hash = '';
  }
  window.history.replaceState({}, '', url);
}

function irParaDocumentacao() {
  ativarRota('docs');
}

function irParaPainel() {
  ativarRota('painel');
}

if (window.location.hash === '#docs') {
  ativarRota('docs');
}

// ===== Diagrama de fluxo (primeira página da documentação) =====

const templateFluxoDados = `
  <h1>Como um lançamento vira saldo consolidado</h1>
  <p>Fluxo real de ponta a ponta: do registro de um débito ou crédito até a projeção de saldo diário aparecer na consulta do comerciante — passando pelos três pontos de resiliência da arquitetura.</p>

  <div class="fluxo-diagrama" id="fluxo-diagrama">
    <div class="fluxo-no" data-no="cliente">
      <span class="fluxo-no-icone">🏪</span>
      <span class="fluxo-no-titulo">Comerciante</span>
      <span class="fluxo-no-desc">Envia POST /api/lancamentos</span>
    </div>
    <div class="fluxo-seta"></div>
    <div class="fluxo-no" data-no="lancamentos">
      <span class="fluxo-no-icone">🧾</span>
      <span class="fluxo-no-titulo">Lançamentos API</span>
      <span class="fluxo-no-desc">Grava lançamento + evento Outbox na mesma transação</span>
    </div>
    <div class="fluxo-seta"></div>
    <div class="fluxo-no" data-no="outbox">
      <span class="fluxo-no-icone">📬</span>
      <span class="fluxo-no-titulo">Outbox Publisher</span>
      <span class="fluxo-no-desc">Publica o evento de forma confiável, mesmo sob falha momentânea do broker</span>
    </div>
    <div class="fluxo-seta"></div>
    <div class="fluxo-no" data-no="fila">
      <span class="fluxo-no-icone">🐇</span>
      <span class="fluxo-no-titulo">RabbitMQ</span>
      <span class="fluxo-no-desc">Fila durável com dead-letter para falhas de consumo</span>
    </div>
    <div class="fluxo-seta"></div>
    <div class="fluxo-no" data-no="consolidado">
      <span class="fluxo-no-icone">📊</span>
      <span class="fluxo-no-titulo">Consolidado API</span>
      <span class="fluxo-no-desc">Consome de forma idempotente e atualiza a projeção de saldo</span>
    </div>
    <div class="fluxo-seta"></div>
    <div class="fluxo-no" data-no="saldo">
      <span class="fluxo-no-icone">✅</span>
      <span class="fluxo-no-titulo">Saldo disponível</span>
      <span class="fluxo-no-desc">GET /api/consolidado/saldo-diario já reflete o novo lançamento</span>
    </div>
  </div>

  <div class="fluxo-legenda">
    <span class="fluxo-pulso-dot"></span> Acompanhe o pulso percorrendo o fluxo — é a mesma trajetória de um lançamento real, de ponta a ponta.
  </div>
`;

let intervalFluxoDados = null;

function iniciarAnimacaoFluxo() {
  pararAnimacaoFluxo();

  const nos = document.querySelectorAll('#fluxo-diagrama .fluxo-no');
  if (nos.length === 0) {
    return;
  }

  let indice = 0;
  nos.forEach(no => no.classList.remove('fluxo-no-ativo'));

  intervalFluxoDados = setInterval(() => {
    nos.forEach(no => no.classList.remove('fluxo-no-ativo'));
    nos[indice].classList.add('fluxo-no-ativo');
    indice = (indice + 1) % nos.length;
  }, 1100);
}

function pararAnimacaoFluxo() {
  if (intervalFluxoDados) {
    clearInterval(intervalFluxoDados);
    intervalFluxoDados = null;
  }
}

// ===== Status dos serviços =====

const statusInfo = {
  Operacional: { icone: '✅', classe: 'status-operacional', texto: 'Operacional' },
  Degradado: { icone: '⚠️', classe: 'status-degradado', texto: 'Degradado' },
  Indisponivel: { icone: '⛔', classe: 'status-indisponivel', texto: 'Indisponível' }
};

async function carregarStatus() {
  const container = document.getElementById('status-grid');
  try {
    const resposta = await fetch('/api/status');
    const dados = await resposta.json();

    container.innerHTML = dados.map(item => {
      const info = statusInfo[item.status] || { icone: '❔', classe: 'status-indisponivel', texto: item.status };
      return `
        <div class="card status-card ${info.classe}">
          <div class="status-card-topo">
            <span class="status-dot"></span>
            <span class="status-icone">${info.icone}</span>
          </div>
          <span class="status-nome">${item.servico}</span>
          <span class="status-texto">${info.texto}</span>
        </div>
      `;
    }).join('');
  } catch {
    container.innerHTML = '<div class="card"><span>Não foi possível obter o status no momento.</span></div>';
  }
}

carregarStatus();
setInterval(carregarStatus, 15000);

// ===== Métricas de volume de processamento e saúde operacional =====

const formatadorNumero = new Intl.NumberFormat('pt-BR');
const formatadorDecimal = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 });

function formatarLatencia(valorMs) {
  return valorMs === null || valorMs === undefined ? '—' : `${formatadorDecimal.format(valorMs)} ms`;
}

function formatarTaxa(valor) {
  return valor === null || valor === undefined ? '—' : `${formatadorDecimal.format(valor)} req/s`;
}

async function carregarMetricas() {
  const container = document.getElementById('metricas-grid');
  try {
    const resposta = await fetchAutenticado('/api/metricas');
    const m = await resposta.json();

    if (!m.disponivel) {
      container.innerHTML = '<div class="card"><span>Métricas indisponíveis no momento — serviços de negócio podem estar inicializando.</span></div>';
      return;
    }

    const classeCircuito = m.estadoCircuitBreaker === 'Contingencia' ? 'valor-alerta' : '';
    const classeOutbox = m.outboxPendentes > 20 ? 'valor-alerta' : '';

    container.innerHTML = `
      <div class="card metrica-card">
        <span class="metrica-icone">🧾</span>
        <span class="metrica-valor">${formatadorNumero.format(m.lancamentosHoje)}</span>
        <span class="metrica-rotulo">Lançamentos hoje</span>
        <span class="metrica-legenda">${formatadorNumero.format(m.lancamentosTotal)} processados no histórico</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">🏪</span>
        <span class="metrica-valor">${formatadorNumero.format(m.comerciantesAtivos)}</span>
        <span class="metrica-rotulo">Comerciantes ativos</span>
        <span class="metrica-legenda">com ao menos 1 lançamento</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">⚡</span>
        <span class="metrica-valor">${formatarTaxa(m.requisicoesPorSegundo)}</span>
        <span class="metrica-rotulo">Vazão atual</span>
        <span class="metrica-legenda">requisições/s, média dos últimos 5min</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">⏱️</span>
        <span class="metrica-valor">${formatarLatencia(m.latenciaMediaLancamentosMs)}</span>
        <span class="metrica-rotulo">Tempo médio — Lançamentos</span>
        <span class="metrica-legenda">latência média de resposta</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">⏱️</span>
        <span class="metrica-valor">${formatarLatencia(m.latenciaMediaConsolidadoMs)}</span>
        <span class="metrica-rotulo">Tempo médio — Consolidado</span>
        <span class="metrica-legenda">latência média de resposta</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">📊</span>
        <span class="metrica-valor">${formatadorNumero.format(m.comerciantesComSaldoHoje)}</span>
        <span class="metrica-rotulo">Projeções atualizadas hoje</span>
        <span class="metrica-legenda">${formatadorNumero.format(m.projecoesTotal)} no histórico</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">🔁</span>
        <span class="metrica-valor">${formatadorNumero.format(m.eventosProcessadosTotal)}</span>
        <span class="metrica-rotulo">Eventos processados</span>
        <span class="metrica-legenda">consumidor da fila, idempotente</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">📬</span>
        <span class="metrica-valor ${classeOutbox}">${formatadorNumero.format(m.outboxPendentes)}</span>
        <span class="metrica-rotulo">Outbox pendente</span>
        <span class="metrica-legenda">eventos aguardando publicação</span>
      </div>
      <div class="card metrica-card">
        <span class="metrica-icone">🛡️</span>
        <span class="metrica-valor ${classeCircuito}">${m.estadoCircuitBreaker === 'Contingencia' ? 'Contingência' : 'Normal'}</span>
        <span class="metrica-rotulo">Circuit breaker</span>
        <span class="metrica-legenda">modo de operação do banco primário</span>
      </div>
    `;
  } catch {
    container.innerHTML = '<div class="card"><span>Não foi possível obter as métricas no momento.</span></div>';
  }
}

carregarMetricas();
setInterval(carregarMetricas, 15000);

// ===== Gráficos de tendência e distribuição =====

let graficoTendencia = null;
let graficoDistribuicao = null;

async function carregarGraficos() {
  try {
    const resposta = await fetchAutenticado('/api/metricas/serie-diaria');
    const dados = await resposta.json();
    const pontos = dados.pontos || [];

    const rotulos = pontos.map(p => new Date(p.data + 'T00:00:00').toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }));
    const creditos = pontos.map(p => p.creditos);
    const debitos = pontos.map(p => p.debitos);

    renderizarGraficoTendencia(rotulos, creditos, debitos);
    renderizarGraficoDistribuicao(
      creditos.reduce((soma, v) => soma + v, 0),
      debitos.reduce((soma, v) => soma + v, 0)
    );
  } catch {
    // Gráficos são complementares aos cards numéricos — uma falha aqui não deve quebrar o painel.
  }
}

function renderizarGraficoTendencia(rotulos, creditos, debitos) {
  const canvas = document.getElementById('grafico-tendencia');
  if (!canvas || typeof Chart === 'undefined') {
    return;
  }

  if (graficoTendencia) {
    graficoTendencia.data.labels = rotulos;
    graficoTendencia.data.datasets[0].data = creditos;
    graficoTendencia.data.datasets[1].data = debitos;
    graficoTendencia.update();
    return;
  }

  graficoTendencia = new Chart(canvas, {
    type: 'line',
    data: {
      labels: rotulos,
      datasets: [
        {
          label: 'Créditos (qtd.)',
          data: creditos,
          borderColor: '#2b4b9b',
          backgroundColor: 'rgba(43, 75, 155, 0.12)',
          fill: true,
          tension: 0.35,
          pointRadius: 2
        },
        {
          label: 'Débitos (qtd.)',
          data: debitos,
          borderColor: '#1facdd',
          backgroundColor: 'rgba(31, 172, 221, 0.12)',
          fill: true,
          tension: 0.35,
          pointRadius: 2
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 11 } } }
      },
      scales: {
        y: { beginAtZero: true, ticks: { precision: 0 } }
      }
    }
  });
}

function renderizarGraficoDistribuicao(totalCreditos, totalDebitos) {
  const canvas = document.getElementById('grafico-distribuicao');
  if (!canvas || typeof Chart === 'undefined') {
    return;
  }

  if (graficoDistribuicao) {
    graficoDistribuicao.data.datasets[0].data = [totalCreditos, totalDebitos];
    graficoDistribuicao.update();
    return;
  }

  graficoDistribuicao = new Chart(canvas, {
    type: 'doughnut',
    data: {
      labels: ['Créditos', 'Débitos'],
      datasets: [{
        data: [totalCreditos, totalDebitos],
        backgroundColor: ['#2b4b9b', '#1facdd'],
        borderWidth: 0
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 11 } } }
      },
      cutout: '65%'
    }
  });
}

carregarGraficos();
setInterval(carregarGraficos, 15000);

// ===== Ações de orquestração =====

const execucoesEmAndamento = {};
let modalContexto = null;
let orchestrationDefaults = null;
let orquestracaoBloqueada = false;

async function carregarBloqueioOrquestracao() {
  const avisoSql = document.getElementById('aviso-bloqueio-sql');
  const avisoSeed = document.getElementById('aviso-bloqueio-seed');
  const btnSql = document.getElementById('btn-sql');
  const btnSeed = document.getElementById('btn-seed');

  try {
    const resposta = await fetchAutenticado('/api/orchestration/bloqueio');
    const bloqueio = await resposta.json();
    orquestracaoBloqueada = bloqueio.bloqueado;

    [avisoSql, avisoSeed].forEach(aviso => {
      aviso.style.display = bloqueio.bloqueado ? 'block' : 'none';
      aviso.textContent = bloqueio.motivo || '';
    });

    btnSql.disabled = bloqueio.bloqueado;
    btnSeed.disabled = bloqueio.bloqueado;
  } catch {
    // Se a checagem falhar, mantém os botões no último estado conhecido em vez de travar a UI.
  }
}

carregarBloqueioOrquestracao();
setInterval(carregarBloqueioOrquestracao, 15000);

async function abrirModalConfirmacao(acao, idBotao, idLog) {
  if (execucoesEmAndamento[acao] || orquestracaoBloqueada) {
    return;
  }

  modalContexto = { acao, idBotao, idLog };

  const titulos = {
    'apply-sql-scripts': 'Confirmar ambiente — Aplicar scripts de banco',
    'seed': 'Confirmar ambiente — Popular dados de demonstração'
  };
  document.getElementById('modal-titulo').textContent = titulos[acao] || 'Confirmar ambiente';

  const camposSql = document.getElementById('modal-campos-sql');
  const camposSeed = document.getElementById('modal-campos-seed');
  camposSql.style.display = acao === 'apply-sql-scripts' ? 'flex' : 'none';
  camposSeed.style.display = acao === 'seed' ? 'flex' : 'none';

  if (!orchestrationDefaults) {
    try {
      const resposta = await fetchAutenticado('/api/orchestration/defaults');
      orchestrationDefaults = await resposta.json();
    } catch {
      orchestrationDefaults = {};
    }
  }

  document.getElementById('modal-sql-connstring').value = orchestrationDefaults.sqlServerConnectionString || '';
  document.getElementById('modal-seed-lancamentos').value = orchestrationDefaults.seedLancamentosBaseUrl || '';
  document.getElementById('modal-seed-consolidado').value = orchestrationDefaults.seedConsolidadoBaseUrl || '';

  document.getElementById('modal-confirmacao').classList.add('visivel');
}

function fecharModalConfirmacao() {
  document.getElementById('modal-confirmacao').classList.remove('visivel');
  modalContexto = null;
}

async function confirmarEExecutar() {
  if (!modalContexto) {
    return;
  }

  const { acao, idBotao, idLog } = modalContexto;

  const parametros = acao === 'apply-sql-scripts'
    ? { sqlServerConnectionString: document.getElementById('modal-sql-connstring').value.trim() || null }
    : {
        seedLancamentosBaseUrl: document.getElementById('modal-seed-lancamentos').value.trim() || null,
        seedConsolidadoBaseUrl: document.getElementById('modal-seed-consolidado').value.trim() || null
      };

  fecharModalConfirmacao();
  await executarAcao(acao, idBotao, idLog, parametros);
}

async function executarAcao(acao, idBotao, idLog, parametros) {
  const botao = document.getElementById(idBotao);
  const log = document.getElementById(idLog);

  if (execucoesEmAndamento[acao]) {
    return;
  }

  botao.disabled = true;
  const textoOriginal = botao.textContent;
  botao.innerHTML = '<span class="spinner"></span> Executando…';
  log.classList.add('visivel');
  log.textContent = 'Execução iniciada…';

  try {
    const resposta = await fetchAutenticado(`/api/orchestration/${acao}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(parametros || {})
    });

    if (resposta.status === 409) {
      const corpo = await resposta.json();
      log.textContent = corpo.mensagem || 'Ação bloqueada: a aplicação está em execução.';
      botao.disabled = false;
      botao.textContent = textoOriginal;
      await carregarBloqueioOrquestracao();
      return;
    }

    execucoesEmAndamento[acao] = true;
    await monitorarExecucao(acao, botao, log, textoOriginal);
  } catch (erro) {
    log.textContent = 'Erro ao iniciar a ação: ' + erro.message;
    botao.disabled = false;
    botao.textContent = textoOriginal;
    execucoesEmAndamento[acao] = false;
  }
}

async function monitorarExecucao(acao, botao, log, textoOriginal) {
  const chave = acao === 'apply-sql-scripts' ? 'AplicarScriptsSql' : 'PopularDadosDemonstracao';

  const intervalo = setInterval(async () => {
    try {
      const resposta = await fetchAutenticado(`/api/orchestration/status/${chave}`);
      const status = await resposta.json();

      if (status.estado === 'EmExecucao') {
        return;
      }

      clearInterval(intervalo);
      execucoesEmAndamento[acao] = false;
      botao.disabled = false;
      botao.textContent = textoOriginal;

      log.textContent = status.log || '(sem saída)';

      if (status.estado === 'Falhou') {
        log.style.color = '#ff9a9a';
      } else {
        log.style.color = '';
      }
    } catch {
      clearInterval(intervalo);
      execucoesEmAndamento[acao] = false;
      botao.disabled = false;
      botao.textContent = textoOriginal;
      log.textContent = 'Não foi possível consultar o status da execução.';
    }
  }, 2000);
}

function copiarComando(comando) {
  navigator.clipboard.writeText(comando);
}

// ===== Token de desenvolvimento =====

function gerarComercianteAleatorio() {
  const uuid = crypto.randomUUID ? crypto.randomUUID() : gerarUuidFallback();
  document.getElementById('input-comerciante').value = uuid;
}

function gerarUuidFallback() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

async function obterToken() {
  const comercianteId = document.getElementById('input-comerciante').value.trim();
  const resultado = document.getElementById('token-resultado');
  const valor = document.getElementById('token-valor');

  if (!comercianteId) {
    valor.textContent = 'Informe ou gere um ComercianteId antes de solicitar o token.';
    resultado.classList.add('visivel');
    return;
  }

  try {
    const resposta = await fetch('http://localhost:5080/dev/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ comercianteId })
    });

    if (!resposta.ok) {
      throw new Error('Endpoint de token indisponível (ambiente precisa estar em Development)');
    }

    const dados = await resposta.json();
    valor.textContent = dados.token;
    resultado.classList.add('visivel');
  } catch (erro) {
    valor.textContent = 'Erro: ' + erro.message;
    resultado.classList.add('visivel');
  }
}

function copiarToken() {
  const texto = document.getElementById('token-valor').textContent;
  navigator.clipboard.writeText(texto);
}

// ===== Documentação (wiki) =====

let listaDocumentos = [];
let cacheConteudo = {};

async function carregarListaDocumentos() {
  try {
    const resposta = await fetchAutenticado('/api/docs/list');
    listaDocumentos = await resposta.json();
    renderizarSidebar(listaDocumentos);

    const params = new URLSearchParams(window.location.search);
    const docInicial = params.get('doc') || 'fluxo-dados';
    selecionarDocumento(docInicial);
  } catch {
    document.getElementById('docs-lista').innerHTML = '<p>Não foi possível carregar a documentação.</p>';
  } finally {
    docsCarregados = true;
  }
}

function renderizarSidebar(documentos) {
  const categorias = [...new Set(documentos.map(d => d.categoria))];
  const container = document.getElementById('docs-lista');

  container.innerHTML = categorias.map(categoria => `
    <div class="docs-categoria">${categoria}</div>
    ${documentos.filter(d => d.categoria === categoria).map(d => `
      <button class="docs-item" data-id="${d.id}" data-titulo="${d.titulo.toLowerCase()}"
              onclick="selecionarDocumento('${d.id}')">${d.titulo}</button>
    `).join('')}
  `).join('');
}

async function selecionarDocumento(id) {
  document.querySelectorAll('.docs-item').forEach(el => {
    el.classList.toggle('ativo', el.dataset.id === id);
  });

  const container = document.getElementById('docs-conteudo');
  pararAnimacaoFluxo();

  if (id === 'fluxo-dados') {
    container.innerHTML = templateFluxoDados;
    iniciarAnimacaoFluxo();

    const url = new URL(window.location);
    url.searchParams.set('doc', id);
    window.history.replaceState({}, '', url);
    return;
  }

  container.innerHTML = '<div class="docs-vazio">Carregando…</div>';

  try {
    let conteudo = cacheConteudo[id];

    if (!conteudo) {
      const resposta = await fetchAutenticado('/api/docs/content?id=' + encodeURIComponent(id));
      if (!resposta.ok) {
        throw new Error('Documento não encontrado');
      }
      conteudo = await resposta.text();
      cacheConteudo[id] = conteudo;
    }

    container.innerHTML = marked.parse(conteudo);

    const url = new URL(window.location);
    url.searchParams.set('doc', id);
    window.history.replaceState({}, '', url);
  } catch (erro) {
    container.innerHTML = `<div class="docs-vazio">Não foi possível carregar este documento: ${erro.message}</div>`;
  }
}

document.getElementById('docs-busca').addEventListener('input', (evento) => {
  const termo = evento.target.value.trim().toLowerCase();
  const itens = document.querySelectorAll('.docs-item');

  if (!termo) {
    itens.forEach(item => item.classList.remove('oculto'));
    document.querySelectorAll('.docs-categoria').forEach(c => c.style.display = '');
    return;
  }

  itens.forEach(item => {
    const titulo = item.dataset.titulo || '';
    const corpoCarregado = cacheConteudo[item.dataset.id] || '';
    const encontrado = titulo.includes(termo) || corpoCarregado.toLowerCase().includes(termo);
    item.classList.toggle('oculto', !encontrado);
  });

  document.querySelectorAll('.docs-categoria').forEach(categoria => {
    let proximo = categoria.nextElementSibling;
    let temVisivel = false;
    while (proximo && !proximo.classList.contains('docs-categoria')) {
      if (!proximo.classList.contains('oculto')) {
        temVisivel = true;
      }
      proximo = proximo.nextElementSibling;
    }
    categoria.style.display = temVisivel ? '' : 'none';
  });
});
