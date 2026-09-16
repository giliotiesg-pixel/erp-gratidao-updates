package main

import (
	"archive/zip"
	"bufio"
	"bytes"
	"crypto/sha256"
	"encoding/csv"
	"encoding/hex"
	"encoding/json"
	"encoding/xml"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"sync"
	"syscall"
	"time"
	"unsafe"
)

const currentVersion = "1.2.27"

var (
	user32                = syscall.NewLazyDLL("user32.dll")
	gdi32                 = syscall.NewLazyDLL("gdi32.dll")
	sqlite                = syscall.NewLazyDLL("winsqlite3.dll")
	pCreateWindowExW      = user32.NewProc("CreateWindowExW")
	pDefWindowProcW       = user32.NewProc("DefWindowProcW")
	pRegisterClassExW     = user32.NewProc("RegisterClassExW")
	pShowWindow           = user32.NewProc("ShowWindow")
	pUpdateWindow         = user32.NewProc("UpdateWindow")
	pGetMessageW          = user32.NewProc("GetMessageW")
	pTranslateMessage     = user32.NewProc("TranslateMessage")
	pDispatchMessageW     = user32.NewProc("DispatchMessageW")
	pPostQuitMessage      = user32.NewProc("PostQuitMessage")
	pGetModuleHandleW     = syscall.NewLazyDLL("kernel32.dll").NewProc("GetModuleHandleW")
	pSendMessageW         = user32.NewProc("SendMessageW")
	pSetWindowTextW       = user32.NewProc("SetWindowTextW")
	pGetWindowTextW       = user32.NewProc("GetWindowTextW")
	pGetWindowTextLengthW = user32.NewProc("GetWindowTextLengthW")
	pDestroyWindow        = user32.NewProc("DestroyWindow")
	pPostMessageW         = user32.NewProc("PostMessageW")
	pMessageBoxW          = user32.NewProc("MessageBoxW")
	pCreateFontW          = gdi32.NewProc("CreateFontW")
	pSetFocus             = user32.NewProc("SetFocus")
	pSetWindowLongPtrW    = user32.NewProc("SetWindowLongPtrW")
	pCallWindowProcW      = user32.NewProc("CallWindowProcW")
	pShowWindowCtrl       = user32.NewProc("ShowWindow")
	comdlg32              = syscall.NewLazyDLL("comdlg32.dll")
	pGetOpenFileNameW     = comdlg32.NewProc("GetOpenFileNameW")

	pSqlOpen     = sqlite.NewProc("sqlite3_open")
	pSqlClose    = sqlite.NewProc("sqlite3_close")
	pSqlExec     = sqlite.NewProc("sqlite3_exec")
	pSqlPrepare  = sqlite.NewProc("sqlite3_prepare_v2")
	pSqlStep     = sqlite.NewProc("sqlite3_step")
	pSqlFinalize = sqlite.NewProc("sqlite3_finalize")
	pSqlColText  = sqlite.NewProc("sqlite3_column_text")
	pSqlErrmsg   = sqlite.NewProc("sqlite3_errmsg")
)

const (
	WM_DESTROY           = 0x0002
	WM_COMMAND           = 0x0111
	WM_SETFONT           = 0x0030
	WM_KEYDOWN           = 0x0100
	VK_RETURN            = 13
	WM_APP_DBREADY       = 0x8002
	WM_APP_DBFAIL        = 0x8003
	WM_APP_LOOKUPDONE    = 0x8004
	WM_APP_LOOKUPFAIL    = 0x8005
	WM_APP_UPDATEFOUND   = 0x8006
	WM_APP_UPDATENONE    = 0x8007
	WM_APP_UPDATEFAIL    = 0x8008
	WM_APP_UPDATEINSTALL = 0x8009
	WM_CTLCOLORSTATIC    = 0x0138
	WS_OVERLAPPEDWINDOW  = 0x00CF0000
	WS_VISIBLE           = 0x10000000
	WS_CHILD             = 0x40000000
	WS_BORDER            = 0x00800000
	WS_VSCROLL           = 0x00200000
	WS_TABSTOP           = 0x00010000
	ES_AUTOHSCROLL       = 0x0080
	BS_PUSHBUTTON        = 0
	LBS_NOTIFY           = 0x0001
	LBS_NOINTEGRALHEIGHT = 0x0100
	CBS_DROPDOWNLIST     = 0x0003
	SW_SHOW              = 5
	LB_ADDSTRING         = 0x0180
	LB_RESETCONTENT      = 0x0184
	LB_GETCURSEL         = 0x0188
	CB_ADDSTRING         = 0x0143
	CB_SETCURSEL         = 0x014E
	CB_GETCURSEL         = 0x0147
	SQLITE_ROW           = 100
	SQLITE_DONE          = 101
)

type WNDCLASSEX struct {
	cbSize, style                            uint32
	lpfnWndProc                              uintptr
	cbClsExtra, cbWndExtra                   int32
	hInstance, hIcon, hCursor, hbrBackground uintptr
	lpszMenuName, lpszClassName              *uint16
	hIconSm                                  uintptr
}
type MSG struct {
	hwnd           uintptr
	message        uint32
	wParam, lParam uintptr
	time           uint32
	pt             struct{ x, y int32 }
	lPrivate       uint32
}
type OPENFILENAME struct {
	lStructSize       uint32
	hwndOwner         uintptr
	hInstance         uintptr
	lpstrFilter       *uint16
	lpstrCustomFilter *uint16
	nMaxCustFilter    uint32
	nFilterIndex      uint32
	lpstrFile         *uint16
	nMaxFile          uint32
	lpstrFileTitle    *uint16
	nMaxFileTitle     uint32
	lpstrInitialDir   *uint16
	lpstrTitle        *uint16
	Flags             uint32
	nFileOffset       uint16
	nFileExtension    uint16
	lpstrDefExt       *uint16
	lCustData         uintptr
	lpfnHook          uintptr
	lpTemplateName    *uint16
	pvReserved        uintptr
	dwReserved        uint32
	FlagsEx           uint32
}
type Product struct {
	ID                 int64
	Barcode, Desc      string
	Price, Cost, Stock float64
}
type CartItem struct {
	Product
	Qty float64
}

var fontMono uintptr
var mainWnd uintptr
var font, fontSmall, fontBig, fontTitle uintptr
var content []uintptr
var navControls []uintptr
var menuVisible bool
var moduleSearch uintptr
var topbarBg, topbarBorder uintptr
var topbarLabels = map[uintptr]bool{}
var brushTop, brushRed uintptr
var pCreateSolidBrush = gdi32.NewProc("CreateSolidBrush")
var pSetTextColor = gdi32.NewProc("SetTextColor")
var pSetBkColor = gdi32.NewProc("SetBkColor")
var db uintptr
var dbReady bool
var dbErr string
var dbMu sync.Mutex
var root, dbPath string
var currentModule = "inicio"
var oldEditProc uintptr

var pdvBarcode, pdvQty, pdvCustomer, pdvPayment, pdvList, pdvTotal uintptr
var cart []CartItem
var prodSearch, prodList uintptr
var salesList uintptr
var salesIDs []int64
var stockList, fiadoList, cashList uintptr
var prodBarcodeReg, prodDescReg, prodBrandReg, prodCategoryReg, prodCostReg, prodPriceReg, prodStockReg, prodUnitReg uintptr
var fiadoSearch, fiadoAmount, fiadoMethod, fiadoCustomerEdit uintptr
var fiadoIDs []int64
var fiadoMode = "ABERTO"
var lookupMu sync.Mutex
var lookupData map[string]string
var lookupErr string

func ws(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func bs(s string) *byte   { b := append([]byte(s), 0); return &b[0] }
func cstr(p uintptr) string {
	if p == 0 {
		return ""
	}
	b := []byte{}
	for i := uintptr(0); ; i++ {
		v := *(*byte)(unsafe.Pointer(p + i))
		if v == 0 {
			break
		}
		b = append(b, v)
	}
	return string(b)
}
func esc(s string) string { return strings.ReplaceAll(s, "'", "''") }
func sqlErr() string      { r, _, _ := pSqlErrmsg.Call(db); return cstr(r) }
func execSQL(q string) error {
	p := bs(q)
	r, _, _ := pSqlExec.Call(db, uintptr(unsafe.Pointer(p)), 0, 0, 0)
	if r != 0 {
		return fmt.Errorf("%s", sqlErr())
	}
	return nil
}
func queryRows(q string, cols int) ([][]string, error) {
	var st uintptr
	p := bs(q)
	r, _, _ := pSqlPrepare.Call(db, uintptr(unsafe.Pointer(p)), ^uintptr(0), uintptr(unsafe.Pointer(&st)), 0)
	if r != 0 {
		return nil, fmt.Errorf("%s", sqlErr())
	}
	defer pSqlFinalize.Call(st)
	out := [][]string{}
	for {
		v, _, _ := pSqlStep.Call(st)
		if v == SQLITE_DONE {
			break
		}
		if v != SQLITE_ROW {
			return nil, fmt.Errorf("%s", sqlErr())
		}
		row := make([]string, cols)
		for i := 0; i < cols; i++ {
			x, _, _ := pSqlColText.Call(st, uintptr(i))
			row[i] = cstr(x)
		}
		out = append(out, row)
	}
	return out, nil
}
func scalar(q string) string {
	r, e := queryRows(q, 1)
	if e != nil || len(r) == 0 {
		return ""
	}
	return r[0][0]
}
func parseF(s string) float64 {
	v, _ := strconv.ParseFloat(strings.ReplaceAll(s, ",", "."), 64)
	return v
}
func money(v float64) string { return strings.ReplaceAll(fmt.Sprintf("%.2f", v), ".", ",") }
func clip(s string, n int) string {
	r := []rune(s)
	if len(r) <= n {
		return s
	}
	return string(r[:n-1]) + "…"
}

func add(class, text string, style uint32, x, y, w, h, id int) uintptr {
	hw, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws(class))), uintptr(unsafe.Pointer(ws(text))), uintptr(style|WS_CHILD|WS_VISIBLE), uintptr(x), uintptr(y), uintptr(w), uintptr(h), mainWnd, uintptr(id), 0, 0)
	if font != 0 {
		pSendMessageW.Call(hw, WM_SETFONT, font, 1)
	}
	content = append(content, hw)
	return hw
}
func addNav(text string, id, y int) uintptr {
	hw, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("BUTTON"))), uintptr(unsafe.Pointer(ws(text))), WS_CHILD|WS_VISIBLE|BS_PUSHBUTTON, 15, uintptr(y), 205, 36, mainWnd, uintptr(id), 0, 0)
	pSendMessageW.Call(hw, WM_SETFONT, font, 1)
	navControls = append(navControls, hw)
	return hw
}
func addNavLabel(text string, y int) {
	hw, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("STATIC"))), uintptr(unsafe.Pointer(ws(text))), WS_CHILD|WS_VISIBLE, 15, uintptr(y), 205, 22, mainWnd, 0, 0, 0)
	pSendMessageW.Call(hw, WM_SETFONT, fontSmall, 1)
	navControls = append(navControls, hw)
}
func setText(h uintptr, s string) { pSetWindowTextW.Call(h, uintptr(unsafe.Pointer(ws(s)))) }
func getText(h uintptr) string {
	n, _, _ := pGetWindowTextLengthW.Call(h)
	b := make([]uint16, n+1)
	pGetWindowTextW.Call(h, uintptr(unsafe.Pointer(&b[0])), n+1)
	return syscall.UTF16ToString(b)
}
func msg(s string) {
	pMessageBoxW.Call(mainWnd, uintptr(unsafe.Pointer(ws(s))), uintptr(unsafe.Pointer(ws("ERP Gratidão"))), 0x40)
}
func msgErr(s string) {
	pMessageBoxW.Call(mainWnd, uintptr(unsafe.Pointer(ws(s))), uintptr(unsafe.Pointer(ws("ERP Gratidão"))), 0x10)
}
func clearContent() {
	for _, h := range content {
		pDestroyWindow.Call(h)
	}
	content = nil
}
func listAdd(h uintptr, s string) {
	pSendMessageW.Call(h, LB_ADDSTRING, 0, uintptr(unsafe.Pointer(ws(s))))
}
func listReset(h uintptr) { pSendMessageW.Call(h, LB_RESETCONTENT, 0, 0) }
func header(title, sub string) {
	// Cabeçalho limpo: somente o nome do módulo, centralizado na caixa principal.
	h := add("STATIC", title, 0x00000001, 18, 67, 1215, 42, 0)
	pSendMessageW.Call(h, WM_SETFONT, fontBig, 1)
}
func section(title string, x, y, w int) {
	h := add("STATIC", title, 0, x, y, w, 30, 0)
	pSendMessageW.Call(h, WM_SETFONT, fontBig, 1)
}
func ensureDB() bool {
	dbMu.Lock()
	defer dbMu.Unlock()
	if !dbReady {
		msgErr("Banco de dados ainda não está pronto.\n" + dbErr)
		return false
	}
	return true
}

