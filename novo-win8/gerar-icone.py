#!/usr/bin/env python3
# Dois arquivos ICO diferentes, sem bibliotecas externas:
# 1) Armazem-Gratidao.ico: carrinho azul/vermelho, EXE do PDV.
# 2) Instalador-Armazem-Gratidao.ico: caixa e seta, EXE de instalacao/reparo.
import struct
from pathlib import Path

BLUE=(27,79,151,255)
DARK=(19,42,77,255)
RED=(212,45,66,255)
WHITE=(251,252,255,255)
SIZES=(16,32,48,64,128,256)

def art(size,kind):
    n=size
    pix=[[(0,0,0,0) for _ in range(n)] for _ in range(n)]
    k=n/64.0
    def scale(p): return int(round(p*k))
    def dot(x,y,color):
        if 0<=x<n and 0<=y<n: pix[y][x]=color
    def rectangle(x0,y0,x1,y1,color):
        for y in range(max(0,scale(y0)),min(n,scale(y1)+1)):
            for x in range(max(0,scale(x0)),min(n,scale(x1)+1)):
                dot(x,y,color)
    def disk(cx,cy,r,color):
        X,Y,R=scale(cx),scale(cy),max(1,scale(r))
        for y in range(max(0,Y-R),min(n,Y+R+1)):
            for x in range(max(0,X-R),min(n,X+R+1)):
                if (x-X)**2+(y-Y)**2<=R*R:dot(x,y,color)
    def stroke(x1,y1,x2,y2,r,color):
        X1,Y1,X2,Y2=scale(x1),scale(y1),scale(x2),scale(y2)
        steps=max(abs(X2-X1),abs(Y2-Y1),1)*3
        for t in range(steps+1):
            x=round(X1+(X2-X1)*t/steps)
            y=round(Y1+(Y2-Y1)*t/steps)
            R=max(1,scale(r))
            for yy in range(max(0,y-R),min(n,y+R+1)):
                for xx in range(max(0,x-R),min(n,x+R+1)):
                    if (xx-x)**2+(yy-y)**2<=R*R:dot(xx,yy,color)
    # Silhuetas bem diferentes, preservando a marca azul/vermelho.
    for y in range(n):
        for x in range(n):
            baseX=x/k;baseY=y/k
            dx=max(6-baseX,0,baseX-57)
            dy=max(6-baseY,0,baseY-57)
            if dx*dx+dy*dy>36:continue
            if kind=="programa":
                dot(x,y,(max(20,36-int(baseY)//4),min(143,96+int(baseY)//3),min(227,188+int(baseY)//2),255))
            else:
                dot(x,y,(min(62,22+int(baseY)//4),min(91,45+int(baseY)//3),min(125,81+int(baseY)//2),255))
    if kind=="programa":
        stroke(9,17,55,17,3,RED)
        stroke(12,25,19,25,2,WHITE)
        stroke(19,25,25,43,2,WHITE)
        stroke(24,42,48,42,2,WHITE)
        stroke(23,29,51,29,2,WHITE)
        stroke(51,29,47,39,2,WHITE)
        stroke(47,39,24,39,2,WHITE)
        for x in (32,40):stroke(x,31,x,37,1,(236,58,73,255))
        for x in (29,45):
            disk(x,49,4,WHITE)
            disk(x,49,2,(202,28,48,255))
    else:
        # Caixa clara de instalacao com seta vermelha apontando para baixo.
        beige=(243,203,131,255)
        shade=(204,145,83,255)
        outline=(123,82,54,255)
        rectangle(13,28,51,48,outline)
        rectangle(15,30,49,46,beige)
        rectangle(15,30,21,46,shade)
        stroke(31,30,31,46,1,shade)
        stroke(13,28,23,22,2,beige)
        stroke(51,28,41,22,2,beige)
        stroke(23,22,41,22,2,beige)
        stroke(32,11,32,31,3,RED)
        stroke(24,25,32,34,3,RED)
        stroke(40,25,32,34,3,RED)
        stroke(15,52,49,52,2,WHITE)
    return pix

def encode_ico(kind):
    blocks=[]
    for size in SIZES:
        pixels=art(size,kind)
        bmp=b''.join(bytes((p[2],p[1],p[0],p[3])) for row in reversed(pixels) for p in row)
        # AND mask padded to 32-bit boundaries; alpha channel carries opacity.
        row_bytes=((size+31)//32)*4
        mask=b'\x00'*(row_bytes*size)
        header=struct.pack('<IiiHHIIiiII',40,size,size*2,1,32,0,len(bmp),0,0,0,0)
        blocks.append(header+bmp+mask)
    start=6+len(SIZES)*16
    entries=b''
    for size,blob in zip(SIZES,blocks):
        entries+=struct.pack('<BBBBHHII',0 if size==256 else size,
                             0 if size==256 else size,0,0,1,32,len(blob),start)
        start+=len(blob)
    return struct.pack('<HHH',0,1,len(SIZES))+entries+b''.join(blocks)

folder=Path(__file__).resolve().parent
targets=(('programa','Armazem-Gratidao.ico'),
         ('instalador','Instalador-Armazem-Gratidao.ico'))
for kind,name in targets:
    path=folder/name
    data=encode_ico(kind)
    path.write_bytes(data)
    print(kind,path,'bytes',len(data))
assert (folder/targets[0][1]).read_bytes()!=(folder/targets[1][1]).read_bytes()
