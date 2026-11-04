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
   db=database;Text="Armazém Gratidão | Consulta de Preços";Size=new Size(680,560);BackColor=Color.FromArgb(242,247,255);
   var area=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16)};
   area.Controls.Add(lista);area.Controls.Add(preco);area.Controls.Add(descricao);area.Controls.Add(busca);
   area.Controls.Add(new Label{Dock=DockStyle.Top,Height=32,Text="Bipe o código de barras ou digite a descrição:",Font=new Font("Segoe UI",11)});
   area.Controls.Add(new Label{Dock=DockStyle.Top,Height=42,Text="CONSULTA DE PREÇOS",TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Color.FromArgb(180,35,50)});
   Controls.Add(area);
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