func initDB() error {
	exe, _ := os.Executable()
	root = filepath.Dir(exe)
	dbPath = filepath.Join(root, "data", "erp.sqlite")
	os.MkdirAll(filepath.Dir(dbPath), 0755)
	p := bs(dbPath)
	r, _, _ := pSqlOpen.Call(uintptr(unsafe.Pointer(p)), uintptr(unsafe.Pointer(&db)))
	if r != 0 {
		return fmt.Errorf("não foi possível abrir o banco: %s", sqlErr())
	}
	schema := `PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;
CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,role TEXT NOT NULL DEFAULT 'ADMIN',active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS products(id INTEGER PRIMARY KEY AUTOINCREMENT,barcode TEXT UNIQUE,internal_code TEXT,description TEXT NOT NULL,brand TEXT,category TEXT,unit TEXT NOT NULL DEFAULT 'UN',cost REAL NOT NULL DEFAULT 0,price REAL NOT NULL DEFAULT 0,stock REAL NOT NULL DEFAULT 0,min_stock REAL NOT NULL DEFAULT 0,active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS inventory_movements(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,movement_type TEXT NOT NULL,origin_type TEXT NOT NULL,origin_id INTEGER,qty REAL NOT NULL,stock_before REAL NOT NULL,stock_after REAL NOT NULL,user_id INTEGER NOT NULL,reason TEXT,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS sales(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_number TEXT UNIQUE NOT NULL,customer_name TEXT NOT NULL DEFAULT 'Consumidor',payment_method TEXT NOT NULL DEFAULT 'DINHEIRO',subtotal REAL NOT NULL DEFAULT 0,discount REAL NOT NULL DEFAULT 0,total REAL NOT NULL DEFAULT 0,cost_total REAL NOT NULL DEFAULT 0,profit REAL NOT NULL DEFAULT 0,tithe_due REAL NOT NULL DEFAULT 0,status TEXT NOT NULL DEFAULT 'CONCLUIDA',created_by INTEGER NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,deleted_at TEXT);
CREATE TABLE IF NOT EXISTS sale_items(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,product_id INTEGER NOT NULL,product_description_snapshot TEXT NOT NULL,qty REAL NOT NULL,unit_price REAL NOT NULL,unit_cost REAL NOT NULL,line_total REAL NOT NULL,line_cost REAL NOT NULL,line_profit REAL NOT NULL);
CREATE TABLE IF NOT EXISTS sale_payments(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,payment_type TEXT NOT NULL,gross_amount REAL NOT NULL DEFAULT 0,discount_percent REAL NOT NULL DEFAULT 0,discount_amount REAL NOT NULL DEFAULT 0,net_amount REAL NOT NULL DEFAULT 0,reference TEXT,pix_payload TEXT,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS cash_sessions(id INTEGER PRIMARY KEY AUTOINCREMENT,opened_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,opening_amount REAL NOT NULL DEFAULT 0,closed_at TEXT,closing_amount REAL,status TEXT NOT NULL DEFAULT 'ABERTO');
CREATE TABLE IF NOT EXISTS credit_payments(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,amount REAL NOT NULL,payment_method TEXT NOT NULL DEFAULT 'DINHEIRO',customer_name TEXT,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS payment_fees(id INTEGER PRIMARY KEY AUTOINCREMENT,payment_type TEXT UNIQUE NOT NULL,fee_type TEXT NOT NULL DEFAULT 'PERCENT',fee_value REAL NOT NULL DEFAULT 0,active INTEGER NOT NULL DEFAULT 1,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS app_settings(setting_key TEXT PRIMARY KEY,setting_value TEXT,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS idx_credit_sale ON credit_payments(sale_id,created_at);
CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode); CREATE INDEX IF NOT EXISTS idx_sales_created ON sales(created_at); CREATE INDEX IF NOT EXISTS idx_inv_product ON inventory_movements(product_id,created_at);`
	if e := execSQL(schema); e != nil {
		return e
	}
	if scalar("SELECT COUNT(*) FROM users") == "0" {
		execSQL("INSERT INTO users(name,role) VALUES('Administrador','ADMIN')")
	}
	for _, pt := range []string{"PIX_QR", "PIX_SEM_QR", "DEBITO", "CREDITO", "ALELO", "PLUXXE", "TICKET", "VR"} {
		execSQL(fmt.Sprintf("INSERT OR IGNORE INTO payment_fees(payment_type,fee_type,fee_value,active) VALUES('%s','PERCENT',0,1)", pt))
	}
	execSQL("INSERT OR IGNORE INTO app_settings(setting_key,setting_value) VALUES('update_manifest_url','')")
	_ = execSQL("ALTER TABLE products ADD COLUMN layout_version TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN photo_url TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN ncm TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN cest TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN cfop TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN cst_csosn TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN origin TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN tax_icms REAL DEFAULT 0")
	_ = execSQL("ALTER TABLE products ADD COLUMN tax_pis REAL DEFAULT 0")
	_ = execSQL("ALTER TABLE products ADD COLUMN tax_cofins REAL DEFAULT 0")
	_ = execSQL("ALTER TABLE products ADD COLUMN gtin_tributable TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN tributary_unit TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN cnae TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN promotion_active INTEGER DEFAULT 0")
	_ = execSQL("ALTER TABLE products ADD COLUMN promotion_discount_percent REAL DEFAULT 0")
	_ = execSQL("ALTER TABLE products ADD COLUMN promotion_price REAL DEFAULT 0")
	_ = execSQL("ALTER TABLE products ADD COLUMN expiration_date TEXT")
	_ = execSQL("ALTER TABLE products ADD COLUMN sold_by_weight INTEGER DEFAULT 0")

	return nil
}

func initDBAsync() {
	go func() {
		e := initDB()
		dbMu.Lock()
		dbReady = e == nil
		if e != nil {
			dbErr = e.Error()
		} else {
			dbErr = ""
		}
		dbMu.Unlock()
		if e != nil {
			pPostMessageW.Call(mainWnd, WM_APP_DBFAIL, 0, 0)
		} else {
			pPostMessageW.Call(mainWnd, WM_APP_DBREADY, 0, 0)
		}
	}()
}

func showBoot() {
	currentModule = "boot"
	clearContent()
	header("ERP Gratidão", "Base nativa inspirada no fluxo operacional do Armazém Gratidão 12.1.5")
	section("Inicializando sistema...", 250, 145, 800)
	add("STATIC", "Abrindo banco de dados em segundo plano. A interface permanece ativa.", 0, 250, 190, 900, 28, 0)
}

func showInicio() {
	if !ensureDB() {
		return
	}
	currentModule = "inicio"
	clearContent()
	header("Início", "Visão geral do estabelecimento e atalhos principais")
	dayCount := scalar("SELECT COUNT(*) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')")
	rev := parseF(scalar("SELECT COALESCE(SUM(total),0) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')"))
	prods := scalar("SELECT COUNT(*) FROM products WHERE active=1")
	low := scalar("SELECT COUNT(*) FROM products WHERE active=1 AND stock<=min_stock")
	cards := []struct {
		t, v string
		x    int
	}{{"Vendas de hoje", dayCount, 250}, {"Faturamento", "R$ " + money(rev), 490}, {"Produtos ativos", prods, 730}, {"Estoque baixo", low, 970}}
	for _, c := range cards {
		add("STATIC", c.t, 0, c.x, 135, 205, 24, 0)
		h := add("STATIC", c.v, 0, c.x, 166, 205, 50, 0)
		pSendMessageW.Call(h, WM_SETFONT, fontTitle, 1)
	}
	section("Acesso rápido", 250, 255, 500)
	add("BUTTON", "Nova venda - PDV", 0, 250, 300, 210, 46, 102)
	add("BUTTON", "Consultar produto", 0, 475, 300, 210, 46, 107)
	add("BUTTON", "Ver vendas", 0, 700, 300, 210, 46, 105)
	add("BUTTON", "Abrir caixa", 0, 925, 300, 210, 46, 108)
	section("Referência 12.1.5 aplicada", 250, 390, 650)
	add("STATIC", "PDV • Vendas • Caixa • Produtos • Estoque • Validade • Compras • Clientes/Fornecedores • Fiado • Lucro • Relatórios", 0, 250, 430, 1000, 32, 0)
	add("STATIC", "A nova base mantém a organização operacional, mas a interface é Windows nativa e não usa navegador.", 0, 250, 470, 1000, 32, 0)
}

func openModuleSearch() {
	q := strings.ToLower(strings.TrimSpace(getText(moduleSearch)))
	if q == "" {
		return
	}
	switch {
	case strings.Contains(q, "inicio"):
		showInicio()
	case strings.Contains(q, "pdv") || strings.Contains(q, "venda rapida"):
		showPDV()
	case strings.Contains(q, "produto") && !strings.Contains(q, "consulta"):
		showProdutos()
	case strings.Contains(q, "estoque"):
		showEstoque()
	case strings.Contains(q, "validade"):
		showValidade()
	case strings.Contains(q, "venda"):
		showVendas()
	case strings.Contains(q, "caixa"):
		showCaixa()
	case strings.Contains(q, "fiado"):
		showFiado()
	case strings.Contains(q, "consulta"):
		showConsulta()
	case strings.Contains(q, "compra"):
		showSimple("Compras", "Entrada de compras, itens, fornecedor e valores")
	case strings.Contains(q, "cliente") || strings.Contains(q, "fornecedor"):
		showSimple("Clientes / Fornecedores", "Cadastros e consulta de parceiros")
	case strings.Contains(q, "lucro"):
		showSimple("Lucro", "Margem, lucro bruto e dízimo sobre o lucro")
	case strings.Contains(q, "relat"):
		showSimple("Relatórios", "Vendas, estoque, caixa, lucro e fiado")
	case strings.Contains(q, "config"):
		handleCommand(114)
	case strings.Contains(q, "atualiza"):
		showUpdater()
	default:
		msg("Módulo não encontrado: " + getText(moduleSearch))
	}
	pSetWindowTextW.Call(moduleSearch, uintptr(unsafe.Pointer(ws(""))))
}

func editProc(hwnd uintptr, m uint32, w, l uintptr) uintptr {
	if m == WM_KEYDOWN && w == VK_RETURN {
		if hwnd == moduleSearch {
			openModuleSearch()
			return 0
		}
		if hwnd == pdvBarcode {
			addCart()
			return 0
		}
		if hwnd == prodSearch {
			loadProducts(getText(prodSearch))
			return 0
		}
		if hwnd == prodBarcodeReg {
			lookupProductOnline()
			return 0
		}
		if hwnd == fiadoSearch {
			loadFiado()
			return 0
		}
	}
	r, _, _ := pCallWindowProcW.Call(oldEditProc, hwnd, uintptr(m), w, l)
	return r
}
func subclassEdit(h uintptr) {
	cb := syscall.NewCallback(editProc)
	prev, _, _ := pSetWindowLongPtrW.Call(h, ^uintptr(3), cb)
	if oldEditProc == 0 {
		oldEditProc = prev
	}
}

func showPDV() {
	if !ensureDB() {
		return
	}
	currentModule = "pdv"
	clearContent()
	header("PDV", "Venda rápida — fluxo reforçado com referência no Armazém Gratidão 12.1.5")
	add("STATIC", "Código de barras", 0, 250, 125, 160, 22, 0)
	pdvBarcode = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 250, 150, 320, 34, 2101)
	subclassEdit(pdvBarcode)
	add("STATIC", "Qtd.", 0, 585, 125, 70, 22, 0)
	pdvQty = add("EDIT", "1", WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 585, 150, 85, 34, 2102)
	add("BUTTON", "Adicionar", 0, 685, 147, 120, 40, 2103)
	add("BUTTON", "Consulta produto", 0, 820, 147, 155, 40, 2107)
	add("STATIC", "Cliente", 0, 990, 125, 110, 22, 0)
	pdvCustomer = add("EDIT", "Consumidor", WS_BORDER|ES_AUTOHSCROLL, 990, 150, 260, 34, 2108)
	pdvList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT, 250, 215, 1000, 355, 2104)
	add("BUTTON", "Remover item selecionado", 0, 250, 585, 210, 40, 2109)
	add("BUTTON", "Limpar venda", 0, 475, 585, 150, 40, 2110)
	add("STATIC", "Pagamento", 0, 650, 585, 100, 24, 0)
	pdvPayment = add("COMBOBOX", "", CBS_DROPDOWNLIST|WS_VSCROLL, 745, 580, 190, 240, 2105)
	for _, s := range []string{"DINHEIRO", "PIX_QR", "PIX_SEM_QR", "DEBITO", "CREDITO", "ALELO", "PLUXXE", "TICKET", "VR", "FIADO"} {
		pSendMessageW.Call(pdvPayment, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(ws(s))))
	}
	pSendMessageW.Call(pdvPayment, CB_SETCURSEL, 0, 0)
	pdvTotal = add("STATIC", "TOTAL: R$ 0,00", 0, 250, 650, 460, 58, 0)
	pSendMessageW.Call(pdvTotal, WM_SETFONT, fontTitle, 1)
	add("BUTTON", "FINALIZAR VENDA", 0, 955, 645, 295, 58, 2106)
	refreshCart()
	pSetFocus.Call(pdvBarcode)
}
func refreshCart() {
	if pdvList == 0 {
		return
	}
	listReset(pdvList)
	total := 0.0
	listAdd(pdvList, "ITEM  CÓDIGO          DESCRIÇÃO                                      QTD       UNIT.          TOTAL")
	for i, it := range cart {
		line := fmt.Sprintf("%02d    %-14s  %-44s  %8.3f   R$ %8.2f   R$ %9.2f", i+1, it.Barcode, clip(it.Desc, 44), it.Qty, it.Price, it.Qty*it.Price)
		listAdd(pdvList, line)
		total += it.Qty * it.Price
	}
	setText(pdvTotal, "TOTAL: R$ "+money(total))
}
func addCart() {
	if !ensureDB() {
		return
	}
	barcode := strings.TrimSpace(getText(pdvBarcode))
	if barcode == "" {
		return
	}
	qty := parseF(strings.TrimSpace(getText(pdvQty)))
	if qty <= 0 {
		qty = 1
	}
	rows, e := queryRows("SELECT id,COALESCE(barcode,''),description,price,cost,stock FROM products WHERE active=1 AND barcode='"+esc(barcode)+"' LIMIT 1", 6)
	if e != nil || len(rows) == 0 {
		msgErr("Produto não cadastrado: " + barcode)
		return
	}
	r := rows[0]
	id, _ := strconv.ParseInt(r[0], 10, 64)
	price := parseF(r[3])
	cost := parseF(r[4])
	stock := parseF(r[5])
	current := 0.0
	for _, it := range cart {
		if it.ID == id {
			current += it.Qty
		}
	}
	if current+qty > stock+0.0001 {
		msgErr(fmt.Sprintf("Estoque insuficiente.\nProduto: %s\nEstoque atual: %.3f", r[2], stock))
		return
	}
	found := false
	for i := range cart {
		if cart[i].ID == id {
			cart[i].Qty += qty
			found = true
			break
		}
	}
	if !found {
		cart = append(cart, CartItem{Product: Product{ID: id, Barcode: r[1], Desc: r[2], Price: price, Cost: cost, Stock: stock}, Qty: qty})
	}
	setText(pdvBarcode, "")
	setText(pdvQty, "1")
	refreshCart()
	pSetFocus.Call(pdvBarcode)
}
func removeCartSelected() {
	idx, _, _ := pSendMessageW.Call(pdvList, LB_GETCURSEL, 0, 0)
	i := int(idx) - 1
	if i < 0 || i >= len(cart) {
		msg("Selecione um item da venda.")
		return
	}
	cart = append(cart[:i], cart[i+1:]...)
	refreshCart()
}
func clearCart() {
	if len(cart) == 0 {
		return
	}
	cart = nil
	refreshCart()
	pSetFocus.Call(pdvBarcode)
}

