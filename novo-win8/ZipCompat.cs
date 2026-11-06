using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
namespace ArmazemGratidaoAtualizador {
 // ZIP32 de origem autenticada por SHA-256; usa apenas System.dll do .NET Framework.
 static class ZipCompat {
  internal static List<string> Extract(string archiveFile,string destination,Func<string,bool> allowed){
   var names=new List<string>();
   using(var stream=new FileStream(archiveFile,FileMode.Open,FileAccess.Read,FileShare.Read))
   using(var rd=new BinaryReader(stream,Encoding.UTF8)){
    int tailLength=(int)Math.Min(stream.Length,65557L);
    stream.Position=stream.Length-tailLength;
    byte[] tail=rd.ReadBytes(tailLength);
    int eocd=-1;
    for(int p=tailLength-22;p>=0;p--)
     if(tail[p]==80&&tail[p+1]==75&&tail[p+2]==5&&tail[p+3]==6){eocd=p;break;}
    if(eocd<0)throw new Exception("Indice ZIP nao encontrado.");
    int count=BitConverter.ToUInt16(tail,eocd+10);
    uint centralSize=BitConverter.ToUInt32(tail,eocd+12);
    uint centralOffset=BitConverter.ToUInt32(tail,eocd+16);
    if(count<1||count>64||(long)centralOffset+centralSize>stream.Length)
     throw new Exception("Indice ZIP invalido.");
    long next=centralOffset;
    for(int i=0;i<count;i++){
     stream.Position=next;
     if(rd.ReadUInt32()!=0x02014b50u)throw new Exception("Registro ZIP invalido.");
     rd.ReadUInt16();rd.ReadUInt16();
     ushort flags=rd.ReadUInt16(),method=rd.ReadUInt16();
     rd.ReadUInt16();rd.ReadUInt16();rd.ReadUInt32();
     uint compressed=rd.ReadUInt32(),uncompressed=rd.ReadUInt32();
     ushort nameLen=rd.ReadUInt16(),extraLen=rd.ReadUInt16(),commentLen=rd.ReadUInt16();
     rd.ReadUInt16();rd.ReadUInt16();rd.ReadUInt32();
     uint local=rd.ReadUInt32();
     if(nameLen<1||nameLen>260||compressed>67108864||uncompressed>134217728||
        (method!=0&&method!=8)||(flags&1)!=0)
      throw new Exception("ZIP invalido, arquivo muito grande ou criptografado.");
     byte[] nameBytes=rd.ReadBytes(nameLen);
     if(nameBytes.Length!=nameLen)throw new Exception("Nome ZIP incompleto.");
     string name=Encoding.UTF8.GetString(nameBytes).Replace('\\','/');
     next=stream.Position+extraLen+commentLen;
     if(name.EndsWith("/",StringComparison.Ordinal))continue;
     if(!allowed(name)||names.Any(x=>x.Equals(name,StringComparison.OrdinalIgnoreCase)))
      throw new Exception("Arquivo no ZIP nao autorizado ou duplicado: "+name);
     if(local>stream.Length-30)throw new Exception("Endereco ZIP fora do arquivo.");
     stream.Position=local;
     if(rd.ReadUInt32()!=0x04034b50u)throw new Exception("Registro ZIP local invalido.");
     rd.ReadUInt16();rd.ReadUInt16();
     if(rd.ReadUInt16()!=method)throw new Exception("Metodo ZIP inconsistente.");
     rd.ReadUInt16();rd.ReadUInt16();rd.ReadUInt32();
     rd.ReadUInt32();rd.ReadUInt32();
     int localName=rd.ReadUInt16(),localExtra=rd.ReadUInt16();
     long offset=stream.Position+localName+localExtra;
     if(offset+compressed>stream.Length)throw new Exception("ZIP truncado.");
     stream.Position=offset;
     byte[] payload=rd.ReadBytes((int)compressed);
     if(payload.Length!=(int)compressed)throw new Exception("Dados ZIP incompletos.");
     string target=Path.GetFullPath(Path.Combine(destination,name.Replace('/',Path.DirectorySeparatorChar)));
     string root=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
     if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Caminho ZIP inseguro.");
     Directory.CreateDirectory(Path.GetDirectoryName(target));
     using(var input=new MemoryStream(payload,false))
     using(Stream decoded=method==8?(Stream)new DeflateStream(input,CompressionMode.Decompress):(Stream)input)
     using(var output=new FileStream(target,FileMode.Create,FileAccess.Write,FileShare.None)){
      byte[] buffer=new byte[65536];int n;long written=0;
      while((n=decoded.Read(buffer,0,buffer.Length))>0){
       written+=n;
       if(written>uncompressed)throw new Exception("Tamanho ZIP invalido: "+name);
       output.Write(buffer,0,n);
      }
      if(written!=uncompressed)throw new Exception("Extracao incompleta: "+name);
     }
     names.Add(name);
    }
   }
   return names;
  }
 }
}