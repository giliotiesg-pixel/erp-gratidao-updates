#!/usr/bin/env python3
"""Auditoria de cobertura da migracao Armazem Gratidao PDV 12.1.15 -> PRO.
Nao altera banco nem dados. Falha o build se modulos essenciais desaparecerem do PRO.
"""
from pathlib import Path
import sys

src = Path('src/ARMAZEM_GRATIDAO_PRO.go').read_text(encoding='utf-8', errors='ignore').lower()

# Nomes funcionais da referencia 12.1.15 que precisam continuar representados no PRO.
required = {
    'PDV': ['pdv'],
    'Vendas': ['vendas'],
    'Caixa': ['caixa'],
    'Produtos': ['produtos'],
    'Estoque': ['estoque'],
    'Validade': ['validade'],
    'Compras': ['compras'],
    'Clientes': ['clientes'],
    'Fornecedores': ['fornecedores'],
    'Fiado': ['fiado'],
    'Contas': ['contas'],
    'Lucro': ['lucro'],
    'Relatorios': ['relat'],
    'Fiscal': ['fiscal'],
    'Ofertas': ['ofertas'],
    'Cofre Digital': ['cofre'],
    'Empresa': ['empresa'],
    'Configuracoes': ['configura'],
    'Consulta de Produto': ['consulta', 'produto'],
}

missing = []
for module, needles in required.items():
    if not all(n in src for n in needles):
        missing.append(module)

if missing:
    print('MIGRACAO 12.1.15 INCOMPLETA. Modulos ausentes no PRO:')
    for item in missing:
        print(' -', item)
    sys.exit(1)

print('Auditoria 12.1.15 -> PRO OK: modulos essenciais presentes.')
print('Banco e Fiado nao sao modificados por esta auditoria.')
