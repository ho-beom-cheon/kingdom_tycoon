from pathlib import Path
import csv, json, subprocess, sys

ROOT = Path(__file__).resolve().parents[1]
CSV = ROOT / 'data' / 'csv'
errors = []

schema_check = subprocess.run(
    [sys.executable, str(ROOT / 'scripts' / 'generate_save_schema.py'), '--check'],
    cwd=ROOT,
    capture_output=True,
    text=True,
    encoding='utf-8',
)
if schema_check.returncode != 0:
    errors.append('generated save schema is stale; run scripts/generate_save_schema.py')
elif schema_check.stdout.strip():
    print(schema_check.stdout.strip())

def rows(name):
    with (CSV/name).open(encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))

def unique(name, key):
    seen=set()
    for i,r in enumerate(rows(name),2):
        v=r[key]
        if not v: errors.append(f'{name}:{i} empty {key}')
        if v in seen: errors.append(f'{name}:{i} duplicate {key}={v}')
        seen.add(v)
    return seen

jobs=unique('jobs.csv','job_id')
grades=unique('grades.csv','grade_id')
ranks=unique('ranks.csv','rank_id')
regions=unique('regions.csv','region_id')
monsters=unique('monsters.csv','monster_id')
materials=unique('materials.csv','item_id')
equipment=unique('equipment_templates.csv','equipment_id')
facilities=set(r['facility_id'] for r in rows('facilities.csv'))
npc_prof=set(r['profession_id'] for r in rows('npc_professions.csv'))
npc_levels=unique('npc_proficiency.csv','proficiency_id')
potions=unique('potions.csv','potion_id')
skills=unique('skills.csv','skill_id')
traits=unique('traits.csv','trait_id')
personalities=unique('personalities.csv','personality_id')
raids=unique('raids.csv','raid_id')

for i,r in enumerate(rows('jobs.csv'),2):
    for sid in [x for x in r['initial_skill_ids'].split('|') if x]:
        if sid not in skills: errors.append(f'jobs.csv:{i} unknown skill {sid}')
for i,r in enumerate(rows('skills.csv'),2):
    if r['job_id'] not in jobs: errors.append(f'skills.csv:{i} unknown job {r["job_id"]}')
    if r['unlock_rank'] not in ranks: errors.append(f'skills.csv:{i} unknown rank {r["unlock_rank"]}')
for i,r in enumerate(rows('regions.csv'),2):
    if r['min_rank_id'] not in ranks: errors.append(f'regions.csv:{i} unknown rank {r["min_rank_id"]}')
for i,r in enumerate(rows('monsters.csv'),2):
    if r['region_id'] not in regions: errors.append(f'monsters.csv:{i} unknown region {r["region_id"]}')
for i,r in enumerate(rows('ranks.csv'),2):
    if r['promotion_token'] and r['promotion_token'] not in materials: errors.append(f'ranks.csv:{i} unknown promotion token {r["promotion_token"]}')
for i,r in enumerate(rows('promotion_grade_requirements.csv'),2):
    if r['grade_id'] not in grades: errors.append(f'promotion:{i} unknown grade')
    if r['from_rank'] not in ranks or r['to_rank'] not in ranks: errors.append(f'promotion:{i} unknown rank')
    if r['item_id'] not in materials: errors.append(f'promotion:{i} unknown item {r["item_id"]}')
for i,r in enumerate(rows('facilities.csv'),2):
    try: mats=json.loads(r['materials_json'])
    except Exception as e:
        errors.append(f'facilities.csv:{i} invalid materials_json {e}'); continue
    for item in mats:
        if item not in materials: errors.append(f'facilities.csv:{i} unknown material {item}')
for i,r in enumerate(rows('recipes.csv'),2):
    if r['facility_id'] not in facilities: errors.append(f'recipes.csv:{i} unknown facility {r["facility_id"]}')
    if r['npc_proficiency'] not in npc_levels: errors.append(f'recipes.csv:{i} unknown npc proficiency {r["npc_proficiency"]}')
    if r['output_type']=='EQUIPMENT' and r['output_id'] not in equipment: errors.append(f'recipes.csv:{i} unknown equipment output')
    if r['output_type']=='POTION' and r['output_id'] not in potions: errors.append(f'recipes.csv:{i} unknown potion output')
    try: ing=json.loads(r['ingredients_json'])
    except Exception as e:
        errors.append(f'recipes.csv:{i} invalid ingredients_json {e}'); continue
    for item in ing:
        if item not in materials: errors.append(f'recipes.csv:{i} unknown ingredient {item}')
for i,r in enumerate(rows('loot_entries.csv'),2):
    if r['reward_type']=='MATERIAL' and r['reward_id'] not in materials: errors.append(f'loot:{i} unknown material {r["reward_id"]}')
for i,r in enumerate(rows('enhancement_rules.csv'),2):
    if r['stone_item_id'] not in materials: errors.append(f'enhancement:{i} unknown stone {r["stone_item_id"]}')
for i,r in enumerate(rows('refine_options.csv'),2):
    if r['material_item_id'] not in materials: errors.append(f'refine:{i} unknown material {r["material_item_id"]}')
for i,r in enumerate(rows('raids.csv'),2):
    if r['boss_monster_id'] not in monsters: errors.append(f'raids.csv:{i} unknown boss {r["boss_monster_id"]}')
    if r['min_rank_id'] not in ranks: errors.append(f'raids.csv:{i} unknown rank {r["min_rank_id"]}')
for i,r in enumerate(rows('raid_parts.csv'),2):
    if r['raid_id'] not in raids: errors.append(f'raid_parts.csv:{i} unknown raid {r["raid_id"]}')
for i,r in enumerate(rows('recruitment_pools.csv'),2):
    if r['grade_id'] not in grades: errors.append(f'recruitment:{i} unknown grade')
for i,r in enumerate(rows('equipment_templates.csv'),2):
    for job in [x for x in r['allowed_jobs'].split('|') if x]:
        if job not in jobs: errors.append(f'equipment:{i} unknown job {job}')

# Sum pool weights per pool
from collections import defaultdict
weights=defaultdict(float)
for r in rows('recruitment_pools.csv'): weights[r['pool_id']]+=float(r['weight'])
for pool,total in weights.items():
    if abs(total-100)>0.001: errors.append(f'recruitment pool {pool} weight sum {total}, expected 100')

# Parse JSON schemas and UI tokens
for p in list((ROOT/'data'/'schemas').glob('*.json'))+[ROOT/'data'/'ui'/'ui_tokens.json']:
    try: json.loads(p.read_text(encoding='utf-8'))
    except Exception as e: errors.append(f'{p.relative_to(ROOT)} invalid json: {e}')

if errors:
    print('CONTENT VALIDATION FAILED')
    for e in errors: print('-',e)
    sys.exit(1)
print('CONTENT VALIDATION PASSED')
print(f'jobs={len(jobs)}, grades={len(grades)}, ranks={len(ranks)}, regions={len(regions)}, monsters={len(monsters)}, materials={len(materials)}, equipment={len(equipment)}')
