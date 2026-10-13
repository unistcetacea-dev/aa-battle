using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 효과 모듈 입력 화면. 엔진 모듈 목록(Effect)의 문장 틀 빈칸을 채운다.
 // 발동 확률·효과 기간은 모듈 빈칸으로 들어 있어 표준 접두어 문장이 그대로 나온다.
 partial class DataEditorForm {
  ModuleBuilderPanel effectBuilder;
  PixelText effectPreview;

  Panel BuildStructuredEffectPanel() {
   var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
   var header = Theme.Label("효과 · 문장 틀의 빈칸 채우기", 40);
   var help = Theme.Label("분류 → 모듈 → 빈칸 순서로 채웁니다.", 58);
   help.ForeColor = Theme.Muted;
   effectBuilder = new ModuleBuilderPanel(new[] { ModuleKind.Effect }, catalogDb, () => Current as PokemonRecord);
   effectPreview = effectBuilder.Preview;
   effectSubject = Theme.Combo(new[] { "자신", "아군", "상대" }, "자신");
   var options = Theme.Table();
   Theme.Row(options, "효과 주체", effectSubject);
   var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
   actions.Controls.Add(Button("구조 효과 입력", 190, () => ApplyPart(false)));
   actions.Controls.Add(Button("효과 추가 +", 150, AppendConfiguredEffect));
   panel.Controls.Add(effectBuilder);
   panel.Controls.Add(options);
   panel.Controls.Add(help);
   panel.Controls.Add(header);
   panel.Controls.Add(actions);
   return panel;
  }

  void AppendConfiguredEffect() {
   if (editWindow == null) { GameDialog.Show(this, "표에서 포텐셜을 골라 「편집」으로 편집 창을 연 뒤 입력하세요."); return; }
   string effect = ConfiguredEffect();
   if (effect.Length == 0) return;
   editWindow.AppendEffect(effect, effectSubject == null ? "" : effectSubject.Text);
  }

  // 화면의 효과 문장. 효과 주체가 자신이 아니면 앞머리의 「자신」을 바꿔 쓴다
  string ConfiguredEffect() {
   string value = effectBuilder == null ? "" : effectBuilder.Result;
   if (value.Length == 0) return "";
   string probability, body;
   if (!EffectNormalizer.TryProbabilityPrefix(value, out probability, out body)) { probability = ""; body = value; }
   if (effectSubject != null && effectSubject.Text != "자신") {
    string owner = effectSubject.Text;
    body = Regex.Replace(body, @"^자신의", owner + "의");
    body = Regex.Replace(body, @"^자신이", owner + (owner == "아군" ? "이" : "가"));
    body = Regex.Replace(body, @"^자신에게", owner + "에게");
   }
   return EffectNormalizer.WithProbability(probability, body);
  }

  // 저장된 효과 문장(첫 줄)을 모듈 빈칸으로 되돌린다. 읽을 수 없으면 false(화면은 그대로)
  bool LoadStructuredEffectSettings(string text, string subject = "") {
   if (effectBuilder == null) return false;
   string line = (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0) ?? "";
   if (line.Length == 0) return false;
   string unqualified = Regex.Replace(line, @"\s*\((?:필요|조건)[^)]*\)\s*$", "");
   var candidates = new List<string>();
   foreach (string source in new[] { line, unqualified }) {
    candidates.Add(source);
    candidates.Add(EffectNormalizer.Normalize(source));
    string probability, body;
    if (EffectNormalizer.TryProbabilityPrefix(source, out probability, out body))
     candidates.Add(EffectNormalizer.WithProbability(probability, EffectNormalizer.Normalize(body)));
   }
   if (subject != "" && subject != "자신") {
    string pattern = "^" + Regex.Escape(subject) + @"(?=의|이|가|에게)";
    Func<string, string> swap = candidate => {
     string chance, rest;
     if (!EffectNormalizer.TryProbabilityPrefix(candidate, out chance, out rest)) return Regex.Replace(candidate, pattern, "자신");
     return EffectNormalizer.WithProbability(chance, Regex.Replace(rest, pattern, "자신"));
    };
    candidates.AddRange(candidates.ToArray().Select(swap));
   }
   return effectBuilder.Load(candidates.Distinct().ToArray());
  }
 }
}
