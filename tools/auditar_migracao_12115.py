#!/usr/bin/env python3
"""Auditoria de cobertura 12.1.15 -> PRO. Somente leitura; nao altera banco/dados."""
from pathlib import Path
import sys

files = [Path('src/ARMAZEM_GRATIDAO_PRO.go'), *sorted(Path('src').glob('MIGRACAO_*_12115.go'))]
src = '\n'.join(p.read_text(encoding='utf-8', errors='ignore') for p in files if p.exists()).lower()

required = {
 'PDV':['pdv'], 'Vendas':['vendas'], 'Caixa':['caixa'], 'Produtos':['produtos'],
 'Estoque':['estoque'], 'Validade':['validade'], 'Compras':['compras'],
 'Clientes':['clientes'], 'Fornecedores':['fornecedores'], 'Fiado':['fiado'],
 # Contas a pagar no PRO usa payable_accounts/payableStatement; nao depende da palavra de interface "contas" no fonte.
 'Contas':['payable_accounts_12115','payablestatement12115'],
 'Lucro':['lucro'], 'Relatorios':['relat'], 'Fiscal':['fiscal'],
 'Ofertas':['ofertas'], 'Cofre Digital':['cofre'], 'Empresa':['empresa'],
 'Configuracoes':['configura'], 'Consulta de Produto':['consulta','produto'],
 'Recibos':['receipt'], 'Auditoria':['migrationaudit12115'],
}
missing=[m for m,n in required.items() if not all(x in src for x in n)]
if missing:
 print('MIGRACAO 12.1.15 INCOMPLETA. Modulos ausentes:')
 for m in missing: print(' -',m)
 sys.exit(1)
print(f'Auditoria 12.1.15 -> PRO OK: {len(required)}/{len(required)} blocos presentes em {len(files)} arquivos nativos.')
print('Auditoria somente leitura: banco, vendas, estoque e Fiado preservados.')
