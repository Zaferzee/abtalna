"""Builds src/CyberLms.Web/Data/StarterPack/starter-content-pack.v1.json and docs/starter-content-pack/ANSWER_KEY.md
from the authoring sources (part1..part4). Run from this folder: python3 build.py ../.."""
import json, random, re, sys, os
import part1, part2, part3, part4
root = sys.argv[1]
items = part1.ITEMS + part2.ITEMS + part3.ITEMS + part4.ITEMS
words = lambda h: len(re.sub('<[^>]+>', ' ', h).split())
out, key = [], ['# Starter content pack v1: answer key\n',
               'Generated from the pack source. Correct answers and the explanation for each question (the question model has no explanation field, so explanations are kept here for trainers and reviewers).\n']
for it in items:
    code = it['code']; pol = it.get('policy', False); spc = it.get('specialized', False)
    body = it['body']
    assert '<script' not in body and 'http' not in body, code
    if not pol:
        assert 450 <= words(body) <= 750, (code, words(body))
        assert 5 <= body.count('<h2>') <= 7 and '<h2>تذكر</h2>' in body, code
        qs = it['questions']; kinds = [q['kind'] for q in qs]
        assert len(qs) == 8 and kinds.count('scenario') == 3 and kinds.count('knowledge') == 3 and kinds.count('truefalse') + kinds.count('bestaction') == 2, code
    else:
        assert 'نموذج استرشادي تجريبي' in body.split('</blockquote>')[0], code
    rec = dict(code=code, title=it['title'], type=it['type'],
               status='Draft' if (pol or spc) else 'Published',
               scope='OwnerOnly' if spc else 'OwnerAndDescendants',
               description=it['description'], body=body,
               requiresAcknowledgment=bool(it.get('ack')), acknowledgmentText=it.get('ack'), acknowledgmentIsTemplate=bool(it.get('ack_template') or pol),
               framework=it['framework'], reference=it['reference'], audience=it['audience'], kind=it['kind'],
               minutes=it.get('minutes'), illustration=it['illustration'],
               officialMediaSlot='OfficialMediaSlot: NCA/Haseen awareness asset can be attached here later' if not pol else None,
               assessment=None)
    if not pol:
        rnd = random.Random(code)  # deterministic shuffle: the position of the correct answer varies
        qrecs = []
        key.append(f'\n## {code} — {it["title"]}\n')
        for n, q in enumerate(it['questions'], 1):
            if q['type'] == 'truefalse':
                opts = [dict(text='True', correct=q['correct']), dict(text='False', correct=not q['correct'])]
                answer = 'صح' if q['correct'] else 'خطأ'
            else:
                opts = [dict(text=t, correct=i == q['correct']) for i, t in enumerate(q['options'])]
                rnd.shuffle(opts); answer = next(o['text'] for o in opts if o['correct'])
            assert sum(o['correct'] for o in opts) == 1
            qrecs.append(dict(text=q['text'], type='TrueFalse' if q['type'] == 'truefalse' else 'SingleChoice', kind=q['kind'], options=opts, explanation=q['explanation']))
            key.append(f'{n}. ({q["kind"]}) {q["text"]}\n   - **الإجابة:** {answer}\n   - **التوضيح:** {q["explanation"]}\n')
        rec['assessment'] = dict(title='اختبار: ' + it['title'], description='اختبار قصير من ثمانية أسئلة يقيس فهمك لأهم ما ورد في المحتوى.',
                                 passingPercentage=80, maxAttempts=3, published=not spc, questions=qrecs)
    out.append(rec)
assert len(out) == 16 and sum(1 for r in out if r['assessment']) == 12
pack = dict(pack='StarterContentPack', version=1,
            notice='Original Abtalna awareness content inspired by public NCA awareness topics. Framework/control references are guidance metadata only. Policy templates must be reviewed and approved by the organization before production use.',
            items=out)
os.makedirs(f'{root}/src/CyberLms.Web/Data/StarterPack', exist_ok=True)
json.dump(pack, open(f'{root}/src/CyberLms.Web/Data/StarterPack/starter-content-pack.v1.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
os.makedirs(f'{root}/docs/starter-content-pack', exist_ok=True)
open(f'{root}/docs/starter-content-pack/ANSWER_KEY.md', 'w', encoding='utf-8').write(''.join(key))
print('items', len(out), 'questions', sum(len(r['assessment']['questions']) for r in out if r['assessment']))
for r in out: print(r['code'], r['status'], r['scope'], words(r['body']), len(r['assessment']['questions']) if r['assessment'] else '-')
