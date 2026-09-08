import data from './reference-data.json';
export const statNames = ['체력', '공격', '방어', '특공', '특방', '속도'];
export type Potential = {
  id: string;
  name: string;
  description: string;
  triggers: (
    | 'onEnter'
    | 'beforeMove'
    | 'beforeDamage'
    | 'afterDamage'
    | 'turnEnd'
  )[];
};
export type Pokemon = {
  id: string;
  team: string;
  name: string;
  level: number;
  types: string[];
  ability: string;
  item: string;
  base: number[];
  iv: number[];
  moves: string[];
  potentials: Potential[];
};
export type Fighter = Pokemon & {
  aa: string;
  hp: number;
  stages: number[];
  multipliers: number[];
  status: string;
};
export type Move = {
  name: string;
  category: string;
  types: string[];
  power: number;
  accuracy: number;
  priority: number;
  effect: string;
  attack: string | null;
  defense: string | null;
  tags: string[];
};
export const pokemon = data.pokemon as Pokemon[],
  moves = data.moves as Move[],
  types = data.types;
export type Rules = {
  weather: string;
  terrain: string;
  critical: boolean;
  powerBonus: number;
  powerMultiplier: number;
  damageMultiplier: number;
  stab: number;
  levelCorrection: number;
  hpCorrection: boolean;
};
export const defaultRules: Rules = {
  weather: '없음',
  terrain: '없음',
  critical: false,
  powerBonus: 0,
  powerMultiplier: 1,
  damageMultiplier: 1,
  stab: 0,
  levelCorrection: 1,
  hpCorrection: false,
};
export function stats(p: Pokemon, otherLevel: number, r: Rules = defaultRules) {
  const low = Math.min(p.level, otherLevel),
    c = 1 + (Math.max(0, p.level - otherLevel) * r.levelCorrection) / 100;
  return p.base.map((b, i) =>
    i === 0
      ? r.hpCorrection
        ? Math.floor(
            (Math.floor(((b * 2 + p.iv[i]) * low) / 100) + 10 + low) * c,
          )
        : Math.floor(((b * 2 + p.iv[i]) * p.level) / 100 + 10 + p.level)
      : Math.floor((Math.floor(((b * 2 + p.iv[i]) * low) / 100) + 5) * c),
  );
}
export function fighter(p: Pokemon, aa = ''): Fighter {
  return {
    ...structuredClone(p),
    aa,
    hp: stats(p, p.level)[0],
    stages: [0, 0, 0, 0, 0, 0],
    multipliers: [1, 1, 1, 1, 1, 1],
    status: '정상',
  };
}
export function stage(n: number) {
  return n >= 0 ? (2 + n) / 2 : 2 / (2 - n);
}
export function effective(
  p: Fighter,
  other: Fighter,
  i: number,
  r: Rules,
  role = 'normal',
) {
  let s = p.stages[i],
    m = p.multipliers[i];
  if (r.critical && role === 'attack') {
    s = Math.max(0, s);
    m = Math.max(1, m);
  }
  if (r.critical && role === 'defense') {
    s = Math.min(0, s);
    m = Math.min(1, m);
  }
  return Math.max(1, Math.floor(stats(p, other.level, r)[i] * m * stage(s)));
}
export function effectiveness(at: string[], dt: string[]) {
  const chart = data.chart as Record<string, Record<string, number>>;
  return at.length
    ? Math.max(
        ...at.map((a) => dt.reduce((v, d) => v * (chart[d]?.[a] ?? 1), 1)),
      )
    : 1;
}
export function resolveTurn(
  input: Fighter[],
  chosen: Move[],
  rules: Rules,
  rng: () => number = Math.random,
) {
  const fs = structuredClone(input),
    entries: string[] = [],
    tie = rng() < 0.5 ? -1 : 1;
  const order = [0, 1].sort(
    (a, b) =>
      chosen[b].priority - chosen[a].priority ||
      effective(fs[b], fs[a], 5, rules) - effective(fs[a], fs[b], 5, rules) ||
      tie,
  );
  for (const i of order) {
    const a = fs[i],
      b = fs[1 - i],
      m = chosen[i];
    if (a.hp <= 0) continue;
    const c = calculate(a, b, m, rules);
    if (!c.supported) {
      entries.push(
        `${a.name}의 ${m.name} — 변화·가변 위력 효과는 수동 판정하세요.`,
      );
      continue;
    }
    if (rng() * 100 >= m.accuracy) {
      entries.push(`${a.name}의 ${m.name}! 그러나 빗나갔다!`);
      continue;
    }
    const damage = c.rolls[Math.min(15, Math.floor(rng() * 16))];
    b.hp = Math.max(0, b.hp - damage);
    entries.push(
      `${a.name}의 ${m.name}! ${b.name}에게 ${damage} 대미지.${rules.critical ? ' 급소에 맞았다!' : ''}${c.type === 0 ? ' 효과가 없다!' : c.type > 1 ? ' 효과가 굉장했다!' : c.type < 1 ? ' 효과가 별로다.' : ''}`,
    );
    if (!b.hp)
      entries.push(`${b.name}은 쓰러졌다! 대기 포켓몬을 선택해 교대하세요.`);
  }
  return { fighters: fs, entries };
}
export function calculate(a: Fighter, b: Fighter, m: Move, r: Rules) {
  const ai = Math.max(
      1,
      statNames.indexOf(m.attack ?? (m.category === '물리' ? '공격' : '특공')),
    ),
    di = Math.max(
      1,
      statNames.indexOf(m.defense ?? (m.category === '물리' ? '방어' : '특방')),
    );
  const attack = effective(a, b, ai, r, 'attack'),
    defense = effective(b, a, di, r, 'defense');
  let power = Math.max(
    0,
    Math.floor(m.power + r.powerBonus) * r.powerMultiplier,
  );
  if (
    (a.status === '화상' && m.category === '물리') ||
    (a.status === '동상' && m.category === '특수')
  )
    power *= 0.5;
  if (r.weather === '쾌청')
    power *= m.types.includes('불꽃') ? 1.5 : m.types.includes('물') ? 0.5 : 1;
  if (r.weather === '비')
    power *= m.types.includes('물') ? 1.5 : m.types.includes('불꽃') ? 0.5 : 1;
  const terrain: Record<string, string> = {
    그래스필드: '풀',
    일렉트릭필드: '전기',
    사이코필드: '에스퍼',
    미스트필드: '페어리',
  };
  if (
    m.types.includes(terrain[r.terrain]) &&
    !a.types.includes('비행') &&
    a.ability !== '부유'
  )
    power *= 1.5;
  const stab = r.stab || (m.types.some((t) => a.types.includes(t)) ? 1.5 : 1),
    type = effectiveness(m.types, b.types),
    base = Math.floor(
      (((((2 * a.level + 10) / 250) * attack) / defense) * power + 2) *
        stab *
        type *
        (r.critical ? 2 : 1) *
        r.damageMultiplier,
    );
  const supported = m.category !== '변화' && m.power > 0,
    rolls = Array.from({ length: 16 }, (_, i) =>
      supported ? Math.floor((base * (85 + i)) / 100) : 0,
    );
  return {
    min: rolls[0],
    max: rolls[15],
    rolls,
    stab,
    type,
    attack,
    defense,
    power,
    supported,
  };
}
