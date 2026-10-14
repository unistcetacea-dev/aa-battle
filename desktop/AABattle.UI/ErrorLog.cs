using System;
using System.IO;

namespace AABattle {
 // 오류 창을 띄울 때 예외 전체와 스택 추적을 실행 파일 옆 error.txt에 남긴다
 static class ErrorLog {
  public static void Write(Exception ex) {
   try {
    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.txt");
    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex + "\r\n\r\n");
   } catch (Exception) {
    // 로그를 쓰지 못해도 원래 오류 창은 그대로 보여준다
   }
  }
 }
}
