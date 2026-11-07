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
  readonly Label display;
  readonly Label expression;
  readonly Label preview;
  decimal stored=0m;
  string operation=null;
  bool entering=true;

  public ModernCalculatorForm(){
   Name="amzgModernCalculator";
   Text="Calculadora | Armazém Gratidão";
   // Dimensoes reais para o visor nao ficar cortado em Windows 8 / DPI maior.
   ClientSize=new Size(386,552);
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

   var screen=new Panel{Dock=DockStyle.Top,Height=164,
    BackColor=Color.FromArgb(24,49,89)};
   expression=new Label{Name="amzgCalcExpression",Text="PRONTO PARA CALCULAR",
    Left=18,Top=5,Width=350,Height=27,
    Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,
    TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.FromArgb(184,205,236),
    Font=new Font("Segoe UI",10,FontStyle.Bold)};
   // Quadro branco independente do teclado. Label no lugar de TextBox evita
   // caracteres iniciais invisiveis e corte vertical do texto no Windows 8.
   var displayFrame=new Panel{Name="amzgCalcDisplayFrame",Left=13,Top=37,
    Width=360,Height=76,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,
    Padding=new Padding(9,2,9,2),BackColor=Color.White};
   display=new Label{Name="amzgCalcDisplay",Dock=DockStyle.Fill,Text="0",
    TabStop=false,AutoEllipsis=false,
    Font=new Font("Segoe UI",30,FontStyle.Bold),
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
   Action fitDigits=()=>{
    if(display.IsDisposed||display.ClientSize.Width<50)return;
    float size=30;
    using(var measured=new Font("Segoe UI",size,FontStyle.Bold)){
     var width=TextRenderer.MeasureText(display.Text,measured).Width;
     if(width>display.ClientSize.Width-12)
      size=Math.Max(15f,size*(display.ClientSize.Width-12f)/width);
    }
    if(Math.Abs(display.Font.SizeInPoints-size)>0.5f)
     display.Font=new Font("Segoe UI",size,FontStyle.Bold);
   };
   display.TextChanged+=(s,e)=>fitDigits();
   display.Resize+=(s,e)=>fitDigits();
   preview=new Label{Name="amzgCalcPreview",Text="Digite os números ou use o teclado",
    Left=16,Top=122,Width=351,Height=32,
    Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top,
    TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.FromArgb(196,217,243),
    Font=new Font("Segoe UI",10,FontStyle.Regular)};
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

 // Imagem enviada pelo usuario, incorporada ao EXE como recurso (offline).
 // Renderizar uma unica imagem com baixa opacidade; nenhuma leitura em disco,
 // nenhuma consulta de rede e nenhum processamento adicional a cada clique.
 internal static class HomeBrandWatermark {
  static readonly Image logo=LoadLogo();
  static Image LoadLogo(){
   try{
    var assembly=typeof(HomeBrandWatermark).Assembly;
    using(var stream=assembly.GetManifestResourceStream("ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(stream==null)return null;
     using(var bitmap=Image.FromStream(stream))return new Bitmap(bitmap);
    }
   }catch{return null;} // A tela inicial funciona mesmo que a imagem falhe.
  }
  public static void Draw(Graphics graphics,Rectangle bounds){
   if(logo==null||bounds.Width<340||bounds.Height<240)return;
   float size=Math.Min(500f,Math.Min(bounds.Width*0.56f,bounds.Height*0.77f));
   if(size<150f)return;
   int side=(int)Math.Round(size);
   // Centralizar na area livre; manter nome, botoes e indicadores na frente.
   int left=bounds.Left+(int)((bounds.Width-side)*0.61f);
   int top=bounds.Top+(int)((bounds.Height-side)*0.51f);
   var area=new Rectangle(left,top,side,side);
   var state=graphics.Save();
   try{
    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
    using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
     var opacity=new System.Drawing.Imaging.ColorMatrix();
     opacity.Matrix00=1f;opacity.Matrix11=1f;opacity.Matrix22=1f;
     opacity.Matrix33=0.095f;opacity.Matrix44=1f;
     attributes.SetColorMatrix(opacity);
     graphics.DrawImage(logo,area,0,0,logo.Width,logo.Height,GraphicsUnit.Pixel,attributes);
    }
   }finally{graphics.Restore(state);}
  }
 }
}