func paymentFeeFor(method string, gross float64) (float64, string, float64) {
	rows, _ := queryRows(fmt.Sprintf("SELECT fee_type,fee_value FROM payment_fees WHERE payment_type='%s' AND active=1 LIMIT 1", esc(method)), 2)
	if len(rows) == 0 {
		return 0, "", 0
	}
	typ := strings.ToUpper(rows[0][0])
	val := parseF(rows[0][1])
	fee := gross * val / 100
	if typ == "FIXED" {
		fee = val
	}
	if fee < 0 {
		fee = 0
	}
	if fee > gross {
		fee = gross
	}
	return fee, typ, val
}
func showPaymentFees() {
	if !ensureDB() {
		return
	}
	clearContent()
	header("Taxas de Cartões / PIX", "Taxas aplicadas automaticamente no pagamento")
	section("Modalidades", 250, 130, 500)
	y := 180
	methods := []string{"PIX_QR", "PIX_SEM_QR", "DEBITO", "CREDITO", "ALELO", "PLUXXE", "TICKET", "VR"}
	for i, m := range methods {
		rows, _ := queryRows(fmt.Sprintf("SELECT fee_type,fee_value FROM payment_fees WHERE payment_type='%s'", m), 2)
		typ := "PERCENT"
		val := "0"
		if len(rows) > 0 {
			typ = rows[0][0]
			val = rows[0][1]
		}
		add("STATIC", m, 0, 260, y, 190, 28, 0)
		c := add("COMBOBOX", "", CBS_DROPDOWNLIST|WS_TABSTOP, 455, y-4, 130, 120, 3100+i*3)
		pSendMessageW.Call(c, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(ws("%"))))
		pSendMessageW.Call(c, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(ws("FIXA"))))
		if typ == "FIXED" {
			pSendMessageW.Call(c, CB_SETCURSEL, 1, 0)
		} else {
			pSendMessageW.Call(c, CB_SETCURSEL, 0, 0)
		}
		add("EDIT", val, WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 595, y-4, 120, 32, 3101+i*3)
		add("BUTTON", "Salvar", 0, 725, y-6, 90, 36, 3102+i*3)
		y += 48
	}
	add("STATIC", "A taxa é custo financeiro. O valor líquido recebido fica gravado junto ao pagamento.", 0, 260, y+15, 900, 40, 0)
}
func savePaymentFee(id int) {
	i := (id - 3102) / 3
	methods := []string{"PIX_QR", "PIX_SEM_QR", "DEBITO", "CREDITO", "ALELO", "PLUXXE", "TICKET", "VR"}
	if i < 0 || i >= len(methods) {
		return
	}
	gd := user32.NewProc("GetDlgItem")
	combo, _, _ := gd.Call(mainWnd, uintptr(3100+i*3))
	edit, _, _ := gd.Call(mainWnd, uintptr(3101+i*3))
	idx, _, _ := pSendMessageW.Call(combo, CB_GETCURSEL, 0, 0)
	typ := "PERCENT"
	if idx == 1 {
		typ = "FIXED"
	}
	val := parseF(getText(edit))
	if val < 0 {
		val = 0
	}
	execSQL(fmt.Sprintf("INSERT INTO payment_fees(payment_type,fee_type,fee_value,active,updated_at) VALUES('%s','%s',%.4f,1,CURRENT_TIMESTAMP) ON CONFLICT(payment_type) DO UPDATE SET fee_type=excluded.fee_type,fee_value=excluded.fee_value,active=1,updated_at=CURRENT_TIMESTAMP", methods[i], typ, val))
	msg("Taxa de " + methods[i] + " salva.")
}

type UpdateManifest struct {
	Version       string `json:"version"`
	PackageURL    string `json:"package_url"`
	SHA256        string `json:"sha256"`
	UpdaterURL    string `json:"updater_url"`
	UpdaterSHA256 string `json:"updater_sha256"`
	Notes         string `json:"notes"`
}

var updateMu sync.Mutex
var updateFound *UpdateManifest
var updateErr string

func versionGreater(a, b string) bool {
	pa := strings.Split(a, ".")
	pb := strings.Split(b, ".")
	for len(pa) < 3 {
		pa = append(pa, "0")
	}
	for len(pb) < 3 {
		pb = append(pb, "0")
	}
	for i := 0; i < 3; i++ {
		ai, _ := strconv.Atoi(pa[i])
		bi, _ := strconv.Atoi(pb[i])
		if ai > bi {
			return true
		}
		if ai < bi {
			return false
		}
	}
	return false
}
func checkUpdatesAsync(silent bool) {
	url := strings.TrimSpace(scalar("SELECT setting_value FROM app_settings WHERE setting_key='update_manifest_url'"))
	if url == "" {
		if !silent {
			msg("Atualizador instalado. Configure uma única vez o endereço HTTPS permanente do manifesto.")
		}
		return
	}
	go func() {
		c := http.Client{Timeout: 20 * time.Second}
		r, e := c.Get(url)
		if e != nil {
			setUpdateError(e)
			return
		}
		defer r.Body.Close()
		if r.StatusCode != 200 {
			setUpdateError(fmt.Errorf("HTTP %d", r.StatusCode))
			return
		}
		var m UpdateManifest
		e = json.NewDecoder(io.LimitReader(r.Body, 1048576)).Decode(&m)
		if e != nil {
			setUpdateError(e)
			return
		}
		if versionGreater(m.Version, currentVersion) {
			updateMu.Lock()
			updateFound = &m
			updateMu.Unlock()
			pPostMessageW.Call(mainWnd, WM_APP_UPDATEFOUND, 0, 0)
		} else if !silent {
			pPostMessageW.Call(mainWnd, WM_APP_UPDATENONE, 0, 0)
		}
	}()
}
func setUpdateError(e error) {
	updateMu.Lock()
	updateErr = e.Error()
	updateMu.Unlock()
	pPostMessageW.Call(mainWnd, WM_APP_UPDATEFAIL, 0, 0)
}
func showUpdater() {
	if !ensureDB() {
		return
	}
	clearContent()
	header("Atualizações", "Atualizador nativo do ARMAZEM GRATIDÃO PRO — seguro, automático e sem arquivos .bat.")
	section("Atualizador de versões", 250, 135, 900)
	add("STATIC", "Manifesto HTTPS permanente:", 0, 255, 190, 220, 28, 0)
	u := scalar("SELECT setting_value FROM app_settings WHERE setting_key='update_manifest_url'")
	add("EDIT", u, WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 475, 185, 610, 34, 3201)
	add("BUTTON", "Salvar", 0, 1095, 182, 100, 40, 3202)
	add("BUTTON", "Verificar agora", 0, 255, 245, 150, 42, 3203)
	add("STATIC", "Fluxo nativo: verifica → baixa o atualizador assinado por SHA-256 → baixa o ERP → cria backup → instala → valida → reinicia. Se houver falha, restaura a versão anterior.", 0, 255, 315, 940, 70, 0)
}
func saveUpdateURL() {
	gd := user32.NewProc("GetDlgItem")
	h, _, _ := gd.Call(mainWnd, 3201)
	u := strings.TrimSpace(getText(h))
	if u != "" && !strings.HasPrefix(strings.ToLower(u), "https://") {
		msgErr("Use somente HTTPS.")
		return
	}
	execSQL(fmt.Sprintf("INSERT INTO app_settings(setting_key,setting_value,updated_at) VALUES('update_manifest_url','%s',CURRENT_TIMESTAMP) ON CONFLICT(setting_key) DO UPDATE SET setting_value=excluded.setting_value,updated_at=CURRENT_TIMESTAMP", esc(u)))
	msg("Endereço salvo. O ERP verificará novas versões automaticamente ao iniciar.")
}
func installUpdateAsync(m *UpdateManifest) {
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
	cmdLine := `"` + updater + `" "` + version + `" "` + packageURL + `" "` + sha + `"`
	var si syscall.StartupInfo
	var pi syscall.ProcessInformation
	si.Cb = uint32(unsafe.Sizeof(si))
	cmdBuf, _ := syscall.UTF16PtrFromString(cmdLine)
	e := syscall.CreateProcess(nil, cmdBuf, nil, nil, false, syscall.CREATE_NEW_PROCESS_GROUP, nil, syscall.StringToUTF16Ptr(root), &si, &pi)
	if e != nil { msgErr("Não foi possível iniciar o atualizador nativo: " + e.Error()); return }
	syscall.CloseHandle(pi.Thread); syscall.CloseHandle(pi.Process)
	if db != 0 { pSqlClose.Call(db); db = 0 }
	pPostQuitMessage.Call(0)
}

func finalizeSale() {
	if len(cart) == 0 {
		msgErr("Não há itens na venda.")
		return
	}
	total, cost := 0.0, 0.0
	for _, it := range cart {
		total += it.Qty * it.Price
		cost += it.Qty * it.Cost
	}
	profit := total - cost
	tithe := 0.0
	if profit > 0 {
		tithe = profit * .10
	}
	idx, _, _ := pSendMessageW.Call(pdvPayment, CB_GETCURSEL, 0, 0)
	pm := []string{"DINHEIRO", "PIX_QR", "PIX_SEM_QR", "DEBITO", "CREDITO", "ALELO", "PLUXXE", "TICKET", "VR", "FIADO"}
	pay := "DINHEIRO"
	if int(idx) >= 0 && int(idx) < len(pm) {
		pay = pm[int(idx)]
	}
	cust := strings.TrimSpace(getText(pdvCustomer))
	if cust == "" {
		cust = "Consumidor"
	}
	saleNo := fmt.Sprintf("V%s-%06d", time.Now().Format("20060102"), time.Now().UnixNano()%1000000)
	if e := execSQL("BEGIN IMMEDIATE"); e != nil {
		msgErr(e.Error())
		return
	}
	ok := true
	var err error
	q := fmt.Sprintf("INSERT INTO sales(sale_number,customer_name,payment_method,subtotal,total,cost_total,profit,tithe_due,status,created_by) VALUES('%s','%s','%s',%.2f,%.2f,%.2f,%.2f,%.2f,'CONCLUIDA',1)", esc(saleNo), esc(cust), pay, total, total, cost, profit, tithe)
	if err = execSQL(q); err != nil {
		ok = false
	}
	sid := scalar("SELECT last_insert_rowid()")
	if ok {
		for _, it := range cart {
			before := parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d", it.ID)))
			after := before - it.Qty
			if after < -0.0001 {
				ok = false
				err = fmt.Errorf("estoque insuficiente para %s", it.Desc)
				break
			}
			line := it.Qty * it.Price
			lc := it.Qty * it.Cost
			lp := line - lc
			qs := fmt.Sprintf("INSERT INTO sale_items(sale_id,product_id,product_description_snapshot,qty,unit_price,unit_cost,line_total,line_cost,line_profit) VALUES(%s,%d,'%s',%.3f,%.2f,%.2f,%.2f,%.2f,%.2f); UPDATE products SET stock=%.3f,updated_at=CURRENT_TIMESTAMP WHERE id=%d; INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'VENDA','VENDA',%s,%.3f,%.3f,%.3f,1,'Venda %s');", sid, it.ID, esc(it.Desc), it.Qty, it.Price, it.Cost, line, lc, lp, after, it.ID, it.ID, sid, -it.Qty, before, after, esc(saleNo))
			if err = execSQL(qs); err != nil {
				ok = false
				break
			}
		}
	}
	if ok {
		fee, feeType, feeValue := paymentFeeFor(pay, total)
		netReceived := total - fee
		feePercent := 0.0
		if feeType == "PERCENT" {
			feePercent = feeValue
		}
		execSQL(fmt.Sprintf("INSERT INTO sale_payments(sale_id,payment_type,gross_amount,discount_percent,discount_amount,net_amount,reference) VALUES(%s,'%s',%.2f,%.4f,%.2f,%.2f,'TAXA_%s_%.4f')", sid, pay, total, feePercent, fee, netReceived, esc(feeType), feeValue))
		execSQL("COMMIT")
		cart = nil
		refreshCart()
		msg("Venda finalizada com sucesso.\nNúmero: " + saleNo)
		pSetFocus.Call(pdvBarcode)
	} else {
		execSQL("ROLLBACK")
		msgErr("Venda não concluída: " + err.Error())
	}
}

