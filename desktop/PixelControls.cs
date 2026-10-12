using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AABattle {
 // Windows' native edit controls rasterize the embedded pixel font through GDI and garble it,
 // so every text-bearing control in the app is drawn with GDI+ here instead.
 static class PixelMeasure {
  static readonly Bitmap surface=new Bitmap(1,1);static readonly Graphics graphics=Graphics.FromImage(surface);
  static readonly Dictionary<string,Dictionary<char,float>> cache=new Dictionary<string,Dictionary<char,float>>();
  public static readonly StringFormat Format=CreateFormat();
  static StringFormat CreateFormat(){var f=(StringFormat)StringFormat.GenericTypographic.Clone();f.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces|StringFormatFlags.NoWrap|StringFormatFlags.NoClip;return f;}
  public static bool IsPixel(Font font){return Fonts.System!=null&&font.FontFamily.Name==Fonts.System.Name;}
  public static TextRenderingHint Hint(Font font){return IsPixel(font)?TextRenderingHint.SingleBitPerPixelGridFit:TextRenderingHint.AntiAliasGridFit;}
  public static float Width(Font font,char c){if(c=='\n'||c=='\r')return 0;if(c=='\t')return Width(font,' ')*4;string key=font.FontFamily.Name+"|"+font.SizeInPoints;Dictionary<char,float> table;if(!cache.TryGetValue(key,out table)){table=new Dictionary<char,float>();cache[key]=table;}float w;if(table.TryGetValue(c,out w))return w;graphics.TextRenderingHint=Hint(font);w=graphics.MeasureString(c.ToString(),font,PointF.Empty,Format).Width;table[c]=w;return w;}
  public static float Width(Font font,string text,int start,int length){float w=0;for(int i=start;i<start+length&&i<text.Length;i++)w+=Width(font,text[i]);return w;}
  public static float Width(Font font,string text){return Width(font,text??"",0,(text??"").Length);}
  public static void Draw(Graphics g,string text,Font font,Color color,float x,float y){if(string.IsNullOrEmpty(text))return;g.TextRenderingHint=Hint(font);using(var b=new SolidBrush(color))g.DrawString(text.Replace('\t',' '),font,b,x,y,Format);}
 }

 class PixelTextBox:Control {
  struct Row {public int Start,Length;public Row(int s,int l){Start=s;Length=l;}}
  string text="",composition="";int caret,anchor,scrollLine,maxLength=32767;float scrollX;bool multiline,wordWrap=true,readOnly,caretOn,dragging,thumbDragging;int thumbGrab;
  ScrollBars scrollBars=ScrollBars.None;BorderStyle borderStyle=BorderStyle.FixedSingle;
  readonly Timer blink=new Timer{Interval=530};List<Row> rows;readonly Stack<KeyValuePair<string,int>> undo=new Stack<KeyValuePair<string,int>>();
  const int Pad=7,Bar=12;
  public PixelTextBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable|ControlStyles.StandardClick|ControlStyles.StandardDoubleClick,true);BackColor=Color.White;ForeColor=Color.Black;base.Font=Theme.Input();Cursor=Cursors.IBeam;TabStop=true;blink.Tick+=(s,e)=>{caretOn=!caretOn;Invalidate();};ContextMenuStrip=BuildMenu();}
  protected override Size DefaultSize{get{return new Size(140,30);}}
  protected override bool CanEnableIme{get{return true;}}
  protected override void Dispose(bool disposing){if(disposing)blink.Dispose();base.Dispose(disposing);}

  public bool Multiline{get{return multiline;}set{multiline=value;Relayout();}}
  public bool WordWrap{get{return wordWrap;}set{wordWrap=value;Relayout();}}
  public bool ReadOnly{get{return readOnly;}set{readOnly=value;Invalidate();}}
  public bool AcceptsReturn{get;set;}
  public bool AcceptsTab{get;set;}
  public bool HideSelection{get;set;}
  public int MaxLength{get{return maxLength;}set{maxLength=Math.Max(0,value);}}
  public ScrollBars ScrollBars{get{return scrollBars;}set{scrollBars=value;Relayout();}}
  public BorderStyle BorderStyle{get{return borderStyle;}set{borderStyle=value;Relayout();}}
  public override string Text{get{return multiline?text.Replace("\n","\r\n"):text;}set{Replace(0,text.Length,Normalize(value),false);caret=anchor=0;scrollLine=0;scrollX=0;undo.Clear();Invalidate();}}
  public string[] Lines{get{return text.Split('\n');}set{Text=string.Join("\r\n",value??new string[0]);}}
  public int TextLength{get{return Text.Length;}}
  public int SelectionStart{get{return Outer(Math.Min(caret,anchor));}set{caret=anchor=Inner(value);EnsureVisible();Invalidate();}}
  public int SelectionLength{get{return Outer(Math.Max(caret,anchor))-Outer(Math.Min(caret,anchor));}set{caret=Inner(SelectionStart+Math.Max(0,value));Invalidate();}}
  public string SelectedText{get{int a=Math.Min(caret,anchor),b=Math.Max(caret,anchor);return text.Substring(a,b-a).Replace("\n",multiline?"\r\n":"\n");}set{Insert(value??"");}}
  public void Select(int start,int length){anchor=Inner(start);caret=Inner(start+Math.Max(0,length));EnsureVisible();Invalidate();}
  public void SelectAll(){anchor=0;caret=text.Length;Invalidate();}
  public void Clear(){Text="";}
  public void AppendText(string value){caret=anchor=text.Length;Insert(Normalize(value),true);}
  public void ScrollToCaret(){EnsureVisible();Invalidate();}
  public void Copy(){string s=SelectedText;if(s.Length>0)Clipboard.SetText(s);}
  public void Cut(){if(readOnly)return;Copy();Insert("");}
  public void Paste(){if(readOnly||!Clipboard.ContainsText())return;Insert(Clipboard.GetText());}
  public void Undo(){if(undo.Count==0)return;var last=undo.Pop();text=last.Key;caret=anchor=Math.Min(last.Value,text.Length);Relayout();OnTextChanged(EventArgs.Empty);EnsureVisible();}

  // The public API speaks "\r\n" like TextBox; internally every line break is a single "\n".
  static string Normalize(string value){return (value??"").Replace("\r\n","\n").Replace("\r","\n");}
  int Outer(int inner){if(!multiline)return inner;int n=0;for(int i=0;i<inner&&i<text.Length;i++)if(text[i]=='\n')n++;return inner+n;}
  int Inner(int outer){if(!multiline)return Math.Max(0,Math.Min(text.Length,outer));int i=0,o=0;while(i<text.Length&&o<outer){o+=text[i]=='\n'?2:1;i++;}return i;}
  bool Replace(int start,int length,string value,bool user){string next=text.Substring(0,start)+value+text.Substring(start+length);if(user&&next.Length>maxLength)return false;if(next==text)return true;if(user){if(undo.Count>200)undo.Clear();undo.Push(new KeyValuePair<string,int>(text,caret));}text=next;Relayout();OnTextChanged(EventArgs.Empty);return true;}
  void Insert(string value,bool force=false){if(readOnly&&!force)return;string norm=Normalize(value);if(!multiline)norm=norm.Replace("\n"," ");int a=Math.Min(caret,anchor),b=Math.Max(caret,anchor);if(Replace(a,b-a,norm,true))caret=anchor=Math.Min(text.Length,a+norm.Length);EnsureVisible();Invalidate();}

  int LineHeight{get{return Math.Max(8,Font.Height+(PixelMeasure.IsPixel(Font)?2:0));}}
  int Edge{get{return borderStyle==BorderStyle.None?0:2;}}
  bool ShowsBar{get{return multiline&&(scrollBars==ScrollBars.Vertical||scrollBars==ScrollBars.Both||Rows.Count>VisibleRows);}}
  Rectangle TextArea{get{int e=Edge;int right=multiline?Bar+4:0;return new Rectangle(e+Pad,e+(multiline?Pad-2:0),Math.Max(1,Width-e*2-Pad*2-right),Math.Max(1,Height-e*2-(multiline?Pad*2-4:0)));}}
  int VisibleRows{get{return Math.Max(1,TextArea.Height/LineHeight);}}
  void Relayout(){rows=null;Invalidate();}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);Relayout();}
  protected override void OnResize(EventArgs e){base.OnResize(e);Relayout();}
  List<Row> Rows{get{if(rows!=null)return rows;var list=new List<Row>();if(!multiline){list.Add(new Row(0,text.Length));return rows=list;}float limit=wordWrap?TextArea.Width:float.MaxValue;int start=0;
    while(true){int end=text.IndexOf('\n',start);if(end<0)end=text.Length;int rowStart=start;float width=0;int lastSpace=-1;
     for(int i=start;i<end;i++){float w=PixelMeasure.Width(Font,text[i]);if(width+w>limit&&i>rowStart){int cut=lastSpace>=rowStart?lastSpace+1:i;list.Add(new Row(rowStart,cut-rowStart));rowStart=cut;width=PixelMeasure.Width(Font,text,rowStart,i-rowStart);lastSpace=-1;}width+=w;if(text[i]==' ')lastSpace=i;}
     list.Add(new Row(rowStart,end-rowStart));if(end>=text.Length)break;start=end+1;}
    return rows=list;}}
  int RowOf(int position){var list=Rows;for(int i=0;i<list.Count;i++){var r=list[i];if(position<r.Start)continue;if(position<r.Start+r.Length)return i;if(position==r.Start+r.Length&&(i==list.Count-1||list[i+1].Start>position))return i;}return list.Count-1;}
  float XOf(int position){var r=Rows[RowOf(position)];return PixelMeasure.Width(Font,text,r.Start,position-r.Start);}
  int RowTop(int row){var area=TextArea;return multiline?area.Y+(row-scrollLine)*LineHeight:Math.Max(area.Y,(Height-LineHeight)/2);}
  int Hit(Point p){var list=Rows;var area=TextArea;int row=multiline?scrollLine+(int)Math.Floor((p.Y-area.Y)/(double)LineHeight):0;row=Math.Max(0,Math.Min(list.Count-1,row));var r=list[row];float x=p.X-area.X+scrollX,acc=0;for(int i=r.Start;i<r.Start+r.Length;i++){float w=PixelMeasure.Width(Font,text[i]);if(acc+w/2>=x)return i;acc+=w;}int last=r.Start+r.Length;if(row<list.Count-1&&list[row+1].Start==last&&last>r.Start)last--;return last;}
  void EnsureVisible(){if(!IsHandleCreated&&Width<=0)return;int row=RowOf(caret);if(multiline){if(row<scrollLine)scrollLine=row;else if(row>=scrollLine+VisibleRows)scrollLine=row-VisibleRows+1;ClampScroll();}if(!multiline||!wordWrap){float x=XOf(caret),width=TextArea.Width;if(x-scrollX>width-4)scrollX=x-width+24;else if(x<scrollX)scrollX=Math.Max(0,x-24);}caretOn=true;}
  void ClampScroll(){scrollLine=Math.Max(0,Math.Min(scrollLine,Math.Max(0,Rows.Count-VisibleRows)));}

  ContextMenuStrip BuildMenu(){var menu=new ContextMenuStrip();menu.Items.Add("잘라내기",null,(s,e)=>Cut());menu.Items.Add("복사",null,(s,e)=>Copy());menu.Items.Add("붙여넣기",null,(s,e)=>Paste());menu.Items.Add(new ToolStripSeparator());menu.Items.Add("모두 선택",null,(s,e)=>SelectAll());return menu;}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);caretOn=true;blink.Start();Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);CommitComposition();blink.Stop();caretOn=false;Invalidate();}
  protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
  protected override bool IsInputKey(Keys key){Keys code=key&Keys.KeyCode;if(code==Keys.Left||code==Keys.Right||code==Keys.Up||code==Keys.Down||code==Keys.Home||code==Keys.End||code==Keys.PageUp||code==Keys.PageDown)return true;if(code==Keys.Enter)return multiline;if(code==Keys.Tab&&(key&Keys.Control)==0)return AcceptsTab&&multiline;return base.IsInputKey(key);}
  void MoveCaret(int position,bool extend){caret=Math.Max(0,Math.Min(text.Length,position));if(!extend)anchor=caret;EnsureVisible();Invalidate();}
  int VerticalTarget(int rowsDown){int row=RowOf(caret);float x=XOf(caret);int target=Math.Max(0,Math.Min(Rows.Count-1,row+rowsDown));var area=TextArea;int saved=scrollLine;scrollLine=Math.Max(0,target-VisibleRows/2);int pos=Hit(new Point((int)(area.X+x-scrollX+1),RowTop(target)+1));scrollLine=saved;return pos;}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.Handled)return;bool shift=e.Shift,ctrl=e.Control;var r=Rows[RowOf(caret)];
   switch(e.KeyCode){
    case Keys.Left:if(!shift&&caret!=anchor)MoveCaret(Math.Min(caret,anchor),false);else MoveCaret(caret-1,shift);break;
    case Keys.Right:if(!shift&&caret!=anchor)MoveCaret(Math.Max(caret,anchor),false);else MoveCaret(caret+1,shift);break;
    case Keys.Up:if(multiline)MoveCaret(VerticalTarget(-1),shift);break;
    case Keys.Down:if(multiline)MoveCaret(VerticalTarget(1),shift);break;
    case Keys.PageUp:if(multiline)MoveCaret(VerticalTarget(-VisibleRows),shift);break;
    case Keys.PageDown:if(multiline)MoveCaret(VerticalTarget(VisibleRows),shift);break;
    case Keys.Home:MoveCaret(ctrl?0:r.Start,shift);break;
    case Keys.End:MoveCaret(ctrl?text.Length:r.Start+r.Length,shift);break;
    case Keys.Back:if(readOnly)break;if(caret==anchor&&caret>0)anchor=caret-1;Insert("");break;
    case Keys.Delete:if(readOnly)break;if(caret==anchor&&caret<text.Length)anchor=caret+1;Insert("");break;
    case Keys.Enter:if(multiline&&!readOnly)Insert("\n");break;
    case Keys.Tab:if(multiline&&AcceptsTab&&!readOnly)Insert("\t");break;
    case Keys.A:if(ctrl)SelectAll();else return;break;
    case Keys.C:if(ctrl)Copy();else return;break;
    case Keys.X:if(ctrl)Cut();else return;break;
    case Keys.V:if(ctrl)Paste();else return;break;
    case Keys.Z:if(ctrl&&!readOnly)Undo();else return;break;
    default:return;}
   e.Handled=true;}
  protected override void OnKeyPress(KeyPressEventArgs e){base.OnKeyPress(e);if(e.Handled)return;if(e.KeyChar>=' '&&e.KeyChar!=(char)127){Insert(e.KeyChar.ToString());e.Handled=true;}}

  // Hangul is composed in the IME; the unfinished syllable is drawn inline and committed text is inserted here.
  [StructLayout(LayoutKind.Sequential)]struct CompositionForm {public int Style;public Point Position;public Rectangle Area;}
  [DllImport("imm32.dll")]static extern IntPtr ImmGetContext(IntPtr window);
  [DllImport("imm32.dll")]static extern bool ImmReleaseContext(IntPtr window,IntPtr context);
  [DllImport("imm32.dll",CharSet=CharSet.Unicode)]static extern int ImmGetCompositionStringW(IntPtr context,int index,byte[] buffer,int length);
  [DllImport("imm32.dll")]static extern bool ImmSetCompositionWindow(IntPtr context,ref CompositionForm form);
  [DllImport("imm32.dll")]static extern bool ImmNotifyIME(IntPtr context,int action,int index,int value);
  const int WM_IME_STARTCOMPOSITION=0x10D,WM_IME_ENDCOMPOSITION=0x10E,WM_IME_COMPOSITION=0x10F,WM_IME_CHAR=0x286,GCS_COMPSTR=0x8,GCS_RESULTSTR=0x800,NI_COMPOSITIONSTR=0x15,CPS_COMPLETE=0x1;
  static string ImeString(IntPtr context,int kind){int size=ImmGetCompositionStringW(context,kind,null,0);if(size<=0)return "";var buffer=new byte[size];ImmGetCompositionStringW(context,kind,buffer,size);return System.Text.Encoding.Unicode.GetString(buffer);}
  void CommitComposition(){if(composition.Length==0||!IsHandleCreated)return;var context=ImmGetContext(Handle);if(context!=IntPtr.Zero){ImmNotifyIME(context,NI_COMPOSITIONSTR,CPS_COMPLETE,0);ImmReleaseContext(Handle,context);}}
  void PlaceImeWindow(IntPtr context){var area=TextArea;var form=new CompositionForm{Style=0x2,Position=new Point((int)(area.X+XOf(caret)-scrollX),RowTop(RowOf(caret)))};ImmSetCompositionWindow(context,ref form);}
  protected override void WndProc(ref Message m){
   if(m.Msg==WM_IME_STARTCOMPOSITION){composition="";var context=ImmGetContext(Handle);if(context!=IntPtr.Zero){PlaceImeWindow(context);ImmReleaseContext(Handle,context);}return;}
   if(m.Msg==WM_IME_COMPOSITION){long flags=m.LParam.ToInt64();var context=ImmGetContext(Handle);if(context!=IntPtr.Zero){try{if((flags&GCS_RESULTSTR)!=0){string result=ImeString(context,GCS_RESULTSTR);composition="";if(result.Length>0)Insert(result);}if((flags&GCS_COMPSTR)!=0)composition=readOnly?"":ImeString(context,GCS_COMPSTR);PlaceImeWindow(context);}finally{ImmReleaseContext(Handle,context);}}caretOn=true;Invalidate();return;}
   if(m.Msg==WM_IME_ENDCOMPOSITION){composition="";Invalidate();return;}
   if(m.Msg==WM_IME_CHAR)return;
   base.WndProc(ref m);}

  Rectangle BarRect{get{int e=Edge;return new Rectangle(Width-e-Bar-3,e+3,Bar,Math.Max(1,Height-e*2-6));}}
  Rectangle ThumbRect{get{var bar=BarRect;int total=Math.Max(1,Rows.Count),visible=VisibleRows;int h=Math.Max(16,bar.Height*Math.Min(visible,total)/total);int range=Math.Max(1,total-visible);int y=bar.Y+(bar.Height-h)*Math.Min(scrollLine,range)/range;return new Rectangle(bar.X,y,bar.Width,h);}}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.Button!=MouseButtons.Left)return;if(ShowsBar&&BarRect.Contains(e.Location)){var thumb=ThumbRect;if(thumb.Contains(e.Location)){thumbDragging=true;thumbGrab=e.Y-thumb.Y;}else{scrollLine+=e.Y<thumb.Y?-VisibleRows:VisibleRows;ClampScroll();Invalidate();}return;}int hit=Hit(e.Location);MoveCaret(hit,(ModifierKeys&Keys.Shift)!=0);dragging=true;Capture=true;}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);Cursor=ShowsBar&&BarRect.Contains(e.Location)?Cursors.Default:Cursors.IBeam;if(thumbDragging){var bar=BarRect;var thumb=ThumbRect;int range=Math.Max(1,Rows.Count-VisibleRows);scrollLine=(int)Math.Round((e.Y-thumbGrab-bar.Y)*(double)range/Math.Max(1,bar.Height-thumb.Height));ClampScroll();Invalidate();return;}if(dragging){caret=Hit(e.Location);EnsureVisible();Invalidate();}}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=thumbDragging=false;Capture=false;}
  protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(ShowsBar&&BarRect.Contains(e.Location))return;int p=Hit(e.Location),a=p,b=p;while(a>0&&!char.IsWhiteSpace(text[a-1]))a--;while(b<text.Length&&!char.IsWhiteSpace(text[b]))b++;anchor=a;caret=b;Invalidate();}
  protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(multiline&&(ModifierKeys&Keys.Shift)==0){scrollLine-=e.Delta/40;ClampScroll();}else{scrollX=Math.Max(0,scrollX-e.Delta/2f);}Invalidate();}

  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.None;g.Clear(BackColor);var whole=new Rectangle(0,0,Width,Height);
   if(borderStyle!=BorderStyle.None){Theme.Pixels(g,Brushes.Black,whole,new[]{2,1});using(var fill=new SolidBrush(BackColor))Theme.Pixels(g,fill,Theme.Inset(whole,Focused?2:1),new[]{1});}
   var area=TextArea;var list=Rows;var clip=new Rectangle(area.X-2,Edge+1,area.Width+4,Math.Max(1,Height-Edge*2-2));var saved=g.Clip;g.SetClip(clip);int a=Math.Min(caret,anchor),b=Math.Max(caret,anchor);bool showSelection=a!=b&&(Focused||!HideSelection);int height=LineHeight;
   int first=multiline?scrollLine:0,last=multiline?Math.Min(list.Count-1,scrollLine+VisibleRows):0;
   for(int i=first;i<=last&&i<list.Count;i++){var r=list[i];int y=RowTop(i);float x=area.X-scrollX;string line=text.Substring(r.Start,r.Length);PixelMeasure.Draw(g,line,Font,Enabled?ForeColor:Color.Black,x,y);
    if(showSelection){int sa=Math.Max(a,r.Start),sb=Math.Min(b,r.Start+r.Length);bool tail=b>r.Start+r.Length&&a<=r.Start+r.Length&&i<list.Count-1;if(sa<sb||tail){float x1=x+PixelMeasure.Width(Font,text,r.Start,Math.Max(0,sa-r.Start)),x2=x+PixelMeasure.Width(Font,text,r.Start,Math.Max(0,sb-r.Start))+(tail?6:0);g.FillRectangle(Brushes.Black,x1,y,Math.Max(1,x2-x1),height);if(sa<sb)PixelMeasure.Draw(g,text.Substring(sa,sb-sa),Font,Color.White,x1,y);}}}
   if(Focused&&list.Count>0){int row=RowOf(caret);int y=RowTop(row);float x=area.X-scrollX+XOf(caret);if(composition.Length>0){float w=PixelMeasure.Width(Font,composition);g.FillRectangle(Brushes.Black,x,y,w+2,height);PixelMeasure.Draw(g,composition,Font,Color.White,x+1,y);x+=w+2;}if(caretOn&&!readOnly)g.FillRectangle(Brushes.Black,(int)x,y,2,height);}
   g.Clip=saved;
   if(ShowsBar&&Rows.Count>VisibleRows){var bar=BarRect;Theme.Dither(g,bar,true);g.DrawRectangle(Pens.Black,bar.X,bar.Y,bar.Width-1,bar.Height-1);var thumb=ThumbRect;g.FillRectangle(Brushes.Black,thumb);}
   if(!Enabled)using(var veil=new HatchBrush(HatchStyle.Percent50,Color.White,Color.Transparent))g.FillRectangle(veil,Theme.Inset(whole,2));}
 }

 // Number field: type digits, or use the arrow keys, wheel and the ▲▼ buttons.
 class PixelNumber:Control {
  decimal value,minimum,maximum=100,increment=1;int decimals;string typing;int pressed;BorderStyle borderStyle=BorderStyle.FixedSingle;readonly Timer repeat=new Timer{Interval=380};
  public event EventHandler ValueChanged;
  public PixelNumber(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);BackColor=Color.White;Font=Theme.Input();TabStop=true;repeat.Tick+=(s,e)=>{repeat.Interval=60;Step(pressed);};}
  protected override Size DefaultSize{get{return new Size(120,30);}}
  protected override void Dispose(bool disposing){if(disposing)repeat.Dispose();base.Dispose(disposing);}
  public decimal Minimum{get{return minimum;}set{minimum=value;if(maximum<minimum)maximum=minimum;Value=this.value;}}
  public decimal Maximum{get{return maximum;}set{maximum=value;if(minimum>maximum)minimum=maximum;Value=this.value;}}
  public decimal Increment{get{return increment;}set{increment=value;}}
  public int DecimalPlaces{get{return decimals;}set{decimals=Math.Max(0,value);Invalidate();}}
  public BorderStyle BorderStyle{get{return borderStyle;}set{borderStyle=value;Invalidate();}}
  public decimal Value{get{return value;}set{decimal next=Math.Max(minimum,Math.Min(maximum,value));if(next==this.value){Invalidate();return;}this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
  public override string Text{get{return value.ToString("F"+decimals,System.Globalization.CultureInfo.InvariantCulture);}set{decimal parsed;if(decimal.TryParse(value,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out parsed))Value=parsed;}}
  void Step(int direction){Commit();Value=value+increment*direction;}
  void Commit(){if(typing==null)return;string pending=typing;typing=null;decimal parsed;if(pending.Length>0&&pending!="-"&&decimal.TryParse(pending,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out parsed))Value=parsed;Invalidate();}
  Rectangle UpRect{get{return new Rectangle(Width-22,3,19,Height/2-3);}}
  Rectangle DownRect{get{return new Rectangle(Width-22,Height/2,19,Height-Height/2-3);}}
  protected override bool IsInputKey(Keys key){Keys code=key&Keys.KeyCode;return code==Keys.Up||code==Keys.Down||base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.Handled)return;if(e.KeyCode==Keys.Up){Step(1);e.Handled=true;}else if(e.KeyCode==Keys.Down){Step(-1);e.Handled=true;}else if(e.KeyCode==Keys.PageUp){Step(10);e.Handled=true;}else if(e.KeyCode==Keys.PageDown){Step(-10);e.Handled=true;}else if(e.KeyCode==Keys.Enter){Commit();e.Handled=true;}else if(e.KeyCode==Keys.Escape&&typing!=null){typing=null;Invalidate();e.Handled=true;}else if(e.KeyCode==Keys.Back){typing=(typing??Text);if(typing.Length>0)typing=typing.Substring(0,typing.Length-1);Invalidate();e.Handled=true;}else if(e.KeyCode==Keys.Delete){typing="";Invalidate();e.Handled=true;}}
  protected override void OnKeyPress(KeyPressEventArgs e){base.OnKeyPress(e);char c=e.KeyChar;if(char.IsDigit(c)||(c=='.'&&decimals>0)||(c=='-'&&minimum<0)){typing=(typing??"")+c;Invalidate();e.Handled=true;}}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Commit();Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(!Enabled||e.Button!=MouseButtons.Left)return;pressed=UpRect.Contains(e.Location)?1:DownRect.Contains(e.Location)?-1:0;if(pressed!=0){Step(pressed);repeat.Interval=380;repeat.Start();}}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);pressed=0;repeat.Stop();Invalidate();}
  protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(Focused)Step(e.Delta>0?1:-1);}
  protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.None;g.Clear(BackColor);var whole=new Rectangle(0,0,Width,Height);if(borderStyle!=BorderStyle.None){Theme.Pixels(g,Brushes.Black,whole,new[]{2,1});Theme.Pixels(g,Brushes.White,Theme.Inset(whole,Focused?2:1),new[]{1});}
   string shown=typing??Text;int y=(Height-Font.Height)/2;PixelMeasure.Draw(g,shown,Font,Color.Black,9,y);if(typing!=null&&Focused)g.FillRectangle(Brushes.Black,9+(int)PixelMeasure.Width(Font,shown),y,2,Font.Height);
   foreach(int dir in new[]{1,-1}){var r=dir>0?UpRect:DownRect;bool down=pressed==dir;if(down)g.FillRectangle(Brushes.Black,r);else g.DrawRectangle(Pens.Black,r.X,r.Y,r.Width-1,r.Height-1);int cx=r.X+r.Width/2,cy=r.Y+r.Height/2;using(var b=new SolidBrush(down?Color.White:Color.Black))for(int k=0;k<4;k++){int row=dir>0?cy-2+k:cy+1-k;g.FillRectangle(b,cx-k,row,k*2+1,1);}}
   if(!Enabled)using(var veil=new HatchBrush(HatchStyle.Percent50,Color.White,Color.Transparent))g.FillRectangle(veil,Theme.Inset(whole,2));}
 }

 class PixelCheckBox:CheckBox {
  public PixelCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Font=Theme.Input();BackColor=Color.White;Cursor=Cursors.Hand;}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.None;g.Clear(BackColor);int size=18,y=(Height-size)/2;var box=new Rectangle(2,y,size,size);Theme.Box(g,box,false);if(Checked)g.FillRectangle(Brushes.Black,Theme.Inset(box,5));if(Focused)for(int x=box.Right+6;x<Width-4;x+=4)g.FillRectangle(Brushes.Black,x,Height-3,2,1);Theme.Text(g,Text,Font,new Rectangle(box.Right+8,0,Math.Max(1,Width-box.Right-10),Height),Color.Black,ContentAlignment.MiddleLeft);if(!Enabled)using(var veil=new HatchBrush(HatchStyle.Percent50,Color.White,Color.Transparent))g.FillRectangle(veil,ClientRectangle);}
 }

 // Selection box: always a fixed list (typing jumps to matching items) drawn with a pixel frame and ▼.
 class PixelComboBox:ComboBox {
  public PixelComboBox(){base.DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;Font=Theme.Input();ItemHeight=24;}
  public new ComboBoxStyle DropDownStyle{get{return base.DropDownStyle;}set{base.DropDownStyle=ComboBoxStyle.DropDownList;}}
  // A list-only box ignores unknown text, so unknown saved values are added instead of being lost.
  public override string Text{get{return base.Text;}set{string v=value??"";if(v.Length>0&&FindStringExact(v)<0)Items.Add(v);if(v.Length>0)SelectedIndex=FindStringExact(v);else base.Text=v;}}
  protected override void OnDrawItem(DrawItemEventArgs e){var g=e.Graphics;bool selected=(e.State&DrawItemState.Selected)!=0&&(e.State&DrawItemState.ComboBoxEdit)==0;g.FillRectangle(selected?Brushes.Black:Brushes.White,e.Bounds);string shown=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):"";if(selected&&e.Bounds.Height>=14)Theme.Cursor(g,e.Bounds.X+4,e.Bounds.Y+e.Bounds.Height/2,Color.White);if(shown.Length>0)Theme.Text(g,shown,Font,new Rectangle(e.Bounds.X+14,e.Bounds.Y,Math.Max(1,e.Bounds.Width-14),e.Bounds.Height),selected?Color.White:Color.Black,ContentAlignment.MiddleLeft);}
  protected override void WndProc(ref Message m){base.WndProc(ref m);if(!IsHandleCreated||Width<=8||Height<=8)return;if(m.Msg==0x000F)using(var g=Graphics.FromHwnd(Handle))PaintChrome(g);else if((m.Msg==0x0317||m.Msg==0x0318)&&m.WParam!=IntPtr.Zero)using(var g=Graphics.FromHdc(m.WParam))PaintChrome(g);}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
  void PaintChrome(Graphics g){g.SmoothingMode=SmoothingMode.None;var whole=new Rectangle(0,0,Width,Height);using(var region=new Region(whole)){region.Exclude(Theme.Inset(whole,2));g.FillRegion(Brushes.White,region);}Theme.Pixels(g,Brushes.Black,whole,new[]{2,1});Theme.Pixels(g,Brushes.White,Theme.Inset(whole,Focused?2:1),new[]{1});var inner=new Rectangle(4,3,Width-30,Height-6);g.FillRectangle(Brushes.White,inner);string shown=SelectedIndex>=0?GetItemText(SelectedItem):Text;Theme.Text(g,shown,Font,new Rectangle(10,0,Math.Max(1,Width-36),Height),Color.Black,ContentAlignment.MiddleLeft);int ax=Width-22;g.FillRectangle(Brushes.White,ax-4,3,22,Height-6);g.FillRectangle(Brushes.Black,ax-5,5,1,Height-10);int cy=Height/2-2;for(int k=0;k<5;k++)g.FillRectangle(Brushes.Black,ax+k,cy+k,10-k*2,1);if(!Enabled)using(var veil=new HatchBrush(HatchStyle.Percent50,Color.White,Color.Transparent))g.FillRectangle(veil,Theme.Inset(whole,2));}
 }

 // Checklist with pixel boxes; mirrors the CheckedListBox members the app uses.
 class PixelCheckedListBox:ListBox {
  readonly HashSet<object> marks=new HashSet<object>();public bool CheckOnClick{get;set;}public event ItemCheckEventHandler ItemCheck;
  public PixelCheckedListBox(){DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=28;Font=Theme.Input();IntegralHeight=false;}
  public int AddItem(object item,bool isChecked){int index=Items.Add(item);if(isChecked)marks.Add(item);return index;}
  public bool GetItemChecked(int index){return index>=0&&index<Items.Count&&marks.Contains(Items[index]);}
  public void SetItemChecked(int index,bool value){if(index<0||index>=Items.Count)return;bool current=GetItemChecked(index);var args=new ItemCheckEventArgs(index,value?CheckState.Checked:CheckState.Unchecked,current?CheckState.Checked:CheckState.Unchecked);if(current!=value&&ItemCheck!=null)ItemCheck(this,args);if(args.NewValue==CheckState.Checked)marks.Add(Items[index]);else marks.Remove(Items[index]);if(IsHandleCreated)Invalidate(GetItemRectangle(index));}
  public List<object> CheckedItems{get{return Items.Cast<object>().Where(marks.Contains).ToList();}}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);int index=IndexFromPoint(e.Location);if(index>=0&&(CheckOnClick||e.X<34))SetItemChecked(index,!GetItemChecked(index));}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space&&SelectedIndex>=0){SetItemChecked(SelectedIndex,!GetItemChecked(SelectedIndex));e.Handled=e.SuppressKeyPress=true;}}
  protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0||e.Index>=Items.Count)return;var g=e.Graphics;g.SmoothingMode=SmoothingMode.None;bool selected=(e.State&DrawItemState.Selected)!=0;g.FillRectangle(selected?Brushes.Black:Brushes.White,e.Bounds);var box=new Rectangle(e.Bounds.X+8,e.Bounds.Y+(e.Bounds.Height-16)/2,16,16);using(var pen=new Pen(selected?Color.White:Color.Black,2))g.DrawRectangle(pen,box.X+1,box.Y+1,box.Width-2,box.Height-2);if(GetItemChecked(e.Index))g.FillRectangle(selected?Brushes.White:Brushes.Black,Theme.Inset(box,4));Theme.Text(g,GetItemText(Items[e.Index]),Font,new Rectangle(box.Right+8,e.Bounds.Y,Math.Max(1,e.Bounds.Width-box.Right-10),e.Bounds.Height),selected?Color.White:Color.Black,ContentAlignment.MiddleLeft);}
 }

 // Game-style message window replacing MessageBox: text in the BW frame, choices with a cursor.
 static class GameDialog {
  public static DialogResult Show(string text){return Show(null,text,"",MessageBoxButtons.OK);}
  public static DialogResult Show(string text,string caption){return Show(null,text,caption,MessageBoxButtons.OK);}
  public static DialogResult Show(string text,string caption,MessageBoxButtons buttons){return Show(null,text,caption,buttons);}
  public static DialogResult Show(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){return Show(null,text,caption,buttons);}
  public static DialogResult Show(IWin32Window owner,string text){return Show(owner,text,"",MessageBoxButtons.OK);}
  public static DialogResult Show(IWin32Window owner,string text,string caption){return Show(owner,text,caption,MessageBoxButtons.OK);}
  public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){return Show(owner,text,caption,buttons);}
  public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons){
   var choices=buttons==MessageBoxButtons.YesNo?new[]{DialogResult.Yes,DialogResult.No}:buttons==MessageBoxButtons.YesNoCancel?new[]{DialogResult.Yes,DialogResult.No,DialogResult.Cancel}:buttons==MessageBoxButtons.OKCancel?new[]{DialogResult.OK,DialogResult.Cancel}:new[]{DialogResult.OK};
   using(var form=new DialogForm(text??"",caption??"",choices))return form.ShowDialog(owner);}
  class DialogForm:Form {
   readonly string message,caption;readonly DialogResult[] choices;int index;readonly List<string> lines;const int Width0=520;
   static string Label(DialogResult r){return r==DialogResult.Yes?"예":r==DialogResult.No?"아니오":r==DialogResult.Cancel?"취소":"확인";}
   public DialogForm(string text,string title,DialogResult[] options){message=text;caption=title;choices=options;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;BackColor=Color.White;DoubleBuffered=true;KeyPreview=true;Font=Theme.Input();Text=title.Length>0?title:"AA BATTLE";
    lines=Wrap(text.Replace("\r\n","\n").Replace('\r','\n'),Width0-64);int height=(caption.Length>0?44:18)+lines.Count*22+30+44+24;ClientSize=new Size(Width0,Math.Min(640,Math.Max(150,height)));if(options.Length==1)AcceptButton=null;}
   List<string> Wrap(string text,int width){var result=new List<string>();foreach(string paragraph in text.Split('\n')){string line="";foreach(char c in paragraph){if(PixelMeasure.Width(Font,line+c)>width&&line.Length>0){int space=line.LastIndexOf(' ');if(space>0){result.Add(line.Substring(0,space));line=line.Substring(space+1)+c;}else{result.Add(line);line=c.ToString();}}else line+=c;}result.Add(line);}return result;}
   Rectangle ChoiceRect(int i){int w=110,gap=14,total=choices.Length*w+(choices.Length-1)*gap;return new Rectangle(ClientSize.Width-24-total+i*(w+gap),ClientSize.Height-60,w,40);}
   protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Color.White);Theme.Frame(g,new Rectangle(0,0,ClientSize.Width,ClientSize.Height),false);int y=16;if(caption.Length>0){var head=new Rectangle(10,10,ClientSize.Width-20,28);Theme.Pixels(g,Brushes.Black,head,new[]{1});Theme.Text(g,caption,Font,head,Color.White,ContentAlignment.MiddleCenter);y=50;}
    foreach(string line in lines){if(y>ClientSize.Height-90)break;PixelMeasure.Draw(g,line,Font,Color.Black,30,y);y+=22;}
    for(int i=0;i<choices.Length;i++){var r=ChoiceRect(i);Theme.Box(g,r,i==index);if(i==index)Theme.Cursor(g,r.X+10,r.Y+r.Height/2,Color.White);Theme.Text(g,Label(choices[i]),Font,new Rectangle(r.X+18,r.Y,r.Width-24,r.Height),i==index?Color.White:Color.Black,ContentAlignment.MiddleCenter);}}
   protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);for(int i=0;i<choices.Length;i++)if(ChoiceRect(i).Contains(e.Location)&&index!=i){index=i;Invalidate();}}
   protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);for(int i=0;i<choices.Length;i++)if(ChoiceRect(i).Contains(e.Location)){DialogResult=choices[i];return;}}
   protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==Keys.Left||key==Keys.Up){index=(index+choices.Length-1)%choices.Length;Invalidate();return true;}if(key==Keys.Right||key==Keys.Down||key==Keys.Tab){index=(index+1)%choices.Length;Invalidate();return true;}if(key==Keys.Enter||key==Keys.Space||key==Keys.Z){DialogResult=choices[index];return true;}if(key==Keys.Escape||key==Keys.X){DialogResult=choices.Contains(DialogResult.Cancel)?DialogResult.Cancel:choices.Contains(DialogResult.No)?DialogResult.No:choices[0];return true;}if(key==(Keys.Control|Keys.C)){Clipboard.SetText(message);return true;}return base.ProcessCmdKey(ref msg,key);}
  }
 }

 static class PixelTree {
  // TreeView labels are drawn by us; the native text path garbles the pixel font.
  public static void Style(TreeView tree){tree.DrawMode=TreeViewDrawMode.OwnerDrawText;tree.Font=Theme.Input();tree.DrawNode+=(s,e)=>{if(e.Node==null||e.Bounds.Height<=0)return;var g=e.Graphics;var r=new Rectangle(e.Bounds.X,e.Bounds.Y,Math.Max(1,tree.ClientSize.Width-e.Bounds.X),e.Bounds.Height);bool selected=(e.State&TreeNodeStates.Selected)!=0;g.FillRectangle(selected?Brushes.Black:Brushes.White,r);if(selected)Theme.Cursor(g,r.X+3,r.Y+r.Height/2,Color.White);Theme.Text(g,e.Node.Text,tree.Font,new Rectangle(r.X+14,r.Y,Math.Max(1,r.Width-16),r.Height),selected?Color.White:Color.Black,ContentAlignment.MiddleLeft);};}
 }

 // Grid cells edit through the pixel text box instead of the native TextBox editing control.
 class PixelGridEditor:PixelTextBox,IDataGridViewEditingControl {
  bool loading;
  public PixelGridEditor(){BorderStyle=BorderStyle.None;}
  public DataGridView EditingControlDataGridView{get;set;}
  public object EditingControlFormattedValue{get{return Text;}set{loading=true;Text=Convert.ToString(value);loading=false;}}
  public int EditingControlRowIndex{get;set;}
  public bool EditingControlValueChanged{get;set;}
  public Cursor EditingPanelCursor{get{return Cursors.IBeam;}}
  public bool RepositionEditingControlOnValueChange{get{return false;}}
  public void ApplyCellStyleToEditingControl(DataGridViewCellStyle style){Font=Theme.Input();BackColor=Color.White;}
  public bool EditingControlWantsInputKey(Keys key,bool dataGridViewWantsInputKey){Keys code=key&Keys.KeyCode;return code==Keys.Left||code==Keys.Right||code==Keys.Home||code==Keys.End||code==Keys.Delete||code==Keys.Back||!dataGridViewWantsInputKey;}
  public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context){return Text;}
  public void PrepareEditingControlForEdit(bool selectAll){if(selectAll)SelectAll();else SelectionStart=TextLength;}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);if(loading)return;EditingControlValueChanged=true;if(EditingControlDataGridView!=null)EditingControlDataGridView.NotifyCurrentCellDirty(true);}
 }
 class PixelTextCell:DataGridViewTextBoxCell {
  public override Type EditType{get{return typeof(PixelGridEditor);}}
 }
}
