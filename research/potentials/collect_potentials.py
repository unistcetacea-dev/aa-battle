"""Collect potential definitions into research data. This does not touch the game build."""
from __future__ import annotations

import concurrent.futures
import datetime as dt
import hashlib
import html
import json
import re
import time
import urllib.parse
import urllib.request
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CACHE = ROOT / "raw-cache"
BASE = "https://wiki.tunaground.net/doku.php"
CHARACTER_INDEX = "포켓몬스터_아바돈_썩어가는_낙원과_부해의_탐험대_캐릭터_시트"
REFERENCE_INDEX = "오레마스_포텐셜일람"
LINK_RE = re.compile(r"\[\[\s*([^\]|#]+)(?:#[^\]|]*)?(?:\|([^\]]+))?\]\]")
HEADING_RE = re.compile(r"^\s*(={2,6})\s*(.*?)\s*\1\s*$")
INLINE_RE = re.compile(r"^[\s　]*『([^』]+)』[\s　]*(?:…+|\\\\)[\s　]*(.*)$")
TITLE_RE = re.compile(r"^[\s　]*『([^』]+)』[\s　]*\\\\\s*$")


def url_for(page: str) -> str:
    return BASE + "?" + urllib.parse.urlencode({"id": page, "do": "export_raw"})


def fetch(page: str) -> str:
    CACHE.mkdir(parents=True, exist_ok=True)
    cache = CACHE / (hashlib.sha1(page.encode("utf-8")).hexdigest() + ".txt")
    if cache.exists():
        return cache.read_text(encoding="utf-8")
    request = urllib.request.Request(url_for(page), headers={"User-Agent": "AA-Battle-research/0.1"})
    error = None
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                text = response.read().decode("utf-8", "replace")
            cache.write_text(text, encoding="utf-8")
            return text
        except Exception as exc:
            error = exc
            time.sleep(1.5 * (attempt + 1))
    raise RuntimeError(f"Could not fetch {page}: {error}")


def clean(value: str) -> str:
    value = html.unescape(value)
    value = re.sub(r"<[^>]+>", " ", value)
    value = value.replace("\\\\", " ").replace("　", " ").replace(" ", " ").replace(" ", " ")
    value = re.sub(r"\[\[([^\]|]+)\|([^\]]+)\]\]", r"\2", value)
    value = re.sub(r"\[\[([^\]]+)\]\]", r"\1", value)
    value = re.sub(r"\*\*|//|__", "", value)
    return re.sub(r"\s+", " ", value).strip(" |")


def split_meta(text: str) -> tuple[dict, str]:
    normalized = text.replace("１", "1").replace("２", "2").replace("３", "3")
    meta = {"mode": None, "uses": None, "probability": None}
    if "선행" in normalized:
        meta["mode"] = "manual_declare"
    elif "임의" in normalized:
        meta["mode"] = "optional"
    elif "자동" in normalized:
        meta["mode"] = "automatic"
    uses = re.search(r"(\d+)\s*/\s*(시합|시|선발|턴)", normalized)
    if uses:
        meta["uses"] = {"count": int(uses.group(1)), "per": uses.group(2)}
    for word, code in (("드물게", "rare"), ("저확률", "low"), ("중확률", "medium"), ("고확률", "high"), ("반드시", "certain")):
        if word in normalized:
            meta["probability"] = code
            break
    stripped = re.sub(r"(?:\d+\s*/\s*(?:시합|시|선발|턴)\s*/?)?(?:선행|자동|임의)\s*", "", normalized).strip()
    return meta, stripped