func showProdutos() {
	if !ensureDB() {
		return
	}
	currentModule = "produtos"
	clearContent()
	header("Cadastro de produtos", "Cadastre manualmente, pelo código de barras, importe ou exporte planilhas — referência Armazém Gratidão 12.1.5")
	prodSearch = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 18, 125, 390, 34, 2201)
	subclassEdit(prodSearch)
	add("BUTTON", "Buscar", 0, 420, 122, 90, 40, 2202)
	add("BUTTON", "Atualizar", 0, 520, 122, 100, 40, 2203)
	add("BUTTON", "Importar Excel", 0, 630, 122, 125, 40, 2206)
	add("BUTTON", "Exportar Excel", 0, 765, 122, 125, 40, 2207)
	add("BUTTON", "Estoque", 0, 900, 122, 115, 40, 104)
	add("BUTTON", "Validade", 0, 1135, 646, 115, 40, 109)
	prodList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT|0x00100000, 18, 175, 1232, 285, 2204)
	pSendMessageW.Call(prodList, WM_SETFONT, fontMono, 1)
	loadProducts("")
	section("Cadastrar / inserir produto", 250, 475, 650)
	add("STATIC", "Código de barras", 0, 250, 512, 145, 22, 0)
	prodBarcodeReg = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 250, 536, 215, 32, 2210)
	subclassEdit(prodBarcodeReg)
	add("BUTTON", "Buscar e preencher", 0, 475, 533, 150, 38, 2211)
	add("STATIC", "Descrição", 0, 640, 512, 110, 22, 0)
	prodDescReg = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL, 640, 536, 330, 32, 2212)
	add("STATIC", "Marca", 0, 985, 512, 90, 22, 0)
	prodBrandReg = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL, 985, 536, 265, 32, 2213)
	add("STATIC", "Categoria", 0, 250, 579, 100, 22, 0)
	prodCategoryReg = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL, 250, 603, 210, 32, 2214)
	add("STATIC", "Custo", 0, 475, 579, 70, 22, 0)
	prodCostReg = add("EDIT", "0,00", WS_BORDER|ES_AUTOHSCROLL, 475, 603, 100, 32, 2215)
	add("STATIC", "Venda", 0, 590, 579, 70, 22, 0)
	prodPriceReg = add("EDIT", "0,00", WS_BORDER|ES_AUTOHSCROLL, 590, 603, 100, 32, 2216)
	add("STATIC", "Estoque", 0, 705, 579, 80, 22, 0)
	prodStockReg = add("EDIT", "0", WS_BORDER|ES_AUTOHSCROLL, 705, 603, 100, 32, 2217)
	add("STATIC", "Unidade", 0, 820, 579, 80, 22, 0)
	prodUnitReg = add("COMBOBOX", "", CBS_DROPDOWNLIST|WS_VSCROLL, 820, 598, 120, 150, 2218)
	for _, u := range []string{"UN", "KG", "LT", "CX", "PCT"} {
		pSendMessageW.Call(prodUnitReg, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(ws(u))))
	}
	pSendMessageW.Call(prodUnitReg, CB_SETCURSEL, 0, 0)
	add("BUTTON", "SALVAR PRODUTO", 0, 970, 596, 180, 42, 2219)
	add("BUTTON", "LIMPAR", 0, 1160, 596, 90, 42, 2220)
	add("BUTTON", "Migrar Produtos + Estoque + Validade 12.1.5", 0, 865, 646, 260, 40, 2221)
	add("STATIC", "Ao informar um código novo, a busca online é feita sem abrir navegador. Confira os dados antes de salvar.", 0, 250, 652, 700, 28, 0)
	pSetFocus.Call(prodSearch)
}
func loadProducts(term string) {
	if prodList == 0 {
		return
	}
	listReset(prodList)
	listAdd(prodList, fmt.Sprintf("%-8s %-14s %-30s %-12s %-12s %-5s %9s %9s %9s %7s %-8s %-10s",
		"CÓDIGO", "CÓD.BARRAS", "DESCRIÇÃO", "MARCA", "CATEGORIA", "UN", "CUSTO", "PREÇO", "ESTOQUE", "MÍN.", "STATUS", "VALIDADE"))
	q := "SELECT COALESCE(internal_code,''),COALESCE(barcode,''),description,COALESCE(brand,''),COALESCE(category,''),unit,cost,price,stock,min_stock,CASE WHEN active=1 THEN 'ATIVO' ELSE 'INATIVO' END,COALESCE(expiration_date,'') FROM products WHERE 1=1"
	if strings.TrimSpace(term) != "" {
		t := esc(strings.TrimSpace(term))
		q += " AND (barcode='" + t + "' OR internal_code='" + t + "' OR description LIKE '%" + t + "%' OR COALESCE(brand,'') LIKE '%" + t + "%')"
	}
	q += " ORDER BY description LIMIT 1000"
	rows, e := queryRows(q, 12)
	if e != nil {
		msgErr(e.Error())
		return
	}
	for _, r := range rows {
		listAdd(prodList, fmt.Sprintf("%-8s %-14s %-30s %-12s %-12s %-5s %9.2f %9.2f %9.3f %7.3f %-8s %-10s",
			clip(r[0], 8), clip(r[1], 14), clip(r[2], 30), clip(r[3], 12), clip(r[4], 12), clip(r[5], 5), parseF(r[6]), parseF(r[7]), parseF(r[8]), parseF(r[9]), clip(r[10], 8), clip(r[11], 10)))
	}
}
func clearProductForm() {
	for _, h := range []uintptr{prodBarcodeReg, prodDescReg, prodBrandReg, prodCategoryReg} {
		if h != 0 {
			setText(h, "")
		}
	}
	if prodCostReg != 0 {
		setText(prodCostReg, "0,00")
	}
	if prodPriceReg != 0 {
		setText(prodPriceReg, "0,00")
	}
	if prodStockReg != 0 {
		setText(prodStockReg, "0")
	}
	if prodUnitReg != 0 {
		pSendMessageW.Call(prodUnitReg, CB_SETCURSEL, 0, 0)
	}
}
func newProductQuick() { clearProductForm(); pSetFocus.Call(prodBarcodeReg) }
func selectedComboText(h uintptr, opts []string) string {
	idx, _, _ := pSendMessageW.Call(h, CB_GETCURSEL, 0, 0)
	if int(idx) >= 0 && int(idx) < len(opts) {
		return opts[int(idx)]
	}
	return opts[0]
}
func saveProductRegistration() {
	barcode := strings.TrimSpace(getText(prodBarcodeReg))
	desc := strings.TrimSpace(getText(prodDescReg))
	if barcode == "" {
		msgErr("Informe o código de barras.")
		return
	}
	if desc == "" {
		msgErr("Informe a descrição do produto.")
		return
	}
	brand := strings.TrimSpace(getText(prodBrandReg))
	cat := strings.TrimSpace(getText(prodCategoryReg))
	cost := parseF(getText(prodCostReg))
	price := parseF(getText(prodPriceReg))
	stock := parseF(getText(prodStockReg))
	if stock < 0 {
		stock = 0
	}
	unit := selectedComboText(prodUnitReg, []string{"UN", "KG", "LT", "CX", "PCT"})
	existing := scalar("SELECT id FROM products WHERE barcode='" + esc(barcode) + "' LIMIT 1")
	if existing != "" {
		q := fmt.Sprintf("UPDATE products SET description='%s',brand='%s',category='%s',unit='%s',cost=%.2f,price=%.2f,active=1,updated_at=CURRENT_TIMESTAMP WHERE id=%s", esc(desc), esc(brand), esc(cat), unit, cost, price, existing)
		if e := execSQL(q); e != nil {
			msgErr(e.Error())
			return
		}
		before := parseF(scalar("SELECT stock FROM products WHERE id=" + existing))
		diff := stock - before
		if abs(diff) > 0.0001 {
			execSQL(fmt.Sprintf("UPDATE products SET stock=%.3f WHERE id=%s; INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%s,'AJUSTE_CADASTRO','PRODUTO',%.3f,%.3f,%.3f,1,'Cadastro/edição de produto');", stock, existing, existing, diff, before, stock))
		}
		msg("Produto atualizado.")
	} else {
		n := scalar("SELECT COALESCE(MAX(id),0)+1 FROM products")
		q := fmt.Sprintf("INSERT INTO products(barcode,internal_code,description,brand,category,unit,cost,price,stock,min_stock,active) VALUES('%s','P%s','%s','%s','%s','%s',%.2f,%.2f,%.3f,0,1)", esc(barcode), n, esc(desc), esc(brand), esc(cat), unit, cost, price, stock)
		if e := execSQL(q); e != nil {
			msgErr(e.Error())
			return
		}
		pid := scalar("SELECT last_insert_rowid()")
		if stock > 0 {
			execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%s,'ENTRADA_CADASTRO','PRODUTO',%.3f,0,%.3f,1,'Estoque inicial do cadastro')", pid, stock, stock))
		}
		msg("Produto cadastrado e inserido no sistema.")
	}
	clearProductForm()
	loadProducts("")
}
func abs(v float64) float64 {
	if v < 0 {
		return -v
	}
	return v
}

