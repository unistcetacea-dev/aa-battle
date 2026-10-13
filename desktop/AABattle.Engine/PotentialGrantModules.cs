// 포텐셜 부여: 「자신에게 『X』를 부여한다」 — 배틀 중 포켓몬에게 포텐셜을 추가한다(역할 임명과 같은 방식).
// 부여할 수 있는 포텐셜은 아래 표(칼데아 시트의 주인·특권 계열)와 편집기 템플릿(특권·범용·선) 이름이다.
// 「이 포텐셜을 『X』로 한다/취급한다」(포텐셜 별칭)는 구조가 정해지지 않아 아직 읽지 않는다.
// 연결: EffectModules.Classify · ChaldeaVariants.Normalize · PotentialExecutors(Register) · ModuleCatalog(RegisterChaldeaEffects)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class PotentialGrantModules
  {
    public const string Kind = "포텐셜 부여";

    sealed class Entry
    {
      public string Slot, Text;
    }

    // 시트에서 부여되는 포텐셜(이름 → 슬롯, 본문)
    static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
    {
      { "별의 주인", new Entry { Slot = "주인", Text = "자신이 받는 「드래곤/강철」 기술의 대미지를 약화(0.5배)한다." } },
      { "길의 주인", new Entry { Slot = "주인", Text = "자신이 받는 「땅」 기술의 대미지를 약화(0.5배)한다." } },
      { "해양의 주인", new Entry { Slot = "주인", Text = "자신이 받는 「물/얼음」 기술의 대미지를 약화(0.5배)한다." } },
      { "관의 주인", new Entry { Slot = "주인", Text = "자신이 받는 「자신과 같은 타입」 기술의 대미지를 약화(0.5배)한다." } },
      { "주인의 특권", new Entry { Slot = "특권", Text = "자신의 「체력」 종족치를 1랭크 올린다." } },
      { "왕자의 특권", new Entry { Slot = "특권", Text = "자신의 「체력」 종족치를 1랭크 올린다." } }
    };

    static readonly Regex Sentence = new Regex(@"^자신에게 (?<names>(?:『[^』]+』)+)(?:을|를) 부여한다$");

    // 이름 비교는 공백을 무시한다(『관의주인』 = 『관의 주인』)
    static string Key(string name)
    {
      return Regex.Replace(name ?? "", @"\s+", "");
    }

    static string Canonical(string name)
    {
      string key = Key(name);
      foreach (string known in Table.Keys)
        if (Key(known) == key)
          return known;
      foreach (var option in Templates())
        if (Key(option.name) == key)
          return option.name;
      return null;
    }

    static IEnumerable<RolePotentialOption> Templates()
    {
      return EditorTemplate.PrivilegePotentials.Concat(EditorTemplate.CommonPotentials).Concat(EditorTemplate.InitiativePotentials);
    }

    // 편집기 빈칸에 보여 줄 부여 가능한 이름
    public static string[] GrantableNames()
    {
      return Table.Keys.Concat(Templates().Select(x => x.name)).Distinct().ToArray();
    }

    static string[] Names(string raw)
    {
      return Regex.Matches(raw ?? "", "『([^』]+)』").Cast<Match>().Select(x => x.Groups[1].Value.Trim()).ToArray();
    }

    static string Particle(string name)
    {
      char c = (name ?? "").LastOrDefault();
      return c >= '가' && c <= '힣' && (c - '가') % 28 != 0 ? "을" : "를";
    }

    // 표준 문장으로 바꾼다. 부여할 수 있는 이름이 아니면 null
    public static string Normalize(string text)
    {
      string s = Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
      var m = Sentence.Match(s);
      if (!m.Success)
        return null;
      var names = Names(m.Groups["names"].Value).Select(Canonical).ToArray();
      if (names.Length == 0 || names.Any(x => x == null))
        return null;
      return "자신에게 " + string.Concat(names.Select(x => "『" + x + "』")) + Particle(names[names.Length - 1]) + " 부여한다.";
    }

    public static bool TryParse(string line, out string[] names)
    {
      names = new string[0];
      string s = Regex.Replace(PotentialParts.Clean(line ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
      var m = Sentence.Match(s);
      if (!m.Success)
        return false;
      names = Names(m.Groups["names"].Value).Select(Canonical).ToArray();
      return names.Length > 0 && names.All(x => x != null);
    }

    public static string Classify(string line)
    {
      string[] names;
      return TryParse(line, out names) ? Kind : "";
    }

    // 부여할 포텐셜의 슬롯과 본문
    public static bool TryDefinition(string name, out string slot, out string text)
    {
      slot = "";
      text = "";
      Entry entry;
      if (Table.TryGetValue(name ?? "", out entry))
      {
        slot = entry.Slot;
        text = entry.Text;
        return true;
      }

      var option = Templates().FirstOrDefault(x => x.name == name);
      if (option == null)
        return false;
      slot = EditorTemplate.PrivilegePotentials.Contains(option) ? "특권" : EditorTemplate.CommonPotentials.Contains(option) ? "범용" : "선";
      text = option.effect ?? "";
      return true;
    }

    public static void Register()
    {
      PotentialExecutors.Register(Kind, c => Classify(c.Line).Length > 0, Grant);
    }

    static EffectOutcome Grant(EffectCall call)
    {
      string[] names;
      var owner = call.Owner;
      if (!TryParse(call.Line, out names) || owner == null || owner.HP <= 0 || owner.Data == null)
        return EffectOutcome.Failed;
      var list = (owner.Data.potentials ?? new Potential[0]).ToList();
      bool any = false;
      foreach (string name in names)
      {
        string slot, text;
        if (!TryDefinition(name, out slot, out text))
          return EffectOutcome.Pending;
        // 이미 가진 포텐셜은 다시 부여하지 않는다
        if (list.Any(x => x != null && Key(x.name) == Key(name)))
          continue;
        list.Add(new Potential { id = slot, name = name, description = text, triggers = new[] { TriggerStructures.AlwaysLabel } });
        call.Say(owner.Data.name + ": 『" + name + "』을(를) 부여받았다");
        any = true;
      }

      if (any)
        owner.Data.potentials = list.ToArray();
      return any ? EffectOutcome.Success : EffectOutcome.Failed;
    }
  }
}
