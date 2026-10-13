using System;
using System.Collections.Generic;
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
  PixelScrollPanel pokemonWorkspace;
  bool responsiveLayoutPending;
  Panel pokemonInfo,trainerInfo;TableLayoutPanel workspaceStack,inlineCatalog;
  ListBox editorMenu;
  int[] editorPages={7,0,1,4,6,3,8,9};
  void BuildRetroEditor(){
   Size=new Size(1480,960);MinimumSize=new Size(1200,820);Font=Theme.UI(9);ImeMode=ImeMode.NoControl;
   tabs.Appearance=TabAppearance.FlatButtons;tabs.SizeMode=TabSizeMode.Fixed;tabs.ItemSize=new Size(0,1);
   var menuFrame=new RetroFrame{Dock=DockStyle.Left,Width=212,Padding=new Padding(12,20,12,12)};
   editorMenu=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=52,Font=Theme.UI(16),IntegralHeight=false};
   editorMenu.Items.AddRange(new object[]{"팀 보관함","트레이너","포켓몬","기술 도감","특성 도감","시트 출력","상태·날씨","효과 안내"});
   editorMenu.DrawItem+=(s,e)=>{if(e.Index<0)return;e.Graphics.FillRectangle(Brushes.White,e.Bounds);bool selected=(e.State&DrawItemState.Selected)!=0;if(selected)Theme.Cursor(e.Graphics,e.Bounds.X+2,e.Bounds.Y+e.Bounds.Height/2,Color.Black,2);Theme.Text(e.Graphics,Convert.ToString(editorMenu.Items[e.Index]),editorMenu.Font,new Rectangle(e.Bounds.X+24,e.Bounds.Y,Math.Max(1,e.Bounds.Width-24),e.Bounds.Height),Color.Black,ContentAlignment.MiddleLeft);};
   editorMenu.SelectedIndexChanged+=(s,e)=>{if(editorMenu.SelectedIndex>=0)ShowEditorPage(editorPages[editorMenu.SelectedIndex]);};
   editorMenu.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){ShowEditorPage(editorPages[Math.Max(0,editorMenu.SelectedIndex)]);if(pokemonWorkspace!=null&&pokemonWorkspace.Visible)pokemonWorkspace.SelectNextControl(null,true,true,true,false);else tabs.SelectedTab.SelectNextControl(null,true,true,true,false);e.Handled=e.SuppressKeyPress=true;}};
   menuFrame.Controls.Add(editorMenu);Controls.Add(menuFrame);menuFrame.SendToBack();
   var hint=Theme.Label("  ↑↓ 메뉴 이동     SPACE 선택     TAB 다음 입력     F6 메뉴로     마우스·한글 입력 가능",30);hint.Dock=DockStyle.Bottom;Controls.Add(hint);hint.SendToBack();
   tabs.SelectedIndexChanged+=(s,e)=>{if(tabs.SelectedIndex==5||tabs.SelectedIndex==2){ShowEditorPage(1);return;}if(tabs.SelectedIndex==1&&pokemonWorkspace!=null){ShowEditorPage(1);return;}int index=Array.IndexOf(editorPages,tabs.SelectedIndex);if(index>=0&&editorMenu.SelectedIndex!=index)editorMenu.SelectedIndex=index;};
   BuildInlinePotentials();EmbedPotentialsInPokemon();
   StyleEditor(this);aa.Font=new Font(Fonts.AA,16,FontStyle.Regular,GraphicsUnit.Pixel);tAA.Font=aa.Font;
   InstallSelector(pTypes,"타입 찾기",PickTypes);InstallSelector(pAbility,"특성 찾기",PickAbility);InstallSelector(pMoves,"기술 찾기",PickMoves);
   InstallPixelChoice(triggerActivation);InstallPixelChoice(effectSubject);
   potentials.CellPainting+=(s,e)=>{if(e.RowIndex!=-1||e.ColumnIndex<0)return;e.PaintBackground(e.ClipBounds,false);Theme.Text(e.Graphics,Convert.ToString(e.FormattedValue),Theme.UI(8),e.CellBounds,Color.White,ContentAlignment.MiddleLeft);e.Handled=true;};
   tree.ItemHeight=32;tree.ShowLines=false;tree.ShowPlusMinus=false;tree.ShowRootLines=false;tree.FullRowSelect=true;
   potentials.RowTemplate.Height=34;
   potentials.RowsAdded+=(s,e)=>RequestResponsiveLayout();potentials.RowsRemoved+=(s,e)=>RequestResponsiveLayout();
   foreach(TabPage page in tabs.TabPages)page.Paint+=(s,e)=>Theme.Frame(e.Graphics,((Control)s).ClientRectangle,false);
   foreach(Control bar in tabs.TabPages[1].Controls.Cast<Control>().Where(x=>x.Dock==DockStyle.Bottom).ToArray())bar.SendToBack();
   var moveTable=pMoves.Parent as TableLayoutPanel;if(moveTable!=null)moveTable.RowStyles[moveTable.GetRow(pMoves)].Height=80;
   pMoves.TextChanged+=(s,e)=>RequestResponsiveLayout();
   Resize+=(s,e)=>RequestResponsiveLayout();
   Theme.Enlarge(this);
   Shown+=(s,e)=>{var host=tree.Parent.Parent as SplitContainer;if(host!=null)host.SplitterDistance=200;var pokemonSplit=tabs.TabPages[1].Controls.OfType<SplitContainer>().FirstOrDefault();if(pokemonSplit!=null)pokemonSplit.SplitterDistance=(int)(pokemonSplit.Width*.60);RequestResponsiveLayout();};
  }
  void InstallSelector(PixelTextBox box,string title,Action action){var table=box.Parent as TableLayoutPanel;if(table==null)return;int column=table.GetColumn(box),row=table.GetRow(box);var panel=new Panel{Dock=DockStyle.Fill};table.Controls.Remove(box);var button=Button(title,105,action);button.Dock=DockStyle.Right;panel.Controls.Add(box);panel.Controls.Add(button);table.Controls.Add(panel,column,row);}
  void InstallPixelChoice(PixelComboBox combo){if(combo==null)return;var table=combo.Parent as TableLayoutPanel;if(table==null)return;int row=table.GetRow(combo),column=table.GetColumn(combo);var choice=new FlatButton{Dock=DockStyle.Fill,Font=Theme.UI(8),Text=combo.Text+" ▼"};table.Controls.Remove(combo);choice.Controls.Add(combo);combo.Visible=false;table.Controls.Add(choice,column,row);combo.SelectedIndexChanged+=(s,e)=>choice.Text=combo.Text+" ▼";choice.Click+=(s,e)=>{using(var dialog=new SearchToggleForm("선택 · 검색 / SPACE",combo.Items.Cast<object>().Select(x=>x.ToString()),new[]{combo.Text},1))if(dialog.ShowDialog(this)==DialogResult.OK&&dialog.Selected.Length>0)combo.SelectedItem=dialog.Selected[0];};}
  void StyleEditor(Control root){
   foreach(Control c in root.Controls){
    if(c is PixelTextBox){c.Font=Theme.Input();c.ImeMode=ImeMode.NoControl;}
    else if(c is TreeView||c is PixelComboBox||c is PixelNumber)c.Font=Theme.Input();
    var combo=c as PixelComboBox;if(combo!=null){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Space){combo.DroppedDown=!combo.DroppedDown;e.Handled=e.SuppressKeyPress=true;}};}
    var grid=c as DataGridView;if(grid!=null){grid.DefaultCellStyle.Font=Theme.Input();grid.DefaultCellStyle.SelectionBackColor=Color.Black;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.GridColor=Color.Black;grid.RowTemplate.Height=32;grid.EditingControlShowing+=(s,e)=>{e.Control.Font=Theme.Input();e.Control.ImeMode=ImeMode.NoControl;};}
    
    StyleEditor(c);
   }
  }
  void BuildInlinePotentials(){
   // 포텐셜 탭에는 표만 남긴다. 트리거·효과 빈칸 채우기 화면은 「포텐셜 도감」 탭에 보관해 두었다가
   // 포텐셜 편집 창이 열릴 때 그 창으로 옮겨 쓰고, 닫으면 돌려놓는다
   potentials.Columns["raw"].Visible=false;potentials.Columns["targetType"].Visible=false;
   var catalog=tabs.TabPages[5];
   foreach(var c in catalog.Controls.Cast<Control>().ToArray()){
    if(c is TableLayoutPanel){
     inlineCatalog=c as TableLayoutPanel;
     foreach(Control panel in c.Controls)foreach(Control table in panel.Controls){var layout=table as TableLayoutPanel;if(layout!=null&&layout.ColumnStyles.Count>0){layout.Padding=new Padding(4);layout.ColumnStyles[0].Width=105;}}
    }else{c.Visible=false;}
   }
  }
  bool navigating;
  void EmbedPotentialsInPokemon(){
   var pokemon=tabs.TabPages[1];var potential=tabs.TabPages[2];
   foreach(Control bar in pokemon.Controls.Cast<Control>().Where(x=>x is FlowLayoutPanel&&x.Controls.Cast<Control>().Any(y=>y.Text.Contains("배틀 슬롯"))).ToArray()){pokemon.Controls.Remove(bar);bar.Dispose();}
   pokemonWorkspace=new PixelScrollPanel{Dock=DockStyle.Fill};
   var stack=new TableLayoutPanel{Dock=DockStyle.Top,Height=1200,ColumnCount=1,RowCount=2,Padding=new Padding(8)};workspaceStack=stack;
   stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   stack.RowStyles.Add(new RowStyle(SizeType.Absolute,360));stack.RowStyles.Add(new RowStyle(SizeType.Absolute,820));
   var info=new Panel{Dock=DockStyle.Fill};pokemonInfo=info;trainerInfo=new Panel{Dock=DockStyle.Fill};
   foreach(Control c in tabs.TabPages[0].Controls.Cast<Control>().ToArray()){trainerInfo.Controls.Add(c);c.Visible=true;}
   var host=new Panel{Dock=DockStyle.Fill,Tag="potential-host"};
   foreach(Control c in pokemon.Controls.Cast<Control>().ToArray()){info.Controls.Add(c);c.Visible=true;}
   foreach(Control c in potential.Controls.Cast<Control>().ToArray()){host.Controls.Add(c);c.Visible=true;}
   stack.Controls.Add(info,0,0);stack.Controls.Add(host,0,1);pokemonWorkspace.Content=stack;
   tabs.Parent.Controls.Add(pokemonWorkspace);pokemonWorkspace.Visible=false;
  }
  void ShowEditorPage(int page){
   if(page==2||page==5)page=1;
   if(navigating)return;navigating=true;
   try{SelectRecordForPage(page);
    tabs.SelectedIndex=page;
    bool integrated=(page==1||page==0)&&pokemonWorkspace!=null;
    if(integrated){var wanted=page==0?trainerInfo:pokemonInfo;var other=page==0?pokemonInfo:trainerInfo;if(other.Parent==workspaceStack)workspaceStack.Controls.Remove(other);if(wanted.Parent!=workspaceStack)workspaceStack.Controls.Add(wanted,0,0);wanted.Visible=true;}
    if(pokemonWorkspace!=null)pokemonWorkspace.Visible=integrated;
    tabs.Visible=!integrated;
    if(integrated)pokemonWorkspace.BringToFront();else tabs.BringToFront();
    int index=Array.IndexOf(editorPages,page);if(index>=0&&editorMenu.SelectedIndex!=index)editorMenu.SelectedIndex=index;
   }finally{navigating=false;}
  }

  void RequestResponsiveLayout(){
   if(loading||IsDisposed||!IsHandleCreated||responsiveLayoutPending)return;
   responsiveLayoutPending=true;
   BeginInvoke((Action)(()=>{responsiveLayoutPending=false;if(!IsDisposed)UpdateResponsiveLayout();}));
  }

  void UpdateResponsiveLayout(){
   if(IsDisposed)return;
   Control moveHost=pMoves;
   while(moveHost!=null&&!(moveHost.Parent is TableLayoutPanel))moveHost=moveHost.Parent;
   var moveTable=moveHost==null?null:moveHost.Parent as TableLayoutPanel;
   int moveLines=pMoves==null?1:Math.Max(1,pMoves.Lines.Length);
   if(moveTable!=null){
    int row=moveTable.GetRow(moveHost);
    moveTable.RowStyles[row].Height=Math.Min(320,Math.Max(110,40+moveLines*30));
    moveTable.PerformLayout();
   }
   foreach(var stat in stats){
    var statTable=stat==null?null:stat.Parent as TableLayoutPanel;
    if(statTable!=null)statTable.RowStyles[statTable.GetRow(stat)].Height=46;
   }
   int potentialRows=potentials==null?0:potentials.Rows.Cast<DataGridViewRow>().Count(x=>!x.IsNewRow);
   if(workspaceStack!=null){
    int infoHeight=Math.Min(1200,Math.Max(900,800+moveLines*30));
    int potentialHeight=Math.Min(1500,Math.Max(620,170+potentialRows*56));
    workspaceStack.RowStyles[0].Height=infoHeight;
    workspaceStack.RowStyles[1].Height=potentialHeight;
    workspaceStack.Height=workspaceStack.Padding.Vertical+infoHeight+potentialHeight;
   }
   GrowEditorWindow();
  }

  void GrowEditorWindow(){
   if(!IsHandleCreated||WindowState==FormWindowState.Maximized)return;
   int desiredWidth=Width;
   if(potentials!=null)desiredWidth=Math.Max(desiredWidth,174+potentials.Columns.Cast<DataGridViewColumn>().Where(x=>x.Visible).Sum(x=>x.Width)+70);
   int desiredHeight=Height;
   if(pokemonWorkspace!=null&&pokemonWorkspace.Visible&&workspaceStack!=null)desiredHeight=Math.Max(desiredHeight,workspaceStack.Height+130);
   var area=Screen.FromControl(this).WorkingArea;
   int maxWidth=Math.Max(MinimumSize.Width,area.Width-24),maxHeight=Math.Max(MinimumSize.Height,area.Height-24);
   int width=Math.Min(maxWidth,desiredWidth),height=Math.Min(maxHeight,desiredHeight);
   if(width>Width||height>Height)Size=new Size(Math.Max(Width,width),Math.Max(Height,height));
  }

  void SelectRecordForPage(int page){
   if((page!=0&&page!=1)||tree==null||project==null)return;bool pokemon=page==1;object selected=Current;
   if((pokemon&&selected is PokemonRecord)||(!pokemon&&selected is TrainerRecord))return;
   object target=pokemon?project.pokemon.Cast<object>().FirstOrDefault():project.trainers.Cast<object>().FirstOrDefault();if(target==null)return;
   SaveCurrent();TreeNode node=tree.Nodes.Cast<TreeNode>().SelectMany(x=>x.Nodes.Cast<TreeNode>()).FirstOrDefault(x=>Object.ReferenceEquals(x.Tag,target));if(node!=null)tree.SelectedNode=node;
  }
  void TestRetroEditor(){
   ShowEditorPage(1);Application.DoEvents();
   if(!pokemonWorkspace.Visible||!potentials.Visible)throw new Exception("Pokemon editor and potential table must be visible together");
   var trainerNode=tree.Nodes.Cast<TreeNode>().SelectMany(x=>x.Nodes.Cast<TreeNode>()).First(x=>x.Tag is TrainerRecord);
   tree.SelectedNode=trainerNode;Application.DoEvents();ShowEditorPage(1);Application.DoEvents();
   if(!(Current is PokemonRecord)||!EditorTemplate.PokemonEditSlotNames.Contains("역할")||!EditorTemplate.PokemonEditSlotNames.Contains(TeamRules.ExclusiveSlot))throw new Exception("Pokemon potential slots must follow Pokemon navigation");
   triggerBuilder.Select("trigger.weather");triggerBuilder.SetValue("장소","쾌청");if(ConfiguredTrigger()!="날씨가 「쾌청」일 때")throw new Exception("Weather slot editing");
   triggerBuilder.Select("trigger.hpchange.이하",new Dictionary<string,string>{{"분자","1"},{"분모","4"}});if(ConfiguredTrigger()!="자신의 체력이 1/4 이하가 되었을 때")throw new Exception("HP threshold slot editing");
   var row=potentials.Rows.Cast<DataGridViewRow>().First(x=>!x.IsNewRow&&Cell(x,"slot")=="역할");potentials.CurrentCell=row.Cells["name"];
   string name=Cell(row,"name");var original=DraftFromRow(row);
   // 편집 창: 입력은 창 안에서만 일어나고, 취소하면 표는 그대로다
   var window=OpenPotentialWindowForTest(original);
   window.ChooseName("귀인");
   if(!window.EffectBox.Text.Contains("공격")||window.TriggerBox.Text.Length==0)throw new Exception("Template must populate trigger and effect in the window");
   window.EffectBox.Text="한글 입력 검증";
   if(Cell(row,"name")!=name||Cell(row,"effect")!=original.Effect)throw new Exception("Table must not change while the window is open");
   window.Cancel();window.Dispose();
   if(Cell(row,"name")!=name||Cell(row,"effect")!=original.Effect)throw new Exception("Cancel must discard window edits");
   window=OpenPotentialWindowForTest(DraftFromRow(row));
   window.ChooseName("탐사대원");
   string explorerEffect=EffectNormalizer.CanonicalRank("자신의 임의의 능력",1);
   if(window.TriggerBox.Text!=TriggerStructures.EntryLabel||window.EffectBox.Text!=explorerEffect||triggerBuilder.Module.Expected!="등장"||ConfiguredTrigger()!=TriggerStructures.EntryLabel||effectBuilder.Module.Id!="effect.rank.raise1"||effectBuilder.GetValue("대상")!="자신의 임의의 능력")throw new Exception("Structured explorer role template loading: ["+window.TriggerBox.Text+"] ["+window.EffectBox.Text+"] ["+triggerBuilder.Module.Expected+"] ["+ConfiguredTrigger()+"] ["+effectBuilder.Module.Id+"] ["+effectBuilder.GetValue("대상")+"]");
   window.ChooseName("추적대원");
   string trackerEffect=EffectNormalizer.CanonicalSwitchRestriction()+" (필요 「통솔：A」 이상)";
   if(window.TriggerBox.Text!=TriggerStructures.FieldLabel||window.EffectBox.Text!=trackerEffect||ConfiguredTrigger()!=TriggerStructures.FieldLabel||effectBuilder.Module.Id!="effect.switch.restrict"||ConfiguredEffect()!=EffectNormalizer.CanonicalSwitchRestriction())throw new Exception("Structured tracker role template loading");
   window.ChooseName("야생 포켓몬");
   if(window.TriggerBox.Text!=TriggerStructures.EntryLabel||window.EffectBox.Text!=EffectNormalizer.CanonicalRank("자신의 임의의 능력",2)||effectBuilder.Module.Id!="effect.rank.raise2"||effectBuilder.GetValue("대상")!="자신의 임의의 능력")throw new Exception("Structured wild random role template loading");
   window.ChooseName("야생의 두목");
   if(window.TriggerBox.Text!=TriggerStructures.EntryLabel||window.EffectBox.Text!=EffectNormalizer.CanonicalRank("자신의 전능력치",2)||effectBuilder.Module.Id!="effect.rank.raise2"||effectBuilder.GetValue("대상")!="자신의 전능력치")throw new Exception("Structured wild boss role template loading");
   window.ChooseName("수렵대원");
   string hunterEffect=EffectNormalizer.CanonicalStatIgnore("『야생의 두목』","능력","상승(강화)")+" (필요 「통솔：A」 이상)";
   if(window.TriggerBox.Text!=TriggerStructures.FieldLabel||window.EffectBox.Text!=hunterEffect||effectBuilder.Module.Id!="effect.stat.ignore.상승(강화)"||effectBuilder.GetValue("대상")!="『야생의 두목』"||effectBuilder.GetValue("능력")!="능력"||ConfiguredEffect()!=EffectNormalizer.CanonicalStatIgnore("『야생의 두목』","능력","상승(강화)"))throw new Exception("Structured hunter stat-ignore template loading");
   window.ChooseName("조사대원");
   string researcherEffect=EffectNormalizer.CanonicalAnalysisPermission()+" (필요 「통솔：A」 이상)";
   if(window.TriggerBox.Text!=TriggerStructures.SelfAndOpponentEntryLabel||window.EffectBox.Text!=researcherEffect||triggerBuilder.Module.Id!="trigger.entry.both"||effectBuilder.Module.Id!="effect.analysis.permission"||ConfiguredEffect()!=EffectNormalizer.CanonicalAnalysisPermission())throw new Exception("Structured researcher analysis template loading");
   window.Cancel();window.Dispose();
   // 확인하면 표에 한 번 반영되고, 원래 이름으로 되돌리면 짝이 되는 부수 포텐셜도 맞춰진다
   window=OpenPotentialWindowForTest(DraftFromRow(row));
   window.ChooseName("탐사대원");window.Accept();
   var confirmed=window.Result;window.Dispose();
   if(confirmed==null)throw new Exception("Confirmed window must return its result");
   CommitPotentialRow(row,original,confirmed);
   if(Cell(row,"name")!="탐사대원"||Cell(row,"effect")!=explorerEffect)throw new Exception("Confirm must write the window result to the table");
   CommitPotentialRow(row,DraftFromRow(row),original);
   if(Cell(row,"name")!=name||EffectNormalizer.NormalizeLines(Cell(row,"effect"))!=EffectNormalizer.NormalizeLines(original.Effect))throw new Exception("Restoring the original potential failed");
   if(editorMenu.Items.Count!=8||potentials.Columns["targetType"].Visible||editWindow!=null)throw new Exception("Integrated menu configuration");
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
   if(keyData==Keys.F6){editorMenu.Focus();return true;}
   Control focused=ActiveControl;while(focused is ContainerControl&&((ContainerControl)focused).ActiveControl!=null)focused=((ContainerControl)focused).ActiveControl;
   if(focused is TextBoxBase||focused is PixelNumber||focused is PixelComboBox||focused is DataGridView||focused is TreeView||focused is ListBox)return base.ProcessCmdKey(ref msg,keyData);
   if(keyData==Keys.Down||keyData==Keys.Right)return SelectNextControl(focused,true,true,true,true);
   if(keyData==Keys.Up||keyData==Keys.Left)return SelectNextControl(focused,false,true,true,true);
   return base.ProcessCmdKey(ref msg,keyData);
  }
 }
}
