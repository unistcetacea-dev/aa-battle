namespace AABattle.Launch {
 // 배틀 실행 파일 진입점. 실제 내용은 AABattle.Core의 Program
 static class Launcher { [System.STAThread] static int Main(string[] args) => AABattle.Program.Main(args); }
}
