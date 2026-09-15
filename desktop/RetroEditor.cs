using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AABattle {
 class RetroFrame:Panel {
  public RetroFrame(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(12);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.Frame(e.Graphics,ClientRectangle,false);}
 }
 partial class DataEditorForm {
  TextBox inlineTrigger,inlineEffect;
  Label inlineTitle;
  bool inlineLoading;
  ListBox editorMenu;
  int[] editorPages={6,0,1,2,4,3,7,8};
  void BuildRetroEditor(){
   Size=new Size(1480,960);MinimumSize=new Size(1200,820);Font=Theme.UI(9);ImeMode=ImeMode.NoControl;
   tabs.Appearance=TabAppearance.FlatButtons;tabs.SizeMode=TabSizeMode.Fixed;tabs.ItemSize=new Size(1,1);
   var menuFrame=new RetroFrame{Dock=DockStyle.Left,Width=174,Padding=new Padding(12,20,12,12)};
   editorMenu=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=48,Font=Theme.UI(11),IntegralHeight=false};
   editorMenu.Items.AddRange(new object[]{"팀 보관함","트레이너","포켓몬","포텐셜","기술 도감","시트 출력","상태·날씨","효과 안내"});
   editorMenu.DrawItem+=(s,e)=>{if(e.Index<0)return;e.Graphics.FillRectangle(Brushes.White,e.Bounds);bool selected=(e.State&DrawItemState.Selected)!=0;Theme.Text(e.Graphics,(selected?"▶ ":"   ")+editorMenu.Items[e.Index],editorMenu.Font,e.Bounds,Color.Black,ContentAlignment.MiddleLeft);};
   editorMenu.SelectedIndexChanged+=(s,e)=>{if(editorMenu.SelectedIndex>=0)tabs.SelectedIndex=editorPages[editorMenu.SelectedIndex];};
   editorMenu.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){tabs.SelectedIndex=editorPages[Math.Max(0,editorMenu.SelectedIndex)];tabs.SelectedTab.SelectNextControl(null,true,true,true,false);e.Handled=e.SuppressKeyPress=true;}};
   menuFrame.Controls.Add(editorMenu);Controls.Add(menuFrame);menuFrame.SendToBack();
   var hint=Theme.Label("방향키: 이동   SPACE: 선택   TAB: 다음 입력   F6: 메뉴   /   한글·마우스 입력",32);hint.Dock=DockStyle.Bottom;Controls.Add(hint);hint.SendToBack();
   tabs.SelectedIndexChanged+=(s,e)=>{if(tabs.SelectedIndex==5){tabs.SelectedIndex=2;return;}int index=Array.IndexOf(editorPages,tabs.SelectedIndex);if(index>=0&&editorMenu.SelectedIndex!=index)editorMenu.SelectedIndex=index;};
   BuildInlinePotentials();StyleEditor(this);aa.Font=new Font(Fonts.AA,16,FontStyle.Regular,GraphicsUnit.Pixel);tAA.Font=aa.Font;
   tree.ItemHeight=32;tree.ShowLines=false;tree.ShowPlusMinus=false;tree.ShowRootLines=false;tree.FullRowSelect=true;
   foreach(TabPage page in tabs.TabPages)page.Paint+=(s,e)=>Theme.Frame(e.Graphics,((Control)s).ClientRectangle,false);
   foreach(Control bar in tabs.TabPages[1].Controls.Cast<Control>().Where(x=>x.Dock==DockStyle.Bottom).ToArray())bar.SendToBack();
   var moveTable=pMoves.Parent as TableLayoutPanel;if(moveTable!=null)moveTable.RowStyles[moveTable.GetRow(pMoves)].Height=80;
   Shown+=(s,e)=>{var host=tree.Parent.Parent as SplitContainer;if(host!=null)host.SplitterDistance=170;var pokemonSplit=tabs.TabPages[1].Controls.OfType<SplitContainer>().FirstOrDefault();if(pokemonSplit!=null)pokemonSplit.SplitterDistance=(int)(pokemonSplit.Width*.60);};
  }
  void StyleEditor(Control root){
   foreach(Control c in root.Controls){
    if(c is TextBox){c.Font=new Font("맑은 고딕",11);c.ImeMode=ImeMode.NoControl;}
    else if(c is TreeView||c is ComboBox||c is NumericUpDown)c.Font=new Font("맑은 고딕",10);
    var combo=c as ComboBox;if(combo!=null){combo.DrawMode=DrawMode.Normal;combo.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Space){combo.DroppedDown=!combo.DroppedDown;e.Handled=e.SuppressKeyPress=true;}};}
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
   potentials.Columns["raw"].Visible=false;potentials.Columns["trigger"].Width=220;potentials.Columns["effect"].Width=270;
   var lower=new RetroFrame{Dock=DockStyle.Fill};split.Panel2.Controls.Add(lower);
   inlineTitle=Theme.Label("포텐셜 선택 → 아래에서 조건과 효과 편집",30);lower.Controls.Add(inlineTitle);
   var inputs=new TableLayoutPanel{Dock=DockStyle.Top,Height=118,ColumnCount=2,RowCount=2};inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));inputs.RowStyles.Add(new RowStyle(SizeType.Absolute,28));inputs.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   inlineTrigger=Box(true);inlineEffect=Box(true);inputs.Controls.Add(Theme.Label("트리거 조건",26),0,0);inputs.Controls.Add(Theme.Label("효과",26),1,0);inputs.Controls.Add(inlineTrigger,0,1);inputs.Controls.Add(inlineEffect,1,1);
   lower.Controls.Add(inputs);inputs.BringToFront();
   var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};lower.Controls.Add(scroll);scroll.BringToFront();
   var catalog=tabs.TabPages[5];foreach(var c in catalog.Controls.Cast<Control>().ToArray()){if(c is TableLayoutPanel){c.Dock=DockStyle.Top;c.Height=690;scroll.Controls.Add(c);foreach(Control panel in c.Controls)foreach(Control table in panel.Controls){var layout=table as TableLayoutPanel;if(layout!=null&&layout.ColumnStyles.Count>0){layout.Padding=new Padding(4);layout.ColumnStyles[0].Width=105;}}}else{c.Visible=false;}}
   inlineTrigger.TextChanged+=(s,e)=>WriteInline("trigger",inlineTrigger.Text);inlineEffect.TextChanged+=(s,e)=>WriteInline("effect",inlineEffect.Text);
   potentials.SelectionChanged+=(s,e)=>RefreshInline();potentials.CellValueChanged+=(s,e)=>RefreshInline();
  }
  void RefreshInline(){if(inlineTrigger==null||inlineLoading)return;var row=SelectedPotentialRow();inlineLoading=true;inlineTrigger.Enabled=inlineEffect.Enabled=row!=null;inlineTitle.Text=row==null?"위 목록에서 포텐셜을 선택하세요":"『"+Cell(row,"name")+"』 · "+Cell(row,"slot");inlineTrigger.Text=row==null?"":Cell(row,"trigger");inlineEffect.Text=row==null?"":Cell(row,"effect");inlineLoading=false;}
  void WriteInline(string column,string text){if(inlineLoading||loading)return;var row=SelectedPotentialRow();if(row==null)return;inlineLoading=true;row.Cells[column].Value=text;GridChanged();inlineLoading=false;}
  void TestRetroEditor(){
   tabs.SelectedIndex=2;var row=potentials.Rows.Cast<DataGridViewRow>().First(x=>!x.IsNewRow&&Cell(x,"slot")=="역할");potentials.CurrentCell=row.Cells["name"];
   string name=Cell(row,"name");row.Cells["name"].Value="귀인";RefreshInline();
   if(!inlineEffect.Text.Contains("공격")||inlineTrigger.Text.Length==0)throw new Exception("Template must populate inline trigger and effect");
   inlineEffect.Text="한글 입력 검증";if(Cell(row,"effect")!="한글 입력 검증")throw new Exception("Inline Korean text must persist");
   row.Cells["name"].Value=name;RefreshInline();if(inlineEffect.Text!=Cell(row,"effect")||inlineTrigger.Text!=Cell(row,"trigger"))throw new Exception("Template and inline editor synchronization");
   if(editorMenu.Items.Count!=8||inlineEffect.ImeMode==ImeMode.Disable)throw new Exception("Menu and IME configuration");
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
