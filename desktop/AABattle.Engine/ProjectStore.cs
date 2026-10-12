using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace AABattle {
 // 저장 파일(.aabdata.json) 읽기·쓰기.
 // 포켓몬·트레이너는 고유 id를 갖고, 팀은 id(trainerId·memberIds)로만 참조한다.
 // 불러온 뒤 팀의 trainer·members는 프로젝트 목록의 같은 객체를 가리키므로 원본 수정이 팀에 그대로 반영된다.
 public static class ProjectStore {
  public const string Format = "aa-battle-data-v2";
  const int MaxJson = 50000000;

  public static string NewId(string prefix) {
   return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
  }

  public static EditorProject Parse(string json) {
   var project = new JavaScriptSerializer { MaxJsonLength = MaxJson }.Deserialize<EditorProject>(json ?? "");
   return Normalize(project);
  }

  // 저장 직전에 id를 채우고 팀 참조를 갱신한 뒤, 팀 사본 없이 직렬화한다
  public static string Serialize(EditorProject project) {
   Normalize(project, false);
   SyncReferences(project);
   project.format = Format;
   var saved = project.teams.Select(t => new { team = t, trainer = t.trainer, members = t.members }).ToList();
   try {
    foreach (var s in saved) { s.team.trainer = null; s.team.members = new List<PokemonRecord>(); }
    return new JavaScriptSerializer { MaxJsonLength = MaxJson }.Serialize(project);
   } finally {
    foreach (var s in saved) { s.team.trainer = s.trainer; s.team.members = s.members; }
   }
  }

  // 빈 목록 보정, id 부여, 옛 형식 팀 이전, 팀 참조 연결
  public static EditorProject Normalize(EditorProject project, bool resolve = true) {
   if (project == null) project = new EditorProject();
   if (project.teams == null) project.teams = new List<TeamRecord>();
   if (project.trainers == null) project.trainers = new List<TrainerRecord>();
   if (project.pokemon == null) project.pokemon = new List<PokemonRecord>();
   project.teams.RemoveAll(t => t == null);
   project.trainers.RemoveAll(t => t == null);
   project.pokemon.RemoveAll(p => p == null);
   EnsureIds(project.trainers, "tr", t => t.id, (t, id) => t.id = id);
   EnsureIds(project.pokemon, "pk", p => p.id, (p, id) => p.id = id);
   foreach (var team in project.teams) {
    if (team.memberIds == null) team.memberIds = new List<string>();
    if (team.members == null) team.members = new List<PokemonRecord>();
    if (team.trainerId == null) team.trainerId = "";
    MigrateCopies(project, team);
   }
   if (resolve) Resolve(project);
   return project;
  }

  // 팀의 trainer·members를 id로 프로젝트 목록의 객체에 연결한다. 없는 id는 버린다
  public static void Resolve(EditorProject project) {
   foreach (var team in project.teams) {
    team.trainer = project.trainers.FirstOrDefault(t => t.id == team.trainerId);
    team.members = team.memberIds
     .Select(id => project.pokemon.FirstOrDefault(p => p.id == id))
     .Where(p => p != null)
     .ToList();
    team.memberIds = team.members.Select(p => p.id).ToList();
    if (team.trainer == null) team.trainerId = "";
   }
  }

  // 편집 중 바뀐 trainer·members를 id 목록에 반영한다. 프로젝트 목록에 없는 객체는 목록에 추가한다
  public static void SyncReferences(EditorProject project) {
   foreach (var team in project.teams) {
    if (team.trainer != null) {
     if (!project.trainers.Contains(team.trainer)) { Adopt(project.trainers, team.trainer, "tr", t => t.id, (t, id) => t.id = id); }
     team.trainerId = team.trainer.id;
    } else team.trainerId = "";
    foreach (var member in team.members ?? new List<PokemonRecord>())
     if (!project.pokemon.Contains(member)) Adopt(project.pokemon, member, "pk", p => p.id, (p, id) => p.id = id);
    team.memberIds = (team.members ?? new List<PokemonRecord>()).Select(p => p.id).ToList();
   }
  }

  // 옛 형식: 팀이 사본을 직접 들고 있으면 id → 이름(내용이 같은 것 우선) 순서로 원본에 연결한다.
  // 원본이 없으면 사본을 목록에 추가한다
  static void MigrateCopies(EditorProject project, TeamRecord team) {
   if (team.trainer != null && team.trainerId.Length == 0) {
    var match = Match(project.trainers, team.trainer, new HashSet<object>(), t => t.id, t => t.name);
    if (match == null) { Adopt(project.trainers, team.trainer, "tr", t => t.id, (t, id) => t.id = id); match = team.trainer; }
    team.trainerId = match.id;
   }
   if (team.members.Count > 0 && team.memberIds.Count == 0) {
    var used = new HashSet<object>();
    foreach (var copy in team.members.Where(m => m != null)) {
     var match = Match(project.pokemon, copy, used, p => p.id, p => p.name);
     if (match == null) { Adopt(project.pokemon, copy, "pk", p => p.id, (p, id) => p.id = id); match = copy; }
     used.Add(match);
     team.memberIds.Add(match.id);
    }
   }
  }

  static T Match<T>(List<T> list, T copy, HashSet<object> used, Func<T, string> id, Func<T, string> name) where T : class {
   if (list.Contains(copy)) return copy;
   string copyId = id(copy) ?? "";
   if (copyId.Length > 0) {
    var byId = list.FirstOrDefault(x => id(x) == copyId);
    if (byId != null) return byId;
   }
   var sameName = list.Where(x => !used.Contains(x) && name(x) == name(copy)).ToList();
   if (sameName.Count == 0) return null;
   string content = Content(copy, id);
   return sameName.FirstOrDefault(x => Content(x, id) == content) ?? sameName[0];
  }

  // id를 빼고 비교하기 위한 내용 문자열. 리플렉션의 필드 순서는 첫 호출과 이후가 다를 수 있으므로 키를 정렬한다
  static string Content<T>(T value, Func<T, string> id) {
   var serializer = new JavaScriptSerializer { MaxJsonLength = MaxJson };
   var map = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(value));
   map.Remove("id");
   return serializer.Serialize(Sorted(map));
  }
  static object Sorted(object value) {
   var map = value as IDictionary<string, object>;
   if (map != null) { var sorted = new SortedDictionary<string, object>(StringComparer.Ordinal); foreach (var pair in map) sorted[pair.Key] = Sorted(pair.Value); return sorted; }
   var list = value as System.Collections.IList;
   if (list != null && !(value is string)) return list.Cast<object>().Select(Sorted).ToList();
   return value;
  }

  static void Adopt<T>(List<T> list, T value, string prefix, Func<T, string> id, Action<T, string> setId) {
   string current = id(value) ?? "";
   if (current.Length == 0 || list.Any(x => id(x) == current)) setId(value, NewId(prefix));
   list.Add(value);
  }

  // 빈 id·중복 id에 새 id를 준다
  static void EnsureIds<T>(List<T> list, string prefix, Func<T, string> id, Action<T, string> setId) {
   var seen = new HashSet<string>();
   foreach (var item in list) {
    string current = id(item) ?? "";
    if (current.Length == 0 || !seen.Add(current)) { current = NewId(prefix); setId(item, current); seen.Add(current); }
   }
  }
 }
}
