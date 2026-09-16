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
   AbilityCatalog.Test();AbilityRuntime.Test();string trigger,effect;PotentialLibrary.Split("자신의 기술의 위력을 강화(1.2배)한다.",out trigger,out effect);Check(trigger==""&&effect.StartsWith("자신의"),"do not split 자신의");
   PotentialLibrary.Split("선발로 필드에 나오면, 자신의 공격이 오른다.",out trigger,out effect);Check(trigger=="선발로 필드에 나오면"&&effect=="자신의 공격이 오른다.","entry condition");
   PotentialLibrary.Split("필드에 있는 한, 상대는 교대할 수 없다.",out trigger,out effect);Check(trigger=="필드에 있는 한"&&!effect.Contains("있는 한"),"continuous condition");
   PotentialLibrary.Split("「여기다！」일 때, 필드에 나오면 자신의 공격이 오른다.",out trigger,out effect);Check(trigger.Contains("여기다")&&trigger.Contains("필드에 나오면")&&effect=="자신의 공격이 오른다.","chained conditions");
   PotentialLibrary.Split("T종료시까지 자신의 공격이 오른다.",out trigger,out effect);Check(trigger==""&&effect.StartsWith("T종료시까지"),"duration remains effect");
   PotentialLibrary.Split("「가열」상태가 된 T 종료시, 아군과 교대한다.",out trigger,out effect);Check(trigger.EndsWith("종료시")&&effect=="아군과 교대한다.","compact turn timing");
   PotentialParts.Test();EffectStructures.Test();EffectImplementationDex.Test();var data=PotentialLibrary.Load();Check(data.Count==4151,"all embedded definitions");Check(data.Any(x=>x.record.name=="선의 선"&&x.record.trigger.Length>0),"research triggers");Check(data.All(x=>x.source.Length>0&&x.urls.Length>0&&x.record.raw.Length>0),"provenance and raw");
   var order=EditorTemplate.BaseOrders().First();Check(order.name=="물러나！"&&order.trigger.Length>0&&order.effect.Contains("한 번"),"user directive semantics");var explorer=EditorTemplate.Role("탐사대원");Check(explorer!=null&&explorer.template!=null&&explorer.template.trigger==TriggerStructures.EntryLabel&&explorer.template.effect==EffectNormalizer.CanonicalRank("자신의 임의의 능력",1),"structured explorer role template");
   var arbitrary=EditorTemplate.Parse("『검사』… 필드를 떠날 때, 아군의 임의의 능력을 올린다.","역할");Check(arbitrary.effect.Contains("임의의 능력")&&arbitrary.activation=="","preserve optional target wording");
   Check(EditorTemplate.FixedOptions("특권").Length==21&&EditorTemplate.FixedOption("특권","계약의 특권").effect.Contains("○")&&EditorTemplate.FixedOption("특권","익스펜션（불꽃）").effect.Contains("\r\n"),"fixed privilege and expansion templates");string expansionTrigger,expansionEffects;PotentialLibrary.Split(EditorTemplate.FixedOption("특권","익스펜션（불꽃）").effect,out expansionTrigger,out expansionEffects);Check(expansionTrigger.Length==0&&expansionEffects.Split('\n').Length==2,"expansion trigger and multiple effects split");Check(StateDex.StatusNames("상태이상").Contains("화상")&&StateDex.StatusNames("상태변화").Contains("혼란")&&StateDex.StatusNames("상태변화").Contains("미래예지")&&StateDex.StatusNames("상태변화").Contains("번개구름")&&StateDex.StatusNames("진영 상태").Contains("흙놀이"),"status dex trigger choices");Check(TriggerStructures.Normalize("턴 중 선언 시")=="","ambiguous turn declaration excluded");
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
   var rules=RuleText.Split(body);trigger=string.Join("\r\n",rules.triggers);effect=string.Join("\r\n",rules.effects);
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
  DataGridView moveDex,abilityDex;PixelSearchBox dexMoveSearch,abilitySearch;PixelText dexMoveDetail,abilityDetail,triggerPreview;ComboBox triggerKind,triggerSubject,triggerEntryMethod,triggerType,triggerAptitude,triggerRank,triggerActivation,triggerMove,effectSubject;TextBox triggerGranted;FlatButton triggerStatusButton;string[] triggerStatusNames=new string[0];TableLayoutPanel triggerTable;
  Label moveCount,abilityCount;
  DataGridView DexGrid(){return new PixelDexGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersHeight=34,RowTemplate={Height=36},DefaultCellStyle=new DataGridViewCellStyle{Font=Theme.UI(10),SelectionBackColor=Color.Black,SelectionForeColor=Color.White},ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.Black,ForeColor=Color.White,Font=Theme.UI(8)}};}
  void DexColumn(DataGridView grid,string title,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=title,Width=width,MinimumWidth=width,AutoSizeMode=title.StartsWith("효과")?DataGridViewAutoSizeColumnMode.Fill:DataGridViewAutoSizeColumnMode.None,SortMode=DataGridViewColumnSortMode.NotSortable});}
  void BuildCatalogTab(){
   var page=Page("기술 도감");var header=new Panel{Dock=DockStyle.Top,Height=78};dexMoveSearch=new PixelSearchBox();dexMoveSearch.Font=Theme.UI(8);dexMoveSearch.Dock=DockStyle.Bottom;moveCount=Theme.Label("기술 도감",36);header.Controls.Add(dexMoveSearch);header.Controls.Add(moveCount);
   moveDex=DexGrid();foreach(var col in new[]{new[]{"기술", "125"},new[]{"타입","70"},new[]{"분류","60"},new[]{"위력","70"},new[]{"명중","70"},new[]{"대상","70"},new[]{"접촉","60"},new[]{"우선도","85"},new[]{"태그","160"},new[]{"효과 요약","220"}})DexColumn(moveDex,col[0],int.Parse(col[1]));
   dexMoveDetail=new PixelText{Dock=DockStyle.Bottom,Height=175};var add=Button("선택 기술 추가",150,AddDexMove);add.Dock=DockStyle.Bottom;page.Controls.Add(new PixelGridHost(moveDex));page.Controls.Add(dexMoveDetail);page.Controls.Add(add);page.Controls.Add(header);page.Controls[0].BringToFront();
   dexMoveSearch.TextChanged+=(s,e)=>RefreshMoveDex();moveDex.SelectionChanged+=(s,e)=>ShowDexMove();moveDex.CurrentCellChanged+=(s,e)=>ShowDexMove();moveDex.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)AddDexMove();};RefreshMoveDex();
   page=Page("포텐셜 도감");
   var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   columns.Controls.Add(BuildPartPanel(true),0,0);columns.Controls.Add(BuildPartPanel(false),1,0);
   var compose=Button("선택한 조건 + 효과로 포텐셜 만들기",360,ComposePotential);compose.Dock=DockStyle.Bottom;
   page.Controls.Add(columns);page.Controls.Add(compose);columns.BringToFront();RefreshPotentialDex();
   page=Page("특성 도감");var abilityHeader=new Panel{Dock=DockStyle.Top,Height=78};abilitySearch=new PixelSearchBox{Dock=DockStyle.Bottom,Font=Theme.UI(8)};abilityCount=Theme.Label("특성 도감",36);abilityHeader.Controls.Add(abilitySearch);abilityHeader.Controls.Add(abilityCount);abilityDex=DexGrid();DexColumn(abilityDex,"특성",130);DexColumn(abilityDex,"트리거",180);DexColumn(abilityDex,"효과",450);DexColumn(abilityDex,"구현",80);abilityDetail=new PixelText{Dock=DockStyle.Bottom,Height=175};var useAbility=Button("선택 특성을 포켓몬에 설정",240,AddDexAbility);useAbility.Dock=DockStyle.Bottom;page.Controls.Add(new PixelGridHost(abilityDex));page.Controls.Add(abilityDetail);page.Controls.Add(useAbility);page.Controls.Add(abilityHeader);page.Controls[0].BringToFront();abilitySearch.TextChanged+=(s,e)=>RefreshAbilityDex();abilityDex.SelectionChanged+=(s,e)=>ShowDexAbility();abilityDex.CurrentCellChanged+=(s,e)=>ShowDexAbility();abilityDex.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)AddDexAbility();};RefreshAbilityDex();
  }
  Panel BuildPartPanel(bool trigger){
   return trigger?BuildStructuredTriggerPanel():BuildStructuredEffectPanel();
  }
  Panel BuildStructuredTriggerPanel(){var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(10)};var header=Theme.Label("트리거 구조 · 원문 없이 값으로 구성",40);var help=Theme.Label("구조를 고르면 그 조건에 필요한 값만 표시합니다.",58);help.ForeColor=Theme.Muted;triggerKind=Theme.Combo(new[]{"상시 (X)",EntryTriggers.Label,DefeatTriggers.Label,TriggerStructures.ObserverLabel,TriggerStructures.TurnEndLabel,TriggerStructures.FieldLabel,TriggerStructures.SwitchLabel,TriggerStructures.ReceivedLabel,TriggerStructures.LeaveLabel,"등장 방법 지정","적진 타입 포켓몬","트레이너 자질 등급",TriggerStructures.ReceivedOrNullifiedLabel},"상시 (X)");triggerEntryMethod=Theme.Combo(new[]{"선발","죽어내밀기","임의교대","선발 또는 죽어내밀기"},"선발");triggerType=Theme.Combo(catalogDb.types,"불꽃");triggerAptitude=Theme.Combo(new[]{"지시","육성","통솔","능력"},"통솔");triggerRank=Theme.Combo(new[]{"E","D","C","B","A","AA","AAA","S"},"A");triggerMove=Theme.Combo(catalogDb.moves.Select(x=>x.name).OrderBy(x=>x).ToArray(),catalogDb.moves.First().name);triggerGranted=new TextBox{Dock=DockStyle.Fill,Font=new Font("맑은 고딕",10),Text="계약의 특권"};triggerStatusButton=new FlatButton{Dock=DockStyle.Fill,Font=Theme.UI(8),Text="상태 선택 · 검색/토글"};triggerStatusButton.Click+=(s,e)=>PickTriggerStatuses();triggerPreview=new PixelText{Dock=DockStyle.Fill};triggerTable=Theme.Table();Theme.Row(triggerTable,"구조",triggerKind);Theme.Row(triggerTable,"등장 방법",triggerEntryMethod);Theme.Row(triggerTable,"타입",triggerType);Theme.Row(triggerTable,"기술",triggerMove);Theme.Row(triggerTable,"부여된 이름",triggerGranted);Theme.Row(triggerTable,"상태 이름",triggerStatusButton);Theme.Row(triggerTable,"트레이너 자질",triggerAptitude);Theme.Row(triggerTable,"자질 등급",triggerRank);Theme.Row(triggerTable,"결과",triggerPreview);var apply=Button("구조 트리거 입력",220,()=>ApplyPart(true));apply.Dock=DockStyle.Bottom;panel.Controls.Add(triggerTable);panel.Controls.Add(help);panel.Controls.Add(header);panel.Controls.Add(apply);EventHandler refresh=(s,e)=>RefreshStructuredTrigger();triggerKind.SelectedIndexChanged+=refresh;triggerEntryMethod.SelectedIndexChanged+=refresh;triggerType.SelectedIndexChanged+=refresh;triggerMove.SelectedIndexChanged+=refresh;triggerGranted.TextChanged+=refresh;triggerAptitude.SelectedIndexChanged+=refresh;triggerRank.SelectedIndexChanged+=refresh;RefreshStructuredTrigger();return panel;}
  void ShowTriggerRow(Control control,bool shown,string labelText=null){if(triggerTable==null||control==null)return;int row=triggerTable.GetRow(control);triggerTable.RowStyles[row].Height=shown?42:0;control.Visible=shown;var label=triggerTable.GetControlFromPosition(0,row);if(label!=null){label.Visible=shown;if(labelText!=null)label.Text=labelText;}}
  bool IsStatusKind(string kind){return (kind??"").StartsWith("상태이상")||(kind??"").StartsWith("상태변화");}
  void PickTriggerStatuses(){bool change=(triggerKind.Text??"").StartsWith("상태변화");string category=change?"상태변화":"상태이상";using(var f=new SearchToggleForm(category+" 선택 · 검색/토글",StateDex.StatusNames(category),triggerStatusNames,0))if(f.ShowDialog(this)==DialogResult.OK){triggerStatusNames=f.Selected;RefreshStructuredTrigger();}}
  void RefreshStructuredTrigger(){if(triggerPreview==null)return;if(triggerActivation==null){triggerActivation=Theme.Combo(new[]{"자동","선행"},"자동");Theme.Row(triggerTable,"입력 방식",triggerActivation);}if(triggerSubject==null){triggerSubject=Theme.Combo(new[]{"자신","아군","상대"},"자신");triggerSubject.SelectedIndexChanged+=(s,e)=>RefreshStructuredTrigger();Theme.Row(triggerTable,"주체",triggerSubject);}if(triggerKind.Items.Count==13)triggerKind.Items.AddRange(new object[]{TriggerStructures.HereLabel,TriggerStructures.SelfOrderLabel,TriggerStructures.AllyOrderLabel,TriggerStructures.AnalyzedLabel,TriggerStructures.BattleStartLabel,TriggerStructures.OpponentEntryLabel,TriggerStructures.MoveUseLabel,TriggerStructures.AttackMoveUseLabel,TriggerStructures.StatusMoveUseLabel,"타입 기술을 내보낼 때","특정 기술을 내보낼 때",TriggerStructures.AttackSuccessLabel,TriggerStructures.DamagedLabel,"XX가 부여되어 있을 때","상태이상일 때","상태이상이 되었을 때","상태변화일 때","상태변화가 되었을 때"});string kind=triggerKind.Text;bool entry=kind=="등장 방법 지정",type=kind=="적진 타입 포켓몬"||kind=="타입 기술을 내보낼 때",move=kind=="특정 기술을 내보낼 때",status=IsStatusKind(kind),granted=kind=="XX가 부여되어 있을 때",aptitude=kind=="트레이너 자질 등급";if(status){bool change=kind.StartsWith("상태변화");var allowed=StateDex.StatusNames(change?"상태변화":"상태이상");triggerStatusNames=(triggerStatusNames??new string[0]).Where(x=>allowed.Contains(x)).ToArray();triggerStatusButton.Text=triggerStatusNames.Length==0?"상태 선택 · 검색/토글":("선택: "+string.Join(", ",triggerStatusNames));}else triggerStatusButton.Text="상태 선택 · 검색/토글";ShowTriggerRow(triggerEntryMethod,entry);ShowTriggerRow(triggerType,type);ShowTriggerRow(triggerMove,move);ShowTriggerRow(triggerGranted,granted,"부여된 이름");ShowTriggerRow(triggerStatusButton,status,"상태 이름");ShowTriggerRow(triggerAptitude,aptitude);ShowTriggerRow(triggerRank,aptitude);if(kind=="상시 (X)")triggerPreview.Text=TriggerStructures.AlwaysLabel;else if(entry)triggerPreview.Text=triggerEntryMethod.Text=="선발"?TriggerStructures.LeadEntryLabel:triggerEntryMethod.Text=="죽어내밀기"?TriggerStructures.ReplacementEntryLabel:triggerEntryMethod.Text=="임의교대"?TriggerStructures.VoluntaryEntryLabel:TriggerStructures.LeadOrReplacementEntryLabel;else if(kind=="적진 타입 포켓몬")triggerPreview.Text=TriggerStructures.OpponentType(triggerType.Text);else if(kind=="타입 기술을 내보낼 때")triggerPreview.Text=TriggerStructures.MoveType(triggerType.Text);else if(move)triggerPreview.Text=TriggerStructures.MoveName(triggerMove.Text);else if(status)triggerPreview.Text=triggerStatusNames.Length==0?"상태를 검색·토글로 선택하세요.":string.Join("\r\n",triggerStatusNames.Select(x=>TriggerStructures.StatusCondition(triggerSubject.Text,x,kind.Contains("되었을 때"),kind.StartsWith("상태변화"))));else if(granted)triggerPreview.Text=TriggerStructures.Granted(triggerGranted.Text.Length>0?triggerGranted.Text:"이름 미지정");else if(aptitude)triggerPreview.Text=TriggerStructures.TrainerAptitude(triggerAptitude.Text,triggerRank.Text);else triggerPreview.Text=kind;}
  string ConfiguredTrigger(){return triggerPreview==null?"":triggerPreview.Text;}
  void LoadPotentialStructureSettings(){
   if(loading)return;var row=SelectedPotentialRow();var record=row==null?null:row.Tag as PotentialRecord;if(record==null)return;loading=true;
   if(triggerActivation!=null)triggerActivation.SelectedItem=triggerActivation.Items.Contains(record.activation)?record.activation:"자동";
   if(triggerSubject!=null)triggerSubject.SelectedItem=triggerSubject.Items.Contains(record.triggerSubject)?record.triggerSubject:"자신";
   if(effectSubject!=null)effectSubject.SelectedItem=effectSubject.Items.Contains(record.effectSubject)?record.effectSubject:"자신";
   triggerStatusNames=new string[0];string[] lines=(Cell(row,"trigger")??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);string name;
   var statusLines=lines.Select(x=>{string s,n;bool b,c;return TriggerStructures.TryStatusCondition(x,out s,out n,out b,out c)?new{subject=s,name=n,became=b,change=c}:null;}).Where(x=>x!=null).ToArray();
   if(statusLines.Length>0){var first=statusLines[0];triggerSubject.SelectedItem=first.subject;triggerStatusNames=statusLines.Where(x=>x.subject==first.subject&&x.became==first.became&&x.change==first.change).Select(x=>x.name).Distinct().ToArray();string kind=first.change?(first.became?"상태변화가 되었을 때":"상태변화일 때"):(first.became?"상태이상이 되었을 때":"상태이상일 때");if(triggerKind.Items.Contains(kind))triggerKind.SelectedItem=kind;}
   else if(lines.Length>0&&TriggerStructures.TryGranted(lines[0],out name)){triggerGranted.Text=name;if(triggerKind.Items.Contains("XX가 부여되어 있을 때"))triggerKind.SelectedItem="XX가 부여되어 있을 때";}
   else {string structured=lines.Select(TriggerStructures.Normalize).FirstOrDefault(x=>triggerKind.Items.Contains(x));if(structured!=null)triggerKind.SelectedItem=structured;}
   LoadStructuredEffectSettings(Cell(row,"effect"));loading=false;RefreshStructuredTrigger();RefreshStructuredEffect();
  }
  void LoadStructuredEffectSettings(string text){
   if(effectKind==null||effectTarget==null||effectAction==null)return;string normalized=EffectNormalizer.Normalize(text??"");
   if(!EffectStructures.IsRank(normalized))return;string target=RankEffectTargets.FirstOrDefault(x=>normalized.StartsWith(x));if(target==null||!effectTarget.Items.Contains(target))return;
   int amount=normalized.Contains("매우 크게")?3:normalized.Contains("크게")?2:1;effectKind.SelectedItem="능력 랭크 변화";effectTarget.SelectedItem=target;effectAction.SelectedItem=normalized.Contains("내린다")?"하락":"상승";effectDenominator.Value=amount;
  }
  void RefreshMoveDex(){string q=dexMoveSearch.Text.Trim();moveDex.Rows.Clear();foreach(var m in catalogDb.moves.Where(x=>(x.name+" "+string.Join(" ",x.types??new string[0])+" "+x.category+" "+x.target+" "+string.Join(" ",x.tags??new string[0])+" "+x.effect).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(x=>x.name)){int i=moveDex.Rows.Add(m.name,string.Join("/",m.types??new string[0]),m.category,m.power,m.accuracy,m.target,Mechanics.Contact(m)?"○":"×",m.priority,string.Join(", ",m.tags??new string[0]),m.effect);moveDex.Rows[i].Tag=m;}if(moveDex.RowCount>0)moveDex.CurrentCell=moveDex.Rows[0].Cells[0];moveCount.Text="기술 도감 · "+moveDex.RowCount+"개 · 이름 / 타입 / 대상 / 태그 / 효과 검색";ShowDexMove();}
  Move SelectedDexMove(){return moveDex.CurrentRow==null?null:moveDex.CurrentRow.Tag as Move;}
  void ShowDexMove(){var m=SelectedDexMove();dexMoveDetail.Text=m==null?"검색 결과가 없습니다.":"【"+m.name+"】  "+string.Join(" / ",m.types??new string[0])+" · "+m.category+"\r\n위력 "+m.power+"   명중 "+m.accuracy+"   우선도 "+m.priority+"   대상 "+m.target+"\r\n판정 "+m.attack+" / "+m.defense+"   태그 "+string.Join(", ",m.tags??new string[0])+"\r\n효과: "+m.effect;}
  void AddDexMove(){var m=SelectedDexMove();if(m==null)return;if(!(Current is PokemonRecord)){MessageBox.Show(this,"포켓몬을 선택한 뒤 기술을 추가하세요.");return;}var names=pMoves.Lines.Where(x=>x.Trim().Length>0).Select(x=>x.Trim()).ToList();if(names.Contains(m.name))return;if(names.Count>=4){MessageBox.Show(this,"기술은 4개까지 넣을 수 있습니다.");return;}names.Add(m.name);pMoves.Text=string.Join("\r\n",names);}
  void RefreshAbilityDex(){string q=abilitySearch.Text.Trim();abilityDex.Rows.Clear();foreach(var ability in AbilityCatalog.All.Where(x=>(x.name+" "+string.Join(" ",x.triggers??new string[0])+" "+string.Join(" ",x.effects??new string[0])+" "+x.effect+" "+x.support+" "+x.blocker).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)){int i=abilityDex.Rows.Add(ability.name,string.Join(" / ",ability.triggers??new string[0]),string.Join(" ",ability.effects??new string[0]),ability.support);abilityDex.Rows[i].Tag=ability;}if(abilityDex.RowCount>0)abilityDex.CurrentCell=abilityDex.Rows[0].Cells[0];abilityCount.Text="특성 도감 · "+abilityDex.RowCount+"개 · 트리거 / 효과 / 구현 상태 검색";ShowDexAbility();}
  AbilityDefinition SelectedDexAbility(){return abilityDex.CurrentRow==null?null:abilityDex.CurrentRow.Tag as AbilityDefinition;}
  void ShowDexAbility(){var ability=SelectedDexAbility();abilityDetail.Text=ability==null?"검색 결과가 없습니다.":"【"+ability.name+"】  "+ability.support+"\r\n트리거: "+(ability.triggers==null||ability.triggers.Length==0?"상시/원문 직접 효과":string.Join(" / ",ability.triggers))+"\r\n효과: "+(ability.effects==null||ability.effects.Length==0?ability.effect:string.Join(" ",ability.effects))+"\r\n"+(ability.blocker.Length==0?"전투 런타임 연결됨.":"수동 사유: "+ability.blocker)+"\r\n원문: "+ability.effect;}
  void AddDexAbility(){var ability=SelectedDexAbility();if(ability==null)return;if(!(Current is PokemonRecord)){MessageBox.Show(this,"포켓몬을 선택한 뒤 특성을 설정하세요.");return;}pAbility.Text=ability.name;SaveCurrent();ShowEditorPage(1);}
  void RefreshPotentialDex(){
   RefreshStructuredTrigger();RefreshStructuredEffect();
  }
  void ApplyPart(bool trigger){
   if(trigger&&string.IsNullOrWhiteSpace(ConfiguredTrigger())){MessageBox.Show(this,"조건 값을 모두 입력하세요.");return;}
   var row=SelectedPotentialRow();if(row==null){MessageBox.Show(this,"포텐셜 편집에서 입력할 행을 먼저 선택하세요.");return;}
   if(trigger&&IsStatusKind(triggerKind.Text)&&triggerStatusNames.Length==0){MessageBox.Show(this,"상태이상 또는 상태변화 도감에서 하나 이상 선택하세요.");return;}
   loading=true;row.Cells[trigger?"trigger":"effect"].Value=trigger?ConfiguredTrigger():ConfiguredEffect();
   var record=row.Tag as PotentialRecord??new PotentialRecord();if(trigger){record.activation=triggerActivation==null?"자동":triggerActivation.Text;record.triggerSubject=triggerSubject==null?"":triggerSubject.Text;}else record.effectSubject=effectSubject==null?"":effectSubject.Text;row.Tag=record;
   loading=false;GridChanged();
  }
  void ComposePotential(){AddPotential(new PotentialRecord{name="새 포텐셜",slot=Current is TrainerRecord?"고유":"종족 ①",activation="자동",triggerSubject=triggerSubject==null?"":triggerSubject.Text,effectSubject=effectSubject==null?"":effectSubject.Text,trigger=ConfiguredTrigger(),effect=ConfiguredEffect(),raw=""});}
  public void TestCatalogUi(){
   TestRetroEditor();
   if(tabs.TabPages[4].Text!="기술 도감"||tabs.TabPages[6].Text!="특성 도감"||abilityDex==null||triggerKind==null||triggerKind.Items.Count<31||!ConfirmedTriggerCatalog.Items.All(x=>triggerKind.Items.Contains(x))||effectKind==null||triggerSubject==null||effectSubject==null)throw new Exception("Structured trigger, subject and effect UI missing");
   abilitySearch.Text="가뭄";var drought=abilityDex.Rows.Cast<DataGridViewRow>().FirstOrDefault(x=>!x.IsNewRow&&((AbilityDefinition)x.Tag).name=="가뭄");if(drought==null||!((AbilityDefinition)drought.Tag).effect.Contains("쾌청")||((AbilityDefinition)drought.Tag).support!="자동"||abilityDex.Columns.Count!=4)throw new Exception("Ability dex trigger/effect classification missing");abilityDex.CurrentCell=drought.Cells[0];AddDexAbility();if(pAbility.Text!="가뭄")throw new Exception("Ability dex must link to the current pokemon");
   tabs.SelectedIndex=1;Application.DoEvents();
   if(!potentials.Columns.Contains("targetType")||potentials.Columns["targetType"].Visible||potentials.Columns.Contains("activation")||potentials.Columns.Contains("uses")||!potentials.Columns.Contains("disabled")||!(potentials.Columns["disabled"] is PixelDisableColumn))throw new Exception("Potential editor columns or pixel toggle invalid");
   dexMoveSearch.Text="비바라기";if(moveDex.Rows.Count==0||moveDex.Columns.Count!=10)throw new Exception("Move classification columns missing");moveDex.CurrentCell=moveDex.Rows[0].Cells[0];ShowDexMove();if(!dexMoveDetail.Text.Contains("비바라기")||!dexMoveDetail.Text.Contains("대상")||!dexMoveDetail.Text.Contains("태그"))throw new Exception("Move details missing");
    triggerKind.SelectedItem="적진 타입 포켓몬";triggerType.SelectedItem="불꽃";if(!triggerType.Visible||triggerEntryMethod.Visible||triggerAptitude.Visible||ConfiguredTrigger()!=TriggerStructures.OpponentType("불꽃"))throw new Exception("Trigger-specific controls failed");triggerKind.SelectedItem="상태이상이 되었을 때";triggerStatusNames=new[]{"화상"};RefreshStructuredTrigger();if(triggerGranted.Visible||!triggerStatusButton.Visible||ConfiguredTrigger()!=TriggerStructures.StatusCondition("자신","화상",true,false))throw new Exception("Status trigger must use the status dex selector");triggerKind.SelectedItem="XX가 부여되어 있을 때";triggerGranted.Text="계약의 특권";if(!triggerGranted.Visible||triggerStatusButton.Visible||ConfiguredTrigger()!=TriggerStructures.Granted("계약의 특권"))throw new Exception("Granted trigger must stay separate from status selector");triggerKind.SelectedItem=DefeatTriggers.Label;var row=potentials.Rows.Cast<DataGridViewRow>().First(r=>!r.IsNewRow&&Cell(r,"name")=="샘플의 전도");potentials.CurrentCell=row.Cells["effect"];string before=Cell(row,"raw");ApplyPart(true);string condition=Cell(row,"trigger");effectKind.SelectedItem="최대 HP의 1/N";effectAction.SelectedItem="회복";ApplyPart(false);if(condition!=DefeatTriggers.Label||Cell(row,"trigger")!=condition||!Cell(row,"effect").Contains("최대 HP")||Cell(row,"raw")!=before)throw new Exception("Independent structured input corrupted data");
   row.Cells["disabled"].Value=true;SaveCurrent();if(CurrentPotentials().First(x=>x.name=="샘플의 전도").enabled)throw new Exception("Never activate toggle failed");
   if(triggerPreview.Font.FontFamily.Name!=Theme.UI(10).FontFamily.Name||dexMoveDetail.Font.FontFamily.Name!=Theme.UI(10).FontFamily.Name)throw new Exception("Catalog font missing");
   var rail=new PixelScrollBar{Maximum=10,Page=3};rail.Value=100;if(rail.Value!=10)throw new Exception("Scroll upper bound");rail.Value=-10;if(rail.Value!=0)throw new Exception("Scroll lower bound");rail.Dispose();
   potentials.HorizontalScrollingOffset=0;if(potentials.RowCount>0)potentials.FirstDisplayedScrollingRowIndex=0;
   effectKind.SelectedItem="수치 배율";effectAction.SelectedItem="약화";effectRate.Value=.8M;if(effectKind.SelectedIndex<0||effectAction.SelectedIndex<0||!effectRate.Visible||effectDenominator.Visible||effectSpeciesRank.Visible||!ConfiguredEffect().Contains("약화(0.8배)"))throw new Exception("Inline structured effect configuration failed");effectKind.SelectedItem="능력 랭크 변화";effectAction.SelectedItem="하락";effectDenominator.Value=3;if(effectKind.SelectedIndex<0||effectAction.SelectedIndex<0||effectRate.Visible||!effectDenominator.Visible||!ConfiguredEffect().Contains("매우 크게 내린다"))throw new Exception("Rank structure configuration failed");effectKind.SelectedItem="우선도 변화";effectTarget.SelectedItem="상대";effectAction.SelectedItem="하락";effectDenominator.Value=2;RefreshStructuredEffect();if(effectKind.SelectedIndex<0||effectAction.SelectedIndex<0||!effectDenominator.Visible||!ConfiguredEffect().Contains("상대의 기술 우선도를 2 내린다"))throw new Exception("Priority structure configuration failed");string firstEffect=Cell(row,"effect");AppendConfiguredEffect();if(Cell(row,"effect")==firstEffect||!Cell(row,"effect").Contains("\r\n"))throw new Exception("Multiple structured effects append failed");triggerKind.SelectedItem=TriggerStructures.ReceivedOrNullifiedLabel;
  }
 }
}
