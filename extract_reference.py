import json, pathlib, openpyxl
from html.parser import HTMLParser
root=pathlib.Path(__file__).parent
w=openpyxl.load_workbook(r'C:/Users/unist/Downloads/슈퍼 마개조 계산기 배포용 Ver 3.617의 사본.xlsx',data_only=True)
mons=[]
for row in list(w['포켓몬 목록'].values)[4:]:
    if row[1] and all(isinstance(v,(int,float)) for v in row[12:18]):
        mons.append(dict(id=str(len(mons)),team=row[0] or '',name=row[1],level=row[2] or 100,types=[v for v in row[3:6] if v],ability=row[6] or '',item=row[10] or '',base=list(row[12:18]),iv=[v or 0 for v in row[19:25]],moves=[v for v in row[25:35] if isinstance(v,str)],potentials=[]))
moves=[]
for r in list(w['기술 목록'].values)[1:]:
    if not r[0] or r[1] not in ['물리','특수','변화']: continue
    moves.append(dict(name=r[0],category=r[1],types=[v for v in r[2:5] if v],power=r[5] if isinstance(r[5],(int,float)) else 0,accuracy=r[6] if isinstance(r[6],(int,float)) else 100,priority=int(str(r[8] or '0').replace('±','').replace('＋','+').replace('－','-')),effect=r[10] or '',attack=r[15],defense=r[16],tags=[v for v in r[11:15] if v]))
rows=list(w['상성표'].values)
types=list(rows[0][1:21]); chart={r[0]:dict(zip(types,r[1:21])) for r in rows[1:21] if r[0]}
class AAParser(HTMLParser):
    def __init__(self): super().__init__(); self.depth=0; self.arts=[]; self.buf=''
    def handle_starttag(self,tag,attrs):
        if self.depth:
            if tag=='br': self.buf+='\n'
            elif tag not in ['img','hr','input','meta','link']: self.depth+=1
        elif 'renderer__AA' in dict(attrs).get('class',''): self.depth=1; self.buf=''
    def handle_endtag(self,tag):
        if self.depth:
            self.depth-=1
            if not self.depth: self.arts.append(self.buf)
    def handle_startendtag(self,tag,attrs):
        if self.depth and tag=='br': self.buf+='\n'
    def handle_data(self,data):
        if self.depth: self.buf+=data
p=AAParser(); p.feed((root/'reference-page.html').read_text(encoding='utf-8'))
(root/'lib/reference-data.json').write_text(json.dumps(dict(pokemon=mons,moves=moves,types=types,chart=chart),ensure_ascii=False),encoding='utf-8')
(root/'lib/reference-aa.json').write_text(json.dumps(p.arts,ensure_ascii=False),encoding='utf-8')
print('Pokemon',len(mons),'moves',len(moves),'AA blocks',[(i,len(a),a.count('\n')) for i,a in enumerate(p.arts)])
