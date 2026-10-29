// ===== Etapa 4: revelar/ocultar valores de credenciais padrão =====

function alternarVisibilidade(botao) {
  const codigo = botao.previousElementSibling;
  const mascarado = codigo.textContent.startsWith('•');

  if (mascarado) {
    codigo.textContent = codigo.dataset.valor;
    botao.textContent = '🙈';
    botao.setAttribute('aria-label', 'Ocultar valor');
  } else {
    codigo.textContent = '•'.repeat(Math.min(codigo.dataset.valor.length, 40));
    botao.textContent = '👁';
    botao.setAttribute('aria-label', 'Mostrar valor');
  }
}

// ===== Checagem inicial: a instalação assistida só faz sentido com o ambiente totalmente
// offline. Se o painel já estiver respondendo, pula direto para a tela "já está rodando". =====

const TOTAL_ETAPAS = 8;
let etapaAtual = 1;
let tipoAmbiente = 'local';

async function checarAmbienteJaRodando() {
  const paginaChecando = document.getElementById('pagina-checando');
  const paginaJaRodando = document.getElementById('pagina-ja-rodando');
  const app = document.getElementById('app');

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 2500);

    const resposta = await fetch('http://localhost:5100/health', {
      signal: controller.signal,
      mode: 'cors'
    });
    clearTimeout(timeout);

    if (resposta.ok) {
      paginaChecando.style.display = 'none';
      paginaJaRodando.style.display = 'flex';
      return;
    }

    iniciarQuiz();
  } catch {
    // Falha de rede/timeout/CORS == nada respondendo em localhost:5100 == ambiente offline,
    // exatamente a condição para liberar o quiz.
    iniciarQuiz();
  }
}

function iniciarQuiz() {
  document.getElementById('pagina-checando').style.display = 'none';
  document.getElementById('app').style.display = 'block';
  irParaEtapa(1);
}

function forcarQuiz() {
  document.getElementById('pagina-ja-rodando').style.display = 'none';
  iniciarQuiz();
}

checarAmbienteJaRodando();

// ===== Navegação entre etapas =====

function irParaEtapa(numero) {
  document.querySelectorAll('.etapa').forEach(el => {
    el.classList.toggle('ativa', Number(el.dataset.etapa) === numero);
  });

  etapaAtual = numero;
  atualizarProgresso();
  window.scrollTo({ top: 0, behavior: 'smooth' });

  if (numero === 5 && typeof atualizarComandoSubida === 'function') {
    atualizarComandoSubida();
  }

  if (numero === 6 && typeof verificarServicos === 'function') {
    verificarServicos();
  }
}

function atualizarProgresso() {
  const percentual = (etapaAtual / TOTAL_ETAPAS) * 100;
  document.getElementById('progresso-barra').style.width = percentual + '%';
  document.getElementById('progresso-rotulo').textContent = `Etapa ${etapaAtual} de ${TOTAL_ETAPAS}`;
}

atualizarProgresso();

// ===== Etapa 2: pré-requisitos =====

function atualizarPrereq() {
  const checkboxes = document.querySelectorAll('.chk-prereq');
  const todosMarcados = Array.from(checkboxes).every(c => c.checked);
  document.getElementById('btn-avancar-prereq').disabled = !todosMarcados;
}

// ===== Etapa 3: tipo de ambiente =====

function atualizarTipoAmbiente() {
  tipoAmbiente = document.querySelector('input[name="tipo-ambiente"]:checked').value;
  document.getElementById('aviso-servidor').style.display = tipoAmbiente === 'servidor' ? 'block' : 'none';

  // A escolha de ambiente aqui informa o texto da etapa de credenciais mais à frente:
  // um servidor remoto nunca deveria manter as credenciais padrão do repositório.
  const chkDev = document.getElementById('chk-ambiente-dev');
  if (tipoAmbiente === 'servidor' && chkDev.checked) {
    chkDev.checked = false;
    atualizarCredenciais();
  }
}

// ===== Etapa 4: credenciais e variável de produção =====

function atualizarCredenciais() {
  const chkDev = document.getElementById('chk-ambiente-dev');
  const avisoProducao = document.getElementById('aviso-producao');

  const tratarComoProducao = !chkDev.checked || tipoAmbiente === 'servidor';
  avisoProducao.style.display = tratarComoProducao ? 'block' : 'none';
}

// ===== Etapa 5: comando de subida, ajustado conforme o tipo de ambiente =====

function atualizarComandoSubida() {
  const comando = document.getElementById('comando-subir');
  const chkDev = document.getElementById('chk-ambiente-dev');
  const tratarComoProducao = !chkDev.checked || tipoAmbiente === 'servidor';

  comando.textContent = tratarComoProducao
    ? 'Orchestration__AmbienteProducao=true docker compose up -d --build'
    : 'docker compose up -d --build';
}

// ===== Copiar comando =====

function copiar(idElemento) {
  const texto = document.getElementById(idElemento).textContent;
  navigator.clipboard.writeText(texto);
}

// ===== Etapa 6: verificação de saúde dos serviços =====

const servicosVerificacao = {
  painel: 'http://localhost:5100/health',
  lancamentos: 'http://localhost:5080/health',
  consolidado: 'http://localhost:5090/health'
};

async function verificarServicos() {
  const botao = document.getElementById('btn-verificar');
  const btnAvancar = document.getElementById('btn-avancar-verificacao');
  botao.disabled = true;
  const textoOriginal = botao.textContent;
  botao.textContent = 'Verificando…';

  let todosOperacionais = true;

  for (const [servico, url] of Object.entries(servicosVerificacao)) {
    const item = document.querySelector(`.verificacao-item[data-servico="${servico}"] .verificacao-status`);
    item.className = 'verificacao-status status-verificando';
    item.textContent = 'Verificando…';

    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 3000);
      const resposta = await fetch(url, { signal: controller.signal, mode: 'cors' });
      clearTimeout(timeout);

      if (resposta.ok) {
        item.className = 'verificacao-status status-ok';
        item.textContent = 'Operacional';
      } else {
        item.className = 'verificacao-status status-falha';
        item.textContent = 'Indisponível';
        todosOperacionais = false;
      }
    } catch {
      item.className = 'verificacao-status status-falha';
      item.textContent = 'Indisponível';
      todosOperacionais = false;
    }
  }

  botao.disabled = false;
  botao.textContent = textoOriginal;
  btnAvancar.textContent = todosOperacionais ? 'Avançar' : 'Avançar mesmo assim';
}