func openLegacyDBFile() string {
	buf := make([]uint16, 1024)
	filter, _ := syscall.UTF16PtrFromString("Banco Armazém Gratidão (*.db;*.sqlite)\x00*.db;*.sqlite\x00Todos os arquivos\x00*.*\x00\x00")
	title := ws("Selecione o banco do Armazém Gratidão 12.1.5")
	of := OPENFILENAME{lStructSize: uint32(unsafe.Sizeof(OPENFILENAME{})), hwndOwner: mainWnd, lpstrFilter: filter, nFilterIndex: 1, lpstrFile: &buf[0], nMaxFile: uint32(len(buf)), lpstrTitle: title, Flags: 0x00001000 | 0x00000800 | 0x00080000}
	r, _, _ := pGetOpenFileNameW.Call(uintptr(unsafe.Pointer(&of)))
	if r == 0 {
		return ""
	}
	return syscall.UTF16ToString(buf)
}
func findArmazem1215DB() string {
	appdata := os.Getenv("APPDATA")
	local := os.Getenv("LOCALAPPDATA")
	candidates := []string{
		filepath.Join(appdata, "Armazem Gratidao", "Universal", "armazem-gratidao.db"),
		filepath.Join(appdata, "Armazem Gratidao", "Dados", "armazem-gratidao.db"),
		filepath.Join(appdata, "armazem-gratidao-pdv", "armazem-gratidao.db"),
		filepath.Join(local, "Armazem Gratidao", "Universal", "armazem-gratidao.db"),
		filepath.Join(local, "armazem-gratidao-pdv", "armazem-gratidao.db"),
	}
	for _, f := range candidates {
		if f != "" {
			if st, e := os.Stat(f); e == nil && !st.IsDir() {
				return f
			}
		}
	}
	return ""
}
func migrateArmazem1215Stock() {
	if !ensureDB() {
		return
	}
	source := findArmazem1215DB()
	if source == "" {
		source = openLegacyDBFile()
	}
	if source == "" {
		return
	}
	if filepath.Clean(source) == filepath.Clean(dbPath) {
		msgErr("Selecione o banco do Armazém Gratidão 12.1.5, não o banco atual do ERP.")
		return
	}
	msg("Migração preparada.\n\nFeche o Armazém Gratidão 12.1.5 durante a migração.\nO ERP fará backup antes de alterar o estoque.")
	go migrateArmazem1215StockWorker(source)
}
func migrateArmazem1215StockWorker(source string) {
	dbMu.Lock()
	defer dbMu.Unlock()
	backupDir := filepath.Join(root, "backups")
	os.MkdirAll(backupDir, 0755)
	backup := filepath.Join(backupDir, "erp_antes_migracao_12.1.5_"+time.Now().Format("20060102_150405")+".sqlite")
	_ = execSQL("PRAGMA wal_checkpoint(FULL)")
	if b, e := os.ReadFile(dbPath); e == nil {
		_ = os.WriteFile(backup, b, 0644)
	}
	_ = execSQL("DETACH DATABASE legacy")
	if e := execSQL("ATTACH DATABASE '" + esc(source) + "' AS legacy"); e != nil {
		msgErr("Não foi possível abrir o banco 12.1.5:\n" + e.Error())
		return
	}
	defer execSQL("DETACH DATABASE legacy")
	// A 12.1.5 usa products.name; o ERP Gratidão usa products.description.
	rows, e := queryRows("SELECT COALESCE(barcode,''),COALESCE(internal_code,''),COALESCE(name,''),COALESCE(brand,''),COALESCE(category,''),COALESCE(unit,'UN'),COALESCE(cost,0),COALESCE(price,0),COALESCE(stock,0),COALESCE(status,'ATIVO'),COALESCE(min_stock,0),COALESCE(expiration_date,''),COALESCE(layout_version,'AG-PRODUTOS-1.0'),COALESCE(photo_url,''),COALESCE(ncm,''),COALESCE(cest,''),COALESCE(cfop,''),COALESCE(cst_csosn,''),COALESCE(origin,''),COALESCE(tax_icms,0),COALESCE(tax_pis,0),COALESCE(tax_cofins,0),COALESCE(gtin_tributable,''),COALESCE(tributary_unit,''),COALESCE(cnae,''),COALESCE(promotion_active,0),COALESCE(promotion_discount_percent,0),COALESCE(promotion_price,0),COALESCE(sold_by_weight,0) FROM legacy.products ORDER BY id", 29)
	if e != nil {
		msgErr("Banco selecionado não é compatível com o Armazém Gratidão 12.1.5:\n" + e.Error())
		return
	}
	if e = execSQL("BEGIN IMMEDIATE"); e != nil {
		msgErr(e.Error())
		return
	}
	ok := true
	imported := 0
	updated := 0
	movements := 0
	for _, r := range rows {
		barcode := strings.TrimSpace(r[0])
		internal := strings.TrimSpace(r[1])
		desc := strings.TrimSpace(r[2])
		if desc == "" {
			continue
		}
		brand := r[3]
		cat := r[4]
		unit := strings.TrimSpace(r[5])
		if unit == "" {
			unit = "UN"
		}
		cost := parseF(r[6])
		price := parseF(r[7])
		stock := parseF(r[8])
		if stock < 0 {
			stock = 0
		}
		minStock := parseF(r[10])
		expiration := strings.TrimSpace(r[11])
		layout := r[12]
		photo := r[13]
		ncm := r[14]
		cest := r[15]
		cfop := r[16]
		cst := r[17]
		origin := r[18]
		icms := parseF(r[19])
		pis := parseF(r[20])
		cofins := parseF(r[21])
		gtin := r[22]
		tribUnit := r[23]
		cnae := r[24]
		promoActive := int(parseF(r[25]))
		promoDiscount := parseF(r[26])
		promoPrice := parseF(r[27])
		soldWeight := int(parseF(r[28]))
		active := 1
		if strings.EqualFold(strings.TrimSpace(r[9]), "INATIVO") && stock <= 0 {
			active = 0
		}
		var existing string
		if barcode != "" {
			existing = scalar("SELECT id FROM products WHERE barcode='" + esc(barcode) + "' LIMIT 1")
		}
		if existing == "" && internal != "" {
			existing = scalar("SELECT id FROM products WHERE internal_code='" + esc(internal) + "' LIMIT 1")
		}
		if existing != "" {
			before := parseF(scalar("SELECT stock FROM products WHERE id=" + existing))
			diff := stock - before
			q := fmt.Sprintf("UPDATE products SET barcode=CASE WHEN '%s'<>'' THEN '%s' ELSE barcode END,internal_code=CASE WHEN '%s'<>'' THEN '%s' ELSE internal_code END,description='%s',brand='%s',category='%s',unit='%s',cost=%.2f,price=%.2f,stock=%.3f,min_stock=%.3f,active=%d,expiration_date=%s,layout_version='%s',photo_url='%s',ncm='%s',cest='%s',cfop='%s',cst_csosn='%s',origin='%s',tax_icms=%.4f,tax_pis=%.4f,tax_cofins=%.4f,gtin_tributable='%s',tributary_unit='%s',cnae='%s',promotion_active=%d,promotion_discount_percent=%.4f,promotion_price=%.2f,sold_by_weight=%d,updated_at=CURRENT_TIMESTAMP WHERE id=%s", esc(barcode), esc(barcode), esc(internal), esc(internal), esc(desc), esc(brand), esc(cat), esc(unit), cost, price, stock, minStock, active, sqlNullable(expiration), esc(layout), esc(photo), esc(ncm), esc(cest), esc(cfop), esc(cst), esc(origin), icms, pis, cofins, esc(gtin), esc(tribUnit), esc(cnae), promoActive, promoDiscount, promoPrice, soldWeight, existing)
			if e = execSQL(q); e != nil {
				ok = false
				break
			}
			if abs(diff) > 0.0001 {
				e = execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%s,'MIGRACAO_12_1_5','MIGRACAO',%.3f,%.3f,%.3f,1,'Migração de estoque do Armazém Gratidão 12.1.5')", existing, diff, before, stock))
				if e != nil {
					ok = false
					break
				}
				movements++
			}
			updated++
		} else {
			q := fmt.Sprintf("INSERT INTO products(barcode,internal_code,description,brand,category,unit,cost,price,stock,min_stock,active,expiration_date,layout_version,photo_url,ncm,cest,cfop,cst_csosn,origin,tax_icms,tax_pis,tax_cofins,gtin_tributable,tributary_unit,cnae,promotion_active,promotion_discount_percent,promotion_price,sold_by_weight) VALUES(%s,%s,'%s','%s','%s','%s',%.2f,%.2f,%.3f,%.3f,%d,%s,'%s','%s','%s','%s','%s','%s','%s',%.4f,%.4f,%.4f,'%s','%s','%s',%d,%.4f,%.2f,%d)", sqlNullable(barcode), sqlNullable(internal), esc(desc), esc(brand), esc(cat), esc(unit), cost, price, stock, minStock, active, sqlNullable(expiration), esc(layout), esc(photo), esc(ncm), esc(cest), esc(cfop), esc(cst), esc(origin), icms, pis, cofins, esc(gtin), esc(tribUnit), esc(cnae), promoActive, promoDiscount, promoPrice, soldWeight)
			if e = execSQL(q); e != nil {
				ok = false
				break
			}
			pid := scalar("SELECT last_insert_rowid()")
			if stock > 0 {
				e = execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%s,'MIGRACAO_12_1_5','MIGRACAO',%.3f,0,%.3f,1,'Produto e estoque migrados do Armazém Gratidão 12.1.5')", pid, stock, stock))
				if e != nil {
					ok = false
					break
				}
				movements++
			}
			imported++
		}
	}
	if !ok {
		execSQL("ROLLBACK")
		msgErr("Migração cancelada sem aplicar alterações:\n" + e.Error())
		return
	}
	if e = execSQL("COMMIT"); e != nil {
		execSQL("ROLLBACK")
		msgErr("Falha ao concluir migração:\n" + e.Error())
		return
	}
	msg(fmt.Sprintf("Migração completa do Armazém Gratidão 12.1.5 concluída.\n\nProdutos novos: %d\nProdutos atualizados: %d\nMovimentos de estoque: %d\nValidades e dados completos dos produtos: migrados\n\nBackup: %s", imported, updated, movements, backup))
}
func sqlNullable(s string) string {
	if strings.TrimSpace(s) == "" {
		return "NULL"
	}
	return "'" + esc(s) + "'"
}

type onlineProduct struct {
	Status  int `json:"status"`
	Product struct {
		ProductName string `json:"product_name"`
		Brands      string `json:"brands"`
		Categories  string `json:"categories"`
		Quantity    string `json:"quantity"`
	} `json:"product"`
}

func lookupProductOnline() {
	barcode := strings.TrimSpace(getText(prodBarcodeReg))
	if barcode == "" {
		msgErr("Informe o código de barras.")
		return
	}
	rows, _ := queryRows("SELECT description,COALESCE(brand,''),COALESCE(category,''),price,cost,stock,unit FROM products WHERE barcode='"+esc(barcode)+"' LIMIT 1", 7)
	if len(rows) > 0 {
		setText(prodDescReg, rows[0][0])
		setText(prodBrandReg, rows[0][1])
		setText(prodCategoryReg, rows[0][2])
		setText(prodPriceReg, money(parseF(rows[0][3])))
		setText(prodCostReg, money(parseF(rows[0][4])))
		setText(prodStockReg, fmt.Sprintf("%.3f", parseF(rows[0][5])))
		msg("Produto já cadastrado. Dados carregados para edição.")
		return
	}
	msg("Código ainda não cadastrado. A busca automática será feita agora.")
	go func(code string) {
		client := &http.Client{Timeout: 8 * time.Second}
		req, _ := http.NewRequest("GET", "https://world.openfoodfacts.org/api/v2/product/"+code+".json", nil)
		req.Header.Set("User-Agent", "ERP-Gratidao/1.2.4")
		resp, e := client.Do(req)
		data := map[string]string{"barcode": code}
		if e == nil {
			defer resp.Body.Close()
			var op onlineProduct
			if json.NewDecoder(resp.Body).Decode(&op) == nil && op.Status == 1 {
				data["description"] = strings.TrimSpace(op.Product.ProductName)
				data["brand"] = strings.TrimSpace(op.Product.Brands)
				data["category"] = strings.TrimSpace(strings.Split(op.Product.Categories, ",")[0])
			}
		}
		lookupMu.Lock()
		lookupData = data
		if e != nil {
			lookupErr = e.Error()
		} else if data["description"] == "" {
			lookupErr = "Produto não encontrado na base pública. Preencha os dados manualmente."
		} else {
			lookupErr = ""
		}
		lookupMu.Unlock()
		if lookupErr != "" {
			pPostMessageW.Call(mainWnd, WM_APP_LOOKUPFAIL, 0, 0)
		} else {
			pPostMessageW.Call(mainWnd, WM_APP_LOOKUPDONE, 0, 0)
		}
	}(barcode)
}

func xmlEsc(s string) string { var b bytes.Buffer; xml.EscapeText(&b, []byte(s)); return b.String() }
func xlsxCol(n int) string {
	s := ""
	for n > 0 {
		n--
		s = string(rune('A'+n%26)) + s
		n /= 26
	}
	return s
}
func writeXlsx(path string, headers []string, rows [][]string) error {
	f, e := os.Create(path)
	if e != nil {
		return e
	}
	defer f.Close()
	z := zip.NewWriter(f)
	defer z.Close()
	files := map[string]string{
		"[Content_Types].xml":        `<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>`,
		"_rels/.rels":                `<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
		"xl/workbook.xml":            `<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Produtos" sheetId="1" r:id="rId1"/></sheets></workbook>`,
		"xl/_rels/workbook.xml.rels": `<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>`,
	}
	var sb strings.Builder
	sb.WriteString(`<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>`)
	all := append([][]string{headers}, rows...)
	for ri, row := range all {
		sb.WriteString(fmt.Sprintf(`<row r="%d">`, ri+1))
		for ci, v := range row {
			ref := xlsxCol(ci+1) + strconv.Itoa(ri+1)
			sb.WriteString(`<c r="` + ref + `" t="inlineStr"><is><t>` + xmlEsc(v) + `</t></is></c>`)
		}
		sb.WriteString(`</row>`)
	}
	sb.WriteString(`</sheetData></worksheet>`)
	files["xl/worksheets/sheet1.xml"] = sb.String()
	for name, data := range files {
		w, _ := z.Create(name)
		io.WriteString(w, data)
	}
	return nil
}
func exportProductsExcel() {
	desktop := filepath.Join(os.Getenv("USERPROFILE"), "Desktop")
	if _, e := os.Stat(desktop); e != nil {
		desktop = root
	}
	path := filepath.Join(desktop, "Produtos_ERP_Gratidao_"+time.Now().Format("20060102_150405")+".xlsx")
	rows, e := queryRows("SELECT COALESCE(internal_code,''),COALESCE(barcode,''),description,COALESCE(brand,''),COALESCE(category,''),unit,printf('%.2f',cost),printf('%.2f',price),printf('%.3f',stock),printf('%.3f',min_stock) FROM products ORDER BY description", 10)
	if e != nil {
		msgErr(e.Error())
		return
	}
	if e = writeXlsx(path, []string{"Codigo Interno", "Codigo de Barras", "Descricao", "Marca", "Categoria", "Unidade", "Custo", "Preco", "Estoque", "Estoque Minimo"}, rows); e != nil {
		msgErr(e.Error())
		return
	}
	msg("Produtos exportados para Excel:\n" + path)
}
func openExcelFile() string {
	buf := make([]uint16, 1024)
	filter, _ := syscall.UTF16PtrFromString("Planilhas Excel (*.xlsx;*.csv)\x00*.xlsx;*.csv\x00Todos os arquivos\x00*.*\x00\x00")
	title := ws("Importar produtos do Excel")
	of := OPENFILENAME{lStructSize: uint32(unsafe.Sizeof(OPENFILENAME{})), hwndOwner: mainWnd, lpstrFilter: filter, nFilterIndex: 1, lpstrFile: &buf[0], nMaxFile: uint32(len(buf)), lpstrTitle: title, Flags: 0x00001000 | 0x00000800 | 0x00080000}
	r, _, _ := pGetOpenFileNameW.Call(uintptr(unsafe.Pointer(&of)))
	if r == 0 {
		return ""
	}
	return syscall.UTF16ToString(buf)
}

type xCell struct {
	R  string `xml:"r,attr"`
	T  string `xml:"t,attr"`
	V  string `xml:"v"`
	IS struct {
		T string `xml:"t"`
	} `xml:"is"`
}
type xRow struct {
	C []xCell `xml:"c"`
}
type xSheet struct {
	SheetData struct {
		Row []xRow `xml:"row"`
	} `xml:"sheetData"`
}
type sharedStrings struct {
	SI []struct {
		T string `xml:"t"`
		R []struct {
			T string `xml:"t"`
		} `xml:"r"`
	} `xml:"si"`
}

