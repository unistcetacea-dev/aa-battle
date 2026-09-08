const fs = require('node:fs'),
  vm = require('node:vm'),
  assert = require('node:assert/strict'),
  ts = require('typescript');
const src = ts.transpileModule(fs.readFileSync('lib/battle.ts', 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, esModuleInterop: true },
}).outputText;
const box = {
  exports: {},
  require: (p) =>
    require(p === './reference-data.json' ? './lib/reference-data.json' : p),
  structuredClone,
};
vm.runInNewContext(src, box);
const b = box.exports;
const a = b.fighter(b.pokemon.find((p) => p.name === '선데이')),
  d = b.fighter(b.pokemon.find((p) => p.name === '잭 한마')),
  m = b.moves.find((m) => m.name === '파괴광선'),
  r = b.defaultRules;
assert.equal(b.stats(a, d.level)[0], 598);
assert.equal(b.stats(a, d.level)[1], 269);
assert.equal(b.stats(d, a.level)[0], 610);
d.stages[4] = 1; // workbook K32: defender special defense +1
const c = b.calculate(a, d, m, r);
assert.equal(c.min, 290);
assert.equal(c.max, 342);
assert.equal(c.rolls[2], 297); // workbook N14/N15/E1 at N13=.87
assert.equal(b.effectiveness(['노말'], ['고스트']), 0);
assert.equal(b.effectiveness(['땅'], ['전기', '강철']), 4);
assert.equal(b.effectiveness(['물'], ['불꽃', '땅', '바위']), 8);
const fire = { ...m, types: ['불꽃'] };
assert.ok(
  b.calculate(a, d, fire, { ...r, weather: '쾌청' }).max >
    b.calculate(a, d, fire, r).max,
);
const phys = { ...m, category: '물리', attack: '공격', defense: '방어' };
assert.ok(
  b.calculate({ ...a, status: '화상' }, d, phys, r).max <
    b.calculate(a, d, phys, r).max,
);
const crit = { ...r, critical: true },
  debuff = { ...a, stages: [0, 0, 0, -6, 0, 0] };
assert.equal(
  b.calculate(debuff, d, m, crit).max,
  b.calculate(a, d, m, crit).max,
);
assert.equal(
  b.calculate(a, d, { ...m, category: '변화', power: 0 }, r).supported,
  false,
);
assert.equal(
  b.calculate(a, d, { ...m, types: ['노말'] }, { ...r, damageMultiplier: 0 })
    .max,
  0,
);
const ko = b.resolveTurn(
  [a, { ...d, hp: 1 }],
  [{ ...m, priority: 7 }, m],
  r,
  () => 0,
);
assert.equal(ko.fighters[1].hp, 0);
assert.equal(ko.fighters[0].hp, a.hp);
assert.equal(ko.entries.length, 2);
const miss = b.resolveTurn(
  [a, d],
  [
    { ...m, accuracy: 0 },
    { ...m, accuracy: 0 },
  ],
  r,
  () => 0.5,
);
assert.equal(miss.fighters[0].hp, a.hp);
assert.equal(miss.fighters[1].hp, d.hp);
assert.equal(a.hp, 598);
console.log(
  'PASS: workbook HP/stats and damage 290–342, .87 roll=297; immunity, triple types, weather, burn, critical ranks, status move, zero multiplier, priority KO, misses and immutable turn inputs.',
);