EVENT_PATTERNS = [
    ("battle_start", r"배틀 시작|시합 개시"), ("field_enter", r"(?:필드|장소)에 (?:나오|나왔|등장)"),
    ("field_exit", r"(?:필드|장소)를 떠|교대할 때"), ("turn_start", r"턴 개시|T개시"),
    ("turn_end", r"턴 종료|T종료"), ("before_action", r"행동하기 전|행동 전에"),
    ("move_use", r"기술을 (?:내보|사용|썼)|공격할 때"), ("move_hit", r"명중했을 때|기술이 명중"),
    ("attack_received", r"공격을 받|기술을 받|공격에 맞"), ("damage_received", r"대미지를 받|데미지를 받"),
    ("knockout", r"쓰러뜨렸을 때|상대를 쓰러"), ("self_faint", r"빈사|쓰러졌을 때"),
    ("opponent_switch", r"상대가 교대|상대의 교대"), ("ally_switch", r"아군과 교대|아군이 교대"),
    ("status_event", r"상태이상|상태변화"), ("hp_threshold", r"체력이|HP가|HP이"),
    ("weather", r"날씨|쾌청|비바라기|모래바람|싸라기눈"), ("terrain", r"필드가|지형"),
    ("party_presence", r"PT에 참여|파티에 참여"), ("command", r"지령|선행입력"),
]
EFFECT_PATTERNS = [
    ("stat_stage", r"(?:능력치|공격|방어|특공|특방|속도).{0,12}(?:오른|내린|상승|저하|랭크)"),
    ("move_power", r"위력.{0,12}(?:강화|증가|배)"), ("damage_modifier", r"대미지.{0,12}(?:완화|감소|배)|데미지.{0,12}(?:완화|감소|배)"),
    ("accuracy_evasion", r"명중률|회피율|필중"), ("priority", r"우선도|먼저 행동"),
    ("type_change", r"타입을 (?:추가|변경|상실)|타입이 된다|타입으로"),
    ("nullify", r"무효화|무시한다|효과가 없다"), ("heal", r"회복"),
    ("direct_damage", r"체력.{0,10}(?:감소|대미지)|HP.{0,10}(?:감소|대미지)"),
    ("switch", r"교대|필드를 떠|장소를 떠"), ("extra_action", r"추가 행동|다시 행동|연속해서"),
    ("move_property", r"추격|유턴|방호|급소|연속기|기술폭"),
    ("weather_terrain", r"날씨|순풍|트릭룸|필드 상태|지형"),
    ("status", r"상태이상|상태변화|혼란|마비|화상|독|잠듦|풀죽음"),
    ("resource", r"소지품|도구|PP|횟수"), ("information", r"데이터를 해석|정보|확인"),
    ("classification", r"(?:포텐셜|퍼텐셜).{0,12}(?:취급|변경)|『.+』로서 취급"),
    ("disable", r"행동불능|발동하지 않|사용할 수 없"),
    ("field_hazard", r"스텔스록|압정|독압정|끈적끈적네트.{0,12}(?:설치|제거)"),
    ("party_modifier", r"아군 전원|아군 포켓몬.{0,15}(?:강화|상승|완화)"),
    ("recoil", r"반동.{0,12}(?:받지 않|무효|감소)"),
    ("base_stat", r"종족치.{0,15}(?:강화|보정|올린|내린|로 한다)"),
    ("reward", r"상금|경험치|소지금"), ("type_effectiveness", r"효과가 (?:굉장|별로)|상성"),
    ("additional_attack", r"추가공격|추가 공격"), ("grant_potential", r"『.+』.{0,8}(?:부여|습득)"),
    ("survival_rule", r"빈사로 할 수 없|체력.{0,8}1로"),
]


def analyze(text: str) -> tuple[dict, list[dict], list[dict], list[str]]:
    meta, body = split_meta(text)
    sentences = [s.strip() for s in re.split(r"(?<=[.!?。])\s+|(?<=다\.)", body) if s.strip()]
    conditions, effects, unresolved = [], [], []
    for sentence in sentences or [body]:
        effect_types = [code for code, pattern in EFFECT_PATTERNS if re.search(pattern, sentence)]
        condition_match = re.match(r"(.+?(?:때|경우|동안|한해서|라면|있으면|없으면|일때))[,，]?\s*(.*)", sentence)
        condition_text, effect_text = (condition_match.group(1), condition_match.group(2)) if condition_match else (None, sentence)
        if condition_text:
            events = [code for code, pattern in EVENT_PATTERNS if re.search(pattern, condition_text)]
            conditions.append({"text": condition_text, "events": events or ["state_check"]})
        if effect_text:
            values = {
                "multipliers": [float(x) for x in re.findall(r"(\d+(?:\.\d+)?)\s*배", effect_text)],
                "fractions": [{"numerator": int(a), "denominator": int(b)} for a, b in re.findall(r"(\d+)\s*/\s*(\d+)", effect_text)],
                "turns": [int(x) for x in re.findall(r"(\d+)\s*T", effect_text, re.I)],
                "ranks": [int(x) for x in re.findall(r"([+-]?\d+)\s*랭크", effect_text)],
                "quoted_terms": re.findall(r"「([^」]+)」", effect_text),
            }
            effects.append({"text": effect_text, "types": effect_types or ["unclassified"], "values": values})
        if not effect_types:
            unresolved.append(sentence)
    confidence = "high" if not unresolved and all(e["types"] != ["unclassified"] for e in effects) else "review"
    meta["confidence"] = confidence
    return meta, conditions, effects, unresolved