func parseXlsx(path string) ([][]string, error) {
	z, e := zip.OpenReader(path)
	if e != nil {
		return nil, e
	}
	defer z.Close()
	var sh []byte
	ss := []string{}
	for _, f := range z.File {
		if f.Name == "xl/worksheets/sheet1.xml" {
			r, _ := f.Open()
			sh, _ = io.ReadAll(r)
			r.Close()
		} else if f.Name == "xl/sharedStrings.xml" {
			r, _ := f.Open()
			b, _ := io.ReadAll(r)
			r.Close()
			var x sharedStrings
			xml.Unmarshal(b, &x)
			for _, si := range x.SI {
				v := si.T
				for _, rr := range si.R {
					v += rr.T
				}
				ss = append(ss, v)
			}
		}
	}
	if len(sh) == 0 {
		return nil, fmt.Errorf("planilha sem aba Produtos")
	}
	var xs xSheet
	if e = xml.Unmarshal(sh, &xs); e != nil {
		return nil, e
	}
	out := [][]string{}
	for _, rr := range xs.SheetData.Row {
		row := []string{}
		for _, c := range rr.C {
			ci := 0
			for _, ch := range c.R {
				if ch >= 'A' && ch <= 'Z' {
					ci = ci*26 + int(ch-'A'+1)
				} else {
					break
				}
			}
			for len(row) < ci {
				row = append(row, "")
			}
			v := c.V
			if c.T == "s" {
				n, _ := strconv.Atoi(v)
				if n >= 0 && n < len(ss) {
					v = ss[n]
				}
			} else if c.T == "inlineStr" {
				v = c.IS.T
			}
			if ci > 0 {
				row[ci-1] = v
			}
		}
		out = append(out, row)
	}
	return out, nil
}
func parseCSV(path string) ([][]string, error) {
	f, e := os.Open(path)
	if e != nil {
		return nil, e
	}
	defer f.Close()
	r := csv.NewReader(bufio.NewReader(f))
	r.Comma = ';'
	rows, e := r.ReadAll()
	if e != nil {
		f.Seek(0, 0)
		r = csv.NewReader(bufio.NewReader(f))
		rows, e = r.ReadAll()
	}
	return rows, e
}
func normHeader(s string) string {
	s = strings.ToLower(strings.TrimSpace(s))
	repl := strings.NewReplacer("á", "a", "ã", "a", "â", "a", "é", "e", "ê", "e", "í", "i", "ó", "o", "ô", "o", "õ", "o", "ú", "u", "ç", "c", "_", " ", "-", " ")
	return strings.Join(strings.Fields(repl.Replace(s)), " ")
}
func importProductsExcel() {
	path := openExcelFile()
	if path == "" {
		return
	}
	var rows [][]string
	var e error
	if strings.HasSuffix(strings.ToLower(path), ".csv") {
		rows, e = parseCSV(path)
	} else {
		rows, e = parseXlsx(path)
	}
	if e != nil {
		msgErr("Falha ao ler planilha: " + e.Error())
		return
	}
	if len(rows) < 2 {
		msgErr("Planilha sem produtos.")
		return
	}
	h := map[string]int{}
	for i, v := range rows[0] {
		h[normHeader(v)] = i
	}
	get := func(r []string, names ...string) string {
		for _, n := range names {
			if i, ok := h[normHeader(n)]; ok && i < len(r) {
				return strings.TrimSpace(r[i])
			}
		}
		return ""
	}
	created, updated, adjusted := 0, 0, 0
	for _, r := range rows[1:] {
		desc := get(r, "Descricao", "Descrição", "Produto", "Nome")
		barcode := get(r, "Codigo de Barras", "Código de Barras", "EAN", "GTIN")
		internal := get(r, "Codigo Interno", "Código Interno", "Codigo")
		if desc == "" {
			continue
		}
		brand := get(r, "Marca")
		cat := get(r, "Categoria")
		unit := get(r, "Unidade")
		if unit == "" {
			unit = "UN"
		}
		cost := parseF(get(r, "Custo", "Preco de custo", "Preço de custo"))
		price := parseF(get(r, "Preco", "Preço", "Preco de venda", "Preço de venda"))
		stock := parseF(get(r, "Estoque", "Quantidade"))
		min := parseF(get(r, "Estoque Minimo", "Estoque Mínimo"))
		id := ""
		if barcode != "" {
			id = scalar("SELECT id FROM products WHERE barcode='" + esc(barcode) + "' LIMIT 1")
		}
		if id == "" && internal != "" {
			id = scalar("SELECT id FROM products WHERE internal_code='" + esc(internal) + "' LIMIT 1")
		}
		if id != "" {
			before := parseF(scalar("SELECT stock FROM products WHERE id=" + id))
			execSQL(fmt.Sprintf("UPDATE products SET barcode=COALESCE(NULLIF('%s',''),barcode),internal_code=COALESCE(NULLIF('%s',''),internal_code),description='%s',brand='%s',category='%s',unit='%s',cost=%.2f,price=%.2f,min_stock=%.3f,active=1,updated_at=CURRENT_TIMESTAMP WHERE id=%s", esc(barcode), esc(internal), esc(desc), esc(brand), esc(cat), esc(unit), cost, price, min, id))
			if abs(stock-before) > 0.0001 {
				execSQL(fmt.Sprintf("UPDATE products SET stock=%.3f WHERE id=%s; INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%s,'IMPORTACAO_EXCEL','PLANILHA',%.3f,%.3f,%.3f,1,'Importação Excel');", stock, id, id, stock-before, before, stock))
				adjusted++
			}
			updated++
		} else {
			if internal == "" {
				internal = "P" + scalar("SELECT COALESCE(MAX(id),0)+1 FROM products")
			}
			execSQL(fmt.Sprintf("INSERT INTO products(barcode,internal_code,description,brand,category,unit,cost,price,stock,min_stock,active) VALUES(NULLIF('%s',''),'%s','%s','%s','%s','%s',%.2f,%.2f,%.3f,%.3f,1)", esc(barcode), esc(internal), esc(desc), esc(brand), esc(cat), esc(unit), cost, price, stock, min))
			pid := scalar("SELECT last_insert_rowid()")
			if stock != 0 {
				execSQL(fmt.Sprintf("INSERT INTO inventory_movements(product_id,movement_type,origin_type,qty,stock_before,stock_after,user_id,reason) VALUES(%s,'IMPORTACAO_EXCEL','PLANILHA',%.3f,0,%.3f,1,'Importação Excel')", pid, stock, stock))
				adjusted++
			}
			created++
		}
	}
	loadProducts("")
	msg(fmt.Sprintf("Importação concluída.\nCadastrados: %d\nAtualizados: %d\nEstoque ajustado: %d", created, updated, adjusted))
}

func showEstoque() {
	if !ensureDB() {
		return
	}
	currentModule = "estoque"
	clearContent()
	header("Estoque", "Movimentação auditada — saldo anterior, entrada/saída e saldo final")
	add("BUTTON", "Atualizar", 0, 18, 130, 120, 40, 2302)
	add("BUTTON", "Produtos com estoque baixo", 0, 148, 130, 220, 40, 2303)
	add("BUTTON", "Produtos", 0, 378, 130, 120, 40, 103)
	stockList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOINTEGRALHEIGHT|0x00100000, 18, 195, 1232, 500, 2301)
	pSendMessageW.Call(stockList, WM_SETFONT, fontMono, 1)
	loadStock(false)
}
func loadStock(low bool) {
	listReset(stockList)
	listAdd(stockList, fmt.Sprintf("%-30s %9s %9s %10s %10s %10s %10s %9s %-8s %-10s",
		"PRODUTO", "QTD", "CUSTO UN.", "TOTAL CUSTO", "VENDA UN.", "TOTAL VENDA", "LUCRO EST.", "MÍNIMO", "STATUS", "VALIDADE"))
	where := "active=1"
	if low {
		where += " AND stock<=min_stock"
	}
	rows, e := queryRows("SELECT description,stock,cost,price,min_stock,CASE WHEN active=1 THEN 'ATIVO' ELSE 'INATIVO' END,COALESCE(expiration_date,'') FROM products WHERE "+where+" ORDER BY description LIMIT 1000", 7)
	if e != nil {
		msgErr(e.Error())
		return
	}
	for _, r := range rows {
		q := parseF(r[1])
		c := parseF(r[2])
		v := parseF(r[3])
		tc := q * c
		tv := q * v
		luc := tv - tc
		listAdd(stockList, fmt.Sprintf("%-30s %9.3f %9.2f %10.2f %10.2f %10.2f %10.2f %9.3f %-8s %-10s",
			clip(r[0], 30), q, c, tc, v, tv, luc, parseF(r[4]), clip(r[5], 8), clip(r[6], 10)))
	}
}

func showValidade() {
	if !ensureDB() {
		return
	}
	currentModule = "validade"
	clearContent()
	header("Controle de validade", "Procure pelo nome, código de barras ou categoria para localizar rapidamente a validade de cada item.")
	add("BUTTON", "Todos", 0, 250, 130, 90, 40, 2701)
	add("BUTTON", "Vencidos", 0, 350, 130, 100, 40, 2702)
	add("BUTTON", "Vencem em 30 dias", 0, 460, 130, 155, 40, 2703)
	add("BUTTON", "Vencem em 60 dias", 0, 625, 130, 155, 40, 2704)
	add("BUTTON", "Sem validade", 0, 790, 130, 120, 40, 2705)
	add("BUTTON", "Produtos", 0, 925, 130, 110, 40, 103)
	add("BUTTON", "Estoque", 0, 1045, 130, 110, 40, 104)
	stockList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOINTEGRALHEIGHT|0x00100000, 18, 195, 1232, 500, 2700)
	pSendMessageW.Call(stockList, WM_SETFONT, fontMono, 1)
	loadValidity("all")
}
func loadValidity(filter string) {
	listReset(stockList)
	listAdd(stockList, fmt.Sprintf("%-34s %-14s %-14s %9s %-10s %7s %-18s %-10s", "PRODUTO", "CÓDIGO", "CATEGORIA", "ESTOQUE", "VALIDADE", "DIAS", "SITUAÇÃO", "AÇÃO"))
	where := "1=1"
	switch filter {
	case "expired":
		where = "expiration_date IS NOT NULL AND expiration_date<>'' AND date(expiration_date)<date('now','localtime')"
	case "30":
		where = "expiration_date IS NOT NULL AND expiration_date<>'' AND date(expiration_date)>=date('now','localtime') AND date(expiration_date)<=date('now','localtime','+30 day')"
	case "60":
		where = "expiration_date IS NOT NULL AND expiration_date<>'' AND date(expiration_date)>=date('now','localtime') AND date(expiration_date)<=date('now','localtime','+60 day')"
	case "none":
		where = "expiration_date IS NULL OR expiration_date=''"
	}
	rows, e := queryRows("SELECT description,COALESCE(barcode,''),COALESCE(category,''),printf('%.3f',stock),COALESCE(expiration_date,''),CASE WHEN expiration_date IS NULL OR expiration_date='' THEN '' ELSE CAST(julianday(date(expiration_date))-julianday(date('now','localtime')) AS INTEGER) END,CASE WHEN expiration_date IS NULL OR expiration_date='' THEN 'SEM VALIDADE' WHEN date(expiration_date)<date('now','localtime') THEN 'VENCIDO' WHEN date(expiration_date)<=date('now','localtime','+30 day') THEN 'ATENÇÃO <=30 DIAS' ELSE 'OK' END FROM products WHERE "+where+" ORDER BY CASE WHEN expiration_date IS NULL OR expiration_date='' THEN 1 ELSE 0 END,expiration_date,description LIMIT 2000", 7)
	if e != nil {
		msgErr(e.Error())
		return
	}
	for _, r := range rows {
		acao := "CONSULTAR"
		listAdd(stockList, fmt.Sprintf("%-34s %-14s %-14s %9s %-10s %7s %-18s %-10s", clip(r[0], 34), clip(r[1], 14), clip(r[2], 14), r[3], clip(r[4], 10), r[5], clip(r[6], 18), acao))
	}
}

