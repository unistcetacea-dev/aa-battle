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
 // 흑백 도트 테마: 색·글꼴·테두리·애니메이션
 static class Theme {
  public static Color Ink=Color.Black,Muted=Color.Black,Line=Color.Black,Paper=Color.White,Accent=Color.Black;
  // Pokemon BW is drawn on a 16px pixel grid; only whole multiples keep every glyph crisp.
  public const int Small=16,Large=32,Huge=48;
  // Captures and self-tests need the final frame immediately, so they switch animations off.
  public static bool Instant;
  static readonly int[] OuterCorner={4,2,1,1},MiddleCorner={2,1},InnerCorner={1};
  public static int Snap(float size){return size>=20?Huge:size>=12?Large:Small;}
  public static Font UI(float size=10,FontStyle style=FontStyle.Regular){return new Font(Fonts.Family,Snap(size),FontStyle.Regular,GraphicsUnit.Pixel);}
  public static Font Input(){return new Font(Fonts.Family,Small,FontStyle.Regular,GraphicsUnit.Pixel);}
  public static void Pixels(Graphics g,Brush brush,Rectangle r,int[] corner){if(r.Width<=0||r.Height<=0)return;int rows=Math.Min(corner.Length,r.Height/2);for(int i=0;i<rows;i++){int inset=Math.Min(corner[i],r.Width/2);g.FillRectangle(brush,r.X+inset,r.Y+i,r.Width-inset*2,1);g.FillRectangle(brush,r.X+inset,r.Bottom-1-i,r.Width-inset*2,1);}if(r.Height-rows*2>0)g.FillRectangle(brush,r.X,r.Y+rows,r.Width,r.Height-rows*2);}
  public static Rectangle Inset(Rectangle r,int n){return new Rectangle(r.X+n,r.Y+n,Math.Max(0,r.Width-n*2),Math.Max(0,r.Height-n*2));}
  public static void Dither(Graphics g,Rectangle r,bool sparse=false){if(r.Width<=0||r.Height<=0)return;using(var b=new HatchBrush(sparse?HatchStyle.Percent25:HatchStyle.Percent50,Color.Black,Color.White)){g.RenderingOrigin=Point.Empty;g.FillRectangle(b,r);}}
  // Game window: 2px rounded outline, white gap and a 1px inner rule like the BW message box.
  public static void Frame(Graphics g,Rectangle r,bool shadow=true){g.SmoothingMode=SmoothingMode.None;int width=Math.Max(1,r.Width),height=Math.Max(1,r.Height);if(width<16||height<16){g.FillRectangle(Brushes.White,r.X,r.Y,width,height);g.DrawRectangle(Pens.Black,r.X,r.Y,width-1,height-1);return;}var box=new Rectangle(r.X,r.Y,width,height);if(shadow){box.Width-=4;box.Height-=4;var shade=new Rectangle(box.X+4,box.Y+4,box.Width,box.Height);using(var b=new HatchBrush(HatchStyle.Percent50,Color.Black,Color.Transparent))Pixels(g,b,shade,OuterCorner);}Pixels(g,Brushes.Black,box,OuterCorner);Pixels(g,Brushes.White,Inset(box,2),MiddleCorner);if(box.Width>=40&&box.Height>=40){Pixels(g,Brushes.Black,Inset(box,4),InnerCorner);g.FillRectangle(Brushes.White,Inset(box,5));}}
  public static void Box(Graphics g,Rectangle r,bool filled){Pixels(g,Brushes.Black,r,MiddleCorner);if(!filled)Pixels(g,Brushes.White,Inset(r,2),InnerCorner);}
  // Menu cursor drawn as pixels because the font has no ▶ glyph.
  public static void Cursor(Graphics g,int x,int centerY,Color color,int scale=1){using(var b=new SolidBrush(color))for(int k=0;k<6;k++)g.FillRectangle(b,x+k*scale,centerY-(6-k)*scale,scale,Math.Max(1,(12-k*2)*scale));}
  public static void MoreArrow(Graphics g,int centerX,int y,Color color){using(var b=new SolidBrush(color))for(int k=0;k<5;k++)g.FillRectangle(b,centerX-5+k,y+k,10-k*2,1);}
  public static void Ball(Graphics g,int x,int y,int size,bool filled=true){g.SmoothingMode=SmoothingMode.None;g.FillEllipse(Brushes.White,x,y,size,size);if(filled)g.FillPie(Brushes.Black,x,y,size,size,180,180);g.DrawEllipse(Pens.Black,x,y,size,size);g.DrawLine(Pens.Black,x,y+size/2,x+size,y+size/2);int c=Math.Max(4,size/4);g.FillEllipse(Brushes.White,x+size/2-c/2,y+size/2-c/2,c,c);g.DrawEllipse(Pens.Black,x+size/2-c/2,y+size/2-c/2,c,c);}
  public static void HpBar(Graphics g,Rectangle r,double ratio){g.SmoothingMode=SmoothingMode.None;if(r.Width<40||r.Height<8)return;var tag=new Rectangle(r.X,r.Y,30,r.Height);Pixels(g,Brushes.Black,tag,MiddleCorner);using(var f=new Font(Fonts.Family,Small,FontStyle.Regular,GraphicsUnit.Pixel))Text(g,"HP",f,tag,Color.White,ContentAlignment.MiddleCenter);var bar=new Rectangle(r.X+28,r.Y+2,r.Width-28,r.Height-4);Pixels(g,Brushes.Black,bar,MiddleCorner);var inner=Inset(bar,2);g.FillRectangle(Brushes.White,inner);ratio=Math.Max(0,Math.Min(1,ratio));int fill=(int)Math.Ceiling((inner.Width-2)*ratio);if(fill<=0)return;var meter=new Rectangle(inner.X+1,inner.Y+1,fill,inner.Height-2);if(ratio>.5)g.FillRectangle(Brushes.Black,meter);else Dither(g,meter,ratio<=.2);}
  public static void Text(Graphics g,string text,Font font,Rectangle bounds,Color color,ContentAlignment align){if(bounds.Width<=0||bounds.Height<=0)return;g.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;using(var format=new StringFormat()){format.Alignment=align==ContentAlignment.MiddleCenter||align==ContentAlignment.TopCenter?StringAlignment.Center:align==ContentAlignment.MiddleRight||align==ContentAlignment.TopRight?StringAlignment.Far:StringAlignment.Near;format.LineAlignment=align==ContentAlignment.TopLeft||align==ContentAlignment.TopCenter||align==ContentAlignment.TopRight?StringAlignment.Near:StringAlignment.Center;format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags=StringFormatFlags.NoWrap;using(var brush=new SolidBrush(color))g.DrawString(text,font,brush,bounds,format);}}
  public static Label Label(string text,int height=26){return new PixelLabel{Text=text,AutoSize=false,Height=height,ForeColor=Ink,Font=UI(),TextAlign=ContentAlignment.MiddleLeft,Dock=DockStyle.Top};}
  public static PixelComboBox Combo(IEnumerable<string> entries,string value){var c=new PixelComboBox{Dock=DockStyle.Fill};c.Items.AddRange(entries.Cast<object>().ToArray());c.SelectedItem=value;if(c.SelectedIndex<0&&c.Items.Count>0)c.SelectedIndex=0;return c;}
  public static PixelNumber Number(decimal value,decimal min,decimal max,int decimals=0){return new PixelNumber{Minimum=min,Maximum=max,Value=Math.Min(max,Math.Max(min,value)),DecimalPlaces=decimals,Increment=decimals>0?.1m:1,Font=Input(),Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle};}
  public static TableLayoutPanel Table(int cols=2){var t=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=cols,Padding=new Padding(15)};t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));for(int i=1;i<cols;i++)t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/(cols-1)));return t;}
  public static void Row(TableLayoutPanel t,string label,Control c){int row=t.RowCount++;t.RowStyles.Add(new RowStyle(SizeType.Absolute,36));t.Controls.Add(Label(label),0,row);t.Controls.Add(c,1,row);}
  // Owner-drawn pixel tabs: the selected tab is inverted like a game menu header.
  public static void Tabs(TabControl tabs){tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.SizeMode=TabSizeMode.Fixed;tabs.Font=Input();tabs.ItemSize=new Size(Math.Max(96,tabs.ItemSize.Width),30);tabs.DrawItem+=(s,e)=>{var page=tabs.TabPages[e.Index];bool selected=e.Index==tabs.SelectedIndex;var r=e.Bounds;e.Graphics.FillRectangle(Brushes.White,r);var box=new Rectangle(r.X+1,r.Y+2,r.Width-3,r.Height-2);Pixels(e.Graphics,Brushes.Black,box,MiddleCorner);if(!selected)Pixels(e.Graphics,Brushes.White,Inset(box,2),InnerCorner);using(var f=UI())Text(e.Graphics,page.Text,f,box,selected?Color.White:Color.Black,ContentAlignment.MiddleCenter);};}
  // Editor and dialog scale: field contents at 24px, labels and buttons at 32px.
  // 24px is a 1.5x step of the 16px grid; GDI+ grid fitting keeps it clean, unlike 18-22px.
  public const int Field=24;
  public static Font FieldFont(){return new Font(Fonts.Family,Field,FontStyle.Regular,GraphicsUnit.Pixel);}
  public static void Enlarge(Control root){if(root==null||"enlarged".Equals(root.Tag as string))return;if(root is Form)root.Tag=root.Tag??"enlarged";root.SuspendLayout();try{EnlargeChildren(root);}finally{root.ResumeLayout(true);}}
  static bool Narrow(Control c){return c.Dock==DockStyle.Fill||c.Dock==DockStyle.Left||c.Dock==DockStyle.Right;}
  static void EnlargeChildren(Control root){
   foreach(Control c in root.Controls){
    if(c is GameMenu||c is AAView||c is MessageLabel){continue;}
    if(c is PixelTextBox){var box=(PixelTextBox)c;if(PixelMeasure.IsPixel(box.Font))box.Font=FieldFont();if(!box.Multiline&&!Narrow(box))box.Height=Math.Max(box.Height,36);}
    else if(c is PixelNumber){c.Font=FieldFont();if(!Narrow(c))c.Height=Math.Max(c.Height,36);}
    else if(c is PixelComboBox){var combo=(PixelComboBox)c;combo.Font=FieldFont();combo.ItemHeight=30;}
    else if(c is PixelCheckBox)c.Font=FieldFont();
    else if(c is PixelCheckedListBox){var list=(PixelCheckedListBox)c;list.Font=FieldFont();list.ItemHeight=34;}
    else if(c is ListBox){var list=(ListBox)c;if(list.Font.Size<=Small+.5f){list.Font=FieldFont();if(list.DrawMode!=DrawMode.Normal)list.ItemHeight=Math.Max(list.ItemHeight,38);}}
    else if(c is TreeView){var tree=(TreeView)c;tree.Font=FieldFont();tree.ItemHeight=Math.Max(tree.ItemHeight,36);}
    else if(c is DataGridView){var grid=(DataGridView)c;grid.DefaultCellStyle.Font=FieldFont();grid.ColumnHeadersDefaultCellStyle.Font=FieldFont();grid.RowsDefaultCellStyle.Font=FieldFont();foreach(DataGridViewColumn column in grid.Columns)if(column.HasDefaultCellStyle)column.DefaultCellStyle.Font=FieldFont();grid.RowTemplate.Height=Math.Max(grid.RowTemplate.Height,38);foreach(DataGridViewRow row in grid.Rows)if(row.Height<38)row.Height=38;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.ColumnHeadersHeight=Math.Max(grid.ColumnHeadersHeight,40);grid.RowHeadersWidth=Math.Max(grid.RowHeadersWidth,grid.RowHeadersVisible&&grid.RowHeadersWidth>60?110:grid.RowHeadersWidth);}
    else if(c is FlatButton){var button=(FlatButton)c;if(button.Subtext.Length==0&&button.Font.Size<=Small+.5f){button.Font=button.Text.EndsWith("▼")||button.Text.EndsWith("…")?FieldFont():UI(16);if(button.Dock==DockStyle.None||button.Dock==DockStyle.Left||button.Dock==DockStyle.Right){int want=(int)PixelMeasure.Width(button.Font,button.Text)+40;button.Width=Math.Max(button.Width,want);}if(button.Dock!=DockStyle.Fill&&button.Dock!=DockStyle.Left&&button.Dock!=DockStyle.Right)button.Height=Math.Max(button.Height,46);}}
    else if(c is PixelLabel){var label=(PixelLabel)c;if(label.Font.Size<=Small+.5f){var table=label.Parent as TableLayoutPanel;bool fieldLabel=table!=null&&table.GetColumn(label)==0&&table.ColumnCount>1;label.Font=fieldFont(fieldLabel);if(label.Dock==DockStyle.Top||label.Dock==DockStyle.Bottom||label.Dock==DockStyle.None)label.Height=Math.Max(label.Height,label.Font.Height+10);}}
    else if(c is PixelSearchBox){c.Height=Math.Max(c.Height,42);}
    else if(c is PixelText){if(c.Font.Size<=Small+.5f)c.Font=FieldFont();}
    if(c is TableLayoutPanel){var table=(TableLayoutPanel)c;foreach(RowStyle style in table.RowStyles)if(style.SizeType==SizeType.Absolute&&style.Height>=20&&style.Height<46)style.Height=46;if(table.ColumnCount>1&&table.ColumnStyles.Count>0&&table.ColumnStyles[0].SizeType==SizeType.Absolute&&table.ColumnStyles[0].Width>=90&&table.ColumnStyles[0].Width<=150)table.ColumnStyles[0].Width=table.ColumnStyles[0].Width+70;}
    else if(c is FlowLayoutPanel&&c.Dock==DockStyle.Top&&((FlowLayoutPanel)c).WrapContents){c.AutoSize=true;((FlowLayoutPanel)c).AutoSizeMode=AutoSizeMode.GrowAndShrink;}
    else if(c is FlowLayoutPanel&&c.Dock!=DockStyle.Fill&&!((FlowLayoutPanel)c).AutoSize)c.Height=Math.Max(c.Height,62);
    if(!(c is DataGridView)&&!(c is PixelTextBox)&&!(c is PixelNumber)&&!(c is PixelComboBox))EnlargeChildren(c);
   }
  }
  static Font fieldFont(bool heading){return heading?UI(16):FieldFont();}
 }
 static class Fonts {
  static List<IntPtr> memory=new List<IntPtr>();static List<PrivateFontCollection> collections=new List<PrivateFontCollection>();
  [DllImport("gdi32.dll")]static extern IntPtr AddFontMemResourceEx(IntPtr data,uint size,IntPtr reserved,ref uint count);
  public static FontFamily AA,System;
  public static FontFamily Family{get{if(System==null)Load();return System;}}
  public static void Load(){if(System!=null)return;AA=LoadOne("HeadKasen.ttf");System=LoadOne("pokemon-bw.otf");}
  static FontFamily LoadOne(string name){var bytes=Resource.Bytes(name);var ptr=Marshal.AllocCoTaskMem(bytes.Length);Marshal.Copy(bytes,0,ptr,bytes.Length);memory.Add(ptr);uint count=0;AddFontMemResourceEx(ptr,(uint)bytes.Length,IntPtr.Zero,ref count);var fonts=new PrivateFontCollection();fonts.AddMemoryFont(ptr,bytes.Length);collections.Add(fonts);return fonts.Families[0];}
 }
}
