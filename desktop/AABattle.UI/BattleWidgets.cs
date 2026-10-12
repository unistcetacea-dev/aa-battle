using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace AABattle {
 // 배틀 화면 부품: 글자·버튼·카드·메뉴·로그
 class PixelLabel:Label {
  public PixelLabel(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){Theme.Text(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextAlign);}
 }
 // Battle message window: text types out like the games, then a blinking down arrow waits.
 class MessageLabel:PixelLabel {
  Timer timer=new Timer{Interval=24};int shown,ticks;
  public MessageLabel(){timer.Tick+=(s,e)=>{ticks++;if(shown<Text.Length)shown=Math.Min(Text.Length,shown+2);Invalidate();};}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);shown=Theme.Instant?Text.Length:0;ticks=0;if(!Theme.Instant)timer.Start();Invalidate();}
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible)timer.Stop();else if(!Theme.Instant)timer.Start();}
  protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.White);Theme.Frame(e.Graphics,new Rectangle(0,0,Width,Height),false);string visible=Text.Substring(0,Math.Min(Text.Length,Theme.Instant?Text.Length:shown));Theme.Text(e.Graphics,visible,Font,new Rectangle(18,4,Width-46,Height-8),Color.Black,ContentAlignment.MiddleLeft);if(shown>=Text.Length&&Text.Length>0&&(Theme.Instant||ticks/18%2==0))Theme.MoreArrow(e.Graphics,Width-22,Height/2-1,Color.Black);}
 }
 class FlatButton:Button {
  public bool Selected,Primary;public string Subtext="";bool hot;
  public FlatButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.White;ForeColor=Color.Black;Font=Theme.UI();Cursor=Cursors.Hand;}
  protected override void OnMouseEnter(EventArgs e){hot=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hot=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
  protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.None;g.Clear(Color.White);if(Width<8||Height<8)return;bool pressed=Enabled&&ClientRectangle.Contains(PointToClient(MousePosition))&&MouseButtons==MouseButtons.Left;int off=pressed?2:0;bool pointing=Enabled&&(hot||Focused||Selected);bool roomy=Width>=96;bool inverted=Primary||Selected||(pointing&&!roomy);
   var r=new Rectangle(off,off,Math.Max(1,Width-3),Math.Max(1,Height-3));if(!pressed)using(var shade=new HatchBrush(HatchStyle.Percent50,Color.Black,Color.Transparent))Theme.Pixels(g,shade,new Rectangle(3,3,Width-3,Height-3),new[]{2,1});Theme.Box(g,r,inverted);if(Primary&&r.Width>12&&r.Height>12){Theme.Pixels(g,Brushes.White,Theme.Inset(r,3),new[]{1});Theme.Pixels(g,Brushes.Black,Theme.Inset(r,4),new int[0]);}
   Color ink=inverted?Color.White:Color.Black;int left=pointing&&roomy?22:6;if(pointing&&roomy)Theme.Cursor(g,8+off,(Subtext.Length>0?Height/3:Height/2)+off,ink);
   var rect=new Rectangle(left+off,Subtext.Length>0?4+off:off,Width-left-10,Subtext.Length>0?Height/2-2:Height-3);Theme.Text(g,Text,Font,rect,ink,ContentAlignment.MiddleCenter);if(Subtext.Length>0)using(var font=Theme.UI(8))Theme.Text(g,Subtext,font,new Rectangle(left+off,Height/2+off,Width-left-10,Height/2-6),ink,ContentAlignment.MiddleCenter);if(!Enabled)using(var veil=new HatchBrush(HatchStyle.Percent50,Color.White,Color.Transparent))g.FillRectangle(veil,Theme.Inset(r,3));}
 }
 class Card:Panel {
  public Card(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.White;Padding=new Padding(16);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Color.White);Theme.Frame(e.Graphics,new Rectangle(0,0,Width,Height));}
 }
 // Vertical game menu with a cursor. Arrow keys move, Enter/Space/Z choose, the mouse points and clicks.
 class GameMenu:Control {
  string[] items=new string[0];int index;public event Action<int> Activated;
  public string[] Items{get{return items;}set{items=value??new string[0];index=Math.Max(0,Math.Min(index,items.Length-1));Invalidate();}}
  public int SelectedIndex{get{return index;}set{index=Math.Max(0,Math.Min(items.Length-1,value));Invalidate();}}
  public int RowHeight{get{return Font.Height+18;}}
  public int PreferredHeight{get{return 28+items.Length*RowHeight;}}
  public GameMenu(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);TabStop=true;BackColor=Color.White;Font=Theme.UI(16);Cursor=Cursors.Hand;}
  int RowAt(int y){int row=(y-14)/RowHeight;return y<14||row>=items.Length?-1:row;}
  protected override void OnMouseMove(MouseEventArgs e){int row=RowAt(e.Y);if(row>=0&&row!=index){index=row;Invalidate();}base.OnMouseMove(e);}
  protected override void OnMouseClick(MouseEventArgs e){Focus();int row=RowAt(e.Y);if(row>=0){index=row;Invalidate();if(Activated!=null)Activated(row);}base.OnMouseClick(e);}
  protected override bool IsInputKey(Keys key){return key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){if(items.Length>0){if(e.KeyCode==Keys.Up){index=(index+items.Length-1)%items.Length;Invalidate();e.Handled=true;}else if(e.KeyCode==Keys.Down){index=(index+1)%items.Length;Invalidate();e.Handled=true;}else if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space||e.KeyCode==Keys.Z){e.Handled=true;if(Activated!=null)Activated(index);}}base.OnKeyDown(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Color.White);Theme.Frame(g,ClientRectangle);for(int i=0;i<items.Length;i++){int y=14+i*RowHeight;if(i==index)Theme.Cursor(g,26,y+RowHeight/2,Color.Black,2);Theme.Text(g,items[i],Font,new Rectangle(52,y,Math.Max(1,Width-70),RowHeight),Color.Black,ContentAlignment.MiddleLeft);}}
 }
 class AAView:Panel {
  string art="";public float TextSize=15;public bool Fit=true;
  public string Art {get{return art;}set{art=value??"";Invalidate();}}
  public AAView(){DoubleBuffered=true;BackColor=Color.White;AutoScroll=true;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;string[] lines=art.Replace("\r","").Split('\n');if(string.IsNullOrWhiteSpace(art)){TextRenderer.DrawText(e.Graphics,"AA를 붙여 넣어 주세요",Theme.UI(),ClientRectangle,Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}using(var f=new Font(Fonts.AA,TextSize,FontStyle.Regular,GraphicsUnit.Pixel)){var format=(StringFormat)StringFormat.GenericTypographic.Clone();format.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces|StringFormatFlags.NoWrap;float width=lines.Max(s=>e.Graphics.MeasureString(s,f,int.MaxValue,format).Width),height=lines.Length*(TextSize+1);float availableWidth=Math.Max(1,ClientSize.Width-18),availableHeight=Math.Max(1,ClientSize.Height-18);float scale=Fit?Math.Min(1,Math.Min(availableWidth/Math.Max(1,width),availableHeight/Math.Max(1,height))):1;AutoScrollMinSize=Fit?Size.Empty:new Size((int)width+20,(int)height+20);float x=Fit?Math.Max(8,(ClientSize.Width-width*scale)/2):8+AutoScrollPosition.X,y=Fit?Math.Max(8,(ClientSize.Height-height*scale)/2):8+AutoScrollPosition.Y;e.Graphics.TranslateTransform(x,y);e.Graphics.ScaleTransform(scale,scale);for(int i=0;i<lines.Length;i++)e.Graphics.DrawString(lines[i],f,Brushes.Black,new PointF(0,i*(TextSize+1)),format);format.Dispose();}}
 }
 class PokemonCard:Card {
  public Label Title,Meta,HP;public AAView Art;public FlatButton Edit,State;public Label StateLine;double hpRatio=1,shownRatio=1;Timer drain=new Timer{Interval=16};
  public PokemonCard(int side){Title=new PixelLabel{Font=Theme.UI(16),ForeColor=Theme.Ink,BackColor=Color.White};Meta=new PixelLabel{Font=Theme.UI(8),ForeColor=Theme.Muted,BackColor=Color.White};HP=new PixelLabel{Font=Theme.UI(8),TextAlign=ContentAlignment.MiddleRight,ForeColor=Theme.Muted,BackColor=Color.White};Edit=new FlatButton{Text="편집",Font=Theme.UI(8)};State=new FlatButton{Text="상태",Font=Theme.UI(8)};StateLine=new PixelLabel{Font=Theme.UI(8),BackColor=Color.White};Art=new AAView();Controls.AddRange(new Control[]{Title,Meta,HP,Edit,State,StateLine,Art});
   // HP drains toward the new value like the games instead of jumping.
   drain.Tick+=(s,e)=>{double gap=hpRatio-shownRatio,step=Math.Max(.006,Math.Abs(gap)*.12);if(Math.Abs(gap)<=step){shownRatio=hpRatio;drain.Stop();}else shownRatio+=gap>0?step:-step;Invalidate(new Rectangle(0,76,Width,28));};}
  protected override void Dispose(bool disposing){if(disposing)drain.Dispose();base.Dispose(disposing);}
  protected override void OnResize(EventArgs e){base.OnResize(e);if(Title==null)return;int width=Math.Max(1,Width),height=Math.Max(1,Height);Title.SetBounds(22,14,Math.Max(1,width-172),38);State.SetBounds(Math.Max(0,width-142),18,58,32);Edit.SetBounds(Math.Max(0,width-80),18,58,32);Meta.SetBounds(24,54,Math.Max(1,width-52),20);StateLine.SetBounds(24,104,Math.Max(1,width-200),20);HP.SetBounds(Math.Max(0,width-178),104,150,20);Art.SetBounds(16,130,Math.Max(1,width-36),Math.Max(1,height-152));}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.HpBar(e.Graphics,new Rectangle(22,80,Math.Max(1,Width-50),20),shownRatio);}
  public void ShowFighter(Fighter f,int max,int side){Title.Text=f.Data.name;Meta.Text="TEAM "+(side+1)+"  ·  Lv."+f.Data.level+"  ·  "+string.Join(" / ",f.Data.types);HP.Text=f.HP+" / "+max;hpRatio=max>0?(double)f.HP/max:0;if(Theme.Instant||!Visible){shownRatio=hpRatio;drain.Stop();}else if(Math.Abs(hpRatio-shownRatio)>.0001)drain.Start();Art.Art=f.AA;Invalidate();}
 }
 class MoveCard:Card {
  // 사용 불가 사유는 디더에 덮이지 않게 표시한다.
  public class MoveButton:FlatButton {protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Enabled||Width<8||Height<8)return;var area=Theme.Inset(ClientRectangle,5);e.Graphics.FillRectangle(Brushes.White,area);Theme.Text(e.Graphics,Text,Font,new Rectangle(area.Left,4,area.Width,Height/2-2),Color.Black,ContentAlignment.MiddleCenter);using(var font=Theme.UI(8))Theme.Text(e.Graphics,Subtext,font,new Rectangle(area.Left,Height/2,area.Width,Height/2-6),Color.Black,ContentAlignment.MiddleCenter);}}
  public static string RestrictionLabel(string reason){int end=reason.IndexOf("」");return end>=0?reason.Substring(0,end+1):reason;}
  public FlatButton[] Buttons=new FlatButton[4];public event Action<int> SelectedMove;bool large;
  // 기술이 4개보다 많으면 4개씩 넘겨 본다
  FlatButton pager;int page;Fighter shown;Database shownDb;
  public MoveCard(){for(int i=0;i<4;i++){int k=i;Buttons[i]=new MoveButton{Font=Theme.UI(8)};Buttons[i].Click+=(s,e)=>{if(SelectedMove!=null)SelectedMove(page*4+k);};Controls.Add(Buttons[i]);}pager=new FlatButton{Font=Theme.UI(8),Visible=false};pager.Click+=(s,e)=>{page++;if(shown!=null)RefreshMoves(shown,shownDb);};Controls.Add(pager);}
  protected override void OnResize(EventArgs e){base.OnResize(e);int top=pager!=null&&pager.Visible?40:14;int w=Math.Max(1,(Width-40)/2),h=Math.Max(1,(Height-26-top)/2);bool wantLarge=h>=66;if(pager!=null)pager.SetBounds(Math.Max(0,Width-134),8,120,28);for(int i=0;i<4;i++)if(Buttons[i]!=null){Buttons[i].SetBounds(14+(i%2)*(w+8),top+(i/2)*(h+8),w,h);if(wantLarge!=large)Buttons[i].Font=Theme.UI(wantLarge?16:8);}large=wantLarge;}
  public void RefreshMoves(Fighter f,Database db){if(!ReferenceEquals(shown,f)&&(shown==null||f==null||shown.Data==null||f.Data==null||shown.Data.name!=f.Data.name))page=f!=null&&f.Moves.Length>4?Math.Max(0,f.Selected)/4:0;shown=f;shownDb=db;int pages=Math.Max(1,(f.Moves.Length+3)/4);page%=pages;bool paging=pages>1;if(pager.Visible!=paging){pager.Visible=paging;OnResize(EventArgs.Empty);}pager.Text="기술 "+(page+1)+"/"+pages+" ▶";for(int i=0;i<4;i++){int index=page*4+i;string name=index<f.Moves.Length?f.Moves[index]:"";var m=db.moves.FirstOrDefault(x=>x.name==name);Buttons[i].Text=name.Length>0?name:"—";Buttons[i].Subtext=m==null?"기술 편집에서 등록":string.Join("/",m.types)+" · "+m.category+" · "+(m.power>0?m.power.ToString():"—");string unavailable=m==null?"":Mechanics.MoveBlock(f,m);if(unavailable.Length>0)Buttons[i].Subtext=RestrictionLabel(unavailable);Buttons[i].Enabled=m!=null&&unavailable.Length==0;Buttons[i].Selected=f.Selected==index;Buttons[i].Invalidate();}}
 }
 class FieldEffectBadge:Panel {
  string summary="없음";public string Summary{get{return summary;}set{summary=value??"없음";Invalidate();}}
  public FieldEffectBadge(){DoubleBuffered=true;BackColor=Color.White;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;Theme.Frame(g,new Rectangle(0,0,Width,Height),false);var head=new Rectangle(6,6,Math.Max(1,Width-12),24);Theme.Pixels(g,Brushes.Black,head,new[]{1});using(var font=Theme.UI(8)){Theme.Text(g,"필드효과",font,head,Color.White,ContentAlignment.MiddleCenter);Theme.Text(g,summary,font,new Rectangle(8,32,Math.Max(1,Width-16),Math.Max(1,Height-38)),Color.Black,ContentAlignment.MiddleCenter);}}
 }
 class LogView:Panel {
  string content="";public LogView(){DoubleBuffered=true;AutoScroll=true;BackColor=Color.White;}
  public void SetLog(string log){content=log;AutoScrollPosition=Point.Empty;Invalidate();}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;float y=AutoScrollPosition.Y;int width=Math.Max(50,ClientSize.Width-22);using(var f=new Font(Fonts.System,20,FontStyle.Regular,GraphicsUnit.Pixel)){foreach(string line in content.Replace("\r","").Split('\n')){float height=Math.Max(27,e.Graphics.MeasureString(line.Length==0?" ":line,f,width).Height+5);using(var brush=new SolidBrush(Theme.Ink))e.Graphics.DrawString(line,f,brush,new RectangleF(2,y,width,height));y+=height;}}AutoScrollMinSize=new Size(0,(int)y-AutoScrollPosition.Y+10);}
 }
}
