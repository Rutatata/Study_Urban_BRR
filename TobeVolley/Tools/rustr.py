import os, re, collections, sys, json
os.chdir(r"C:/Users/User/Desktop/Study_Urban_BRR-claude-anime-game-rematch-nauwh5/study_urban_brr/TobeVolley/Assets/_Tobe/Scripts")
STR = re.compile(r'"((?:[^"\\]|\\.)*)"')
cnt = collections.Counter(); tot = collections.OrderedDict()
for d, _, fs in os.walk('.'):
    for f in fs:
        if not f.endswith('.cs'): continue
        p = os.path.join(d, f).replace('\\', '/')
        for i, l in enumerate(open(p, encoding='utf-8'), 1):
            code = l
            if 'Debug.Log' in code: continue
            k = code.find('//')
            # строки до комментария
            for m in STR.finditer(code):
                if k >= 0 and m.start() > k and code[:k].count('"') % 2 == 0: break
                s = m.group(1)
                if re.search('[А-Яа-яЁё]', s):
                    cnt[p] += 1; tot.setdefault(s, []).append(f"{p}:{i}")
for p, c in cnt.most_common(): print(c, p)
print('uniq', len(tot), 'total', sum(cnt.values()))
if len(sys.argv) > 1:
    json.dump(tot, open(sys.argv[1], 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
