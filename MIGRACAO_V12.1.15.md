# Migração integral — Armazém Gratidão PDV v12.1.15 → ARMAZÉM GRATIDÃO PRO

Origem funcional: `Armazem_Gratidao_PDV_v12.1.15_Compras_Corrigida(1).zip`.
Destino: ARMAZÉM GRATIDÃO PRO nativo Windows (Go/Win32).

## Regra da migração
- Migrar regras e funcionalidades, sem incorporar Electron/WebView/HTML ao PRO.
- Preservar banco de dados e dados comerciais.
- Remover dependências antigas de lote; manter validade.
- Manter atualizador nativo, backup, SHA-256 e rollback.
- Cabeçalho de cada módulo: somente nome do módulo, centralizado, sem subtítulo.

## Módulos da referência v12.1.15
- Início / dashboard
- PDV / vendas
- Vendas e histórico
- Caixa
- Produtos
- Estoque / reposição / auditoria
- Validade
- Compras / entradas
- Clientes e fornecedores
- Fiado / extratos / compras pagas
- Financeiro / contas
- Lucro
- Relatórios
- Fiscal
- Ofertas
- Cofre Digital
- Empresa
- Configurações
- Consulta de Produto

## Funcionalidades adicionais identificadas na origem
- Produtos mais vendidos e histórico mensal
- Pesquisa e edição rápida de estoque
- Reposição de estoque com custo, preço e validade
- Promoção por produto
- Impressão de extratos
- Compras com itens, quantidade, custo unitário e total
- Controle de validade sem lote

## Estado
Migração integral iniciada. Este documento passa a ser o checklist de paridade funcional da v12.1.15 no PRO.
