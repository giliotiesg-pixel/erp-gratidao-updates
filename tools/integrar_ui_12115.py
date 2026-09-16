#!/usr/bin/env python3
from pathlib import Path
p=Path('src/ARMAZEM_GRATIDAO_PRO.go')
s=p.read_text(encoding='utf-8')
old='''\tcase 110:\n\t\tshowSimple("Compras", "Entrada de compras, itens, fornecedor e valores")'''
new='''\tcase 110:\n\t\tshowComprasUI12115()'''
if old not in s and new not in s:
    raise SystemExit('rota Compras nao encontrada')
s=s.replace(old,new)
anchor='''\tcase 2701:\n\t\tloadValidity("all")'''
cases='''\tcase 4113:\n\t\taddCompraItemUI12115()\n\tcase 4114:\n\t\tremoveCompraItemUI12115()\n\tcase 4121:\n\t\tsaveCompraUI12115()\n\tcase 4122:\n\t\tclearCompraUI12115()\n\tcase 4123:\n\t\tloadCompraHistoryUI12115()\n\tcase 4124:\n\t\tdeleteCompraUI12115()\n'''
if 'case 4113:' not in s:
    if anchor not in s: raise SystemExit('ancora de comandos nao encontrada')
    s=s.replace(anchor,cases+anchor)
p.write_text(s,encoding='utf-8')
print('UI Compras 12.1.15 integrada ao roteamento nativo.')
