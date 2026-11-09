using System;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public sealed class ConsultaPrecoForm:Form {
  readonly string db;
  readonly TextBox busca=new TextBox{Dock=DockStyle.Top,Font=new Font("Segoe UI",17)};
  readonly Label descricao=new Label{Dock=DockStyle.Top,Height=90,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",18,FontStyle.Bold)};
  readonly Label preco=new Label{Dock=DockStyle.Top,Height=105,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",35,FontStyle.Bold),ForeColor=Color.FromArgb(180,35,50)};
  readonly ListBox lista=new ListBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",11)};
  readonly System.Collections.Generic.List<long> ids=new System.Collections.Generic.List<long>();
  readonly Timer atraso=new Timer{Interval=300};
  public ConsultaPrecoForm(string database){
   db=database;Text="Armazém Gratidão | Consulta de Preços";
   Size=new Size(720,590);MinimumSize=new Size(530,450);
   BackColor=Color.FromArgb(245,249,254);
   var layout=new TableLayoutPanel{Name="amzgConsultaPrecoLayout",
    Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,
    Padding=new Padding(10),BackColor=Color.FromArgb(245,249,254)};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,95));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,130));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var top=new Panel{Name="amzgConsultaPrecoHeader",Dock=DockStyle.Fill,
    BackColor=Color.FromArgb(15,59,134),Margin=Padding.Empty};
   var emblem=new PictureBox{Name="amzgConsultaPrecoLogo",Left=9,Top=7,
    Width=53,Height=53,BackColor=Color.Transparent,SizeMode=PictureBoxSizeMode.Zoom};
   try{
    using(var embedded=typeof(ConsultaPrecoForm).Assembly.GetManifestResourceStream(
     "ArmazemGratidao.LogotipoMarcaDagua.jpg")){
     if(embedded!=null)using(var original=Image.FromStream(embedded))
      emblem.Image=new Bitmap(original);
    }
   }catch(Exception){}
   emblem.Disposed+=(sender,args)=>{if(emblem.Image!=null)emblem.Image.Dispose();};
   var title=new Label{Name="amzgConsultaPrecoTitle",
    Text="ARMAZÉM GRATIDÃO   •   CONSULTA DE PREÇOS",
    Left=73,Top=7,Height=55,Width=605,
    ForeColor=Color.White,Font=new Font("Segoe UI",16f,FontStyle.Bold),
    TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
   top.Resize+=(sender,args)=>title.Width=Math.Max(85,top.Width-84);
   top.Controls.Add(emblem);top.Controls.Add(title);
   layout.Controls.Add(top,0,0);

   var searchArea=new TableLayoutPanel{Name="amzgConsultaPrecoSearch",
    Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(0,8,0,4),
    Padding=new Padding(11,6,11,3),BackColor=Color.FromArgb(228,238,252)};
   searchArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,83));
   searchArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,17));
   searchArea.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
   searchArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   searchArea.Controls.Add(new Label{
    Text="Bipe o código de barras ou pesquise por descrição",
    Font=new Font("Segoe UI",10,FontStyle.Bold),
    ForeColor=Color.FromArgb(20,62,133),
    Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
   busca.Dock=DockStyle.Fill;busca.Margin=new Padding(0,0,8,0);
   busca.Font=new Font("Segoe UI",14f);
   searchArea.Controls.Add(busca,0,1);
   var reset=new Button{Name="amzgConsultaPrecoClear",Text="Limpar",
    Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,
    BackColor=Color.FromArgb(25,83,172),ForeColor=Color.White,
    FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10f,FontStyle.Bold)};
   reset.FlatAppearance.BorderSize=0;
   reset.Click+=(sender,args)=>{busca.Clear();busca.Focus();};
   searchArea.Controls.Add(reset,1,1);
   layout.Controls.Add(searchArea,0,1);

   var info=new TableLayoutPanel{Name="amzgConsultaPrecoResultado",
    Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,5,0,4),
    BackColor=Color.White,Padding=new Padding(5)};
   info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,61));
   info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));
   info.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var chosen=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};
   var selectedHeading=new Label{Text="PRODUTO SELECIONADO",Dock=DockStyle.Top,
    Height=31,ForeColor=Color.FromArgb(197,39,57),
    Font=new Font("Segoe UI",10.2f,FontStyle.Bold),
    Padding=new Padding(11,6,0,0)};
   descricao.Dock=DockStyle.Fill;descricao.Text="Selecione um produto na lista";
   descricao.Font=new Font("Segoe UI",13f,FontStyle.Bold);
   descricao.ForeColor=Color.FromArgb(20,60,129);
   descricao.TextAlign=ContentAlignment.MiddleLeft;
   descricao.Padding=new Padding(10,0,8,0);
   chosen.Controls.Add(descricao);chosen.Controls.Add(selectedHeading);
   info.Controls.Add(chosen,0,0);
   var priceCard=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(225,237,253)};
   var label=new Label{Dock=DockStyle.Top,Height=31,Text="PREÇO DE VENDA",
    Font=new Font("Segoe UI",10.2f,FontStyle.Bold),
    ForeColor=Color.FromArgb(23,75,155),
    TextAlign=ContentAlignment.MiddleCenter};
   preco.Dock=DockStyle.Fill;preco.Text="R$ —";
   preco.Font=new Font("Segoe UI",29f,FontStyle.Bold);
   preco.ForeColor=Color.FromArgb(190,31,50);
   preco.TextAlign=ContentAlignment.MiddleCenter;
   priceCard.Controls.Add(preco);priceCard.Controls.Add(label);
   info.Controls.Add(priceCard,1,0);layout.Controls.Add(info,0,2);

   layout.Controls.Add(new Label{Name="amzgConsultaPrecoResultsTitle",
    Text="  RESULTADOS DA PESQUISA",Dock=DockStyle.Fill,
    BackColor=Color.FromArgb(17,71,152),ForeColor=Color.White,
    Font=new Font("Segoe UI",10f,FontStyle.Bold),
    TextAlign=ContentAlignment.MiddleLeft},0,3);
   lista.Dock=DockStyle.Fill;lista.Margin=new Padding(0);
   lista.BorderStyle=BorderStyle.None;lista.Font=new Font("Segoe UI",10.7f);
   lista.BackColor=Color.White;
   layout.Controls.Add(lista,0,4);
   Controls.Add(layout);
   busca.TextChanged+=(s,e)=>{atraso.Stop();atraso.Start();};
   atraso.Tick+=(s,e)=>{atraso.Stop();Consultar();};
   busca.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){atraso.Stop();Consultar();e.SuppressKeyPress=true;}};
   lista.SelectedIndexChanged+=(s,e)=>Exibir();
   Shown+=(s,e)=>busca.Focus();
   FormClosed+=(s,e)=>atraso.Dispose();
  }
  void Consultar(){
   ids.Clear();lista.Items.Clear();string termo=busca.Text.Trim();
   if(termo.Length==0){descricao.Text="";preco.Text="";return;}
   if(!File.Exists(db)){descricao.Text="Banco não encontrado";preco.Text="";return;}
   try{
    using(var c=new SQLiteConnection("Data Source="+db+";Version=3;Read Only=True;")){
     c.Open();
     using(var cmd=new SQLiteCommand("SELECT id,name,price,TRIM(COALESCE(barcode,'')) FROM products WHERE TRIM(COALESCE(barcode,''))=@codigo OR name LIKE @termo ORDER BY CASE WHEN TRIM(COALESCE(barcode,''))=@codigo THEN 0 ELSE 1 END,name LIMIT 40",c)){
      cmd.Parameters.AddWithValue("@codigo",termo);
      cmd.Parameters.AddWithValue("@termo","%"+termo+"%");
      bool exato=false;
      using(var r=cmd.ExecuteReader()){
       while(r.Read()){
        ids.Add(Convert.ToInt64(r.GetValue(0)));
        string nome=r.IsDBNull(1)?"Sem descrição":Convert.ToString(r.GetValue(1));
        decimal valor=r.IsDBNull(2)?0m:Convert.ToDecimal(r.GetValue(2),CultureInfo.InvariantCulture);
        lista.Items.Add(nome+" — "+valor.ToString("C2",CultureInfo.GetCultureInfo("pt-BR")));
        if(ids.Count==1)exato=string.Equals(Convert.ToString(r.GetValue(3)),termo,StringComparison.Ordinal);
       }
      }
      if(ids.Count==0){descricao.Text="Produto não cadastrado";preco.Text="";}
      else if(ids.Count==1||exato)lista.SelectedIndex=0;
      else{descricao.Text="Selecione o produto";preco.Text="";}
     }
    }
   }catch(Exception ex){descricao.Text="Erro ao consultar";preco.Text="";MessageBox.Show(this,ex.Message,"Consulta de Preços");}
  }
  void Exibir(){
   int i=lista.SelectedIndex;if(i<0||i>=ids.Count)return;
   try{
    using(var c=new SQLiteConnection("Data Source="+db+";Version=3;Read Only=True;")){
     c.Open();using(var cmd=new SQLiteCommand("SELECT name,price FROM products WHERE id=@id",c)){
      cmd.Parameters.AddWithValue("@id",ids[i]);using(var r=cmd.ExecuteReader()){
       if(r.Read()){
        descricao.Text=r.IsDBNull(0)?"Sem descrição":Convert.ToString(r.GetValue(0));
        preco.Text=(r.IsDBNull(1)?0m:Convert.ToDecimal(r.GetValue(1),CultureInfo.InvariantCulture)).ToString("C2",CultureInfo.GetCultureInfo("pt-BR"));
       }
      }
     }
    }
   }catch(Exception ex){MessageBox.Show(this,ex.Message,"Consulta de Preços");}
  }
 }
}
