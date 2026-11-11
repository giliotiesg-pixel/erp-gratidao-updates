using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using QRCoder;
namespace ArmazemGratidao {
 static class Program {
  [STAThread] static void Main() {
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   // Personalizar ANTES da primeira pintura; eliminar o antigo atraso de 3,5 s
   // e a varredura permanente que ocupava CPU no Windows 8.
   using(var main=new MainForm()){
    VisualStartup.Prepare(main);
    Application.Run(main);
   }
  }
 }
 // Padrao global aplicado uma vez por janela, nunca depois de aparecer.
 static class VisualStartup {
  static readonly System.Collections.Generic.HashSet<Form> prepared=
   new System.Collections.Generic.HashSet<Form>();
  public static void Prepare(Form form){
   if(form==null||form.IsDisposed||!prepared.Add(form))return;
   form.FormClosed+=(sender,args)=>prepared.Remove(form);
   form.SuspendLayout();
   try{
    CompactFormDimensions.Prepare(form);
    WindowBoundsGuard.Prepare(form);
    PdvShortcutButtons.Prepare(form);
    CalculatorShortcut.Prepare(form);
    FiadoGlobalHeader.Prepare(form);
    RoundedGridTheme.Prepare(form);
    ModuleVisualTheme.Prepare(form);
    ButtonTextVisibility.Prepare(form);
    // Medir DEPOIS do tema final: negrito altera a largura renderizada.
    CompactModuleToolbars.Apply(form);
   }finally{form.ResumeLayout(true);}
  }
 }
 // Modulos e dialogos recebem o tema na primeira Load, antes do Paint.
 public class StyledDialogForm : Form {
  protected override void OnLoad(EventArgs e){
   VisualStartup.Prepare(this);
   base.OnLoad(e);
  }
 }
 // Dimensoes iniciais compactas; evita desperdiçar metade do monitor.
 static class CompactFormDimensions {
  static readonly System.Collections.Generic.HashSet<Form> done=
   new System.Collections.Generic.HashSet<Form>();
  public static void Prepare(Form form){
   if(form==null||form.IsDisposed||!done.Add(form))return;
   form.Disposed+=(sender,args)=>done.Remove(form);
   if(form is MainForm||form is PdvForm||form is StartMenuPopup||
      form is ModernCalculatorForm)return;
   var bounds=Screen.FromControl(form).WorkingArea;
   int width=Math.Min(form.Width,Math.Max(500,Math.Min(form is FiadoClientesForm?820:940,bounds.Width-28)));
   int height=Math.Min(form.Height,Math.Max(370,Math.Min(form is FiadoClientesForm?575:635,bounds.Height-38)));
   if(form.MinimumSize.Width>width||form.MinimumSize.Height>height)
    form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,width),
     Math.Min(form.MinimumSize.Height,height));
   if(form.Width!=width||form.Height!=height)form.Size=new Size(width,height);
  }
 }
 // Botao de Servicos do Fiado fica no alto como no menu Iniciar.
 static class FiadoGlobalHeader {
  public static void Prepare(Form form){
   if(!(form is FiadoClientesForm))return;
   var header=form.Controls.Find("amzgBackBar",true).OfType<Panel>()
    .FirstOrDefault(x=>x.Parent==form&&x.Dock==DockStyle.Top);
   var back=form.Controls.Find("amzgFiadoBackClients",true).OfType<Button>().FirstOrDefault();
   var toggle=form.Controls.Find("amzgFiadoToggleServices",true).OfType<Button>().FirstOrDefault();
   var local=form.Controls.Find("amzgFiadoLocalNav",true).OfType<Panel>().FirstOrDefault();
   if(header==null||back==null||toggle==null)return;
   if(local!=null){local.Controls.Remove(back);local.Controls.Remove(toggle);
    local.Visible=false;local.Height=0;}
   var mainBack=header.Controls.OfType<Button>().FirstOrDefault();
   if(mainBack!=null){mainBack.Text="← Menu Inicial";mainBack.Width=124;}
   back.Dock=DockStyle.Right;back.Width=149;back.Height=28;
   toggle.Dock=DockStyle.Right;toggle.Width=116;toggle.Height=28;
   back.Visible=false;toggle.Visible=false;
   header.Controls.Add(toggle);header.Controls.Add(back);
   toggle.BringToFront();back.BringToFront();
  }
 }
 // Botao comum em todas as janelas, sem cobrir campos ou menus.
 static class CalculatorShortcut {
  static readonly System.Collections.Generic.HashSet<Form> installed=new System.Collections.Generic.HashSet<Form>();
  static ModernCalculatorForm independent;
  public static void Prepare(Form form){
   if(form==null||form.IsDisposed||form is MainForm||form is ModernCalculatorForm||
     form is StartMenuPopup||!installed.Add(form))return;
   var bar=new Panel{Name="amzgCalculatorShortcutBar",Dock=DockStyle.Top,Height=33,
    BackColor=Color.FromArgb(230,240,252),Padding=new Padding(4,2,8,2)};
   var button=new Button{Name="amzgOpenCalculatorModule",Text="▦  Calculadora",
    Dock=DockStyle.Right,Width=126,TabStop=false,
    BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
    Font=new Font("Segoe UI",9f,FontStyle.Bold),FlatStyle=FlatStyle.Flat};
   button.FlatAppearance.BorderSize=0;
   button.Click+=(sender,args)=>Open();
   bar.Controls.Add(button);
   var shared=form.Controls.Find("amzgBackBar",true).OfType<Panel>()
    .FirstOrDefault(x=>x.Parent==form&&x.Dock==DockStyle.Top);
   if(shared!=null){
    bar.Dock=DockStyle.Right;bar.Width=109;bar.Height=29;button.Dock=DockStyle.Fill;
    shared.Controls.Add(bar);bar.BringToFront();
   }else{form.Controls.Add(bar);bar.BringToFront();}
   form.FormClosed+=(sender,args)=>installed.Remove(form);
  }
  static void Open(){
   var main=Application.OpenForms.OfType<MainForm>().FirstOrDefault(x=>!x.IsDisposed);
   if(main!=null){main.OpenCalculatorFromModule();return;}
   if(independent!=null&&!independent.IsDisposed){
    if(independent.WindowState==FormWindowState.Minimized)
      independent.WindowState=FormWindowState.Normal;
    independent.BringToFront();independent.Activate();return;
   }
   var calc=new ModernCalculatorForm();independent=calc;
   calc.FormClosed+=(sender,args)=>{if(ReferenceEquals(independent,calc))independent=null;};
   calc.Show();calc.Activate();
  }
 }
 // Reduz as barras de acoes em TODOS os modulos sem alterar nenhum handler.
 // Abre espaço para o DataGridView e mantem a pesquisa/acoes acessiveis
 // pela rolagem horizontal do toolbar no Windows 8.
 static class CompactModuleToolbars {
  static readonly System.Collections.Generic.HashSet<Form> done=
   new System.Collections.Generic.HashSet<Form>();
  static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent){
   foreach(Control child in parent.Controls){
    yield return child;
    if(child is DataGridView)continue;
    foreach(Control nested in Descendants(child))yield return nested;
   }
  }
  public static void Apply(Form form){
   if(form==null||form.IsDisposed||!done.Add(form))return;
   form.FormClosed+=(sender,args)=>done.Remove(form);
   if(form is MainForm||form is ModernCalculatorForm||form is StartMenuPopup)return;
   var toolTip=new ToolTip{AutoPopDelay=7000,InitialDelay=500};
   form.Disposed+=(sender,args)=>toolTip.Dispose();
   var flows=Descendants(form).OfType<FlowLayoutPanel>().ToArray();
   foreach(var flow in flows){
    if(flow.Dock!=DockStyle.Top && flow.Dock!=DockStyle.Bottom)continue;
    var buttons=flow.Controls.OfType<Button>().Where(btn=>btn.Dock==DockStyle.None).ToArray();
    if(buttons.Length==0)continue;
    bool hasEditor=flow.Controls.Cast<Control>().Any(c=>
     c is TextBox||c is ComboBox||c is NumericUpDown||
     c is DateTimePicker||c is DataGridView);
    foreach(var button in buttons){
     if(button.IsDisposed)continue;
     string label=button.Text??"";
     button.AutoSize=false;
     button.AutoEllipsis=true;
     button.UseMnemonic=false;
     if(button.Height>29)button.Height=29;
     var oldFont=button.Font;
     if(oldFont!=null&&oldFont.SizeInPoints>8.5f)
      button.Font=new Font("Segoe UI",8.5f,FontStyle.Bold);
     // Nunca transformar um botao em campo minusculo; conservar o texto
     // inteiro quando couber e mostrar Tooltip quando houver truncamento.
     int natural=TextRenderer.MeasureText(label,button.Font).Width+20;
     int desiredWidth=label.Trim().Length<=2?
      Math.Min(46,Math.Max(30,button.Width)):
      Math.Min(192,Math.Max(78,natural));
     // Um AutoSize antigo pode ter deixado Width pequeno antes de medirmos.
     // A largura deve acompanhar o nome, e nao somente encolher: evita
     // 'Cadastrar...', 'Editar clie...' e 'Importar 1...' no toolbar.
     if(button.Width!=desiredWidth)button.Width=desiredWidth;
     button.Margin=new Padding(3,2,3,2);
     if(label.Length>0)toolTip.SetToolTip(button,label);
    }
    if(flow.Dock==DockStyle.Top && flow.Height>49 && !hasEditor){
     flow.WrapContents=false;
     flow.AutoScroll=true;
     flow.Height=39;
     flow.Padding=new Padding(5,3,5,2);
    }else if(flow.Dock==DockStyle.Top && flow.Height>49 && hasEditor){
     // Acesso direto aos filtros: reduzir a faixa mas preservar o campo.
     flow.AutoScroll=true;
     flow.WrapContents=false;
     flow.Height=48;
     flow.Padding=new Padding(5,3,5,3);
    }else if(flow.Dock==DockStyle.Bottom && flow.Height>50 && !hasEditor){
     flow.AutoScroll=true;
     flow.WrapContents=false;
     flow.Height=40;
     flow.Padding=new Padding(5,2,5,2);
    }
   }
  }
 }
 // Mantém cabeçalho, botões de controle e rodapé acessíveis em monitores menores.
 static class WindowBoundsGuard {
  static readonly System.Collections.Generic.HashSet<Form> initialized=new System.Collections.Generic.HashSet<Form>();
  public static void Prepare(Form form){
   if(form==null||form.IsDisposed||form is StartMenuPopup||!initialized.Add(form))return;
   form.Disposed+=(sender,args)=>initialized.Remove(form);
   form.Resize+=(sender,args)=>{if(form.WindowState==FormWindowState.Normal)KeepVisible(form);};
   form.Shown+=(sender,args)=>KeepVisible(form);
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
  static readonly System.Collections.Generic.HashSet<Form> installed=
   new System.Collections.Generic.HashSet<Form>();
  public static void Prepare(Form form){
   if(form==null||form.IsDisposed||installed.Contains(form))return;
   bool module=form is ConsultaForm ||
    form is ProdutosForm || form is LucroForm || form is ModuloConsultaForm;
   if(!module)return;
   installed.Add(form);
   var shortcut=new Button {
    Text="PDV - Nova Venda",Width=129,Height=26,
    BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
    Font=new Font("Segoe UI",9,FontStyle.Bold),
    Margin=new Padding(4,2,4,2)
   };
   var strip=new FlowLayoutPanel {
    Dock=DockStyle.Top,Height=31,WrapContents=false,
    FlowDirection=FlowDirection.RightToLeft,
    BackColor=Color.FromArgb(229,242,255)
   };
   shortcut.Click+=(sender,args)=>{
    MainForm main=null;
    foreach(Form open in Application.OpenForms)if(open is MainForm){main=(MainForm)open;break;}
    if(main!=null)main.OpenPdvFromModule();
    else MessageBox.Show("A janela principal não está disponível.","PDV");
   };
   var stockNavigation=form.Controls.Find("amzgBackBar",true).FirstOrDefault() as Panel;
   if(form is ModuloConsultaForm&&form.Text.Contains("Estoque")&&stockNavigation!=null){
    shortcut.Width=122;shortcut.Height=24;shortcut.Top=3;
    shortcut.Anchor=AnchorStyles.Right|AnchorStyles.Top;
    stockNavigation.Controls.Add(shortcut);
    Action position=()=>{shortcut.Left=Math.Max(8,
     stockNavigation.ClientSize.Width-132-shortcut.Width-25);};
    stockNavigation.Resize+=(sender,args)=>position();
    position();
   }else{
    strip.Controls.Add(shortcut);
    var shared=form.Controls.Find("amzgBackBar",true).OfType<Panel>()
     .FirstOrDefault(x=>x.Parent==form&&x.Dock==DockStyle.Top);
    if(shared!=null){
     strip.Dock=DockStyle.Right;strip.Width=140;strip.Height=29;
     shortcut.Height=27;shortcut.Width=131;
     shared.Controls.Add(strip);strip.BringToFront();
    }else{form.Controls.Add(strip);strip.BringToFront();}
   }
   form.FormClosed+=(sender,args)=>installed.Remove(form);
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
  public static void Prepare(Form form){Watch(form);}
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
 // Identidade grafica oficial nas barras de titulo de todas as janelas,
 // inclusive dialogs e formularios abertos posteriormente.
 // Nao adiciona painel sobre botao nem tira area de trabalho do modulo.
 static class ModuleWindowBranding {
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);
  static readonly Icon icon=CreateIcon();
  static Icon CreateIcon(){
   try{
    using(var source=typeof(MainForm).Assembly.GetManifestResourceStream(
     "ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(source==null)return null;
     using(var original=Image.FromStream(source))
     using(var bitmap=new Bitmap(original,new Size(32,32))){
      IntPtr handle=bitmap.GetHicon();
      try{using(var temporary=Icon.FromHandle(handle))
       return (Icon)temporary.Clone();
      }finally{DestroyIcon(handle);}
     }
    }
   }catch(Exception){return null;}
  }
  public static void Apply(Form form){
   if(form==null||form.IsDisposed)return;
   if(icon!=null){form.ShowIcon=true;form.Icon=icon;}
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
   if(control==null||control.IsDisposed||!decorated.Add(control))return;
   control.Disposed+=(sender,args)=>decorated.Remove(control);
   var form=control as Form;
   if(form!=null){
    ModuleWindowBranding.Apply(form);
    // Os modulos ja modernizados usam seu proprio layout; nao impor outra
    // barra, outros tamanhos ou cores por cima do trabalho aprovado.
    bool customized=form is MainForm||form is ModernCalculatorForm||
     form is PdvForm||form is ConsultaForm||form is StartMenuPopup;
    if(!customized){
     form.BackColor=Color.FromArgb(245,249,254);
     form.ForeColor=Color.FromArgb(29,55,101);
    }
   }
   bool special=control.FindForm() is MainForm ||
    control.FindForm() is PdvForm ||
    control.FindForm() is ModernCalculatorForm ||
    control.FindForm() is ConsultaForm ||
    control.FindForm() is StartMenuPopup;
   if(!special){
    var button=control as Button;
    if(button!=null){
     // Apenas aparencia: nao altera clique, validacao, largura nem altura.
     bool danger=button.Text!=null&&(
      button.Text.IndexOf("Excluir",StringComparison.OrdinalIgnoreCase)>=0||
      button.Text.IndexOf("Apagar",StringComparison.OrdinalIgnoreCase)>=0||
      button.Text.IndexOf("Remover",StringComparison.OrdinalIgnoreCase)>=0||
      button.Text.IndexOf("Retirar",StringComparison.OrdinalIgnoreCase)>=0);
     bool goBack=button.Text!=null&&(
      button.Text.IndexOf("Voltar",StringComparison.OrdinalIgnoreCase)>=0);
     bool cancel=button.Text!=null&&(
      button.Text.IndexOf("Cancelar",StringComparison.OrdinalIgnoreCase)>=0);
     Color oldBackground=button.BackColor;
     bool defaultBackground=oldBackground.IsEmpty||oldBackground.IsSystemColor||
      oldBackground.ToArgb()==SystemColors.Control.ToArgb();
     if(danger||goBack){
      button.BackColor=Color.FromArgb(190,40,57);
      button.ForeColor=Color.White;
     }else if(cancel){
      button.BackColor=Color.FromArgb(232,240,250);
      button.ForeColor=Color.FromArgb(24,67,140);
     }else if(defaultBackground){
      button.BackColor=Color.FromArgb(22,75,160);
      button.ForeColor=Color.White;
     }
     button.UseVisualStyleBackColor=false;
     button.FlatStyle=FlatStyle.Flat;
     button.FlatAppearance.BorderSize=0;
     if(button.Font!=null && !button.Font.Bold)
      button.Font=new Font(button.Font,FontStyle.Bold);
     Action roundButton=()=>Round(button,button.Height<=34?7:10);
     button.Resize+=(sender,args)=>roundButton();
     roundButton();
     button.FlatAppearance.MouseOverBackColor=danger||goBack?
      Color.FromArgb(217,54,75):Color.FromArgb(37,105,195);
     button.FlatAppearance.MouseDownBackColor=danger||goBack?
      Color.FromArgb(152,34,48):Color.FromArgb(12,54,124);
    }else if(control is TextBox){
     var input=(TextBox)control;
     if(!input.Multiline&&input.BorderStyle!=BorderStyle.None)
      input.BorderStyle=BorderStyle.FixedSingle;
     input.BackColor=Color.White;
    }else if(control is ComboBox){
     var combo=(ComboBox)control;
     combo.FlatStyle=FlatStyle.Flat;
     combo.BackColor=Color.White;
     // Preservar a seta nativa; recorte arredondado cortaria o seletor.
    }else if(control is NumericUpDown){
     var number=(NumericUpDown)control;
     number.BackColor=Color.White;
     number.BorderStyle=BorderStyle.FixedSingle;
     // Nao arredondar o campo: preserva as duas setas de quantidade.
    }else if(control is DateTimePicker){
     // Preservar a seta do calendario e o seletor do Windows 8.
    }else if(control is GroupBox){
     var group=(GroupBox)control;group.ForeColor=blue;
    }else if(control is FlowLayoutPanel){
     var flow=(FlowLayoutPanel)control;
     // Faixas de filtros e botoes seguem o fundo claro de Vendas e PDV.
     // Evitar trocar cor de barra escura que possui labels brancas.
     bool whiteTitle=flow.Controls.OfType<Label>().Any(x=>x.ForeColor==Color.White);
     if(!whiteTitle && (flow.Dock==DockStyle.Top||flow.Dock==DockStyle.Bottom)
         && flow.BackColor!=Color.Transparent)
      flow.BackColor=Color.FromArgb(231,240,252);
    }else if(control is Panel && !(control is TableLayoutPanel)){
     var panel=(Panel)control;
     if(panel.BorderStyle==BorderStyle.FixedSingle){
      panel.BorderStyle=BorderStyle.None;
      panel.Paint+=(sender,args)=>RoundedVisuals.DrawOutline(args.Graphics,
       new Rectangle(1,1,panel.ClientSize.Width-3,panel.ClientSize.Height-3),
       11,outline,1.3f);
     }
    }
   }
   // Novos controles (inclusive dialogos tardios) recebem o mesmo tema.
   // Nada e acrescentado aos handlers, DataSource ou controles de vendas.
   control.ControlAdded+=(sender,args)=>DecorateTree(args.Control);
  }
  static void DecorateTree(Control root){
   if(root==null||root.IsDisposed)return;
   Apply(root);
   // O grid mantém seus próprios editores e é tratado separadamente.
   if(root is DataGridView)return;
   foreach(Control child in root.Controls)DecorateTree(child);
  }
  public static void Prepare(Form form){
   if(form==null||form.IsDisposed||!forms.Add(form))return;
   DecorateTree(form);
   form.FormClosed+=(sender,args)=>forms.Remove(form);
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
   // Não expandir botões além das faixas do layout: evita desalinhar o
   // menu e esconder botões vizinhos em computadores com tela pequena.
   if(button.Font!=null&&button.Text!=null&&button.Text.Length>4){
    float points=button.Font.SizeInPoints;
    float best=points;
    var available=Math.Max(30,button.Width-button.Padding.Horizontal-15-
     (button.Image==null?0:button.Image.Width+8));
    while(best>8.2f){
     using(var font=new Font(button.Font.FontFamily,best,button.Font.Style)){
      var measured=TextRenderer.MeasureText(button.Text,font,new Size(2000,140),
       TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
      if(measured.Width<=available)break;
     }
     best-=0.5f;
    }
    if(best<points-0.3f)
     button.Font=new Font(button.Font.FontFamily,Math.Max(8.2f,best),button.Font.Style);
   }
   button.TextAlign=ContentAlignment.MiddleCenter;
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
  public static void Prepare(Form form){Watch(form);}
 }
 public sealed class MainForm : StyledDialogForm {
  readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
  readonly string dbPath;
  MobileStockServer mobileStock;
  readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer {Interval=6*60*60*1000};
  readonly System.Windows.Forms.Timer dailyBackupTimer=new System.Windows.Forms.Timer {Interval=60*60*1000};
  bool dailyBackupRunning=false;
  readonly System.Threading.SemaphoreSlim backupGate=new System.Threading.SemaphoreSlim(1,1);
  bool checkingUpdates=false;
  Form modulesWindow;
  StartMenuPopup startMenuPopup;
  int lastPromptedBuild=-1;
  int InstalledBuild {
   get {
    // AssemblyVersion 1.0.0.591: 591 e REVISION, nao o campo Build (zero).
    // Compatibilidade com versões antigas que publicavam 1.0.582.0.
    var version=typeof(MainForm).Assembly.GetName().Version;
    return version.Revision>0?version.Revision:version.Build;
   }
  }
  string InstalledVersion {
   get {
    var v=typeof(MainForm).Assembly.GetName().Version;
    return v.Major+"."+v.Minor;
   }
  }
  readonly FlowLayoutPanel sideMenu=new FlowLayoutPanel {Dock=DockStyle.Left,Width=231,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(5,7,5,7),BackColor=Color.FromArgb(234,241,251)};
  readonly Panel header=new Panel {Dock=DockStyle.Top,Height=52,BackColor=Color.FromArgb(15,61,128)};
  readonly Button menuToggle=new Button {Name="amzgStartHeader",Text="▦  Menu Principal",Width=155,Height=40,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
  readonly DataGridView grid=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AllowUserToAddRows=false };
  readonly Label status=new Label { Dock=DockStyle.Bottom,Height=27,TextAlign=ContentAlignment.MiddleLeft };
  public MainForm() {
   dbPath=Path.Combine(dataDir,"armazem-gratidao.db");
   Text="Armazém Gratidão | Versão "+InstalledVersion+" (build "+InstalledBuild+")";Width=1280;Height=800;MinimumSize=new Size(950,600);Font=new Font("Segoe UI",10,FontStyle.Bold);BackColor=Color.FromArgb(246,249,253);
   var bar=sideMenu;
   grid.ScrollBars=ScrollBars.Both;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
   var working=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,working.Width),Math.Min(Height,working.Height));

   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(20,69,147);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);grid.ColumnHeadersHeight=38;grid.RowTemplate.Height=32;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(241,245,252);
   status.BackColor=Color.FromArgb(14,54,116);status.ForeColor=Color.White;status.Padding=new Padding(12,0,0,0);
   menuToggle.FlatAppearance.BorderSize=0;
   menuToggle.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
   menuToggle.FlatAppearance.MouseDownBackColor=Color.FromArgb(13,53,128);
   menuToggle.Text="⊞  Iniciar";
   menuToggle.Name="amzgStartHeader";
   menuToggle.BackColor=Color.FromArgb(200,34,54);
   menuToggle.Click+=(sender,args)=>ToggleStartMenu();
   RoundHomeControl(menuToggle,10);header.Controls.Add(menuToggle);
   // Atualização sempre acessível no cabeçalho, independentemente do módulo aberto.
   var updateHeader=new Button{Text="Verificar Atualização",Left=181,Top=13,Width=177,Height=40,
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(35,84,154),ForeColor=Color.White,
    Font=new Font("Segoe UI",9.5f,FontStyle.Bold)};
   updateHeader.FlatAppearance.BorderSize=0;
   updateHeader.Click+=async (sender,args)=>await CheckUpdates();
   RoundHomeControl(updateHeader,9);header.Controls.Add(updateHeader);
   var inicioHeader=new Button{Text="Início",Left=367,Top=13,Width=90,Height=40,
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,
    Font=new Font("Segoe UI",9.5f,FontStyle.Bold)};
   inicioHeader.FlatAppearance.BorderSize=0;
   inicioHeader.FlatAppearance.MouseOverBackColor=Color.FromArgb(36,96,185);
   inicioHeader.Click+=(sender,args)=>ShowMainMenu();
   RoundHomeControl(inicioHeader,9);header.Controls.Add(inicioHeader);
   // Calculadora em janela propria; nao ocupa mais a tela de Inicio.
   var calculatorHeader=new Button{Text="▦  Calculadora",Name="amzgOpenCalculator",
    Left=467,Top=13,Width=158,Height=40,Tag="home-shortcut",
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(174,41,66),ForeColor=Color.White,
    Font=new Font("Segoe UI",10f,FontStyle.Bold),Cursor=Cursors.Hand};
   calculatorHeader.FlatAppearance.BorderSize=0;
   calculatorHeader.FlatAppearance.MouseOverBackColor=Color.FromArgb(200,52,79);
   calculatorHeader.Click+=(sender,args)=>{CloseStartMenu();ShowCalculator();};
   RoundHomeControl(calculatorHeader,11);header.Controls.Add(calculatorHeader);
   // Identidade da loja sempre visivel ANTES do botao Iniciar.
   // Reutiliza o logotipo incorporado: nao ha download nem marca d'agua.
   var brandPicture=new PictureBox{Name="amzgHeaderLogo",
    Width=38,Height=38,Left=9,Top=7,
    SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent};
   try{
    using(var source=typeof(MainForm).Assembly.GetManifestResourceStream(
     "ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(source!=null)using(var img=Image.FromStream(source))
      brandPicture.Image=new Bitmap(img);
    }
   }catch(Exception){/* Nome permanece legivel mesmo se a imagem falhar. */}
   if(brandPicture.Image==null)brandPicture.Image=HomeShortcutImage("PDV");
   brandPicture.Disposed+=(sender,args)=>{if(brandPicture.Image!=null){
    brandPicture.Image.Dispose();brandPicture.Image=null;}};
   var brandCaption=new Label{Name="amzgHeaderBrand",Text="ARMAZÉM\nGRATIDÃO",
    Font=new Font("Segoe UI",10.4f,FontStyle.Bold),
    ForeColor=Color.White,BackColor=Color.Transparent,
    TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=false,
    Left=53,Top=5,Width=136,Height=42};
   header.Controls.Add(brandPicture);
   header.Controls.Add(brandCaption);
   // Cabecalho fino. Nao encostar nos indicadores nem desperdiçar altura.
   Action alignHeader=()=>{
    if(header.IsDisposed)return;
    const int gap=6;
    int x=brandCaption.Right+12;
    int[] baseWidths={88,142,62,119};
    int total=baseWidths.Sum()+3*gap;
    int available=Math.Max(305,header.ClientSize.Width-x-10);
    float factor=available>=total?1f:
     Math.Max(0.73f,(float)(available-3*gap)/(total-3*gap));
    Button[] items={menuToggle,updateHeader,inicioHeader,calculatorHeader};
    for(int i=0;i<items.Length;i++){
     var btn=items[i];
     btn.Left=x;btn.Top=9;btn.Height=33;
     btn.Width=Math.Max(52,(int)(baseWidths[i]*factor));
     btn.Font=new Font("Segoe UI",factor<0.9f?8f:8.5f,FontStyle.Bold);
     btn.AutoEllipsis=true;
     x+=btn.Width+gap;
    }
   };
   header.Resize+=(sender,args)=>alignHeader();
   alignHeader();
   brandCaption.BringToFront();brandPicture.BringToFront();

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
      int lastReported=-10;
      web.DownloadProgressChanged+=(sender,args)=>{
       if(IsDisposed||Disposing)return;
       int percent=args.ProgressPercentage;
       if(percent>=lastReported+5||percent==100){
        lastReported=percent;
        status.Text="Baixando pacote completo da build "+build+": "+percent+"%"+
         (args.TotalBytesToReceive>0?" de "+(args.TotalBytesToReceive/1024)+" KB":"");
       }
      };
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
  // Atualizador independente: executado de uma copia TEMPORARIA para que
  // o ZIP possa instalar/reparar AtualizadorGratidao.exe sem arquivo bloqueado.
  // A origem do EXE e o pacote ja validado por SHA-256 pela aplicacao.
  string PrepareVerifiedUpdater(string zipPath){
   string installed=Path.Combine(Application.StartupPath,"AtualizadorGratidao.exe");
   string staged=Path.Combine(Path.GetTempPath(),
     "AMZG-Atualizador-"+Guid.NewGuid().ToString("N")+".exe");
   try{
    using(var archive=ZipFile.OpenRead(zipPath)){
     var matches=archive.Entries.Where(e=>string.Equals(e.FullName,
       "AtualizadorGratidao.exe",StringComparison.OrdinalIgnoreCase)).ToList();
     if(matches.Count>1)throw new Exception("O pacote contém atualizadores duplicados.");
     if(matches.Count==1){
      var entry=matches[0];
      if(entry.Length<20000||entry.Length>16777216)
       throw new Exception("O executável do atualizador no ZIP possui tamanho inválido.");
      using(var input=entry.Open())
      using(var output=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
       byte[] buffer=new byte[32768];int n;long count=0;
       while((n=input.Read(buffer,0,buffer.Length))>0){
        count+=n;
        if(count>entry.Length)throw new Exception("Atualizador ZIP com tamanho inconsistente.");
        output.Write(buffer,0,n);
       }
       if(count!=entry.Length)throw new Exception("Atualizador ZIP incompleto.");
      }
      return staged;
     }
    }
    // Compatibilidade com ZIP legado (build 582), que nao tinha o atualizador:
    // usar uma copia temporaria do atualizador ja presente no programa.
    if(File.Exists(installed)){
     File.Copy(installed,staged,false);
     return staged;
    }
    throw new Exception("Este pacote antigo nao contém o atualizador e ele está ausente no computador. "+
      "Baixe somente o arquivo AtualizadorGratidao.exe na página de manutenção do GitHub "+
      "e copie para C:\\ERP-Gratidao. Não é necessário reinstalar o programa.");
   }catch{
    try{if(File.Exists(staged))File.Delete(staged);}catch{}
    throw;
   }
  }
  // Detectar o problema ANTES de baixar MBs ou gerar um backup demorado.
  // O atualizador nao pode substituir seu EXE se outra janela antiga o usa.
  internal static void EnsureInstalledUpdaterFree(string appDirectory){
   string updater=Path.Combine(appDirectory,"AtualizadorGratidao.exe");
   if(!File.Exists(updater))return; // ZIP novo recupera o auxiliar ausente.
   Exception locked=null;
   for(int attempt=0;attempt<5;attempt++){
    try{
     using(var handle=new FileStream(updater,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
     return;
    }catch(IOException ex){locked=ex;}
    catch(UnauthorizedAccessException ex){locked=ex;}
    System.Threading.Thread.Sleep(250);
   }
   throw new IOException(
    "O Atualizador Gratidao antigo ainda esta aberto ou bloqueado. "+
    "Feche todas as janelas do Atualizador Automatico antes de continuar. "+
    "Se nao houver janela, finalize somente o processo AtualizadorGratidao.exe "+
    "no Gerenciador de Tarefas. A instalacao nao foi iniciada e nenhum "+
    "arquivo foi substituido.",locked);
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
    string remoteDisplayVersion="";
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
     remoteDisplayVersion=manifest.ContainsKey("version")?Convert.ToString(manifest["version"]):"1.0";
     if(!System.Text.RegularExpressions.Regex.IsMatch(remoteDisplayVersion??"", @"^\d{1,2}\.\d{1,2}$"))
      throw new Exception("Número da versão publicado é inválido.");
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
       remoteBuild=build;release=candidate;remoteDisplayVersion="";
      }
     }
    }
    if(release==null){
     status.Text="Nenhuma versao Windows8 publicada.";
     if(manual)MessageBox.Show("Nao existe uma atualizacao Windows8 publicada.","Verificar atualizacao");
     return;
    }
    string remoteLabel=string.IsNullOrEmpty(remoteDisplayVersion)?"build "+remoteBuild:
     "versão "+remoteDisplayVersion+" (build "+remoteBuild+")";
    if(remoteBuild<=InstalledBuild){
     status.Text=InstalledBuild>remoteBuild
      ?"Versão "+InstalledVersion+" instalada; canal oficial na "+remoteLabel+"."
      :"Versão "+InstalledVersion+" atualizada (build "+InstalledBuild+").";
     if(manual)MessageBox.Show("Versão instalada: "+InstalledVersion+" (build "+InstalledBuild+")"+Environment.NewLine+
      "Local executado: "+Application.ExecutablePath+Environment.NewLine+
      "Última versão publicada no canal oficial: "+remoteLabel+Environment.NewLine+Environment.NewLine+
      (InstalledBuild>remoteBuild?"Esta é uma versão de teste mais recente que a oficial; não haverá atualização automática para uma versão anterior."
       :"Não existe atualização oficial mais recente."),
      "Verificar atualização");
     return;
    }
    status.Text="Nova versão publicada: "+remoteLabel+".";
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
    // Verificacao automatica consulta SOMENTE o manifesto. Nao baixar
    // pacotes grandes durante vendas ou ao abrir a tela inicial.
    if(!manual){
     if(lastPromptedBuild!=remoteBuild){
      lastPromptedBuild=remoteBuild;
      status.Text="Nova "+remoteLabel+" disponível. Clique em Verificar atualização para instalar.";
     }
     return;
    }
    if(MessageBox.Show(this,"Nova "+remoteLabel+" disponível."+Environment.NewLine+
       "O programa, o atualizador e as bibliotecas serão atualizados juntos."+
       Environment.NewLine+"Deseja baixar e instalar agora?"+
       Environment.NewLine+"Um backup completo será verificado antes de fechar o PDV.",
       "Atualização completa do Armazém Gratidão",
       MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    bool isZip=string.Equals(Convert.ToString(asset["name"]),patchZip,StringComparison.OrdinalIgnoreCase);
    if(isZip){
     status.Text="Conferindo se o atualizador antigo esta fechado...";
     await Task.Run(()=>EnsureInstalledUpdaterFree(Application.StartupPath));
    }
    // O download só começa após consentimento; o SHA-256 é verificado.
    status.Text="Preparando download da "+remoteLabel+"...";
    string installer=await DownloadVerifiedUpdate(remoteBuild,url,digest,isZip);
    status.Text="Download 100% verificado. Preparando backup obrigatório...";
    // O backup vem ANTES de fechar qualquer janela. Se falhar, nada é encerrado.
    // Aguardar qualquer backup diario e copiar em segundo plano, evitando travar a interface.
    status.Text="Criando e conferindo o backup antes da atualização...";
    string backupPath;
    await backupGate.WaitAsync();
    try{
     backupPath=await Task.Run(()=>isZip?CreateVerifiedFullUpdateBackup():CreateVerifiedPreUpdateBackup());
    }finally{backupGate.Release();}
    status.Text="Backup confirmado: "+backupPath;
    // O usuario pode ter reaberto uma janela antiga durante o backup.
    // Verificar NOVAMENTE antes de encerrar qualquer janela do PDV.
    if(isZip)await Task.Run(()=>EnsureInstalledUpdaterFree(Application.StartupPath));
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
     // A copia temporaria nao bloqueia o executavel da pasta de instalacao:
     // o proprio ZIP pode repor o atualizador que estava faltando.
     string updater=PrepareVerifiedUpdater(installer);
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
  // A tela inicial e unica: conserva o ranking, indicadores e todos os modulos.
  void ShowHome(object sender,EventArgs e){ShowMainMenu();}

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
  // Instancia unica: ao clicar novamente, traz a janela movel para frente.
  ModernCalculatorForm calculatorWindow;
  internal void OpenCalculatorFromModule(){ShowCalculator();}
  void ShowCalculator(){
   if(calculatorWindow!=null&&!calculatorWindow.IsDisposed){
    if(calculatorWindow.WindowState==FormWindowState.Minimized)
     calculatorWindow.WindowState=FormWindowState.Normal;
    calculatorWindow.BringToFront();calculatorWindow.Activate();return;
   }
   var calc=new ModernCalculatorForm();
   calculatorWindow=calc;
   calc.FormClosed+=(sender,args)=>{if(ReferenceEquals(calculatorWindow,calc))calculatorWindow=null;};
   calc.Show(this);calc.Activate();
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
      if(!block.Visible)continue;
      var row=item.Item2;
      block.Width=width;row.Width=width;
      int usable=Math.Max(80,width-row.Padding.Horizontal-5);
      int line=0,lines=1;
      foreach(Button button in row.Controls.OfType<Button>().Where(x=>x.Visible)){
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
   panel.Tag=arrange;
   panel.ResumeLayout(true);
   arrange();
  }
  // Botao Iniciar funciona de verdade como o do Windows:
  // janela flutuante que some ao clicar fora, nunca sidebar fixa.
  void ToggleStartMenu(){
   if(startMenuPopup!=null&&!startMenuPopup.IsDisposed){
    CloseStartMenu();
    status.Text="Menu Iniciar fechado.";
    return;
   }
   ShowStartMenuPopup();
  }

  void CloseStartMenu(){
   var popup=startMenuPopup;
   startMenuPopup=null;
   if(popup!=null&&!popup.IsDisposed)popup.Close();
  }

  void ShowStartMenuPopup(){
   if(IsDisposed||Disposing)return;
   var popup=new StartMenuPopup(AllStartActions(),ModuleSection,HomeShortcutImage,
    item=>{
     // O menu fecha ANTES de executar o modulo e volta a ficar disponivel
     // no botao Iniciar de qualquer tela do programa.
     if(!IsDisposed&&!Disposing)item.Item2(this,EventArgs.Empty);
    });
   popup.FormClosed+=(sender,args)=>{
    if(ReferenceEquals(startMenuPopup,popup))startMenuPopup=null;
   };
   startMenuPopup=popup;
   Rectangle working=Screen.FromControl(this).WorkingArea;
   Point start=menuToggle.PointToScreen(new Point(0,menuToggle.Height+5));
   int x=Math.Max(working.Left+4,Math.Min(start.X,working.Right-500));
    int y=Math.Max(working.Top+4,Math.Min(start.Y,working.Bottom-395));
    popup.Location=new Point(x,y);
    popup.Height=Math.Max(355,Math.Min(418,working.Bottom-y-8));
   popup.Show(this);
   popup.Activate();
   status.Text="Menu Iniciar aberto. Escolha uma secao para ver os modulos com icones.";
  }

  // Exatamente os handlers dos modulos atuais, sem recriar janelas de PDV
  // ou cadastro dentro do menu. Todos os recursos antigos permanecem.
  System.Collections.Generic.List<Tuple<string,EventHandler>> AllStartActions(){
   var list=ModuleActions();
   list.Insert(2,Tuple.Create("Arquivos do Programa",
    (EventHandler)((sender,args)=>OpenProgramFilesFolder())));
   list.Insert(3,Tuple.Create("Atualizações Salvas",
    (EventHandler)((sender,args)=>OpenSavedUpdates())));
   list.Add(Tuple.Create("Importar banco",(EventHandler)ImportDatabase));
   list.Add(Tuple.Create("Pasta do backup das atualizações",
    (EventHandler)ConfigureUpdateBackupFolder));
   list.Add(Tuple.Create("Localizar vendas",(EventHandler)CompareSales));
   list.Add(Tuple.Create("Preparar recuperação",(EventHandler)PrepareRecoveredSales));
   list.Add(Tuple.Create("Aplicar recuperação",(EventHandler)ApplyRecovery));
   list.Add(Tuple.Create("Conferir valores",(EventHandler)CheckSaleDifferences));
   list.Add(Tuple.Create("Integrar vendas 01 a 19/10",(EventHandler)IntegrateHistoricalSales));
   list.Add(Tuple.Create("Itens",(EventHandler)((sender,args)=>
    ShowTable("sale_items","SELECT sale_id,name,quantity,unit_price,total FROM sale_items ORDER BY sale_id DESC LIMIT 5000"))));
   list.Add(Tuple.Create("Conferência rápida de estoque",(EventHandler)((sender,args)=>{
    if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de conferir estoque.");return;}
    OpenModuleWindow(new StockCountQuickForm(dbPath));
   })));
   return list;
  }


  // Tela inicial consolidada: sem marca d'agua e sem duplicar ranking.
  // Os botoes continuam chamando os handlers existentes (nada e gravado aqui).
  void ShowMainMenu(){
   CloseStartMenu();
   grid.Visible=false;
   var old=Controls["homePanel"];
   if(old!=null){Controls.Remove(old);old.Dispose();}
   var page=new Panel{Name="homePanel",Dock=DockStyle.Fill,AutoScroll=true,
    BackColor=Color.FromArgb(245,249,254)};
   // Painel COMUM sem desenho em segundo plano.
   var canvas=new Panel{Name="amzgDashboardCanvas",Left=0,Top=0,
    Width=1015,Height=565,BackColor=Color.FromArgb(245,249,254)};
   page.Controls.Add(canvas);
   // A marca ja esta no cabecalho, sem duplicar nome nem descricao.
   // O botao Iniciar do cabecalho continua sempre presente.
   var metrics=new Panel{Name="amzgDashboardMetrics",Left=18,Top=10,
    Width=979,Height=75,BackColor=Color.Transparent};
   canvas.Controls.Add(metrics);
   string[] labels={"VENDAS DO DIA","TOTAL VENDIDO HOJE",
    "PRODUTOS CADASTRADOS","TOTAL VENDIDO NO MÊS"};
   var values=new Label[4];
   var metricCards=new Panel[4];
   for(int i=0;i<4;i++){
    Color accent=(i%2==0)?Color.FromArgb(23,93,185):Color.FromArgb(211,37,57);
    var card=new Panel{Name="amzgDashboardMetric"+i,Top=0,Width=230,
     Height=70,BackColor=Color.White};
    RoundHomeControl(card,12);
    var bar=new Panel{Dock=DockStyle.Left,Width=6,BackColor=accent};
    var caption=new Label{Text=labels[i],Left=13,Top=7,Width=200,
     Height=17,Font=new Font("Segoe UI",8.2f,FontStyle.Bold),
     ForeColor=Color.FromArgb(155,39,60),AutoEllipsis=true};
    var figure=new Label{Text="—",Left=13,Top=26,Width=200,Height=38,
     Font=new Font("Segoe UI",16f,FontStyle.Bold),
     ForeColor=Color.FromArgb(19,55,112),AutoEllipsis=true};
    card.Controls.Add(bar);card.Controls.Add(caption);card.Controls.Add(figure);
    metrics.Controls.Add(card);metricCards[i]=card;values[i]=figure;
   }
   var tiles=new Panel{Name="amzgDashboardTiles",Left=18,Top=98,
    Width=979,Height=94,BackColor=Color.Transparent};
   canvas.Controls.Add(tiles);
   var quick=new[]{
    Tuple.Create("PDV","PDV  •  Nova venda"),Tuple.Create("Vendas","Vendas"),
    Tuple.Create("Produtos","Produtos"),Tuple.Create("Estoque","Estoque"),
    Tuple.Create("Clientes","Clientes"),Tuple.Create("Fiado","Fiado"),
    Tuple.Create("Caixa","Caixa"),Tuple.Create("Relatórios","Relatórios"),
    Tuple.Create("Backup","Backup do banco"),
    Tuple.Create("Atualização","Verificar atualização")
   };
   var moduleActions=ModuleActions();
   var buttons=new System.Collections.Generic.List<Button>();
   for(int i=0;i<quick.Length;i++){
    var entry=quick[i];var action=moduleActions.FirstOrDefault(a=>a.Item1==entry.Item2);
    if(action==null)continue;
    var btn=new Button{Name="amzgDashboardTile"+i,Tag=entry.Item2,Text=entry.Item1,
     FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(20,81,170),
     ForeColor=Color.White,Font=new Font("Segoe UI",9.2f,FontStyle.Bold),
     Image=HomeWhiteTileIcon(entry.Item1),ImageAlign=ContentAlignment.MiddleLeft,
     TextImageRelation=TextImageRelation.ImageBeforeText,
     TextAlign=ContentAlignment.MiddleCenter,Cursor=Cursors.Hand,UseMnemonic=false,
     AutoEllipsis=true,Padding=new Padding(5,0,3,0),Width=177,Height=42};
    btn.FlatAppearance.BorderSize=0;
    btn.FlatAppearance.MouseOverBackColor=Color.FromArgb(35,103,202);
    RoundHomeControl(btn,11);
    btn.Disposed+=(sender,args)=>{if(btn.Image!=null){btn.Image.Dispose();btn.Image=null;}};
    btn.Click+=(sender,args)=>{CloseStartMenu();action.Item2(this,EventArgs.Empty);};
    tiles.Controls.Add(btn);buttons.Add(btn);
   }
   // Ranking em duas tabelas compactas: 1-10 e 11-20.
   var ranking=new Panel{Name="amzgDashboardRankingPanel",Left=18,Top=207,
    Width=979,Height=320,BackColor=Color.White};
   RoundHomeControl(ranking,12);
   var rankHeader=new Panel{Name="amzgDashboardRankingHeader",
    Left=0,Top=0,Width=979,Height=38,
    BackColor=Color.FromArgb(19,73,157)};
   var rankTitle=new Label{Name="amzgDashboardRankingTitle",Dock=DockStyle.Fill,
    Text="  RANKING  •  PRODUTOS MAIS VENDIDOS NO MÊS",
    TextAlign=ContentAlignment.MiddleLeft,
    Font=new Font("Segoe UI",10f,FontStyle.Bold),ForeColor=Color.White,
    AutoEllipsis=true};
   var refresh=new Button{Name="amzgDashboardRefresh",Dock=DockStyle.Right,
    Width=153,Text="↻  Atualizar",FlatStyle=FlatStyle.Flat,
    BackColor=Color.FromArgb(40,107,199),ForeColor=Color.White,
    Font=new Font("Segoe UI",9f,FontStyle.Bold),Cursor=Cursors.Hand};
   refresh.FlatAppearance.BorderSize=0;
   refresh.FlatAppearance.MouseOverBackColor=Color.FromArgb(58,130,222);
   rankHeader.Controls.Add(rankTitle);rankHeader.Controls.Add(refresh);
   ranking.Controls.Add(rankHeader);
   var rankArea=new Panel{Name="amzgRankingTables",
    Left=0,Top=38,Width=979,Height=282,
    Padding=new Padding(9,5,9,7),BackColor=Color.White};
   var leftGrid=CreateHomeRankingGrid("amzgRankingLeft");
   var rightGrid=CreateHomeRankingGrid("amzgRankingRight");
   leftGrid.Top=5;rightGrid.Top=5;
   rankArea.Controls.Add(leftGrid);rankArea.Controls.Add(rightGrid);
   ranking.Controls.Add(rankArea);
   canvas.Controls.Add(ranking);

   Action fit=()=>{
    if(page.IsDisposed||canvas.IsDisposed)return;
    int width=Math.Max(944,page.ClientSize.Width-2);
    canvas.Width=width;
    metrics.Width=width-36;
    const int mGap=11;
    int cardWidth=(metrics.Width-3*mGap)/4;
    for(int i=0;i<4;i++){
     metricCards[i].Left=i*(cardWidth+mGap);
     metricCards[i].Width=cardWidth;
     foreach(Control c in metricCards[i].Controls)
      if(c is Label)c.Width=cardWidth-23;
    }
    tiles.Width=width-36;
    const int tGap=11;
    int buttonWidth=(tiles.Width-4*tGap)/5;
    for(int i=0;i<buttons.Count;i++){
     buttons[i].Left=(i%5)*(buttonWidth+tGap);
     buttons[i].Top=(i/5)*48;
     buttons[i].Width=buttonWidth;
    }
    ranking.Width=width-36;
    rankHeader.Width=ranking.ClientSize.Width;
    rankArea.Width=ranking.ClientSize.Width;
    rankArea.Height=ranking.ClientSize.Height-38;
    int tableWidth=Math.Max(320,(rankArea.ClientSize.Width-rankArea.Padding.Horizontal-12)/2);
    leftGrid.Left=rankArea.Padding.Left;
    rightGrid.Left=rankArea.Padding.Left+tableWidth+12;
    leftGrid.Width=tableWidth;rightGrid.Width=tableWidth;
    int h=Math.Max(210,rankArea.ClientSize.Height-rankArea.Padding.Vertical);
    leftGrid.Height=h;rightGrid.Height=h;
    page.AutoScrollMinSize=new Size(width,566);
   };
   page.Resize+=(sender,args)=>fit();
   rankArea.Resize+=(sender,args)=>fit();
   // CORRECAO: Dock.Fill no topo do formulario permitia a barra
   // cobrir os valores dos indicadores. Reservar uma area propria fixa
   // ABAIXO do cabecalho, antes de criar qualquer layout de dashboard.
   page.Dock=DockStyle.None;
   Action positionPage=()=>{
    if(page.IsDisposed)return;
    page.Bounds=new Rectangle(0,header.Height,ClientSize.Width,
     Math.Max(100,ClientSize.Height-header.Height-status.Height));
   };
   EventHandler positionOnResize=(sender,args)=>positionPage();
   ClientSizeChanged+=positionOnResize;
   page.Disposed+=(sender,args)=>ClientSizeChanged-=positionOnResize;
   Controls.Add(page);
   page.BringToFront();header.BringToFront();status.BringToFront();
   sideMenu.Visible=false;positionPage();fit();

   // Dados verdadeiros, consultados somente para leitura e fora da interface.
   bool loading=false;
   Action reload=async ()=>{
    if(loading||page.IsDisposed)return;
    loading=true;refresh.Enabled=false;
    try{
     DashboardSnapshot snapshot=await Task.Run(()=>ReadHomeDashboardSummary());
     if(page.IsDisposed)return;
     for(int i=0;i<4;i++)values[i].Text=snapshot.Indicators[i];
     leftGrid.Rows.Clear();rightGrid.Rows.Clear();
     var culture=System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
     for(int i=0;i<snapshot.Ranking.Count&&i<20;i++){
      var p=snapshot.Ranking[i];
      decimal qty=p.Item2;
      string count=qty==decimal.Truncate(qty)?
       qty.ToString("N0",culture):qty.ToString("N3",culture);
      var table=i<10?leftGrid:rightGrid;
      table.Rows.Add((i+1).ToString("00"),p.Item1,count);
     }
     leftGrid.ClearSelection();rightGrid.ClearSelection();
    }catch(Exception ex){
     if(!page.IsDisposed){
      foreach(var label in values)label.Text="Indisponível";
      status.Text="Indicadores indisponíveis: "+ex.Message;
     }
    }finally{
     loading=false;
     if(!page.IsDisposed)refresh.Enabled=true;
    }
   };
   refresh.Click+=(sender,args)=>reload();
   if(page.IsHandleCreated)reload();
   else{
    EventHandler onCreated=null;
    onCreated=(sender,args)=>{page.HandleCreated-=onCreated;reload();};
    page.HandleCreated+=onCreated;
   }
   status.Text="Início • Ranking de 20 produtos, indicadores e atalhos. Calculadora somente pelo botão.";
  }

  // Duas tabelas lado a lado sem ocupar toda a altura da tela.
  static DataGridView CreateHomeRankingGrid(string name){
   var grid=new DataGridView{Name=name,ReadOnly=true,
    AllowUserToAddRows=false,AllowUserToDeleteRows=false,
    AllowUserToResizeRows=false,RowHeadersVisible=false,
    MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
    AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,
    ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,
    BackgroundColor=Color.White,GridColor=Color.FromArgb(218,229,243),
    ColumnHeadersHeight=27,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
    EnableHeadersVisualStyles=false,Font=new Font("Segoe UI",9f),
    Height=247,Width=435,RowTemplate={Height=22}};
   grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(225,234,247);
   grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(25,52,99);
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9f,FontStyle.Bold);
   grid.DefaultCellStyle.ForeColor=Color.FromArgb(30,55,92);
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(224,238,254);
   grid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(25,52,99);
   grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(246,249,254);
   grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="Nº",Width=41,
    SortMode=DataGridViewColumnSortMode.NotSortable});
   grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="Produto",
    AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=190,
    SortMode=DataGridViewColumnSortMode.NotSortable});
   grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="Qtd. total",Width=83,
    DefaultCellStyle=new DataGridViewCellStyle{Alignment=DataGridViewContentAlignment.MiddleRight},
    SortMode=DataGridViewColumnSortMode.NotSortable});
   grid.CellFormatting+=(sender,args)=>{
    if(args.RowIndex>=0&&args.ColumnIndex==1&&args.Value!=null)
     grid.Rows[args.RowIndex].Cells[args.ColumnIndex].ToolTipText=Convert.ToString(args.Value);
   };
   return grid;
  }

  sealed class DashboardSnapshot {
   public string[] Indicators;
   public System.Collections.Generic.List<Tuple<string,decimal>> Ranking=
    new System.Collections.Generic.List<Tuple<string,decimal>>();
  }

  // Sem dados falsos: retorna ate 20 produtos vendidos no mes com Qtd. total.
  DashboardSnapshot ReadHomeDashboardSummary(){
   var result=new DashboardSnapshot{Indicators=new[]{"—","—","—","—"}};
   if(!File.Exists(dbPath))return result;
   using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder{
    DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
    conn.Open();
    string day=DateTime.Today.ToString("yyyy-MM-dd");
    string month=DateTime.Today.ToString("yyyy-MM");
    long count=0,products=0;
    decimal today=0m,monthTotal=0m;
    using(var cmd=new SQLiteCommand(
     "SELECT COUNT(*),COALESCE(SUM(gross_total),0) FROM sales WHERE substr(created_at,1,10)=@day AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",conn)){
     cmd.Parameters.AddWithValue("@day",day);
     using(var reader=cmd.ExecuteReader()){
      if(reader.Read()){count=Convert.ToInt64(reader.GetValue(0));today=Convert.ToDecimal(reader.GetValue(1));}
     }
    }
    using(var cmd=new SQLiteCommand(
     "SELECT COUNT(*) FROM products",conn))products=Convert.ToInt64(cmd.ExecuteScalar());
    using(var cmd=new SQLiteCommand(
     "SELECT COALESCE(SUM(gross_total),0) FROM sales WHERE substr(created_at,1,7)=@month AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",conn)){
     cmd.Parameters.AddWithValue("@month",month);
     monthTotal=Convert.ToDecimal(cmd.ExecuteScalar());
    }
    using(var cmd=new SQLiteCommand(
     "SELECT si.name,COALESCE(SUM(si.quantity),0) AS qtd FROM sale_items si JOIN sales s ON s.id=si.sale_id WHERE substr(s.created_at,1,7)=@month AND UPPER(COALESCE(s.status,'')) NOT LIKE '%EXCLUID%' GROUP BY si.product_id,si.name ORDER BY qtd DESC,SUM(si.total) DESC LIMIT 20",conn)){
     cmd.Parameters.AddWithValue("@month",month);
     using(var reader=cmd.ExecuteReader()){
      while(reader.Read()){
       result.Ranking.Add(Tuple.Create(
        Convert.ToString(reader.GetValue(0)),Convert.ToDecimal(reader.GetValue(1))));
      }
     }
    }
    var br=System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
    result.Indicators=new[]{count.ToString("N0",br),
     "R$ "+today.ToString("N2",br),products.ToString("N0",br),
     "R$ "+monthTotal.ToString("N2",br)};
   }
   return result;
  }

  // Icones brancos sobre o azul, sem carregar arquivos nem rede.
  static Image HomeWhiteTileIcon(string name){
   using(var original=HomeShortcutImage(name)){
    var output=new Bitmap(original);
    for(int y=0;y<output.Height;y++)
     for(int x=0;x<output.Width;x++){
      Color pixel=output.GetPixel(x,y);
      if(pixel.A>0)output.SetPixel(x,y,Color.FromArgb(pixel.A,255,255,255));
     }
    return output;
   }
  }
  static string NormalizeMenuSearch(string value){
   if(string.IsNullOrEmpty(value))return "";
   var decomposed=value.Normalize(System.Text.NormalizationForm.FormD);
   var characters=decomposed.Where(ch=>
    System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)!=
    System.Globalization.UnicodeCategory.NonSpacingMark);
   return new string(characters.ToArray()).ToUpperInvariant();
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
   var dialog=new StyledDialogForm{Text="Armazém Gratidão • Módulos",Width=720,Height=490,
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
    Font=new Font("Segoe UI",8.3f,FontStyle.Bold)};
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
    using(var dialog=new StyledDialogForm{Text="Estoque pelo celular - QR Code",Width=760,Height=510,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
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
   var backBar=new Panel{Name="amzgBackBar",Dock=DockStyle.Top,Height=33,
    BackColor=Color.FromArgb(238,244,253)};
   var back=new Button{Text="← Voltar ao Menu Principal",Left=7,Top=3,Width=163,Height=26,
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
    backBar.Height=30;
    back.Width=126;back.Height=24;back.Top=3;
    back.Text="← Menu Principal";
    back.Anchor=AnchorStyles.Top|AnchorStyles.Right;
    backBar.Resize+=(sender,args)=>{back.Left=Math.Max(8,backBar.ClientSize.Width-back.Width-10);};
   }
   backBar.Controls.Add(back);
   var salesBackSlot=form.Controls.Find("amzgSalesBackSlot",true).FirstOrDefault() as Panel;
   var pdvBackSlot=form.Controls.Find("pdvBackSlot",true).FirstOrDefault() as Panel;
   var stockArea=form.Controls.Find("stockNavigation",true).FirstOrDefault() as Panel;
   if(salesBackSlot!=null){
    // Vendas: o botao de retorno ocupa a PRIMEIRA posicao da barra superior.
    backBar.Dock=DockStyle.Fill;
    backBar.Margin=Padding.Empty;
    backBar.Padding=Padding.Empty;
    backBar.BackColor=Color.Transparent;
    back.Dock=DockStyle.Fill;
    back.Left=0;back.Top=0;
    back.Width=salesBackSlot.Width;
    back.Height=salesBackSlot.Height;
    back.Text="⌂  Voltar ao Menu Inicial";
    back.Font=new Font("Segoe UI",9.3f,FontStyle.Bold);
    back.BackColor=Color.FromArgb(198,30,48);
    back.FlatAppearance.MouseOverBackColor=Color.FromArgb(225,51,65);
    salesBackSlot.Controls.Add(backBar);
   }else if(pdvBackSlot!=null){
    // No PDV, o botao Voltar fica antes do campo Codigo/Nome.
    backBar.Dock=DockStyle.Fill;
    backBar.Margin=Padding.Empty;
    backBar.BackColor=Color.FromArgb(248,250,254);
    back.Left=4;back.Top=5;back.Width=256;back.Height=39;
    back.Font=new Font("Segoe UI",10,FontStyle.Bold);
    pdvBackSlot.Controls.Add(backBar);
   }else if(stockArea!=null){
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
   // Uma unica montagem visual, antes de exibir o modulo.
   VisualStartup.Prepare(form);
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
 public sealed class WhatsAppNormalForm:StyledDialogForm {
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
 public sealed class StockCountQuickForm:StyledDialogForm {
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
