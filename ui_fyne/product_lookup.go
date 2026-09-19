package main

import (
 "fmt"
 "image/color"
 "strings"

 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/canvas"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/widget"
)

// buildProductLookup implementa uma consulta somente leitura. Nenhuma operacao
// desta tela altera cadastro, preco ou estoque do ERP oficial.
func buildProductLookup(store *Store) fyne.CanvasObject {
 search := widget.NewEntry()
 search.SetPlaceHolder("Bipe o código de barras ou pesquise pela descrição...")

 description := canvas.NewText("Selecione um produto", color.NRGBA{R: 18, G: 73, B: 145, A: 255})
 description.TextSize = 64
 description.TextStyle = fyne.TextStyle{Bold: true}
 description.Alignment = fyne.TextAlignCenter

 price := canvas.NewText("R$ 0,00", color.NRGBA{R: 190, G: 32, B: 38, A: 255})
 price.TextSize = 100
 price.TextStyle = fyne.TextStyle{Bold: true}
 price.Alignment = fyne.TextAlignCenter

 stock := widget.NewLabelWithStyle("Estoque atual: —", fyne.TextAlignCenter, fyne.TextStyle{Bold: true})
 status := widget.NewLabel("")

 rows := store.SearchProducts("")
 list := widget.NewList(
  func() int { return len(rows) },
  func() fyne.CanvasObject { return widget.NewLabel("") },
  func(id widget.ListItemID, obj fyne.CanvasObject) {
   if id < 0 || id >= len(rows) { return }
   r := rows[id]
   obj.(*widget.Label).SetText(fmt.Sprintf("%s   •   %s   •   R$ %s   •   estoque %s %s", r[0], r[1], strings.ReplaceAll(r[2], ".", ","), r[3], r[4]))
  },
 )

 showRow := func(r []string) {
  if len(r) < 5 { return }
  description.Text = r[1]
  description.Refresh()
  price.Text = "R$ " + strings.ReplaceAll(r[2], ".", ",")
  price.Refresh()
  stock.SetText("Estoque atual: " + r[3] + " " + r[4])
  status.SetText("")
 }
 list.OnSelected = func(id widget.ListItemID) {
  if id >= 0 && id < len(rows) { showRow(rows[id]) }
 }

 refresh := func(q string) {
  rows = store.SearchProducts(q)
  list.UnselectAll()
  list.Refresh()
  if len(rows) == 0 && strings.TrimSpace(q) != "" {
   status.SetText("Produto não cadastrado")
  } else {
   status.SetText("")
  }
 }
 search.OnChanged = refresh
 search.OnSubmitted = func(q string) {
  q = strings.TrimSpace(q)
  if q == "" { return }
  matches := store.SearchProducts(q)
  for _, r := range matches {
   if len(r) >= 5 && strings.EqualFold(strings.TrimSpace(r[0]), q) {
    showRow(r)
    search.SetText("")
    return
   }
  }
  if len(matches) == 1 {
   showRow(matches[0])
   return
  }
  status.SetText("Produto não cadastrado")
 }

 clear := widget.NewButton("Limpar", func() {
  search.SetText("")
  description.Text = "Selecione um produto"
  description.Refresh()
  price.Text = "R$ 0,00"
  price.Refresh()
  stock.SetText("Estoque atual: —")
  status.SetText("")
  search.FocusGained()
 })

 hero := widget.NewCard("Consulta rápida", "Descrição, preço e quantidade atual", container.NewVBox(
  container.NewCenter(description),
  container.NewCenter(price),
  stock,
  container.NewCenter(status),
 ))

 return container.NewBorder(
  container.NewVBox(
   widget.NewLabelWithStyle("Consulta de Produto", fyne.TextAlignLeading, fyne.TextStyle{Bold: true}),
   widget.NewLabel("Bipe o código ou pesquise pelo nome. Esta tela não altera o banco de dados."),
   container.NewBorder(nil, nil, nil, clear, search),
   hero,
  ),
  nil, nil, nil,
  container.NewPadded(list),
 )
}
