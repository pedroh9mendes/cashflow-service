// Se já existe um token válido salvo, pula direto para o painel.
const tokenExistente = localStorage.getItem('painel_token');
if (tokenExistente) {
  window.location.replace('/');
}

document.getElementById('form-login').addEventListener('submit', async (evento) => {
  evento.preventDefault();

  const usuario = document.getElementById('input-usuario').value.trim();
  const senha = document.getElementById('input-senha').value;
  const botao = document.getElementById('btn-entrar');
  const erro = document.getElementById('login-erro');

  erro.classList.remove('visivel');
  botao.disabled = true;
  const textoOriginal = botao.textContent;
  botao.textContent = 'Entrando…';

  try {
    const resposta = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ usuario, senha })
    });

    if (!resposta.ok) {
      throw new Error('Credenciais inválidas');
    }

    const dados = await resposta.json();
    localStorage.setItem('painel_token', dados.token);
    localStorage.setItem('painel_token_expira', dados.expiraEm);
    window.location.replace('/');
  } catch {
    erro.classList.add('visivel');
    botao.disabled = false;
    botao.textContent = textoOriginal;
  }
});
