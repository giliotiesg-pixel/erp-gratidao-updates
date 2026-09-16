#!/usr/bin/env python3
from pathlib import Path
p=Path('src/ARMAZEM_GRATIDAO_PRO.go');s=p.read_text(encoding='utf-8')
# O menu nativo real usa: 106 Fiado, 108 Caixa, 109 Validade, 110 Compras,
# 111 Clientes/Fornecedores, 112 Lucro e 113 Relatórios. Não usar o mapa antigo.
repls={
'''\tcase 103:\n\t\tshowProdutos()''':'''\tcase 103:\n\t\tshowProdutosUI12115()''',
'''\tcase 104:\n\t\tshowEstoque()''':'''\tcase 104:\n\t\tshowEstoqueUI12115()''',
'''\tcase 105:\n\t\tshowVendas()''':'''\tcase 105:\n\t\tshowVendasUI12115()''',
'''\tcase 106:\n\t\tshowFiado()''':'''\tcase 106:\n\t\tshowFiadoUI12115()''',
'''\tcase 107:\n\t\tshowConsulta()''':'''\tcase 107:\n\t\tshowConsultaGlobal12115()''',
'''\tcase 108:\n\t\tshowCaixa()''':'''\tcase 108:\n\t\tshowCaixaUI12115()''',
'''\tcase 109:\n\t\tshowValidade()''':'''\tcase 109:\n\t\tshowEstoqueUI12115()''',
'''\tcase 110:\n\t\tshowSimple("Compras", "Entrada de compras, itens, fornecedor e valores")''':'''\tcase 110:\n\t\tshowComprasUI12115()''',
'''\tcase 111:\n\t\tshowSimple("Clientes / Fornecedores", "Cadastros e consulta de parceiros")''':'''\tcase 111:\n\t\tshowFinanceiroUI12115()''',
'''\tcase 112:\n\t\tshowSimple("Lucro", "Margem, lucro bruto e dízimo sobre o lucro")''':'''\tcase 112:\n\t\tshowRelatoriosUI12115()''',
'''\tcase 113:\n\t\tshowSimple("Relatórios", "Vendas, estoque, caixa, lucro e fiado")''':'''\tcase 113:\n\t\tshowRelatoriosUI12115()''',
'''\tcase 9002:\n\t\tshowConsulta()''':'''\tcase 9002:\n\t\tshowConsultaGlobal12115()''',
'''\tcase 2107:\n\t\tshowConsulta()''':'''\tcase 2107:\n\t\tshowConsultaGlobal12115()''',
'''\t\tshowProdutos()\n\tcase strings.Contains(q, "estoque"):''':'''\t\tshowProdutosUI12115()\n\tcase strings.Contains(q, "estoque"):''',
'''\t\tshowEstoque()\n\tcase strings.Contains(q, "validade"):''':'''\t\tshowEstoqueUI12115()\n\tcase strings.Contains(q, "validade"):''',
'''\t\tshowValidade()\n\tcase strings.Contains(q, "venda"):''':'''\t\tshowEstoqueUI12115()\n\tcase strings.Contains(q, "venda"):''',
'''\t\tshowVendas()\n\tcase strings.Contains(q, "caixa"):''':'''\t\tshowVendasUI12115()\n\tcase strings.Contains(q, "caixa"):''',
'''\t\tshowCaixa()\n\tcase strings.Contains(q, "fiado"):''':'''\t\tshowCaixaUI12115()\n\tcase strings.Contains(q, "fiado"):''',
'''\t\tshowFiado()\n\tcase strings.Contains(q, "consulta"):''':'''\t\tshowFiadoUI12115()\n\tcase strings.Contains(q, "consulta"):''',
'''\t\tshowConsulta()\n\tcase strings.Contains(q, "compra"):''':'''\t\tshowConsultaGlobal12115()\n\tcase strings.Contains(q, "compra"):''',
'''\t\tshowSimple("Compras", "Entrada de compras, itens, fornecedor e valores")''':'''\t\tshowComprasUI12115()''',
'''\t\tshowSimple("Clientes / Fornecedores", "Cadastros e consulta de parceiros")''':'''\t\tshowFinanceiroUI12115()''',
'''\tcase strings.Contains(q, "lucro"):\n\t\tshowSimple("Lucro", "Margem, lucro bruto e dízimo sobre o lucro")''':'''\tcase strings.Contains(q, "lucro"):\n\t\tshowRelatoriosUI12115()''',
'''\tcase strings.Contains(q, "relat"):\n\t\tshowSimple("Relatórios", "Vendas, estoque, caixa, lucro e fiado")''':'''\tcase strings.Contains(q, "relat"):\n\t\tshowRelatoriosUI12115()''',
}
for old,new in repls.items():
 if old in s:s=s.replace(old,new)
