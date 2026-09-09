"""Audit collected definitions against cached sources and live index pages."""
import hashlib
import json
import re
import urllib.request
from pathlib import Path
import collect_potentials as collect

root = Path(__file__).resolve().parent
catalog = json.loads((root / 'potential-catalog.json').read_text(encoding='utf-8'))
items = catalog['items']
sources = {s['source_page'] for x in items for s in x['sources']}
cached = {}
for page in sources:
    path = root / 'raw-cache' / (hashlib.sha1(page.encode()).hexdigest() + '.txt')
    if path.exists():
        cached[page] = path.read_text(encoding='utf-8')
missing = sorted(sources - cached.keys())
name_matches = sum(any(x['name'] in cached.get(s['source_page'], '') for s in x['sources']) for x in items)
normalized = {page: re.sub(r'\s+', '', collect.clean(text)) for page, text in cached.items()}
raw_matches = sum(any(re.sub(r'\s+', '', collect.clean(x['raw'])) in normalized.get(s['source_page'], '')
                      for s in x['sources']) for x in items)
live = []
for page in [collect.CHARACTER_INDEX, collect.REFERENCE_INDEX, '오레마스_역할포텐셜', '아바돈_유키나리']:
    request = urllib.request.Request(collect.url_for(page), headers={'User-Agent': 'AA-Battle-catalog-audit/1.0'})
    try:
        with urllib.request.urlopen(request, timeout=25) as response:
            content = response.read().decode('utf-8')
        links = {collect.clean(m.group(1)) for m in collect.LINK_RE.finditer(content)}
        old_path = root / 'raw-cache' / (hashlib.sha1(page.encode()).hexdigest() + '.txt')
        old = old_path.read_text(encoding='utf-8') if old_path.exists() else ''
        old_links = {collect.clean(m.group(1)) for m in collect.LINK_RE.finditer(old)}
        live.append({'page': page, 'url': collect.url_for(page), 'same_as_cache': content == old,
                     'new_links': sorted(links-old_links), 'removed_links': sorted(old_links-links)})
    except Exception as error:
        live.append({'page': page, 'error': str(error)})
report = {'definitions': len(items), 'source_pages': len(sources), 'cached_source_pages': len(cached),
          'missing_cache_pages': missing, 'definitions_with_name_in_source': name_matches,
          'definitions_with_raw_in_source': raw_matches,
          'definitions_without_trigger': sum(not x['triggers'] for x in items),
          'definitions_needing_review': sum(bool(x['needs_review']) for x in items), 'live_checks': live}
(root / 'catalog-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
