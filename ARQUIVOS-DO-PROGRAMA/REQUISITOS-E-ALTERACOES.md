# REQUISITOS E ALTERAÇÕES — ARMAZÉM GRATIDÃO NOVO

## Princípio
Atualizar o aplicativo existente, preservando código, módulos, funções, banco e caminho de dados. Alterar somente o que for solicitado. Aplicativo desktop nativo; sem navegador, WebView ou Electron.

## Interface solicitada (04/11/2026)
1. **Primeira tela: Menu Principal**. Todos os atalhos de módulos e funções em uma única tela, botões compactos, arredondados, bem organizados; módulos mais utilizados primeiro. Rolagem quando necessário. Não depender do menu lateral ocultável.
2. **Início / Indicadores**: tela separada, dedicada aos indicadores e ranking de produtos mais vendidos do mês; manter cálculos e informações existentes. Ranking legível, sem texto cortado.
3. **Calculadora**: botão discreto no Menu Principal; abrir calculadora nativa independente, com visual azul/branco, bordas arredondadas e transparência leve, sem afetar vendas ou banco.
4. Janelas e botões dos módulos: acabamento arredondado, tamanhos adequados, cabeçalhos e barra de tarefas acessíveis; janelas independentes minimizáveis.
5. Manter atalhos para PDV, Vendas, Cascos retornáveis, Fiado, Produtos, Estoque, Clientes, Compras, Caixa, Financeiro, Relatórios, Lucro, Sistema, WhatsApp, backup, atualização e funções auxiliares já existentes.

## Estado da implementação
- Alterações de navegação e calculadora em **PR #44**, ramificação `feature/novo-win8-menu-inicial-build467`:
  https://github.com/giliotiesg-pixel/erp-gratidao-updates/pull/44
- **Não considerar implantado** até mesclar, compilar, testar, publicar Release com build superior à instalada e confirmar atualização no aplicativo.
- Não atribuir número de build manualmente; workflow usa `github.run_number`.

## Preservação obrigatória
- Preservar vendas, itens de venda, fiado, clientes, estoque, produtos, compras, caixa, configurações e auditorias.
- Antes de toda atualização: backup completo para a pasta configurada; se falhar, **abortar**.
- Não modificar esquema do banco ou rotinas financeiras por uma alteração exclusivamente visual.
- Não usar projetos `amzg-src`, `amzg` ou `erp` como substitutos do `novo-win8`.