func showVendas() {
	if !ensureDB() {
		return
	}
	currentModule = "vendas"
	clearContent()
	header("Vendas", "Vendas do dia e anteriores em uma área de trabalho única")
	add("BUTTON", "Atualizar", 0, 18, 130, 110, 40, 2402)
	add("BUTTON", "EXCLUIR VENDA", 0, 140, 130, 205, 40, 2403)
	add("BUTTON", "Limpar vendas excluídas", 0, 357, 130, 190, 40, 2406)
	add("BUTTON", "Somente hoje", 0, 559, 130, 140, 40, 2404)
	add("BUTTON", "Todas as vendas", 0, 711, 130, 150, 40, 2405)
	salesList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT, 250, 195, 1000, 500, 2401)
	loadSales(false)
}
func loadSales(today bool) {
	listReset(salesList)
	salesIDs = nil
	q := "SELECT id,sale_number,datetime(created_at,'localtime'),customer_name,payment_method,printf('%.2f',total),status FROM sales WHERE status<>'EXCLUIDA_LIMPA'"
	if today {
		q += " AND date(created_at,'localtime')=date('now','localtime')"
	}
	q += " ORDER BY id DESC LIMIT 1000"
	rows, e := queryRows(q, 7)
	if e != nil {
		msgErr(e.Error())
		return
	}
	listAdd(salesList, "NÚMERO               DATA/HORA            CLIENTE                     PAGAMENTO       TOTAL          STATUS")
	for _, r := range rows {
		id, _ := strconv.ParseInt(r[0], 10, 64)
		salesIDs = append(salesIDs, id)
		listAdd(salesList, fmt.Sprintf("%-20s %-19s %-26s %-15s R$ %10s   %s", r[1], r[2], clip(r[3], 26), r[4], r[5], r[6]))
	}
}
func deleteSelectedSale() {
	idx, _, _ := pSendMessageW.Call(salesList, LB_GETCURSEL, 0, 0)
	i := int(idx) - 1
	if i < 0 || i >= len(salesIDs) {
		msg("Selecione uma venda.")
		return
	}
	sid := salesIDs[i]
	status := scalar(fmt.Sprintf("SELECT status FROM sales WHERE id=%d", sid))
	if status == "EXCLUIDA" {
		msg("Essa venda já está excluída.")
		return
	}
	if e := execSQL("BEGIN IMMEDIATE"); e != nil {
		msgErr(e.Error())
		return
	}
	items, e := queryRows(fmt.Sprintf("SELECT product_id,qty,product_description_snapshot FROM sale_items WHERE sale_id=%d", sid), 3)
	if e != nil {
		execSQL("ROLLBACK")
		msgErr(e.Error())
		return
	}
	ok := true
	for _, r := range items {
		pid, _ := strconv.ParseInt(r[0], 10, 64)
		qty := parseF(r[1])
		before := parseF(scalar(fmt.Sprintf("SELECT stock FROM products WHERE id=%d", pid)))
		after := before + qty
		q := fmt.Sprintf("UPDATE products SET stock=%.3f,updated_at=CURRENT_TIMESTAMP WHERE id=%d; INSERT INTO inventory_movements(product_id,movement_type,origin_type,origin_id,qty,stock_before,stock_after,user_id,reason) VALUES(%d,'ESTORNO_VENDA','VENDA_EXCLUIDA',%d,%.3f,%.3f,%.3f,1,'Exclusão da venda');", after, pid, pid, sid, qty, before, after)
		if execSQL(q) != nil {
			ok = false
			break
		}
	}
	if ok {
		e = execSQL(fmt.Sprintf("UPDATE sales SET status='EXCLUIDA',deleted_at=CURRENT_TIMESTAMP,updated_at=CURRENT_TIMESTAMP WHERE id=%d", sid))
		ok = e == nil
	}
	if ok {
		execSQL("COMMIT")
		msg("Venda excluída e estoque estornado.")
		loadSales(false)
	} else {
		execSQL("ROLLBACK")
		msgErr("Não foi possível excluir a venda.")
	}
}

func clearDeletedSales() {
	count := scalar("SELECT COUNT(*) FROM sales WHERE status='EXCLUIDA'")
	if count == "" || count == "0" {
		msg("Não existem vendas excluídas para limpar.")
		return
	}
	// LIMPAR não apaga fisicamente a venda: preserva auditoria, itens e estornos.
	// Apenas retira vendas já excluídas da listagem operacional.
	if e := execSQL("UPDATE sales SET status='EXCLUIDA_LIMPA',updated_at=CURRENT_TIMESTAMP WHERE status='EXCLUIDA'"); e != nil {
		msgErr(e.Error())
		return
	}
	msg(count + " venda(s) excluída(s) foram removidas da lista. O histórico de auditoria foi preservado.")
	loadSales(false)
}

func showCaixa() {
	if !ensureDB() {
		return
	}
	currentModule = "caixa"
	clearContent()
	header("Caixa", "Abertura, acompanhamento e fechamento do caixa")
	status := scalar("SELECT status FROM cash_sessions ORDER BY id DESC LIMIT 1")
	if status == "" {
		status = "SEM CAIXA"
	}
	section("Situação atual: "+status, 250, 140, 600)
	add("BUTTON", "Abrir caixa", 0, 250, 200, 160, 45, 2501)
	add("BUTTON", "Fechar caixa", 0, 425, 200, 160, 45, 2502)
	add("BUTTON", "Atualizar", 0, 600, 200, 130, 45, 108)
	cashList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOINTEGRALHEIGHT, 250, 280, 1000, 410, 2503)
	rows, _ := queryRows("SELECT id,datetime(opened_at,'localtime'),printf('%.2f',opening_amount),COALESCE(datetime(closed_at,'localtime'),''),COALESCE(printf('%.2f',closing_amount),''),status FROM cash_sessions ORDER BY id DESC LIMIT 100", 6)
	for _, r := range rows {
		listAdd(cashList, fmt.Sprintf("#%-5s | abertura %s | R$ %8s | fechamento %-19s | R$ %8s | %s", r[0], r[1], r[2], r[3], r[4], r[5]))
	}
}
func openCash() {
	if scalar("SELECT COUNT(*) FROM cash_sessions WHERE status='ABERTO'") != "0" {
		msg("Já existe um caixa aberto.")
		return
	}
	if e := execSQL("INSERT INTO cash_sessions(opening_amount,status) VALUES(0,'ABERTO')"); e != nil {
		msgErr(e.Error())
		return
	}
	msg("Caixa aberto com sucesso.")
	showCaixa()
}
func closeCash() {
	id := scalar("SELECT id FROM cash_sessions WHERE status='ABERTO' ORDER BY id DESC LIMIT 1")
	if id == "" {
		msg("Não existe caixa aberto.")
		return
	}
	rev := parseF(scalar("SELECT COALESCE(SUM(total),0) FROM sales WHERE status='CONCLUIDA' AND created_at >= (SELECT opened_at FROM cash_sessions WHERE id=" + id + ")"))
	if e := execSQL(fmt.Sprintf("UPDATE cash_sessions SET closed_at=CURRENT_TIMESTAMP,closing_amount=%.2f,status='FECHADO' WHERE id=%s", rev, id)); e != nil {
		msgErr(e.Error())
		return
	}
	msg("Caixa fechado.\nTotal de vendas no período: R$ " + money(rev))
	showCaixa()
}

func showFiado() {
	if !ensureDB() {
		return
	}
	currentModule = "fiado"
	clearContent()
	header("Fiado", "Mesmo fluxo do Armazém Gratidão, com recebimento parcial e alteração de cliente")
	add("STATIC", "Buscar cliente / venda", 0, 250, 122, 180, 22, 0)
	fiadoSearch = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 250, 146, 260, 32, 2610)
	subclassEdit(fiadoSearch)
	add("BUTTON", "Buscar", 0, 520, 143, 90, 38, 2611)
	add("BUTTON", "Em aberto", 0, 620, 143, 105, 38, 2612)
	add("BUTTON", "Compras pagas", 0, 735, 143, 125, 38, 2613)
	add("BUTTON", "Atualizar", 0, 870, 143, 105, 38, 2602)
	fiadoList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOTIFY|LBS_NOINTEGRALHEIGHT, 250, 195, 1000, 300, 2601)
	add("STATIC", "Valor a receber", 0, 250, 510, 125, 22, 0)
	fiadoAmount = add("EDIT", "0,00", WS_BORDER|ES_AUTOHSCROLL, 250, 534, 120, 32, 2620)
	add("STATIC", "Forma", 0, 385, 510, 70, 22, 0)
	fiadoMethod = add("COMBOBOX", "", CBS_DROPDOWNLIST|WS_VSCROLL, 385, 529, 165, 180, 2621)
	for _, m := range []string{"DINHEIRO", "PIX", "DEBITO", "CREDITO"} {
		pSendMessageW.Call(fiadoMethod, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(ws(m))))
	}
	pSendMessageW.Call(fiadoMethod, CB_SETCURSEL, 0, 0)
	add("BUTTON", "RECEBER SELECIONADA", 0, 565, 528, 190, 40, 2622)
	add("BUTTON", "RECEBER TODAS DO CLIENTE", 0, 765, 528, 220, 40, 2623)
	add("STATIC", "Cliente da venda selecionada", 0, 250, 585, 200, 22, 0)
	fiadoCustomerEdit = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL, 250, 609, 360, 32, 2630)
	add("BUTTON", "SALVAR CLIENTE", 0, 625, 606, 145, 38, 2631)
	add("STATIC", "Melhorias: pagamentos parciais, histórico de pagas, recebimento de todas as compras do cliente e correção do nome após a venda.", 0, 250, 660, 1000, 28, 0)
	loadFiado()
}
func loadFiado() {
	if fiadoList == 0 {
		return
	}
	listReset(fiadoList)
	fiadoIDs = nil
	term := ""
	if fiadoSearch != 0 {
		term = strings.TrimSpace(getText(fiadoSearch))
	}
	q := `SELECT s.id,s.sale_number,datetime(s.created_at,'localtime'),s.customer_name,printf('%.2f',s.total),printf('%.2f',COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0)),printf('%.2f',MAX(0,s.total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0))) FROM sales s WHERE s.payment_method='FIADO' AND s.status<>'EXCLUIDA'`
	if fiadoMode == "ABERTO" {
		q += ` AND (s.total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0))>0.005`
	} else {
		q += ` AND (s.total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0))<=0.005`
	}
	if term != "" {
		t := esc(term)
		q += ` AND (s.customer_name LIKE '%` + t + `%' OR s.sale_number LIKE '%` + t + `%')`
	}
	q += ` ORDER BY s.customer_name,s.id DESC LIMIT 1000`
	rows, e := queryRows(q, 7)
	if e != nil {
		msgErr(e.Error())
		return
	}
	listAdd(fiadoList, "VENDA             DATA/HORA            CLIENTE                      TOTAL       PAGO        SALDO")
	for _, r := range rows {
		id, _ := strconv.ParseInt(r[0], 10, 64)
		fiadoIDs = append(fiadoIDs, id)
		listAdd(fiadoList, fmt.Sprintf("%-17s %-19s %-27s R$ %8s  R$ %8s  R$ %8s", r[1], r[2], clip(r[3], 27), r[4], r[5], r[6]))
	}
}
func selectedFiadoID() int64 {
	idx, _, _ := pSendMessageW.Call(fiadoList, LB_GETCURSEL, 0, 0)
	i := int(idx) - 1
	if i < 0 || i >= len(fiadoIDs) {
		return 0
	}
	return fiadoIDs[i]
}
func receiveFiado(allClient bool) {
	sid := selectedFiadoID()
	if sid == 0 {
		msg("Selecione uma compra fiado.")
		return
	}
	amount := parseF(getText(fiadoAmount))
	method := selectedComboText(fiadoMethod, []string{"DINHEIRO", "PIX", "DEBITO", "CREDITO"})
	customer := scalar(fmt.Sprintf("SELECT customer_name FROM sales WHERE id=%d", sid))
	if allClient {
		rows, _ := queryRows("SELECT s.id,printf('%.2f',MAX(0,s.total-COALESCE((SELECT SUM(amount) FROM credit_payments cp WHERE cp.sale_id=s.id),0))) FROM sales s WHERE s.payment_method='FIADO' AND s.status<>'EXCLUIDA' AND s.customer_name='"+esc(customer)+"'", 2)
		total := 0.0
		for _, r := range rows {
			bal := parseF(r[1])
			if bal > 0.005 {
				execSQL(fmt.Sprintf("INSERT INTO credit_payments(sale_id,amount,payment_method,customer_name) VALUES(%s,%.2f,'%s','%s')", r[0], bal, method, esc(customer)))
				total += bal
			}
		}
		msg(fmt.Sprintf("Todas as compras em aberto de %s foram recebidas.\nTotal: R$ %s", customer, money(total)))
		loadFiado()
		return
	}
	balance := parseF(scalar(fmt.Sprintf("SELECT MAX(0,total-COALESCE((SELECT SUM(amount) FROM credit_payments WHERE sale_id=%d),0)) FROM sales WHERE id=%d", sid, sid)))
	if amount <= 0 {
		amount = balance
	}
	if amount > balance {
		amount = balance
	}
	if amount <= 0 {
		msg("Essa compra já está paga.")
		return
	}
	if e := execSQL(fmt.Sprintf("INSERT INTO credit_payments(sale_id,amount,payment_method,customer_name) VALUES(%d,%.2f,'%s','%s')", sid, amount, method, esc(customer))); e != nil {
		msgErr(e.Error())
		return
	}
	msg("Recebimento registrado: R$ " + money(amount))
	loadFiado()
}
func saveFiadoCustomer() {
	sid := selectedFiadoID()
	if sid == 0 {
		msg("Selecione uma compra fiado.")
		return
	}
	name := strings.TrimSpace(getText(fiadoCustomerEdit))
	if name == "" {
		msgErr("Informe o nome do cliente.")
		return
	}
	if e := execSQL(fmt.Sprintf("UPDATE sales SET customer_name='%s',updated_at=CURRENT_TIMESTAMP WHERE id=%d", esc(name), sid)); e != nil {
		msgErr(e.Error())
		return
	}
	msg("Cliente atualizado no Fiado e na venda.")
	loadFiado()
}
func showSimple(title, sub string) {
	clearContent()
	header(title, sub)
	section("Módulo preparado na nova navegação", 250, 150, 750)
	add("STATIC", "A estrutura foi trazida da referência 12.1.5. As operações específicas deste módulo serão implementadas na camada nativa, sem web.", 0, 250, 200, 970, 50, 0)
}
func showConsulta() {
	if !ensureDB() {
		return
	}
	clearContent()
	header("Consulta de Produto", "Consulta global inspirada na função da versão 12.1.5")
	prodSearch = add("EDIT", "", WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 250, 140, 500, 38, 2201)
	subclassEdit(prodSearch)
	add("BUTTON", "Buscar", 0, 765, 137, 120, 44, 2202)
	prodList = add("LISTBOX", "", WS_BORDER|WS_VSCROLL|LBS_NOINTEGRALHEIGHT, 250, 205, 1000, 480, 2204)
	loadProducts("")
	pSetFocus.Call(prodSearch)
}