def infer_category(name: str, canonical: dict[str, str]) -> str | None:
    if name in canonical:
        return canonical[name]
    rules = [(r"^(에이스|킬러|어시스트|탐사대장|톱배터|선발)", "역할"),
             (r"^(천부의 재능|금관 사이즈|고유종|변종|델타종|적응종)", "분류"),
             (r"^주인의", "주인"), (r"^(선의 선|대의 선|후의 선)", "선제"),
             (r"^대.+(회피|내성|격|박격|추격|저격)", "대○"),
             (r"^(기합|전투속행|초근성)", "범용"), (r"^익스펜션", "특권")]
    for pattern, category in rules:
        if re.search(pattern, name):
            return category
    return None


def parse_occurrences(page: str, text: str, source_kind: str, canonical: dict[str, str]) -> list[dict]:
    lines = text.splitlines()
    section, entity, in_potentials = "", "", False
    records = []
    for index, raw in enumerate(lines):
        heading = HEADING_RE.match(raw)
        if heading:
            section = clean(heading.group(2))
            if "포텐셜" not in section:
                in_potentials = False
        named = re.search(r"【이름】\s*([^\\|]+)", raw)
        if named:
            entity = clean(named.group(1))
        if re.search(r"【(?:고유 |전용 )?포텐셜】", raw):
            in_potentials = True
            continue
        inline = INLINE_RE.match(raw)
        if inline and (in_potentials or source_kind == "reference"):
            name, description = clean(inline.group(1)), clean(inline.group(2))
            if not description or len(description) < 2:
                continue
            meta, triggers, effects, unresolved = analyze(description)
            records.append({"name": name, "category": infer_category(name, canonical), "source_kind": source_kind,
                            "source_page": page, "source_url": url_for(page), "line": index + 1,
                            "section": section, "character": entity, "raw": description,
                            "activation": meta, "triggers": triggers, "effects": effects,
                            "needs_review": unresolved})
        elif in_potentials and TITLE_RE.match(raw):
            name = clean(TITLE_RE.match(raw).group(1))
            following = []
            for next_raw in lines[index + 1:index + 6]:
                next_line = clean(next_raw)
                if not next_line or next_line == "----":
                    continue
                if INLINE_RE.match(next_raw) or HEADING_RE.match(next_raw) or "【" in next_line:
                    break
                following.append(next_line)
            description = " ".join(following)
            if description:
                meta, triggers, effects, unresolved = analyze(description)
                records.append({"name": name, "category": infer_category(name, canonical), "source_kind": source_kind,
                                "source_page": page, "source_url": url_for(page), "line": index + 1,
                                "section": section, "character": entity, "raw": description,
                                "activation": meta, "triggers": triggers, "effects": effects,
                                "needs_review": unresolved})
        if in_potentials and (raw.startswith("====") or re.search(r"【(?:기술|종족치|트레이너)", raw)):
            in_potentials = False
    return records


