using System;
using System.Linq;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AABattle {
 public class EffectImplementationEntry {public string status,family,effect,interpretation,implementation,sources;}
 public static class EffectImplementationDex {
  static readonly HashSet<string> DirectiveNames=new HashSet<string>(EditorTemplate.Orders(false).Concat(EditorTemplate.Orders(true)).Concat(EditorTemplate.ExtendedOrders(false)).Concat(EditorTemplate.ExtendedOrders(true)).Select(x=>x.name));
  public static List<EffectImplementationEntry> Entries(){return PotentialParts.Build(PotentialParts.StandardEntries()).effects.Where(x=>!x.excludedFromImplementation).Select(Analyze).ToList();}
  public static EffectImplementationEntry Analyze(PotentialPart part){
   string value=part.text??"",family=Family(value),status="미구현",implementation="포텐셜 원문은 배틀 데이터에 전달되지만 효과 실행기에는 연결되지 않았습니다. 현재는 배틀 로그와 상태 창을 보며 수동 판정합니다.";
   int directiveCount=part.names.Count(DirectiveNames.Contains);
   string module=EffectModules.Classify(value),normalized=EffectNormalizer.Normalize(value);
   if(directiveCount>0){status=directiveCount==part.names.Count?"자동 구현":"일부 자동 구현";family="트레이너 4식·지령";implementation="이 효과가 트레이너 지령으로 사용될 때 Engine.Resolve가 선언 횟수와 행동 조건을 확인합니다. 4식은 회피·필중·버티기·후공 교대를 처리하고, Engine.ApplyOrder가 능력 랭크·1턴 배율·회복을 적용합니다. 같은 문장을 일반 포켓몬 포텐셜에서 사용하면 아직 자동 실행되지 않습니다.";}
   else if(module.Length>0&&!SpecificModule(module)){family=module;status=normalized.Contains("「○」")?"타입 지정 필요":normalized.Contains("임의의")?"일부 자동 구현":"자동 구현";implementation=ModuleImplementation(module)+(status=="타입 지정 필요"?" 빈 괄호(「○」)는 템플릿에서 타입을 고르면 자동 실행됩니다.":status=="일부 자동 구현"?" 「임의의」 대상·능력은 선택 창 연결 전까지 판정 대기로 남습니다.":"");}
   else if(PotentialProbability.Rate(value).HasValue){status="구현 규칙 준비";family="확률 발동";implementation=PotentialProbability.Help+" 포켓몬 포텐셜 실행기가 연결되면 이 판정기를 호출합니다.";}
   else if(value.Contains("확률로")){status="기준 질문 필요";family="확률 발동";implementation="확률 수치가 적혀 있지 않아 자동 판정할 수 없습니다.";}
   else if(EffectStructures.IsAnalysisPermission(value)){status="일부 자동 구현";family="정보·해석";implementation="등장 이벤트에서 선택 발동 여부를 묻고, 수락하면 상대의 데이터 해석 창을 엽니다.";}
   else if(EffectStructures.IsSwitchEffect(value)||EffectStructures.IsPivotGrant(value)){status="자동 구현";family="교대·등장";implementation=EffectStructures.IsPivotGrant(value)?"확정된 기술 처리 로그 뒤 유턴 효과 부여를 확인해 교대 요청을 만들고, 선택한 아군을 같은 등장 경로로 처리합니다.":"확정된 교대 트리거 로그 뒤 아군 임의교대 효과를 확인해 교대 요청을 만들고, 선택한 아군을 같은 등장 경로로 처리합니다.";}
   else if(EffectStructures.IsSwitchRestriction(value)){status="자동 구현";family="교대·등장";implementation="활성 상태의 ‘장소에 있는 한’ 교대 제한을 Engine의 교대 요청에 적용합니다. 일반 교대와 『돌아와！』, 강제교대는 허용하고 유턴·포텐셜 교대만 제한합니다.";}
   else if(EffectStructures.IsStatIgnore(value)){status="자동 구현";family="능력치 무시";implementation="피격 시 공격·특공, 명중 시 방어·특방, 속도 비교 시의 상승(랭크)·강화(배율)를 구조화된 대상과 능력치 범위에 따라 무시합니다. 기술 우선도는 속도와 별도 계산으로 유지합니다.";}
   else if(family!="기타 원문"){status="구현 규칙 준비";implementation=Prepared(family);}
   string interpretation="문장 분류: "+family+". "+(module.Length>0?"모듈 문장: "+normalized+" ":"")+Interpret(value)+(part.protectedTemplate?" 기본 템플릿이므로 정리·치환하지 않습니다.":"");
   return new EffectImplementationEntry{status=status,family=family,effect=value,interpretation=interpretation,implementation=implementation,sources=string.Join(" / ",part.sources.Take(4))+(part.sources.Count>4?" 외 "+(part.sources.Count-4)+"곳":"")};
  }
  static string Family(string value){
   if(EffectStructures.IsStatIgnore(value))return "능력치 무시";
   if(Regex.IsMatch(value,@"종족치.*(오른|올린|상승|동랭크|로 한다|취급한다)"))return "종족치 변경";
   if(EffectStructures.IsRank(value)||Regex.IsMatch(value,@"(랭크|능력치).*(올린|오른|상승|내린|저하)|전능력치가 오른다"))return "능력 랭크 변화";
   if(Regex.IsMatch(value,@"(강화|약화|완화|반감|배가|[0-9.]+배)"))return "수치 배율 변화";
   if(Regex.IsMatch(value,@"(체력|HP).*(회복|감소)|회복한다"))return "체력 회복·소모";
   if(Regex.IsMatch(value,@"(상태이상|상태변화|화상|독|맹독|마비|잠듦|얼음|동상|혼란|풀죽음).*(한다|된다|해제|회복|무효)"))return "상태이상·상태변화";
   if(Regex.IsMatch(value,@"(날씨|쾌청|비|모래바람|싸라기눈|필드|트릭룸|중력|설치기|스텔스록|압정)"))return "장소·날씨·설치물";
   if(Regex.IsMatch(value,@"(교대|필드에 내보|장소에 내보|장소를 떠)"))return "교대·등장";
   if(Regex.IsMatch(value,@"(무효화|무효로|관통|회피|반사|버틴다|미룬다)"))return "방호·무효·관통";
   if(Regex.IsMatch(value,@"(우선도|먼저 행동|행동 순서|추가행동)"))return "행동 순서·우선도";
   if(Regex.IsMatch(value,@"(타입을|타입이|타입으로|타입 상성|효과가 굉장|효과가 별로)"))return "타입·상성 변경";
   if(Regex.IsMatch(value,@"(기술 「|기능확장|내보낼 수|기술을 변경|기술로 변경|기술을 잊)"))return "기술 생성·변경";
   if(Regex.IsMatch(value,@"(소지품|도구|리사이클|아이템)"))return "소지품";
   if(Regex.IsMatch(value,@"(데이터를 해석|상세를 확인|정보를|데이터 해석)"))return "정보·해석";
   if(Regex.IsMatch(value,@"(대미지|데미지|위력|급소|명중률|명중|회피율)"))return "공격 판정·대미지";
   return "기타 원문";
  }
  // 교대·유턴·교대 제한·상승 무시·데이터 해석은 아래 전용 설명을 쓴다
  static bool SpecificModule(string module){return module.Split(new[]{" + "},StringSplitOptions.None).Any(x=>new[]{"교대 효과","유턴 효과","교대 제한","능력 상승·강화 무시","데이터 해석"}.Contains(x));}
  static string ModuleImplementation(string module){
   var parts=new List<string>();
   foreach(string kind in module.Split(new[]{" + "},StringSplitOptions.RemoveEmptyEntries))switch(kind){
    case "수치 배율":parts.Add("계속 성립하는 포텐셜(상시·필드에 있는 한·「…일 때」·적진 타입 조건)은 PotentialModifiers가 대미지 계산의 해당 단계(능력치·위력·주는/받는 대미지·명중)에 곱합니다. 사건으로 발동하면 NT동안·T종료시까지는 그 기간, 기술·피격 사건은 이번 T, 그 밖에는 필드를 떠날 때까지 유지합니다. 기술 조건(타입·물리/특수·타입 일치/불일치·효과가 굉장/별로/보통·단일/전체·위력·태그·기술 이름)을 판정하고, 급소는 방어 측 약화를 무시합니다.");break;
    case "능력 랭크 변화":parts.Add("Mechanics.ChangeStage(명중·회피·급소는 해당 랭크)로 바꾸고 능력 변화 사건을 냅니다. 랜덤한 능력치·가장 높은/낮은 능력은 자동으로 고릅니다.");break;
    case "종족치 변경":parts.Add("지정 등급으로 변경·지정 등급까지 상승·N랭크 상승·가장 높은/낮은 종족치 복사·다른 종족치 복사·트레이너 자질 등급까지 상승을 적용합니다. 가장 최근 효과로 덮어쓰고 교대하면 원래 값으로 돌아갑니다.");break;
    case "최대 HP의 1/N":parts.Add("회복은 빈사에 적용하지 않고 회복봉인을 따릅니다. 대미지는 간접 대미지(매직가드에 막힘, 빈사 불가 지정 가능), 소비는 대가(매직가드 무시, 체력이 소비량 이하이면 실패해 뒤 효과도 실패)입니다.");break;
    case "상태이상 치유":parts.Add("대상의 상태이상을 정상으로 되돌립니다.");break;
    case "상태 부여":parts.Add("StateEffects 표로 상태이상·상태변화를 겁니다. 면역과 이미 걸린 상태(실패, 남은 턴 유지)는 기술과 같은 규칙입니다.");break;
    case "우선도 변화":parts.Add("PriorityEffectRuntime이 이번 T 우선도를 보정합니다.");break;
    case "장소 전개":parts.Add("날씨·지형·트릭룸·중력을 기술과 같은 규칙으로 전개합니다(기간이 없으면 5T, 특수 날씨는 덮어쓸 수 없고 같은 날씨·지형이면 실패, 트릭룸은 다시 쓰면 해제). 이계 《필드》와는 별개입니다.");break;
    case "설치물":parts.Add("적진에 기술과 같은 층수 제한으로 설치하고, 최대면 실패해 묶인 뒤 효과도 실패합니다. 자진의 설치물 제거는 자기 진영 설치물을 모두 없앱니다.");break;
    case "강제교대":parts.Add("행동 뒤 교대 단계에서 상대 멤버 중 무작위로 교대시킵니다. 교대할 멤버가 없거나 상대가 강제교대되지 않으면 실패합니다.");break;
    case "강제교대 무효":parts.Add("계속 성립하는 동안 포텐셜 강제교대를 받지 않습니다.");break;
    case "추가행동":parts.Add("기술과 대상을 새로 고르는 추가행동을 얻습니다(「같은 기술」이면 직전 기술). 가장 먼저는 일반 행동 앞(사건이면 바로 다음), 가장 마지막에는 턴 마지막 행동입니다. 배수 T 조건과 기술 조건을 판정하고, 추가행동 중에는 추가행동을 얻지 못하며 교대·빈사면 사라집니다. 「지령에 의한 추가행동권」은 다른 규칙이라 아직 판정 대기입니다.");break;
    case "기술 무효화":parts.Add("방호(포켓몬 포텐셜 출처)입니다. 명중 판정 직전에 조건에 맞는 상대 기술을 무효화하고 「무효화했을 때」 사건을 냅니다. 확률이 있으면 공격마다 판정합니다. 공격 측의 방호 관통이 조건에 맞으면 뚫립니다.");break;
    case "회피":parts.Add("방호가 아니므로 방호 관통에 뚫리지 않습니다. 명중 판정 직전에 공격마다 확률을 판정하고, 필중 기술은 회피하지 못합니다.");break;
    case "방호 관통":parts.Add("조건에 맞는 자신의 기술이 상대의 포켓몬 포텐셜 방호(기술 무효화)를 무시합니다. 회피·방어 기술·방호 특성·트레이너 방호는 뚫지 않습니다(확정 28).");break;
    case "데이터 해석 무효":parts.Add("상대의 해석 허가 효과가 자신에게 실패합니다.");break;
    case "부유":parts.Add("계속 성립하는 동안 접지하지 않습니다(중력·철구·뿌리박기는 우선).");break;
    case "상태이상 면역":parts.Add("계속 성립하는 동안 지정한 상태이상에 걸리지 않습니다(기술·포텐셜 모두).");break;
   }
   return "공통 효과 큐(사용 제한·확률·선택·소비) 뒤 실행합니다. "+string.Join(" ",parts);
  }
  static string Prepared(string family){
   switch(family){
    case "종족치 변경":return "대상 종족치에 직접 수치를 지정하거나 가장 높은/낮은 종족치 또는 지정한 다른 능력의 실제 종족치 숫자를 복사합니다. 다른 종족치와 동랭크까지 올린다는 표현도 현재 값 비교 없이 복사합니다. 1·2랭크 상승은 기존의 -, 무표기, +를 유지한 도착 등급 범위에서 값을 고릅니다. 지정 등급까지 상승·하락은 기준을 이미 넘은 값을 유지하고, 지정 등급으로 변경은 현재 값과 관계없이 바꿉니다. 동률 비교는 후보 중 무작위입니다. 포텐셜 실행기 연결은 아직 준비 단계입니다.";
    case "능력 랭크 변화":return "대상, 능력 종류, 증감 랭크를 구조화한 뒤 Fighter.Stages, AccuracyStage, EvasionStage, CriticalStage에 연결할 예정입니다. 임의의 능력은 공격·방어·특공·특방·속도·명중·회피·C 중 사용자가 선택하고, 랜덤한 능력치는 공격·방어·특공·특방·속도 중 자동 선택합니다. 가장 높은/낮은 능력은 5개 전투 능력의 현재 랭크·배율·상태·장소 보정을 적용한 실능력값을 비교하고 동률 후보 중 무작위로 정합니다. 아직 포텐셜에서 자동 적용되지는 않습니다.";
    case "수치 배율 변화":return "대상 수치와 괄호 안 배율을 구조화한 뒤 Fighter.Multipliers 또는 대미지 계산 배율에 곱하도록 준비한 분류입니다. 적용 기간과 중첩 기준 확정이 필요합니다.";
    case "체력 회복·소모":return "최대 HP/N은 소수점을 버리고 최소 1로 계산합니다. 회복은 최대 HP를 넘지 않고, 대미지는 기본적으로 빈사가 가능하며 ‘빈사로 할 수 없다’가 지정된 경우만 HP 1에서 멈춥니다. 포텐셜 실행기에서 Mechanics.Heal/Hurt에 연결할 예정입니다.";
    case "상태이상·상태변화":return "상태 이름과 부여·해제 대상을 구조화해 Mechanics.ApplyStatus와 ConditionState에 연결할 예정입니다. 기존 타입·특성 면역 판정은 재사용합니다.";
    case "장소·날씨·설치물":return "Rules의 Weather, Terrain, TrickRoom, Gravity, Sides 상태에 연결할 예정입니다. 지속 턴과 덮어쓰기 규칙은 상태·날씨 도감의 기존 규칙을 사용합니다.";
    case "교대·등장":return "등장 트리거는 EntryTriggers에서 선발·통상교대·죽어내밀기 등 모든 등장을 받고 강제교대만 제외합니다. 표기는 ‘장소에 나올 때’로 통일합니다. 그 밖의 교대 요청과 퇴장 효과는 실행기 연결이 필요합니다.";
    case "방호·무효·관통":return "공격 판정 단계별 방호 태그와 관통 범위를 만든 뒤 Block/대미지 처리에 연결할 예정입니다. 트레이너 방호는 포켓몬 관통 대상에서 제외합니다.";
    case "행동 순서·우선도":return "기술 우선도 계산과 행동 순서 결정 전에 보정하도록 분류했습니다. 같은 시점 효과가 겹칠 때의 우선순위가 필요합니다.";
    case "타입·상성 변경":return "공격·방어 타입과 일시 타입을 분리해 Mechanics.TypeEffect에 전달할 예정입니다. 원래 타입의 유지 여부를 문장별로 확인해야 합니다.";
    case "기술 생성·변경":return "기술 데이터 조회 후 이번 행동의 Move를 복제·교체하도록 분류했습니다. 기능확장과 기술 습득은 별도 취급할 예정입니다.";
    case "소지품":return "현재 소지품, 파괴·소모 여부, 복원 대상을 별도 상태로 만든 뒤 처리할 예정입니다.";
    case "정보·해석":return "상대 데이터와 선행입력 공개 범위를 UI에 표시하는 효과입니다. 공개되는 항목 범위를 확정해야 합니다.";
    default:return "대미지 계산의 위력·명중·급소·최종 대미지 단계 중 어느 위치에 적용할지 구조화할 예정입니다. 문장별 계산 단계 확인이 필요합니다.";
   }
  }
  static string Interpret(string value){var rate=Regex.Match(value,@"([0-9]+(?:\.[0-9]+)?)배");var rank=Regex.Match(value,@"([0-9]+)랭크");var chance=PotentialProbability.Rate(value);return (rate.Success?"인식 배율 "+rate.Groups[1].Value+"배. ":"")+(rank.Success?"인식 랭크 "+rank.Groups[1].Value+". ":"")+(chance.HasValue?"인식 발동률 "+(int)(chance.Value*100)+"%. ":"")+"원문을 실행 규칙으로 바꾸기 전 검토 결과입니다.";}
  public static Control CreateView(){
   var entries=Entries();var host=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var status=Theme.Combo(new[]{"전체"}.Concat(entries.Select(x=>x.status).Distinct().OrderBy(x=>x=="자동 구현"?0:x=="일부 자동 구현"?1:x=="타입 지정 필요"?2:3)),"전체");status.Dock=DockStyle.Top;var family=Theme.Combo(new[]{"전체 분류"}.Concat(entries.GroupBy(x=>x.family).OrderByDescending(x=>x.Count()).Select(x=>x.Key)),"전체 분류");family.Dock=DockStyle.Top;var search=new PixelSearchBox{Dock=DockStyle.Top};var showDone=new PixelCheckBox{Text="구현 완료(자동 구현)도 보기",Checked=false,Dock=DockStyle.Top,Height=40,Font=Theme.UI(8)};var count=Theme.Label("",30);count.Dock=DockStyle.Top;
   var grid=new PixelDexGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersHeight=34,DefaultCellStyle=new DataGridViewCellStyle{Font=Theme.Input(),SelectionBackColor=Color.Black,SelectionForeColor=Color.White},ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.Black,ForeColor=Color.White,Font=Theme.UI(8)}};
   grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="구현 상태",Width=120});grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="분류",Width=150});grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="효과 원문",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=480});var detail=new PixelText{Dock=DockStyle.Bottom,Height=210};
   Action show=()=>{var x=grid.CurrentRow==null?null:grid.CurrentRow.Tag as EffectImplementationEntry;detail.Text=x==null?"검색 결과 없음":"【"+x.status+"】 "+x.family+"\n\n원문: "+x.effect+"\n\n해석: "+x.interpretation+"\n\n실제 처리: "+x.implementation+"\n\n출처: "+x.sources;};
   Action refresh=()=>{string q=search.Text.Trim();var shown=entries.Where(x=>(showDone.Checked||x.status!="자동 구현"||status.Text=="자동 구현")&&(status.Text=="전체"||x.status==status.Text)&&(family.Text=="전체 분류"||x.family==family.Text)&&(x.effect+x.family+x.status+x.interpretation+x.implementation+x.sources).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).ToList();grid.Rows.Clear();foreach(var x in shown){int i=grid.Rows.Add(x.status,x.family,x.effect);grid.Rows[i].Tag=x;}count.Text="남은 효과 "+entries.Count(x=>x.status!="자동 구현")+"개 (자동 구현 "+entries.Count(x=>x.status=="자동 구현")+"개는 편집기 모듈로 처리되어 숨김) · 현재 표시 "+shown.Count+"개 · "+string.Join(" / ",entries.GroupBy(x=>x.status).Select(x=>x.Key+" "+x.Count()));if(grid.RowCount>0)grid.CurrentCell=grid.Rows[0].Cells[0];show();};
   grid.SelectionChanged+=(s,e)=>show();status.SelectedIndexChanged+=(s,e)=>refresh();showDone.CheckedChanged+=(s,e)=>refresh();family.SelectedIndexChanged+=(s,e)=>refresh();search.TextChanged+=(s,e)=>refresh();host.Controls.Add(new PixelGridHost(grid));host.Controls.Add(detail);host.Controls.Add(search);host.Controls.Add(showDone);host.Controls.Add(family);host.Controls.Add(status);host.Controls.Add(count);host.Controls[0].BringToFront();refresh();return host;
  }
  public static void Test(){PotentialProbability.Test();var all=Entries();int expected=PotentialParts.Build(PotentialParts.StandardEntries()).effects.Count(x=>!x.excludedFromImplementation);if(all.Count!=expected)throw new Exception("Implementation dex effect count mismatch");if(!all.Any(x=>x.status=="자동 구현"&&x.family=="트레이너 4식·지령")||!all.Any(x=>x.family=="확률 발동"&&x.status=="구현 규칙 준비")||!all.Any(x=>x.status=="구현 규칙 준비")||!all.Any(x=>x.status=="미구현"))throw new Exception("Implementation dex statuses missing");var switchEntry=Analyze(new PotentialPart{text=EffectNormalizer.CanonicalSwitch()});var pivotEntry=Analyze(new PotentialPart{text=EffectNormalizer.CanonicalPivotGrant()});var ignoreEntry=Analyze(new PotentialPart{text=EffectNormalizer.CanonicalStatIgnore("『야생의 두목』","능력","상승(강화)")});if(switchEntry.status!="자동 구현"||pivotEntry.status!="자동 구현"||ignoreEntry.status!="자동 구현"||ignoreEntry.family!="능력치 무시")throw new Exception("Structured effect implementation status missing");var analysisEntry=Analyze(new PotentialPart{text=EffectNormalizer.CanonicalAnalysisPermission()});if(analysisEntry.status!="일부 자동 구현"||analysisEntry.family!="정보·해석")throw new Exception("Analysis effect implementation status missing");}
 }
 partial class DataEditorForm {void BuildEffectImplementationTab(){var page=Page("효과 구현 방식");bool built=false;Action build=()=>{if(built||!page.Visible)return;built=true;var view=EffectImplementationDex.CreateView();page.Controls.Add(view);view.BringToFront();};page.VisibleChanged+=(s,e)=>build();/* 목록 계산이 무거워 화면을 처음 열 때 만든다 */}}
}
