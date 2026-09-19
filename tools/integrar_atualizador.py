from pathlib import Path
import re

p = Path('src/ARMAZEM_GRATIDAO_PRO.go')
s = p.read_text(encoding='utf-8')

s = re.sub(r'''type UpdateManifest struct \{.*?\n\}''', '''type UpdateManifest struct {
\tVersion       string `json:"version"`
\tPackageURL    string `json:"package_url"`
\tSHA256        string `json:"sha256"`
\tUpdaterURL    string `json:"updater_url"`
\tUpdaterSHA256 string `json:"updater_sha256"`
\tNotes         string `json:"notes"`
}''', s, count=1, flags=re.S)

start = s.index('func installUpdateAsync(m *UpdateManifest) {')
end = s.index('\nfunc finalizeSale()', start)
new = r'''func installUpdateAsync(m *UpdateManifest) {
	if m == nil { return }
	go func() {
		if !versionGreater(m.Version, currentVersion) { return }
		if !strings.HasPrefix(strings.ToLower(m.PackageURL), "https://") || !strings.HasPrefix(strings.ToLower(m.UpdaterURL), "https://") {
			setUpdateError(fmt.Errorf("URLs da atualização devem usar HTTPS")); return
		}
		if len(strings.TrimSpace(m.SHA256)) != 64 || len(strings.TrimSpace(m.UpdaterSHA256)) != 64 {
			setUpdateError(fmt.Errorf("SHA-256 ausente ou inválido no manifesto")); return
		}
		upd := filepath.Join(root, "Updates")
		if e := os.MkdirAll(upd, 0755); e != nil { setUpdateError(e); return }
		updater := filepath.Join(upd, "ATUALIZADOR_GRATIDAO.exe")
		tmp := updater + ".download"
		os.Remove(tmp)
		c := http.Client{Timeout: 5 * time.Minute}
		r, e := c.Get(m.UpdaterURL)
		if e != nil { setUpdateError(e); return }
		defer r.Body.Close()
		if r.StatusCode != 200 { setUpdateError(fmt.Errorf("download do atualizador HTTP %d", r.StatusCode)); return }
		f, e := os.Create(tmp)
		if e != nil { setUpdateError(e); return }
		h := sha256.New()
		_, e = io.Copy(io.MultiWriter(f, h), io.LimitReader(r.Body, 64*1024*1024))
		f.Close()
		if e != nil { os.Remove(tmp); setUpdateError(e); return }
		got := hex.EncodeToString(h.Sum(nil))
		if !strings.EqualFold(got, strings.TrimSpace(m.UpdaterSHA256)) {
			os.Remove(tmp); setUpdateError(fmt.Errorf("SHA-256 do atualizador não confere")); return
		}
		os.Remove(updater)
		if e = os.Rename(tmp, updater); e != nil { setUpdateError(e); return }
		updateMu.Lock()
		updateErr = updater + "\n" + m.Version + "\n" + m.PackageURL + "\n" + strings.TrimSpace(m.SHA256)
		updateMu.Unlock()
		pPostMessageW.Call(mainWnd, WM_APP_UPDATEINSTALL, 0, 0)
	}()
}
func launchPreparedUpdate() {
	updateMu.Lock()
	parts := strings.Split(updateErr, "\n")
	updateMu.Unlock()
	if len(parts) != 4 { msgErr("Atualização preparada inválida."); return }
	execSQL("PRAGMA wal_checkpoint(FULL)")
	updater, version, packageURL, sha := parts[0], parts[1], parts[2], parts[3]
	cmd := exec.Command(updater, version, packageURL, sha)
	cmd.Dir = root
	e := cmd.Start()
	if e != nil { msgErr("Não foi possível iniciar o atualizador nativo: " + e.Error()); return }
	if db != 0 { pSqlClose.Call(db); db = 0 }
	pPostQuitMessage.Call(0)
}
'''
s = s[:start] + new + s[end:]

s = s.replace('Atualizador automático do ARMAZEM GRATIDÃO PRO — verifica, valida, faz backup, instala e reinicia.', 'Atualizador nativo do ARMAZEM GRATIDÃO PRO — seguro, automático e sem arquivos .bat.')
s = s.replace('Fluxo automático: nova versão → download → SHA-256 → preparação → backup → troca do executável → reinício. Em falha, o executável anterior permanece em backups.', 'Fluxo nativo: verifica → baixa o atualizador assinado por SHA-256 → baixa o ERP → cria backup → instala → valida → reinicia. Se houver falha, restaura a versão anterior.')
s = s.replace('O ERP fará o download, validará o SHA-256 e instalará automaticamente.', 'O atualizador nativo fará o download, validará SHA-256, criará backup e instalará automaticamente.')
s = s.replace('Atualização validada e preparada. O ERP será fechado, fará backup, instalará a nova versão e abrirá novamente.', 'Atualizador nativo validado. O ERP será fechado com segurança, atualizado e aberto novamente.')

p.write_text(s, encoding='utf-8')
