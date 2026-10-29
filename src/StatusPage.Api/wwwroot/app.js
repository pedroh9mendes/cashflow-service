// ===== Navegação entre abas =====

const abaBotoes = document.querySelectorAll('.aba-botao');
const paginas = document.querySelectorAll('.pagina');

abaBotoes.forEach(botao => {
  botao.addEventListener('click', () => ativarPagina(botao.dataset.pagina));
});

function ativarPagina(nome) {
  abaBotoes.forEach(b => b.classList.toggle('ativa', b.dataset.pagina === nome));
  paginas.forEach(p => p.classList.toggle('ativa', p.id === `pagina-${nome}`));

  if (nome === 'docs' && !docsCarregados) {
    carregarListaDocumentos();
  }
}

function irParaDocumentacao(event, docId) {
  event.preventDefault();
  ativarPagina('docs');
  selecionarDocumento(docId);
}

// ===== Status dos serviços =====

async function carregarStatus() {
  const container = document.getElementById('status-grid');
  try {
    const resposta = await fetch('/api/status');
    const dados = await resposta.json();

    container.innerHTML = dados.map(item => `
      <div class="card status-card">
        <span class="nome">${item.servico}</span>
        <span class="badge ${item.status}">${item.status}</span>
      </div>
    `).join('');

    document.getElementById('status-atualizado').textContent =
      'última verificação ' + new Date().toLocaleTimeString('pt-BR');
  } catch {
    container.innerHTML = '<div class="card"><span>Não foi possível obter o status no momento.</span></div>';
  }
}

carregarStatus();
setInterval(carregarStatus, 15000);

// ===== Ações de orquestração =====

const execucoesEmAndamento = {};

async function executarAcao(acao, idBotao, idLog) {
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
    await fetch(`/api/orchestration/${acao}`, { method: 'POST' });
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
      const resposta = await fetch(`/api/orchestration/status/${chave}`);
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

let docsCarregados = false;
let listaDocumentos = [];
let cacheConteudo = {};

async function carregarListaDocumentos() {
  try {
    const resposta = await fetch('/api/docs/list');
    listaDocumentos = await resposta.json();
    renderizarSidebar(listaDocumentos);
    docsCarregados = true;

    const params = new URLSearchParams(window.location.search);
    const docInicial = params.get('doc');
    if (docInicial) {
      selecionarDocumento(docInicial);
    }
  } catch {
    document.getElementById('docs-lista').innerHTML = '<p>Não foi possível carregar a documentação.</p>';
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
  container.innerHTML = '<div class="docs-vazio">Carregando…</div>';

  try {
    let conteudo = cacheConteudo[id];

    if (!conteudo) {
      const resposta = await fetch('/api/docs/content?id=' + encodeURIComponent(id));
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
