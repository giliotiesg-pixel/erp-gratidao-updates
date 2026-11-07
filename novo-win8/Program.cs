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
  static DateTime nextVisualScanUtc=DateTime.MinValue;
  [STAThread] static void Main() {
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   // Nao percorrer todas as janelas a cada mensagem ociosa do Windows: em PCs lentos isso consome CPU.
   Application.Idle+=(sender,args)=>{
    var now=DateTime.UtcNow;
    if(now<nextVisualScanUtc)return;
    nextVisualScanUtc=now.AddMilliseconds(3500);
    ButtonTextVisibility.CheckOpenForms();
    PdvShortcutButtons.CheckOpenForms();
    WindowBoundsGuard.CheckOpenForms();
    RoundedGridTheme.CheckOpenForms();
    ModuleVisualTheme.CheckOpenForms();
   };
   Application.Run(new MainForm());
  }
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
    // No estoque, usar a barra ja reservada no layout. Nao adicionar outro Dock.Top
    // diretamente no formulario, pois ele escondia o cabecalho e os primeiros produtos.
    var stockNavigation=form.Controls.Find("amzgBackBar",true).FirstOrDefault() as Panel;
    if(form is ModuloConsultaForm&&form.Text.Contains("Estoque")&&stockNavigation!=null){
     shortcut.Width=150;shortcut.Height=27;shortcut.Top=3;
     shortcut.Anchor=AnchorStyles.Right|AnchorStyles.Top;
     stockNavigation.Controls.Add(shortcut);
     Action position=()=>{shortcut.Left=Math.Max(8,stockNavigation.ClientSize.Width-132-shortcut.Width-25);};
     stockNavigation.Resize+=(sender,args)=>position();
     position();
    }else{
     strip.Controls.Add(shortcut);
     form.Controls.Add(strip);
     strip.BringToFront();
    }
    form.FormClosed+=(sender,args)=>installed.Remove(form);
   }
  }
 }
 // Padrão visual global de tabelas: somente apresentação.
 // Aplicado a controles criados dinamicamente e novas janelas, sem trocar
 // DataSource, seleção, eventos, valores ou comportamento do editor.
 static class RoundedGridTheme {
  static readonly System.Collections.Generic.HashSet<DataGridView> styled=new System.Collections.Generic.HashSet<DataGridView>();
  static void Apply(DataGridView table){
   if(table.IsDisposed||!styled.Add(table))return;
   table.BorderStyle=BorderStyle.None;
   table.BackgroundColor=Color.White;
   table.EnableHeadersVisualStyles=false;
   table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(22,75,160);
   table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   table.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(22,75,160);
   table.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);
   table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(215,232,252);
   table.DefaultCellStyle.SelectionForeColor=Color.FromArgb(18,54,109);
   table.GridColor=Color.FromArgb(227,235,248);
   table.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   table.RowHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
   table.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
   table.Paint+=(sender,args)=>{
    if(table.ClientSize.Width<22||table.ClientSize.Height<22)return;
    RoundedVisuals.DrawOutline(args.Graphics,
     new Rectangle(1,1,table.ClientSize.Width-3,table.ClientSize.Height-3),
     10,Color.FromArgb(172,194,228),1.6f);
   };
   table.Disposed+=(sender,args)=>styled.Remove(table);
  }
  static readonly System.Collections.Generic.HashSet<Control> watched=
   new System.Collections.Generic.HashSet<Control>();
  static void Watch(Control parent){
   if(parent==null||parent.IsDisposed||!watched.Add(parent))return;
   parent.Disposed+=(sender,args)=>watched.Remove(parent);
   var grid=parent as DataGridView;
   if(grid!=null){Apply(grid);return;}
   parent.ControlAdded+=(sender,args)=>Watch(args.Control);
   foreach(Control child in parent.Controls)Watch(child);
  }
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms)Watch(form);
  }
 }
 static class RoundedVisuals {
  public static void DrawOutline(Graphics graphics,Rectangle area,int radius,Color color,float weight){
   if(area.Width<radius*2+3||area.Height<radius*2+3)return;
   int d=radius*2;
   using(var path=new System.Drawing.Drawing2D.GraphicsPath()){
    path.AddArc(area.Left,area.Top,d,d,180,90);
    path.AddArc(area.Right-d,area.Top,d,d,270,90);
    path.AddArc(area.Right-d,area.Bottom-d,d,d,0,90);
    path.AddArc(area.Left,area.Bottom-d,d,d,90,90);
    path.CloseFigure();
    graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    using(var border=new Pen(color,weight))graphics.DrawPath(border,path);
   }
  }
 }
 // Tema visual nativo para cada módulo e para diálogos criados depois.
 // Só altera acabamento de controles; nunca tamanho, posição, edição,
 // DataSource, navegacao, eventos ou conteúdo de dados do cliente.
 static class ModuleVisualTheme {
  static readonly System.Collections.Generic.HashSet<Form> forms=new System.Collections.Generic.HashSet<Form>();
  static readonly System.Collections.Generic.HashSet<Control> decorated=new System.Collections.Generic.HashSet<Control>();
  static readonly Color outline=Color.FromArgb(178,201,232);
  static readonly Color blue=Color.FromArgb(20,69,147);
  static void Round(Control c,int radius){
   if(c.IsDisposed||c.Width<radius*2+4||c.Height<radius*2+4)return;
   var rect=new Rectangle(0,0,c.Width,c.Height);
   int diameter=radius*2;
   using(var path=new System.Drawing.Drawing2D.GraphicsPath()){
    path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);
    path.AddArc(rect.Right-diameter-1,rect.Top,diameter,diameter,270,90);
    path.AddArc(rect.Right-diameter-1,rect.Bottom-diameter-1,diameter,diameter,0,90);
    path.AddArc(rect.Left,rect.Bottom-diameter-1,diameter,diameter,90,90);
    path.CloseFigure();
    var old=c.Region;
    c.Region=new Region(path);
    if(old!=null)old.Dispose();
   }
  }
  static void Apply(Control control){
   if(control.IsDisposed||!decorated.Add(control))return;
   control.Disposed+=(sender,args)=>decorated.Remove(control);
   var button=control as Button;
   if(button!=null){
    // Atalhos de Inicio e menu ja possuem moldura propria.
    bool ownFrame=string.Equals(Convert.ToString(button.Tag),"home-shortcut",StringComparison.Ordinal)
       ||(button.Parent is FlowLayoutPanel && button.FindForm() is MainForm);
    // Todos os botoes recebem regiao arredondada, inclusive atalhos e menu lateral.
    // Os que ja possuem desenho proprio mantem sua moldura e seus eventos.
    Action roundButton=()=>Round(button,9);
    button.Resize+=(sender,args)=>roundButton();
    roundButton();
    if(!ownFrame){
     button.FlatStyle=FlatStyle.Flat;
     button.FlatAppearance.BorderSize=0;
     // Padrao visual unico: azul vivo, letras brancas, negrito e cantos arredondados.
     // Preserva cores de alerta/exclusao e dimensoes dos formularios para nao cortar textos.
     bool danger=button.Text!=null && (button.Text.IndexOf("Excluir",StringComparison.OrdinalIgnoreCase)>=0
       ||button.Text.IndexOf("Apagar",StringComparison.OrdinalIgnoreCase)>=0
       ||button.Text.IndexOf("Remover",StringComparison.OrdinalIgnoreCase)>=0);
     if(!danger){
      button.BackColor=Color.FromArgb(22,75,160);
      button.ForeColor=Color.White;
     }
     if(button.Font!=null && !button.Font.Bold)
      button.Font=new Font(button.Font,FontStyle.Bold);
     button.FlatAppearance.MouseOverBackColor=danger?Color.FromArgb(180,53,65):Color.FromArgb(36,96,185);
     button.FlatAppearance.MouseDownBackColor=danger?Color.FromArgb(150,39,52):Color.FromArgb(13,53,128);
     button.Paint+=(sender,args)=>RoundedVisuals.DrawOutline(args.Graphics,
      new Rectangle(1,1,button.ClientSize.Width-3,button.ClientSize.Height-3),
      9,danger?Color.FromArgb(175,60,75):Color.FromArgb(72,119,194),1.25f);
    }
   }else if(control is TextBox){
    var input=(TextBox)control;
    if(!input.Multiline&&input.BorderStyle!=BorderStyle.None){
     input.BorderStyle=BorderStyle.FixedSingle;
     Action round=()=>Round(input,6);
     input.Resize+=(sender,args)=>round();
     round();
    }
   }else if(control is ComboBox){
    var select=(ComboBox)control;
    select.FlatStyle=FlatStyle.Flat;
    Action round=()=>Round(select,6);
    select.Resize+=(sender,args)=>round();
    round();
   }else if(control is NumericUpDown){
    var number=(NumericUpDown)control;
    number.BorderStyle=BorderStyle.FixedSingle;
    Action round=()=>Round(number,6);
    number.Resize+=(sender,args)=>round();
    round();
   }else if(control is DateTimePicker){
    var date=(DateTimePicker)control;
    Action round=()=>Round(date,6);
    date.Resize+=(sender,args)=>round();
    round();
   }else if(control is GroupBox){
    var group=(GroupBox)control;
    group.ForeColor=blue;
   }else if(control is Panel && !(control is FlowLayoutPanel) && !(control is TableLayoutPanel)){
    var panel=(Panel)control;
    if(panel.BorderStyle==BorderStyle.FixedSingle){
     panel.BorderStyle=BorderStyle.None;
     panel.Paint+=(sender,args)=>RoundedVisuals.DrawOutline(args.Graphics,
      new Rectangle(1,1,panel.ClientSize.Width-3,panel.ClientSize.Height-3),
      10,outline,1.35f);
    }
   }
   // Mudanças de campos dentro de uma janela também ganham o acabamento.
   control.ControlAdded+=(sender,args)=>DecorateTree(args.Control);
  }
  static void DecorateTree(Control root){
   if(root==null||root.IsDisposed)return;
   Apply(root);
   // O grid mantém seus próprios editores e é tratado separadamente.
   if(root is DataGridView)return;
   foreach(Control child in root.Controls)DecorateTree(child);
  }
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms){
    if(form==null||form.IsDisposed||!forms.Add(form))continue;
    DecorateTree(form);
    form.FormClosed+=(sender,args)=>forms.Remove(form);
   }
  }
 }
 static class ButtonTextVisibility {
  // Aplicar a correção apenas uma vez por controle. Os eventos acompanham
  // mudanças posteriores: não varrer a árvore de dezenas de janelas no Idle.
  static readonly System.Collections.Generic.HashSet<Control> watched=
   new System.Collections.Generic.HashSet<Control>();
  static readonly System.Collections.Generic.Dictionary<Button,string> inspected=
   new System.Collections.Generic.Dictionary<Button,string>();
  static void Inspect(Button button){
   if(button.IsDisposed)return;
   string signature=button.Text+"|"+button.Font.SizeInPoints+"|"+button.Width;
   string previous;
   if(inspected.TryGetValue(button,out previous)&&previous==signature)return;
   button.AutoEllipsis=true;
   button.UseMnemonic=false;
   if(!string.Equals(Convert.ToString(button.Tag),"home-shortcut",StringComparison.Ordinal)){
    var size=TextRenderer.MeasureText(button.Text,button.Font,new Size(1000,1000),TextFormatFlags.SingleLine);
    int desired=size.Width+24;
    if(button.Image!=null)desired+=button.Image.Width+8;
    if(button.Width<desired && button.Dock==DockStyle.None &&
      button.Parent is FlowLayoutPanel &&
      !(button.MinimumSize.Width>0&&button.MaximumSize.Width==button.MinimumSize.Width))
     button.Width=desired;
    button.TextAlign=ContentAlignment.MiddleCenter;
   }
   inspected[button]=button.Text+"|"+button.Font.SizeInPoints+"|"+button.Width;
  }
  static void Watch(Control c){
   if(c==null||c.IsDisposed||!watched.Add(c))return;
   c.Disposed+=(sender,args)=>{
    watched.Remove(c);
    var b=c as Button;
    if(b!=null)inspected.Remove(b);
   };
   var button=c as Button;
   if(button!=null){
    button.TextChanged+=(sender,args)=>Inspect(button);
    button.FontChanged+=(sender,args)=>Inspect(button);
    button.SizeChanged+=(sender,args)=>Inspect(button);
    Inspect(button);
   }
   if(c is DataGridView)return;
   c.ControlAdded+=(sender,args)=>Watch(args.Control);
   foreach(Control child in c.Controls)Watch(child);
  }
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms)Watch(form);
  }
 }
 public sealed class MainForm : Form {
  readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
  readonly string dbPath;
  MobileStockServer mobileStock;
  readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer {Interval=6*60*60*1000};
  readonly System.Windows.Forms.Timer dailyBackupTimer=new System.Windows.Forms.Timer {Interval=60*60*1000};
  bool dailyBackupRunning=false;
  readonly System.Threading.SemaphoreSlim backupGate=new System.Threading.SemaphoreSlim(1,1);
  bool checkingUpdates=false;
  Form modulesWindow;
  int lastPromptedBuild=-1;
  int InstalledBuild { get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build; } }
  readonly FlowLayoutPanel sideMenu=new FlowLayoutPanel {Dock=DockStyle.Left,Width=231,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(5,7,5,7),BackColor=Color.FromArgb(234,241,251)};
  readonly Panel header=new Panel {Dock=DockStyle.Top,Height=66,BackColor=Color.White};
  readonly Button menuToggle=new Button {Text="▦  Menu Principal",Width=155,Height=40,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
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
   menuToggle.Left=14;menuToggle.Top=13;menuToggle.FlatAppearance.BorderSize=0;
   menuToggle.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
   menuToggle.FlatAppearance.MouseDownBackColor=Color.FromArgb(13,53,128);
   menuToggle.Click+=(s,e)=>ShowMainMenu();
   RoundHomeControl(menuToggle,10);header.Controls.Add(menuToggle);
   // Atualização sempre acessível no cabeçalho, independentemente do módulo aberto.
   var updateHeader=new Button{Text="Verificar Atualização",Left=181,Top=13,Width=177,Height=40,
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(62,76,99),ForeColor=Color.White,
    Font=new Font("Segoe UI",9.5f,FontStyle.Bold)};
   updateHeader.FlatAppearance.BorderSize=0;
   updateHeader.Click+=async (sender,args)=>await CheckUpdates();
   RoundHomeControl(updateHeader,9);header.Controls.Add(updateHeader);
   var inicioHeader=new Button{Text="Início",Left=367,Top=13,Width=90,Height=40,
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
    Font=new Font("Segoe UI",9.5f,FontStyle.Bold)};
   inicioHeader.FlatAppearance.BorderSize=0;
   inicioHeader.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
   inicioHeader.Click+=ShowHome;
   RoundHomeControl(inicioHeader,9);header.Controls.Add(inicioHeader);
   var brand=new Label {Text="ARMAZÉM GRATIDÃO",ForeColor=Color.FromArgb(180,35,50),Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Left=475,Top=17};header.Controls.Add(brand);
   AddSection(bar,"PRINCIPAL");
   AddButton(bar,"Início / Indicadores",ShowHome);
   AddButton(bar,"Verificar atualização",async (s,e)=>await CheckUpdates());
   AddSection(bar,"DADOS E RECUPERAÇÃO");
   AddButton(bar,"Importar banco",ImportDatabase);
   AddButton(bar,"Backup do banco",BackupDatabase);
   AddButton(bar,"Pasta do backup das atualizacoes",ConfigureUpdateBackupFolder);
   AddButton(bar,"Central de manutencao", (sender,args)=>OpenProgramFilesFolder());
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
   Controls.Add(grid);Controls.Add(status);Controls.Add(bar);Controls.Add(header);
   EnableWheelScrolling(bar);
   bar.Visible=false;Directory.CreateDirectory(dataDir);
   status.Text=File.Exists(dbPath)?"Banco encontrado. Selecione Vendas ou Fiado.":"Importe uma copia do banco SQLite.";
   ShowMainMenu();
   Shown+=async (sender,args)=>{
    // Abertura do caixa recebe prioridade. Tarefas automáticas só começam
    // depois que o menu já estiver desenhado e respondendo ao usuário.
    await Task.Delay(12000);
    if(!IsDisposed)await EnsureDailyBackupAsync();
    // A verificação remota e a procura de vendas antigas são secundárias.
    await Task.Delay(90000);
    if(!IsDisposed)await CheckUpdates(false);
    if(!IsDisposed)await Task.Run(()=>AutoRecoverSalesIfDue());
   };
   dailyBackupTimer.Tick+=async (s,e)=>await EnsureDailyBackupAsync();
   dailyBackupTimer.Start();
   updateTimer.Tick+=async (s,e)=>await CheckUpdates(false);
   updateTimer.Start();
   FormClosed+=(s,e)=>{dailyBackupTimer.Stop();dailyBackupTimer.Dispose();updateTimer.Stop();updateTimer.Dispose();if(mobileStock!=null){mobileStock.Dispose();mobileStock=null;}};
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
  string UpdateBackupDirectory(){
   string file=Path.Combine(dataDir,"backup-location.txt");
   string unified=Path.Combine(Application.StartupPath,"Arquivos","Backups");
   // NUNCA trocar a pasta que o usuario ja configurou.
   string directory=File.Exists(file)?File.ReadAllText(file).Trim():
    (Directory.Exists(unified)?unified:Path.Combine(dataDir,"Backups"));
   if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory))
    throw new Exception("A pasta de backup configurada e invalida.");
   Directory.CreateDirectory(directory);
   return directory;
  }
  void ConfigureUpdateBackupFolder(object sender,EventArgs e){
   try{
    using(var dlg=new FolderBrowserDialog{Description="Selecione a pasta onde os backups antes de cada atualizacao serao salvos",SelectedPath=UpdateBackupDirectory()}){
     if(dlg.ShowDialog(this)!=DialogResult.OK)return;
     Directory.CreateDirectory(dlg.SelectedPath);
     File.WriteAllText(Path.Combine(dataDir,"backup-location.txt"),dlg.SelectedPath);
     MessageBox.Show("Pasta de backup configurada: "+dlg.SelectedPath+"\\nAs atualizacoes serao bloqueadas se o backup falhar.","Armazem Gratidao");
    }
   }catch(Exception ex){MessageBox.Show("Nao foi possivel configurar o backup: "+ex.Message,"Backup",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Uma copia consistente por dia, ao abrir o sistema e enquanto permanecer aberto.
  // Falhas ficam visiveis; nunca registrar sucesso sem validar a integridade.
  async Task EnsureDailyBackupAsync(){
   if(dailyBackupRunning||!File.Exists(dbPath))return;
   dailyBackupRunning=true;
   try{
    await backupGate.WaitAsync();
    try{
     // Copia e verificacao SQLite feitas fora da thread de interface.
     string report=await Task.Run(()=>{
      string folder=UpdateBackupDirectory();
      string marker=Path.Combine(folder,"ultimo-backup-diario.txt");
      string today=DateTime.Now.ToString("yyyy-MM-dd");
      if(File.Exists(marker)){
       string[] lines=File.ReadAllLines(marker);
       if(lines.Length>=2&&lines[0]==today&&File.Exists(lines[1])&&new FileInfo(lines[1]).Length>0)return null;
      }
      string backup=CreateVerifiedPreUpdateBackup();
      if(!File.Exists(backup))throw new Exception("Arquivo de backup não encontrado.");
      string tmp=marker+".tmp";
      File.WriteAllLines(tmp,new[]{today,backup,DateTime.Now.ToString("o")});
      if(File.Exists(marker))File.Delete(marker);
      File.Move(tmp,marker);
      return "Backup diário verificado: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm");
     });
     if(report!=null&&!IsDisposed)status.Text=report;
    }finally{backupGate.Release();}
   }catch(Exception ex){
    if(!IsDisposed)status.Text="ATENÇÃO: backup diário falhou: "+ex.Message;
    // Não bloquear as vendas; uma nova tentativa ocorrerá na próxima verificação.
   }finally{dailyBackupRunning=false;}
  }
  string CreateVerifiedPreUpdateBackup(string destination=null){
   if(!File.Exists(dbPath))return "Sem banco cadastrado (instalação inicial)";
   Directory.CreateDirectory(dataDir);
   string backup=destination??Path.Combine(UpdateBackupDirectory(),"armazem-gratidao-antes-atualizacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
   try{
    Directory.CreateDirectory(Path.GetDirectoryName(backup));
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
  // Backup integral em PASTA: evita dependencias System.IO.Compression
  // nao disponiveis em alguns computadores Windows 8.
  // O banco e copiado pelo SQLite BackupDatabase, nunca pela copia bruta em uso.
  string CreateVerifiedFullUpdateBackup(){
   if(!File.Exists(dbPath))throw new Exception("Banco nao encontrado. Atualizacao bloqueada.");
   // O backup integral gera apenas UMA copia SQLite verificada, diretamente na pasta final.
   string folder=Path.Combine(UpdateBackupDirectory(),
    "armazem-gratidao-completo-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
   string dataBackup=Path.Combine(folder,"Dados");
   try{
    Directory.CreateDirectory(dataBackup);
    string dbCopy=Path.Combine(dataBackup,"armazem-gratidao.db");
    CreateVerifiedPreUpdateBackup(dbCopy);
    if(new FileInfo(dbCopy).Length<=0)throw new Exception("Backup do banco ficou vazio.");
    string root=Path.GetFullPath(dataDir).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    string backupRoot=Path.GetFullPath(UpdateBackupDirectory()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    string updatesRoot=Path.GetFullPath(UpdatesDirectory()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    string legacyBackupRoot=Path.GetFullPath(Path.Combine(dataDir,"Backups")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    string legacyUpdateRoot=Path.GetFullPath(Path.Combine(dataDir,"Atualizacoes")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    var pending=new System.Collections.Generic.Stack<string>();
    pending.Push(dataDir);
    while(pending.Count>0){
     string current=pending.Pop();
     foreach(string sub in Directory.GetDirectories(current)){
      string full=Path.GetFullPath(sub).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
      if(full.StartsWith(backupRoot,StringComparison.OrdinalIgnoreCase)||
         full.StartsWith(updatesRoot,StringComparison.OrdinalIgnoreCase)||
         full.StartsWith(legacyBackupRoot,StringComparison.OrdinalIgnoreCase)||
         full.StartsWith(legacyUpdateRoot,StringComparison.OrdinalIgnoreCase)||
         (File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0)continue;
      pending.Push(sub);
     }
     foreach(string file in Directory.GetFiles(current)){
      string full=Path.GetFullPath(file);
      string name=Path.GetFileName(full);
      if(string.Equals(full,dbPath,StringComparison.OrdinalIgnoreCase)||
         full.EndsWith(".db-wal",StringComparison.OrdinalIgnoreCase)||
         full.EndsWith(".db-shm",StringComparison.OrdinalIgnoreCase)||
         name.StartsWith("armazem-gratidao-completo-",StringComparison.OrdinalIgnoreCase)||
         name.StartsWith("armazem-gratidao-antes-atualizacao-",StringComparison.OrdinalIgnoreCase))continue;
      string relative=full.Substring(root.Length);
      string target=Path.Combine(dataBackup,relative);
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      File.Copy(file,target);
      if(new FileInfo(target).Length!=new FileInfo(file).Length)
       throw new Exception("Falha ao validar copia de "+relative);
     }
    }
    if(!File.Exists(dbCopy)||new FileInfo(dbCopy).Length<=0)throw new Exception("Backup completo invalido.");
    return folder;
   }catch{
    try{if(Directory.Exists(folder))Directory.Delete(folder,true);}catch{}
    throw;
   }
  }
  string UpdatesDirectory(){
   // O instalador unico prepara esta pasta com permissoes de escrita para o usuario.
   string unified=Path.Combine(Application.StartupPath,"Arquivos","Atualizacoes");
   string folder=Directory.Exists(unified)?unified:Path.Combine(dataDir,"Atualizacoes");
   Directory.CreateDirectory(folder);
   return folder;
  }
  void OpenSavedUpdates(){
   try{
    string folder=UpdatesDirectory();
    Process.Start("explorer.exe","\""+folder+"\"");
   }catch(Exception ex){MessageBox.Show(this,ex.Message,"Atualizações Salvas",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  async Task<string> DownloadVerifiedUpdate(int build,string url,string digest,bool zipPackage){
   string installer=Path.Combine(UpdatesDirectory(),"Armazem-Gratidao-build-"+build+"-Atualizacao"+(zipPackage?".zip":".exe"));
   // Aproveitar versoes baixadas antes de unificar as pastas, sem redownload.
   if(!File.Exists(installer)){
    string legacy=Path.Combine(dataDir,"Atualizacoes",Path.GetFileName(installer));
    if(!string.Equals(legacy,installer,StringComparison.OrdinalIgnoreCase)&&File.Exists(legacy)){
     using(var old=File.OpenRead(legacy))using(var sha=SHA256.Create()){
      string hash=BitConverter.ToString(sha.ComputeHash(old)).Replace("-","").ToLowerInvariant();
      if(string.Equals(hash,digest.Substring(7),StringComparison.OrdinalIgnoreCase))
       File.Copy(legacy,installer,false);
     }
    }
   }
   bool valid=false;
   if(File.Exists(installer)){
    using(var stream=File.OpenRead(installer))using(var sha=SHA256.Create())
     valid=string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),digest.Substring(7),StringComparison.OrdinalIgnoreCase);
    if(!valid)File.Delete(installer);
   }
   if(!valid){
    string temp=installer+".download";
    try{
     status.Text="Salvando atualização build "+build+"...";
     using(var web=new WebClient()){
      web.Headers[HttpRequestHeader.UserAgent]="ArmazemGratidaoNovo-Windows";
      await web.DownloadFileTaskAsync(new Uri(url),temp);
     }
     string actual;
     using(var stream=File.OpenRead(temp))using(var sha=SHA256.Create())
      actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
     if(!string.Equals(actual,digest.Substring(7),StringComparison.OrdinalIgnoreCase))
      throw new Exception("SHA-256 inválido. Atualização rejeitada.");
     File.Move(temp,installer);
    }finally{try{if(File.Exists(temp))File.Delete(temp);}catch{}}
   }
   return installer;
  }
  async Task CheckUpdates(bool manual=true){
   if(checkingUpdates){if(manual)MessageBox.Show("Ja existe uma verificacao em andamento.","Atualizacao");return;}
   checkingUpdates=true;
   try{
    status.Text="Consultando versão publicada...";
    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    const string updateRepo="https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases/download/";
    const string officialSetup="Armazem-Gratidao-Novo-Setup.exe";
    const string legacySetup="Armazem-Gratidao-Novo-Windows8-Setup.exe";
    const string patchZip="Armazem-Gratidao-Atualizacao.zip";
    const string prefix="amzg-novo-win8-build-";
    System.Collections.Generic.Dictionary<string,object> release=null;
    int remoteBuild=-1;
    string alternativeError="";
    // Primeiro consultar manifesto estatico (CDN raw). Evita depender da API que pode responder 504.
    try{
     status.Text="Consultando manifesto oficial de atualizacoes...";
     string manifestJson;
     using(var web=new WebClient()){
      web.Headers[HttpRequestHeader.UserAgent]="ArmazemGratidaoNovo-Windows";
      // O manifesto novo entrega ZIP e usa o atualizador independente.
       // O manifesto antigo permanece fixo no instalador UNICO para versões legadas.
       try{
        manifestJson=await web.DownloadStringTaskAsync(new Uri("https://raw.githubusercontent.com/giliotiesg-pixel/erp-gratidao-updates/main/atualizacao-novo-win8-pacote.json"));
       }catch(WebException){
        manifestJson=await web.DownloadStringTaskAsync(new Uri("https://raw.githubusercontent.com/giliotiesg-pixel/erp-gratidao-updates/main/atualizacao-novo-win8.json"));
       }
     }
     var manifest=new JavaScriptSerializer().DeserializeObject(manifestJson) as System.Collections.Generic.Dictionary<string,object>;
     if(manifest==null||!manifest.ContainsKey("build")||!manifest.ContainsKey("tag")||!manifest.ContainsKey("asset")||!manifest.ContainsKey("url")||!manifest.ContainsKey("sha256"))
      throw new Exception("Manifesto de atualizacao incompleto.");
     int version;
     if(!int.TryParse(Convert.ToString(manifest["build"]),out version)||version<1)throw new Exception("Versao invalida no manifesto.");
     string tag=Convert.ToString(manifest["tag"]);
     string assetName=Convert.ToString(manifest["asset"]);
     string manifestDownloadUrl=Convert.ToString(manifest["url"]);
     string sha256=Convert.ToString(manifest["sha256"]);
     if(tag!=prefix+version||(assetName!=officialSetup&&assetName!=legacySetup&&assetName!=patchZip)||manifestDownloadUrl!=updateRepo+tag+"/"+assetName||
       sha256==null||sha256.Length!=64||!sha256.All(ch=>Uri.IsHexDigit(ch)))
      throw new Exception("Manifesto de atualizacao nao passou na validacao.");
     var manifestAsset=new System.Collections.Generic.Dictionary<string,object>();
     manifestAsset["name"]=assetName;
     manifestAsset["digest"]="sha256:"+sha256;
     manifestAsset["browser_download_url"]=manifestDownloadUrl;
     release=new System.Collections.Generic.Dictionary<string,object>();
     release["assets"]=new object[]{manifestAsset};
     remoteBuild=version;
    }catch(WebException ex){
     alternativeError=ex.Message;
    }
    // Compatibilidade: se o manifesto nao estiver acessivel, usar API de Releases.
    if(release==null){
     status.Text="Consultando versoes no GitHub...";
     string json=null;
     for(int attempt=1;attempt<=3;attempt++){
      try{
       using(var web=new WebClient()){
        web.Headers[HttpRequestHeader.UserAgent]="ArmazemGratidaoNovo-Windows";
        web.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";
        json=await web.DownloadStringTaskAsync(new Uri("https://api.github.com/repos/giliotiesg-pixel/erp-gratidao-updates/releases?per_page=20"));
       }
       break;
      }catch(WebException ex){
       var response=ex.Response as HttpWebResponse;
       int code=response==null?0:(int)response.StatusCode;
       if(attempt==3||(code!=502&&code!=503&&code!=504&&code!=429))
        throw new Exception("Falharam a consulta alternativa e a API do GitHub ("+(code==0?ex.Message:code.ToString())+"). Consulte as versoes pelo navegador; o programa e seus dados permanecem intactos.",ex);
       status.Text="Servidor temporariamente indisponivel ("+code+"). Tentativa "+(attempt+1)+" de 3...";
       await Task.Delay(1500*attempt);
      }
     }
     var releases=new JavaScriptSerializer().DeserializeObject(json) as object[];
     if(releases==null)throw new Exception("Lista de versoes invalida.");
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
    }
    if(release==null){
     status.Text="Nenhuma versao Windows8 publicada.";
     if(manual)MessageBox.Show("Nao existe uma atualizacao Windows8 publicada.","Verificar atualizacao");
     return;
    }
    if(remoteBuild<=InstalledBuild){
     status.Text="Versao atualizada: build "+InstalledBuild+".";
     if(manual)MessageBox.Show("Versão instalada: "+InstalledBuild+Environment.NewLine+"Última build publicada: "+remoteBuild+Environment.NewLine+Environment.NewLine+"Nenhuma atualização nova foi publicada no GitHub. Alterações em teste não são instaladas automaticamente.","Verificar atualização");
     return;
    }
    status.Text="Nova versao publicada: build "+remoteBuild+".";
    var assets=release["assets"] as object[];if(assets==null)throw new Exception("Pacote de atualização não encontrado.");
    System.Collections.Generic.Dictionary<string,object> asset=null;
    foreach(object obj in assets){
     var item=obj as System.Collections.Generic.Dictionary<string,object>;
     if(item!=null&&string.Equals(Convert.ToString(item["name"]),patchZip,StringComparison.OrdinalIgnoreCase)){asset=item;break;}
     if(item!=null&&asset==null&&string.Equals(Convert.ToString(item["name"]),officialSetup,StringComparison.OrdinalIgnoreCase))asset=item;
     if(item!=null&&asset==null&&string.Equals(Convert.ToString(item["name"]),legacySetup,StringComparison.OrdinalIgnoreCase))asset=item;
    }
    if(asset==null)throw new Exception("O release não contém atualização compatível.");
    string digest=asset.ContainsKey("digest")?Convert.ToString(asset["digest"]):"";
    if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||digest.Length!=71)throw new Exception("Atualização bloqueada: SHA-256 não publicado.");
    string url=Convert.ToString(asset["browser_download_url"]);
    if(!url.StartsWith("https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases/download/",StringComparison.OrdinalIgnoreCase))throw new Exception("Endereço de atualização inválido.");
    // A nova versão é salva e validada automaticamente, mesmo sem autorização para instalar.
    bool isZip=string.Equals(Convert.ToString(asset["name"]),patchZip,StringComparison.OrdinalIgnoreCase);
    if(isZip&&!File.Exists(Path.Combine(Application.StartupPath,"AtualizadorGratidao.exe")))
     throw new Exception("O atualizador visual não está instalado. Utilize uma única vez o instalador completo.");
    string installer=await DownloadVerifiedUpdate(remoteBuild,url,digest,isZip);
    status.Text="Atualização build "+remoteBuild+" salva em Atualizações Salvas.";
    if(!manual&&lastPromptedBuild==remoteBuild)return;
    lastPromptedBuild=remoteBuild;
    if(MessageBox.Show("Atualização build "+remoteBuild+" já baixada e verificada."+Environment.NewLine+
     "Instalar agora? Você também pode instalar depois pelo botão Atualizações Salvas."+
     Environment.NewLine+"O programa será fechado somente ao iniciar o instalador.",
     "Atualização do Armazém Gratidão",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    // O backup vem ANTES de fechar qualquer janela. Se falhar, nada é encerrado.
    // Aguardar qualquer backup diario e copiar em segundo plano, evitando travar a interface.
    status.Text="Criando e conferindo o backup antes da atualização...";
    string backupPath;
    await backupGate.WaitAsync();
    try{
     backupPath=await Task.Run(()=>isZip?CreateVerifiedFullUpdateBackup():CreateVerifiedPreUpdateBackup());
    }finally{backupGate.Release();}
    status.Text="Backup confirmado: "+backupPath;
    // Encerrar janelas secundárias automaticamente, respeitando eventuais cancelamentos
    // de fechamento (por exemplo, uma venda em edição ainda não salva).
    foreach(Form window in Application.OpenForms.Cast<Form>().Where(x=>x!=this&&!x.IsDisposed).ToArray()){
     window.Close();
    }
    if(Application.OpenForms.Cast<Form>().Any(x=>x!=this&&!x.IsDisposed&&x.Visible)){
     MessageBox.Show(this,"Uma janela impediu o fechamento. Salve a operação em andamento e tente novamente.","Atualização adiada",MessageBoxButtons.OK,MessageBoxIcon.Warning);
     return;
    }
    if(isZip){
     string updater=Path.Combine(Application.StartupPath,"AtualizadorGratidao.exe");
     Func<string,string> quote=value=>"\""+value.Replace("\"","")+"\"";
     var start=new ProcessStartInfo(updater){
      Arguments="--apply-zip "+quote(installer)+" "+quote(digest.Substring(7))+" "+
        quote(Application.ExecutablePath)+" "+quote(backupPath)+" "+Process.GetCurrentProcess().Id,
      WorkingDirectory=Application.StartupPath,UseShellExecute=true,Verb="runas"
     };
     Process.Start(start);
    }else{
     // O instalador completo é reservado para a migração inicial do atualizador.
     Process.Start(new ProcessStartInfo(installer){UseShellExecute=true});
    }
    Close();
   }catch(Exception ex){status.Text="Nao foi possivel atualizar: "+ex.Message;if(manual)MessageBox.Show(ex.Message,"Atualizacao nao iniciada",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{checkingUpdates=false;}
  }
  void ShowHome(object sender,EventArgs e){
   grid.Visible=false;
   var old=Controls["homePanel"];if(old!=null){Controls.Remove(old);old.Dispose();}
   var home=new FlowLayoutPanel {Name="homePanel",Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12,10,12,10),BackColor=Color.FromArgb(246,249,253)};
   var title=new Label {Text="INÍCIO  •  ARMAZÉM GRATIDÃO",Width=920,Height=36,Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Color.FromArgb(180,35,50)};
   home.Controls.Add(title);
   var date=new Label {Text=DateTime.Now.ToString("dddd, dd/MM/yyyy  HH:mm"),Width=920,Height=25,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Color.FromArgb(160,20,40)};
   home.Controls.Add(date);
   var refresh=new Button {Text="Atualizar indicadores",Width=185,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(20,69,147),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(8)};
   refresh.FlatAppearance.BorderSize=0;
   refresh.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
   refresh.FlatAppearance.MouseDownBackColor=Color.FromArgb(13,53,128);
   RoundHomeControl(refresh,11);
   refresh.Click+=ShowHome;home.Controls.Add(refresh);
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
   // Indicadores menores, com contorno fechado pintado no proprio cartao.
   // Evita o recorte das bordas causado pela combinacao Region+FixedSingle.
   var indicators=new FlowLayoutPanel {
    Width=920,Height=83,WrapContents=false,FlowDirection=FlowDirection.LeftToRight,
    AutoScroll=true,Margin=new Padding(0,1,0,5),Padding=new Padding(0),
    BackColor=Color.FromArgb(246,249,253)
   };
   foreach(var metric in metrics){
    var panel=new Panel {Width=183,Height=70,BackColor=Color.White,Margin=new Padding(5,2,5,2)};
    panel.Paint+=(paintControl,paintArgs)=>{
     var rect=new Rectangle(1,1,panel.ClientSize.Width-3,panel.ClientSize.Height-3);
     int r=10,diameter=r*2;
     using(var outline=new System.Drawing.Drawing2D.GraphicsPath()){
      outline.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);
      outline.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
      outline.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
      outline.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);
      outline.CloseFigure();
      paintArgs.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
      using(var border=new Pen(Color.FromArgb(180,197,222),1.6f))paintArgs.Graphics.DrawPath(border,outline);
     }
    };
    panel.Controls.Add(new Label {Text=metric.Item1,Left=11,Top=7,Width=163,Height=20,
     ForeColor=Color.FromArgb(165,37,57),Font=new Font("Segoe UI",8,FontStyle.Bold),AutoEllipsis=true});
    panel.Controls.Add(new Label {Text=metric.Item2,Left=11,Top=29,Width=163,Height=34,
     ForeColor=Color.FromArgb(15,57,146),Font=new Font("Segoe UI",17,FontStyle.Bold),AutoEllipsis=true});
    indicators.Controls.Add(panel);
   }
   home.Controls.Add(indicators);
   home.Controls.Add(new Label {Text="RANKING  •  PRODUTOS MAIS VENDIDOS NO MÊS",Width=920,Height=29,ForeColor=Color.FromArgb(180,35,50),Font=new Font("Segoe UI",11,FontStyle.Bold)});
   var ranking=new Panel {Width=920,Height=355,BackColor=Color.White,Margin=new Padding(0,2,0,5),AutoScroll=false};
   RoundHomeControl(ranking,12);
   var rankingText=new Label {Dock=DockStyle.Fill,Padding=new Padding(14,12,12,8),ForeColor=Color.FromArgb(180,35,50),Font=new Font("Segoe UI",10,FontStyle.Bold),AutoSize=false,Text="Nenhuma venda encontrada neste mês."};
   var rankingGrid=new DataGridView {Dock=DockStyle.Fill,Visible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,ScrollBars=ScrollBars.Both,ColumnHeadersHeight=40,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing};
   rankingGrid.Font=new Font("Segoe UI",10.5f,FontStyle.Regular);rankingGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
   rankingGrid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
   rankingGrid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(226,238,251);
   rankingGrid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(180,35,50);
   rankingGrid.EnableHeadersVisualStyles=false;
   rankingGrid.DefaultCellStyle.BackColor=Color.White;
   rankingGrid.DefaultCellStyle.ForeColor=Color.FromArgb(20,40,75);
   rankingGrid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(230,239,250);
   rankingGrid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(180,35,50);
   rankingGrid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(247,250,255);
   rankingGrid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   rankingGrid.GridColor=Color.FromArgb(232,237,245);
   rankingGrid.RowTemplate.Height=39;
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="posicao",HeaderText="Nº",Width=49,SortMode=DataGridViewColumnSortMode.NotSortable});
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="produto",HeaderText="Produto",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=300,SortMode=DataGridViewColumnSortMode.NotSortable});
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="quantidade",HeaderText="Qtd.",Width=96,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle {Alignment=DataGridViewContentAlignment.MiddleRight,Format="N3"}});
   rankingGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="valor",HeaderText="Total vendido",Width=134,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle {Alignment=DataGridViewContentAlignment.MiddleRight,Format="C2"}});
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
       if(position>0){rankingText.Visible=false;rankingGrid.Visible=true;rankingGrid.ClearSelection();rankingGrid.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);}
      }
     }
    }}catch(Exception ex){rankingText.Text="Ranking indisponível: "+ex.Message;}
   }
   Action fitHome=()=>{
    if(home.IsDisposed)return;
    int w=Math.Max(560,home.ClientSize.Width-home.Padding.Horizontal-24);
    title.Width=w;date.Width=w;indicators.Width=w;
    foreach(Control c in indicators.Controls){
     c.Width=Math.Max(125,(w-16)/4-10);
     foreach(Control field in c.Controls)field.Width=Math.Max(105,c.Width-20);
    }
    foreach(Control c in home.Controls)
     if(c is Label && c!=title && c!=date)c.Width=w;
    ranking.Width=w;
    if(rankingGrid.Columns.Contains("produto"))rankingGrid.Columns["produto"].MinimumWidth=220;
   };
   home.SizeChanged+=(resizeSender,resizeArgs)=>fitHome();
   Controls.Add(home);home.BringToFront();header.BringToFront();sideMenu.BringToFront();status.BringToFront();
   fitHome();EnableWheelScrolling(home);
   status.Text="Início • Indicadores e ranking. Acesse as ferramentas em Módulos.";
  }

  // Pasta persistente de suporte: fora do diretório de instalação e sem copiar dados privados automaticamente.
  void OpenProgramFilesFolder(){
   try{
    string unified=Path.Combine(Application.StartupPath,"Arquivos");
    string folder=Directory.Exists(unified)?unified:Path.Combine(dataDir,"Arquivos do Programa");
    Directory.CreateDirectory(folder);
    string instructions=Path.Combine(folder,"LEIA-ME.txt");
    if(!File.Exists(instructions)){
     File.WriteAllText(instructions,
      "ARMAZÉM GRATIDÃO NOVO - ARQUIVOS DO PROGRAMA"+Environment.NewLine+
      "Esta pasta permanece no computador durante as atualizações."+Environment.NewLine+
      "Use-a para guardar documentos de manutenção, instruções e arquivos de suporte."+Environment.NewLine+
      "Atualizacoes: pacotes baixados e verificados SHA-256."+Environment.NewLine+
      "Backups: copia de seguranca antes de atualizar, exceto quando voce configurou outra pasta."+Environment.NewLine+
      "Logs: registros de falhas e execucao do atualizador."+Environment.NewLine+
      "Rollback: arquivos antigos para reparacao em caso de falha."+Environment.NewLine+
      "Codigo-fonte e compilacao: https://github.com/giliotiesg-pixel/erp-gratidao-updates"+Environment.NewLine+
      "Dados ativos permanecem na pasta AppData do usuario para preservar o historico."+Environment.NewLine+
      "NUNCA envie bancos de clientes, fiados ou vendas ao GitHub publico."+Environment.NewLine,
      System.Text.Encoding.UTF8);
    }
    Process.Start("explorer.exe", "\""+folder+"\"");
   }catch(Exception ex){
    MessageBox.Show(this,"Não foi possível abrir a pasta de arquivos: "+ex.Message,
     "Arquivos do Programa",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   }
  }
  // Calculadora nativa, independente do PDV e sem acesso ao banco.
  Form calculatorWindow;
  void ShowCalculator(){
   if(calculatorWindow!=null&&!calculatorWindow.IsDisposed){
    if(calculatorWindow.WindowState==FormWindowState.Minimized)calculatorWindow.WindowState=FormWindowState.Normal;
    calculatorWindow.BringToFront();calculatorWindow.Activate();return;
   }
   var calc=new Form{Text="Calculadora • Armazém Gratidão",Width=320,Height=420,
    MinimumSize=new Size(290,385),StartPosition=FormStartPosition.CenterScreen,
    BackColor=Color.FromArgb(242,247,255),FormBorderStyle=FormBorderStyle.FixedSingle,
    MaximizeBox=false,ShowInTaskbar=true,Opacity=0.97};
   calculatorWindow=calc;
   calc.FormClosed+=(sender,args)=>{if(calculatorWindow==calc)calculatorWindow=null;};
   var display=new TextBox{Name="embeddedCalculatorDisplay",Dock=DockStyle.Top,Height=55,ReadOnly=true,
    Text="0",TextAlign=HorizontalAlignment.Right,
    Font=new Font("Segoe UI",24,FontStyle.Bold),BackColor=Color.White,
    ForeColor=Color.FromArgb(20,69,147)};
   calc.Controls.Add(display);
   var keys=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=5,
    Padding=new Padding(10),BackColor=Color.FromArgb(242,247,255)};
   for(int i=0;i<4;i++)keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   for(int i=0;i<5;i++)keys.RowStyles.Add(new RowStyle(SizeType.Percent,20));
   decimal stored=0m;
   string operation=null;
   bool newNumber=true;
   string[] labels={"C","⌫","%","÷","7","8","9","×","4","5","6","−","1","2","3","+","±","0",",","="};
   for(int i=0;i<labels.Length;i++){
    string key=labels[i];
    var b=new Button{Text=key,Dock=DockStyle.Fill,Margin=new Padding(3),
     Font=new Font("Segoe UI",15,FontStyle.Bold),FlatStyle=FlatStyle.Flat,
     BackColor=(key=="=")?Color.FromArgb(20,69,147):Color.White,
     ForeColor=(key=="=")?Color.White:Color.FromArgb(20,69,147)};
    b.FlatAppearance.BorderSize=0;
    b.Paint+=(sender,args)=>RoundedVisuals.DrawOutline(args.Graphics,
     new Rectangle(1,1,b.ClientSize.Width-3,b.ClientSize.Height-3),9,
     Color.FromArgb(181,201,228),1.2f);
    b.Click+=(sender,args)=>{
     try{
      decimal current;
      if(!decimal.TryParse(display.Text,System.Globalization.NumberStyles.Number,
        System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),out current))current=0m;
      if(key.Length==1&&char.IsDigit(key[0])){
       display.Text=newNumber?key:(display.Text=="0"?key:display.Text+key);
       newNumber=false;
      }else if(key==","){
       if(newNumber){display.Text="0,";newNumber=false;}
       else if(!display.Text.Contains(","))display.Text+=",";
      }else if(key=="C"){stored=0m;operation=null;newNumber=true;display.Text="0";}
      else if(key=="⌫"){display.Text=display.Text.Length<=1?"0":display.Text.Substring(0,display.Text.Length-1);newNumber=false;}
      else if(key=="±"){display.Text=(-current).ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));}
      else if(key=="%"){display.Text=(current/100m).ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));newNumber=true;}
      else if(key=="="){
       if(operation!=null){
        decimal answer=current;
        if(operation=="+")answer=stored+current;
        else if(operation=="−")answer=stored-current;
        else if(operation=="×")answer=stored*current;
        else if(operation=="÷"){if(current==0m){MessageBox.Show(calc,"Não é possível dividir por zero.","Calculadora");return;}answer=stored/current;}
        display.Text=answer.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        operation=null;newNumber=true;
       }
      }else{stored=current;operation=key;newNumber=true;}
     }catch(OverflowException){MessageBox.Show(calc,"Número muito grande.","Calculadora");display.Text="0";stored=0m;operation=null;newNumber=true;}
    };
    keys.Controls.Add(b,i%4,i/4);
   }
   calc.Controls.Add(keys);keys.BringToFront();
   calc.Show();
  }
