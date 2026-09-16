package main

import "fmt"

type MigrationAudit12115 struct { Name string; OK bool; Detail string }

// Auditoria executavel: valida estruturas principais já migradas sem apagar ou recriar dados comerciais.
func migrationAudit12115() []MigrationAudit12115 {
 checks:=[]struct{name,sql string}{
  {"Produtos","SELECT COUNT(*) FROM products"},
  {"Vendas","SELECT COUNT(*) FROM sales"},
  {"Itens de vendas","SELECT COUNT(*) FROM sale_items"},
  {"Movimentacoes de estoque","SELECT COUNT(*) FROM inventory_movements"},
  {"Compras 12.1.15","SELECT COUNT(*) FROM purchases_12115"},
  {"Fiado","SELECT COUNT(*) FROM credit_payments"},
  {"Parceiros","SELECT COUNT(*) FROM partners_12115"},
  {"Financeiro","SELECT COUNT(*) FROM payable_accounts_12115"},
  {"Ofertas","SELECT COUNT(*) FROM offers_12115"},
  {"Cofre Digital","SELECT COUNT(*) FROM vault_12115"},
 }
 _=ensurePurchase12115Schema();_=ensureEstoque12115Schema();_=ensureFiado12115Schema();_=ensurePartnersFinance12115Schema();_=ensureOfertasConfig12115Schema();_=ensureCofre12115Schema()
 out:=make([]MigrationAudit12115,0,len(checks));for _,c:=range checks{v:=scalar(c.sql);ok:=v!="";detail:="estrutura disponivel";if !ok{detail="estrutura ausente ou consulta falhou"};out=append(out,MigrationAudit12115{Name:c.name,OK:ok,Detail:detail})};return out
}

func migrationAuditSummary12115() string {rows:=migrationAudit12115();ok:=0;for _,r:=range rows{if r.OK{ok++}};return fmt.Sprintf("Migracao 12.1.15: %d/%d estruturas verificadas",ok,len(rows))}
