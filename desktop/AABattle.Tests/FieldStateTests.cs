using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 public static class FieldStateTests {
  static void Check(bool value,string name){if(!value)throw new Exception("Field state FAIL: "+name);}
  static Fighter F(){return new Fighter(new Pokemon{name="FIELD TEST",level=100,types=new[]{"노말"},ability="",item="",@base=new[]{100,100,100,100,100,100},iv=new[]{31,31,31,31,31,31},moves=new[]{"몸통박치기"}});}
  static Move M(string name,string type="에스퍼"){return new Move{name=name,category="변화",types=new[]{type},tags=new string[0],power=0,accuracy=100};}
  public static void Run(Database db){var a=F();var b=F();var r=new Rules();var log=new List<string>();Mechanics.StatusMove(a,b,M("리플렉터"),r,()=>.5,log,0);Mechanics.StatusMove(a,b,M("빛의장막"),r,()=>.5,log,0);Mechanics.StatusMove(a,b,M("순풍","비행"),r,()=>.5,log,0);Check(r.Sides[1].Reflect==5&&r.Sides[1].LightScreen==5&&r.Sides[0].Tailwind==4,"move effects target sides");Check(!a.Conditions.Labels(a).Any(x=>x.Contains("리플렉터")||x.Contains("빛의장막")||x.Contains("순풍")),"field effects are not fighter conditions");Mechanics.EndTurn(new[]{a,b},r,log);Check(r.Sides[0].Tailwind==3&&r.Sides[1].Reflect==4&&r.Sides[1].LightScreen==4,"side durations tick");var damageMove=new Move{name="물리",category="물리",types=new[]{"노말"},power=80};Check(Mechanics.DamageFactor(a,b,damageMove,r,1)==.5,"reflect applies to receiving side");Check(Mechanics.StatFactor(a,b,5,r,0)==2,"tailwind applies to acting side");r.Sides[0].StealthRock=true;r.Sides[0].Reflect=2;Mechanics.StatusMove(a,b,M("안개제거","비행"),r,()=>.5,log,0);Check(!r.Sides[0].HasHazards()&&r.Sides[0].Reflect==2,"defog preserves screens");}
 }
}
