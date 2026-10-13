using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AABattle {
 public class AbilityDefinition { public string name,effect; public string[] triggers=new string[0],effects=new string[0]; public string support="수동",blocker=""; }
 public static class AbilityCatalog {
  static AbilityDefinition[] cached;
  public static AbilityDefinition[] All {
   get { if(cached==null)cached=Parse(Decode()).GroupBy(x=>x.name).Select(x=>Describe(x.First())).OrderBy(x=>x.name).ToArray();return cached; }
  }
  static string Decode(){
   byte[] packed=Convert.FromBase64String(Resource.Text("abilities.b64").Trim());
   using(var source=new MemoryStream(packed))using(var gzip=new GZipStream(source,CompressionMode.Decompress))using(var reader=new StreamReader(gzip,Encoding.UTF8))return reader.ReadToEnd();
  }
  static IEnumerable<AbilityDefinition> Parse(string text){
   var rows=new List<string>();var current=new StringBuilder();bool quoted=false;
   foreach(char ch in text){if(ch=='\"')quoted=!quoted;if((ch=='\n'||ch=='\r')&&!quoted){if(current.Length>0){rows.Add(current.ToString());current.Clear();}}else current.Append(ch);}if(current.Length>0)rows.Add(current.ToString());
   foreach(string row in rows.Skip(1)){int tab=row.IndexOf('\t');if(tab<1)continue;string name=row.Substring(0,tab).Trim();string effect=row.Substring(tab+1).Trim().Trim('\"').Replace("，",",").Replace("．",".");if(name.Length>0)yield return new AbilityDefinition{name=name,effect=effect};}
  }
  public static AbilityDefinition Find(string name){return All.FirstOrDefault(x=>x.name==name);}
  static AbilityDefinition Describe(AbilityDefinition ability){
   var rules=RuleText.Split(ability.effect);ability.triggers=rules.triggers;ability.effects=rules.effects;bool auto=AbilityModules.Dedicated.Contains(ability.name)||AbilityModules.Runnable(ability);
   string pending;
   if (AbilityModules.Pending.TryGetValue(ability.name, out pending)) {
    ability.support = auto ? "부분 자동" : "수동"; ability.blocker = pending; return ability;
   }
   if(auto){ability.support="자동";return ability;}
   ability.support="수동";
   string text = AbilityModules.Clean(ability.effect).Replace(" ", "");
   if(text.Contains("더블배틀")||text.Contains("인접")||text.Contains("아군전체"))ability.blocker="다인전 대상/범위 엔진 필요";
   else if(text.Contains("PP")||text.Contains("마지막으로사용")||text.Contains("기술을바꾸")||text.Contains("기술을선택"))ability.blocker="PP·기술 이력/교체 엔진 필요";
   else if(text.Contains("폼체인지")||text.Contains("변신")||text.Contains("모습")||text.Contains("진화")||text.Contains("폼으로"))ability.blocker="폼·종족 데이터 변경 엔진 필요";
   else if(text.Contains("파괴(약탈)"))ability.blocker="사용자 판단 필요: 소지품 파괴/약탈의 선택 방식";
   else if(text.Contains("〔특성〕")||Regex.IsMatch(text,@"이(?:〔특성〕|특성)은.*(?:변화|무효화)"))ability.blocker="특성 교체·복사·보호 모듈 미구현";
   else if(text.Contains("소지품")||text.Contains("나무열매")||text.Contains("아이템")||text.Contains("소모")||text.Contains("소비"))ability.blocker="소지품 사건·사용·이전 모듈 미구현";
   else if(text.Contains("추가행동"))ability.blocker="기술 호출·복사 연결 미구현";
   else if(text.Contains("특성을바꾸")||text.Contains("특성을없애")||text.Contains("특성을무효")||text.Contains("특성을복사"))ability.blocker="특성 교체·무효화 상태 엔진 필요";
   else if(text.Contains("임의")||text.Contains("랜덤")||text.Contains("무작위"))ability.blocker="선택/난수 규칙 확정 필요";
   else if(text.Contains("교대")||text.Contains("파티"))ability.blocker="교대·파티 선택 엔진 필요";
   else ability.blocker="효과 실행기 미구현";
   return ability;
  }
  public static IEnumerable<AbilityDefinition> Gaps {get{return All.Where(x=>x.support!="자동");}}
  public static void Test(){var drought=Find("가뭄");var speed=Find("가속");var trouble=Find("트러블메이커");if(All.Length<580||drought==null||!drought.effect.Contains("쾌청")||drought.support!="자동"||drought.triggers.Length==0||speed==null||speed.support!="자동"||trouble==null||trouble.blocker.Length==0)throw new Exception("Ability catalog import and classification");}
 }
}
