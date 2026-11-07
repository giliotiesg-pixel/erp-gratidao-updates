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
  readonly Label expression;
  readonly Label preview;
  decimal stored=0m;
  string operation=null;
  bool entering=true;

  public ModernCalculatorForm(){
   Name="amzgModernCalculator";
   Text="Calculadora | Armazém Gratidão";
   // Dimensoes reais para o visor nao ficar cortado em Windows 8 / DPI maior.
   ClientSize=new Size(430,618);
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

   var top=new Panel{Dock=DockStyle.Top,Height=51,BackColor=Color.FromArgb(24,49,89)};
   var name=new Label{Text="  ▦  CALCULADORA",Dock=DockStyle.Left,Width=203,
    TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.White,
    Font=new Font("Segoe UI",11,FontStyle.Bold)};
   var copy=new Button{Text="Copiar",Dock=DockStyle.Right,Width=108,
    Margin=new Padding(9),FlatStyle=FlatStyle.Flat,
    BackColor=Color.FromArgb(46,91,164),ForeColor=Color.White,
    Cursor=Cursors.Hand,Font=new Font("Segoe UI",9,FontStyle.Bold)};
   copy.FlatAppearance.BorderSize=0;
   copy.Click+=(s,e)=>CopyResult();
   top.Controls.Add(name);top.Controls.Add(copy);

   var screen=new Panel{Dock=DockStyle.Top,Height=200,
    BackColor=Color.FromArgb(24,49,89)};
   expression=new Label{Name="amzgCalcExpression",Text="PRONTO PARA CALCULAR",
    Left=19,Top=9,Width=391,Height=29,
    Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,
    TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.FromArgb(184,205,236),
    Font=new Font("Segoe UI",10,FontStyle.Bold)};
   // Quadro branco independente do teclado. Label no lugar de TextBox evita
   // caracteres iniciais invisiveis e corte vertical do texto no Windows 8.
   var displayFrame=new Panel{Name="amzgCalcDisplayFrame",Left=13,Top=42,
    Width=402,Height=106,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,
    Padding=new Padding(9,2,9,2),BackColor=Color.White};
   display=new CalculatorDisplayLabel{Name="amzgCalcDisplay",Dock=DockStyle.Fill,Text="0",
    TabStop=false,AutoEllipsis=false,
    Font=new Font("Segoe UI",33,FontStyle.Bold),
    TextAlign=ContentAlignment.MiddleRight,BackColor=Color.White,
    ForeColor=Color.FromArgb(20,61,122)};
   displayFrame.Controls.Add(display);
   Action roundDisplay=()=>{
    if(displayFrame.Width<24||displayFrame.Height<24)return;
    using(var outline=new GraphicsPath()){
     int d=19,w=displayFrame.Width-1,h=displayFrame.Height-1;
     outline.AddArc(0,0,d,d,180,90);
     outline.AddArc(w-d,0,d,d,270,90);
     outline.AddArc(w-d,h-d,d,d,0,90);
     outline.AddArc(0,h-d,d,d,90,90);
     outline.CloseFigure();
     var previous=displayFrame.Region;
     displayFrame.Region=new Region(outline);
     if(previous!=null)previous.Dispose();
    }
   };
   displayFrame.Resize+=(s,e)=>roundDisplay();
   roundDisplay();
   preview=new Label{Name="amzgCalcPreview",Text="Digite os números ou use o teclado",
    Left=17,Top=158,Width=395,Height=32,
    Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top,
    TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.FromArgb(196,217,243),
    Font=new Font("Segoe UI",10,FontStyle.Regular)};
   // Labels guardam os valores de forma acessivel a testes e teclas.
   // A pintura direta nos paineis impede que o Windows 8 corte ou esconda
   // os algarismos (inclusive em captura real com DrawToBitmap).
   display.Visible=false;expression.Visible=false;preview.Visible=false;
   displayFrame.Paint+=(sender,e)=>{
    e.Graphics.Clear(Color.White);
    if(displayFrame.ClientSize.Width<50||displayFrame.ClientSize.Height<50)return;
    string digits=string.IsNullOrWhiteSpace(display.Text)?"0":display.Text;
    Font font=null;
    try{
     for(float size=35f;size>=14f;size-=2f){
      if(font!=null)font.Dispose();
      font=new Font("Segoe UI",size,FontStyle.Bold);
      Size measured=TextRenderer.MeasureText(e.Graphics,digits,font,
       new Size(100000,displayFrame.Height),TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
      if(measured.Width<displayFrame.Width-26&&measured.Height<displayFrame.Height-14)break;
     }
     TextRenderer.DrawText(e.Graphics,digits,font,
      new Rectangle(10,5,displayFrame.Width-23,displayFrame.Height-10),
      Color.FromArgb(20,61,122),
      TextFormatFlags.Right|TextFormatFlags.VerticalCenter|
      TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
    }finally{if(font!=null)font.Dispose();}
   };
   screen.Paint+=(sender,e)=>{
    TextRenderer.DrawText(e.Graphics,expression.Text,expression.Font,
     new Rectangle(16,7,screen.Width-30,30),expression.ForeColor,
     TextFormatFlags.Right|TextFormatFlags.VerticalCenter|
     TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
    TextRenderer.DrawText(e.Graphics,preview.Text,preview.Font,
     new Rectangle(16,158,screen.Width-30,30),preview.ForeColor,
     TextFormatFlags.Right|TextFormatFlags.VerticalCenter|
     TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
   };
   display.TextChanged+=(sender,e)=>displayFrame.Invalidate();
   expression.TextChanged+=(sender,e)=>screen.Invalidate();
   preview.TextChanged+=(sender,e)=>screen.Invalidate();
   screen.Controls.Add(expression);screen.Controls.Add(displayFrame);screen.Controls.Add(preview);

   var keys=new TableLayoutPanel{Name="amzgCalcKeys",Dock=DockStyle.Fill,
    ColumnCount=4,RowCount=6,Padding=new Padding(8,10,8,10),
    BackColor=Color.FromArgb(238,243,251)};
   for(int c=0;c<4;c++)keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   for(int r=0;r<6;r++)keys.RowStyles.Add(new RowStyle(SizeType.Percent,100f/6f));
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
   Controls.Add(screen);
   Controls.Add(top);
   KeyDown+=(s,e)=>{
    string key=KeyboardKey(e);
    if(key==null)return;
    if(key=="Copiar")CopyResult();else Press(key);
    e.SuppressKeyPress=true;e.Handled=true;
   };
   Shown+=(s,e)=>Activate();
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
  void RefreshPreview(){
   if(operation==null||entering){
    if(operation==null)preview.Text="Cálculo rápido em reais e centavos";
    else preview.Text="Digite o próximo valor";
    return;
   }
   decimal answer;
   preview.Text=Calculate(stored,Current(),operation,out answer,false)?
    "= "+Format(answer):"";
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
     display.Text="0";expression.Text="PRONTO PARA CALCULAR";
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
     preview.Text="Digite o próximo valor";
    }
   }catch(OverflowException){Error("Número muito grande.");}
    catch(FormatException){Error("Número inválido.");}
  }
  void CopyResult(){
   try{
    Clipboard.SetText(display.Text);
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
