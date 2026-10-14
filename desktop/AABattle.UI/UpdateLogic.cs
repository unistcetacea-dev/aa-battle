using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AABattle {
 // 업데이트 설정: 저장소 경로와 URL은 이 한 곳에서만 정한다
 static class UpdateConfig {
  public const string Repository = "unistcetacea-dev/aa-battle";
  // zip 자산 이름: AABattle-<버전>.zip
  public const string AssetPrefix = "AABattle-";
  // 교체 대상: 실행 파일 2개와 함께 쓰는 어셈블리(.config는 바뀌지 않아 zip에만 넣는다)
  public static readonly string[] Files = { "AABattle.exe", "AABattleDataEditor.exe", "AABattle.UI.dll", "AABattle.Engine.dll" };

  public static string LatestReleaseUrl {
   get { return "https://api.github.com/repos/" + Repository + "/releases/latest"; }
  }
 }

 // 점으로 나눈 숫자 버전(예: 2026.10.14.1). 형식이 틀리면 null
 static class UpdateVersion {
  public static int[] Parse(string text) {
   if (text == null) return null;
   string[] parts = text.Trim().Split('.');
   if (parts.Length < 1 || parts.Length > 4) return null;
   var numbers = new int[4];
   for (int i = 0; i < parts.Length; i++) {
    int value;
    if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value)) return null;
    numbers[i] = value;
   }
   return numbers;
  }

  // 앞이 더 크면 양수, 같으면 0, 작으면 음수. 해석할 수 없으면 null
  public static int? Compare(string left, string right) {
   int[] a = Parse(left);
   int[] b = Parse(right);
   if (a == null || b == null) return null;
   for (int i = 0; i < 4; i++) {
    if (a[i] != b[i]) return a[i].CompareTo(b[i]);
   }
   return 0;
  }

  // 받아온 버전이 내 버전보다 높을 때만 true (형식 오류는 false)
  public static bool IsNewer(string remote, string local) {
   int? result = Compare(remote, local);
   return result.HasValue && result.Value > 0;
  }
 }

 // GitHub Releases "latest" JSON: tag_name = 버전(앞의 v는 뗀다), body = 변경 내용, assets 중 AABattle-*.zip = 받을 파일
 class UpdateManifest {
  public string Version = "";
  public string Notes = "";
  public string AssetUrl = "";

  public static UpdateManifest Parse(string json) {
   if (string.IsNullOrWhiteSpace(json)) return null;
   Dictionary<string, object> root;
   try {
    root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
   } catch (Exception) {
    return null;
   }
   if (root == null) return null;
   string tag = Text(root, "tag_name").Trim();
   if (tag.Length > 0 && (tag[0] == 'v' || tag[0] == 'V')) tag = tag.Substring(1);
   if (UpdateVersion.Parse(tag) == null) return null;
   string url = FindZipUrl(root);
   if (url == null) return null;
   return new UpdateManifest { Version = tag, Notes = Text(root, "body").Replace("\r\n", "\n").Trim(), AssetUrl = url };
  }

  static string Text(Dictionary<string, object> map, string key) {
   object value;
   return map.TryGetValue(key, out value) && value is string ? (string)value : "";
  }

  // assets 중 이름이 AABattle-*.zip이고 https 주소인 첫 자산
  static string FindZipUrl(Dictionary<string, object> root) {
   object assets;
   if (!root.TryGetValue("assets", out assets)) return null;
   var list = assets as System.Collections.IEnumerable;
   if (list == null || assets is string) return null;
   foreach (object item in list) {
    var asset = item as Dictionary<string, object>;
    if (asset == null) continue;
    string name = Text(asset, "name");
    string url = Text(asset, "browser_download_url");
    bool nameOk = name.StartsWith(UpdateConfig.AssetPrefix, StringComparison.OrdinalIgnoreCase)
     && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    if (nameOk && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return url;
   }
   return null;
  }
 }

 // zip 안의 파일 찾기: 루트 또는 폴더 하나 아래의 정확한 이름만 인정한다(.. 경로 거부)
 static class UpdateZip {
  public static string FindEntryName(IEnumerable<string> entryNames, string file) {
   foreach (string raw in entryNames) {
    string[] parts = raw.Replace('\\', '/').Split('/');
    if (parts.Length < 1 || parts.Length > 2) continue;
    if (parts[parts.Length - 1] != file) continue;
    if (parts.Length == 2 && (parts[0].Length == 0 || parts[0] == "." || parts[0] == "..")) continue;
    return raw;
   }
   return null;
  }

  // zip의 샘플 파티: samples/<이름>.aabdata.json (폴더 한 겹만, 이름에 경로 문자가 없을 때만)
  public static List<KeyValuePair<string, string>> SampleEntries(IEnumerable<string> entryNames) {
   var result = new List<KeyValuePair<string, string>>();
   foreach (string raw in entryNames) {
    string[] parts = raw.Replace('\\', '/').Split('/');
    if (parts.Length != 2 || !string.Equals(parts[0], "samples", StringComparison.OrdinalIgnoreCase)) continue;
    string name = parts[1];
    if (!name.EndsWith(".aabdata.json", StringComparison.OrdinalIgnoreCase)) continue;
    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("..")) continue;
    result.Add(new KeyValuePair<string, string>(raw, name));
   }
   return result;
  }

  // 풀 위치는 항상 폴더 바로 아래의 고정 이름이며, 폴더 밖으로 나가면 예외
  public static string TargetPath(string folder, string file) {
   string full = Path.GetFullPath(Path.Combine(folder, file));
   string root = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
   if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(full) != file) {
    throw new InvalidDataException("올바르지 않은 파일 경로: " + file);
   }
   return full;
  }
 }

 // 받은 파일 검사: 크기와 PE 헤더(MZ + PE\0\0)
 // EXE는 DLL을 부르는 작은 실행 파일(약 5KB)이라 크기 기준은 깨진 다운로드만 거르는 정도로 둔다
 static class UpdateFileCheck {
  public const int MinimumSize = 1024;

  public static bool LooksLikeExe(byte[] data) {
   if (data == null || data.Length < MinimumSize) return false;
   if (data[0] != 'M' || data[1] != 'Z') return false;
   int peOffset = BitConverter.ToInt32(data, 0x3C);
   if (peOffset < 0 || peOffset + 4 > data.Length) return false;
   return data[peOffset] == 'P' && data[peOffset + 1] == 'E' && data[peOffset + 2] == 0 && data[peOffset + 3] == 0;
  }
 }

 // 교체 스크립트(배치 파일) 문장 생성: 프로세스 종료 대기 → 이동 → 재실행
 static class UpdateScript {
  // cmd에서 경로를 큰따옴표로 감싸고 %를 %%로 바꾼다
  public static string Quote(string path) {
   return "\"" + path.Replace("%", "%%").Replace("\"", "") + "\"";
  }

  public static string OldName(string file) {
   return Path.GetFileNameWithoutExtension(file) + ".old" + Path.GetExtension(file);
  }

  public static string Build(int processId, string appDirectory, string downloadDirectory, IList<string> files, string restartFile) {
   var script = new StringBuilder();
   script.AppendLine("@echo off");
   script.AppendLine("chcp 65001 >nul");
   // 실행 중인 프로그램이 끝날 때까지 최대 60초 기다린다
   script.AppendLine("set /a tries=0");
   script.AppendLine(":wait");
   script.AppendLine("tasklist /FI \"PID eq " + processId + "\" 2>nul | find \"" + processId + "\" >nul");
   script.AppendLine("if errorlevel 1 goto swap");
   script.AppendLine("set /a tries+=1");
   script.AppendLine("if %tries% GEQ 60 goto done");
   script.AppendLine("ping -n 2 127.0.0.1 >nul");
   script.AppendLine("goto wait");
   script.AppendLine(":swap");
   foreach (string file in files) {
    string target = Quote(Path.Combine(appDirectory, file));
    string old = Quote(Path.Combine(appDirectory, OldName(file)));
    string fresh = Quote(Path.Combine(downloadDirectory, file));
    script.AppendLine("if exist " + old + " del /f /q " + old + " >nul 2>nul");
    script.AppendLine("if exist " + target + " move /y " + target + " " + old + " >nul");
    script.AppendLine("move /y " + fresh + " " + target + " >nul");
    // 새 파일을 옮기지 못했으면 이전 파일을 되돌린다
    script.AppendLine("if not exist " + target + " if exist " + old + " move /y " + old + " " + target + " >nul");
   }
   script.AppendLine(":done");
   script.AppendLine("start \"\" " + Quote(Path.Combine(appDirectory, restartFile)));
   script.AppendLine("rd /s /q " + Quote(downloadDirectory) + " >nul 2>nul");
   script.AppendLine("del /f /q \"%~f0\" >nul 2>nul");
   return script.ToString();
  }
 }
}
