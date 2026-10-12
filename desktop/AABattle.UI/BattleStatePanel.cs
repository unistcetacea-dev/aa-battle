using System;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 class BattleStatePanel:Card {
  PixelLabel title;PixelText body;
  public BattleStatePanel(){title=new PixelLabel{Text="전장 전체 현황",Font=Theme.UI(12),Dock=DockStyle.Top,Height=42,TextAlign=ContentAlignment.MiddleCenter};body=new PixelText{Dock=DockStyle.Fill,Font=Theme.UI(9)};Controls.Add(body);Controls.Add(title);body.BringToFront();}
  public void SetReport(string value){body.Text=value;}
 }

}