Panel BuildEmbeddedCalculator(){
   var panel=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(234,242,253),Padding=new Padding(9)};
   var display=new TextBox{Name="embeddedCalculatorDisplay",Dock=DockStyle.Top,Height=60,ReadOnly=true,BorderStyle=BorderStyle.None,
    Text="0",TextAlign=HorizontalAlignment.Right,
    Font=new Font("Segoe UI",23,FontStyle.Bold),BackColor=Color.White,
    ForeColor=Color.FromArgb(20,69,147)};
   var liveResult=new Label{Dock=DockStyle.Top,Height=43,TextAlign=ContentAlignment.MiddleRight,
    Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Color.FromArgb(180,35,50),
    BackColor=Color.FromArgb(234,242,253),Text="Resultado: 0"};

   display.TabStop=true;
   display.Cursor=Cursors.IBeam;
   display.ShortcutsEnabled=false;
   var keys=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=5,
    Padding=new Padding(3,12,3,4),BackColor=Color.FromArgb(234,242,253)};
   for(int i=0;i<4;i++)keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   for(int i=0;i<5;i++)keys.RowStyles.Add(new RowStyle(SizeType.Percent,20));
   decimal stored=0m;
   string operation=null;
   bool newNumber=true;
   Action updatePreview=()=>{
    decimal current;
    if(!decimal.TryParse(display.Text,System.Globalization.NumberStyles.Number,
       System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),out current))current=0m;
    if(operation!=null&&newNumber){liveResult.Text="Digite o próximo número";return;}
    if(operation==null){liveResult.Text="Resultado: "+current.ToString("N2",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));return;}
    if(operation=="÷"&&current==0m){liveResult.Text="Aguardando divisor";return;}
    try{
     decimal result=operation=="+"?checked(stored+current):
       operation=="−"?checked(stored-current):
       operation=="×"?checked(stored*current):
       operation=="÷"?stored/current:current;
     liveResult.Text="Resultado: "+result.ToString("N2",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    }catch(OverflowException){liveResult.Text="Valor muito grande";}
   };
   string[] labels={"C","⌫","%","÷","7","8","9","×","4","5","6","−","1","2","3","+","±","0",",","="};
   var calculatorButtons=new System.Collections.Generic.Dictionary<string,Button>();
   for(int i=0;i<labels.Length;i++){
    string key=labels[i];
    var b=new Button{Text=key,Dock=DockStyle.Fill,Margin=new Padding(3,4,3,4),
     Font=new Font("Segoe UI",12,FontStyle.Bold),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,
     BackColor=(key=="=")?Color.FromArgb(22,75,160):(key=="C"?Color.FromArgb(184,55,69):((key=="+"||key=="−"||key=="×"||key=="÷")?Color.FromArgb(207,226,251):Color.White)),
     ForeColor=(key=="="||key=="C")?Color.White:Color.FromArgb(20,69,147)};
    b.FlatAppearance.BorderSize=0;
    b.FlatAppearance.MouseOverBackColor=Color.FromArgb(174,209,250);
    RoundHomeControl(b,12);
    b.Paint+=(sender,args)=>RoundedVisuals.DrawOutline(args.Graphics,
     new Rectangle(1,1,b.ClientSize.Width-3,b.ClientSize.Height-3),12,
     Color.FromArgb(181,201,228),1.2f);
    b.Click+=(sender,args)=>{
     try{
      decimal current;
      if(!decimal.TryParse(display.Text,System.Globalization.NumberStyles.Number,
        System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),out current))current=0m;
      if(key.Length==1&&char.IsDigit(key[0])){
       display.Text=newNumber?key:(display.Text=="0"?key:display.Text+key);
       newNumber=false;
      }else if(key==","){
       if(newNumber){display.Text="0,";newNumber=false;}
       else if(!display.Text.Contains(","))display.Text+=",";
      }else if(key=="C"){stored=0m;operation=null;newNumber=true;display.Text="0";}
      else if(key=="⌫"){display.Text=display.Text.Length<=1?"0":display.Text.Substring(0,display.Text.Length-1);newNumber=false;}
      else if(key=="±"){display.Text=(-current).ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));}
      else if(key=="%"){display.Text=(current/100m).ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));newNumber=true;}
      else if(key=="="){
       if(operation!=null&&!newNumber){
        decimal answer=current;
        if(operation=="+")answer=stored+current;
        else if(operation=="−")answer=stored-current;
        else if(operation=="×")answer=stored*current;
        else if(operation=="÷"){if(current==0m){MessageBox.Show(panel.FindForm(),"Não é possível dividir por zero.","Calculadora");return;}answer=stored/current;}
        display.Text=answer.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        operation=null;newNumber=true;
       }
      }else{
       if(operation!=null&&!newNumber){
        if(operation=="+")stored=checked(stored+current);
        else if(operation=="−")stored=checked(stored-current);
        else if(operation=="×")stored=checked(stored*current);
        else if(operation=="÷"){
         if(current==0m){MessageBox.Show(panel.FindForm(),"Não é possível dividir por zero.","Calculadora");return;}
         stored=stored/current;
        }
        display.Text=stored.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
       }else if(operation==null)stored=current;
       operation=key;newNumber=true;
      }
     }catch(OverflowException){MessageBox.Show(panel.FindForm(),"Número muito grande.","Calculadora");display.Text="0";stored=0m;operation=null;newNumber=true;}
     updatePreview();
    };
    calculatorButtons[key]=b;
    b.Click+=(sender,args)=>display.Focus();
    keys.Controls.Add(b,i%4,i/4);
   }

   // Teclas do teclado numérico e comandos especiais são tratados por KeyDown.
   // Sinais do teclado alfanumérico usam KeyPress: respeita ABNT2 e outros layouts.
   Action<string> pressCalculatorKey=key=>{
    Button button;
    if(calculatorButtons.TryGetValue(key,out button))button.PerformClick();
   };
   KeyEventHandler calculatorKeyDown=(sender,args)=>{
    string key=null;
    if(args.KeyCode>=Keys.NumPad0&&args.KeyCode<=Keys.NumPad9)
     key=((int)(args.KeyCode-Keys.NumPad0)).ToString();
    else if(args.KeyCode==Keys.Add)key="+";
    else if(args.KeyCode==Keys.Subtract)key="−";
    else if(args.KeyCode==Keys.Multiply)key="×";
    else if(args.KeyCode==Keys.Divide)key="÷";
    else if(args.KeyCode==Keys.Decimal)key=",";
    else if(args.KeyCode==Keys.Enter||args.KeyCode==Keys.Return)key="=";
    else if(args.KeyCode==Keys.Back)key="⌫";
    else if(args.KeyCode==Keys.Escape||args.KeyCode==Keys.Delete)key="C";
    if(key!=null){
     args.Handled=true;args.SuppressKeyPress=true;
     pressCalculatorKey(key);
    }
   };
   KeyPressEventHandler calculatorKeyPress=(sender,args)=>{
    string key=null;
    char ch=args.KeyChar;
    if(ch>='0'&&ch<='9')key=ch.ToString();
    else if(ch=='+')key="+";
    else if(ch=='-')key="−";
    else if(ch=='*'||ch=='x'||ch=='X'||ch=='×')key="×";
    else if(ch=='/'||ch=='÷')key="÷";
    else if(ch==','||ch=='.')key=",";
    else if(ch=='='||ch=='\r')key="=";
    else if(ch=='%')key="%";
    if(key!=null){
     args.Handled=true;
     pressCalculatorKey(key);
    }
   };
   // Captura na janela principal mesmo se o foco estiver em outro botão do menu.
   // Só atua enquanto o menu principal e a calculadora estiverem visíveis.
   var mainForm=this;
   mainForm.KeyPreview=true;
   KeyEventHandler guardedKeyDown=(sender,args)=>{
    var home=mainForm.Controls["homePanel"];
    if(home==null||!home.Visible||!panel.Visible||!mainForm.ContainsFocus)return;
    calculatorKeyDown(sender,args);
   };
   KeyPressEventHandler guardedKeyPress=(sender,args)=>{
    var home=mainForm.Controls["homePanel"];
    if(home==null||!home.Visible||!panel.Visible||!mainForm.ContainsFocus)return;
    calculatorKeyPress(sender,args);
   };
   mainForm.KeyDown+=guardedKeyDown;
   mainForm.KeyPress+=guardedKeyPress;
   panel.Disposed+=(sender,args)=>{mainForm.KeyDown-=guardedKeyDown;mainForm.KeyPress-=guardedKeyPress;};
   // Grade explicita impede que visor, resultado e teclas se sobreponham.
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty,Padding=Padding.Empty};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   display.Dock=DockStyle.Fill;liveResult.Dock=DockStyle.Fill;
   layout.Controls.Add(display,0,0);layout.Controls.Add(liveResult,0,1);layout.Controls.Add(keys,0,2);
   panel.Controls.Add(layout);
   updatePreview();
   return panel;
  }
  // Tela inicial única de navegação; preserva os handlers originais dos módulos.
  // Seções visuais do menu; não altera a ação associada a cada botão.
  string ModuleSection(string name){
   if(new[]{"PDV  •  Nova venda","Vendas","Lançar vendas anotadas","Cascos retornáveis","Fiado","Clientes","WhatsApp"}.Contains(name))return "VENDAS E ATENDIMENTO";
   if(new[]{"Produtos","Estoque","Compras","Estoque pelo celular","Conferência rápida de estoque"}.Contains(name))return "PRODUTOS E ESTOQUE";
   if(new[]{"Caixa","Financeiro","Lucro","Taxas dos pagamentos"}.Contains(name))return "FINANCEIRO E CAIXA";
   if(new[]{"Relatórios","Consulta de preços","Início / Indicadores","Localizar vendas","Conferir valores","Itens"}.Contains(name))return "CONSULTAS E RELATÓRIOS";
   return "SISTEMA E MANUTENÇÃO";
  }
  // Menu nativo e compacto: linhas horizontais de largura total, sem colunas fixas.
  // TODAS as secoes possuem controle Mostrar/Ocultar, sem alterar o clique dos modulos.
  void GroupModuleButtons(FlowLayoutPanel panel,bool adjustHeight=false){
   var buttons=panel.Controls.OfType<Button>().ToList();
   panel.SuspendLayout();panel.Controls.Clear();
   panel.WrapContents=false;
   panel.FlowDirection=FlowDirection.TopDown;
   panel.Padding=new Padding(5,4,5,4);
   panel.AutoScroll=!adjustHeight;
   var order=new[]{"VENDAS E ATENDIMENTO","PRODUTOS E ESTOQUE","FINANCEIRO E CAIXA",
    "CONSULTAS E RELATÓRIOS","SISTEMA E MANUTENÇÃO"};
   var blocks=new System.Collections.Generic.List<Tuple<Panel,FlowLayoutPanel,Button>>();
   Action arrange=null;
   foreach(string section in order){
    var matches=buttons.Where(x=>ModuleSection(x.AccessibleDescription)==section).ToList();
    if(matches.Count==0)continue;
    var block=new Panel{Width=600,Height=50,Margin=new Padding(0,0,0,4),
     BackColor=Color.FromArgb(246,249,253)};
    var bar=new Panel{Dock=DockStyle.Top,Height=25,BackColor=Color.FromArgb(226,236,250)};
    var toggle=new Button{Text="Ocultar ▲",Dock=DockStyle.Right,Width=88,Height=23,
     Font=new Font("Segoe UI",8,FontStyle.Bold),FlatStyle=FlatStyle.Flat,
     BackColor=Color.FromArgb(226,236,250),ForeColor=Color.FromArgb(24,75,149),
     UseMnemonic=false,Cursor=Cursors.Hand};
    toggle.FlatAppearance.BorderSize=0;
    var heading=new Label{Dock=DockStyle.Fill,Text=section,TextAlign=ContentAlignment.MiddleLeft,
     Padding=new Padding(9,0,0,0),Font=new Font("Segoe UI",8.5f,FontStyle.Bold),
     ForeColor=Color.FromArgb(180,35,50),AutoEllipsis=true};
    bar.Controls.Add(heading);bar.Controls.Add(toggle);
    var row=new FlowLayoutPanel{Left=0,Top=25,Width=600,Height=28,AutoScroll=false,
     FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Margin=new Padding(0),
     Padding=new Padding(3,2,3,2),BackColor=Color.FromArgb(246,249,253)};
    foreach(var button in matches){
     button.Dock=DockStyle.None;
     button.Anchor=AnchorStyles.None;
     button.AutoEllipsis=true;
     button.Font=new Font("Segoe UI",7.8f,FontStyle.Bold);
     button.Width=Math.Max(54,Math.Min(210,TextRenderer.MeasureText(button.Text,button.Font).Width+15));
     button.Height=20;
     button.MinimumSize=Size.Empty;button.MaximumSize=Size.Empty;
     button.Margin=new Padding(2,1,2,1);
     button.Padding=new Padding(2,0,2,0);
     button.TextAlign=ContentAlignment.MiddleCenter;
     button.FlatAppearance.BorderSize=0;
     var help=new ToolTip();help.SetToolTip(button,button.Text);
     row.Controls.Add(button);
    }
    block.Controls.Add(row);block.Controls.Add(bar);
    blocks.Add(Tuple.Create(block,row,toggle));
    panel.Controls.Add(block);
    toggle.Click+=(sender,args)=>{
     row.Visible=!row.Visible;
     toggle.Text=row.Visible?"Ocultar ▲":"Mostrar ▼";
     if(arrange!=null)arrange();
    };
   }
   bool resizing=false;
   arrange=()=>{
    if(resizing||panel.IsDisposed)return;
    resizing=true;
    try{
     int width=Math.Max(230,panel.ClientSize.Width-panel.Padding.Horizontal-
      (adjustHeight?0:SystemInformation.VerticalScrollBarWidth)-5);
     int sum=panel.Padding.Vertical;
     foreach(var item in blocks){
      var block=item.Item1;
      var row=item.Item2;
      block.Width=width;row.Width=width;
      int usable=Math.Max(80,width-row.Padding.Horizontal-5);
      int line=0,lines=1;
      foreach(Button button in row.Controls.OfType<Button>()){
       int size=button.Width+button.Margin.Horizontal;
       if(line>0&&line+size>usable){lines++;line=0;}
       line+=size;
      }
      int bodyHeight=row.Visible?lines*22+row.Padding.Vertical+1:0;
      row.Height=bodyHeight;
      block.Height=25+bodyHeight;
      sum+=block.Height+block.Margin.Vertical;
     }
     if(adjustHeight&&panel.Height!=sum)panel.Height=sum;
    }finally{resizing=false;}
   };
   panel.SizeChanged+=(sender,args)=>{if(arrange!=null)arrange();};
   panel.ResumeLayout(true);
   arrange();
  }
  void ShowMainMenu(){
   grid.Visible=false;
   var old=Controls["homePanel"];if(old!=null){Controls.Remove(old);old.Dispose();}
   var page=new Panel{Name="homePanel",Dock=DockStyle.Fill,BackColor=Color.FromArgb(246,249,253)};
   var top=new Panel{Dock=DockStyle.Top,Height=76,BackColor=Color.White};
   top.Controls.Add(new Label{Text="MENU PRINCIPAL  •  ARMAZÉM GRATIDÃO",Dock=DockStyle.Top,Height=40,
    Padding=new Padding(14,8,0,0),Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Color.FromArgb(180,35,50)});
   top.Controls.Add(new Label{Text="Selecione um módulo. Os mais utilizados aparecem primeiro.",Dock=DockStyle.Bottom,Height=28,
    Padding=new Padding(14,0,0,0),Font=new Font("Segoe UI",9,FontStyle.Regular),ForeColor=Color.FromArgb(75,91,115)});
   var content=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,
    FlowDirection=FlowDirection.TopDown,Padding=new Padding(11,7,11,7),
    BackColor=Color.FromArgb(246,249,253)};
   var tiles=new FlowLayoutPanel{Width=1060,Height=260,AutoScroll=false,WrapContents=false,
    FlowDirection=FlowDirection.TopDown,Padding=new Padding(4,3,4,3),Margin=new Padding(0),
    BackColor=Color.FromArgb(246,249,253)};
   var actions=ModuleActions();


   actions.Insert(2,Tuple.Create("Arquivos do Programa",(EventHandler)((sender,args)=>OpenProgramFilesFolder())));
   actions.Insert(3,Tuple.Create("Atualizações Salvas",(EventHandler)((sender,args)=>OpenSavedUpdates())));
   actions.Add(Tuple.Create("Importar banco",(EventHandler)ImportDatabase));
   actions.Add(Tuple.Create("Pasta do backup das atualizações",(EventHandler)ConfigureUpdateBackupFolder));
   actions.Add(Tuple.Create("Localizar vendas",(EventHandler)CompareSales));
   actions.Add(Tuple.Create("Preparar recuperação",(EventHandler)PrepareRecoveredSales));
   actions.Add(Tuple.Create("Aplicar recuperação",(EventHandler)ApplyRecovery));
   actions.Add(Tuple.Create("Conferir valores",(EventHandler)CheckSaleDifferences));
   actions.Add(Tuple.Create("Integrar vendas 01 a 19/10",(EventHandler)IntegrateHistoricalSales));
   actions.Add(Tuple.Create("Itens",(EventHandler)((sender,args)=>ShowTable("sale_items","SELECT sale_id,name,quantity,unit_price,total FROM sale_items ORDER BY sale_id DESC LIMIT 5000"))));
   actions.Add(Tuple.Create("Conferência rápida de estoque",(EventHandler)((sender,args)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de conferir estoque.");return;}OpenModuleWindow(new StockCountQuickForm(dbPath));})));
   // Sistema e manutencao ficam na mesma faixa dos demais modulos, recolhidos inicialmente.
   foreach(var item in actions){
    string displayName=System.Globalization.CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(item.Item1.ToLower(System.Globalization.CultureInfo.GetCultureInfo("pt-BR")));
    displayName=displayName.Replace("Pdv","PDV").Replace("Whatsapp","WhatsApp").Replace("Qr","QR").Replace("Pix","PIX");
    var tile=new Button{Text=displayName,Tag="home-shortcut",AccessibleDescription=item.Item1,
     Width=100,Height=20,Margin=new Padding(2,1,2,1),
     Padding=new Padding(2,0,2,0),FlatStyle=FlatStyle.Flat,
     BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
     Font=new Font("Segoe UI",7.8f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,
     AutoEllipsis=true,UseMnemonic=false,Cursor=Cursors.Hand};
    tile.FlatAppearance.BorderSize=0;
    tile.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
    tile.Paint+=(sender,args)=>RoundedVisuals.DrawOutline(args.Graphics,
     new Rectangle(1,1,tile.ClientSize.Width-3,tile.ClientSize.Height-3),
     9,Color.FromArgb(92,117,155),1.3f);
    tile.Click+=item.Item2;
    tiles.Controls.Add(tile);
   }
   GroupModuleButtons(tiles,true);
   var calculatorSide=new Panel{Dock=DockStyle.None,Width=350,Height=410,
    BackColor=Color.FromArgb(242,247,255),Padding=new Padding(7),Margin=new Padding(0,9,0,10)};
   var calcTitle=new Label{Dock=DockStyle.Top,Height=36,Text="CALCULADORA",
    Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(180,35,50),
    TextAlign=ContentAlignment.MiddleCenter};
   var calcBody=BuildEmbeddedCalculator();
   calculatorSide.Controls.Add(calcBody);
   calculatorSide.Controls.Add(calcTitle);
   // A calculadora recebe foco ao abrir o menu e ao voltar a ele.
   // Não desloca o ponteiro do mouse nem captura teclas em outros módulos.
   Action focusCalculator=()=>{
    if(!IsDisposed&&Visible&&Controls["homePanel"]==page&&
       Form.ActiveForm==this&&calcBody.Visible){
     var visor=calcBody.Controls.Find("embeddedCalculatorDisplay",true).FirstOrDefault();
     if(visor!=null&&visor.CanFocus)visor.Focus();
    }
   };
   EventHandler onActivated=(sender,args)=>BeginInvoke((Action)(()=>focusCalculator()));
   Activated+=onActivated;
   page.Disposed+=(sender,args)=>Activated-=onActivated;
   page.VisibleChanged+=(sender,args)=>{if(page.Visible)BeginInvoke((Action)(()=>focusCalculator()));};
   content.Controls.Add(tiles);
   content.Controls.Add(calculatorSide);
   page.Controls.Add(content);page.Controls.Add(top);
   Action resizeMenu=()=>{
    int width=Math.Max(350,content.ClientSize.Width-content.Padding.Horizontal-18);
    if(tiles.Width!=width)tiles.Width=width;
    int calculatorWidth=Math.Min(350,width);
    if(calculatorSide.Width!=calculatorWidth)calculatorSide.Width=calculatorWidth;
    calculatorSide.Margin=new Padding(Math.Max(0,width-calculatorWidth),9,0,10);
   };
   content.Resize+=(sender,args)=>resizeMenu();
   resizeMenu();
   Controls.Add(page);page.BringToFront();header.BringToFront();status.BringToFront();
   sideMenu.Visible=false;EnableWheelScrolling(content);
   BeginInvoke((Action)(()=>focusCalculator()));
   status.Text="Menu Principal • Selecione um módulo. Início exibe apenas indicadores e ranking.";
  }
  // Janela independente de atalhos: os mais usados primeiro.
  // O menu lateral antigo continua acessível como alternativa de navegação.
  System.Collections.Generic.List<Tuple<string,EventHandler>> ModuleActions(){
   return new System.Collections.Generic.List<Tuple<string,EventHandler>>{
    Tuple.Create("PDV  •  Nova venda",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.");return;}OpenModuleWindow(new PdvForm(dbPath));})),
    Tuple.Create("Vendas",(EventHandler)((s,a)=>OpenConsult(false))),
    Tuple.Create("Cascos retornáveis",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de controlar cascos.");return;}OpenModuleWindow(new CascosForm(dbPath));})),
    Tuple.Create("Fiado",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new FiadoClientesForm(dbPath));})),
    Tuple.Create("Consulta de preços",(EventHandler)((sender,args)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco de produtos.");return;}OpenModuleWindow(new ConsultaPrecoForm(dbPath));})),
    Tuple.Create("Produtos",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new ProdutosForm(dbPath));})),
    Tuple.Create("Estoque",(EventHandler)((s,a)=>OpenHomeModule("Estoque"))),
    Tuple.Create("Clientes",(EventHandler)((s,a)=>OpenHomeModule("Clientes"))),
    Tuple.Create("Compras",(EventHandler)((s,a)=>OpenHomeModule("Compras"))),
    Tuple.Create("Caixa",(EventHandler)((s,a)=>OpenHomeModule("Caixa"))),
    Tuple.Create("Lucro",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new LucroForm(dbPath));})),
    Tuple.Create("Relatórios",(EventHandler)((s,a)=>OpenHomeModule("Relatorios"))),
    Tuple.Create("Backup do banco",(EventHandler)BackupDatabase),
    Tuple.Create("Verificar atualização",(EventHandler)(async (s,a)=>await CheckUpdates())),
    // Funções complementares vêm depois dos acessos mais frequentes.
    Tuple.Create("Lançar vendas anotadas",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}OpenModuleWindow(new PdvForm(dbPath,true));})),
    Tuple.Create("Estoque pelo celular",(EventHandler)((s,a)=>OpenMobileStock())),
    Tuple.Create("Financeiro",(EventHandler)((s,a)=>OpenHomeModule("Financeiro"))),
    Tuple.Create("Sistema",(EventHandler)((s,a)=>OpenHomeModule("Sistema"))),
    Tuple.Create("WhatsApp",(EventHandler)((s,a)=>OpenModuleWindow(new WhatsAppNormalForm()))),
    Tuple.Create("Taxas dos pagamentos",(EventHandler)((s,a)=>PaymentFees.ShowEditor(dbPath,this))),
    Tuple.Create("Verificar banco",(EventHandler)CheckDatabase)
   };
  }
  void ShowModulesWindow(){
   if(modulesWindow!=null&&!modulesWindow.IsDisposed){
    if(modulesWindow.WindowState==FormWindowState.Minimized)modulesWindow.WindowState=FormWindowState.Normal;
    modulesWindow.BringToFront();modulesWindow.Activate();return;
   }
   var dialog=new Form{Text="Armazém Gratidão • Módulos",Width=720,Height=490,
    MinimumSize=new Size(480,340),StartPosition=FormStartPosition.CenterParent,
    BackColor=Color.FromArgb(246,249,253),ShowInTaskbar=true,AutoScroll=false};
   modulesWindow=dialog;
   dialog.FormClosed+=(sender,args)=>{if(modulesWindow==dialog)modulesWindow=null;};
   var top=new Panel{Dock=DockStyle.Top,Height=72,BackColor=Color.White};
   var title=new Label{Text="MÓDULOS  •  ACESSO RÁPIDO",Left=14,Top=8,Width=665,Height=25,
    Font=new Font("Segoe UI",13,FontStyle.Bold),ForeColor=Color.FromArgb(15,57,146)};
   var info=new Label{Text="Acessos mais usados primeiro. Outras ferramentas continuam no menu antigo.",
    Left=14,Top=36,Width=660,Height=20,Font=new Font("Segoe UI",9,FontStyle.Regular),
    ForeColor=Color.FromArgb(75,91,115)};
   top.Controls.Add(title);top.Controls.Add(info);
   var tiles=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,
    FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(7),BackColor=Color.FromArgb(246,249,253)};
   foreach(var item in ModuleActions()){
    var tile=new Button{Text=item.Item1,Tag="home-shortcut",Width=116,Height=37,
     MinimumSize=new Size(116,37),MaximumSize=new Size(116,37),Margin=new Padding(2),
     Padding=new Padding(4,2,2,2),FlatStyle=FlatStyle.Flat,
     BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
     Font=new Font("Segoe UI",9.2f,FontStyle.Bold),
     Image=HomeShortcutImage(item.Item1),
     ImageAlign=ContentAlignment.MiddleLeft,TextAlign=ContentAlignment.MiddleCenter,
     TextImageRelation=TextImageRelation.ImageBeforeText,AutoEllipsis=true};
    tile.FlatAppearance.BorderSize=0;
    tile.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
    tile.Paint+=(paintSender,paintArgs)=>RoundedVisuals.DrawOutline(paintArgs.Graphics,
     new Rectangle(1,1,tile.ClientSize.Width-3,tile.ClientSize.Height-3),
     9,Color.FromArgb(92,117,155),1.3f);
    tile.Click+=item.Item2;
    tiles.Controls.Add(tile);
   }
   GroupModuleButtons(tiles);
   var bottom=new Panel{Dock=DockStyle.Bottom,Height=57,BackColor=Color.White};
   var previous=new Button{Text="Mostrar/ocultar menu antigo",Width=218,Height=35,Top=10,Left=17,
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
    Font=new Font("Segoe UI",9,FontStyle.Bold)};
   previous.FlatAppearance.BorderSize=0;RoundHomeControl(previous,9);
   previous.Click+=(sender,args)=>{sideMenu.Visible=!sideMenu.Visible;dialog.Close();};
   var dashboard=new Button{Text="Início / Indicadores",Width=179,Height=35,Top=10,Left=248,
    FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(22,75,160),
    Font=new Font("Segoe UI",9,FontStyle.Bold)};
   dashboard.FlatAppearance.BorderSize=0;RoundHomeControl(dashboard,9);
   dashboard.Click+=(sender,args)=>{ShowHome(sender,args);dialog.Close();};
   bottom.Controls.Add(previous);bottom.Controls.Add(dashboard);
   dialog.Controls.Add(tiles);dialog.Controls.Add(bottom);dialog.Controls.Add(top);
   EnableWheelScrolling(tiles);
   dialog.Show(this);
  }
  // Garante rolagem pela roda do mouse tambem quando o ponteiro
  // esta sobre atalhos, rotulos, indicadores ou itens do menu lateral.
  // Grids de dados conservam sua propria rolagem.
  static void EnableWheelScrolling(ScrollableControl viewport){
   Action<Control> bind=null;
   bind=control=>{
    if(control is DataGridView)return;
    control.MouseWheel+=(sender,args)=>{
     if(!viewport.Visible||!viewport.IsHandleCreated)return;
     int current=-viewport.AutoScrollPosition.Y;
     int max=Math.Max(0,viewport.VerticalScroll.Maximum-viewport.VerticalScroll.LargeChange+1);
     if(max<=0)return;
     int lines=SystemInformation.MouseWheelScrollLines;
     int step=lines<=0?60:Math.Max(42,lines*22);
     int next=Math.Max(0,Math.Min(max,current-(args.Delta>0?step:-step)));
     if(next==current)return;
     viewport.AutoScrollPosition=new Point(-viewport.AutoScrollPosition.X,next);
    };
    foreach(Control child in control.Controls)bind(child);
   };
   bind(viewport);
  }
  // Icones nativos desenhados com GDI+, sem dependencia de fontes especiais,
  // internet, WebView ou recursos externos. Exibidos apenas nos atalhos do Inicio.
  static Image HomeShortcutImage(string title){
   var image=new Bitmap(48,48);
   using(var g=Graphics.FromImage(image)){
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);
    Color blue=Color.FromArgb(14,58,151),red=Color.FromArgb(211,36,57);
    // Sem circulo ou caixa atras da figura: icones livres e fortes.
    using(var pen=new Pen(blue,4.0f))
    using(var accent=new Pen(red,4.0f))
    using(var fill=new SolidBrush(Color.FromArgb(205,225,253)))
    using(var redFill=new SolidBrush(red)){
     pen.StartCap=pen.EndCap=System.Drawing.Drawing2D.LineCap.Round;
     accent.StartCap=accent.EndCap=System.Drawing.Drawing2D.LineCap.Round;
     string key=title.ToLowerInvariant();
     if(key.Contains("pdv")||key.Contains("compras")||key.Contains("venda")||key.Contains("anotadas")||key.Contains("integrar")){
      // Carrinho de compras: a operacao de venda e identificada pelo carrinho.
      g.DrawLine(pen,5,11,10,11);g.DrawLine(pen,10,11,16,33);
      g.DrawLine(pen,16,33,39,33);g.DrawLine(pen,13,16,43,16);
      g.DrawLine(pen,43,16,38,29);g.DrawLine(pen,38,29,15,29);
      g.FillEllipse(redFill,17,36,7,7);g.FillEllipse(redFill,33,36,7,7);
     }else if(key.Contains("clientes")||key.Contains("fiado")){
      // Pessoas e ficha de cliente.
      g.DrawEllipse(pen,17,5,14,14);g.DrawArc(pen,9,20,30,23,190,160);
      if(key.Contains("fiado")){g.DrawLine(accent,35,9,44,9);g.DrawLine(accent,40,4,40,15);}
      else{g.DrawEllipse(accent,3,13,10,10);g.DrawArc(accent,1,23,13,15,200,140);}
     }else if(key.Contains("produtos")||key.Contains("estoque")){
      g.FillRectangle(fill,8,13,32,29);g.DrawRectangle(pen,8,13,32,29);
      g.DrawLine(pen,8,13,24,5);g.DrawLine(pen,40,13,24,5);
      g.DrawLine(pen,24,5,24,29);g.DrawLine(pen,8,13,24,22);g.DrawLine(pen,40,13,24,22);
      g.DrawLine(accent,18,30,30,30);
     }else if(key.Contains("cascos")){
      g.DrawLine(pen,19,4,29,4);g.DrawLine(pen,19,4,19,12);g.DrawLine(pen,29,4,29,12);
      g.DrawLine(pen,19,12,13,19);g.DrawLine(pen,29,12,35,19);
      g.DrawRectangle(pen,13,19,22,23);g.DrawLine(accent,17,30,31,30);
     }else if(key.Contains("caixa")||key.Contains("financeiro")||key.Contains("lucro")){
      g.DrawRectangle(pen,5,15,38,27);g.DrawLine(pen,5,22,43,22);
      g.DrawEllipse(accent,18,24,12,12);
      g.DrawLine(accent,10,9,23,4);g.DrawLine(accent,23,4,35,9);
     }else if(key.Contains("relat")){
      g.DrawLine(pen,7,41,42,41);g.DrawLine(pen,7,41,7,6);
      g.FillRectangle(fill,12,25,7,15);g.DrawRectangle(pen,12,25,7,15);
      g.FillRectangle(fill,23,17,7,23);g.DrawRectangle(pen,23,17,7,23);
      g.FillRectangle(redFill,34,8,7,32);
     }else if(key.Contains("backup")){
      g.DrawEllipse(pen,5,5,38,38);g.DrawArc(accent,11,10,25,25,205,275);
      g.DrawLine(accent,32,10,39,14);g.DrawLine(accent,32,10,33,19);
     }else if(key.Contains("atualiza")||key.Contains("sistema")){
      g.DrawArc(pen,7,7,34,34,40,265);g.DrawArc(accent,7,7,34,34,225,190);
      g.DrawLine(pen,9,7,11,18);g.DrawLine(pen,9,7,20,9);
     }else{
      g.DrawRectangle(pen,11,8,28,32);g.DrawLine(accent,17,18,33,18);
      g.DrawLine(pen,17,25,33,25);g.DrawLine(pen,17,32,29,32);
     }
    }
   }
   var compact=new Bitmap(image,new Size(26,26));
   image.Dispose();
   return compact;
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
  // Navegacao padronizada: a seta retorna ao menu sem encerrar o aplicativo.
  // O fechamento normal da janela preserva validacoes de alteracoes pendentes.
  void AddBackToMenu(Form form){
   if(form==null||form.IsDisposed||form.Controls.Find("amzgBackBar",true).Length>0)return;
   var backBar=new Panel{Name="amzgBackBar",Dock=DockStyle.Top,Height=37,
    BackColor=Color.FromArgb(238,244,253)};
   var back=new Button{Text="← Voltar ao Menu Principal",Left=8,Top=3,Width=193,Height=30,
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
    Font=new Font("Segoe UI",9,FontStyle.Bold)};
   back.FlatAppearance.BorderSize=0;
   RoundHomeControl(back,9);
   back.Click+=(sender,args)=>{
    form.Close();
    if(!form.IsDisposed&&form.Visible)return;
    ShowMainMenu();
    WindowState=FormWindowState.Normal;
    BringToFront();Activate();
   };
   if(form is ModuloConsultaForm&&form.Text.Contains("Estoque")){
    backBar.Height=32;
    back.Width=132;back.Height=26;back.Top=3;
    back.Text="← Menu Principal";
    back.Anchor=AnchorStyles.Top|AnchorStyles.Right;
    backBar.Resize+=(sender,args)=>{back.Left=Math.Max(8,backBar.ClientSize.Width-back.Width-10);};
   }
   backBar.Controls.Add(back);
   var stockArea=form.Controls.Find("stockNavigation",true).FirstOrDefault() as Panel;
   if(stockArea!=null){
    backBar.Dock=DockStyle.Fill;
    backBar.Margin=new Padding(0);
    stockArea.Controls.Add(backBar);
   }else{
    form.Controls.Add(backBar);
   }
   backBar.BringToFront();
  }
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
   AddBackToMenu(form);
   // Janelas independentes: minimizar o programa principal não oculta o módulo.
   form.Show();
  }
  void OpenHomeModule(string module){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   OpenModuleWindow(new ModuloConsultaForm(dbPath,module));
  }
  void AddSection(Control parent,string title){
   var heading=new Label{Text=title,Width=209,Height=22,TextAlign=ContentAlignment.BottomLeft,
    ForeColor=Color.FromArgb(26,79,154),Font=new Font("Segoe UI",8.2f,FontStyle.Bold),
    Padding=new Padding(7,0,0,3),Margin=new Padding(1,6,1,2)};
   parent.Controls.Add(heading);
  }
  void AddButton(Control parent,string title,EventHandler click){
   var b=new Button{Text=title,Width=209,Height=33,
    FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(20,69,147),
    TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(9,0,3,0),
    Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(1,1,1,1),
    UseMnemonic=false,AutoEllipsis=true};
   b.FlatAppearance.BorderSize=0;
   b.FlatAppearance.MouseOverBackColor=Color.FromArgb(213,231,253);
   b.FlatAppearance.MouseDownBackColor=Color.FromArgb(192,218,250);
   b.Paint+=(paintSender,paintArgs)=>RoundedVisuals.DrawOutline(paintArgs.Graphics,
    new Rectangle(1,1,b.ClientSize.Width-3,b.ClientSize.Height-3),7,
    Color.FromArgb(194,211,233),1.1f);
   RoundHomeControl(b,7);
   b.Click+=click;parent.Controls.Add(b);
  }
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
  void AutoRecoverSalesIfDue(){
   if(!File.Exists(dbPath))return;
   // Evita vasculhar vendas antigas e discos inteiros toda vez que o PDV abre.
   string marker=Path.Combine(dataDir,"ultima-varredura-recuperacao.txt");
   string today=DateTime.Now.ToString("yyyy-MM-dd");
   try{
    if(File.Exists(marker)&&File.ReadAllText(marker).Trim()==today)return;
   }catch(IOException){}catch(UnauthorizedAccessException){}
   AutoRecoverSales();
   try{File.WriteAllText(marker,today);}catch(IOException){}catch(UnauthorizedAccessException){}
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
    string report="Recuperação automática: "+bestMissing+" vendas ausentes preparadas em cópia de teste.";
    string prompt="Foram localizadas "+bestMissing+" vendas ausentes da versão 12.1.6.\n\nUma cópia recuperada foi criada automaticamente em:\n"+destination+"\n\nO banco atual e o estoque NÃO foram alterados. Confira a cópia antes de substituir dados.";
    if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)(()=>{
     if(IsDisposed)return;
     status.Text=report;
     MessageBox.Show(this,prompt,"Vendas recuperadas para conferência",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }));
   }catch(Exception ex){
    string report="Recuperação automática não concluída: "+ex.Message;
    if(!IsDisposed&&IsHandleCreated){
     try{BeginInvoke((Action)(()=>{if(!IsDisposed)status.Text=report;}));}
     catch(InvalidOperationException){} // janela encerrada durante a recuperação
    }
   }
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
