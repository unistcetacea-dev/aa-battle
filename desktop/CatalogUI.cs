using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace AABattle {
 public class ResearchClause { public string text; }
 public class ResearchSource { public string source_page,source_url,character; public int line; }
 public class ResearchUses { public int count;public string per; }
 public class ResearchActivation { public string mode,probability,confidence; public ResearchUses uses; }
 public class ResearchPotential {
  public string id,name,category,raw; public ResearchClause[] triggers,effects;
  public ResearchActivation activation; public string[] needs_review; public ResearchSource[] sources;
 }
 public class ResearchCatalog { public ResearchPotential[] items; }
 public class DexPotential {
  public PotentialRecord record; public string source,review; public string[] urls;
  public string Search { get { return record.name+" "+record.slot+" "+record.trigger+" "+record.effect+" "+record.raw+" "+source; } }
 }
 public static class CatalogTests {
  static void Check(bool value,string message){if(!value)throw new Exception("Catalog test failed: "+message);}
  public static void Run(){
   string trigger,effect;PotentialLibrary.Split("자신의 기술의 위력을 강화(1.2배)한다.",out trigger,out effect);Check(trigger==""&&effect.StartsWith("자신의"),"do not split 자신의");
   PotentialLibrary.Split("선발로 필드에 나오면, 자신의 공격이 오른다.",out trigger,out effect);Check(trigger=="선발로 필드에 나오면"&&effect=="자신의 공격이 오른다.","entry condition");
   PotentialLibrary.Split("필드에 있는 한, 상대는 교대할 수 없다.",out trigger,out effect);Check(trigger=="필드에 있는 한"&&!effect.Contains("있는 한"),"continuous condition");
   PotentialLibrary.Split("「여기다！」일 때, 필드에 나오면 자신의 공격이 오른다.",out trigger,out effect);Check(trigger.Contains("여기다")&&trigger.Contains("필드에 나오면")&&effect=="자신의 공격이 오른다.","chained conditions");
   PotentialLibrary.Split("T종료시까지 자신의 공격이 오른다.",out trigger,out effect);Check(trigger==""&&effect.StartsWith("T종료시까지"),"duration remains effect");
   PotentialLibrary.Split("「가열」상태가 된 T 종료시, 아군과 교대한다.",out trigger,out effect);Check(trigger.EndsWith("종료시")&&effect=="아군과 교대한다.","compact turn timing");
   PotentialParts.Test();var data=PotentialLibrary.Load();Check(data.Count==4151,"all embedded definitions");Check(data.Any(x=>x.record.name=="선의 선"&&x.record.trigger.Length>0),"research triggers");Check(data.All(x=>x.source.Length>0&&x.urls.Length>0&&x.record.raw.Length>0),"provenance and raw");
   var order=EditorTemplate.BaseOrders().First();Check(order.name=="물러나！"&&order.trigger.Length>0&&order.effect.Contains("한 번"),"user directive semantics");
   var arbitrary=EditorTemplate.Parse("『검사』… 필드를 떠날 때, 아군의 임의의 능력치를 올린다.","역할");Check(arbitrary.effect.Contains("임의의")&&arbitrary.activation=="","preserve optional target wording");
  }
 }
 public static class PotentialLibrary {
  static List<DexPotential> cache;
  static bool SplitClause(string value,out string condition,out string remainder){
   condition=remainder="";value=value.Trim();
   if(Regex.IsMatch(value,@"^[0-9０-９]+\s*T\s*동안")||Regex.IsMatch(value,@"^T\s*종료\s*시\s*까지"))return false;
   var enteredTurn=Regex.Match(value,@"^(.+?교대해\s*필드에\s*나온)\s*T\s*종료\s*시\s*까지\s+(.+)$");
   if(enteredTurn.Success){condition=enteredTurn.Groups[1].Value.Trim()+" 때";remainder="T 종료시까지 "+enteredTurn.Groups[2].Value.Trim();return true;}
   var enteredSecond=Regex.Match(value,@"^(.+?교대해\s*필드에\s*나와\s*[0-9０-９]+T째)\s*[,，、]\s*(.+)$");
   if(enteredSecond.Success){condition=enteredSecond.Groups[1].Value.Trim();remainder=enteredSecond.Groups[2].Value.Trim();return true;}
   var switchedTurn=Regex.Match(value,@"^(.+?교대로?\s*(?:필드에\s*)?나온\s*T)\s*[,，、]\s*(.+)$");
   if(switchedTurn.Success){condition=switchedTurn.Groups[1].Value.Trim();remainder=switchedTurn.Groups[2].Value.Trim();return true;}
   var ending=@"(?:때(?:에)?도|때|일때|경우|한해서|라면|있으면|없으면|(?:으)?면(?:\([^)]*(?:으)?면\))?|나오면|있는\s*동안|있는\s*한|참가\s*시에?|발동\s*시에?|사용\s*시에?|발생\s*시에?|개시\s*시에?|종료\s*시에?|교대\s*시에?|명중\s*시에?|피격\s*시에?)";
   var match=Regex.Match(value,@"^(.+?"+ending+@")\s*(?:[,，、.]\s*|\s+)(.+)$");
   if(!match.Success)return false;condition=match.Groups[1].Value.Trim();remainder=match.Groups[2].Value.Trim();return remainder.Length>0;
  }
  static bool ConditionOnly(string value){return Regex.IsMatch(value.Trim().TrimEnd('.','。'),@"(?:때(?:에)?도|때|일때|경우|한해서|라면|있으면|없으면|(?:으)?면(?:\([^)]*(?:으)?면\))?|나오면|있는\s*동안|있는\s*한|참가\s*시에?|발동\s*시에?|사용\s*시에?|발생\s*시에?|개시\s*시에?|종료\s*시에?|교대\s*시에?|명중\s*시에?|피격\s*시에?)$");}
  // Extract chained conditions while preserving duration phrases such as 1T 동안 and T종료시까지.
  public static void Split(string body,out string trigger,out string effect) {
   var conditions=new List<string>();var results=new List<string>();
   var sentences=Regex.Split(body.Trim(),@"(?<=[.!?。])\s+").Where(x=>x.Length>0).ToArray();
   foreach(var original in sentences){
    string sentence=original,condition,remainder;
    while(SplitClause(sentence,out condition,out remainder)){conditions.Add(condition);sentence=remainder;}
    if(ConditionOnly(sentence)){conditions.Add(sentence.Trim().TrimEnd('.','。'));continue;}
    if(sentence.Length>0)results.Add(sentence);
   }
   trigger=string.Join("\r\n",conditions);effect=string.Join("\r\n",results);
  }
  public static List<DexPotential> Load(){
   if(cache!=null)return cache;
   var catalog=new JavaScriptSerializer{MaxJsonLength=20000000}.Deserialize<ResearchCatalog>(Resource.Text("potentials.json"));
   cache=new List<DexPotential>();
   foreach(var x in catalog.items){
    var parsed=EditorTemplate.Parse("『"+x.name+"』… "+x.raw,x.category??"미분류");
    // Derive parts from preserved raw text, not legacy metadata that matched 임의 inside 임의교대.
    parsed.raw=x.raw;
    cache.Add(new DexPotential{record=parsed,source=string.Join(" / ",(x.sources??new ResearchSource[0]).Select(s=>s.source_page+" : "+s.line).Distinct()),urls=(x.sources??new ResearchSource[0]).Select(s=>s.source_url).Distinct().ToArray(),review=(x.needs_review!=null&&x.needs_review.Length>0)||(x.activation!=null&&x.activation.confidence=="review")?"검토 필요 · 자동 분리 초안":"자동 분리 · 원문 대조 가능"});
   }
   return cache;
  }
 }

 // Pixel rail, arrow buttons and draggable thumb; no native Windows scrollbar.
 public class PixelScrollBar:Control {
  public bool Horizontal; public int Maximum,Page=1; int value,drag=-1;
  public event EventHandler ValueChanged;
  public int Value {get{return value;}set{int next=Math.Max(0,Math.Min(Maximum,value));if(this.value==next)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
  int Length {get{return Horizontal?Width:Height;}}
  int ThumbLength {get{return Math.Max(18,(int)((Length-44)*(double)Page/Math.Max(1,Maximum+Page)));}}
  int Travel {get{return Math.Max(1,Length-44-ThumbLength);}}
  Rectangle Thumb {get{int p=22+(int)(Travel*(double)value/Math.Max(1,Maximum));return Horizontal?new Rectangle(p,3,ThumbLength,Width>0?Height-6:16):new Rectangle(3,p,Width-6,ThumbLength);}}
  public PixelScrollBar(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);BackColor=Color.White;Width=24;Height=24;TabStop=true;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.Clear(Color.White);e.Graphics.DrawRectangle(Pens.Black,0,0,Width-1,Height-1);if(Length<48)return;int c=Horizontal?Height/2:Width/2;for(int i=0;i<5;i++){if(Horizontal){e.Graphics.FillRectangle(Brushes.Black,6+i,c-i,1,i*2+1);e.Graphics.FillRectangle(Brushes.Black,Width-7-i,c-i,1,i*2+1);}else{e.Graphics.FillRectangle(Brushes.Black,c-i,6+i,i*2+1,1);e.Graphics.FillRectangle(Brushes.Black,c-i,Height-7-i,i*2+1,1);}}if(Maximum>0){e.Graphics.FillRectangle(Brushes.Black,Thumb);var r=Thumb;r.Inflate(-4,-4);e.Graphics.DrawRectangle(Pens.White,r);}}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();int p=Horizontal?e.X:e.Y;if(p<22)Value--;else if(p>Length-22)Value++;else if(Thumb.Contains(e.Location)){drag=p-(Horizontal?Thumb.X:Thumb.Y);Capture=true;}else Value+=p<(Horizontal?Thumb.X:Thumb.Y)?-Page:Page;}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(drag>=0)Value=(int)Math.Round(((Horizontal?e.X:e.Y)-22-drag)*(double)Maximum/Travel);}
  protected override void OnMouseUp(MouseEventArgs e){drag=-1;Capture=false;base.OnMouseUp(e);}
  protected override void OnMouseWheel(MouseEventArgs e){Value-=Math.Sign(e.Delta)*3;base.OnMouseWheel(e);}
  protected override bool IsInputKey(Keys keyData){return true;}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=Maximum;else if(e.KeyCode==Keys.PageDown)Value+=Page;else if(e.KeyCode==Keys.PageUp)Value-=Page;else if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Right)Value++;else if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Left)Value--;base.OnKeyDown(e);}
 }
 public class PixelGridHost:Panel {
  readonly DataGridView grid;readonly PixelScrollBar vertical,horizontal;bool syncing;
  public PixelGridHost(DataGridView value){grid=value;Dock=DockStyle.Fill;grid.Dock=DockStyle.Fill;grid.ScrollBars=ScrollBars.None;vertical=new PixelScrollBar{Dock=DockStyle.Right};horizontal=new PixelScrollBar{Dock=DockStyle.Bottom,Horizontal=true};Controls.Add(grid);Controls.Add(vertical);Controls.Add(horizontal);grid.BringToFront();vertical.ValueChanged+=(s,e)=>{if(!syncing&&grid.Rows.Count>0)grid.FirstDisplayedScrollingRowIndex=Math.Min(vertical.Value,grid.Rows.Count-1);};horizontal.ValueChanged+=(s,e)=>{if(!syncing)grid.HorizontalScrollingOffset=horizontal.Value;};grid.Scroll+=(s,e)=>Sync();grid.Resize+=(s,e)=>Sync();grid.RowsAdded+=(s,e)=>Sync();grid.RowsRemoved+=(s,e)=>Sync();grid.ColumnWidthChanged+=(s,e)=>Sync();grid.DataBindingComplete+=(s,e)=>Sync();}
  public void Sync(){if(syncing||grid.IsDisposed)return;syncing=true;try{int page=Math.Max(1,grid.DisplayedRowCount(false));vertical.Maximum=Math.Max(0,grid.RowCount-page);vertical.Page=page;vertical.Value=Math.Max(0,grid.FirstDisplayedScrollingRowIndex);horizontal.Maximum=Math.Max(0,grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible)-grid.ClientSize.Width+(grid.RowHeadersVisible?grid.RowHeadersWidth:0));horizontal.Page=Math.Max(1,grid.ClientSize.Width);horizontal.Value=grid.HorizontalScrollingOffset;vertical.Invalidate();horizontal.Invalidate();}finally{syncing=false;}}
 }
 // The supplied OTF needs GDI+ drawing; native GDI TextRenderer corrupts its glyphs.
 public class PixelDexGrid:DataGridView {
  protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e){
   if(e.ColumnIndex<0){base.OnCellPainting(e);return;}
   bool selected=e.RowIndex<0||(e.State&DataGridViewElementStates.Selected)!=0;
   e.Graphics.FillRectangle(selected?Brushes.Black:Brushes.White,e.CellBounds);
   e.Graphics.DrawRectangle(Pens.DarkGray,e.CellBounds.X,e.CellBounds.Y,e.CellBounds.Width-1,e.CellBounds.Height-1);
   var state=e.Graphics.Save();e.Graphics.SetClip(e.CellBounds);e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
   using(var format=new StringFormat{LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter}){
    if(e.RowIndex<0||e.CellStyle.WrapMode!=DataGridViewTriState.True)format.FormatFlags=StringFormatFlags.NoWrap;
    e.Graphics.DrawString(Convert.ToString(e.FormattedValue),e.CellStyle.Font??Font,selected?Brushes.White:Brushes.Black,new RectangleF(e.CellBounds.X+6,e.CellBounds.Y+3,e.CellBounds.Width-12,e.CellBounds.Height-6),format);
   }
   if(e.RowIndex>=0&&Columns[e.ColumnIndex] is DataGridViewComboBoxColumn){int x=e.CellBounds.Right-16,y=e.CellBounds.Y+e.CellBounds.Height/2-2;using(var brush=new SolidBrush(selected?Color.White:Color.Black))e.Graphics.FillPolygon(brush,new[]{new Point(x,y),new Point(x+8,y),new Point(x+4,y+5)});}
   e.Graphics.Restore(state);e.Handled=true;
  }
 }
 public class PixelDisableColumn:DataGridViewCheckBoxColumn {
  public PixelDisableColumn(){CellTemplate=new PixelDisableCell();FalseValue=false;TrueValue=true;IndeterminateValue=false;}
 }
 public class PixelDisableCell:DataGridViewCheckBoxCell {
  public override object Clone(){return base.Clone();}
  protected override void Paint(Graphics g,Rectangle clip,Rectangle bounds,int rowIndex,DataGridViewElementStates state,object value,object formattedValue,string error,DataGridViewCellStyle style,DataGridViewAdvancedBorderStyle border,DataGridViewPaintParts parts){
   bool selected=(state&DataGridViewElementStates.Selected)!=0,blocked=value!=null&&Convert.ToBoolean(value);g.FillRectangle(selected?Brushes.Black:Brushes.White,bounds);g.DrawRectangle(Pens.DarkGray,bounds.X,bounds.Y,bounds.Width-1,bounds.Height-1);
   int size=18,x=bounds.X+(bounds.Width-size)/2,y=bounds.Y+(bounds.Height-size)/2;g.FillRectangle(blocked?(selected?Brushes.White:Brushes.Black):(selected?Brushes.Black:Brushes.White),x,y,size,size);using(var pen=new Pen(selected?Color.White:Color.Black,2))g.DrawRectangle(pen,x,y,size,size);
   if(blocked)using(var pen=new Pen(selected?Color.Black:Color.White,3)){g.DrawLine(pen,x+4,y+4,x+size-4,y+size-4);g.DrawLine(pen,x+size-4,y+4,x+4,y+size-4);}
  }
 }
 public class PixelSearchBox:UserControl {
  readonly TextBox editor;readonly PixelLabel display;
  public override string Text {get{return editor==null?"":editor.Text;}set{if(editor!=null)editor.Text=value??"";}}
  public PixelSearchBox(){Height=34;Dock=DockStyle.Top;BackColor=Color.White;BorderStyle=BorderStyle.FixedSingle;Font=Theme.UI(8);
   editor=new TextBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=new Font("맑은 고딕",10)};
   display=new PixelLabel{Dock=DockStyle.Fill,Font=Theme.UI(8),Text="검색어 입력...",BackColor=Color.White};Controls.Add(editor);Controls.Add(display);display.BringToFront();
   display.Click+=(s,e)=>{display.Visible=false;editor.Focus();};editor.Enter+=(s,e)=>display.Visible=false;
   editor.Leave+=(s,e)=>{display.Visible=true;display.BringToFront();};editor.TextChanged+=(s,e)=>{display.Text=editor.Text.Length>0?editor.Text:"검색어 입력...";OnTextChanged(EventArgs.Empty);};
  }
 }
 // Wrapped detail text scrolls with the same pixel rail and mouse wheel.
 public class PixelText:Control {
  readonly PixelScrollBar bar;int offset;string content="";
  public override string Text {get{return content;}set{content=value??"";offset=0;if(bar!=null){bar.Value=0;Measure();}Invalidate();}}
  public PixelText(){DoubleBuffered=true;BackColor=Color.White;Font=Theme.UI(10);bar=new PixelScrollBar{Dock=DockStyle.Right};Controls.Add(bar);bar.ValueChanged+=(s,e)=>{offset=bar.Value;Invalidate();};}
  void Measure(){if(Width<30)return;int h;using(var g=CreateGraphics())h=(int)Math.Ceiling(g.MeasureString(content,Font,Math.Max(1,Width-44)).Height);bar.Maximum=Math.Max(0,h-Height+24);bar.Page=Math.Max(1,Height-24);bar.Value=bar.Value;bar.Invalidate();}
  protected override void OnResize(EventArgs e){base.OnResize(e);Measure();}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.DrawRectangle(Pens.Black,0,0,Width-1,Height-1);var state=e.Graphics.Save();e.Graphics.SetClip(new Rectangle(8,8,Math.Max(1,Width-40),Math.Max(1,Height-16)));e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;e.Graphics.DrawString(content,Font,Brushes.Black,new RectangleF(10,10-offset,Math.Max(1,Width-44),Math.Max(Height,bar.Maximum+Height)));e.Graphics.Restore(state);}
  protected override void OnMouseWheel(MouseEventArgs e){bar.Value-=Math.Sign(e.Delta)*48;base.OnMouseWheel(e);}
 }

 partial class DataEditorForm {
  DataGridView moveDex,triggerDex,effectDex;PixelSearchBox dexMoveSearch,triggerPartSearch,effectPartSearch;PixelText dexMoveDetail,triggerPartDetail,effectPartDetail;
  Label moveCount,triggerPartCount,effectPartCount;PotentialParts partCatalog;
  DataGridView DexGrid(){return new PixelDexGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersHeight=34,RowTemplate={Height=36},DefaultCellStyle=new DataGridViewCellStyle{Font=Theme.UI(10),SelectionBackColor=Color.Black,SelectionForeColor=Color.White},ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.Black,ForeColor=Color.White,Font=Theme.UI(8)}};}
  void DexColumn(DataGridView grid,string title,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=title,Width=width,MinimumWidth=width,AutoSizeMode=title.StartsWith("효과")?DataGridViewAutoSizeColumnMode.Fill:DataGridViewAutoSizeColumnMode.None,SortMode=DataGridViewColumnSortMode.NotSortable});}
  void BuildCatalogTab(){
   var page=Page("기술 도감");var header=new Panel{Dock=DockStyle.Top,Height=78};dexMoveSearch=new PixelSearchBox();dexMoveSearch.Font=Theme.UI(8);dexMoveSearch.Dock=DockStyle.Bottom;moveCount=Theme.Label("기술 도감",36);header.Controls.Add(dexMoveSearch);header.Controls.Add(moveCount);
   moveDex=DexGrid();foreach(var col in new[]{new[]{"기술", "155"},new[]{"타입","100"},new[]{"분류","65"},new[]{"위력","60"},new[]{"명중","60"},new[]{"우선도","70"},new[]{"효과 요약","340"}})DexColumn(moveDex,col[0],int.Parse(col[1]));
   dexMoveDetail=new PixelText{Dock=DockStyle.Bottom,Height=175};var add=Button("선택 기술 추가",150,AddDexMove);add.Dock=DockStyle.Bottom;page.Controls.Add(new PixelGridHost(moveDex));page.Controls.Add(dexMoveDetail);page.Controls.Add(add);page.Controls.Add(header);page.Controls[0].BringToFront();
   dexMoveSearch.TextChanged+=(s,e)=>RefreshMoveDex();moveDex.SelectionChanged+=(s,e)=>ShowDexMove();moveDex.CurrentCellChanged+=(s,e)=>ShowDexMove();moveDex.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)AddDexMove();};RefreshMoveDex();
   page=Page("포텐셜 도감");
   var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   columns.Controls.Add(BuildPartPanel(true),0,0);columns.Controls.Add(BuildPartPanel(false),1,0);
   var compose=Button("선택한 조건 + 효과로 포텐셜 만들기",360,ComposePotential);compose.Dock=DockStyle.Bottom;
   page.Controls.Add(columns);page.Controls.Add(compose);columns.BringToFront();RefreshPotentialDex();
  }
  Panel BuildPartPanel(bool trigger){
   var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(4)};
   var grid=DexGrid();grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;grid.RowTemplate.Height=62;grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=trigger?"트리거 조건":"효과",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=160,SortMode=DataGridViewColumnSortMode.NotSortable});
   var header=Theme.Label("",32);var search=new PixelSearchBox();search.Font=Theme.UI(8);search.Dock=DockStyle.Top;
   var detail=new PixelText{Dock=DockStyle.Bottom,Height=155};var apply=Button(trigger?"선택한 조건 입력":"선택한 효과 입력",220,()=>ApplyPart(trigger));apply.Dock=DockStyle.Bottom;
   panel.Controls.Add(new PixelGridHost(grid));panel.Controls.Add(detail);panel.Controls.Add(apply);panel.Controls.Add(search);panel.Controls.Add(header);panel.Controls[0].BringToFront();
   if(trigger){triggerDex=grid;triggerPartSearch=search;triggerPartCount=header;triggerPartDetail=detail;}else{effectDex=grid;effectPartSearch=search;effectPartCount=header;effectPartDetail=detail;}
   search.TextChanged+=(o,e)=>FilterParts(trigger);grid.CurrentCellChanged+=(o,e)=>ShowPart(trigger);grid.CellDoubleClick+=(o,e)=>{if(e.RowIndex>=0)ApplyPart(trigger);};return panel;
  }
  void RefreshMoveDex(){string q=dexMoveSearch.Text.Trim();moveDex.Rows.Clear();foreach(var m in catalogDb.moves.Where(x=>(x.name+" "+string.Join(" ",x.types??new string[0])+" "+x.category+" "+x.effect).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(x=>x.name)){int i=moveDex.Rows.Add(m.name,string.Join("/",m.types??new string[0]),m.category,m.power,m.accuracy,m.priority,m.effect);moveDex.Rows[i].Tag=m;}if(moveDex.RowCount>0)moveDex.CurrentCell=moveDex.Rows[0].Cells[0];moveCount.Text="기술 도감 · "+moveDex.RowCount+"개 · 이름 / 타입 / 효과 검색";ShowDexMove();}
  Move SelectedDexMove(){return moveDex.CurrentRow==null?null:moveDex.CurrentRow.Tag as Move;}
  void ShowDexMove(){var m=SelectedDexMove();dexMoveDetail.Text=m==null?"검색 결과가 없습니다.":"【"+m.name+"】  "+string.Join(" / ",m.types??new string[0])+" · "+m.category+"\r\n위력 "+m.power+"   명중 "+m.accuracy+"   우선도 "+m.priority+"\r\n판정 "+m.attack+" / "+m.defense+"   태그 "+string.Join(", ",m.tags??new string[0])+"\r\n효과: "+m.effect;}
  void AddDexMove(){var m=SelectedDexMove();if(m==null)return;if(!(Current is PokemonRecord)){MessageBox.Show(this,"포켓몬을 선택한 뒤 기술을 추가하세요.");return;}var names=pMoves.Lines.Where(x=>x.Trim().Length>0).Select(x=>x.Trim()).ToList();if(names.Contains(m.name))return;if(names.Count>=4){MessageBox.Show(this,"기술은 4개까지 넣을 수 있습니다.");return;}names.Add(m.name);pMoves.Text=string.Join("\r\n",names);}
  void RefreshPotentialDex(){
   if(triggerDex==null||effectDex==null)return;
   var local=ProjectPotentials().Select(p=>new DexPotential{record=p,source="내 데이터",urls=new string[0]});
   partCatalog=PotentialParts.Build(PotentialParts.StandardEntries().Concat(local));FilterParts(true);FilterParts(false);
  }
  PotentialPart SelectedPart(bool trigger){var grid=trigger?triggerDex:effectDex;return grid==null||grid.CurrentRow==null?null:grid.CurrentRow.Tag as PotentialPart;}
  void FilterParts(bool trigger){
   if(partCatalog==null)return;var grid=trigger?triggerDex:effectDex;var previous=SelectedPart(trigger);string q=PotentialParts.Key((trigger?triggerPartSearch:effectPartSearch).Text);
   grid.Rows.Clear();foreach(var part in (trigger?partCatalog.triggers:partCatalog.effects).Where(x=>PotentialParts.Key(x.Display).Contains(q))){int i=grid.Rows.Add(part.Display);grid.Rows[i].Tag=part;}
   if(grid.RowCount>0){var row=previous==null?null:grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>((PotentialPart)r.Tag).id==previous.id);grid.CurrentCell=(row??grid.Rows[0]).Cells[0];}
   (trigger?triggerPartCount:effectPartCount).Text=(trigger?"트리거 조건":"효과")+" · "+grid.RowCount+"개";ShowPart(trigger);
  }
  void ShowPart(bool trigger){var p=SelectedPart(trigger);(trigger?triggerPartDetail:effectPartDetail).Text=p==null?"검색 결과가 없습니다.":p.Display;}
  void ApplyPart(bool trigger){
   var p=SelectedPart(trigger);var row=SelectedPotentialRow();if(p==null)return;if(row==null){MessageBox.Show(this,"포텐셜 편집에서 입력할 행을 먼저 선택하세요.");return;}
   loading=true;row.Cells[trigger?"trigger":"effect"].Value=p.text;
   if(trigger){var record=row.Tag as PotentialRecord??new PotentialRecord();record.activation=p.activation;record.uses=p.uses;row.Tag=record;}
   loading=false;GridChanged();
  }
  void ComposePotential(){var trigger=SelectedPart(true);var effect=SelectedPart(false);if(trigger==null||effect==null){MessageBox.Show(this,"조건과 효과를 각각 선택하세요.");return;}AddPotential(new PotentialRecord{name="새 포텐셜",slot=Current is TrainerRecord?"고유":"종족 ①",activation=trigger.activation,uses=trigger.uses,trigger=trigger.text,effect=effect.text,raw=""});}
  public void TestCatalogUi(){
   if(tabs.TabPages[4].Text!="기술 도감"||tabs.TabPages[5].Text!="포텐셜 도감"||triggerDex.ColumnCount!=1||effectDex.ColumnCount!=1)throw new Exception("Separate component UI missing");
   if(potentials.Columns.Contains("targetType")||potentials.Columns.Contains("activation")||potentials.Columns.Contains("uses")||!potentials.Columns.Contains("disabled")||!(potentials.Columns["disabled"] is PixelDisableColumn))throw new Exception("Potential editor columns or pixel toggle invalid");
   dexMoveSearch.Text="비바라기";if(moveDex.Rows.Count==0)throw new Exception("Move search failed");moveDex.CurrentCell=moveDex.Rows[0].Cells[0];ShowDexMove();if(!dexMoveDetail.Text.Contains("비바라기"))throw new Exception("Move details missing");
   triggerPartSearch.Text="필드에 나왔을 때";effectPartSearch.Text="회복";if(triggerDex.RowCount==0||effectDex.RowCount==0)throw new Exception("Part search failed");
   var row=potentials.Rows.Cast<DataGridViewRow>().First(r=>!r.IsNewRow&&Cell(r,"name")=="샘플의 전도");potentials.CurrentCell=row.Cells["effect"];string before=Cell(row,"raw");ApplyPart(true);string condition=Cell(row,"trigger");ApplyPart(false);if(condition.Length==0||Cell(row,"trigger")!=condition||Cell(row,"raw")!=before)throw new Exception("Independent part apply corrupted data");
   row.Cells["disabled"].Value=true;SaveCurrent();if(CurrentPotentials().First(x=>x.name=="샘플의 전도").enabled)throw new Exception("Never activate toggle failed");
   var selected=SelectedPart(false).id;RefreshPotentialDex();if(SelectedPart(false).id!=selected)throw new Exception("Part refresh lost selection");
   if(triggerDex.DefaultCellStyle.Font.FontFamily.Name!=Theme.UI(10).FontFamily.Name||dexMoveDetail.Font.FontFamily.Name!=Theme.UI(10).FontFamily.Name)throw new Exception("Catalog font missing");
   var rail=new PixelScrollBar{Maximum=10,Page=3};rail.Value=100;if(rail.Value!=10)throw new Exception("Scroll upper bound");rail.Value=-10;if(rail.Value!=0)throw new Exception("Scroll lower bound");rail.Dispose();
   potentials.HorizontalScrollingOffset=0;if(potentials.RowCount>0)potentials.FirstDisplayedScrollingRowIndex=0;
  }
 }
}
