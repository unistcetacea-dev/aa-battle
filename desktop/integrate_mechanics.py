from pathlib import Path
p=Path(__file__).with_name('BattleEngine.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public string[] Moves; public int Selected;','public string[] Moves; public int Selected; public ConditionState Conditions=new ConditionState();')
s=s.replace('f.Selected=Selected;return f;','f.Selected=Selected;f.Conditions=Conditions.Copy();return f;')
s=s.replace('public double PowerBonus, PowerMultiplier=1, DamageMultiplier=1, Stab, LevelCorrection=1;','public double PowerBonus, PowerMultiplier=1, DamageMultiplier=1, Stab, LevelCorrection=1;\n  public int WeatherTurns=5,TerrainTurns=5,TrickRoom,Gravity;public bool SheetTerrain;public Rules Copy(){return (Rules)MemberwiseClone();}')
s=s.replace('public bool Supported; }','public bool Supported;public string Reason=""; }')
s=s.replace('public class TurnResult { public Fighter[] Fighters;','public class TurnResult { public Rules Rules; public Fighter[] Fighters;')
s=s.replace('Stats(a,b.Data.level,r)[i]*m*Stage(s)','Stats(a,b.Data.level,r)[i]*m*Stage(s)*Mechanics.StatFactor(a,b,i,r)')
start=s.index('   if(a.Status=="화상"',s.index('public static Damage Calculate'))
end=s.index('   double basis=',start)
s=s[:start]+'''   power*=Mechanics.PowerFactor(a,b,m,r);
   double stab=r.Stab>0?r.Stab:m.types.Any(t=>a.Data.types.Contains(t))?1.5:1,type=Mechanics.TypeEffect(db,a,b,m,r);
   string reason=Mechanics.Block(db,a,b,m,r);if(reason.Length>0)type=0;
''' + s[end:]
s=s.replace('*(r.Critical?2:1)*r.DamageMultiplier);','*(r.Critical?2:1)*r.DamageMultiplier*Mechanics.DamageFactor(a,b,m,r));')
s=s.replace('Type=type,Supported=supported};','Type=type,Supported=supported,Reason=reason};')
start=s.index('  public static TurnResult Resolve(');end=s.index('  public static string Test(',start)
s=s[:start]+'''  public static TurnResult Resolve(Database db,Fighter[] input,Move[] moves,Rules original,Func<double> rng){
   var result=new TurnResult{Fighters=input.Select(p=>p.Copy()).ToArray(),Rules=original.Copy()};var f=result.Fighters;var r=result.Rules;
   int delta=Mechanics.Priority(f[0],moves[0]).CompareTo(Mechanics.Priority(f[1],moves[1]));if(delta==0){delta=Effective(f[0],f[1],5,r).CompareTo(Effective(f[1],f[0],5,r));if(r.TrickRoom>0)delta=-delta;}int first=delta>0?0:delta<0?1:rng()<.5?0:1;
   foreach(int i in new[]{first,1-first}){var a=f[i];var b=f[1-i];var m=moves[i];if(a.HP<=0)continue;
    if(!Mechanics.BeforeMove(a,b,m,r,rng,result.Log))continue;
    if(m.name!="방어"&&m.name!="판별")a.Conditions.ProtectChain=0;
    bool target=Mechanics.TargetsOpponent(m);string reason=target?Mechanics.Block(db,a,b,m,r):"";
    if(reason.Length>0){result.Log.Add(a.Data.name+"의 "+m.name+" — "+reason);Mechanics.Absorb(a,b,reason,r,result.Log);continue;}
    if(target&&b.Conditions.Substitute>0&&m.category=="변화"&&!Mechanics.BypassSub(a,m)){result.Log.Add("대타가 "+m.name+"을 막았다.");continue;}
    if(rng()*100>=Mechanics.Accuracy(a,b,m,r)){result.Log.Add(a.Data.name+"의 "+m.name+"! 그러나 빗나갔다!");continue;}
    if(m.category=="변화"){Mechanics.StatusMove(a,b,m,r,rng,result.Log);continue;}
    var d=Calculate(db,a,b,m,r);if(!d.Supported){result.Log.Add(m.name+": 가변 위력 — 수동 판정.");continue;}
    int damage=d.Rolls[Math.Min(15,(int)(rng()*16))];bool sub=b.Conditions.Substitute>0&&!Mechanics.BypassSub(a,m);
    if(sub){b.Conditions.Substitute=Math.Max(0,b.Conditions.Substitute-damage);result.Log.Add(m.name+": 대타에 "+damage+" 대미지. 남은 대타 HP "+b.Conditions.Substitute);}
    else{b.HP=Math.Max(0,b.HP-damage);result.Log.Add(a.Data.name+"의 "+m.name+"! "+b.Data.name+"에게 "+damage+" 대미지."+(r.Critical?" 급소!":"")+(d.Type>1?" 효과가 굉장했다!":d.Type<1?" 효과가 별로다.":""));if(b.HP==0)result.Log.Add(b.Data.name+"은 쓰러졌다!");}
    if(damage>0)Mechanics.AfterHit(a,b,m,r,rng,result.Log,sub);
   }
   Mechanics.EndTurn(f,r,result.Log);return result;
  }
''' + s[end:]
p.write_text(s,encoding='utf-8')
p=Path(__file__).with_name('build.ps1');s=p.read_text(encoding='utf-8-sig').replace('desktop\\BattleEngine.cs desktop\\BattleApp.cs','desktop\\BattleEngine.cs desktop\\Mechanics.cs desktop\\BattleApp.cs desktop\\ConditionUI.cs desktop\\MechanicsTests.cs');p.write_text(s,encoding='utf-8')
