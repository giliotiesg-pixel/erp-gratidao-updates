//go:build windows

package main

import (
    "archive/zip"
    "crypto/sha256"
    "encoding/hex"
    "fmt"
    "io"
    "net/http"
    "os"
    "os/exec"
    "path/filepath"
    "syscall"
    "time"
    "unsafe"
)

const (
    targetExe = `C:\ERP-Gratidao\ERP Gratidao.exe`
    backupDir = `C:\ERP-Gratidao\backups`
)

var (
    user32 = syscall.NewLazyDLL("user32.dll")
    messageBoxW = user32.NewProc("MessageBoxW")
)

func ws(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func msg(title, text string) { messageBoxW.Call(0, uintptr(unsafe.Pointer(ws(text))), uintptr(unsafe.Pointer(ws(title))), 0x40) }

func fileSHA(path string) (string, error) {
    f, err := os.Open(path); if err != nil { return "", err }; defer f.Close()
    h := sha256.New(); if _, err = io.Copy(h, f); err != nil { return "", err }
    return hex.EncodeToString(h.Sum(nil)), nil
}

func download(url, dst string) error {
    c := &http.Client{Timeout: 10 * time.Minute}
    r, err := c.Get(url); if err != nil { return err }; defer r.Body.Close()
    if r.StatusCode < 200 || r.StatusCode >= 300 { return fmt.Errorf("download HTTP %s", r.Status) }
    f, err := os.Create(dst); if err != nil { return err }
    _, cpErr := io.Copy(f, r.Body); closeErr := f.Close()
    if cpErr != nil { return cpErr }; return closeErr
}

func main() {
    if len(os.Args) < 4 {
        msg("ARMAZEM GRATIDÃO PRO", "Atualizador nativo. Use pelo botão Atualizações do ERP.")
        return
    }
    version, url, expected := os.Args[1], os.Args[2], os.Args[3]
    root := filepath.Dir(targetExe)
    if err := os.MkdirAll(backupDir, 0755); err != nil { msg("Falha", err.Error()); return }
    tmp := filepath.Join(root, "Atualizacao-"+version+".download")
    _ = os.Remove(tmp)
    msg("ARMAZEM GRATIDÃO PRO", "Nova versão "+version+" encontrada.\n\nBaixando e verificando a atualização...")
    if err := download(url, tmp); err != nil { msg("Falha no download", err.Error()); return }
    got, err := fileSHA(tmp); if err != nil || got != expected { _ = os.Remove(tmp); msg("Falha de segurança", "SHA-256 da atualização não confere."); return }
    installFile := tmp
    if filepath.Ext(url) == ".zip" || filepath.Ext(tmp) == ".zip" {
        zr, ze := zip.OpenReader(tmp)
        if ze != nil { _ = os.Remove(tmp); msg("Falha no pacote", "ZIP não é válido: "+ze.Error()); return }
        var exe *zip.File
        for _, zf := range zr.File { if filepath.Ext(zf.Name) == ".exe" { exe = zf; break } }
        if exe == nil { zr.Close(); _ = os.Remove(tmp); msg("Falha no pacote", "ZIP não contém o ERP executável."); return }
        rc, ze := exe.Open(); if ze != nil { zr.Close(); _ = os.Remove(tmp); msg("Falha no pacote", ze.Error()); return }
        extracted := filepath.Join(root, "ERP-Gratidao-"+version+".exe.download")
        out, ze := os.Create(extracted); if ze == nil { _, ze = io.Copy(out, rc); _ = out.Close() }
        _ = rc.Close(); _ = zr.Close()
        if ze != nil { _ = os.Remove(tmp); _ = os.Remove(extracted); msg("Falha no pacote", ze.Error()); return }
        installFile = extracted
    }
    _ = exec.Command("taskkill", "/F", "/IM", "ERP Gratidao.exe").Run()
    time.Sleep(1200 * time.Millisecond)
    backup := filepath.Join(backupDir, "ERP Gratidao.antes-"+version+"."+time.Now().Format("20060102-150405")+".exe")
    if err := os.Rename(targetExe, backup); err != nil { msg("Falha ao criar backup", err.Error()); return }
    if err := os.Rename(installFile, targetExe); err != nil { _ = os.Rename(backup, targetExe); msg("Falha ao instalar", err.Error()); return }
    if installFile == tmp {
        got, err = fileSHA(targetExe); if err != nil || got != expected { _ = os.Remove(targetExe); _ = os.Rename(backup, targetExe); msg("Falha na verificação", "O executável anterior foi restaurado."); return }
    }
    _ = os.Remove(tmp)
    if err := exec.Command(targetExe).Start(); err != nil { msg("Atualização concluída", "Versão "+version+" instalada, mas o ERP não abriu automaticamente."); return }
    msg("Atualização concluída", "ARMAZEM GRATIDÃO PRO "+version+" instalado com sucesso.\n\nBanco de dados e Fiado preservados.")
}
