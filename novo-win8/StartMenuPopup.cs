using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ArmazemGratidao {
 // Menu Iniciar REAL: janela flutuante sem bordas, categorias e ramificacoes.
 // Nao fica ancorado na area de trabalho nem substitui os modulos WinForms.
 internal sealed class StartMenuPopup : Form {
  readonly List<Tuple<string,EventHandler>> actions;
  readonly Func<string,string> categoryOf;
  readonly Func<string,Image> iconFor;
  readonly Action<Tuple<string,EventHandler>> launch;
  readonly List<Image> allocatedIcons=new List<Image>();
  readonly Dictionary<string,Image> iconsByName=new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);
  readonly FlowLayoutPanel categories;
  readonly FlowLayoutPanel modules;
  readonly Panel branchPane;
  readonly Label branchTitle;
  readonly TextBox search;
  readonly Dictionary<string,Button> sectionButtons=new Dictionary<string,Button>();
  const int CompactWidth=296;
  const int ExpandedWidth=860;
  string selectedSection=null;
  bool closing=false;

  public StartMenuPopup(IEnumerable<Tuple<string,EventHandler>> available,
   Func<string,string> classify,Func<string,Image> drawIcon,
   Action<Tuple<string,EventHandler>> open) {
   actions=available.ToList();
   categoryOf=classify;
   iconFor=drawIcon;
   launch=open;
   Name="amzgFloatingStartMenu";
   Text="Iniciar - Armazem Gratidao";
   FormBorderStyle=FormBorderStyle.None;
   ShowInTaskbar=false;
   StartPosition=FormStartPosition.Manual;
   BackColor=Color.FromArgb(241,246,253);
   Size=new Size(CompactWidth,566);
   MinimumSize=new Size(CompactWidth,395);
   KeyPreview=true;
   DoubleBuffered=true;

   // Area de ramificacoes so aparece quando a categoria for escolhida.
   branchPane=new Panel{Name="amzgStartFlyout",Dock=DockStyle.Fill,
    Visible=false,BackColor=Color.FromArgb(246,249,254),
    Padding=new Padding(10,9,9,7)};
   branchTitle=new Label{Name="amzgBranchTitle",Dock=DockStyle.Top,Height=48,
    Font=new Font("Segoe UI",12f,FontStyle.Bold),
    ForeColor=Color.FromArgb(25,68,139),TextAlign=ContentAlignment.MiddleLeft};
   modules=new FlowLayoutPanel{Name="amzgStartModules",Dock=DockStyle.Fill,
    AutoScroll=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,
    BackColor=Color.FromArgb(246,249,254),Padding=new Padding(4,5,3,9)};
   var branchFooter=new Label{Dock=DockStyle.Bottom,Height=25,
    ForeColor=Color.FromArgb(108,117,135),Font=new Font("Segoe UI",8f),
    Text="Clique no modulo para abrir. Esc fecha o menu."};
   branchPane.Controls.Add(modules);
   branchPane.Controls.Add(branchTitle);
   branchPane.Controls.Add(branchFooter);

   var navigation=new Panel{Name="amzgStartCategoriesPanel",Dock=DockStyle.Left,
    Width=CompactWidth,BackColor=Color.FromArgb(18,52,107)};
   var branding=new Label{Dock=DockStyle.Top,Height=62,
    Text="  ⊞  INICIAR\n       Armazém Gratidão",
    Font=new Font("Segoe UI",11.6f,FontStyle.Bold),
    ForeColor=Color.White,TextAlign=ContentAlignment.MiddleLeft,
    BackColor=Color.FromArgb(12,39,90)};
   var instructions=new Label{Dock=DockStyle.Top,Height=31,
    Text="  SEÇÕES DO PROGRAMA",
    Font=new Font("Segoe UI",9f,FontStyle.Bold),
    ForeColor=Color.FromArgb(193,211,238),
    TextAlign=ContentAlignment.MiddleLeft};
   categories=new FlowLayoutPanel{Name="amzgStartCategories",Dock=DockStyle.Fill,
    AutoScroll=true,FlowDirection=FlowDirection.TopDown,
    WrapContents=false,Padding=new Padding(7,4,5,5),
    BackColor=Color.FromArgb(29,47,79)};
   var footer=new Panel{Dock=DockStyle.Bottom,Height=42,
    BackColor=Color.FromArgb(20,37,67)};
   var close=new Button{Name="amzgCloseStart",Text="  ×  Fechar menu",
    Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,
    Font=new Font("Segoe UI",9f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,
    ForeColor=Color.White,BackColor=Color.FromArgb(20,37,67),Cursor=Cursors.Hand};
   close.FlatAppearance.BorderSize=0;close.Click+=(sender,args)=>Close();
   footer.Controls.Add(close);
   navigation.Controls.Add(categories);
   navigation.Controls.Add(instructions);
   navigation.Controls.Add(branding);
   navigation.Controls.Add(footer);

   var searchBar=new Panel{Dock=DockStyle.Top,Height=52,
    BackColor=Color.White,Padding=new Padding(11,10,11,9)};
   search=new TextBox{Name="amzgStartSearch",Dock=DockStyle.Fill,
    BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",11f),
    AccessibleName="Pesquisar modulo",TabStop=true};
   searchBar.Controls.Add(search);
   var searchHint=new Label{Text="⌕  Pesquisar:",Dock=DockStyle.Left,
    Width=119,ForeColor=Color.FromArgb(73,89,112),
    TextAlign=ContentAlignment.MiddleLeft,
    Font=new Font("Segoe UI",9.3f,FontStyle.Bold)};
   searchBar.Controls.Add(searchHint);

   Controls.Add(branchPane);
   Controls.Add(navigation);
   Controls.Add(searchBar);
   var order=new[]{"VENDAS E ATENDIMENTO","PRODUTOS E ESTOQUE",
     "FINANCEIRO E CAIXA","CONSULTAS E RELATÓRIOS","SISTEMA E MANUTENÇÃO"};
   foreach(string section in order){
    int count=actions.Count(a=>categoryOf(a.Item1)==section);
    if(count==0)continue;
    string categoryIcon=section.StartsWith("VENDAS")?"PDV":
     section.StartsWith("PRODUTOS")?"Estoque":
     section.StartsWith("FINANCEIRO")?"Caixa":
     section.StartsWith("CONSULTAS")?"Relatórios":"Sistema";
    var btn=new Button{Name="amzgCategory"+sectionButtons.Count,
     Tag=section,Text="  "+section+"   ›",
     Size=new Size(272,66),MinimumSize=new Size(272,66),
     MaximumSize=new Size(272,66),Margin=new Padding(0,2,0,3),
     Font=new Font("Segoe UI",10f,FontStyle.Bold),
     BackColor=Color.FromArgb(23,81,169),ForeColor=Color.White,
     FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,
     Image=ManagedIcon(categoryIcon,40),ImageAlign=ContentAlignment.MiddleLeft,
     TextImageRelation=TextImageRelation.ImageBeforeText,
     AutoEllipsis=true,Cursor=Cursors.Hand,UseMnemonic=false};
    btn.FlatAppearance.BorderSize=0;
    btn.FlatAppearance.MouseOverBackColor=Color.FromArgb(43,111,207);
    RoundedBlue(btn,12);
    btn.Click+=(sender,args)=>SelectSection(section);
    categories.Controls.Add(btn);sectionButtons.Add(section,btn);
   }
   search.TextChanged+=(sender,args)=>SearchModules();
   search.KeyDown+=(sender,args)=>{
    if(args.KeyCode==Keys.Escape){Close();args.SuppressKeyPress=true;}
    else if(args.KeyCode==Keys.Enter){
     var first=modules.Controls.OfType<Button>().FirstOrDefault(b=>b.Visible);
     if(first!=null)first.PerformClick();
     args.SuppressKeyPress=true;
    }
   };
   KeyDown+=(sender,args)=>{
    if(args.KeyCode==Keys.Escape){Close();args.Handled=true;}
   };
   Deactivate+=(sender,args)=>{
    if(closing||IsDisposed)return;
    // O clique fora fecha o menu sem tocar nas janelas em uso.
    if(IsHandleCreated)BeginInvoke((Action)(()=>{
     if(!closing&&!IsDisposed&&!ContainsFocus)Close();
    }));
   };
   FormClosed+=(sender,args)=>{
    closing=true;
    foreach(var icon in allocatedIcons)icon.Dispose();
    allocatedIcons.Clear();
   };
  }

  // Botao arredondado no mesmo estilo dos acessos rapidos da pagina inicial.
  static void RoundedBlue(Button b,int radius){
   Action apply=()=>{
    if(b.IsDisposed||b.Width<radius*2+4||b.Height<radius*2+4)return;
    using(var regionPath=new GraphicsPath()){
     int w=b.Width-1,h=b.Height-1,d=radius*2;
     regionPath.AddArc(0,0,d,d,180,90);
     regionPath.AddArc(w-d,0,d,d,270,90);
     regionPath.AddArc(w-d,h-d,d,d,0,90);
     regionPath.AddArc(0,h-d,d,d,90,90);
     regionPath.CloseFigure();
     var previous=b.Region;
     b.Region=new Region(regionPath);
     if(previous!=null)previous.Dispose();
    }
   };
   b.Resize+=(sender,args)=>apply();
   apply();
  }

  Image ManagedIcon(string name,int size){
   string key=name+"/"+size;
   Image cached;
   if(iconsByName.TryGetValue(key,out cached))return cached;
   using(var original=iconFor(name)){
    var icon=new Bitmap(size,size);
    using(var graphics=Graphics.FromImage(icon)){
     graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
     graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
     graphics.DrawImage(original,new Rectangle(0,0,size,size));
    }
    // A imagem e pintada de branco, preservando a transparencia original.
    for(int y=0;y<icon.Height;y++)for(int x=0;x<icon.Width;x++){
     Color pixel=icon.GetPixel(x,y);
     if(pixel.A>0)icon.SetPixel(x,y,Color.FromArgb(pixel.A,255,255,255));
    }
    allocatedIcons.Add(icon);
    iconsByName[key]=icon;
    return icon;
   }
  }

  public int CategoryCount {get{return sectionButtons.Count;}}
  public int ModuleCount {get{return actions.Count;}}
  public bool IsBranchOpen {get{return branchPane.Visible;}}
  public string SelectedSection {get{return selectedSection;}}
  public void SelectSection(string section){
   if(IsDisposed)return;
   selectedSection=section;
   search.Clear();
   PopulateModules(actions.Where(a=>categoryOf(a.Item1)==section));
   branchTitle.Text=section+"   ›";
   branchPane.Visible=true;
   ResizeExpanded();
   foreach(var entry in sectionButtons)
    entry.Value.BackColor=entry.Key==section?Color.FromArgb(30,117,218):Color.FromArgb(23,81,169);
   branchPane.BringToFront();
   modules.PerformLayout();
  }

  void ResizeExpanded(){
   Rectangle work=Screen.FromControl(this).WorkingArea;
   int maxWidth=Math.Max(CompactWidth+210,work.Width-16);
   int targetWidth=Math.Min(ExpandedWidth,maxWidth);
   if(Left+targetWidth>work.Right-8)
    Left=Math.Max(work.Left+8,work.Right-targetWidth-8);
   Width=targetWidth;
  }

  void SearchModules(){
   string query=Normalize(search.Text);
   if(query.Length==0){
    if(selectedSection!=null)SelectSectionContents(selectedSection);
    else CollapseBranch();
    return;
   }
   var result=actions.Where(a=>Normalize(a.Item1).Contains(query)).ToList();
   branchTitle.Text="RESULTADOS DA PESQUISA";
   PopulateModules(result);
   branchPane.Visible=true;ResizeExpanded();
   foreach(var button in sectionButtons.Values)button.BackColor=Color.FromArgb(23,81,169);
  }

  void SelectSectionContents(string section){
   PopulateModules(actions.Where(a=>categoryOf(a.Item1)==section));
   branchTitle.Text=section+"   ›";
   branchPane.Visible=true;ResizeExpanded();
  }

  public void CollapseBranch(){
   selectedSection=null;
   branchPane.Visible=false;
   modules.Controls.Clear();
   Width=CompactWidth;
   foreach(var btn in sectionButtons.Values)btn.BackColor=Color.FromArgb(29,47,79);
  }

  void PopulateModules(IEnumerable<Tuple<string,EventHandler>> source){
   modules.SuspendLayout();
   modules.Controls.Clear();
   foreach(var item in source){
    string label=CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(
     item.Item1.ToLower(CultureInfo.GetCultureInfo("pt-BR")));
    label=label.Replace("Pdv","PDV").Replace("Whatsapp","WhatsApp");
    var tile=new Button{Name="amzgStartModule"+modules.Controls.Count,
     AccessibleDescription=item.Item1,Tag=item.Item1,
     Text=label,Size=new Size(228,79),
     MinimumSize=new Size(228,79),MaximumSize=new Size(228,79),
     Margin=new Padding(5,5,5,5),Padding=new Padding(6,0,6,0),
     BackColor=Color.FromArgb(20,78,166),ForeColor=Color.White,
     FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,AutoEllipsis=true,
     UseMnemonic=false,Font=new Font("Segoe UI",10f,FontStyle.Bold),
     Image=ManagedIcon(item.Item1,40),ImageAlign=ContentAlignment.MiddleLeft,
     TextAlign=ContentAlignment.MiddleCenter,
     TextImageRelation=TextImageRelation.ImageBeforeText};
    tile.FlatAppearance.BorderSize=0;
    RoundedBlue(tile,12);
    tile.FlatAppearance.MouseOverBackColor=Color.FromArgb(38,112,211);
    tile.Click+=(sender,args)=>{
     if(closing)return;
     closing=true;
     Hide();Close();
     launch(item);
    };
    modules.Controls.Add(tile);
   }
   modules.ResumeLayout(true);
  }

  static string Normalize(string text){
   if(string.IsNullOrEmpty(text))return "";
   var form=text.Normalize(System.Text.NormalizationForm.FormD);
   return new string(form.Where(ch=>
    CharUnicodeInfo.GetUnicodeCategory(ch)!=UnicodeCategory.NonSpacingMark)
    .ToArray()).ToUpperInvariant();
  }
 }
}
