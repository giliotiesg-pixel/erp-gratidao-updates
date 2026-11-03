using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using QRCoder;
namespace ArmazemGratidao {
 static class Program {
  [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Idle+=(sender,args)=>{ButtonTextVisibility.CheckOpenForms(); PdvShortcutButtons.CheckOpenForms(); WindowBoundsGuard.CheckOpenForms();}; Application.Run(new MainForm()); }
 }
 // Mantém cabeçalho, botões de controle e rodapé acessíveis em monitores menores.
 static class WindowBoundsGuard {
  static readonly System.Collections.Generic.HashSet<Form> initialized=new System.Collections.Generic.HashSet<Form>();
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms.Cast<Form>().ToArray()){
    if(form==null||form.IsDisposed)continue;
    if(initialized.Add(form)){
     form.ShowInTaskbar=true;
     form.Resize+=(sender,args)=>{if(form.WindowState==FormWindowState.Normal)KeepVisible(form);};
     form.Shown+=(sender,args)=>KeepVisible(form);
    }
    if(form.Visible&&form.WindowState==FormWindowState.Normal)KeepVisible(form);
   }
   initialized.RemoveWhere(f=>f.IsDisposed);
  }
  static void KeepVisible(Form f){
   if(f.IsDisposed||f.WindowState!=FormWindowState.Normal)return;
   Rectangle working=Screen.FromControl(f).WorkingArea;
   if(working.Width<=0||working.Height<=0)return;
   // Não ampliar janelas pequenas: reduzir somente as que ultrapassam a área útil.
   int width=Math.Min(f.Width,Math.Max(300,working.Width-24));
   int height=Math.Min(f.Height,Math.Max(220,working.Height-30));
   if(f.Width!=width||f.Height!=height)f.Size=new Size(width,height);
   int left=Math.Max(working.Left,Math.Min(f.Left,working.Right-f.Width));
   int top=Math.Max(working.Top,Math.Min(f.Top,working.Bottom-f.Height));
   if(f.Left!=left||f.Top!=top)f.Location=new Point(left,top);
  }
 }
 // Atalho centralizado: aparece somente nos formulários principais dos módulos.
 // Não modifica diálogos de confirmação nem os dados de uma venda em andamento.
 static class PdvShortcutButtons {
  static readonly System.Collections.Generic.HashSet<Form> installed=new System.Collections.Generic.HashSet<Form>();
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms){
    if(form==null || form.IsDisposed || installed.Contains(form))continue;
    bool module=form is ConsultaForm || form is FiadoClientesForm ||
     form is ProdutosForm || form is LucroForm || form is ModuloConsultaForm;
    if(!module)continue;
    installed.Add(form);
    var shortcut=new Button {
     Text="PDV - Nova Venda",Width=180,Height=38,
     BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
     Font=new Font("Segoe UI",10,FontStyle.Bold),
     Margin=new Padding(6)
    };
    var strip=new FlowLayoutPanel {
     Dock=DockStyle.Top,Height=48,WrapContents=false,
     FlowDirection=FlowDirection.RightToLeft,
     BackColor=Color.FromArgb(229,242,255)
    };
    shortcut.Click+=(sender,args)=>{
     MainForm main=null;
     foreach(Form open in Application.OpenForms)if(open is MainForm){main=(MainForm)open;break;}
     if(main!=null)main.OpenPdvFromModule();
     else MessageBox.Show("A janela principal não está disponível.","PDV");
    };
    strip.Controls.Add(shortcut);
    form.Controls.Add(strip);
    strip.BringToFront();
    form.FormClosed+=(sender,args)=>installed.Remove(form);
   }
  }
 }
 static class ButtonTextVisibility {
  static readonly System.Collections.Generic.Dictionary<Button,string> inspected=new System.Collections.Generic.Dictionary<Button,string>();
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms)Check(form);
  }
  static void Check(Control parent){
   foreach(Control child in parent.Controls){
    var button=child as Button;
    if(button!=null && !string.IsNullOrWhiteSpace(button.Text)){
     string signature=button.Text+"|"+button.Font.SizeInPoints+"|"+button.Width;
     string previous;
     if(!inspected.TryGetValue(button,out previous) || previous!=signature){
      var size=TextRenderer.MeasureText(button.Text,button.Font,new Size(1000,1000),TextFormatFlags.SingleLine);
      int desired=size.Width+24;
      if(button.Image!=null)desired+=button.Image.Width+8;
      if(button.Width<desired && button.Dock==DockStyle.None && button.Parent is FlowLayoutPanel && !(button.MinimumSize.Width>0 && button.MaximumSize.Width==button.MinimumSize.Width)){
       button.AutoSize=false;button.Width=desired;
      }
      button.AutoEllipsis=true;
      button.TextAlign=ContentAlignment.MiddleCenter;
      button.UseMnemonic=false;
      inspected[button]=button.Text+"|"+button.Font.SizeInPoints+"|"+button.Width;
     }
    }
    if(child.HasChildren)Check(child);
   }
  }
 }
 public sealed class MainForm : Form {
  readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
  readonly string dbPath;
  MobileStockServer mobileStock;
  readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer {Interval=6*60*60*1000};
  bool checkingUpdates=false;
  int lastPromptedBuild=-1;
  int InstalledBuild { get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build; } }
  readonly FlowLayoutPanel sideMenu=new FlowLayoutPanel {Dock=DockStyle.Left,Width=255,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(11),BackColor=Color.FromArgb(234,241,251)};
  readonly Panel header=new Panel {Dock=DockStyle.Top,Height=66,BackColor=Color.White};
  readonly Button menuToggle=new Button {Text="☰  Menu",Width=120,Height=40,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
  readonly DataGridView grid=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AllowUserToAddRows=false };
  readonly Label status=new Label { Dock=DockStyle.Bottom,Height=34,TextAlign=ContentAlignment.MiddleLeft };
  public MainForm() {
   dbPath=Path.Combine(dataDir,"armazem-gratidao.db");
   Text="Armazém Gratidão Novo";Width=1280;Height=800;MinimumSize=new Size(950,600);Font=new Font("Segoe UI",10,FontStyle.Bold);BackColor=Color.FromArgb(246,249,253);
   var bar=sideMenu;
   grid.ScrollBars=ScrollBars.Both;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
   var working=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,working.Width),Math.Min(Height,working.Height));

   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(20,69,147);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);grid.ColumnHeadersHeight=38;grid.RowTemplate.Height=32;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(241,245,252);
   status.BackColor=Color.FromArgb(234,238,247);status.ForeColor=Color.FromArgb(30,40,55);status.Padding=new Padding(12,0,0,0);
   menuToggle.Left=14;menuToggle.Top=13;menuToggle.FlatAppearance.BorderSize=0;menuToggle.Click+=(s,e)=>{sideMenu.Visible=!sideMenu.Visible;};header.Controls.Add(menuToggle);
   var brand=new Label {Text="ARMAZÉM GRATIDÃO",ForeColor=Color.FromArgb(10,52,145),Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Left=155,Top=17};header.Controls.Add(brand);
   AddSection(bar,"PRINCIPAL");
   AddButton(bar,"Início",ShowHome);
   AddButton(bar,"Verificar atualização",async (s,e)=>await CheckUpdates());
   AddSection(bar,"DADOS E RECUPERAÇÃO");
   AddButton(bar,"Importar banco",ImportDatabase);
   AddButton(bar,"Backup do banco",BackupDatabase);
   AddButton(bar,"Verificar banco",CheckDatabase);
   AddButton(bar,"Localizar vendas",CompareSales);
   AddButton(bar,"Preparar recuperação",PrepareRecoveredSales);
   AddButton(bar,"Aplicar recuperação",ApplyRecovery);
   AddButton(bar,"Conferir valores",CheckSaleDifferences);
   AddSection(bar,"OPERAÇÃO");
   AddButton(bar,"WhatsApp",(sender,args)=>OpenModuleWindow(new WhatsAppNormalForm()));
   AddButton(bar,"Lançar vendas anotadas",(sender,args)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de lançar vendas.");return;}OpenModuleWindow(new PdvForm(dbPath,true));});
   AddButton(bar,"PDV - Nova Venda",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.");return;}OpenModuleWindow(new PdvForm(dbPath));});
   AddButton(bar,"Cascos retornáveis",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de controlar cascos.");return;}OpenModuleWindow(new CascosForm(dbPath));});
   AddButton(bar,"Vendas",(s,e)=>OpenConsult(false));
   AddButton(bar,"Taxas dos pagamentos",(s,e)=>PaymentFees.ShowEditor(dbPath,this));
   AddButton(bar,"Integrar vendas 01 a 19/10",IntegrateHistoricalSales);
   AddButton(bar,"Itens",(s,e)=>ShowTable("sale_items","SELECT sale_id,name,quantity,unit_price,total FROM sale_items ORDER BY sale_id DESC LIMIT 5000"));
   AddButton(bar,"Fiado",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new FiadoClientesForm(dbPath));});
   AddButton(bar,"Conferência rápida de estoque",(sender,args)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de conferir estoque.");return;}OpenModuleWindow(new StockCountQuickForm(dbPath));});
   AddButton(bar,"Produtos",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new ProdutosForm(dbPath));});
   AddButton(bar,"Estoque pelo celular",(s,e)=>OpenMobileStock());
   AddButton(bar,"Lucro",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new LucroForm(dbPath));});
   AddSection(bar,"GESTÃO");
   foreach(var module in new[]{"Estoque","Clientes","Compras","Caixa","Financeiro","Relatorios","Sistema"}) AddButton(bar,module,(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new ModuloConsultaForm(dbPath,module));});
   Controls.Add(grid);Controls.Add(status);Controls.Add(bar);Controls.Add(header);bar.Visible=false;Directory.CreateDirectory(dataDir);
   status.Text=File.Exists(dbPath)?"Banco encontrado. Selecione Vendas ou Fiado.":"Importe uma copia do banco SQLite.";
   ShowHome(this,EventArgs.Empty);
   Shown+=(s,e)=>BeginInvoke((Action)(async ()=>{AutoRecoverSales();await CheckUpdates(false);}));
   updateTimer.Tick+=async (s,e)=>await CheckUpdates(false);
   updateTimer.Start();
   FormClosed+=(s,e)=>{updateTimer.Stop();updateTimer.Dispose();if(mobileStock!=null){mobileStock.Dispose();mobileStock=null;}};
  }
  public void OpenPdvFromModule(){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.","PDV");return;}
   // Evitar abrir vários PDVs sobrepostos; reutilizar a janela existente.
   foreach(Form open in Application.OpenForms){
    if(open is PdvForm && !open.IsDisposed){
     if(open.WindowState==FormWindowState.Minimized)open.WindowState=FormWindowState.Normal;
     open.BringToFront();open.Activate();
     return;
    }
   }
   OpenModuleWindow(new PdvForm(dbPath));
  }
  string CreateVerifiedPreUpdateBackup(){
   if(!File.Exists(dbPath))return "Sem banco cadastrado (instalação inicial)";
   Directory.CreateDirectory(dataDir);
   string backup=Path.Combine(dataDir,"armazem-gratidao-antes-atualizacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
   try{
    using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
    using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup,FailIfMissing=false}.ToString())){
     source.Open();dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
    }
    using(var check=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup,ReadOnly=true,FailIfMissing=true}.ToString())){
     check.Open();
     using(var cmd=new SQLiteCommand("PRAGMA integrity_check",check)){
      if(!string.Equals(Convert.ToString(cmd.ExecuteScalar()),"ok",StringComparison.OrdinalIgnoreCase))
       throw new Exception("Cópia gerada com erro de integridade.");
     }
    }
    if(new FileInfo(backup).Length<=0)throw new Exception("O backup foi gerado vazio.");
    return backup;
   }catch{
    try{if(File.Exists(backup))File.Delete(backup);}catch{}
    throw;
   }
  }
  async Task CheckUpdates(bool manual=true){
   if(checkingUpdates){if(manual)MessageBox.Show("Ja existe uma verificacao em andamento.","Atualizacao");return;}
   checkingUpdates=true;
   try{
    status.Text="Consultando versão publicada...";
    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    string json;
    using(var web=new WebClient()){
     web.Headers[HttpRequestHeader.UserAgent]="ArmazemGratidaoNovo-Windows";
     web.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";
     json=await web.DownloadStringTaskAsync(new Uri("https://api.github.com/repos/giliotiesg-pixel/erp-gratidao-updates/releases?per_page=100"));
    }
    var releases=new JavaScriptSerializer().DeserializeObject(json) as object[];
    if(releases==null)throw new Exception("Lista de versoes invalida.");
    const string prefix="amzg-novo-win8-build-";
    System.Collections.Generic.Dictionary<string,object> release=null;
    int remoteBuild=-1;
    foreach(object obj in releases){
     var candidate=obj as System.Collections.Generic.Dictionary<string,object>;
     if(candidate==null||!candidate.ContainsKey("tag_name"))continue;
     if(candidate.ContainsKey("draft")&&Convert.ToBoolean(candidate["draft"]))continue;
     if(candidate.ContainsKey("prerelease")&&Convert.ToBoolean(candidate["prerelease"]))continue;
     string tag=Convert.ToString(candidate["tag_name"]);
     int build;
     if(tag.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&int.TryParse(tag.Substring(prefix.Length),out build)&&build>remoteBuild){
      remoteBuild=build;release=candidate;
     }
    }
    if(release==null){
     status.Text="Nenhuma versao Windows8 publicada.";
     if(manual)MessageBox.Show("Nao existe uma atualizacao Windows8 publicada.","Verificar atualizacao");
     return;
    }
    if(remoteBuild<=InstalledBuild){
     status.Text="Versao atualizada: build "+InstalledBuild+".";
     if(manual)MessageBox.Show("Versao instalada: "+InstalledBuild+"\\nUltima build publicada: "+remoteBuild+"\\nNenhuma atualizacao nova disponivel.","Verificar atualizacao");
     return;
    }
    status.Text="Nova versao publicada: build "+remoteBuild+".";
    if(!manual&&lastPromptedBuild==remoteBuild)return;
    lastPromptedBuild=remoteBuild;
    var assets=release["assets"] as object[];if(assets==null)throw new Exception("Instalador não encontrado.");
    System.Collections.Generic.Dictionary<string,object> asset=null;
    foreach(object obj in assets){
     var item=obj as System.Collections.Generic.Dictionary<string,object>;
     if(item!=null&&Convert.ToString(item["name"])=="Armazem-Gratidao-Novo-Windows8-Setup.exe"){asset=item;break;}
    }
    if(asset==null)throw new Exception("O release não contém o instalador Windows.");
    string digest=asset.ContainsKey("digest")?Convert.ToString(asset["digest"]):"";
    if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||digest.Length!=71)throw new Exception("Atualização bloqueada: SHA-256 não publicado.");
    string url=Convert.ToString(asset["browser_download_url"]);
    if(!url.StartsWith("https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases/download/",StringComparison.OrdinalIgnoreCase))throw new Exception("Endereço de atualização inválido.");
    if(MessageBox.Show("Nova versão: build "+remoteBuild+"\\nVersão atual: build "+InstalledBuild+"\\n\\nBaixar, conferir e iniciar a instalação?\\nO programa será fechado após iniciar o instalador.","Atualização do Armazém Gratidão",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){status.Text="Atualização cancelada.";return;}
    // Não fechar a venda em andamento ou módulos com dados não salvos.
    if(Application.OpenForms.Cast<Form>().Any(x=>x!=this&&!x.IsDisposed&&x.Visible)){
     MessageBox.Show("Feche as demais janelas do Armazem antes da instalacao.","Atualizacao adiada");
     status.Text="Atualizacao adiada por janelas abertas.";return;
    }
    string dir=Path.Combine(Path.GetTempPath(),"ArmazemGratidaoNovo-Atualizacoes");
    Directory.CreateDirectory(dir);
    string installer=Path.Combine(dir,"Armazem-Gratidao-Novo-build-"+remoteBuild+"-Setup.exe");
    status.Text="Baixando atualização build "+remoteBuild+"...";
    using(var web=new WebClient())await web.DownloadFileTaskAsync(new Uri(url),installer);
    string actual;
    using(var stream=File.OpenRead(installer))using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    if(!string.Equals(actual,digest.Substring(7),StringComparison.OrdinalIgnoreCase)){File.Delete(installer);throw new Exception("SHA-256 inválido. Instalador rejeitado.");}
    // A instalação EXE também cria seu próprio backup, para cobrir atualizações manuais.
    // Um backup validado agora é obrigatório antes de iniciar o instalador pelo programa.
    string backupPath=CreateVerifiedPreUpdateBackup();
    status.Text="Backup confirmado: "+backupPath;
    Process.Start(new ProcessStartInfo(installer){UseShellExecute=true});
    Close();
   }catch(Exception ex){status.Text="Nao foi possivel atualizar: "+ex.Message;if(manual)MessageBox.Show(ex.Message,"Atualizacao nao iniciada",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{checkingUpdates=false;}
  }
  void ShowHome(object sender,EventArgs e){
   grid.Visible=false;
   var old=Controls["homePanel"];if(old!=null){Controls.Remove(old);old.Dispose();}
   var home=new FlowLayoutPanel {Name="homePanel",Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new Padding(24),BackColor=Color.FromArgb(246,249,253)};
   var title=new Label {Text="INÍCIO  •  ARMAZÉM GRATIDÃO",Width=940,Height=58,Font=new Font("Segoe UI",19,FontStyle.Bold),ForeColor=Color.FromArgb(10,52,145)};
   home.Controls.Add(title);
   var date=new Label {Text=DateTime.Now.ToString("dddd, dd/MM/yyyy  HH:mm"),Width=940,Height=36,Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(160,20,40)};
   home.Controls.Add(date);
   var refresh=new Button {Text="Atualizar indicadores",Width=200,Height=40,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(20,69,147),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(8)};
   refresh.FlatAppearance.BorderSize=0;refresh.Click+=ShowHome;home.Controls.Add(refresh);
   home.Controls.Add(new Label {Text="",Width=680,Height=40});
   var metrics=new System.Collections.Generic.List<Tuple<string,string>>();
   if(File.Exists(dbPath)){
    try{
     using(var db=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
      db.Open();
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE substr(created_at,1,10)=@day AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",db)){
       q.Parameters.AddWithValue("@day",DateTime.Today.ToString("yyyy-MM-dd"));
       metrics.Add(Tuple.Create("VENDAS DO DIA",Convert.ToString(q.ExecuteScalar())));
      }
      using(var q=new SQLiteCommand("SELECT COALESCE(SUM(gross_total),0) FROM sales WHERE substr(created_at,1,10)=@day AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",db)){
       q.Parameters.AddWithValue("@day",DateTime.Today.ToString("yyyy-MM-dd"));
       metrics.Add(Tuple.Create("TOTAL VENDIDO HOJE","R$ "+Convert.ToDecimal(q.ExecuteScalar()).ToString("N2")));
      }
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM products",db))
       metrics.Add(Tuple.Create("PRODUTOS CADASTRADOS",Convert.ToString(q.ExecuteScalar())));
      using(var q=new SQLiteCommand("SELECT COALESCE(SUM(gross_total),0) FROM sales WHERE substr(created_at,1,7)=@month AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",db)){
       q.Parameters.AddWithValue("@month",DateTime.Today.ToString("yyyy-MM"));
       metrics.Add(Tuple.Create("TOTAL VENDIDO NO MÊS","R$ "+Convert.ToDecimal(q.ExecuteScalar()).ToString("N2")));
      }
     }
    }catch(Exception ex){metrics.Clear();metrics.Add(Tuple.Create("INDICADORES INDISPONÍVEIS","Verifique o banco: "+ex.Message));}
   }else metrics.Add(Tuple.Create("BANCO NÃO IMPORTADO","Use o botão Importar banco"));
   foreach(var metric in metrics){
    var panel=new Panel {Width=285,Height=103,BackColor=Color.White,Margin=new Padding(7,4,7,14)};
    RoundHomeControl(panel,16);
    panel.Controls.Add(new Label {Text=metric.Item1,Dock=DockStyle.Top,Height=34,Padding=new Padding(12,8,0,0),ForeColor=Color.FromArgb(160,20,40),Font=new Font("Segoe UI",9,FontStyle.Bold)});
    panel.Controls.Add(new Label {Text=metric.Item2,Dock=DockStyle.Bottom,Height=60,Padding=new Padding(12,8,0,0),ForeColor=Color.FromArgb(20,69,147),Font=new Font("Segoe UI",15,FontStyle.Bold),AutoEllipsis=true});
    home.Controls.Add(panel);
   }
   home.Controls.Add(new Label {Text="ACESSO RÁPIDO",Width=940,Height=35,ForeColor=Color.FromArgb(20,69,147),Font=new Font("Segoe UI",12,FontStyle.Bold)});

   var actions=new System.Collections.Generic.List<Tuple<string,EventHandler>>{
    Tuple.Create("PDV  •  Nova venda",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.");return;}OpenModuleWindow(new PdvForm(dbPath));})),
    Tuple.Create("Lançar vendas anotadas",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de lançar vendas.");return;}OpenModuleWindow(new PdvForm(dbPath,true));})),
    Tuple.Create("Vendas",(EventHandler)((s,a)=>OpenConsult(false))),
    Tuple.Create("Cascos retornáveis",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de controlar cascos.");return;}OpenModuleWindow(new CascosForm(dbPath));})),
    Tuple.Create("Integrar vendas 01 a 19/10",(EventHandler)IntegrateHistoricalSales),
    Tuple.Create("Fiado",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new FiadoClientesForm(dbPath));})),
    Tuple.Create("Produtos",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new ProdutosForm(dbPath));})),
    Tuple.Create("Estoque pelo celular",(EventHandler)((s,a)=>OpenMobileStock())),
    Tuple.Create("Estoque",(EventHandler)((s,a)=>OpenHomeModule("Estoque"))),
    Tuple.Create("Clientes",(EventHandler)((s,a)=>OpenHomeModule("Clientes"))),
    Tuple.Create("Compras",(EventHandler)((s,a)=>OpenHomeModule("Compras"))),
    Tuple.Create("Caixa",(EventHandler)((s,a)=>OpenHomeModule("Caixa"))),
    Tuple.Create("Financeiro",(EventHandler)((s,a)=>OpenHomeModule("Financeiro"))),
    Tuple.Create("Lucro",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new LucroForm(dbPath));})),
    Tuple.Create("Relatórios",(EventHandler)((s,a)=>OpenHomeModule("Relatorios"))),
    Tuple.Create("Sistema",(EventHandler)((s,a)=>OpenHomeModule("Sistema"))),
    Tuple.Create("Backup do banco",(EventHandler)BackupDatabase),
    Tuple.Create("Verificar atualização",(EventHandler)(async (s,a)=>await CheckUpdates()))
   };
   var shortcuts=new FlowLayoutPanel {Width=950,Height=260,AutoSize=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,Margin=new Padding(0)};
   foreach(var action in actions){
    var button=new Button {Text=action.Item1,Width=174,Height=49,Margin=new Padding(7),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(10,52,145),Font=new Font("Segoe UI",9,FontStyle.Bold)};
    button.FlatAppearance.BorderColor=Color.FromArgb(235,125,135);button.Click+=action.Item2;RoundHomeControl(button,12);shortcuts.Controls.Add(button);
   }
   home.Controls.Add(shortcuts);
   home.Controls.Add(new Label {Text="RANKING  •  PRODUTOS MAIS VENDIDOS NO MÊS",Width=940,Height=39,ForeColor=Color.FromArgb(10,52,145),Font=new Font("Segoe UI",12,FontStyle.Bold)});
   var ranking=new Panel {Width=920,Height=270,BackColor=Color.White,Margin=new Padding(7,4,7,14),AutoScroll=true};
   RoundHomeControl(ranking,16);
   var rankingText=new Label {Dock=DockStyle.Fill,Padding=new Padding(14,12,12,8),ForeColor=Color.FromArgb(10,52,145),Font=new Font("Segoe UI",10,FontStyle.Bold),AutoSize=false,Text="Nenhuma venda encontrada neste mês."};
   var rankingGrid=new DataGridView {Dock=DockStyle.Fill,Visible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,ScrollBars=ScrollBars.Vertical,ColumnHeadersHeight=32,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing};
   rankingGrid.Font=new Font("Segoe UI",10,FontStyle.Regular);
   rankingGrid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
   rankingGrid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(226,238,251);
   rankingGrid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(10,52,145);
   rankingGrid.EnableHeadersVisualStyles=false;
   rankingGrid.DefaultCellStyle.BackColor=Color.White;
   rankingGrid.DefaultCellStyle.ForeColor=Color.FromArgb(20,40,75);
   rankingGrid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(230,239,250);
   rankingGrid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(10,52,145);
   rankingGrid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(247,250,255);
   rankingGrid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   rankingGrid.GridColor=Color.FromArgb(232,237,245);
   rankingGrid.RowTemplate.Height=27;
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="posicao",HeaderText="Nº",Width=54,SortMode=DataGridViewColumnSortMode.NotSortable});
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="produto",HeaderText="Produto",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=200,SortMode=DataGridViewColumnSortMode.NotSortable});
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="quantidade",HeaderText="Quantidade",Width=125,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle {Alignment=DataGridViewContentAlignment.MiddleRight,Format="N3"}});
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="valor",HeaderText="Total vendido",Width=146,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle {Alignment=DataGridViewContentAlignment.MiddleRight,Format="C2"}});
   ranking.Controls.Add(rankingText);ranking.Controls.Add(rankingGrid);home.Controls.Add(ranking);
   if(File.Exists(dbPath)){
    try{using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
     conn.Open();
     string sql="SELECT si.name, SUM(si.quantity) quantidade, SUM(si.total) valor FROM sale_items si JOIN sales s ON s.id=si.sale_id WHERE substr(s.created_at,1,7)=@month AND UPPER(COALESCE(s.status,'')) NOT LIKE '%EXCLUID%' GROUP BY si.product_id,si.name ORDER BY quantidade DESC,valor DESC LIMIT 10";
     using(var cmd=new SQLiteCommand(sql,conn)){
      cmd.Parameters.AddWithValue("@month",DateTime.Today.ToString("yyyy-MM"));
      using(var reader=cmd.ExecuteReader()){
       int position=0;
       while(reader.Read()){
        position++;
        rankingGrid.Rows.Add(position.ToString("00"),Convert.ToString(reader["name"]),Convert.ToDecimal(reader["quantidade"]),Convert.ToDecimal(reader["valor"]));
       }
       if(position>0){rankingText.Visible=false;rankingGrid.Visible=true;rankingGrid.ClearSelection();}
      }
     }
    }}catch(Exception ex){rankingText.Text="Ranking indisponível: "+ex.Message;}
   }
   Controls.Add(home);home.BringToFront();header.BringToFront();sideMenu.BringToFront();status.BringToFront();status.Text="Início • Acesso rápido aos módulos. Banco de dados preservado.";
  }
  // WhatsApp Business usa a janela nativa; nao altera os demais modulos.
  static void RoundHomeControl(Control control,int radius){
   Action apply=()=>{
    if(control.Width<radius*2||control.Height<radius*2)return;
    using(var path=new System.Drawing.Drawing2D.GraphicsPath()){
     int d=radius*2;var rect=new Rectangle(0,0,control.Width,control.Height);
     path.AddArc(rect.Left,rect.Top,d,d,180,90);
     path.AddArc(rect.Right-d,rect.Top,d,d,270,90);
     path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);
     path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);
     path.CloseFigure();
     var previous=control.Region;control.Region=new Region(path);if(previous!=null)previous.Dispose();
    }
   };
   control.Resize+=(s,e)=>apply();apply();
  }
  void OpenMobileStock(){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco de dados.");return;}
   try{
    if(mobileStock==null){mobileStock=new MobileStockServer(dbPath);mobileStock.Start();}
    string ip=MobileStockServer.LanAddress();
    if(string.IsNullOrWhiteSpace(ip)){MessageBox.Show("Não foi encontrada uma rede Wi-Fi local. Conecte o computador e o celular à mesma rede.","Estoque pelo celular");return;}
    string address="http://"+ip+":"+MobileStockServer.Port+"/?chave="+mobileStock.Token;
    using(var dialog=new Form{Text="Estoque pelo celular - QR Code",Width=760,Height=510,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
     var title=new Label{Text="Conecte o celular pelo QR Code",Left=20,Top=13,Width=710,Height=42,Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=Color.FromArgb(15,65,141)};
     var instructions=new Label{Text="1. Conecte o celular e o computador à mesma rede Wi-Fi."+Environment.NewLine+
      "2. Abra a câmera do celular e aponte para o QR Code."+Environment.NewLine+
      "3. Toque no endereço exibido no celular."+Environment.NewLine+
      "4. Identifique o produto, informe a quantidade e confirme.",Left=300,Top=73,Width=410,Height=132,Font=new Font("Segoe UI",11,FontStyle.Regular)};
     var input=new TextBox{Text=address,Left=300,Top=220,Width=415,Height=30,ReadOnly=true,Font=new Font("Segoe UI",9,FontStyle.Regular)};
     var note=new Label{Text="Acesso exclusivo à rede local. Use somente Wi-Fi privado e não compartilhe o QR Code: ele contém a chave temporária do estoque.",Left=300,Top=267,Width=405,Height=74,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(152,31,45)};
     using(var qrGenerator=new QRCodeGenerator())
     using(var qrData=qrGenerator.CreateQrCode(address,QRCodeGenerator.ECCLevel.Q))
     using(var qr=new QRCode(qrData))
     using(var bitmap=qr.GetGraphic(7)){
      var display=new PictureBox{Left=17,Top=75,Width=265,Height=265,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle,SizeMode=PictureBoxSizeMode.Zoom};
      display.Image=new Bitmap(bitmap);
      dialog.Controls.Add(display);
      dialog.FormClosed+=(s,e)=>{if(display.Image!=null){display.Image.Dispose();display.Image=null;}};
     }
     var stop=new Button{Text="Desligar acesso pelo celular",Left=18,Top=380,Width=320,Height=40};
     stop.Click+=(s,e)=>{if(mobileStock!=null){mobileStock.Dispose();mobileStock=null;}dialog.Close();MessageBox.Show("Acesso do celular encerrado.","Armazém Gratidão");};
     var close=new Button{Text="Manter ligado e fechar janela",Left=353,Top=380,Width=359,Height=40};
     close.Click+=(s,e)=>dialog.Close();
     dialog.Controls.AddRange(new Control[]{title,instructions,input,note,stop,close});
     dialog.ShowDialog(this);
    }
   }catch(Exception ex){if(mobileStock!=null){mobileStock.Dispose();mobileStock=null;}MessageBox.Show("Não foi possível iniciar o estoque pelo celular: "+ex.Message+"\nConfira a porta "+MobileStockServer.Port+" e as permissões de firewall.","Armazém Gratidão",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Abrir os modulos sem bloquear a pagina inicial.
  // Mantem as janelas abertas de forma independente; nao usar 'using' com Show().
  void OpenModuleWindow(Form form){
   if(form==null)return;
   // Reutiliza a janela do mesmo módulo, mesmo se ela estiver minimizada.
   Form existing=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>!x.IsDisposed&&x.GetType()==form.GetType()&&
    (!(x is ConsultaForm)||((ConsultaForm)x).IsCreditWindow==((ConsultaForm)form).IsCreditWindow)&&
    (!(x is PdvForm)||((PdvForm)x).IsManualEntry==((PdvForm)form).IsManualEntry));
   if(existing!=null){
    form.Dispose();
    if(existing.WindowState==FormWindowState.Minimized)existing.WindowState=FormWindowState.Normal;
    existing.Show();existing.BringToFront();existing.Activate();
    return;
   }
   form.StartPosition=FormStartPosition.CenterScreen;
   form.ShowInTaskbar=true;
   // Janelas independentes: minimizar o programa principal não oculta o módulo.
   form.Show();
  }
  void OpenHomeModule(string module){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   OpenModuleWindow(new ModuloConsultaForm(dbPath,module));
  }
  void AddSection(Control parent,string title){parent.Controls.Add(new Label {Text=title,Width=175,Height=29,TextAlign=ContentAlignment.BottomLeft,ForeColor=Color.FromArgb(188,209,245),Font=new Font("Segoe UI",8,FontStyle.Bold),Padding=new Padding(7,0,0,4),Margin=new Padding(3,11,3,2)});}
  void AddButton(Control parent,string title,EventHandler click) {var b=new Button {Text=title,Width=175,Height=40,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(20,69,147),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(3,3,3,3)};b.FlatAppearance.BorderSize=0;b.Click+=click;parent.Controls.Add(b);}
  void BackupDatabase(object sender,EventArgs e) {
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   using(var dlg=new SaveFileDialog {Title="Salvar copia de seguranca SQLite",Filter="Banco SQLite (*.db)|*.db",FileName="armazem-gratidao-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db"}) {
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try {
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Escolha um destino diferente do banco em uso.");
     if(File.Exists(dlg.FileName))throw new Exception("Escolha um nome de arquivo novo para nao sobrescrever backups anteriores.");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,FailIfMissing=false}.ToString())) {
      source.Open();dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
     }
     using(var check=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())) {
      check.Open();using(var cmd=new SQLiteCommand("PRAGMA integrity_check",check))if(Convert.ToString(cmd.ExecuteScalar())!="ok")throw new Exception("Backup criado, mas a verificacao de integridade falhou.");
     }
     status.Text="Backup SQLite criado e validado: "+dlg.FileName;
     MessageBox.Show("Backup concluido e verificado. O banco em uso nao foi modificado.");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Backup nao concluido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void CheckDatabase(object sender,EventArgs e) {
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   try {
    using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
     conn.Open();
     string result;using(var cmd=new SQLiteCommand("PRAGMA integrity_check",conn))result=Convert.ToString(cmd.ExecuteScalar());
     if(result!="ok")throw new Exception("Integridade SQLite: "+result);
     var counts=new System.Text.StringBuilder("Integridade SQLite: OK"+Environment.NewLine);
     foreach(var table in new[]{"sales","sale_items","credit_payments","products"}){
      using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM "+table,conn))counts.AppendLine(table+": "+cmd.ExecuteScalar());
     }
     status.Text="Integridade do banco: OK. Consultas realizadas sem alteracoes.";
     MessageBox.Show(counts.ToString(),"Verificacao do banco");
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Problema na verificacao",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  static string SalesSummary(string path) {
   using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=path,ReadOnly=true,FailIfMissing=true}.ToString())){
    conn.Open();
    using(var cmd=new SQLiteCommand("SELECT COUNT(*),COALESCE(MAX(id),0),COALESCE(MAX(created_at),'SEM DATA') FROM sales",conn))
    using(var reader=cmd.ExecuteReader()){
     if(!reader.Read())return "Nenhuma venda encontrada.";
     return "Quantidade: "+reader.GetValue(0)+" | Último número: "+reader.GetValue(1)+" | Data mais recente: "+reader.GetValue(2);
    }
   }
  }
  void CheckSaleDifferences(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro um banco.");return;}
   using(var dlg=new OpenFileDialog {Title="Selecione o banco original para conferir valores das vendas",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}){
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try{
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um banco diferente do importado.");
     var imported=new System.Collections.Generic.Dictionary<long,decimal>();
     using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
      conn.Open();using(var cmd=new SQLiteCommand("SELECT id,gross_total FROM sales",conn))using(var reader=cmd.ExecuteReader())
       while(reader.Read())imported[Convert.ToInt64(reader.GetValue(0))]=Convert.ToDecimal(reader.GetValue(1));
     }
     var differences=new System.Collections.Generic.List<string>();
     long compared=0;
     using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      conn.Open();using(var cmd=new SQLiteCommand("SELECT id,gross_total FROM sales ORDER BY id",conn))using(var reader=cmd.ExecuteReader()){
       while(reader.Read()){
        long id=Convert.ToInt64(reader.GetValue(0));
        decimal original=Convert.ToDecimal(reader.GetValue(1));
        decimal current;
        if(!imported.TryGetValue(id,out current))continue;
        compared++;
        if(Math.Abs(original-current)>0.005m)differences.Add(id+";"+original.ToString(System.Globalization.CultureInfo.InvariantCulture)+";"+current.ToString(System.Globalization.CultureInfo.InvariantCulture));
       }
      }
     }
     string msg="Vendas presentes nos dois bancos: "+compared+Environment.NewLine+
      "Vendas com total diferente: "+differences.Count+Environment.NewLine+
      "Comparação somente de leitura; não altera banco, vendas ou estoque.";
     if(differences.Count==0){MessageBox.Show(msg,"Conferir valores");return;}
     if(MessageBox.Show(msg+Environment.NewLine+"Salvar relatório CSV?","Conferir valores",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
     using(var save=new SaveFileDialog {Title="Salvar diferenças de valores",Filter="CSV (*.csv)|*.csv",FileName="divergencias-vendas-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
      if(save.ShowDialog()!=DialogResult.OK)return;
      if(File.Exists(save.FileName))throw new Exception("Escolha um nome novo para não sobrescrever relatório anterior.");
      using(var writer=new StreamWriter(save.FileName,false,new System.Text.UTF8Encoding(true))){
       writer.WriteLine("numero_venda;valor_original;valor_importado");
       foreach(string line in differences)writer.WriteLine(line);
      }
      MessageBox.Show("Relatório salvo: "+save.FileName);
     }
    }catch(Exception ex){MessageBox.Show(ex.Message,"Conferência não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void ApplyRecovery(object sender,EventArgs e){
   MessageBox.Show("Aplicacao automatica temporariamente bloqueada: a validacao anterior comparava apenas contagens e poderia substituir registros alterados, inclusive do Fiado. O banco ativo foi preservado. Use Preparar recuperacao para gerar e conferir uma copia de teste.","Protecao contra perda de dados",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   return;

   if(!File.Exists(dbPath)){MessageBox.Show("Banco atual não encontrado.");return;}
   using(var pick=new OpenFileDialog {Title="Selecione o banco de TESTE gerado em Preparar recuperação",Filter="Banco SQLite (*.db)|*.db"}){
    if(pick.ShowDialog()!=DialogResult.OK)return;
    if(Path.GetFullPath(pick.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Selecione um banco de teste diferente do banco atual.");return;}
    try{
     int currentCount,testCount,extra;
     using(var current=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var candidate=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=pick.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      current.Open();candidate.Open();
      foreach(var conn in new[]{current,candidate}){
       using(var check=new SQLiteCommand("PRAGMA integrity_check",conn))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Integridade SQLite inválida.");
      }
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM sales",current))currentCount=Convert.ToInt32(q.ExecuteScalar());
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM sales",candidate))testCount=Convert.ToInt32(q.ExecuteScalar());
      var currentIds=new System.Collections.Generic.HashSet<long>();
      using(var q=new SQLiteCommand("SELECT id FROM sales",current))using(var reader=q.ExecuteReader())while(reader.Read())currentIds.Add(Convert.ToInt64(reader.GetValue(0)));
      extra=0;
      using(var q=new SQLiteCommand("SELECT id FROM sales",candidate))using(var reader=q.ExecuteReader())while(reader.Read())if(!currentIds.Contains(Convert.ToInt64(reader.GetValue(0))))extra++;
      if(extra==0){MessageBox.Show("Nenhuma venda nova identificada. Banco atual preservado.");return;}
      if(testCount!=currentCount+extra)throw new Exception("O banco de teste não contém todas as vendas atuais. Recuperação bloqueada.");
      foreach(var table in new[]{"sales","sale_items","sale_payments","credit_payments","products"}){
       using(var a=new SQLiteCommand("SELECT COUNT(*) FROM "+table,current))
       using(var b=new SQLiteCommand("SELECT COUNT(*) FROM "+table,candidate)){
        long x=Convert.ToInt64(a.ExecuteScalar()),y=Convert.ToInt64(b.ExecuteScalar());
        if(y<x)throw new Exception("Banco de teste perdeu registros em "+table+". Aplicação bloqueada.");
        if(table=="products" && x!=y)throw new Exception("Quantidade de produtos diferente. Aplicação bloqueada.");
       }
      }
     }
     if(MessageBox.Show("Foram identificadas "+extra+" vendas adicionais no banco de teste."+Environment.NewLine+
      "O programa fará um BACKUP do banco atual e depois substituirá a cópia de trabalho pelo banco de teste."+Environment.NewLine+
      "Faça isso somente com o caixa parado e depois de conferir os dados. Deseja continuar?",
      "CONFIRMAR RECUPERAÇÃO",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
     string backup=dbPath+".antes-recuperacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".db";
     using(var current=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var target=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup,FailIfMissing=false}.ToString())){
      current.Open();target.Open();current.BackupDatabase(target,"main","main",-1,null,0);
     }
     string staged=Path.Combine(dataDir,"recuperacao-"+Guid.NewGuid().ToString("N")+".db");
     try{
      using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=pick.FileName,ReadOnly=true,FailIfMissing=true}.ToString()))
      using(var target=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=staged,FailIfMissing=false}.ToString())){
       source.Open();target.Open();source.BackupDatabase(target,"main","main",-1,null,0);
      }
      using(var verify=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=staged,ReadOnly=true,FailIfMissing=true}.ToString())){
       verify.Open();using(var q=new SQLiteCommand("PRAGMA integrity_check",verify))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Falha na verificação final da cópia.");
      }
      File.Replace(staged,dbPath,null);
      status.Text="Recuperação aplicada. "+extra+" vendas adicionais. Backup: "+backup;
      MessageBox.Show("Recuperação aplicada. Vendas adicionais: "+extra+Environment.NewLine+"Backup do banco anterior: "+backup+Environment.NewLine+"Confira Vendas e Fiado antes de continuar a operar.","Recuperação concluída");
     }finally{if(File.Exists(staged))File.Delete(staged);}
    }catch(Exception ex){MessageBox.Show("Recuperação não aplicada: "+ex.Message,"Recuperação bloqueada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void AutoRecoverSales(){
   // Nunca altera o banco ativo: somente produz uma copia de teste.
   if(!File.Exists(dbPath))return;
   try{
    string[] dirs={dataDir,AppDomain.CurrentDomain.BaseDirectory,
      Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")};
    var sources=new System.Collections.Generic.List<string>();
    foreach(string dir in dirs){
     if(!Directory.Exists(dir))continue;
     try{
      foreach(string file in Directory.GetFiles(dir,"*.db",SearchOption.TopDirectoryOnly)){
       string name=Path.GetFileName(file).ToLowerInvariant();
       if(!name.StartsWith("recuperacao-automatica-") && (name.Contains("12.1.6")||name.Contains("consolidado")||name.Contains("antes-da-atualizacao")||name.Contains("recuperacao")||name.Contains("01_a_19")||name.Contains("01-a-19")||name.Contains("vendas_01"))&&
          !Path.GetFullPath(file).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))sources.Add(file);
      }
     }catch(System.UnauthorizedAccessException){}
    }
    int bestMissing=0;string best=null;
    using(var current=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
     current.Open();
     var known=new System.Collections.Generic.HashSet<long>();
     using(var q=new SQLiteCommand("SELECT id FROM sales",current))using(var reader=q.ExecuteReader())while(reader.Read())known.Add(Convert.ToInt64(reader[0]));
     foreach(string file in sources){
      try{
       using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=file,ReadOnly=true,FailIfMissing=true}.ToString())){
        source.Open();int missing=0;
        using(var q=new SQLiteCommand("SELECT id FROM sales",source))using(var reader=q.ExecuteReader())while(reader.Read())if(!known.Contains(Convert.ToInt64(reader[0])))missing++;
        if(missing>bestMissing){bestMissing=missing;best=file;}
       }
      }catch(SQLiteException){}
     }
    }
    if(best==null)return;
    string destination=Path.Combine(dataDir,"recuperacao-automatica-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db");
    using(var current=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
    using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=best,ReadOnly=true,FailIfMissing=true}.ToString()))
    using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=destination,FailIfMissing=false}.ToString())){
     current.Open();source.Open();dest.Open();
     using(var q=new SQLiteCommand("PRAGMA integrity_check",source))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Banco histórico com erro de integridade.");
     current.BackupDatabase(dest,"main","main",-1,null,0);
     var missing=new System.Collections.Generic.HashSet<long>();
     using(var q=new SQLiteCommand("SELECT id FROM sales",source))using(var rd=q.ExecuteReader()){
      while(rd.Read()){
       long id=Convert.ToInt64(rd[0]);
       using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE id=@id",dest)){
        exists.Parameters.AddWithValue("@id",id);
        if(Convert.ToInt32(exists.ExecuteScalar())==0)missing.Add(id);
       }
      }
     }
     using(var tx=dest.BeginTransaction()){
      foreach(string table in new[]{"sales","sale_items","sale_payments","credit_payments"}){
       var cols=new System.Collections.Generic.List<string>();
       using(var q=new SQLiteCommand("PRAGMA table_info("+table+")",source))using(var rd=q.ExecuteReader())while(rd.Read())cols.Add(Convert.ToString(rd["name"]));
       if(cols.Count==0)throw new Exception("Tabela histórica ausente: "+table);
       var targetCols=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
       using(var q=new SQLiteCommand("PRAGMA table_info("+table+")",dest,tx))using(var rd=q.ExecuteReader())while(rd.Read())targetCols.Add(Convert.ToString(rd["name"]));
       foreach(string col in cols)if(!targetCols.Contains(col))throw new Exception("Estrutura incompatível: "+table+"."+col);
       string fields=string.Join(",",cols.ConvertAll(x=>"\""+x.Replace("\"","\"\"")+"\"").ToArray());
       string parameters=string.Join(",",cols.ConvertAll(x=>"@"+x).ToArray());
       using(var q=new SQLiteCommand("SELECT "+fields+" FROM "+table,source))using(var rd=q.ExecuteReader()){
        while(rd.Read()){
         long saleId=Convert.ToInt64(rd[table=="sales"?"id":"sale_id"]);
         if(!missing.Contains(saleId))continue;
         long id=Convert.ToInt64(rd["id"]);
         using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM "+table+" WHERE id=@id",dest,tx)){
          exists.Parameters.AddWithValue("@id",id);
          if(Convert.ToInt32(exists.ExecuteScalar())!=0)throw new Exception("Conflito de ID "+id+" em "+table+". Cópia não aplicada.");
         }
         using(var insert=new SQLiteCommand("INSERT INTO "+table+" ("+fields+") VALUES ("+parameters+")",dest,tx)){
          foreach(string col in cols)insert.Parameters.AddWithValue("@"+col,rd[col]);
          insert.ExecuteNonQuery();
         }
        }
       }
      }
      tx.Commit();
     }
     using(var q=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Integridade da cópia recuperada inválida.");
    }
    status.Text="Recuperação automática: "+bestMissing+" vendas ausentes preparadas em cópia de teste.";
    MessageBox.Show("Foram localizadas "+bestMissing+" vendas ausentes da versão 12.1.6.\n\nUma cópia recuperada foi criada automaticamente em:\n"+destination+"\n\nO banco atual e o estoque NÃO foram alterados. Confira a cópia antes de substituir dados.","Vendas recuperadas para conferência",MessageBoxButtons.OK,MessageBoxIcon.Information);
   }catch(Exception ex){status.Text="Recuperação automática não concluída: "+ex.Message;}
  }
  void IntegrateHistoricalSales(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco atual.");return;}
   using(var picker=new OpenFileDialog {Title="Selecionar vendas de 01 a 19 de outubro de 2026",Filter="Banco de vendas (*.db)|*.db"}){
    if(picker.ShowDialog(this)!=DialogResult.OK)return;
    if(Path.GetFullPath(picker.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Escolha um banco histórico diferente do banco atual.");return;}
    try{
     int added=0,items=0,credit=0,payments=0;
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=picker.FileName,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var target=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=dbPath,FailIfMissing=true}.ToString())){
      source.Open();target.Open();
      using(var q=new SQLiteCommand("PRAGMA integrity_check",source))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Banco histórico danificado.");
      using(var q=new SQLiteCommand("PRAGMA integrity_check",target))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Banco atual danificado.");
      var missing=new System.Collections.Generic.HashSet<long>();
      using(var q=new SQLiteCommand("SELECT id FROM sales WHERE substr(created_at,1,10) BETWEEN '2026-10-01' AND '2026-10-19'",source))
      using(var reader=q.ExecuteReader()){
       while(reader.Read()){
        long id=Convert.ToInt64(reader[0]);
        using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE id=@id",target)){
         check.Parameters.AddWithValue("@id",id);
         if(Convert.ToInt32(check.ExecuteScalar())==0)missing.Add(id);
        }
       }
      }
      if(missing.Count==0){MessageBox.Show("Todas as vendas deste período já existem no banco atual.");return;}
      if(MessageBox.Show("Foram encontradas "+missing.Count+" vendas ausentes de 01/10 a 19/10/2026.\\n\\nCriar backup e integrar essas vendas com itens e pagamentos, sem alterar estoque nem vendas existentes?","Integrar vendas históricas",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      // Backup SQLite consistente antes de qualquer escrita.
      string backup=Path.Combine(dataDir,"antes-integracao-vendas-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db");
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup,FailIfMissing=false}.ToString())){
       dest.Open();target.BackupDatabase(dest,"main","main",-1,null,0);
      }
      using(var tx=target.BeginTransaction()){
       foreach(string table in new[]{"sales","sale_items","sale_payments","credit_payments"}){
        var cols=new System.Collections.Generic.List<string>();
        using(var q=new SQLiteCommand("PRAGMA table_info("+table+")",source))using(var rd=q.ExecuteReader())while(rd.Read())cols.Add(Convert.ToString(rd["name"]));
        if(cols.Count==0)throw new Exception("Tabela ausente no banco histórico: "+table);
        var destCols=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var q=new SQLiteCommand("PRAGMA table_info("+table+")",target,tx))using(var rd=q.ExecuteReader())while(rd.Read())destCols.Add(Convert.ToString(rd["name"]));
        foreach(string col in cols)if(!destCols.Contains(col))throw new Exception("Coluna incompatível: "+table+"."+col);
        string names=string.Join(",",cols.ConvertAll(x=>"\""+x.Replace("\"","\"\"")+"\"").ToArray());
        string values=string.Join(",",cols.ConvertAll(x=>"@"+x).ToArray());
        using(var q=new SQLiteCommand("SELECT "+names+" FROM "+table,source))using(var rd=q.ExecuteReader()){
         while(rd.Read()){
          long saleId=Convert.ToInt64(rd[table=="sales"?"id":"sale_id"]);
          if(!missing.Contains(saleId))continue;
          long id=Convert.ToInt64(rd["id"]);
          using(var check=new SQLiteCommand("SELECT COUNT(*) FROM "+table+" WHERE id=@id",target,tx)){
           check.Parameters.AddWithValue("@id",id);
           if(Convert.ToInt32(check.ExecuteScalar())!=0)throw new Exception("ID conflitante em "+table+": "+id+". Nenhum dado foi integrado.");
          }
          using(var insert=new SQLiteCommand("INSERT INTO "+table+" ("+names+") VALUES ("+values+")",target,tx)){
           foreach(string col in cols)insert.Parameters.AddWithValue("@"+col,rd[col]);
           insert.ExecuteNonQuery();
          }
          if(table=="sales")added++;else if(table=="sale_items")items++;else if(table=="credit_payments")credit++;else payments++;
         }
        }
       }
       tx.Commit();
      }
      using(var q=new SQLiteCommand("PRAGMA integrity_check",target))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Falha de integridade após integração. Restaure o backup: "+backup);
      MessageBox.Show("Integração concluída.\\nVendas: "+added+"\\nItens: "+items+"\\nPagamentos fiado: "+credit+"\\nOutros pagamentos: "+payments+"\\n\\nBackup: "+backup+"\\nEstoque preservado.","Vendas 01/10 a 19/10");
      ShowTable("Vendas","SELECT id,created_at,customer_name,payment_method,gross_total,status FROM sales ORDER BY created_at DESC LIMIT 2000");
     }
    }catch(Exception ex){MessageBox.Show("Integração não concluída: "+ex.Message+"\\nAs alterações de uma transação incompleta são revertidas.","Proteção das vendas",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   }
  }
  void PrepareRecoveredSales(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco atual.");return;}
   using(var pick=new OpenFileDialog {Title="Selecione o banco SQLite com vendas recuperadas",Filter="Banco SQLite (*.db)|*.db"}){
    if(pick.ShowDialog()!=DialogResult.OK)return;
    if(Path.GetFullPath(pick.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Selecione outro banco.");return;}
    using(var save=new SaveFileDialog {Title="Salvar banco de teste com vendas recuperadas",Filter="Banco SQLite (*.db)|*.db",FileName="armazem-recuperado-teste-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db"}){
     if(save.ShowDialog()!=DialogResult.OK)return;
     if(File.Exists(save.FileName)){MessageBox.Show("Escolha um arquivo novo, sem sobrescrever.");return;}
     if(Path.GetFullPath(save.FileName).Equals(Path.GetFullPath(pick.FileName),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("O destino não pode ser a origem.");return;}
     try{
      int imported=0,items=0,credits=0,payments=0;
      using(var original=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=save.FileName,FailIfMissing=false}.ToString()))
      using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=pick.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
       original.Open();dest.Open();source.Open();
       using(var cmd=new SQLiteCommand("PRAGMA integrity_check",source))if(Convert.ToString(cmd.ExecuteScalar())!="ok")throw new Exception("Banco de recuperação com erro de integridade.");
       original.BackupDatabase(dest,"main","main",-1,null,0);
       var absent=new System.Collections.Generic.HashSet<long>();
       using(var cmd=new SQLiteCommand("SELECT id FROM sales",source))using(var reader=cmd.ExecuteReader()){
        while(reader.Read()){
         long id=Convert.ToInt64(reader.GetValue(0));
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE id=@id",dest)){
          check.Parameters.AddWithValue("@id",id);
          if(Convert.ToInt32(check.ExecuteScalar())==0)absent.Add(id);
         }
        }
       }
       if(absent.Count==0){MessageBox.Show("Nenhuma venda ausente encontrada. Banco de teste criado sem alterações.");return;}
       if(MessageBox.Show("Foram identificadas "+absent.Count+" vendas ausentes. Criar uma CÓPIA de teste com essas vendas, itens e pagamentos? O estoque não será movimentado e o banco em uso ficará intacto.","Preparar recuperação",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
       using(var tx=dest.BeginTransaction()){
        foreach(string table in new[]{"sales","sale_items","sale_payments","credit_payments"}){
         var columns=new System.Collections.Generic.List<string>();
         using(var schema=new SQLiteCommand("PRAGMA table_info("+table+")",source))using(var reader=schema.ExecuteReader()){
          while(reader.Read())columns.Add(Convert.ToString(reader["name"]));
         }
         if(columns.Count==0)throw new Exception("Tabela ausente no arquivo de recuperação: "+table);
         var target=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
         using(var schema=new SQLiteCommand("PRAGMA table_info("+table+")",dest,tx))using(var reader=schema.ExecuteReader()){
          while(reader.Read())target.Add(Convert.ToString(reader["name"]));
         }
         foreach(string col in columns)if(!target.Contains(col))throw new Exception("Coluna incompatível em "+table+": "+col);
         string quoted=string.Join(",",columns.ConvertAll(x=>"\""+x.Replace("\"","\"\"")+"\"").ToArray());
         using(var query=new SQLiteCommand("SELECT "+quoted+" FROM "+table,source))
         using(var reader=query.ExecuteReader()){
          while(reader.Read()){
           long saleId=Convert.ToInt64(reader[table=="sales"?"id":"sale_id"]);
           if(!absent.Contains(saleId))continue;
           long rowId=Convert.ToInt64(reader["id"]);
           using(var check=new SQLiteCommand("SELECT COUNT(*) FROM "+table+" WHERE id=@id",dest,tx)){
            check.Parameters.AddWithValue("@id",rowId);
            if(Convert.ToInt32(check.ExecuteScalar())!=0)throw new Exception("Conflito de identificador em "+table+" ID "+rowId+". Nenhuma alteração será salva.");
           }
           string placeholders=string.Join(",",columns.ConvertAll(x=>"@"+x).ToArray());
           using(var insert=new SQLiteCommand("INSERT INTO "+table+" ("+quoted+") VALUES ("+placeholders+")",dest,tx)){
            foreach(string col in columns)insert.Parameters.AddWithValue("@"+col,reader[col]==DBNull.Value?(object)DBNull.Value:reader[col]);
            insert.ExecuteNonQuery();
           }
           if(table=="sales")imported++;
           if(table=="sale_items")items++;
           if(table=="credit_payments")credits++;
           if(table=="sale_payments")payments++;
          }
         }
        }
        tx.Commit();
       }
       using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Falha na integridade do banco de teste.");
      }
      MessageBox.Show("BANCO DE TESTE CRIADO"+Environment.NewLine+"Vendas: "+imported+Environment.NewLine+"Itens: "+items+Environment.NewLine+"Pagamentos Fiado: "+credits+Environment.NewLine+"Outros pagamentos: "+payments+Environment.NewLine+Environment.NewLine+"Arquivo: "+save.FileName+Environment.NewLine+"Banco original e estoque preservados.","Recuperação preparada");
     }catch(Exception ex){MessageBox.Show(ex.Message+Environment.NewLine+"O banco original não foi modificado. Se existir um arquivo de teste incompleto, descarte-o.","Recuperação interrompida",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
   }
  }
  void CompareSales(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro um banco para comparar.");return;}
   using(var dlg=new OpenFileDialog {Title="Selecione o banco original para localizar vendas posteriores (somente leitura)",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}){
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try{
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Escolha o banco original, diferente do banco importado.");
     string imported=SalesSummary(dbPath),original=SalesSummary(dlg.FileName);
     var known=new System.Collections.Generic.HashSet<long>();
     using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
      c.Open();using(var q=new SQLiteCommand("SELECT id FROM sales",c))using(var reader=q.ExecuteReader())
       while(reader.Read())known.Add(Convert.ToInt64(reader.GetValue(0)));
     }
     var missing=new System.Collections.Generic.List<string[]>();
     using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      c.Open();using(var q=new SQLiteCommand("SELECT id,created_at,customer_name,gross_total FROM sales ORDER BY id",c))
      using(var reader=q.ExecuteReader()){
       while(reader.Read()){
        long id=Convert.ToInt64(reader.GetValue(0));
        if(!known.Contains(id))missing.Add(new string[]{id.ToString(),Convert.ToString(reader.GetValue(1)),Convert.ToString(reader.GetValue(2)),Convert.ToString(reader.GetValue(3))});
       }
      }
     }
     string message="BANCO IMPORTADO"+Environment.NewLine+imported+Environment.NewLine+Environment.NewLine+
      "BANCO ORIGINAL SELECIONADO"+Environment.NewLine+original+Environment.NewLine+Environment.NewLine+
      "Vendas ausentes por número de identificação: "+missing.Count+Environment.NewLine+
      "A comparação considera todos os números, inclusive lacunas antigas."+Environment.NewLine+
      "Nenhum registro foi alterado. Números iguais podem conter dados diferentes.";
     if(missing.Count>0 && MessageBox.Show(message+Environment.NewLine+Environment.NewLine+"Deseja salvar a relação das vendas ausentes em CSV?","Localizar vendas ausentes",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes){
      using(var save=new SaveFileDialog {Title="Salvar relação de vendas ausentes",Filter="CSV (*.csv)|*.csv",FileName="vendas-ausentes-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
       if(save.ShowDialog()==DialogResult.OK){
        if(File.Exists(save.FileName))throw new Exception("Escolha um nome de relatório novo para não sobrescrever arquivos.");
        using(var writer=new StreamWriter(save.FileName,false,new System.Text.UTF8Encoding(true))){
         writer.WriteLine("numero;data;cliente;valor");
         foreach(var sale in missing){
          var parts=new System.Collections.Generic.List<string>();
          foreach(var value in sale)parts.Add(((char)34)+value.Replace(((char)34).ToString(),new string((char)34,2))+((char)34));
          writer.WriteLine(string.Join(";",parts.ToArray()));
         }
        }
        MessageBox.Show("Relatório salvo: "+save.FileName);
       }
      }
     }else if(missing.Count==0)MessageBox.Show(message,"Localizar vendas ausentes");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Comparação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void ImportDatabase(object sender,EventArgs e) {
   // Nunca substituir um banco existente durante importacao.
   // O fluxo de recuperacao cria uma COPIA de teste e preserva os registros ativos.
   if(File.Exists(dbPath)){
    MessageBox.Show("Ja existe um banco de dados no programa. A importacao direta foi bloqueada para impedir que vendas, fiado, produtos e pagamentos desaparecam.\n\nSelecione o banco de origem na proxima janela para PREPARAR uma recuperacao em copia de teste. O banco atual nao sera substituido.","Protecao dos dados",MessageBoxButtons.OK,MessageBoxIcon.Information);
    PrepareRecoveredSales(sender,e);
    return;
   }
   using(var dlg=new OpenFileDialog {Title="Selecionar banco antigo (somente leitura)",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}) {
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try {
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um banco de origem diferente.");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())) {
      source.Open();
      foreach(var table in new[]{"sales","sale_items","credit_payments","products"}) {
       using(var cmd=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name",source)) {cmd.Parameters.AddWithValue("@name",table);if(Convert.ToInt32(cmd.ExecuteScalar())==0)throw new Exception("Tabela ausente: "+table);}
      }
      var temp=Path.Combine(dataDir,"importacao-"+Guid.NewGuid().ToString("N")+".db");
      try {
       using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=temp,FailIfMissing=false}.ToString())) {dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);}
       using(var verify=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=temp,ReadOnly=true}.ToString())) {verify.Open();using(var check=new SQLiteCommand("PRAGMA integrity_check",verify))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Falha na verificacao SQLite");}
       if(File.Exists(dbPath))File.Copy(dbPath,dbPath+".backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),false);
       File.Copy(temp,dbPath,true);
      } finally {if(File.Exists(temp))File.Delete(temp);}
     }
     status.Text="Banco importado. Original preservado.";ShowTable("sales","SELECT id,created_at,customer_name,payment_method,gross_total,status FROM sales ORDER BY created_at DESC LIMIT 2000");
    } catch(Exception ex) {MessageBox.Show(ex.Message,"Importacao nao concluida",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void OpenConsult(bool credit) {if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new ConsultaForm(dbPath,credit));}
  void ShowTable(string table,string sql) {
   if(!File.Exists(dbPath)) {MessageBox.Show("Importe primeiro o banco de dados.");return;}
   try {using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true}.ToString())) {conn.Open();var dt=new DataTable();using(var cmd=new SQLiteCommand(sql,conn))using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(dt);grid.DataSource=dt;var home=Controls["homePanel"];if(home!=null){Controls.Remove(home);home.Dispose();}grid.Visible=true;grid.BringToFront();header.BringToFront();sideMenu.BringToFront();status.BringToFront();status.Text=table+": "+dt.Rows.Count+" registros exibidos (somente leitura).";}}
   catch(Exception ex) {MessageBox.Show(ex.Message,"Erro na consulta",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
 // Interface Windows Forms independente, sem navegador embutido e sem salvar o token no banco.
 public sealed class WhatsAppNormalForm:Form {
  readonly TextBox phone=new TextBox{Width=330};
  readonly TextBox message=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text="Olá! Aqui é do Armazém Gratidão."};
  readonly Label info=new Label{Dock=DockStyle.Bottom,Height=62,Text="WhatsApp comum: o envio é confirmado no WhatsApp Web ou aplicativo externo. PDFs precisam ser anexados manualmente.",Padding=new Padding(12)};
  public WhatsAppNormalForm(){
   Text="Armazém Gratidão | WhatsApp";Width=690;Height=480;MinimumSize=new Size(550,380);
   Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(229,242,255);
   var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=55,Padding=new Padding(10)};
   top.Controls.Add(new Label{Text="Número com DDI e DDD:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   top.Controls.Add(phone);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=62,Padding=new Padding(9)};
   var open=new Button{Text="Abrir conversa",Width=155,Height=40};
   var pdf=new Button{Text="Escolher PDF",Width=145,Height=40};
   var whatsapp=new Button{Text="Abrir WhatsApp Web",Width=175,Height=40};
   buttons.Controls.Add(open);buttons.Controls.Add(pdf);buttons.Controls.Add(whatsapp);
   open.Click+=(s,e)=>{
    string digits=new string(phone.Text.Where(char.IsDigit).ToArray());
    if(digits.Length<10||digits.Length>15){MessageBox.Show("Digite um número com DDI e DDD, por exemplo 5514999999999.");return;}
    OpenExternal("https://wa.me/"+digits+"?text="+Uri.EscapeDataString(message.Text));
   };
   pdf.Click+=(s,e)=>{
    using(var dlg=new OpenFileDialog{Filter="Arquivo PDF|*.pdf",Title="Selecionar comprovante"}){
     if(dlg.ShowDialog(this)!=DialogResult.OK)return;
     try{System.Windows.Forms.Clipboard.SetText(dlg.FileName);MessageBox.Show("Caminho do PDF copiado. Abra a conversa e anexe o arquivo pelo WhatsApp. O WhatsApp comum não permite anexação automática por link.","Comprovante");}
     catch(Exception ex){MessageBox.Show(ex.Message,"Comprovante");}
    }
   };
   whatsapp.Click+=(s,e)=>OpenExternal("https://web.whatsapp.com/");
   Controls.Add(message);Controls.Add(top);Controls.Add(buttons);Controls.Add(info);
  }
  static void OpenExternal(string url){
   try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
   catch(Exception ex){MessageBox.Show("Não foi possível abrir o WhatsApp: "+ex.Message);}
  }
 }
 public sealed class StockCountQuickForm:Form {
  readonly string database;
  readonly TextBox code=new TextBox{Width=240,Font=new Font("Segoe UI",15,FontStyle.Bold)};
  readonly TextBox description=new TextBox{Width=450,ReadOnly=true,BackColor=Color.White,Font=new Font("Segoe UI",12)};
  readonly NumericUpDown count=new NumericUpDown{Width=155,DecimalPlaces=3,Maximum=10000000,Minimum=0,Font=new Font("Segoe UI",14)};
  readonly Label entryDate=new Label{AutoSize=true,Padding=new Padding(12,8,0,0),Font=new Font("Segoe UI",10,FontStyle.Bold)};
  readonly Label status=new Label{Dock=DockStyle.Bottom,Height=48,Text="Bipe ou digite o código e pressione Enter.",Padding=new Padding(12,8,0,0)};
  long productId;decimal previous;string productBarcode;
  public StockCountQuickForm(string db){
   database=db;Text="Armazém Gratidão | Entrada rápida de quantidade";Width=930;Height=335;MinimumSize=new Size(740,260);
   Font=new Font("Segoe UI",10,FontStyle.Bold);BackColor=Color.FromArgb(246,249,253);
   var row=new FlowLayoutPanel{Dock=DockStyle.Top,Height=110,Padding=new Padding(14,16,10,8),AutoScroll=true,WrapContents=true};
   row.Controls.Add(new Label{Text="Código de barras",AutoSize=true,Padding=new Padding(0,9,0,0)});
   row.Controls.Add(code);
   row.Controls.Add(new Label{Text="Descrição",AutoSize=true,Padding=new Padding(0,9,0,0)});
   row.Controls.Add(description);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=65,Padding=new Padding(14),AutoScroll=true};
   actions.Controls.Add(new Label{Text="Quantidade de entrada",AutoSize=true,Padding=new Padding(0,9,0,0)});
   actions.Controls.Add(count);
   var save=new Button{Text="Registrar entrada",Width=220,Height=37,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
   var clear=new Button{Text="Próximo produto",Width=150,Height=37};
   actions.Controls.Add(save);actions.Controls.Add(clear);
   var dateRow=new Panel{Dock=DockStyle.Top,Height=36};dateRow.Controls.Add(entryDate);
   entryDate.Text="Data e hora da entrada: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
   Controls.Add(status);Controls.Add(actions);Controls.Add(row);Controls.Add(dateRow);
   code.KeyDown+=(sender,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Search();}};
   save.Click+=(sender,e)=>SaveCount();
   clear.Click+=(sender,e)=>Reset();
   Shown+=(sender,e)=>code.Focus();
  }
  SQLiteConnection Open(){var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=database,FailIfMissing=true}.ToString());conn.Open();return conn;}
  void Reset(){productId=0;previous=0;productBarcode=null;code.Clear();description.Clear();count.Value=0;entryDate.Text="Data e hora da entrada: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");status.Text="Pronto para o próximo produto.";code.Focus();}
  void Search(){
   productId=0;description.Clear();count.Value=0;
   string entered=code.Text.Trim();
   if(entered.Length==0){status.Text="Informe o código do produto.";return;}
   try{
    using(var conn=Open())
    using(var cmd=new SQLiteCommand("SELECT id,barcode,name,COALESCE(stock,0) FROM products WHERE TRIM(COALESCE(barcode,''))=@code OR TRIM(COALESCE(internal_code,''))=@code LIMIT 2",conn)){
     cmd.Parameters.AddWithValue("@code",entered);
     using(var rd=cmd.ExecuteReader()){
      if(!rd.Read()){status.Text="Produto não encontrado: "+entered;return;}
      long id=Convert.ToInt64(rd[0]);string barcode=Convert.ToString(rd[1]), name=Convert.ToString(rd[2]);
      decimal recorded=Convert.ToDecimal(rd[3],System.Globalization.CultureInfo.InvariantCulture);
      if(rd.Read()){status.Text="Código ambíguo; confira o cadastro.";return;}
      productId=id;productBarcode=barcode;previous=recorded;description.Text=name;
      count.Value=0;status.Text="Estoque atual: "+recorded.ToString("N3")+". Informe apenas a quantidade que está entrando.";
      count.Focus();count.Select(0,count.Text.Length);
     }
    }
   }catch(Exception ex){status.Text="Erro na consulta: "+ex.Message;}
  }
  void SaveCount(){
   if(productId==0){MessageBox.Show("Pesquise um produto antes de salvar.");return;}
   decimal entered=count.Value;
   if(entered<=0){MessageBox.Show("Informe a quantidade positiva que está entrando.");return;}
   DateTime movementTime=DateTime.Now;
   decimal next=previous+entered;
   if(next>10000000){MessageBox.Show("Quantidade final acima do limite permitido.");return;}
   if(MessageBox.Show("Registrar ENTRADA de "+entered.ToString("N3")+" unidades de "+description.Text+"?\nData: "+movementTime.ToString("dd/MM/yyyy HH:mm:ss")+"\nAnterior: "+previous.ToString("N3")+"\nApós entrada: "+next.ToString("N3"),"Entrada rápida de estoque",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   try{
    using(var conn=Open()){
     using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('runtime_operation_context','stock_audit_log')",conn))
      if(Convert.ToInt32(check.ExecuteScalar())!=2)throw new Exception("Proteção e auditoria de estoque não disponíveis.");
     string backup=database+".antes-conferencia-fisica-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){dest.Open();conn.BackupDatabase(dest,"main","main",-1,null,0);}
     using(var tx=conn.BeginTransaction()){
      decimal current;
      using(var cmd=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",conn,tx)){cmd.Parameters.AddWithValue("@id",productId);object value=cmd.ExecuteScalar();if(value==null)throw new Exception("Produto não encontrado.");current=Convert.ToDecimal(value==DBNull.Value?0:value,System.Globalization.CultureInfo.InvariantCulture);}
      if(current!=previous)throw new Exception("Estoque alterado desde a pesquisa. Pesquise novamente para evitar sobrescrever outra movimentação.");
      int oldAuth;string oldOp;
      using(var cmd=new SQLiteCommand("SELECT authorized,operation FROM runtime_operation_context WHERE id=1",conn,tx))
      using(var reader=cmd.ExecuteReader()){if(!reader.Read())throw new Exception("Contexto de proteção ausente.");oldAuth=Convert.ToInt32(reader[0]);oldOp=Convert.ToString(reader[1]);}
      using(var cmd=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=1,operation='ENTRADA_QTD_AUDITADA' WHERE id=1",conn,tx))
       if(cmd.ExecuteNonQuery()!=1)throw new Exception("Não foi possível autorizar conferência.");
      using(var cmd=new SQLiteCommand("UPDATE products SET stock=@physical WHERE id=@id AND stock=@previous",conn,tx)){cmd.Parameters.AddWithValue("@physical",next);cmd.Parameters.AddWithValue("@id",productId);cmd.Parameters.AddWithValue("@previous",previous);if(cmd.ExecuteNonQuery()!=1)throw new Exception("Estoque alterado durante a conferência.");}
      using(var cmd=new SQLiteCommand("INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reason) VALUES(@id,@name,@barcode,'ENTRADA_QTD',@before,@after,@delta,'ESTOQUE',@reason)",conn,tx)){
       cmd.Parameters.AddWithValue("@id",productId);cmd.Parameters.AddWithValue("@name",description.Text);cmd.Parameters.AddWithValue("@barcode",productBarcode??"");cmd.Parameters.AddWithValue("@before",previous);cmd.Parameters.AddWithValue("@after",next);cmd.Parameters.AddWithValue("@delta",entered);cmd.Parameters.AddWithValue("@reason","Entrada rápida de quantidade em "+movementTime.ToString("yyyy-MM-dd HH:mm:ss"));if(cmd.ExecuteNonQuery()!=1)throw new Exception("Falha na auditoria.");}
      using(var cmd=new SQLiteCommand("CREATE TABLE IF NOT EXISTS product_quantity_movements (id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,movement_at TEXT NOT NULL,quantity_before NUMERIC NOT NULL,quantity_added NUMERIC NOT NULL,quantity_after NUMERIC NOT NULL,reason TEXT NOT NULL)",conn,tx))cmd.ExecuteNonQuery();
      using(var cmd=new SQLiteCommand("INSERT INTO product_quantity_movements(product_id,movement_at,quantity_before,quantity_added,quantity_after,reason) VALUES(@id,@at,@before,@added,@after,@reason)",conn,tx)){
       cmd.Parameters.AddWithValue("@id",productId);cmd.Parameters.AddWithValue("@at",movementTime.ToString("yyyy-MM-dd HH:mm:ss"));
       cmd.Parameters.AddWithValue("@before",previous);cmd.Parameters.AddWithValue("@added",entered);cmd.Parameters.AddWithValue("@after",next);
       cmd.Parameters.AddWithValue("@reason","Entrada rápida de estoque");if(cmd.ExecuteNonQuery()!=1)throw new Exception("Falha ao registrar histórico da entrada.");
      }
      using(var cmd=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=@auth,operation=@operation WHERE id=1",conn,tx)){cmd.Parameters.AddWithValue("@auth",oldAuth);cmd.Parameters.AddWithValue("@operation",oldOp);if(cmd.ExecuteNonQuery()!=1)throw new Exception("Falha ao restaurar proteção.");}
      tx.Commit();
     }
    }
    Reset();status.Text="Entrada salva com data, hora e histórico. Pronto para o próximo produto.";
   }catch(Exception ex){MessageBox.Show("Quantidade não salva: "+ex.Message,"Conferência",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
 }

}
