using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AABattle {
 class RetroFrame:Panel {
  public RetroFrame(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(12);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.Frame(e.Graphics,ClientRectangle,false);}
 }
 partial class DataEditorForm {
  TextBox inlineTrigger,inlineEffect;
  Label inlineTitle;ComboBox inlineTemplateType;
  bool inlineLoading;Panel pokemonWorkspace;
  Panel pokemonInfo,trainerInfo;TableLayoutPanel workspaceStack;
  TextBox triggerArguments;string argumentKind="";bool argumentLoading;
  ListBox editorMenu;
  int[] editorPages={7,0,1,4,6,3,8,9};
  void BuildRetroEditor(){
   Size=new Size(1480,960);MinimumSize=new Size(1200,820);Font=Theme.UI(9);ImeMode=ImeMode.NoControl;
   tabs.Appearance=TabAppearance.Normal;tabs.SizeMode=TabSizeMode.Normal;
   var menuFrame=new RetroFrame{Dock=DockStyle.Left,Width=174,Padding=new Padding(12,20,12,12)};
   editorMenu=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=48,Font=Theme.UI(11),IntegralHeight=false};
   editorMenu.Items.AddRange(new object[]{"팀 보관함","트레이너","포켓몬","기술 도감","특성 도감","시트 출력","상태·날씨","효과 안내"});
   editorMenu.DrawItem+=(s,e)=>{if(e.Index<0)return;e.Graphics.FillRectangle(Brushes.White,e.Bounds);bool selected=(e.State&DrawItemState.Selected)!=0;Theme.Text(e.Graphics,(selected?"▶ ":"   ")+editorMenu.Items[e.Index],editorMenu.Font,e.Bounds,Color.Black,ContentAlignment.MiddleLeft);};
   editorMenu.SelectedIndexChanged+=(s,e)=>{if(editorMenu.SelectedIndex>=0)ShowEditorPage(editorPages[editorMenu.SelectedIndex]);};
   editorMenu.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){ShowEditorPage(editorPages[Math.Max(0,editorMenu.SelectedIndex)]);if(pokemonWorkspace!=null&&pokemonWorkspace.Visible)pokemonWorkspace.SelectNextControl(null,true,true,true,false);else tabs.SelectedTab.SelectNextControl(null,true,true,true,false);e.Handled=e.SuppressKeyPress=true;}};
   menuFrame.Controls.Add(editorMenu);Controls.Add(menuFrame);menuFrame.SendToBack();
   var hint=Theme.Label("방향키: 이동   SPACE: 선택   TAB: 다음 입력   F6: 메뉴   /   한글·마우스 입력",32);hint.Dock=DockStyle.Bottom;Controls.Add(hint);hint.SendToBack();
   tabs.SelectedIndexChanged+=(s,e)=>{if(tabs.SelectedIndex==5||tabs.SelectedIndex==2){ShowEditorPage(1);return;}if(tabs.SelectedIndex==1&&pokemonWorkspace!=null){ShowEditorPage(1);return;}int index=Array.IndexOf(editorPages,tabs.SelectedIndex);if(index>=0&&editorMenu.SelectedIndex!=index)editorMenu.SelectedIndex=index;};
   BuildInlinePotentials();EmbedPotentialsInPokemon();foreach(string item in ConfirmedTriggerCatalog.Items)if(!triggerKind.Items.Contains(item))triggerKind.Items.Add(item);
   triggerArguments=Box(false);Theme.Row(triggerTable,"조건 값 (| 구분)",triggerArguments);
   triggerKind.SelectedIndexChanged+=(s,e)=>RefreshTriggerArguments();triggerArguments.TextChanged+=(s,e)=>UpdateTriggerArguments();
   StyleEditor(this);aa.Font=new Font(Fonts.AA,16,FontStyle.Regular,GraphicsUnit.Pixel);tAA.Font=aa.Font;
   InstallSelector(pTypes,"타입 찾기",PickTypes);InstallSelector(pAbility,"특성 찾기",PickAbility);InstallSelector(pMoves,"기술 찾기",PickMoves);
   RefreshTriggerArguments();
   InstallPixelChoice(triggerKind);InstallPixelChoice(triggerActivation);InstallPixelChoice(triggerSubject);InstallPixelChoice(effectSubject);
   potentials.CellPainting+=(s,e)=>{if(e.RowIndex!=-1||e.ColumnIndex<0)return;e.PaintBackground(e.ClipBounds,false);Theme.Text(e.Graphics,Convert.ToString(e.FormattedValue),Theme.UI(8),e.CellBounds,Color.White,ContentAlignment.MiddleLeft);e.Handled=true;};
   tree.ItemHeight=32;tree.ShowLines=false;tree.ShowPlusMinus=false;tree.ShowRootLines=false;tree.FullRowSelect=true;
   foreach(TabPage page in tabs.TabPages)page.Paint+=(s,e)=>Theme.Frame(e.Graphics,((Control)s).ClientRectangle,false);
   foreach(Control bar in tabs.TabPages[1].Controls.Cast<Control>().Where(x=>x.Dock==DockStyle.Bottom).ToArray())bar.SendToBack();
   var moveTable=pMoves.Parent as TableLayoutPanel;if(moveTable!=null)moveTable.RowStyles[moveTable.GetRow(pMoves)].Height=80;
   Shown+=(s,e)=>{var host=tree.Parent.Parent as SplitContainer;if(host!=null)host.SplitterDistance=170;var pokemonSplit=tabs.TabPages[1].Controls.OfType<SplitContainer>().FirstOrDefault();if(pokemonSplit!=null)pokemonSplit.SplitterDistance=(int)(pokemonSplit.Width*.60);};
  }
  void InstallSelector(TextBox box,string title,Action action){var table=box.Parent as TableLayoutPanel;if(table==null)return;int column=table.GetColumn(box),row=table.GetRow(box);var panel=new Panel{Dock=DockStyle.Fill};table.Controls.Remove(box);var button=Button(title,105,action);button.Dock=DockStyle.Right;panel.Controls.Add(box);panel.Controls.Add(button);table.Controls.Add(panel,column,row);}
  void InstallPixelChoice(ComboBox combo){if(combo==null)return;var table=combo.Parent as TableLayoutPanel;if(table==null)return;int row=table.GetRow(combo),column=table.GetColumn(combo);var choice=new FlatButton{Dock=DockStyle.Fill,Font=Theme.UI(8),Text=combo.Text+" ▼"};table.Controls.Remove(combo);choice.Controls.Add(combo);combo.Visible=false;table.Controls.Add(choice,column,row);combo.SelectedIndexChanged+=(s,e)=>choice.Text=combo.Text+" ▼";choice.Click+=(s,e)=>{using(var dialog=new SearchToggleForm("선택 · 검색 / SPACE",combo.Items.Cast<object>().Select(x=>x.ToString()),new[]{combo.Text},1))if(dialog.ShowDialog(this)==DialogResult.OK&&dialog.Selected.Length>0)combo.SelectedItem=dialog.Selected[0];};}
  static MatchCollection ArgumentMatches(string text){return Regex.Matches(text??"",@"(?<=「)[^」]+(?=」)|(?<=『)[^』]+(?=』)|[+-]?\d+(?:/\d+)?|(?<=조건 )[AB]");}
  void RefreshTriggerArguments(){if(triggerArguments==null)return;argumentLoading=true;argumentKind=triggerKind.Text;triggerArguments.Text=string.Join(" | ",ArgumentMatches(argumentKind).Cast<Match>().Select(x=>x.Value));ShowTriggerRow(triggerArguments,ConfirmedTriggerCatalog.Items.Contains(argumentKind)&&ArgumentMatches(argumentKind).Count>0,"조건 값 (| 구분)");argumentLoading=false;UpdateTriggerArguments();}
  void UpdateTriggerArguments(){if(argumentLoading||triggerArguments==null||!ConfirmedTriggerCatalog.Items.Contains(argumentKind))return;var values=triggerArguments.Text.Split('|').Select(x=>x.Trim()).ToArray();var matches=ArgumentMatches(argumentKind);if(matches.Count==0){triggerPreview.Text=argumentKind;return;}if(values.Length!=matches.Count||values.Any(x=>x.Length==0)){triggerPreview.Text="";return;}string result=argumentKind;for(int i=matches.Count-1;i>=0;i--)result=result.Remove(matches[i].Index,matches[i].Length).Insert(matches[i].Index,values[i]);triggerPreview.Text=result;}
  void StyleEditor(Control root){
   foreach(Control c in root.Controls){
    if(c is TextBox){c.Font=new Font("맑은 고딕",11);c.ImeMode=ImeMode.NoControl;}
    else if(c is TreeView||c is ComboBox||c is NumericUpDown)c.Font=new Font("맑은 고딕",10);
    var combo=c as ComboBox;if(combo!=null){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Space){combo.DroppedDown=!combo.DroppedDown;e.Handled=e.SuppressKeyPress=true;}};}
    var grid=c as DataGridView;if(grid!=null){grid.DefaultCellStyle.Font=new Font("맑은 고딕",10);grid.DefaultCellStyle.SelectionBackColor=Color.Black;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.GridColor=Color.Black;grid.RowTemplate.Height=32;grid.EditingControlShowing+=(s,e)=>{e.Control.Font=new Font("맑은 고딕",10);e.Control.ImeMode=ImeMode.NoControl;};}
    var button=c as FlatButton;if(button!=null){button.GotFocus+=(s,e)=>{button.Selected=true;button.Invalidate();};button.LostFocus+=(s,e)=>{button.Selected=false;button.Invalidate();};}
    StyleEditor(c);
   }
  }
  void BuildInlinePotentials(){
   var page=tabs.TabPages[2];var oldControls=page.Controls.Cast<Control>().ToArray();
   var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,Size=new Size(950,820),SplitterDistance=260,Panel1MinSize=180,Panel2MinSize=280,SplitterWidth=8};
   foreach(var control in oldControls)split.Panel1.Controls.Add(control);
   page.Controls.Add(split);split.BringToFront();
   potentials.Columns["raw"].Visible=false;potentials.Columns["targetType"].Visible=false;potentials.Columns["trigger"].Width=220;potentials.Columns["effect"].Width=270;
   var lower=new RetroFrame{Dock=DockStyle.Fill};split.Panel2.Controls.Add(lower);
   var titleBar=new TableLayoutPanel{Dock=DockStyle.Top,Height=38,ColumnCount=2};titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));inlineTitle=Theme.Label("포텐셜 선택 → 아래에서 조건과 효과 편집",30);inlineTemplateType=Theme.Combo(EditorTemplate.CounterTypeNames(),"불꽃");inlineTemplateType.Visible=false;titleBar.Controls.Add(inlineTitle,0,0);titleBar.Controls.Add(inlineTemplateType,1,0);lower.Controls.Add(titleBar);
   var inputs=new TableLayoutPanel{Dock=DockStyle.Top,Height=118,ColumnCount=2,RowCount=2};inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));inputs.RowStyles.Add(new RowStyle(SizeType.Absolute,28));inputs.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   inlineTrigger=Box(true);inlineEffect=Box(true);inputs.Controls.Add(Theme.Label("트리거 조건",26),0,0);inputs.Controls.Add(Theme.Label("효과",26),1,0);inputs.Controls.Add(inlineTrigger,0,1);inputs.Controls.Add(inlineEffect,1,1);
   lower.Controls.Add(inputs);inputs.BringToFront();
   var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};lower.Controls.Add(scroll);scroll.BringToFront();
   var catalog=tabs.TabPages[5];foreach(var c in catalog.Controls.Cast<Control>().ToArray()){if(c is TableLayoutPanel){c.Dock=DockStyle.Top;c.Height=690;scroll.Controls.Add(c);foreach(Control panel in c.Controls)foreach(Control table in panel.Controls){var layout=table as TableLayoutPanel;if(layout!=null&&layout.ColumnStyles.Count>0){layout.Padding=new Padding(4);layout.ColumnStyles[0].Width=105;}}}else{c.Visible=false;}}
   inlineTrigger.TextChanged+=(s,e)=>WriteInline("trigger",inlineTrigger.Text);inlineEffect.TextChanged+=(s,e)=>WriteInline("effect",inlineEffect.Text);
   potentials.SelectionChanged+=(s,e)=>RefreshInline();potentials.CellValueChanged+=(s,e)=>RefreshInline();
   inlineTemplateType.SelectedIndexChanged+=(s,e)=>ApplyInlineTemplateType();
  }
  bool navigating;
  void EmbedPotentialsInPokemon(){
   var pokemon=tabs.TabPages[1];var potential=tabs.TabPages[2];
   foreach(Control bar in pokemon.Controls.Cast<Control>().Where(x=>x is FlowLayoutPanel&&x.Controls.Cast<Control>().Any(y=>y.Text.Contains("배틀 슬롯"))).ToArray()){pokemon.Controls.Remove(bar);bar.Dispose();}
   pokemonWorkspace=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,AutoScroll=true};
   var stack=new TableLayoutPanel{Dock=DockStyle.Top,Height=1200,ColumnCount=1,RowCount=2,Padding=new Padding(8)};workspaceStack=stack;
   stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   stack.RowStyles.Add(new RowStyle(SizeType.Absolute,360));stack.RowStyles.Add(new RowStyle(SizeType.Absolute,820));
   var info=new Panel{Dock=DockStyle.Fill};pokemonInfo=info;trainerInfo=new Panel{Dock=DockStyle.Fill};
   foreach(Control c in tabs.TabPages[0].Controls.Cast<Control>().ToArray()){trainerInfo.Controls.Add(c);c.Visible=true;}
   var host=new Panel{Dock=DockStyle.Fill,Tag="potential-host"};
   foreach(Control c in pokemon.Controls.Cast<Control>().ToArray()){info.Controls.Add(c);c.Visible=true;}
   foreach(Control c in potential.Controls.Cast<Control>().ToArray()){host.Controls.Add(c);c.Visible=true;}
   stack.Controls.Add(info,0,0);stack.Controls.Add(host,0,1);pokemonWorkspace.Controls.Add(stack);
   tabs.Parent.Controls.Add(pokemonWorkspace);pokemonWorkspace.Visible=false;
  }
  void ShowEditorPage(int page){
   if(navigating)return;navigating=true;
   try{if(page==2||page==5)page=1;
    tabs.SelectedIndex=page;
    bool integrated=(page==1||page==0)&&pokemonWorkspace!=null;
    if(integrated){var wanted=page==0?trainerInfo:pokemonInfo;var other=page==0?pokemonInfo:trainerInfo;if(other.Parent==workspaceStack)workspaceStack.Controls.Remove(other);if(wanted.Parent!=workspaceStack)workspaceStack.Controls.Add(wanted,0,0);wanted.Visible=true;}
    if(pokemonWorkspace!=null)pokemonWorkspace.Visible=integrated;
    tabs.Visible=!integrated;
    if(integrated)pokemonWorkspace.BringToFront();else tabs.BringToFront();
    int index=Array.IndexOf(editorPages,page);if(index>=0&&editorMenu.SelectedIndex!=index)editorMenu.SelectedIndex=index;
   }finally{navigating=false;}
  }
  void RefreshInline(){if(inlineTrigger==null||inlineLoading)return;var row=SelectedPotentialRow();inlineLoading=true;inlineTrigger.Enabled=inlineEffect.Enabled=row!=null;string slot=row==null?"":Cell(row,"slot");inlineTitle.Text=row==null?"위 목록에서 포텐셜을 선택하세요":"『"+Cell(row,"name")+"』 · "+slot;inlineTrigger.Text=row==null?"":Cell(row,"trigger");inlineEffect.Text=row==null?"":Cell(row,"effect");bool typed=row!=null&&(EditorTemplate.IsCounterSlot(slot)||EditorTemplate.IsTypedPotential(slot,Cell(row,"name")));inlineTemplateType.Visible=typed;if(typed&&inlineTemplateType.Items.Contains(Cell(row,"targetType")))inlineTemplateType.SelectedItem=Cell(row,"targetType");inlineLoading=false;}
  void ApplyInlineTemplateType(){if(inlineLoading||!inlineTemplateType.Visible)return;var row=SelectedPotentialRow();if(row==null)return;inlineLoading=true;row.Cells["targetType"].Value=inlineTemplateType.Text;inlineLoading=false;RefreshInline();}
  void WriteInline(string column,string text){if(inlineLoading||loading)return;var row=SelectedPotentialRow();if(row==null)return;inlineLoading=true;row.Cells[column].Value=text;GridChanged();inlineLoading=false;}
  void TestRetroEditor(){
   ShowEditorPage(1);Application.DoEvents();if(!pokemonWorkspace.Visible||!potentials.Visible||!inlineEffect.Visible)throw new Exception("Pokemon and potential editors must be visible together");
   triggerKind.SelectedItem="날씨가 「비」일 때";triggerArguments.Text="쾌청";if(ConfiguredTrigger()!="날씨가 「쾌청」일 때")throw new Exception("Weather parameter editing");
   triggerKind.SelectedItem="자신의 체력이 1/2 이하가 되었을 때";triggerArguments.Text="1/4";if(ConfiguredTrigger()!="자신의 체력이 1/4 이하가 되었을 때")throw new Exception("HP threshold parameter editing");
   var row=potentials.Rows.Cast<DataGridViewRow>().First(x=>!x.IsNewRow&&Cell(x,"slot")=="역할");potentials.CurrentCell=row.Cells["name"];
   string name=Cell(row,"name");row.Cells["name"].Value="귀인";RefreshInline();
   if(!inlineEffect.Text.Contains("공격")||inlineTrigger.Text.Length==0)throw new Exception("Template must populate inline trigger and effect");
   inlineEffect.Text="한글 입력 검증";if(Cell(row,"effect")!="한글 입력 검증")throw new Exception("Inline Korean text must persist");
   row.Cells["name"].Value=name;RefreshInline();if(inlineEffect.Text!=Cell(row,"effect")||inlineTrigger.Text!=Cell(row,"trigger"))throw new Exception("Template and inline editor synchronization");
   if(editorMenu.Items.Count!=8||inlineEffect.ImeMode==ImeMode.Disable||potentials.Columns["targetType"].Visible||triggerKind.Items.Count<ConfirmedTriggerCatalog.Items.Length)throw new Exception("Integrated menu, trigger catalog and IME configuration");
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
   if(keyData==Keys.F6){editorMenu.Focus();return true;}
   Control focused=ActiveControl;while(focused is ContainerControl&&((ContainerControl)focused).ActiveControl!=null)focused=((ContainerControl)focused).ActiveControl;
   if(focused is TextBoxBase||focused is NumericUpDown||focused is ComboBox||focused is DataGridView||focused is TreeView||focused is ListBox)return base.ProcessCmdKey(ref msg,keyData);
   if(keyData==Keys.Down||keyData==Keys.Right)return SelectNextControl(focused,true,true,true,true);
   if(keyData==Keys.Up||keyData==Keys.Left)return SelectNextControl(focused,false,true,true,true);
   return base.ProcessCmdKey(ref msg,keyData);
  }
 }
}
