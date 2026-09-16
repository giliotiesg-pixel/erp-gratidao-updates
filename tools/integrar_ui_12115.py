#!/usr/bin/env python3
from pathlib import Path
p=Path('src/ARMAZEM_GRATIDAO_PRO.go');s=p.read_text(encoding='utf-8')
repls={
'''\tcase 103:\n\t\tshowProdutos()''':'''\tcase 103:\n\t\tshowProdutosUI12115()''',
'''\tcase 104:\n\t\tshowEstoque()''':'''\tcase 104:\n\t\tshowEstoqueUI12115()''',
'''\tcase 105:\n\t\tshowVendas()''':'''\tcase 105:\n\t\tshowVendasUI12115()''',
'''\tcase 106:\n\t\tshowCaixa()''':'''\tcase 106:\n\t\tshowCaixaUI12115()''',
'''\tcase 107:\n\t\tshowConsulta()''':'''\tcase 107:\n\t\tshowConsultaGlobal12115()''',
'''\tcase 108:\n\t\tshowFiado()''':'''\tcase 108:\n\t\tshowFiadoUI12115()''',
'''\tcase 109:\n\t\tshowFinanceiro()''':'''\tcase 109:\n\t\tshowFinanceiroUI12115()''',
'''\tcase 110:\n\t\tshowSimple("Compras", "Entrada de compras, itens, fornecedor e valores")''':'''\tcase 110:\n\t\tshowComprasUI12115()''',
'''\tcase 111:\n\t\tshowFiscal()''':'''\tcase 111:\n\t\tshowFiscalUI12115()''',
'''\tcase 112:\n\t\tshowRelatorios()''':'''\tcase 112:\n\t\tshowRelatoriosUI12115()''',
'''\tcase 115:\n\t\tshowSimple("Ofertas", "Promoções e encartes")''':'''\tcase 115:\n\t\tshowOfertasUI12115()''',
'''\tcase 9002:\n\t\tshowConsulta()''':'''\tcase 9002:\n\t\tshowConsultaGlobal12115()''',
'''\tcase 2107:\n\t\tshowConsulta()''':'''\tcase 2107:\n\t\tshowConsultaGlobal12115()''',
'''\t\tshowProdutos()\n\tcase strings.Contains(q, "estoque"):''':'''\t\tshowProdutosUI12115()\n\tcase strings.Contains(q, "estoque"):''',
'''\t\tshowEstoque()\n\tcase strings.Contains(q, "validade"):''':'''\t\tshowEstoqueUI12115()\n\tcase strings.Contains(q, "validade"):''',
'''\t\tshowVendas()\n\tcase strings.Contains(q, "caixa"):''':'''\t\tshowVendasUI12115()\n\tcase strings.Contains(q, "caixa"):''',
'''\t\tshowCaixa()\n\tcase strings.Contains(q, "fiado"):''':'''\t\tshowCaixaUI12115()\n\tcase strings.Contains(q, "fiado"):''',
'''\t\tshowFiado()\n\tcase strings.Contains(q, "consulta"):''':'''\t\tshowFiadoUI12115()\n\tcase strings.Contains(q, "consulta"):''',
'''\t\tshowConsulta()\n\tcase strings.Contains(q, "compra"):''':'''\t\tshowConsultaGlobal12115()\n\tcase strings.Contains(q, "compra"):''',
'''\t\tshowSimple("Compras", "Entrada de compras, itens, fornecedor e valores")''':'''\t\tshowComprasUI12115()''',
'''\tcase strings.Contains(q, "lucro"):\n\t\tshowSimple("Lucro", "Margem, lucro bruto e dízimo sobre o lucro")''':'''\tcase strings.Contains(q, "lucro"):\n\t\tshowRelatoriosUI12115()''',
'''\tcase strings.Contains(q, "relat"):\n\t\tshowSimple("Relatórios", "Vendas, estoque, caixa, lucro e fiado")''':'''\tcase strings.Contains(q, "relat"):\n\t\tshowRelatoriosUI12115()''',
}
for old,new in repls.items():
 if old in s:s=s.replace(old,new)
needle='''\tcase strings.Contains(q, "config"):\n\t\thandleCommand(114)'''
if 'strings.Contains(q, "oferta")' not in s and needle in s:s=s.replace(needle,'''\tcase strings.Contains(q, "oferta") || strings.Contains(q, "promo"):\n\t\tshowOfertasUI12115()\n'''+needle)
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
insert=needle+''.join('\tif '+h+'(id) { return }\n' for h in handlers)
if needle not in s:raise SystemExit('handleCommand nao encontrado')
s=s.replace(needle,insert,1)
# Valida as rotas pelo case do menu, e não por simples presença do nome da função.
routes={103:'showProdutosUI12115()',104:'showEstoqueUI12115()',105:'showVendasUI12115()',106:'showCaixaUI12115()',107:'showConsultaGlobal12115()',108:'showFiadoUI12115()',109:'showFinanceiroUI12115()',110:'showComprasUI12115()',111:'showFiscalUI12115()',112:'showRelatoriosUI12115()',115:'showOfertasUI12115()'}
for cid,fn in routes.items():
 marker=f'case {cid}:'
 pos=s.find(marker)
 if pos<0 or fn not in s[pos:pos+180]:raise SystemExit(f'rota {cid} nao integrada: {fn}')
for old in ['showProdutos()','showEstoque()','showVendas()','showCaixa()','showFiado()','showConsulta()']:
 block=s[s.find('func openModuleSearch()'):s.find('func editProc(')]
 if old in block:raise SystemExit('busca global ainda usa tela antiga: '+old)
for required in ['handlePDV12115','CORRIGIR ESTOQUE REAL','DEIXAR VENDA EM ABERTO']:
 if required not in s:raise SystemExit('PDV 12.1.15 nao integrado: '+required)
p.write_text(s,encoding='utf-8');print('UI 12.1.15 integrada e rotas principais validadas.')
