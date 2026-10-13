// 대미지 지연: 「상대의 기술의 대미지를 (확률) 자신의 행동 후로 미룬다」
//  표준 문장  자신은 상대의 기술의 대미지를 (드물게/저확률로/중확률로) 자신의 행동 후로 미룬다.
//  해석: 대미지 계산 뒤 체력을 깎기 전에 확률 판정. 성공하면 체력 감소만 보류한다.
//   - 아직 행동하지 않은 상대(행동 예정)에게만 걸린다. 이미 행동했으면 평소대로 즉시 적용.
//   - 보류 대미지는 자신의 행동 직후, 교대 전, 행동하지 못한 채 행동 루프가 끝났을 때 적용한다.
//   - 추가효과·반동·드레인·접촉 반응 등은 원래 시점에 처리하고, 버팀 판정은 적용 시점에 한다.
//   - 여러 번 보류되면 합산해 한 번에 적용한다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class DeferredDamage
  {
    const string Keyword = "미룬다";

    static readonly Regex Standard = new Regex(@"^자신은 상대의 기술의 대미지를 (?<p>드물게|저확률로|중확률로) 자신의 행동 후로 미룬다$");

    // 보류 중인 대미지 한 건(포켓몬별 합산)
    class Entry
    {
      public Fighter Attacker;
      public Move Move;
      public Move HitMove;
      public string DefendingForm;
      public bool Multi;
      public int Total;
      public string Label;
    }

    static readonly ConditionalWeakTable<Fighter, Entry> Pending = new ConditionalWeakTable<Fighter, Entry>();

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
    }

    // 확률 접두어까지 한 문장으로 읽어 확률을 문장 안으로 옮긴다(공격마다 판정)
    public static string NormalizeWhole(string clean)
    {
      var m = Regex.Match(clean ?? "", @"^(?<p>드물게|드믈게|저확률로|중확률로)\s*상대의 기술의 (?:대미지|데미지)를\s*자신의 행동 후로 미룬다\.?$");
      if (!m.Success)
        return null;
      string p = m.Groups["p"].Value == "드믈게" ? "드물게" : m.Groups["p"].Value;
      return "자신은 상대의 기술의 대미지를 " + p + " 자신의 행동 후로 미룬다.";
    }

    public static string Normalize(string text)
    {
      string s = Clean(text);
      return Standard.IsMatch(s) ? s + "." : null;
    }

    public static string Classify(string sentence)
    {
      return Standard.IsMatch(Clean(sentence)) ? "대미지 지연" : "";
    }

    static double Rate(string word)
    {
      return word == "드물게" ? .10 : word == "저확률로" ? .20 : .50;
    }

    // 지금 맞은 대미지를 미룰 수 있으면 보류하고 true. 확률 판정은 이 때 굴린다
    public static bool TryDefer(Fighter attacker, Fighter defender, Move move, Move hitMove, string defendingForm, bool multi, int damage, Rules rules, Func<double> rng, List<string> log)
    {
      if (damage <= 0 || defender == null || defender.HP <= 0 || !defender.Conditions.PendingAction)
        return false;
      string line = PotentialModifiers.Sentences(defender, attacker, rules ?? new Rules(), Keyword).Select(Clean).FirstOrDefault(x => Standard.IsMatch(x));
      if (line == null)
        return false;
      if ((rng ?? (() => 1))() >= Rate(Standard.Match(line).Groups["p"].Value))
        return false;
      Entry entry;
      if (!Pending.TryGetValue(defender, out entry))
      {
        entry = new Entry();
        Pending.Add(defender, entry);
      }

      entry.Attacker = attacker;
      entry.Move = move;
      entry.HitMove = hitMove;
      entry.DefendingForm = defendingForm;
      entry.Multi = multi;
      entry.Total += damage;
      entry.Label = LabelOf(defender);
      log.Add("〔" + entry.Label + "〕 " + defender.Data.name + ": " + (hitMove == null ? "" : hitMove.name + "의 ") + "대미지 " + damage + "을(를) 자신의 행동 후로 미뤘다.");
      return true;
    }

    // 로그에 쓸 포텐셜 이름
    static string LabelOf(Fighter f)
    {
      var p = (f.Data.potentials ?? new Potential[0]).FirstOrDefault(x => x != null && (x.description ?? "").IndexOf(Keyword, StringComparison.Ordinal) >= 0);
      return p == null || string.IsNullOrEmpty(p.name) ? "대미지 지연" : "『" + p.name + "』";
    }

    public static bool Has(Fighter f)
    {
      Entry entry;
      return f != null && Pending.TryGetValue(f, out entry);
    }

    // 행동을 끝낸 포켓몬의 보류 대미지를 적용한다(행동 루프 앞머리에서 부른다)
    public static void FlushActed(Database db, Fighter[] fighters, Rules rules, Func<double> rng, List<string> log)
    {
      foreach (var f in fighters)
        if (f != null && Has(f) && !f.Conditions.PendingAction)
          Flush(db, f, fighters, rules, rng, log, "행동 후");
    }

    // 행동하지 못한 채 남은 보류 대미지까지 전부 적용한다(행동 루프 끝)
    public static void FlushAll(Database db, Fighter[] fighters, Rules rules, Func<double> rng, List<string> log)
    {
      foreach (var f in fighters)
        if (f != null && Has(f))
          Flush(db, f, fighters, rules, rng, log, "행동 차례");
    }

    // 한 포켓몬의 보류 대미지를 지금 적용한다(교대 직전에도 부른다)
    public static void Flush(Database db, Fighter target, Fighter[] fighters, Rules rules, Func<double> rng, List<string> log, string when)
    {
      Entry entry;
      if (target == null || !Pending.TryGetValue(target, out entry))
        return;
      Pending.Remove(target);
      if (target.HP <= 0 || entry.Total <= 0)
        return;
      var a = entry.Attacker;
      int hpBefore = target.HP;
      log.Add("〔" + entry.Label + "〕 " + target.Data.name + ": " + when + " — 보류한 대미지 " + entry.Total + " 적용.");
      int damage = Engine.ReduceByEndure(db, a, target, entry.Move, entry.HitMove, rules, rng, entry.DefendingForm, entry.Multi, entry.Total, log);
      target.HP = Math.Max(0, target.HP - damage);
      int body = hpBefore - target.HP;
      if (body > 0)
      {
        target.Conditions.TimesHit++;
        target.Conditions.LastDamageTaken = body;
      }

      if (entry.HitMove != null && entry.HitMove.category == "물리")
        target.Conditions.TakenPhysical += body;
      else if (entry.HitMove != null && entry.HitMove.category == "특수")
        target.Conditions.TakenSpecial += body;
      MoveMiscModules.Store(target, body);
      log.Add(target.Data.name + "은 " + body + " 대미지를 받았다. (남은 HP " + target.HP + ")");
      AnsweredTriggers.HpChanged(target, a, rules, hpBefore, target.HP, entry.Label, log.Add);
      if (target.HP == 0)
      {
        log.Add(target.Data.name + "은 쓰러졌다!");
        StateTicks.Fainted(a, target, entry.HitMove, log);
        DefeatTriggers.ByMove(a, target, entry.HitMove, hpBefore, damage, log.Add);
        FieldEffectRuntime.Fire(rules, TriggerStructures.DefeatLabel, fighters, a.Data.name + "이 " + target.Data.name + "을 쓰러뜨림", rng, log.Add);
      }

      StateWatch.Check(fighters, rules, log.Add);
    }
  }
}
