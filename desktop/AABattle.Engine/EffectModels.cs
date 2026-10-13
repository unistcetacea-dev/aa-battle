// 배율·종족치·랭크·비율 효과 데이터
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 수치 배율: 대상(자신/상대/아군) · 단계(능력치/위력/주는 대미지/받는 대미지/명중) · 기술 조건 · 강화·약화 배율
  public sealed class ScalarEffect
  {
    public string Subject = "자신";
    public string Stage = "능력치"; // 능력치 · 위력 · 주는 대미지 · 받는 대미지 · 명중
    public int[] Stats = new int[0]; // 능력치 단계: Engine.Names 색인(1~5)
    public bool HighestStat, LowestStat;
    public MoveCondition Condition = new MoveCondition();
    public double Rate = 1;
    public Func<Fighter, Rules, double> Dynamic; // 상황에 따라 달라지는 배율(총대장: 빈사 아군 수에 비례)
    public string Text = "";
  }

  // 종족치 변경: 지정 등급으로 변경 · 지정 등급까지 상승·하락 · N랭크 상승 · 가장 높은/낮은 종족치 복사 · 다른 종족치 복사 · 직접 수치 · 트레이너 자질 등급까지
  public sealed class SpeciesEffect
  {
    public string Subject = "자신";
    public int[] Stats = new int[0]; // 0(체력)~5
    public string Operation = ""; // 변경 · 상승 · 하락 · 랭크 · 최고 복사 · 최저 복사 · 복사 · 수치 · 트레이너
    public string Grade = "";
    public int Value = -1, Ranks, Source = -1;
    public string Aptitude = ""; // 지시 · 육성 · 통솔 · 능력
  }

  // 최대 HP의 1/N: 회복 · 대미지(간접) · 소비(대가)
  public sealed class FractionEffect
  {
    public string Target = "자신", Action = "회복";
    public int Denominator = 1;
    public bool CanFaint = true;
  }

  // 능력 랭크 변화
  public sealed class RankEffect
  {
    public string Subject = "자신";
    public string Mode = "지정"; // 지정 · 임의 · 랜덤 · 최고 · 최저 · 전능력치
    public int[] Stats = new int[0]; // 1~5 능력, 6 명중, 7 회피, 8 C(급소)
    public int Amount;
  }
}
