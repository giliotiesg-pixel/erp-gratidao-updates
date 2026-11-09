using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ArmazemGratidao {
 // Janela nativa: uma unica calculadora, sem ocupar espaco na pagina Inicial.
 // Nao acessa SQLite, nao cria temporizadores e nao exige bibliotecas externas.
 internal sealed class ModernCalculatorForm : Form {
  readonly CultureInfo locale=CultureInfo.GetCultureInfo("pt-BR");
  readonly CalculatorDisplayLabel display;
  readonly Label result;
  readonly TextBox expression;
  readonly Label preview;
  decimal stored=0m;
  string operation=null;
  bool entering=true;

  public ModernCalculatorForm(){
   Name="amzgModernCalculator";
   Text="Calculadora | Armazém Gratidão";
   // Dimensoes reais para o visor nao ficar cortado em Windows 8 / DPI maior.
   // Ajustar a altura a tela real: nenhuma fileira do teclado pode ficar fora.
   int usable=Screen.PrimaryScreen.WorkingArea.Height;
   ClientSize=new Size(430,Math.Min(685,Math.Max(490,usable-40)));
   AutoScaleMode=AutoScaleMode.None;
   MinimumSize=Size;MaximumSize=Size;
   FormBorderStyle=FormBorderStyle.FixedSingle;
   MaximizeBox=false;
   MinimizeBox=true;
   StartPosition=FormStartPosition.CenterScreen;
   ShowInTaskbar=true;
   KeyPreview=true;
   BackColor=Color.FromArgb(238,243,251);
   Font=new Font("Segoe UI",10,FontStyle.Regular);

   // Identidade no topo conforme modulo Vendas: icone proporcional,
   // nome inteiro visivel e comando Copiar sem sobreposicao.
   var top=new Panel{Name="amzgCalcHeader",Dock=DockStyle.Top,Height=66,
    BackColor=Color.FromArgb(16,59,130)};
   var logo=new PictureBox{Name="amzgCalcLogo",Left=10,Top=9,
    Width=47,Height=47,BackColor=Color.Transparent,
    SizeMode=PictureBoxSizeMode.Zoom};
   try{
    using(var embedded=typeof(ModernCalculatorForm).Assembly.GetManifestResourceStream(
     "ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(embedded!=null)using(var img=Image.FromStream(embedded))
      logo.Image=new Bitmap(img);
    }
   }catch(Exception){/* Mantem nome sempre visivel sem depender de imagem. */}
   if(logo.Image==null)logo.Image=CreateFallbackCart();
   logo.Disposed+=(sender,args)=>{
    if(logo.Image!=null){logo.Image.Dispose();logo.Image=null;}
   };
   var name=new Label{Name="amzgCalcBrand",
    Text="ARMAZÉM GRATIDÃO\nCALCULADORA",
    Left=65,Top=10,Width=231,Height=49,AutoEllipsis=false,
    TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.White,
    BackColor=Color.Transparent,
    Font=new Font("Segoe UI",11.9f,FontStyle.Bold)};
   var copy=new Button{Name="amzgCalcCopy",Text="Copiar",
    Left=310,Top=16,Width=105,Height=35,Margin=new Padding(0),
    FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(43,103,189),
    ForeColor=Color.White,Cursor=Cursors.Hand,
    Font=new Font("Segoe UI",9.2f,FontStyle.Bold)};
   copy.FlatAppearance.BorderSize=0;
   copy.FlatAppearance.MouseOverBackColor=Color.FromArgb(55,126,211);
   copy.Click+=(sender,args)=>CopyResult();
   copy.Resize+=(sender,args)=>Round(copy);
   top.Controls.Add(logo);top.Controls.Add(name);top.Controls.Add(copy);

   // Dois visores NATIVOS: a conta em cima e o resultado maior embaixo.
   // Antes os valores eram pintados em um painel e seus labels ficavam
   // invisiveis. Agora o texto e um controle WinForms realmente visivel.
   var screen=new Panel{Name="amzgCalcScreen",Dock=DockStyle.Top,Height=194,
    BackColor=Color.FromArgb(24,49,89)};
   var calcCaption=new Label{Name="amzgCalcInputCaption",Text="CÁLCULO",
    Left=17,Top=5,Width=140,Height=22,
    Font=new Font("Segoe UI",9.2f,FontStyle.Bold),
    ForeColor=Color.FromArgb(207,226,250)};
   var inputFrame=new Panel{Name="amzgCalcInputFrame",
    Left=13,Top=30,Width=404,Height=48,BackColor=Color.FromArgb(248,251,255),
    Padding=new Padding(10,6,10,6)};
   expression=new TextBox{Name="amzgCalcExpression",Text="0",
    Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,
    BackColor=Color.FromArgb(248,251,255),
    ForeColor=Color.FromArgb(29,72,140),
    Font=new Font("Segoe UI",17f,FontStyle.Bold),
    TextAlign=HorizontalAlignment.Right,ReadOnly=true,
    ShortcutsEnabled=true,TabStop=true};
   inputFrame.Controls.Add(expression);
   // Campo de trabalho nao visivel, usado para preservar a logica antiga.
   // As duas telas verdadeiras ficam acima, com Visible=true.
   var displayFrame=new Panel{Name="amzgCalcDisplayFrame",
    Left=0,Top=0,Width=1,Height=42,Visible=false};
   display=new CalculatorDisplayLabel{Name="amzgCalcDisplay",
    Text="0",Visible=false};
   displayFrame.Controls.Add(display);
   var resultFrame=new Panel{Name="amzgCalcResultFrame",
    Left=13,Top=85,Width=404,Height=101,
    Padding=new Padding(10,7,10,7),
    BackColor=Color.FromArgb(216,232,255)};
   var resultCaption=new Label{Name="amzgCalcResultCaption",Text="RESULTADO",
    Left=13,Top=5,Width=105,Height=18,
    Font=new Font("Segoe UI",9f,FontStyle.Bold),
    ForeColor=Color.FromArgb(29,83,158)};
   result=new Label{Name="amzgCalcResult",Text="0,00",
    Left=9,Top=22,Width=386,Height=70,
    AutoEllipsis=true,TextAlign=ContentAlignment.MiddleRight,
    Font=new Font("Segoe UI",33f,FontStyle.Bold),
    ForeColor=Color.FromArgb(10,57,130),
    BackColor=Color.Transparent};
   resultFrame.Controls.Add(result);
   resultFrame.Controls.Add(resultCaption);
   // Mantido somente por compatibilidade com outros modulos/testes.
   preview=new Label{Name="amzgCalcPreview",Visible=false,Text="Resultado"};
   screen.Controls.Add(displayFrame);
   screen.Controls.Add(calcCaption);
   screen.Controls.Add(inputFrame);
   screen.Controls.Add(resultFrame);
   Action fitScreens=()=>{
    int width=Math.Max(260,screen.ClientSize.Width-26);
    inputFrame.Width=width;
    resultFrame.Width=width;
    result.Width=Math.Max(190,resultFrame.ClientSize.Width-18);
   };
   screen.Resize+=(sender,args)=>fitScreens();
   fitScreens();
   // A janela permanece focada quando o usuario clica em qualquer visor.
   expression.KeyDown+=(sender,e)=>{
    // O formulario recebe primeiro os eventos com KeyPreview=true.
    // Manter o campo somente-leitura evita resultado desincronizado.
   };
   // Fonte fixa legivel: evitar CreateGraphics/FontChanged dentro de TextChanged,
   // pois essa combinacao pode reentrar no layout enquanto uma tecla e clicada.
   var keys=new TableLayoutPanel{Name="amzgCalcKeys",Dock=DockStyle.Fill,
    ColumnCount=4,RowCount=7,Padding=new Padding(8,10,8,10),
    BackColor=Color.FromArgb(238,243,251)};
   for(int c=0;c<4;c++)keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   // Altura ABSOLUTA dimensionada pelo espaco real do monitor. Com porcentagens,
   // o TableLayoutPanel mantinha alturas minimas e cortava o 0, a virgula e o =.
   for(int r=0;r<6;r++)keys.RowStyles.Add(new RowStyle(SizeType.Absolute,50f));
   // A 7a linha nao possui botoes: ela absorve o espaco excedente.
   // Sem este espaco flexivel o WinForms ESTICA a sexta linha, deixando
   // apenas 0, virgula e = desproporcionalmente grandes.
   keys.RowStyles.Add(new RowStyle(SizeType.Percent,100f));
   Action fitKeyRows=()=>{
    int available=keys.ClientSize.Height-keys.Padding.Vertical-12;
    int rowHeight=Math.Max(34,Math.Min(52,available/6));
    for(int i=0;i<6;i++)keys.RowStyles[i].Height=rowHeight;
   };
   keys.Resize+=(sender,args)=>fitKeyRows();
   string[] labels={"%","CE","C","⌫","1/x","x²","√","÷",
    "7","8","9","×","4","5","6","−","1","2","3","+","±","0",",","="};
   for(int i=0;i<labels.Length;i++){
    string key=labels[i];
    bool equal=key=="=", clear=key=="C", number=key.Length==1&&char.IsDigit(key[0]);
    var button=new Button{Text=key,Name="amzgCalcKey"+i,Tag=key,
     Dock=DockStyle.Fill,Margin=new Padding(3),FlatStyle=FlatStyle.Flat,
     Cursor=Cursors.Hand,TabStop=false,
     Font=new Font("Segoe UI",number?17f:14f,FontStyle.Bold),
     BackColor=equal?Color.FromArgb(26,88,182):clear?Color.FromArgb(182,45,66):
       number||key==","?Color.White:Color.FromArgb(214,228,248),
     ForeColor=equal||clear?Color.White:Color.FromArgb(28,62,109)};
    button.FlatAppearance.BorderSize=0;
    button.FlatAppearance.MouseOverBackColor=equal?Color.FromArgb(35,106,209):
       clear?Color.FromArgb(200,60,78):Color.FromArgb(188,216,250);
    button.Resize+=(s,e)=>Round(button);
    button.Click+=(s,e)=>Press(key);
    keys.Controls.Add(button,i%4,i/4);
   }

   Controls.Add(keys);
   fitKeyRows();
   Controls.Add(screen);
   Controls.Add(top);
   KeyDown+=(s,e)=>{
    string key=KeyboardKey(e);
    if(key==null)return;
    if(key=="Copiar")CopyResult();else Press(key);
    e.SuppressKeyPress=true;e.Handled=true;
   };
   Shown+=(s,e)=>{Activate();expression.Focus();};
  }

  // Icone sem arquivos externos caso o recurso embutido esteja indisponivel.
  static Image CreateFallbackCart(){
   var b=new Bitmap(47,47);
   using(var g=Graphics.FromImage(b))
   using(var pen=new Pen(Color.White,3f)){
    g.SmoothingMode=SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);
    g.DrawLine(pen,5,11,10,11);g.DrawLine(pen,10,11,16,32);
    g.DrawLine(pen,16,32,37,32);g.DrawLine(pen,13,17,42,17);
    g.DrawLine(pen,42,17,37,28);g.DrawLine(pen,37,28,15,28);
    g.DrawEllipse(pen,17,35,5,5);g.DrawEllipse(pen,30,35,5,5);
   }
   return b;
  }

  static void AddCalculatorRoundedRect(GraphicsPath path,Rectangle r,int radius){
   int d=radius*2,w=r.Width-1,h=r.Height-1,x=r.X,y=r.Y;
   path.AddArc(x,y,d,d,180,90);
   path.AddArc(x+w-d,y,d,d,270,90);
   path.AddArc(x+w-d,y+h-d,d,d,0,90);
   path.AddArc(x,y+h-d,d,d,90,90);
   path.CloseFigure();
  }

  static void Round(Button button){
   if(button.Width<18||button.Height<18)return;
   using(var path=new GraphicsPath()){
    int d=14,w=button.Width-1,h=button.Height-1;
    path.AddArc(0,0,d,d,180,90);
    path.AddArc(w-d,0,d,d,270,90);
    path.AddArc(w-d,h-d,d,d,0,90);
    path.AddArc(0,h-d,d,d,90,90);
    path.CloseFigure();
    var old=button.Region;
    button.Region=new Region(path);
    if(old!=null)old.Dispose();
   }
  }

  string Format(decimal value){return value.ToString("0.############################",locale);}
  string FormatResult(decimal value){return value.ToString("0.00##########################",locale);}
  decimal Current(){
   decimal value;
   return decimal.TryParse(display.Text,NumberStyles.Number,locale,out value)?value:0m;
  }
  void SetValue(decimal value){display.Text=Format(value);}
  void Error(string message){
   MessageBox.Show(this,message,"Calculadora",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   preview.Text="Confira o cálculo";
  }
  bool Calculate(decimal a,decimal b,string op,out decimal result,bool showError){
   result=0m;
   try{
    if(op=="+")result=a+b;
    else if(op=="−")result=a-b;
    else if(op=="×")result=a*b;
    else if(op=="÷"){
     if(b==0m){if(showError)Error("Não é possível dividir por zero.");return false;}
     result=a/b;
    }
    return true;
   }catch(OverflowException){
    if(showError)Error("O resultado ultrapassa o limite da calculadora.");
    return false;
   }
  }
  // O visor superior SEMPRE mostra a conta escrita, e nao apenas o
  // ultimo numero digitado; o inferior mostra a resposta em tempo real.
  void RefreshPreview(){
   if(operation==null){
    expression.Text=display.Text;
    result.Text=FormatResult(Current());
    preview.Text="Resultado";
    return;
   }
   if(entering){
    expression.Text=Format(stored)+" "+operation;
    result.Text=FormatResult(stored);
    preview.Text="Digite o próximo valor";
    return;
   }
   expression.Text=Format(stored)+" "+operation+" "+display.Text;
   decimal answer;
   bool ok=Calculate(stored,Current(),operation,out answer,false);
   result.Text=ok?FormatResult(answer):"—";
   preview.Text=ok?"Resultado parcial":"Verifique a operação";
  }
  void Press(string key){
   try{
    if(key.Length==1&&char.IsDigit(key[0])){
     if(entering||display.Text=="0"){display.Text=key;entering=false;}
     else if(display.Text.Replace("-","").Replace(",","").Length<25)display.Text+=key;
     RefreshPreview();
     return;
    }
    if(key==","){
     if(entering){display.Text="0,";entering=false;}
     else if(!display.Text.Contains(","))display.Text+=",";
     RefreshPreview();return;
    }
    if(key=="C"){
     stored=0m;operation=null;entering=true;
     display.Text="0";result.Text="0,00";expression.Text="0";
     preview.Text="Use os botões ou o teclado";return;
    }
    if(key=="CE"){
     display.Text="0";entering=true;
     RefreshPreview();return;
    }
    if(key=="⌫"){
     if(entering)return;
     string value=display.Text;
     display.Text=value.Length<=1||value.Length==2&&value[0]=='-'?
      "0":value.Substring(0,value.Length-1);
     RefreshPreview();return;
    }
    if(key=="±"){
     SetValue(-Current());entering=false;RefreshPreview();return;
    }
    if(key=="%"||key=="x²"||key=="√"||key=="1/x"){
     decimal n=Current(),answer;
     if(key=="%"){
      answer=operation=="+"||operation=="−"?stored*n/100m:n/100m;
     }else if(key=="x²")answer=n*n;
     else if(key=="1/x"){
      if(n==0m){Error("Não é possível dividir por zero.");return;}
      answer=1m/n;
     }else{
      if(n<0m){Error("Não é possível calcular a raiz de número negativo.");return;}
      answer=Convert.ToDecimal(Math.Sqrt((double)n));
     }
     SetValue(answer);entering=false;RefreshPreview();return;
    }
    if(key=="="){
     if(operation==null)return;
     decimal right=entering?stored:Current(),answer;
     if(!Calculate(stored,right,operation,out answer,true))return;
     expression.Text=Format(stored)+" "+operation+" "+Format(right)+" =";
     SetValue(answer);
     operation=null;entering=true;
     result.Text=FormatResult(answer);
     preview.Text="Resultado";return;
    }
    if(key=="+"||key=="−"||key=="×"||key=="÷"){
     decimal current=Current();
     if(operation!=null&&!entering){
      decimal total;
      if(!Calculate(stored,current,operation,out total,true))return;
      stored=total;SetValue(total);
     }else if(operation==null)stored=current;
     operation=key;entering=true;
     expression.Text=Format(stored)+" "+key;
     result.Text=FormatResult(stored);
     preview.Text="Digite o próximo valor";
    }
   }catch(OverflowException){Error("Número muito grande.");}
    catch(FormatException){Error("Número inválido.");}
  }
  void CopyResult(){
   try{
    Clipboard.SetText(result.Text);
    preview.Text="Resultado copiado";
   }catch(Exception){
    preview.Text="Não foi possível copiar agora";
   }
  }
  static string KeyboardKey(KeyEventArgs e){
   if(e.Control&&e.KeyCode==Keys.C)return "Copiar";
   if(e.Control||e.Alt)return null;
   if(e.KeyCode>=Keys.D0&&e.KeyCode<=Keys.D9&&!e.Shift)
    return ((int)e.KeyCode-(int)Keys.D0).ToString();
   if(e.KeyCode>=Keys.NumPad0&&e.KeyCode<=Keys.NumPad9)
    return ((int)e.KeyCode-(int)Keys.NumPad0).ToString();
   switch(e.KeyCode){
    case Keys.Add: return "+";
    case Keys.Subtract: case Keys.OemMinus: return "−";
    case Keys.Multiply: return "×";
    case Keys.Divide: case Keys.OemQuestion: return "÷";
    case Keys.Oemplus: return e.Shift?"+":"=";
    case Keys.Return: return "=";
    case Keys.Decimal: case Keys.Oemcomma: case Keys.OemPeriod: return ",";
    case Keys.Back: return "⌫";
    case Keys.Delete: return "CE";
    case Keys.Escape: return "C";
    default: return null;
   }
  }
 }


 // Label desenhado explicitamente ao centro, sem recorte na primeira linha.
 // A fonte e reduzida apenas quando o numero nao cabe, respeitando o DPI.
 internal sealed class CalculatorDisplayLabel : Label {
  public CalculatorDisplayLabel(){
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
    ControlStyles.OptimizedDoubleBuffer,true);
   AutoSize=false;
  }
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(BackColor);
   if(ClientSize.Width<20||ClientSize.Height<20)return;
   string text=string.IsNullOrEmpty(Text)?"0":Text;
   float fontSize=33f;
   Font rendered=null;
   try{
    while(fontSize>=14f){
     if(rendered!=null)rendered.Dispose();
     rendered=new Font("Segoe UI",fontSize,FontStyle.Bold);
     var measured=TextRenderer.MeasureText(e.Graphics,text,rendered,
      new Size(int.MaxValue,ClientSize.Height),
      TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
     if(measured.Width<=ClientSize.Width-22&&measured.Height<=ClientSize.Height-14)
      break;
     fontSize-=2f;
    }
    var bounds=new Rectangle(9,5,Math.Max(20,ClientSize.Width-19),
     Math.Max(22,ClientSize.Height-10));
    TextRenderer.DrawText(e.Graphics,text,rendered,bounds,ForeColor,
     TextFormatFlags.Right|TextFormatFlags.VerticalCenter|
     TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|
     TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   }finally{if(rendered!=null)rendered.Dispose();}
  }
 }

 // Painel da tela inicial: marca d'agua pintada no fundo, sem capturar clique.
 internal sealed class BrandedHomePanel : Panel {
  public BrandedHomePanel(){
   DoubleBuffered=true;ResizeRedraw=true;
   BackColor=Color.FromArgb(246,249,253);
  }
  protected override void OnPaintBackground(PaintEventArgs e){
   base.OnPaintBackground(e);
   HomeBrandWatermark.Draw(e.Graphics,ClientRectangle);
  }
 }

 // Marca d'agua vetorial sem fundo quadrado: apenas contornos suaves
 // da identidade Armazem Gratidao. O logotipo original nao e modificado.
 // E pintada atras dos controles e nunca interfere em cliques ou leitura.
 internal static class HomeBrandWatermark {
  public static void Draw(Graphics graphics,Rectangle bounds){
   if(bounds.Width<380||bounds.Height<320)return;
   float size=Math.Min(430f,Math.Min(bounds.Width*0.55f,bounds.Height*0.72f));
   if(size<200f)return;
   float scale=size/400f;
   var saved=graphics.Save();
   try{
    graphics.SmoothingMode=SmoothingMode.AntiAlias;
    graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;
    float left=bounds.Left+Math.Max(0f,(bounds.Width-size)*0.69f);
    float top=bounds.Top+Math.Max(8f,(bounds.Height-size)*0.30f);
    graphics.TranslateTransform(left,top);
    graphics.ScaleTransform(scale,scale);
    // As cores sao proximas do fundo claro: sem bloco escuro e sem saturacao.
    using(var pale=new Pen(Color.FromArgb(226,236,249),8f))
    using(var secondary=new Pen(Color.FromArgb(231,239,250),5f))
    using(var textBrush=new SolidBrush(Color.FromArgb(228,237,249)))
    using(var fontMain=new Font("Segoe UI",30f,FontStyle.Bold))
    using(var fontSmall=new Font("Segoe UI",20f,FontStyle.Bold)){
     pale.StartCap=pale.EndCap=LineCap.Round;
     pale.LineJoin=LineJoin.Round;
     secondary.StartCap=secondary.EndCap=LineCap.Round;
     graphics.DrawLines(pale,new PointF[]{
      new PointF(26,126),new PointF(199,25),new PointF(374,126)});
     graphics.DrawLine(pale,50,128,50,245);
     graphics.DrawLine(pale,350,128,350,245);
     graphics.DrawRectangle(secondary,67,192,44,51);
     graphics.DrawLine(secondary,88,192,88,243);
     graphics.DrawRectangle(secondary,292,168,47,75);
     graphics.DrawLine(secondary,314,168,314,243);
     // Carrinho estilizado, inspirado no simbolo do logotipo original.
     graphics.DrawLine(pale,118,116,142,116);
     graphics.DrawLine(pale,142,116,160,218);
     graphics.DrawLine(pale,160,218,275,218);
     graphics.DrawLine(pale,148,143,288,143);
     graphics.DrawLine(pale,288,143,271,201);
     graphics.DrawLine(pale,271,201,155,201);
     for(int i=1;i<4;i++)
      graphics.DrawLine(secondary,150+i*30,146,157+i*25,200);
     graphics.DrawLine(secondary,151,171,280,171);
     graphics.FillEllipse(textBrush,171,225,19,19);
     graphics.FillEllipse(textBrush,246,225,19,19);
     // Letras sem moldura colorida: marca d'agua quase imperceptivel.
     graphics.DrawString("ARMAZÉM",fontSmall,textBrush,116,260);
     graphics.DrawString("GRATIDÃO",fontMain,textBrush,72,291);
     graphics.DrawString("PDV",fontSmall,textBrush,178,343);
    }
   }finally{graphics.Restore(saved);}
  }
 }
}
