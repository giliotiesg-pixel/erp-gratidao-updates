package main

import (
	"fmt"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
)

// buildReports cria consultas gerenciais somente leitura. Nenhuma operação desta
// tela grava no banco do ERP oficial.
func buildReports(s *Store, w fyne.Window) fyne.CanvasObject {
	title := widget.NewLabelWithStyle("Relatórios", fyne.TextAlignLeading, fyne.TextStyle{Bold: true})
	note := widget.NewLabel("Consultas gerenciais em modo somente leitura")

	typeSelect := widget.NewSelect([]string{
		"Vendas do período",
		"Estoque baixo",
		"Validades",
		"Fiado em aberto",
	}, nil)
	typeSelect.SetSelected("Vendas do período")

	periodSelect := widget.NewSelect([]string{"Hoje", "Últimos 7 dias", "Mês atual"}, nil)
	periodSelect.SetSelected("Hoje")

	rows := [][]string{}
	summary := widget.NewLabel("")
	list := widget.NewList(
		func() int { return len(rows) },
		func() fyne.CanvasObject { return widget.NewLabel("") },
		func(id widget.ListItemID, o fyne.CanvasObject) {
			if id < 0 || id >= len(rows) { return }
			o.(*widget.Label).SetText(formatReportRow(typeSelect.Selected, rows[id]))
		},
	)

	load := func() {
		if s == nil || s.DB == nil {
			rows = nil
			summary.SetText("Banco de dados indisponível")
			list.Refresh()
			return
		}
		switch typeSelect.Selected {
		case "Estoque baixo":
			periodSelect.Disable()
			rows = s.rows("SELECT COALESCE(barcode,''),description,printf('%.3f',stock),printf('%.3f',min_stock) FROM products WHERE active=1 AND stock<=min_stock ORDER BY stock ASC,description LIMIT 1000")
			summary.SetText(fmt.Sprintf("%d produto(s) com estoque no mínimo ou abaixo", len(rows)))
		case "Validades":
			periodSelect.Disable()
			rows = s.rows("SELECT description,COALESCE(expiration_date,''),printf('%.3f',stock) FROM products WHERE active=1 AND expiration_date IS NOT NULL AND expiration_date<>'' ORDER BY expiration_date,description LIMIT 1000")
			summary.SetText(fmt.Sprintf("%d produto(s) com validade cadastrada", len(rows)))
		case "Fiado em aberto":
			periodSelect.Disable()
			rows = s.rows("SELECT sale_number,customer_name,printf('%.2f',total),printf('%.2f',MAX(0,total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=sales.id),0))) FROM sales WHERE payment_method='FIADO' AND deleted_at IS NULL AND total>COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=sales.id),0) ORDER BY customer_name,id DESC LIMIT 1000")
			var balance float64
			_ = s.DB.QueryRow("SELECT COALESCE(SUM(MAX(0,total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=sales.id),0))),0) FROM sales WHERE payment_method='FIADO' AND deleted_at IS NULL").Scan(&balance)
			summary.SetText(fmt.Sprintf("%d venda(s) em aberto • Saldo: R$ %.2f", len(rows), balance))
		default:
			periodSelect.Enable()
			modifier := "date('now','localtime')"
			switch periodSelect.Selected {
			case "Últimos 7 dias": modifier = "date('now','localtime','-6 days')"
			case "Mês atual": modifier = "date('now','localtime','start of month')"
			}
			q := "SELECT sale_number,datetime(created_at,'localtime'),COALESCE(customer_name,'Consumidor'),COALESCE(payment_method,''),printf('%.2f',total) FROM sales WHERE deleted_at IS NULL AND status='CONCLUIDA' AND date(created_at,'localtime') >= " + modifier + " ORDER BY id DESC LIMIT 1000"
			rows = s.rows(q)
			var total float64
			qTotal := "SELECT COALESCE(SUM(total),0) FROM sales WHERE deleted_at IS NULL AND status='CONCLUIDA' AND date(created_at,'localtime') >= " + modifier
			_ = s.DB.QueryRow(qTotal).Scan(&total)
			summary.SetText(fmt.Sprintf("%d venda(s) • Faturamento: R$ %.2f", len(rows), total))
		}
		list.Refresh()
	}

	typeSelect.OnChanged = func(string) { load() }
	periodSelect.OnChanged = func(string) { if typeSelect.Selected == "Vendas do período" { load() } }
	refresh := widget.NewButton("Atualizar", load)
	stamp := widget.NewLabel("Consulta local • " + time.Now().Format("02/01/2006"))
	load()

	filters := container.NewGridWithColumns(3,
		container.NewVBox(widget.NewLabel("Relatório"), typeSelect),
		container.NewVBox(widget.NewLabel("Período"), periodSelect),
		container.NewVBox(widget.NewLabel("Ações"), refresh),
	)
	return container.NewBorder(
		container.NewVBox(title, note, filters, widget.NewSeparator(), summary, stamp),
		nil, nil, nil,
		container.NewPadded(list),
	)
}

func formatReportRow(kind string, row []string) string {
	get := func(i int) string { if i >= 0 && i < len(row) { return row[i] }; return "" }
	switch kind {
	case "Estoque baixo":
		return fmt.Sprintf("%s • %s • Estoque: %s • Mínimo: %s", get(0), get(1), get(2), get(3))
	case "Validades":
		return fmt.Sprintf("%s • Validade: %s • Estoque: %s", get(0), get(1), get(2))
	case "Fiado em aberto":
		return fmt.Sprintf("Venda %s • %s • Total R$ %s • Saldo R$ %s", get(0), get(1), get(2), get(3))
	default:
		return fmt.Sprintf("Venda %s • %s • %s • %s • R$ %s", get(0), get(1), get(2), get(3), get(4))
	}
}