def main() -> None:
    char_index = fetch(CHARACTER_INDEX)
    ref_index = fetch(REFERENCE_INDEX)
    character_pages = sorted({clean(m.group(1)) for m in LINK_RE.finditer(char_index) if clean(m.group(1)).startswith("아바돈_")})
    reference_pages = sorted({clean(m.group(1)) for m in LINK_RE.finditer(ref_index) if clean(m.group(1)).startswith("오레마스_") and clean(m.group(1)) != REFERENCE_INDEX})
    pages = character_pages + reference_pages
    fetched, errors = {}, []
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        future_pages = {pool.submit(fetch, page): page for page in pages}
        for future in concurrent.futures.as_completed(future_pages):
            page = future_pages[future]
            try:
                fetched[page] = future.result()
            except Exception as exc:
                errors.append({"page": page, "error": str(exc)})

    canonical_records = []
    canonical = {}
    for page in reference_pages:
        category = page.replace("오레마스_", "").replace("포텐셜", "")
        parsed = parse_occurrences(page, fetched.get(page, ""), "reference", {})
        for record in parsed:
            record["category"] = category
            canonical.setdefault(record["name"], category)
        canonical_records.extend(parsed)

    occurrences = []
    for page in character_pages:
        occurrences.extend(parse_occurrences(page, fetched.get(page, ""), "character_sheet", canonical))

    unique_map = {}
    for record in occurrences + canonical_records:
        key = (record["name"], re.sub(r"\s+", "", record["raw"]))
        if key not in unique_map:
            item = {k: v for k, v in record.items() if k not in ("source_page", "source_url", "line", "section", "character")}
            item["id"] = hashlib.sha1((key[0] + "\0" + key[1]).encode("utf-8")).hexdigest()[:16]
            item["sources"] = []
            unique_map[key] = item
        unique_map[key]["sources"].append({k: record[k] for k in ("source_kind", "source_page", "source_url", "line", "section", "character")})

    unique = sorted(unique_map.values(), key=lambda x: (x["category"] or "미분류", x["name"], x["raw"]))
    metadata = {"generated_at": dt.datetime.now(dt.timezone.utc).isoformat(),
                "character_index": url_for(CHARACTER_INDEX), "reference_index": url_for(REFERENCE_INDEX),
                "character_pages_discovered": len(character_pages), "reference_pages_discovered": len(reference_pages),
                "pages_fetched": len(fetched), "fetch_errors": errors,
                "occurrence_count": len(occurrences), "canonical_occurrence_count": len(canonical_records),
                "unique_definition_count": len(unique), "parser": "heuristic-v1",
                "warning": "trigger/effect fields are heuristic; retain raw and inspect needs_review before game use"}
    (ROOT / "potential-occurrences.json").write_text(json.dumps({"metadata": metadata, "items": occurrences}, ensure_ascii=False, indent=2), encoding="utf-8")
    (ROOT / "potential-catalog.json").write_text(json.dumps({"metadata": metadata, "items": unique}, ensure_ascii=False, indent=2), encoding="utf-8")

    categories = Counter(x["category"] or "미분류" for x in unique)
    events = Counter(event for x in unique for trigger in x["triggers"] for event in trigger["events"])
    effects = Counter(kind for x in unique for effect in x["effects"] for kind in effect["types"])
    characters = len({x["character"] for x in occurrences if x["character"]})
    review = sum(bool(x["needs_review"]) for x in unique)
    report = ["# 포텐셜 수집 보고서", "", f"생성: {metadata['generated_at']}", "",
              f"- 캐릭터 시트 링크: {len(character_pages)}", f"- 수집 성공 페이지: {len(fetched)} / {len(pages)}",
              f"- 식별된 캐릭터: {characters}", f"- 캐릭터 시트 포텐셜 출현: {len(occurrences)}",
              f"- 중복 제거 정의: {len(unique)}", f"- 사람 검토 필요 정의: {review}", "", "## 분류", ""]
    report += [f"- {key}: {value}" for key, value in categories.most_common()]
    report += ["", "## 트리거 태그", ""] + [f"- {key}: {value}" for key, value in events.most_common()]
    report += ["", "## 효과 태그", ""] + [f"- {key}: {value}" for key, value in effects.most_common()]
    report += ["", "## 데이터 사용 원칙", "", "`raw`가 원문 기준이다. `triggers`와 `effects`는 자동 분리 초안이다.",
               "`activation.confidence`가 `review`이거나 `needs_review`가 비어 있지 않으면 게임 규칙으로 변환하기 전에 사람이 확인해야 한다.",
               "같은 이름이어도 효과 문장이 다르면 별도 정의로 보존한다. `sources`에 캐릭터·버전 문맥을 남긴다."]
    if errors:
        report += ["", "## 수집 실패", ""] + [f"- {x['page']}: {x['error']}" for x in errors]
    (ROOT / "REPORT.md").write_text("\n".join(report) + "\n", encoding="utf-8")
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
