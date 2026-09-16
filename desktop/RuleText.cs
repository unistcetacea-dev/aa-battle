using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 public class RuleClauseSet { public string[] triggers=new string[0],effects=new string[0]; }
 // 포텐셜·특성 등 규칙 원문의 조건/결과 분리에만 쓰는 공용 텍스트 계층이다. 소유 슬롯이나 발동 주체는 담지 않는다.
 public static class RuleText {
  static bool SplitClause(string value,out string condition,out string remainder){
   condition=remainder="";value=(value??"").Trim();if(Regex.IsMatch(value,@"^[0-9０-９]+\s*T\s*동안")||Regex.IsMatch(value,@"^T\s*종료\s*시\s*까지"))return false;
   var enteredTurn=Regex.Match(value,@"^(.+?교대해\s*필드에\s*나온)\s*T\s*종료\s*시\s*까지\s+(.+)$");if(enteredTurn.Success){condition=enteredTurn.Groups[1].Value.Trim()+" 때";remainder="T 종료시까지 "+enteredTurn.Groups[2].Value.Trim();return true;}
   var enteredSecond=Regex.Match(value,@"^(.+?교대해\s*필드에\s*나와\s*[0-9０-９]+T째)\s*[,，、]\s*(.+)$");if(enteredSecond.Success){condition=enteredSecond.Groups[1].Value.Trim();remainder=enteredSecond.Groups[2].Value.Trim();return true;}
   var switchedTurn=Regex.Match(value,@"^(.+?교대로?\s*(?:필드에\s*)?나온\s*T)\s*[,，、]\s*(.+)$");if(switchedTurn.Success){condition=switchedTurn.Groups[1].Value.Trim();remainder=switchedTurn.Groups[2].Value.Trim();return true;}
   var ending=@"(?:때(?:에)?도|때|일때|경우|한해서|라면|있으면|없으면|(?:으)?면(?:\([^)]*(?:으)?면\))?|나오면|있는\s*동안|있는\s*한|참가\s*시에?|발동\s*시에?|사용\s*시에?|발생\s*시에?|개시\s*시에?|종료\s*시에?|교대\s*시에?|명중\s*시에?|피격\s*시에?)";
   var match=Regex.Match(value,@"^(.+?"+ending+@")[\s]*(?:[,，、.]\s*|\s+)(.+)$");if(!match.Success)return false;condition=match.Groups[1].Value.Trim();remainder=match.Groups[2].Value.Trim();return remainder.Length>0;
  }
  static bool ConditionOnly(string value){return Regex.IsMatch((value??"").Trim().TrimEnd('.','。'),@"(?:때(?:에)?도|때|일때|경우|한해서|라면|있으면|없으면|(?:으)?면(?:\([^)]*(?:으)?면\))?|나오면|있는\s*동안|있는\s*한|참가\s*시에?|발동\s*시에?|사용\s*시에?|발생\s*시에?|개시\s*시에?|종료\s*시에?|교대\s*시에?|명중\s*시에?|피격\s*시에?)$");}
  public static RuleClauseSet Split(string body){
   var conditions=new List<string>();var results=new List<string>();var sentences=Regex.Split((body??"").Trim(),@"(?<=[.!?。])\s+").Where(x=>x.Length>0).ToArray();
   foreach(var original in sentences){string sentence=original,condition,remainder;while(SplitClause(sentence,out condition,out remainder)){conditions.Add(condition);sentence=remainder;}if(ConditionOnly(sentence)){conditions.Add(sentence.Trim().TrimEnd('.','。'));continue;}if(sentence.Length>0)results.Add(sentence);}
   return new RuleClauseSet{triggers=conditions.ToArray(),effects=results.ToArray()};
  }
 }
}
