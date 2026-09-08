'use client';
import { useState } from 'react';
import {
  Swords,
  RotateCcw,
  Pencil,
  ChevronRight,
  Settings2,
  ArrowRight,
  CircleHelp,
  Undo2,
  Plus,
  Terminal,
} from 'lucide-react';
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Checkbox } from '@/components/ui/checkbox';
import { Progress } from '@/components/ui/progress';
import {
  calculate,
  resolveTurn,
  defaultRules,
  effective,
  fighter,
  moves,
  pokemon,
  stats,
  statNames,
  types,
  type Fighter,
  type Rules,
  type Move,
} from '@/lib/battle';
import arts from '@/lib/reference-aa.json';
function Pick({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (v: string) => void;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <Select value={value} onValueChange={(v) => v !== null && onChange(v)}>
        <SelectTrigger aria-label={label}>
          <SelectValue>{value}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          {Array.from(new Set(options)).map((v) => (
            <SelectItem key={v} value={v}>
              {v}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </label>
  );
}
function Num({
  label,
  value,
  onChange,
  min = 0,
  max = 9999,
  step = 1,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
  min?: number;
  max?: number;
  step?: number;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <input
        type="number"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => {
          const v = Number(e.target.value);
          if (Number.isFinite(v)) onChange(Math.min(max, Math.max(min, v)));
        }}
      />
    </label>
  );
}
function art(index: number) {
  const lines = (arts[index] || '').split('\n');
  const end = lines.findIndex((x, i) => i > 10 && x.includes('【이름'));
  return lines.slice(1, end > 0 ? end - 1 : 45).join('\n');
}
function initial() {
  return [
    fighter(
      pokemon.find((p) => p.name === '선데이')!,
      art(4),
    ),
    fighter(
      pokemon.find((p) => p.name === '잭 한마')!,
      art(3),
    ),
  ];
}
export default function Home() {
  const [teams, setTeams] = useState<Fighter[][]>(() =>
      initial().map((p) => [p]),
    ),
    [active, setActive] = useState([0, 0]);
  const fighters = teams.map((t, i) => t[active[i]]);
  const [selected, setSelected] = useState(['파괴광선', '깨물어부수기']),
    [rules, setRules] = useState<Rules>(defaultRules),
    [turn, setTurn] = useState(1),
    [logs, setLogs] = useState<string[]>([
      '양쪽 포켓몬과 기술을 확인한 뒤 턴을 실행하세요.',
    ]);
  const [history, setHistory] = useState<
      { teams: Fighter[][]; active: number[]; turn: number; logs: string[] }[]
    >([]),
    [editor, setEditor] = useState<number | null>(null),
    [draft, setDraft] = useState<Fighter | null>(null),
    [help, setHelp] = useState(false),
    [zoom, setZoom] = useState(15),
    [search, setSearch] = useState(''),
    [custom, setCustom] = useState<Record<number, Move>>({});
  const chosen = fighters.map(
      (_, i) =>
        custom[i] || moves.find((m) => m.name === selected[i]) || moves[0],
    ),
    calculations = fighters.map((a, i) =>
      calculate(a, fighters[1 - i], chosen[i], rules),
    );
  function patchRule(key: keyof Rules, value: Rules[keyof Rules]) {
    setRules((r) => ({ ...r, [key]: value }));
  }
  function patchFighter(side: number, patch: Partial<Fighter>) {
    setTeams((ts) =>
      ts.map((team, i) =>
        i === side
          ? team.map((p, j) => (j === active[i] ? { ...p, ...patch } : p))
          : team,
      ),
    );
  }
  function snapshot() {
    setHistory((h) => [
      ...h.slice(-19),
      {
        teams: structuredClone(teams),
        active: [...active],
        turn,
        logs: [...logs],
      },
    ]);
  }
  function selectMove(i: number, v: string) {
    setSelected((s) => s.map((x, k) => (k === i ? v : x)));
    setCustom((c) => {
      const n = { ...c };
      delete n[i];
      return n;
    });
  }
  function undo() {
    const prev = history.at(-1);
    if (prev) {
      setTeams(prev.teams);
      setActive(prev.active);
      setTurn(prev.turn);
      setLogs(prev.logs);
      setHistory((h) => h.slice(0, -1));
    }
  }
  function execute() {
    if (fighters.some((p) => p.hp <= 0)) return;
    snapshot();
    const result = resolveTurn(fighters, chosen, rules);
    setTeams((ts) =>
      ts.map((t, i) =>
        t.map((p, j) => (j === active[i] ? result.fighters[i] : p)),
      ),
    );
    setLogs((l) => [`TURN ${turn}`, ...result.entries, ...l].slice(0, 150));
    setTurn((t) => t + 1);
  }
  function edit(i: number) {
    setDraft(structuredClone(fighters[i]));
    setEditor(i);
    setSearch('');
  }
  return (
    <main className="battle-app">
      <header className="topbar">
        <div className="brand">
          <Swords size={25} />
          <div>
            AA BATTLE LAB <span>창작 포켓몬 배틀 시뮬레이터</span>
          </div>
        </div>
        <div className="top-actions">
          <span className="build-tag">
            <i /> TEST BUILD 0.1
          </span>
          <button onClick={() => setHelp(true)} aria-label="구현 범위와 사용법">
            <CircleHelp size={19} />
          </button>
          <button
            onClick={() => {
              snapshot();
              setTeams((ts) =>
                ts.map((t, i) =>
                  t.map((p) => ({
                    ...p,
                    hp: stats(p, fighters[1 - i].level, rules)[0],
                    stages: [0, 0, 0, 0, 0, 0],
                    status: '정상',
                  })),
                ),
              );
              setTurn(1);
              setLogs(['배틀이 초기화되었습니다.']);
            }}
          >
            <RotateCcw size={16} /> 배틀 초기화
          </button>
        </div>
      </header>
      <section className="fieldbar">
        <div className="field-title">
          <span className="live-dot" /> 배틀 필드 <small>SINGLE BATTLE</small>
        </div>
        <div className="environment">
          <Pick
            label="날씨"
            value={rules.weather}
            options={['없음', '쾌청', '비']}
            onChange={(v) => patchRule('weather', v)}
          />
          <Pick
            label="필드"
            value={rules.terrain}
            options={[
              '없음',
              '그래스필드',
              '일렉트릭필드',
              '사이코필드',
              '미스트필드',
            ]}
            onChange={(v) => patchRule('terrain', v)}
          />
        </div>
        <div className="turn-label">
          TURN <strong>{String(turn).padStart(2, '0')}</strong>
        </div>
      </section>
      <div className="arena">
        {fighters.map((p, i) => {
          const max = stats(p, fighters[1 - i].level, rules)[0];
          return (
            <section className={`combatant side-${i}`} key={i}>
              <div className="team-heading">
                <span>
                  <b>0{i + 1}</b> TEAM {i + 1}
                </span>
                <div className="roster">
                  {teams[i].map((v, j) => (
                    <button
                      title={`${v.name} · HP ${v.hp}`}
                      aria-label={`${v.name}으로 교대`}
                      className={`${j === active[i] ? 'active' : ''} ${v.hp <= 0 ? 'fainted' : ''}`}
                      key={j}
                      onClick={() => {
                        if (j === active[i] || v.hp <= 0) return;
                        snapshot();
                        setActive((a) => a.map((v, k) => (k === i ? j : v)));
                        selectMove(i, teams[i][j].moves[0] || '몸통박치기');
                        setLogs((l) => [
                          `${p.name} → ${v.name} 교대 (수동 교대, 턴 소모 없음)`,
                          ...l,
                        ]);
                      }}
                    >
                      {j + 1}
                    </button>
                  ))}
                  {teams[i].length < 6 && (
                    <button
                      onClick={() =>
                        setTeams((ts) =>
                          ts.map((t, k) =>
                            k === i ? [...t, fighter(pokemon[0])] : t,
                          ),
                        )
                      }
                      aria-label={`팀 ${i + 1} 포켓몬 추가`}
                    >
                      <Plus size={12} />
                    </button>
                  )}
                </div>
              </div>
              <div className="pokemon-header">
                <div>
                  <div className="eyebrow">
                    {p.team || 'CUSTOM POKÉMON'} <span>Lv. {p.level}</span>
                  </div>
                  <h1>{p.name}</h1>
                  <div className="type-row">
                    {p.types.map((t) => (
                      <span className={`type type-${t}`} key={t}>
                        {t}
                      </span>
                    ))}
                    <span className="condition">
                      {p.hp === 0 ? '기절' : p.status}
                    </span>
                  </div>
                </div>
                <button
                  className="icon-button"
                  onClick={() => edit(i)}
                  aria-label={`${p.name} 데이터와 AA 편집`}
                >
                  <Pencil size={16} />
                </button>
              </div>
              <div className="hp-block">
                <div>
                  <span>HP</span>
                  <strong>
                    {p.hp} <em>/ {max}</em>
                  </strong>
                </div>
                <Progress
                  aria-label={`${p.name} HP`}
                  value={Math.min(100, (p.hp / max) * 100)}
                  className={p.hp / max < 0.25 ? 'low-hp' : ''}
                />
              </div>
              <div className="art-stage">
                <div className="art-label">
                  {p.aa ? 'REFERENCE AA · 표시 테스트' : 'AA 미등록'}
                </div>
                <pre
                  className="aa"
                  style={{ fontSize: zoom, lineHeight: `${zoom + 1}px` }}
                >
                  {p.aa ||
                    '\n\n　AA를 붙여 넣어 주세요.\n\n　편집 버튼에서 등록할 수 있습니다.'}
                </pre>
                <button className="art-edit" onClick={() => edit(i)}>
                  <Pencil size={13} /> AA 편집
                </button>
              </div>
              <div className="attributes">
                <span>
                  특성 <b>{p.ability || '없음'}</b>
                </span>
                <span>
                  소지품 <b>{p.item || '없음'}</b>
                </span>
                <span className="muted">효과 수동 반영</span>
              </div>
              <div className="stat-grid">
                {statNames.slice(1).map((name, k) => (
                  <div key={name}>
                    <span>{name}</span>
                    <strong>
                      {effective(p, fighters[1 - i], k + 1, rules)}
                    </strong>
                    <div className="stage-control">
                      <button
                        aria-label={`${p.name} ${name} 랭크 감소`}
                        onClick={() =>
                          patchFighter(i, {
                            stages: p.stages.map((v, j) =>
                              j === k + 1 ? Math.max(-6, v - 1) : v,
                            ),
                          })
                        }
                      >
                        −
                      </button>
                      <span>
                        {p.stages[k + 1] > 0 ? '+' : ''}
                        {p.stages[k + 1]}
                      </span>
                      <button
                        aria-label={`${p.name} ${name} 랭크 증가`}
                        onClick={() =>
                          patchFighter(i, {
                            stages: p.stages.map((v, j) =>
                              j === k + 1 ? Math.min(6, v + 1) : v,
                            ),
                          })
                        }
                      >
                        +
                      </button>
                    </div>
                  </div>
                ))}
              </div>
              <div className="moves-area">
                <div className="section-label">
                  행동 선택{' '}
                  <small>
                    우선도 {chosen[i].priority >= 0 ? '+' : ''}
                    {chosen[i].priority}
                  </small>
                </div>
                <div className="move-grid">
                  {Array.from(new Set([...p.moves.slice(0, 4), selected[i]]))
                    .slice(0, 4)
                    .map((name) => {
                      const m = moves.find((m) => m.name === name);
                      return (
                        <button
                          className={
                            selected[i] === name && !custom[i] ? 'selected' : ''
                          }
                          key={name}
                          onClick={() => selectMove(i, name)}
                        >
                          <strong>{name}</strong>
                          <small>
                            {m?.types.join(' / ')}{' '}
                            <span>
                              {m?.category} · {m?.power || '—'}
                            </span>
                          </small>
                        </button>
                      );
                    })}
                </div>
                <Pick
                  label="전체 기술 목록"
                  value={selected[i]}
                  options={moves.map((m) => m.name)}
                  onChange={(v) => selectMove(i, v)}
                />
                <details>
                  <summary>기술 수치 직접 수정</summary>
                  <div className="inline-fields">
                    <Num
                      label="위력"
                      value={chosen[i].power}
                      onChange={(v) =>
                        setCustom((c) => ({
                          ...c,
                          [i]: { ...chosen[i], power: v },
                        }))
                      }
                    />
                    <Num
                      label="명중"
                      value={chosen[i].accuracy}
                      max={100}
                      onChange={(v) =>
                        setCustom((c) => ({
                          ...c,
                          [i]: { ...chosen[i], accuracy: v },
                        }))
                      }
                    />
                    <Num
                      label="우선도"
                      value={chosen[i].priority}
                      min={-7}
                      max={7}
                      onChange={(v) =>
                        setCustom((c) => ({
                          ...c,
                          [i]: { ...chosen[i], priority: v },
                        }))
                      }
                    />
                  </div>
                </details>
                <p className="move-effect">
                  {chosen[i].effect || '추가 효과 없음'}{' '}
                  <span>부가 효과는 수동 판정</span>
                </p>
              </div>
            </section>
          );
        })}
        <div className="versus">VS</div>
      </div>
      <section className="calculation-panel">
        <div className="calculation-title">
          <Settings2 size={18} />
          <h2>대미지 계산</h2>
          <span>마개조 Ver 3.617 기반</span>
          <label className="check" htmlFor="critical">
            <Checkbox
              id="critical"
              checked={rules.critical}
              onCheckedChange={(v) => patchRule('critical', !!v)}
            />
            급소 ×2
          </label>
        </div>
        <div className="results">
          {calculations.map((c, i) => (
            <div className={`result side-${i}`} key={i}>
              <div>
                <span>
                  TEAM {i + 1} <ArrowRight size={13} /> TEAM {2 - i}
                </span>
                <h3>{chosen[i].name}</h3>
              </div>
              <div className="damage">
                <strong>
                  {c.supported ? `${c.min} – ${c.max}` : '수동 판정'}
                </strong>
                <span>
                  {c.supported
                    ? `${((c.min / stats(fighters[1 - i], fighters[i].level, rules)[0]) * 100).toFixed(1)} – ${((c.max / stats(fighters[1 - i], fighters[i].level, rules)[0]) * 100).toFixed(1)}%`
                    : '변화 / 가변 위력'}
                </span>
              </div>
              <div className="factors">
                자속 ×{c.stab}　상성 ×{c.type}
                <small>난수 85–100% · 부가 효과 제외</small>
              </div>
            </div>
          ))}
        </div>
        <details className="advanced">
          <summary>
            계산 보정 <ChevronRight size={14} />
          </summary>
          <div className="inline-fields">
            <Num
              label="위력 고정치"
              value={rules.powerBonus}
              min={-999}
              onChange={(v) => patchRule('powerBonus', v)}
            />
            <Num
              label="위력 배율"
              value={rules.powerMultiplier}
              step={0.1}
              onChange={(v) => patchRule('powerMultiplier', v)}
            />
            <Num
              label="대미지 배율"
              value={rules.damageMultiplier}
              step={0.1}
              onChange={(v) => patchRule('damageMultiplier', v)}
            />
            <Num
              label="자속 (0=자동)"
              value={rules.stab}
              step={0.5}
              onChange={(v) => patchRule('stab', v)}
            />
            <Num
              label="레벨 차 보정 %"
              value={rules.levelCorrection}
              step={0.1}
              max={10}
              onChange={(v) => patchRule('levelCorrection', v)}
            />
            <label className="check" htmlFor="hp-correction">
              <Checkbox
                id="hp-correction"
                checked={rules.hpCorrection}
                onCheckedChange={(v) => patchRule('hpCorrection', !!v)}
              />
              HP에도 레벨 차 보정
            </label>
          </div>
        </details>
      </section>
      <section className="system-section">
        <div className="system-window">
          <div className="section-label">
            <span>
              <Terminal size={16} /> BATTLE LOG
            </span>
            <small>포텐셜 미적용</small>
          </div>
          <div className="log-lines" role="log" aria-live="polite">
            {logs.map((l, i) => (
              <p
                className={l.startsWith('TURN') ? 'log-turn' : ''}
                key={`${turn}-${i}`}
              >
                <span>›</span> {l}
              </p>
            ))}
          </div>
        </div>
        <div className="action-panel">
          <p>
            {fighters.some((p) => p.hp <= 0)
              ? '포켓몬을 교대하거나 배틀을 초기화하세요.'
              : '양쪽의 행동을 실행합니다.'}
          </p>
          <button
            className="execute"
            disabled={fighters.some((p) => p.hp <= 0)}
            onClick={execute}
          >
            <Swords size={20} /> 턴 실행 <ChevronRight size={19} />
          </button>
          <button className="undo" disabled={!history.length} onClick={undo}>
            <Undo2 size={15} /> 되돌리기
          </button>
        </div>
      </section>
      <footer>
        <span>AA BATTLE LAB / PROTOTYPE</span>
        <div>
          <button onClick={() => setZoom((z) => Math.max(8, z - 1))}>
            AA −
          </button>
          <span>{zoom}px</span>
          <button onClick={() => setZoom((z) => Math.min(24, z + 1))}>
            AA +
          </button>
          <a
            href="https://bbs2.tunaground.net/trace/anchor/13988/1/11"
            target="_blank"
            rel="noreferrer"
          >
            AA 참조 ↗
          </a>
        </div>
      </footer>
      <Dialog
        open={editor !== null}
        onOpenChange={(open) => {
          if (!open) setEditor(null);
        }}
      >
        <DialogContent className="editor-dialog">
          <DialogTitle>포켓몬 · AA 편집</DialogTitle>
          <DialogDescription>
            참조 AA는 글꼴 표시용 샘플입니다. 이 포켓몬의 AA로 교체해 주세요.
          </DialogDescription>
          {draft && (
            <>
              <label className="field">
                <span>포켓몬 검색</span>
                <input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="시트에서 이름 검색"
                />
              </label>
              <Pick
                label="시트 데이터 불러오기"
                value={draft.name}
                options={[
                  draft.name,
                  ...pokemon
                    .filter((p) => p.name.includes(search))
                    .map((p) => p.name),
                ]}
                onChange={(name) => {
                  const p = pokemon.find((p) => p.name === name);
                  if (p) setDraft(fighter(p, draft.aa));
                }}
              />
              <div className="inline-fields">
                <label className="field">
                  <span>이름</span>
                  <input
                    value={draft.name}
                    onChange={(e) =>
                      setDraft({ ...draft, name: e.target.value })
                    }
                  />
                </label>
                <Num
                  label="레벨"
                  value={draft.level}
                  min={1}
                  max={999}
                  onChange={(v) => setDraft({ ...draft, level: v })}
                />
                <Num
                  label="현재 HP"
                  value={draft.hp}
                  onChange={(v) => setDraft({ ...draft, hp: v })}
                />
                <Pick
                  label="상태"
                  value={draft.status}
                  options={['정상', '화상', '동상']}
                  onChange={(v) => setDraft({ ...draft, status: v })}
                />
              </div>
              <div className="inline-fields">
                {[0, 1, 2].map((i) => (
                  <Pick
                    key={i}
                    label={`타입 ${i + 1}`}
                    value={draft.types[i] || '없음'}
                    options={['없음', ...types]}
                    onChange={(v) => {
                      const t = [...draft.types];
                      t[i] = v === '없음' ? '' : v;
                      setDraft({ ...draft, types: t });
                    }}
                  />
                ))}
              </div>
              <div className="editor-stats">
                {statNames.map((n, i) => (
                  <div key={n}>
                    <Num
                      label={`${n} 종족치`}
                      value={draft.base[i]}
                      min={1}
                      max={999}
                      onChange={(v) =>
                        setDraft({
                          ...draft,
                          base: draft.base.map((b, j) => (j === i ? v : b)),
                        })
                      }
                    />
                    <Num
                      label="개체치"
                      value={draft.iv[i]}
                      max={31}
                      onChange={(v) =>
                        setDraft({
                          ...draft,
                          iv: draft.iv.map((b, j) => (j === i ? v : b)),
                        })
                      }
                    />
                    {i > 0 && (
                      <Num
                        label="배율"
                        value={draft.multipliers[i]}
                        step={0.1}
                        max={100}
                        onChange={(v) =>
                          setDraft({
                            ...draft,
                            multipliers: draft.multipliers.map((b, j) =>
                              j === i ? v : b,
                            ),
                          })
                        }
                      />
                    )}
                  </div>
                ))}
              </div>
              <label className="field">
                <span>아스키아트 원문 · 공백과 줄바꿈 유지</span>
                <textarea
                  className="aa aa-input"
                  wrap="off"
                  value={draft.aa}
                  onChange={(e) => setDraft({ ...draft, aa: e.target.value })}
                />
              </label>
              <button
                className="execute"
                disabled={!draft.name.trim() || !draft.types.some(Boolean)}
                onClick={() => {
                  if (editor === null) return;
                  const d = {
                    ...draft,
                    types: Array.from(new Set(draft.types.filter(Boolean))),
                  };
                  d.hp = Math.min(
                    d.hp,
                    stats(d, fighters[1 - editor].level, rules)[0],
                  );
                  patchFighter(editor, d);
                  selectMove(editor, d.moves[0] || '몸통박치기');
                  setEditor(null);
                }}
              >
                적용
              </button>
            </>
          )}
        </DialogContent>
      </Dialog>
      <Dialog open={help} onOpenChange={setHelp}>
        <DialogContent className="help-dialog">
          <DialogTitle>테스트 빌드 안내</DialogTitle>
          <DialogDescription>
            시트의 기본 계산을 옮긴 로컬 2인용 수동 판정 보조 시뮬레이터입니다.
          </DialogDescription>
          <p>
            ① 양쪽 포켓몬을 편집하고 AA를 붙여 넣습니다. 팀당 최대 6마리를
            추가할 수 있습니다.
          </p>
          <p>
            ② 기술, 랭크, 날씨와 배율을 설정합니다. 턴 실행 시 우선도·속도
            순으로 명중과 난수를 굴려 HP에 반영합니다.
          </p>
          <p>
            ③ 변화기·가변 위력·연속기·반동·상태 부여·특성·소지품 효과는 자동
            처리되지 않습니다. 편집의 HP·능력치와 계산 보정으로 수동 반영하세요.
            PP와 지속 턴도 미구현입니다.
          </p>
          <p>
            포텐셜은 데이터와 이벤트 확장 구조만 준비되어 있으며 발동하지
            않습니다. 설정은 새로고침하면 초기화됩니다.
          </p>
          <p>
            기본 AA는 링크의 캐릭터 표시 샘플이며, 시트 포켓몬과의 대응을
            의미하지 않습니다.
          </p>
        </DialogContent>
      </Dialog>
    </main>
  );
}
