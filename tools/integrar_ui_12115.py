#!/usr/bin/env python3
from pathlib import Path
p=Path('src/ARMAZEM_GRATIDAO_PRO.go')
s=p.read_text(encoding='utf-8')
repls={
'''\tcase 110:\n\t\tshowSimple("Compras", "Entrada de compras, itens, fornecedor e valores")''':'''\tcase 110:\n\t\tshowComprasUI12115()''',
'''\tcase 105:\n\t\tshowVendas()''':'''\tcase 105:\n\t\tshowVendasUI12115()''',
'''\tcase 104:\n\t\tshowEstoque()''':'''\tcase 104:\n\t\tshowEstoqueUI12115()''',
'''\tcase 107:\n\t\tshowConsulta()''':'''\tcase 107:\n\t\tshowConsultaGlobal12115()''',
'''\tcase 9002:\n\t\tshowConsulta()''':'''\tcase 9002:\n\t\tshowConsultaGlobal12115()''',
'''\tcase 2107:\n\t\tshowConsulta()''':'''\tcase 2107:\n\t\tshowConsultaGlobal12115()''',
}
for old,new in repls.items(): s=s.replace(old,new)
anchor='''\tcase 2701:\n\t\tloadValidity("all")'''
cases='''\tcase 4113:\n\t\taddCompraItemUI12115()\n\tcase 4114:\n\t\tremoveCompraItemUI12115()\n\tcase 4121:\n\t\tsaveCompraUI12115()\n\tcase 4122:\n\t\tclearCompraUI12115()\n\tcase 4123:\n\t\tloadCompraHistoryUI12115()\n\tcase 4124:\n\t\tdeleteCompraUI12115()\n'''
if 'case 4113:' not in s:
 if anchor not in s: raise SystemExit('ancora de comandos nao encontrada')
 s=s.replace(anchor,cases+anchor)
needle='''func handleCommand(id int) {\n'''
insert='''func handleCommand(id int) {\n\tif handleVendasUI12115(id) { return }\n\tif handleConsultaGlobal12115(id) { return }\n\tif handleEstoqueUI12115(id) { return }\n'''
# normaliza bloco anterior para permitir reexecucao do integrador
oldinsert='''func handleCommand(id int) {\n\tif handleVendasUI12115(id) { return }\n\tif handleConsultaGlobal12115(id) { return }\n'''
if insert not in s:
 if oldinsert in s:s=s.replace(oldinsert,insert,1)
 elif needle in s:s=s.replace(needle,insert,1)
 else:raise SystemExit('handleCommand nao encontrado')
p.write_text(s,encoding='utf-8')
print('UI Compras, Vendas, Estoque e Consulta 12.1.15 integradas ao roteamento nativo.')
