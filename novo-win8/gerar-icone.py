#!/usr/bin/env python3
"""Gera o icone azul/vermelho com carrinho (ICO 64x64), sem dependencias."""
import struct
from pathlib import Path

N=64
rgba=[[(0,0,0,0) for _ in range(N)] for _ in range(N)]
def px(x,y,c):
    if 0<=x<N and 0<=y<N: rgba[y][x]=c
def disk(cx,cy,r,c):
    for y in range(cy-r,cy+r+1):
        for x in range(cx-r,cx+r+1):
            if (x-cx)**2+(y-cy)**2<=r*r: px(x,y,c)
def stroke(x1,y1,x2,y2,r,c):
    steps=max(abs(x2-x1),abs(y2-y1),1)*3
    for k in range(steps+1):
        t=k/steps
        disk(round(x1+(x2-x1)*t),round(y1+(y2-y1)*t),r,c)
for y in range(N):
    for x in range(N):
        dx=max(6-x,0,x-57);dy=max(6-y,0,y-57)
        if dx*dx+dy*dy>36: continue
        px(x,y,(max(20,36-y//4),min(143,96+y//3),min(227,188+y//2),255))
stroke(9,17,55,17,3,(215,36,53,255))
stroke(12,25,19,25,2,(255,255,255,255))
stroke(19,25,25,43,2,(255,255,255,255))
stroke(24,42,48,42,2,(255,255,255,255))
stroke(23,29,51,29,2,(255,255,255,255))
stroke(51,29,47,39,2,(255,255,255,255))
stroke(47,39,24,39,2,(255,255,255,255))
for x in (32,40): stroke(x,31,x,37,1,(236,58,73,255))
for cx in (29,45):
    disk(cx,49,4,(250,250,250,255))
    disk(cx,49,2,(202,28,48,255))
raw=b''.join(bytes((b,g,r,a)) for row in reversed(rgba) for r,g,b,a in row)
mask=b''.join(bytes(8) for _ in range(N))
dib=struct.pack('<IiiHHIIiiII',40,N,N*2,1,32,0,len(raw)+len(mask),0,0,0,0)
payload=dib+raw+mask
ico=struct.pack('<HHH',0,1,1)+struct.pack('<BBBBHHII',N,N,0,0,1,32,len(payload),22)+payload
out=Path(__file__).resolve().parent/'Armazem-Gratidao.ico'
out.write_bytes(ico)
print('Icone:',out,'bytes',len(ico))
