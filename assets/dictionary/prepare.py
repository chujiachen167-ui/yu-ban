"""Build the embedded dictionary from ECDICT's CSV (MIT). No network access."""
import csv, gzip, json, re, sys
rows = {}
with open(sys.argv[1], encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        word = r['word'].strip().lower()
        if not re.fullmatch(r"[a-z]+(?:[-'][a-z]+)*", word) or len(word)>45:
            continue
        if not r['translation']: continue
        # Frequency/exam metadata is not a coverage guarantee: it drops valid words such as scrollable.
        rows[word] = [r['phonetic'], r['translation'].replace('\\n','\n'), r['exchange']]
with gzip.open(sys.argv[2], 'wt', encoding='utf-8', compresslevel=9) as f:
    # TSV escapes are handled by JSON strings; each record can be read independently.
    for word, fields in sorted(rows.items()):
        f.write(json.dumps([word]+fields, ensure_ascii=False, separators=(',', ':'))+'\n')
print('Embedded dictionary:', len(rows), 'entries')
