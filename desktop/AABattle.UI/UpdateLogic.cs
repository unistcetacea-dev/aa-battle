using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AABattle {
 // 업데이트 설정: 저장소 경로와 URL은 이 한 곳에서만 정한다
 static class UpdateConfig {
  public const string Repository = "unistcetacea-dev/aa-battle";
  public const string Branch = "main";
  public const string Folder = "release/windows";
  public static readonly string[] Files = { "AABattle.exe", "AABattleDataEditor.exe" };

  public static string RawUrl(string file) {
   return "https://raw.githubusercontent.com/" + Repository + "/" + Branch + "/" + Folder + "/" + file;
  }

  public static string VersionUrl { get { return RawUrl("version.txt"); } }
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

 // version.txt: 첫 비어 있지 않은 줄 = 버전, 나머지 = 변경 내용
 class UpdateManifest {
  public string Version = "";
  public string Notes = "";

  public static UpdateManifest Parse(string text) {
   if (string.IsNullOrWhiteSpace(text)) return null;
   string[] lines = text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
   int first = 0;
   while (first < lines.Length && lines[first].Trim().Length == 0) first++;
   if (first >= lines.Length) return null;
   string version = lines[first].Trim();
   if (UpdateVersion.Parse(version) == null) return null;
   var notes = new StringBuilder();
   for (int i = first + 1; i < lines.Length; i++) notes.AppendLine(lines[i].TrimEnd());
   return new UpdateManifest { Version = version, Notes = notes.ToString().Trim() };
  }
 }

 // 받은 파일 검사: 크기와 PE 헤더(MZ + PE\0\0)
 static class UpdateFileCheck {
  public const int MinimumSize = 100 * 1024;

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
