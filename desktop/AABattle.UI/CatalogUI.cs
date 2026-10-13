using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace AABattle {

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
  protected override void OnMouseWheel(MouseEventArgs e){Value-=Math.Sign(e.Delta)*3;base.OnMouseWheel(e);PixelScrollPanel.WheelHandled(e);}
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
  protected override void OnColumnAdded(DataGridViewColumnEventArgs e){if(e.Column is DataGridViewTextBoxColumn&&!(e.Column.CellTemplate is PixelTextCell))e.Column.CellTemplate=new PixelTextCell();base.OnColumnAdded(e);}
  protected override void OnEditingControlShowing(DataGridViewEditingControlShowingEventArgs e){base.OnEditingControlShowing(e);var combo=e.Control as ComboBox;if(combo!=null&&!"pixel".Equals(combo.Tag)){combo.Tag="pixel";combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;combo.DrawItem+=(s,a)=>{bool selected=(a.State&DrawItemState.Selected)!=0;a.Graphics.FillRectangle(selected?Brushes.Black:Brushes.White,a.Bounds);string shown=a.Index>=0&&a.Index<combo.Items.Count?combo.GetItemText(combo.Items[a.Index]):"";Theme.Text(a.Graphics,shown,Theme.Input(),new Rectangle(a.Bounds.X+6,a.Bounds.Y,Math.Max(1,a.Bounds.Width-8),a.Bounds.Height),selected?Color.White:Color.Black,ContentAlignment.MiddleLeft);};}}
  protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e){
   // Row headers carry labels too (the type chart), so they are drawn inverted like column headers.
   if(e.ColumnIndex<0){if(!RowHeadersVisible){base.OnCellPainting(e);return;}e.Graphics.FillRectangle(Brushes.Black,e.CellBounds);string label=e.RowIndex>=0?Convert.ToString(Rows[e.RowIndex].HeaderCell.Value):"";bool current=e.RowIndex>=0&&CurrentCell!=null&&CurrentCell.RowIndex==e.RowIndex;if(current)Theme.Cursor(e.Graphics,e.CellBounds.X+4,e.CellBounds.Y+e.CellBounds.Height/2,Color.White);if(label.Length>0)Theme.Text(e.Graphics,label,Theme.Input(),new Rectangle(e.CellBounds.X+14,e.CellBounds.Y,Math.Max(1,e.CellBounds.Width-16),e.CellBounds.Height),Color.White,ContentAlignment.MiddleLeft);e.Graphics.DrawLine(Pens.DarkGray,e.CellBounds.X,e.CellBounds.Bottom-1,e.CellBounds.Right,e.CellBounds.Bottom-1);e.Handled=true;return;}
   if(e.RowIndex>=0&&Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn){base.OnCellPainting(e);return;}
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
  readonly PixelTextBox editor;readonly PixelLabel display;
  public override string Text {get{return editor==null?"":editor.Text;}set{if(editor!=null)editor.Text=value??"";}}
  public PixelSearchBox(){Height=34;Dock=DockStyle.Top;BackColor=Color.White;BorderStyle=BorderStyle.FixedSingle;Font=Theme.UI(8);
   editor=new PixelTextBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=Theme.Input()};
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
  protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(bar.Maximum<=0)return;bar.Value-=Math.Sign(e.Delta)*48;PixelScrollPanel.WheelHandled(e);}
 }

 partial class DataEditorForm {
  DataGridView moveDex,abilityDex;PixelSearchBox dexMoveSearch,abilitySearch;PixelText dexMoveDetail,abilityDetail;PixelComboBox effectSubject;
  Label moveCount,abilityCount;
  DataGridView DexGrid(){return MoveDexSearch.CreateGrid();}
  void DexColumn(DataGridView grid,string title,int width){MoveDexSearch.AddColumn(grid,title,width);}
  void BuildCatalogTab(){
   var page=Page("기술 도감");var header=new Panel{Dock=DockStyle.Top,Height=78};dexMoveSearch=new PixelSearchBox();dexMoveSearch.Font=Theme.UI(8);dexMoveSearch.Dock=DockStyle.Bottom;moveCount=Theme.Label("기술 도감",36);header.Controls.Add(dexMoveSearch);header.Controls.Add(moveCount);
   moveDex=DexGrid();foreach(var col in new[]{new[]{"기술", "125"},new[]{"타입","70"},new[]{"분류","60"},new[]{"위력","70"},new[]{"명중","70"},new[]{"대상","70"},new[]{"접촉","60"},new[]{"우선도","85"},new[]{"태그","160"},new[]{"효과 요약","220"}})DexColumn(moveDex,col[0],int.Parse(col[1]));
   dexMoveDetail=new PixelText{Dock=DockStyle.Bottom,Height=175};var add=Button("선택 기술 추가",150,AddDexMove);add.Dock=DockStyle.Bottom;page.Controls.Add(new PixelGridHost(moveDex));page.Controls.Add(dexMoveDetail);page.Controls.Add(add);page.Controls.Add(header);page.Controls[0].BringToFront();
   dexMoveSearch.TextChanged+=(s,e)=>RefreshMoveDex();moveDex.SelectionChanged+=(s,e)=>ShowDexMove();moveDex.CurrentCellChanged+=(s,e)=>ShowDexMove();moveDex.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)AddDexMove();};RefreshMoveDex();
   page=Page("포텐셜 도감");
   var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   columns.Controls.Add(BuildPartPanel(true),0,0);columns.Controls.Add(BuildPartPanel(false),1,0);
   var compose=Button("선택한 조건 + 효과로 포텐셜 만들기",360,ComposePotential);compose.Dock=DockStyle.Bottom;
   page.Controls.Add(columns);page.Controls.Add(compose);columns.BringToFront();
   page=Page("특성 도감");var abilityHeader=new Panel{Dock=DockStyle.Top,Height=78};abilitySearch=new PixelSearchBox{Dock=DockStyle.Bottom,Font=Theme.UI(8)};abilityCount=Theme.Label("특성 도감",36);abilityHeader.Controls.Add(abilitySearch);abilityHeader.Controls.Add(abilityCount);abilityDex=DexGrid();DexColumn(abilityDex,"특성",130);DexColumn(abilityDex,"트리거",180);DexColumn(abilityDex,"효과",450);DexColumn(abilityDex,"구현",80);abilityDetail=new PixelText{Dock=DockStyle.Bottom,Height=175};var useAbility=Button("선택 특성을 포켓몬에 설정",240,AddDexAbility);useAbility.Dock=DockStyle.Bottom;page.Controls.Add(new PixelGridHost(abilityDex));page.Controls.Add(abilityDetail);page.Controls.Add(useAbility);page.Controls.Add(abilityHeader);page.Controls[0].BringToFront();abilitySearch.TextChanged+=(s,e)=>RefreshAbilityDex();abilityDex.SelectionChanged+=(s,e)=>ShowDexAbility();abilityDex.CurrentCellChanged+=(s,e)=>ShowDexAbility();abilityDex.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)AddDexAbility();};RefreshAbilityDex();
  }
  Panel BuildPartPanel(bool trigger){
   return trigger?BuildStructuredTriggerPanel():BuildStructuredEffectPanel();
  }
  void RefreshMoveDex(){string q=dexMoveSearch.Text.Trim();moveDex.Rows.Clear();foreach(var m in MoveDexSearch.Filter(catalogDb.moves,q)){int i=moveDex.Rows.Add(m.name,string.Join("/",m.types??new string[0]),m.category,m.power,m.accuracy,m.target,Mechanics.Contact(m)?"○":"×",m.priority,string.Join(", ",m.tags??new string[0]),m.effect);moveDex.Rows[i].Tag=m;}if(moveDex.RowCount>0)moveDex.CurrentCell=moveDex.Rows[0].Cells[0];moveCount.Text="기술 도감 · "+moveDex.RowCount+"개 · 이름 / 타입 / 대상 / 태그 / 효과 검색";ShowDexMove();}
  Move SelectedDexMove(){return moveDex.CurrentRow==null?null:moveDex.CurrentRow.Tag as Move;}
  void ShowDexMove(){var m=SelectedDexMove();dexMoveDetail.Text=m==null?"검색 결과가 없습니다.":"【"+m.name+"】  "+string.Join(" / ",m.types??new string[0])+" · "+m.category+"\r\n위력 "+m.power+"   명중 "+m.accuracy+"   우선도 "+m.priority+"   대상 "+m.target+"\r\n판정 "+m.attack+" / "+m.defense+"   태그 "+string.Join(", ",m.tags??new string[0])+"\r\n효과: "+m.effect;}
  void AddDexMove(){var m=SelectedDexMove();if(m==null)return;if(!(Current is PokemonRecord)){GameDialog.Show(this,"포켓몬을 선택한 뒤 기술을 추가하세요.");return;}var names=pMoves.Lines.Where(x=>x.Trim().Length>0).Select(x=>x.Trim()).ToList();if(names.Contains(m.name))return;if(names.Count>=4){GameDialog.Show(this,"기술은 4개까지 넣을 수 있습니다.");return;}names.Add(m.name);pMoves.Text=string.Join("\r\n",names);}
  void RefreshAbilityDex(){string q=abilitySearch.Text.Trim();abilityDex.Rows.Clear();foreach(var ability in AbilityCatalog.All.Where(x=>(x.name+" "+string.Join(" ",x.triggers??new string[0])+" "+string.Join(" ",x.effects??new string[0])+" "+x.effect+" "+x.support+" "+x.blocker).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)){int i=abilityDex.Rows.Add(ability.name,string.Join(" / ",ability.triggers??new string[0]),string.Join(" ",ability.effects??new string[0]),ability.support);abilityDex.Rows[i].Tag=ability;}if(abilityDex.RowCount>0)abilityDex.CurrentCell=abilityDex.Rows[0].Cells[0];abilityCount.Text="특성 도감 · "+abilityDex.RowCount+"개 · 트리거 / 효과 / 구현 상태 검색";ShowDexAbility();}
  AbilityDefinition SelectedDexAbility(){return abilityDex.CurrentRow==null?null:abilityDex.CurrentRow.Tag as AbilityDefinition;}
  void ShowDexAbility(){var ability=SelectedDexAbility();abilityDetail.Text=ability==null?"검색 결과가 없습니다.":"【"+ability.name+"】  "+ability.support+"\r\n트리거: "+(ability.triggers==null||ability.triggers.Length==0?"상시/원문 직접 효과":string.Join(" / ",ability.triggers))+"\r\n효과: "+(ability.effects==null||ability.effects.Length==0?ability.effect:string.Join(" ",ability.effects))+"\r\n"+(ability.blocker.Length==0?"전투 런타임 연결됨.":ability.support=="자동"?"전투 런타임 연결됨. "+ability.blocker:"수동 사유: "+ability.blocker)+"\r\n원문: "+ability.effect;}
  void AddDexAbility(){var ability=SelectedDexAbility();if(ability==null)return;if(!(Current is PokemonRecord)){GameDialog.Show(this,"포켓몬을 선택한 뒤 특성을 설정하세요.");return;}pAbility.Text=ability.name;SaveCurrent();ShowEditorPage(1);}
  // 모듈 화면의 「구조 트리거/효과 입력」: 편집 창의 트리거·효과 칸에 문장을 넣는다(표는 확인할 때 바뀐다)
  void ApplyPart(bool trigger){
   if(editWindow==null){GameDialog.Show(this,"표에서 포텐셜을 골라 「편집」으로 편집 창을 연 뒤 입력하세요.");return;}
   string text=trigger?ConfiguredTrigger():ConfiguredEffect();
   if(string.IsNullOrWhiteSpace(text)){GameDialog.Show(this,"빈칸을 모두 채우세요.");return;}
   editWindow.SetPart(trigger,text,trigger&&triggerActivation!=null?triggerActivation.Text:"자동",trigger?TriggerSubjectText():(effectSubject==null?"":effectSubject.Text));
  }
  void ComposePotential(){AddPotential(new PotentialRecord{name="새 포텐셜",slot=Current is TrainerRecord?"고유":"종족 ①",activation="자동",triggerSubject=TriggerSubjectText(),effectSubject=effectSubject==null?"":effectSubject.Text,trigger=ConfiguredTrigger(),effect=ConfiguredEffect(),raw=""});}
  void Expect(bool ok,string message){if(!ok)throw new Exception(message);}
  void TestStructuredEffectUi(){
   effectSubject.SelectedItem="자신";
   effectBuilder.Select("effect.switch.voluntary");
   Expect(ConfiguredEffect()==EffectNormalizer.CanonicalSwitch(),"Switch effect module UI failed");
   effectBuilder.Select("effect.switch.pivot");
   Expect(ConfiguredEffect()==EffectNormalizer.CanonicalPivotGrant(),"Pivot effect module UI failed");
   effectBuilder.Select("effect.stat.ignore.강화");
   effectBuilder.SetValue("대상","상대");
   effectBuilder.SetValue("능력","공격");
   Expect(ConfiguredEffect().Contains("상대의 공격 강화")&&ConfiguredEffect().Contains("무시한다"),"Stat ignore module UI failed");
   effectBuilder.Select("effect.analysis.permission");
   Expect(ConfiguredEffect()==EffectNormalizer.CanonicalAnalysisPermission(),"Analysis effect module UI failed");
  }
  public void TestCatalogUi(){
   TestRetroEditor();
   Expect(tabs.TabPages[4].Text=="기술 도감"&&tabs.TabPages[6].Text=="특성 도감"&&abilityDex!=null,"Catalog tabs missing");
   Expect(triggerBuilder!=null&&effectBuilder!=null&&triggerPreview!=null&&effectPreview!=null&&effectSubject!=null&&triggerActivation!=null,"Module builder UI missing");
   Expect(ConfirmedTriggerCatalog.Items.All(x=>triggerBuilder.Load(x)),"Every confirmed trigger sentence must be loadable into the module builder");
   Expect(effectBuilder.Editor("발동확률")!=null,"Probability slot missing");
   abilitySearch.Text="가뭄";
   var drought=abilityDex.Rows.Cast<DataGridViewRow>().FirstOrDefault(x=>!x.IsNewRow&&((AbilityDefinition)x.Tag).name=="가뭄");
   Expect(drought!=null&&((AbilityDefinition)drought.Tag).effect.Contains("쾌청")&&((AbilityDefinition)drought.Tag).support=="자동"&&abilityDex.Columns.Count==4,"Ability dex trigger/effect classification missing");
   abilityDex.CurrentCell=drought.Cells[0];AddDexAbility();
   Expect(pAbility.Text=="가뭄","Ability dex must link to the current pokemon");
   tabs.SelectedIndex=1;Application.DoEvents();TestStructuredEffectUi();
   triggerBuilder.Select("trigger.entry.both");
   Expect(ConfiguredTrigger()==TriggerStructures.SelfAndOpponentEntryLabel,"Self and opponent entry trigger UI failed");
   Expect(potentials.Columns.Contains("targetType")&&!potentials.Columns["targetType"].Visible&&!potentials.Columns.Contains("activation")&&!potentials.Columns.Contains("uses")&&potentials.Columns.Contains("disabled")&&potentials.Columns["disabled"] is PixelDisableColumn,"Potential editor columns or pixel toggle invalid");
   dexMoveSearch.Text="비바라기";
   Expect(moveDex.Rows.Count>0&&moveDex.Columns.Count==10,"Move classification columns missing");
   moveDex.CurrentCell=moveDex.Rows[0].Cells[0];ShowDexMove();
   Expect(dexMoveDetail.Text.Contains("비바라기")&&dexMoveDetail.Text.Contains("대상")&&dexMoveDetail.Text.Contains("태그"),"Move details missing");
   triggerBuilder.Select("trigger.move.False");
   Expect(triggerBuilder.GetValue("기술조건")==""&&ConfiguredTrigger()==TriggerStructures.MoveUseLabel,"Move use module failed");
   triggerBuilder.SetValue("기술조건","공격");
   Expect(ConfiguredTrigger()==TriggerStructures.AttackMoveUseLabel,"Attack move module failed");
   triggerBuilder.SetValue("기술조건","불꽃");
   Expect(ConfiguredTrigger()==TriggerStructures.MoveType("불꽃"),"Move type module failed");
   triggerBuilder.Select("trigger.move.True",new Dictionary<string,string>{{"기술조건","「불꽃」"}});
   Expect(ConfiguredTrigger()=="「불꽃」 기술을 내보냈을 때","Move used-after module failed");
   triggerBuilder.Select("trigger.timing.event.False",new Dictionary<string,string>{{"사건","공격을 받았을 때"}});
   Expect(ConfiguredTrigger()=="「상대의 공격을 받았을 때」가 성립한 T 종료 시","Turn-end after event module failed");
   triggerBuilder.Select("trigger.timing.every",new Dictionary<string,string>{{"배수","3"},{"시점",TriggerStructures.TurnEndLabel}});
   Expect(ConfiguredTrigger()=="「3」배수 T 종료 시","Turn multiple module failed");
   triggerBuilder.Select("trigger.entry.method",new Dictionary<string,string>{{"주체","상대"},{"방식","강제교대"}});
   Expect(ConfiguredTrigger()=="상대가 강제교대로 장소에 나왔을 때","Opponent entry method module failed");
   triggerBuilder.Select("trigger.opponent.type",new Dictionary<string,string>{{"이름","불꽃"}});
   Expect(ConfiguredTrigger()==TriggerStructures.OpponentType("불꽃"),"Opponent type module failed");
   triggerBuilder.Select("trigger.state.상태 됨",new Dictionary<string,string>{{"주체","자신"},{"이름","화상"}});
   Expect(ConfiguredTrigger()==TriggerStructures.StatusCondition("자신","화상",true,false),"Status trigger module failed");
   triggerBuilder.Select("trigger.named.부여",new Dictionary<string,string>{{"이름","계약의 특권"}});
   Expect(ConfiguredTrigger()==TriggerStructures.Granted("계약의 특권"),"Granted trigger module failed");
   triggerBuilder.Select("trigger.defeat");
   var row=potentials.Rows.Cast<DataGridViewRow>().First(r=>!r.IsNewRow&&Cell(r,"name")=="샘플의 전도");
   potentials.CurrentCell=row.Cells["effect"];
   string before=Cell(row,"raw");
   var sample=OpenPotentialWindowForTest(DraftFromRow(row));
   triggerBuilder.Select("trigger.defeat");
   ApplyPart(true);
   string condition=sample.TriggerBox.Text;
   effectBuilder.Select("effect.hp.회복");
   ApplyPart(false);
   Expect(condition==DefeatTriggers.Label&&sample.TriggerBox.Text==condition&&sample.EffectBox.Text.Contains("최대 HP")&&sample.Collect().Raw==before,"Independent structured input corrupted data");
   Expect(Cell(row,"trigger")!=condition,"Table must not change until the window is confirmed");
   sample.Accept();var sampleResult=sample.Result;sample.Dispose();
   CommitPotentialRow(row,DraftFromRow(row),sampleResult);
   Expect(Cell(row,"trigger")==condition&&Cell(row,"effect").Contains("최대 HP")&&Cell(row,"raw")==before,"Confirmed structured input must reach the table");
   string legacyTrigger=Cell(row,"trigger");
   var legacy=DraftFromRow(row);legacy.Trigger="공격기술을 내보낼 때";
   LoadBuilders(legacy);
   Expect(triggerBuilder.Module.Id=="trigger.move.False"&&triggerBuilder.GetValue("기술조건")=="공격"&&((MoveConditionSlotEditor)triggerBuilder.Editor("기술조건")).Mode==MoveConditionSlotEditor.CategoryMode&&ConfiguredTrigger()==TriggerStructures.AttackMoveUseLabel,"Legacy attack move trigger loading failed: "+triggerBuilder.Module.Id+" / "+ConfiguredTrigger());
   legacy.Trigger=legacyTrigger;
   LoadBuilders(legacy);
   row.Cells["disabled"].Value=true;SaveCurrent();
   Expect(!CurrentPotentials().First(x=>x.name=="샘플의 전도").enabled,"Never activate toggle failed");
   Expect(triggerPreview.Font.FontFamily.Name==Theme.UI(10).FontFamily.Name&&dexMoveDetail.Font.FontFamily.Name==Theme.UI(10).FontFamily.Name,"Catalog font missing");
   var rail=new PixelScrollBar{Maximum=10,Page=3};rail.Value=100;Expect(rail.Value==10,"Scroll upper bound");rail.Value=-10;Expect(rail.Value==0,"Scroll lower bound");rail.Dispose();
   potentials.HorizontalScrollingOffset=0;if(potentials.RowCount>0)potentials.FirstDisplayedScrollingRowIndex=0;
   effectBuilder.Select("effect.scalar.stats.약화");effectBuilder.SetValue("배율","0.8");
   Expect(ConfiguredEffect().Contains("약화(0.8배)"),"Scalar module configuration failed");
   effectBuilder.Select("effect.rank.lower3");
   Expect(ConfiguredEffect().Contains("매우 크게 내린다"),"Rank module configuration failed");
   effectBuilder.Select("effect.priority.내린다");effectBuilder.SetValue("주체","상대");effectBuilder.SetValue("수치","2");
   Expect(ConfiguredEffect().Contains("상대의 기술 우선도를 2 내린다"),"Priority module configuration failed");
   effectBuilder.SetValue("발동확률","중확률");
   string probabilistic=ConfiguredEffect();
   Expect(probabilistic=="중확률로 상대의 기술 우선도를 2 내린다.","Probability effect prefix failed: "+probabilistic);
   var probabilityDraft=DraftFromRow(row);probabilityDraft.Effect=probabilistic;
   var append=OpenPotentialWindowForTest(probabilityDraft);
   Expect(effectBuilder.GetValue("발동확률")=="중확률"&&ConfiguredEffect()==probabilistic,"Probability effect prefix loading failed");
   effectBuilder.SetValue("발동확률","없음");
   string firstEffect=append.EffectBox.Text;
   AppendConfiguredEffect();
   Expect(append.EffectBox.Text!=firstEffect&&append.EffectBox.Text.Contains("\r\n"),"Multiple structured effects append failed");
   append.Cancel();append.Dispose();
   triggerBuilder.Select("trigger.nullified",new Dictionary<string,string>{{"기술조건","「불꽃」「접촉」"}});
   effectBuilder.Select("effect.secondary.state");effectBuilder.SetValue("발동확률","없음");
  }
 }
}