# Fiscal e Ofertas não existiam no menu nativo antigo: adiciona rotas próprias e busca global.
case114='''\tcase 114:\n\t\tclearContent()'''
if '\tcase 115:\n\t\tshowFiscalUI12115()' not in s:
 if case114 not in s:raise SystemExit('case 114 nao encontrado')
 s=s.replace(case114,'''\tcase 115:\n\t\tshowFiscalUI12115()\n\tcase 116:\n\t\tshowOfertasUI12115()\n'''+case114,1)
search_anchor='''\tcase strings.Contains(q, "config"):\n\t\thandleCommand(114)'''
if 'strings.Contains(q, "fiscal")' not in s:
 if search_anchor not in s:raise SystemExit('busca config nao encontrada')
 s=s.replace(search_anchor,'''\tcase strings.Contains(q, "fiscal"):\n\t\tshowFiscalUI12115()\n\tcase strings.Contains(q, "oferta") || strings.Contains(q, "promo"):\n\t\tshowOfertasUI12115()\n'''+search_anchor)
# Botões visíveis no menu lateral, preservando Configurações.
nav_anchor='''\taddNav("Configurações", 114, 703)'''
if 'addNav("Fiscal", 115' not in s:
 if nav_anchor not in s:raise SystemExit('ancora menu configuracoes nao encontrada')
 s=s.replace(nav_anchor,'''\taddNav("Fiscal", 115, 621)\n\taddNav("Ofertas", 116, 662)\n\taddNav("Configurações", 114, 703)''')
pdv_anchor='''\tadd("BUTTON", "Remover item selecionado", 0, 250, 585, 210, 40, 2109)'''
if '"CORRIGIR ESTOQUE REAL"' not in s:
 if pdv_anchor not in s:raise SystemExit('ancora do PDV nao encontrada')
 s=s.replace(pdv_anchor,pdv_anchor+'\n\tadd("BUTTON", "CORRIGIR ESTOQUE REAL", 0, 250, 630, 210, 38, 2111)')
final_anchor='''\tadd("BUTTON", "FINALIZAR VENDA", 0, 955, 645, 295, 58, 2106)'''
if '"DEIXAR VENDA EM ABERTO"' not in s:
 if final_anchor not in s:raise SystemExit('botao finalizar do PDV nao encontrado')
 s=s.replace(final_anchor,'''\tadd("BUTTON", "DEIXAR VENDA EM ABERTO", 0, 735, 645, 205, 58, 2112)\n'''+final_anchor)
anchor='''\tcase 2701:\n\t\tloadValidity("all")''';cases='''\tcase 4113:\n\t\taddCompraItemUI12115()\n\tcase 4114:\n\t\tremoveCompraItemUI12115()\n\tcase 4121:\n\t\tsaveCompraUI12115()\n\tcase 4122:\n\t\tclearCompraUI12115()\n\tcase 4123:\n\t\tloadCompraHistoryUI12115()\n\tcase 4124:\n\t\tdeleteCompraUI12115()\n'''
if 'case 4113:' not in s:
 if anchor not in s:raise SystemExit('ancora de comandos nao encontrada')
 s=s.replace(anchor,cases+anchor)
needle='''func handleCommand(id int) {\n''';handlers=['handlePDV12115','handleVendasUI12115','handleConsultaGlobal12115','handleEstoqueUI12115','handleFiadoUI12115','handleFinanceiroUI12115','handleCaixaUI12115','handleProdutosUI12115','handleRelatoriosUI12115','handleFiscalUI12115','handleOfertasUI12115']
for h in handlers:s=s.replace('\tif '+h+'(id) { return }\n','')
if needle not in s:raise SystemExit('handleCommand nao encontrado')
s=s.replace(needle,needle+''.join('\tif '+h+'(id) { return }\n' for h in handlers),1)
routes={103:'showProdutosUI12115()',104:'showEstoqueUI12115()',105:'showVendasUI12115()',106:'showFiadoUI12115()',107:'showConsultaGlobal12115()',108:'showCaixaUI12115()',109:'showEstoqueUI12115()',110:'showComprasUI12115()',111:'showFinanceiroUI12115()',112:'showRelatoriosUI12115()',113:'showRelatoriosUI12115()',115:'showFiscalUI12115()',116:'showOfertasUI12115()'}
for cid,fn in routes.items():
 marker=f'case {cid}:';pos=s.find(marker)
 if pos<0 or fn not in s[pos:pos+220]:raise SystemExit(f'rota {cid} nao integrada: {fn}')
for required in ['handlePDV12115','CORRIGIR ESTOQUE REAL','DEIXAR VENDA EM ABERTO','addNav("Fiscal", 115','addNav("Ofertas", 116']:
 if required not in s:raise SystemExit('integracao ausente: '+required)
p.write_text(s,encoding='utf-8');print('UI 12.1.15 integrada aos IDs reais do menu nativo, incluindo Fiscal e Ofertas.')
