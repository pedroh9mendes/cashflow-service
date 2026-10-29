const rotulosStatus = {
  Operacional: { texto: 'Operacional', classe: 'badge-operacional' },
  Degradado: { texto: 'Degradado', classe: 'badge-degradado' },
  Indisponivel: { texto: 'Indisponível', classe: 'badge-indisponivel' }
};

async function carregarStatus() {
  const grid = document.getElementById('grid-status');
  const resumoIcone = document.getElementById('resumo-icone');
  const resumoTitulo = document.getElementById('resumo-titulo');
  const resumoLegenda = document.getElementById('resumo-legenda');
  const atualizacao = document.getElementById('atualizacao');

  try {
    const resposta = await fetch('/api/status');
    const dados = await resposta.json();

    grid.innerHTML = dados.map(item => {
      const info = rotulosStatus[item.status] || { texto: item.status, classe: 'badge-indisponivel' };
      return `
        <div class="card-status">
          <span class="status-nome">${item.servico}</span>
          <span class="status-badge ${info.classe}">${info.texto}</span>
        </div>
      `;
    }).join('');

    const todosOperacionais = dados.every(item => item.status === 'Operacional');
    const algumIndisponivel = dados.some(item => item.status === 'Indisponivel');

    if (todosOperacionais) {
      resumoIcone.textContent = '✅';
      resumoTitulo.textContent = 'Todos os serviços estão operacionais';
      resumoLegenda.textContent = 'Nenhuma instabilidade identificada no momento.';
    } else if (algumIndisponivel) {
      resumoIcone.textContent = '⛔';
      resumoTitulo.textContent = 'Um ou mais serviços estão indisponíveis';
      resumoLegenda.textContent = 'A equipe responsável já tem visibilidade sobre isso. Consulte os detalhes abaixo.';
    } else {
      resumoIcone.textContent = '⚠️';
      resumoTitulo.textContent = 'Degradação parcial identificada';
      resumoLegenda.textContent = 'Alguns serviços podem responder mais lentamente que o normal.';
    }

    atualizacao.textContent = 'Última verificação: ' + new Date().toLocaleTimeString('pt-BR');
  } catch {
    grid.innerHTML = '<div class="card-status"><span class="status-nome">Não foi possível consultar o status no momento.</span></div>';
    resumoIcone.textContent = '❔';
    resumoTitulo.textContent = 'Status indisponível';
    resumoLegenda.textContent = 'Não foi possível conectar ao serviço de status.';
  }
}

carregarStatus();
setInterval(carregarStatus, 15000);
