using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace AABattle.Tests
{
  // 연구 도구(Explicit): 시트에서 뽑은 포텐셜 TSV를 엔진 판정으로 돌려 결과 TSV를 쓴다.
  // 환경 변수 CHALDEA_IN(입력 TSV) · CHALDEA_OUT(출력 TSV)가 있을 때만 동작한다.
  [TestFixture]
  public class ChaldeaJudgeTool
  {
    // 문장 목록 파일(줄마다 "T\t트리거" 또는 "E\t효과")을 읽어 정규화·모듈 판정을 출력한다
    [Test, Explicit("연구 도구: 문장 탐침")]
    public void Probe()
    {
      string input = Environment.GetEnvironmentVariable("CHALDEA_PROBE_IN");
      string output = Environment.GetEnvironmentVariable("CHALDEA_PROBE_OUT");
      if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        Assert.Inconclusive("CHALDEA_PROBE_IN / CHALDEA_PROBE_OUT 필요");
      var builder = new StringBuilder();
      foreach (string line in File.ReadAllLines(input, Encoding.UTF8))
      {
        var f = line.Split('\t');
        if (f.Length < 2)
          continue;
        if (f[0] == "T")
        {
          string normalized = TriggerStructures.NormalizeLines(f[1]).Replace("\r\n", " | ");
          var spec = TriggerModules.Parse(TriggerStructures.NormalizeLines(f[1]));
          builder.AppendLine("T\t" + f[1] + "\t" + normalized + "\t" + spec.Module);
        }
        else
        {
          var steps = PotentialQueue.JudgeSteps(f[1], f[0] == "ET");
          builder.AppendLine(f[0] + "\t" + f[1] + "\t" + EffectNormalizer.Normalize(f[1]) + "\t" + string.Join(" | ", steps.Select(s => s.Line + " => " + (s.Ok ? s.Kind : "X"))));
        }
      }

      File.WriteAllText(output, builder.ToString(), new UTF8Encoding(false));
    }

    [Test, Explicit("연구 도구: 모듈 목록 출력")]
    public void DumpCatalog()
    {
      string output = Environment.GetEnvironmentVariable("CHALDEA_PROBE_OUT");
      if (string.IsNullOrEmpty(output))
        Assert.Inconclusive("CHALDEA_PROBE_OUT 필요");
      var builder = new StringBuilder();
      foreach (var m in ModuleCatalog.All)
        builder.AppendLine(m.Kind + "\t" + m.Id + "\t" + m.Family + "\t" + m.Name + "\t" + m.Template);
      File.WriteAllText(output, builder.ToString(), new UTF8Encoding(false));
    }

    [Test, Explicit("연구 도구: 칼데아 시트 판정")]
    public void JudgeSheet()
    {
      string input = Environment.GetEnvironmentVariable("CHALDEA_IN");
      string output = Environment.GetEnvironmentVariable("CHALDEA_OUT");
      if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        Assert.Inconclusive("CHALDEA_IN / CHALDEA_OUT 필요");
      var lines = File.ReadAllLines(input, Encoding.UTF8);
      var moves = Database.Load().moves;
      var builder = new StringBuilder();
      builder.AppendLine(lines[0] + "\t트리거\t효과\t트리거모듈\t엔진판정\t읽지못한 트리거\t실행기/모듈\t읽지못한 효과줄");
      foreach (string line in lines.Skip(1))
      {
        var f = line.Split('\t');
        string kind = f[2];
        string text = f.Length > 7 && kind != "기술" && kind != "특성" ? f[7] : f[4];
        var cols = new List<string>();
        if (kind == "특성")
        {
          string note;
          string status = SheetJudge.Ability(f[3].Trim(), text, out note);
          cols.AddRange(new[] { "", "", "", status, "", note, "" });
        }
        else if (kind == "기술")
        {
          string unknown;
          string moveName = f[3].Trim();
          string status = SheetJudge.Move(moveName, text, out unknown);
          string known = Array.Exists(moves, x => x.name == moveName) ? "도감 기술 동명" : "시트 오리지널";
          cols.AddRange(new[] { "", "", "", status, "", known, unknown });
        }
        else
        {
          var r = PotentialJudge.Judge(text, kind == "고유");
          string status = r.Ok ? "OK" : !r.TriggerOk && !r.EffectOk ? "BOTH" : !r.TriggerOk ? "TRIGGER" : "EFFECT";
          cols.Add(r.Trigger.Replace("\r\n", " | "));
          cols.Add(r.Effect.Replace("\r\n", " | "));
          cols.Add(r.TriggerModules);
          cols.Add(status);
          cols.Add(string.Join(" | ", r.BadTriggers));
          cols.Add(string.Join(" | ", r.Steps.Where(s => s.Ok).Select(s => s.Kind).Distinct()));
          cols.Add(string.Join(" | ", r.Steps.Where(s => !s.Ok).Select(s => s.Line)));
        }

        builder.AppendLine(line + "\t" + string.Join("\t", cols.Select(x => x.Replace("\t", " "))));
      }

      File.WriteAllText(output, builder.ToString(), new UTF8Encoding(false));
    }
  }
}