func handleCommand(id int) {
	if id >= 3102 && id <= 3123 && (id-3102)%3 == 0 {
		savePaymentFee(id)
		return
	}
	if id == 99 {
		toggleMenu()
		return
	}
	if id >= 101 && id <= 114 {
		setMenuVisible(false)
	}
	switch id {
	case 9001:
		showUpdater()
	case 9002:
		showConsulta()
	case 9003:
		clearContent()
		header("Configurações", "Empresa, usuários, permissões, backup e manutenção")
		section("Financeiro e manutenção", 250, 140, 700)
		add("BUTTON", "Taxas de Cartões / PIX", 0, 255, 195, 220, 48, 3001)
		add("BUTTON", "Atualizações", 0, 490, 195, 180, 48, 3002)
	case 101:
		showInicio()
	case 102:
		showPDV()
	case 103:
		showProdutos()
	case 104:
		showEstoque()
	case 105:
		showVendas()
	case 106:
		showFiado()
	case 107:
		showConsulta()
	case 108:
		showCaixa()
	case 109:
		showValidade()
	case 110:
		showSimple("Compras", "Entrada de compras, itens, fornecedor e valores")
	case 111:
		showSimple("Clientes / Fornecedores", "Cadastros e consulta de parceiros")
	case 112:
		showSimple("Lucro", "Margem, lucro bruto e dízimo sobre o lucro")
	case 113:
		showSimple("Relatórios", "Vendas, estoque, caixa, lucro e fiado")
	case 114:
		clearContent()
		header("Configurações", "Empresa, usuários, permissões, backup e manutenção")
		section("Financeiro e manutenção", 250, 140, 700)
		add("BUTTON", "Taxas de Cartões / PIX", 0, 255, 195, 220, 48, 3001)
		add("BUTTON", "Atualizações", 0, 490, 195, 180, 48, 3002)
	case 3001:
		showPaymentFees()
	case 3002:
		showUpdater()
	case 3202:
		saveUpdateURL()
	case 3203:
		checkUpdatesAsync(false)
	case 2103:
		addCart()
	case 2106:
		finalizeSale()
	case 2107:
		showConsulta()
	case 2109:
		removeCartSelected()
	case 2110:
		clearCart()
	case 2202:
		loadProducts(getText(prodSearch))
	case 2203:
		loadProducts("")
	case 2205:
		newProductQuick()
	case 2206:
		importProductsExcel()
	case 2207:
		exportProductsExcel()
	case 2211:
		lookupProductOnline()
	case 2219:
		saveProductRegistration()
	case 2220:
		clearProductForm()
		pSetFocus.Call(prodBarcodeReg)
	case 2221:
		migrateArmazem1215Stock()
	case 2701:
		loadValidity("all")
	case 2702:
		loadValidity("expired")
	case 2703:
		loadValidity("30")
	case 2704:
		loadValidity("60")
	case 2705:
		loadValidity("none")
	case 2302:
		loadStock(false)
	case 2303:
		loadStock(true)
	case 2402:
		loadSales(false)
	case 2403:
		deleteSelectedSale()
	case 2404:
		loadSales(true)
	case 2405:
		loadSales(false)
	case 2406:
		clearDeletedSales()
	case 2501:
		openCash()
	case 2502:
		closeCash()
	case 2602:
		loadFiado()
	case 2611:
		loadFiado()
	case 2612:
		fiadoMode = "ABERTO"
		loadFiado()
	case 2613:
		fiadoMode = "PAGO"
		loadFiado()
	case 2622:
		receiveFiado(false)
	case 2623:
		receiveFiado(true)
	case 2631:
		saveFiadoCustomer()
	}
}

func wndProc(hwnd uintptr, m uint32, w, l uintptr) uintptr {
	switch m {
	case WM_CTLCOLORSTATIC:
		h := l
		if h == topbarBg || topbarLabels[h] {
			pSetTextColor.Call(w, 0x00FFFFFF)
			pSetBkColor.Call(w, 0x0058310B)
			return brushTop
		}
		if h == topbarBorder {
			return brushRed
		}
	case WM_COMMAND:
		handleCommand(int(w & 0xffff))
		return 0
	case WM_APP_DBREADY:
		showInicio()
		checkUpdatesAsync(true)
		return 0
	case WM_APP_LOOKUPDONE:
		lookupMu.Lock()
		d := lookupData
		lookupMu.Unlock()
		if prodBarcodeReg != 0 && d != nil {
			setText(prodDescReg, d["description"])
			setText(prodBrandReg, d["brand"])
			setText(prodCategoryReg, d["category"])
			msg("Produto encontrado. Confira preço, custo e estoque e clique em SALVAR PRODUTO.")
		}
		return 0
	case WM_APP_UPDATEFOUND:
		updateMu.Lock()
		um := updateFound
		updateMu.Unlock()
		if um != nil {
			msg("Nova versão " + um.Version + " encontrada.\n\n" + um.Notes + "\n\nO atualizador nativo fará o download, validará SHA-256, criará backup e instalará automaticamente.")
			installUpdateAsync(um)
		}
		return 0
	case WM_APP_UPDATEINSTALL:
		msg("Atualizador nativo validado. O ERP será fechado com segurança, atualizado e aberto novamente.")
		launchPreparedUpdate()
		return 0
	case WM_APP_UPDATENONE:
		msg("ERP Gratidão está atualizado.")
		return 0
	case WM_APP_UPDATEFAIL:
		updateMu.Lock()
		ue := updateErr
		updateMu.Unlock()
		msgErr("Falha ao verificar atualizações: " + ue)
		return 0
	case WM_APP_LOOKUPFAIL:
		lookupMu.Lock()
		e := lookupErr
		lookupMu.Unlock()
		if e != "" {
			msg(e)
		}
		return 0
	case WM_APP_DBFAIL:
		clearContent()
		header("ERP Gratidão", "Falha ao abrir banco de dados")
		dbMu.Lock()
		e := dbErr
		dbMu.Unlock()
		add("STATIC", e, 0, 250, 150, 1000, 80, 0)
		return 0
	case WM_DESTROY:
		if db != 0 {
			pSqlClose.Call(db)
		}
		pPostQuitMessage.Call(0)
		return 0
	}
	r, _, _ := pDefWindowProcW.Call(hwnd, uintptr(m), w, l)
	return r
}

func setMenuVisible(show bool) {
	menuVisible = show
	navCmd := uintptr(0)
	contentCmd := uintptr(SW_SHOW)
	if show {
		navCmd = SW_SHOW
		contentCmd = 0
	}
	for _, h := range content {
		pShowWindow.Call(h, contentCmd)
	}
	for _, h := range navControls {
		pShowWindow.Call(h, navCmd)
	}
}
func toggleMenu() { setMenuVisible(!menuVisible) }

func buildNavigation() {
	// Estrutura copiada da referência 12.1.5:
	// [☰][ERP GRATIDÃO] | [⌕ Ir para...] | [versão][Sistema local][Atualizações][Consulta de produto][⚙]
	brushTop, _, _ = pCreateSolidBrush.Call(0x0058310B) // RGB aproximado #0b3158 em COLORREF BGR
	brushRed, _, _ = pCreateSolidBrush.Call(0x00251DC8) // #c81d25

	topbarBg, _, _ = pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("STATIC"))), 0, WS_CHILD|WS_VISIBLE, 0, 0, 1305, 54, mainWnd, 0, 0, 0)
	topbarBorder, _, _ = pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("STATIC"))), 0, WS_CHILD|WS_VISIBLE, 0, 51, 1305, 3, mainWnd, 0, 0, 0)

	btn, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("BUTTON"))), uintptr(unsafe.Pointer(ws("☰"))), WS_CHILD|WS_VISIBLE|BS_PUSHBUTTON, 10, 9, 36, 34, mainWnd, 99, 0, 0)
	pSendMessageW.Call(btn, WM_SETFONT, fontBig, 1)

	brand, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("STATIC"))), uintptr(unsafe.Pointer(ws("ARMAZEM GRATIDÃO PRO"))), WS_CHILD|WS_VISIBLE, 56, 14, 205, 27, mainWnd, 0, 0, 0)
	pSendMessageW.Call(brand, WM_SETFONT, fontBig, 1)
	topbarLabels[brand] = true

	moduleSearch, _, _ = pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("EDIT"))), uintptr(unsafe.Pointer(ws("Ir para..."))), WS_CHILD|WS_VISIBLE|WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP, 405, 10, 390, 34, mainWnd, 98, 0, 0)
	pSendMessageW.Call(moduleSearch, WM_SETFONT, font, 1)
	subclassEdit(moduleSearch)

	ver, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("STATIC"))), uintptr(unsafe.Pointer(ws("v"+currentVersion))), WS_CHILD|WS_VISIBLE, 820, 18, 48, 20, mainWnd, 0, 0, 0)
	stat, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("STATIC"))), uintptr(unsafe.Pointer(ws("Sistema local"))), WS_CHILD|WS_VISIBLE, 870, 18, 75, 20, mainWnd, 0, 0, 0)
	pSendMessageW.Call(ver, WM_SETFONT, fontSmall, 1)
	pSendMessageW.Call(stat, WM_SETFONT, fontSmall, 1)
	topbarLabels[ver] = true
	topbarLabels[stat] = true

	u, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("BUTTON"))), uintptr(unsafe.Pointer(ws("Atualizações"))), WS_CHILD|WS_VISIBLE|BS_PUSHBUTTON, 950, 10, 92, 34, mainWnd, 9001, 0, 0)
	c, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("BUTTON"))), uintptr(unsafe.Pointer(ws("Consulta de produto"))), WS_CHILD|WS_VISIBLE|BS_PUSHBUTTON, 1047, 10, 160, 34, mainWnd, 9002, 0, 0)
	g, _, _ := pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(ws("BUTTON"))), uintptr(unsafe.Pointer(ws("⚙"))), WS_CHILD|WS_VISIBLE|BS_PUSHBUTTON, 1212, 10, 38, 34, mainWnd, 9003, 0, 0)
	pSendMessageW.Call(u, WM_SETFONT, fontSmall, 1)
	pSendMessageW.Call(c, WM_SETFONT, fontSmall, 1)
	pSendMessageW.Call(g, WM_SETFONT, fontBig, 1)

	// Menu deslizante oculto, igual ao comportamento da referência.
	addNavLabel("OPERAÇÃO", 60)
	addNav("Início", 101, 83)
	addNav("PDV", 102, 124)
	addNav("Vendas", 105, 165)
	addNav("Caixa", 108, 206)
	addNavLabel("CADASTROS / ESTOQUE", 253)
	addNav("Produtos", 103, 276)
	addNav("Estoque", 104, 317)
	addNav("Validade", 109, 358)
	addNav("Compras", 110, 399)
	addNav("Clientes / Fornecedores", 111, 440)
	addNavLabel("FINANCEIRO", 487)
	addNav("Fiado", 106, 510)
	addNav("Lucro", 112, 551)
	addNav("Relatórios", 113, 592)
	addNavLabel("FERRAMENTAS", 639)
	addNav("Consulta de Produto", 107, 662)
	addNav("Configurações", 114, 703)
	setMenuVisible(false)
}

func main() {
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()
	font, _, _ = pCreateFontW.Call(18, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 0, 0, uintptr(unsafe.Pointer(ws("Segoe UI"))))
	fontSmall, _, _ = pCreateFontW.Call(14, 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 0, 0, uintptr(unsafe.Pointer(ws("Segoe UI"))))
	fontMono, _, _ = pCreateFontW.Call(16, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, uintptr(unsafe.Pointer(ws("Courier New"))))
	fontBig, _, _ = pCreateFontW.Call(22, 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 0, 0, uintptr(unsafe.Pointer(ws("Segoe UI"))))
	fontTitle, _, _ = pCreateFontW.Call(30, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 0, 0, uintptr(unsafe.Pointer(ws("Segoe UI"))))
	hinst, _, _ := pGetModuleHandleW.Call(0)
	cls := ws("ERPGratidaoNative1211")
	wc := WNDCLASSEX{cbSize: uint32(unsafe.Sizeof(WNDCLASSEX{})), lpfnWndProc: syscall.NewCallback(wndProc), hInstance: hinst, lpszClassName: cls, hbrBackground: 6}
	pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc)))
	mainWnd, _, _ = pCreateWindowExW.Call(0, uintptr(unsafe.Pointer(cls)), uintptr(unsafe.Pointer(ws("ARMAZEM GRATIDÃO PRO v1.2.17"))), WS_OVERLAPPEDWINDOW|WS_VISIBLE, 20, 15, 1320, 790, 0, 0, hinst, 0)
	buildNavigation()
	showBoot()
	pShowWindow.Call(mainWnd, SW_SHOW)
	pUpdateWindow.Call(mainWnd)
	initDBAsync()
	var m MSG
	for {
		r, _, _ := pGetMessageW.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0)
		if int32(r) <= 0 {
			break
		}
		pTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
		pDispatchMessageW.Call(uintptr(unsafe.Pointer(&m)))
	}
}
