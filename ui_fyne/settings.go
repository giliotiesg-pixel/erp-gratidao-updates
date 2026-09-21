package main

import (
 "fmt"
 "os"
 "path/filepath"
 "time"

 "fyne.io/fyne/v2"
 "fyne.io/fyne/v2/container"
 "fyne.io/fyne/v2/dialog"
 "fyne.io/fyne/v2/widget"
)

func buildSettings(store *Store, w fyne.Window) fyne.CanvasObject {
 title := widget.NewLabelWithStyle("Configurações", fyne.TextAlignLeading, fyne.TextStyle{Bold: true})
 subtitle := widget.NewLabel("Sistema, banco de dados, diagnóstico e atualizações automáticas")

 exePath := "—"
 dbPath := "—"
 if exe, err := os.Executable(); err == nil {
  exePath = exe
  root := filepath.Dir(exe)
  candidates := []string{filepath.Join(root, "data", "erp.sqlite"), filepath.Join(filepath.Dir(root), "data", "erp.sqlite")}
  for _, p := range candidates {
   if _, err := os.Stat(p); err == nil {
    dbPath = p
    break
   }
  }
  if dbPath == "—" {
   dbPath = candidates[0]
  }
 }

 status := widget.NewLabel("Aguardando verificação")
 status.Wrapping = fyne.TextWrapWord
 products := widget.NewLabel("—")
 sales := widget.NewLabel("—")
 cash := widget.NewLabel("—")

 refresh := func() {
  if store == nil || store.DB == nil {
   status.SetText("Banco de dados indisponível. Nenhuma alteração foi realizada.")
   products.SetText("—")
   sales.SetText("—")
   cash.SetText("—")
   return
  }
  products.SetText(store.scalar("SELECT COUNT(*) FROM products"))
  sales.SetText(store.scalar("SELECT COUNT(*) FROM sales"))
  cash.SetText(store.scalar("SELECT COUNT(*) FROM cash_sessions"))
  status.SetText("Conexão com o banco disponível")
 }

 verify := widget.NewButton("Verificar banco de dados", func() {
  if store == nil || store.DB == nil {
   dialog.ShowError(fmt.Errorf("banco de dados indisponível"), w)
   return
  }
  var result string
  if err := store.DB.QueryRow("PRAGMA integrity_check").Scan(&result); err != nil {
   status.SetText("Falha na verificação: " + err.Error())
   dialog.ShowError(err, w)
   return
  }
  if result == "ok" {
   status.SetText("Integridade do SQLite verificada: OK. Nenhum dado foi alterado.")
   dialog.ShowInformation("Verificação concluída", "O banco respondeu OK ao PRAGMA integrity_check. Esta operação é somente leitura.", w)
  } else {
   status.SetText("O SQLite informou: " + result)
   dialog.ShowInformation("Atenção", "Resultado da verificação: "+result+"\nNenhuma correção automática foi executada.", w)
  }
 })
 verify.Importance = widget.HighImportance

 refreshButton := widget.NewButton("Atualizar informações", refresh)

 updateStatus := widget.NewLabel("Versão instalada: " + appVersion + " • aguardando verificação")
 updateStatus.Wrapping = fyne.TextWrapWord
 var available updateManifest
 checkUpdate := widget.NewButton("Verificar atualização", func() {
  updateStatus.SetText("Verificando atualização...")
  m, err := fetchUpdateManifest()
  if err != nil { updateStatus.SetText("Não foi possível verificar: " + err.Error()); return }
  available = m
  if newerVersion(m.Version, appVersion) {
   updateStatus.SetText("Nova versão disponível: " + m.Version + " • " + m.Notes)
  } else {
   updateStatus.SetText("Sistema atualizado • versão " + appVersion)
  }
 })
 installUpdate := widget.NewButton("Baixar e instalar atualização", func() {
  if available.Version == "" || !newerVersion(available.Version, appVersion) {
   dialog.ShowInformation("Atualizações", "Primeiro verifique se existe uma nova versão disponível.", w); return
  }
  dialog.ShowConfirm("Atualizar ERP Gratidão", "Será feito backup automático do banco antes da atualização. Deseja continuar?", func(ok bool) {
   if !ok { return }
   backup, err := backupBeforeUpdate(); if err != nil { dialog.ShowError(fmt.Errorf("backup não concluído; atualização cancelada: %w", err), w); return }
   updateStatus.SetText("Backup criado em " + backup + " • baixando atualização...")
   pkg, err := downloadUpdate(available); if err != nil { dialog.ShowError(err, w); return }
   if err = prepareAndLaunchUpdate(pkg); err != nil { dialog.ShowError(err, w); return }
   dialog.ShowInformation("Atualização pronta", "Backup concluído e pacote validado por SHA-256. O ERP será fechado para aplicar a atualização.", w)
   go func(){ time.Sleep(1200*time.Millisecond); fyne.Do(func(){ w.Close() }) }()
  }, w)
 })
 installUpdate.Importance = widget.HighImportance

 paths := widget.NewForm(
  widget.NewFormItem("Executável", widget.NewLabel(exePath)),
  widget.NewFormItem("Banco SQLite", widget.NewLabel(dbPath)),
 )
 paths.Items[0].Widget.(*widget.Label).Wrapping = fyne.TextWrapWord
 paths.Items[1].Widget.(*widget.Label).Wrapping = fyne.TextWrapWord

 counts := container.NewGridWithColumns(3,
  widget.NewCard("Produtos", "registros cadastrados", products),
  widget.NewCard("Vendas", "registros no histórico", sales),
  widget.NewCard("Caixas", "sessões registradas", cash),
 )

 warning := widget.NewCard("Proteção do ERP oficial", "Modo seguro", widget.NewLabel("Esta tela não altera estrutura, conteúdo ou configuração do banco. A verificação de integridade é somente leitura e não executa reparos automáticos."))
 warning.Content.(*widget.Label).Wrapping = fyne.TextWrapWord

 refresh()
 return container.NewVBox(
  title,
  subtitle,
  widget.NewSeparator(),
  widget.NewCard("Ambiente local", "Caminhos usados pela interface experimental", paths),
  counts,
  widget.NewCard("Diagnóstico", "Estado atual", container.NewVBox(status, container.NewHBox(verify, refreshButton))),
  widget.NewCard("Atualizações", "Atualização automática segura", container.NewVBox(updateStatus, container.NewHBox(checkUpdate, installUpdate))),
  warning,
 )
}
