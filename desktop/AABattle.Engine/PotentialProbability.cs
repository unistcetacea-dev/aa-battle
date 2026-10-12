using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 public static class PotentialProbability {
  public static double? Rate(string text){string value=text??"";if(value.Contains("드물게"))return .10;if(value.Contains("저확률"))return .20;if(value.Contains("중확률"))return .50;return null;}
  public static bool[] Resolve(string[] effects,Func<double> rng){
   effects=effects??new string[0];rng=rng??new Random().NextDouble;var rates=effects.Select(Rate).ToArray();var result=rates.Select(x=>x.HasValue&&rng()<x.Value).ToArray();var medium=Enumerable.Range(0,effects.Length).Where(i=>rates[i]==.50).ToArray();
   if(medium.Length>=2&&!medium.Any(i=>result[i])){int selected=medium[Math.Min(medium.Length-1,(int)(rng()*medium.Length))];result[selected]=true;}
   return result;
  }
  public static string Help="드물게는 10%, 저확률은 20%, 중확률은 50%로 각각 판정합니다. 같은 포켓몬에서 같은 발동 시점에 성립한 중확률 포텐셜이 2개 이상이고 전부 실패하면, 그중 하나를 무작위로 골라 반드시 발동시킵니다. 여러 개가 원래 판정에 성공했다면 성공한 효과는 모두 발동합니다. 고확률은 지원하지 않습니다.";
  public static void Test(){
   if(Rate("드물게 발동")!=.10||Rate("저확률로 발동")!=.20||Rate("중확률로 발동")!=.50||Rate("고확률로 발동")!=null)throw new Exception("Potential probability rates failed");
   var rolls=new Queue<double>(new[]{.9,.9,.9});var forced=Resolve(new[]{"중확률 A","중확률 B"},()=>rolls.Dequeue());if(forced.Count(x=>x)!=1||!forced[1])throw new Exception("Medium probability guarantee failed");
   if(Resolve(new[]{"중확률 A"},()=>.9)[0])throw new Exception("Single medium probability was forced");
   if(!Resolve(new[]{"저확률 A","드물게 B"},()=>0).All(x=>x))throw new Exception("Independent probability roll failed");
  }
 }
}
