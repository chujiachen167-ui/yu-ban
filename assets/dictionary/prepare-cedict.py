"""Build the embedded Chinese dictionary from CC-CEDICT (CC BY-SA 4.0).

输入是 MDBG 发布的 cedict_1_0_ts_utf-8_mdbg.txt.gz，输出与英文词典同构：
每行一个 JSON 数组 [词, 拼音, 释义, 词形字段]，便于同一套加载代码读取。

拼音从数字声调转成带声调符号的形式（ni3 hao3 -> nǐ hǎo），
因为学中文的人看的是拼音本身，不是数字。
"""
import gzip, json, re, sys

TONE_MARKS = {
    'a': 'āáǎà', 'e': 'ēéěè', 'i': 'īíǐì',
    'o': 'ōóǒò', 'u': 'ūúǔù', 'ü': 'ǖǘǚǜ',
}


def mark(syllable):
    """把 ni3 这样的音节转成 nǐ。轻声（无数字）原样返回。"""
    m = re.fullmatch(r"([A-Za-zü:]+)([1-5]?)", syllable)
    if not m:
        return syllable
    body, tone = m.group(1), m.group(2)
    body = body.replace('u:', 'ü').replace('v', 'ü')
    if tone in ('', '5'):
        return body
    n = int(tone)
    low = body.lower()
    # 声调标在 a/o/e 上；没有则标在最后一个元音上。
    target = -1
    for c in ('a', 'o', 'e'):
        p = low.find(c)
        if p >= 0:
            target = p
            break
    if target < 0:
        for p in range(len(low) - 1, -1, -1):
            if low[p] in 'iuü':
                target = p
                break
    if target < 0:
        return body
    ch = low[target]
    if ch not in TONE_MARKS:
        return body
    marked = TONE_MARKS[ch][n - 1]
    return body[:target] + (marked.upper() if body[target].isupper() else marked) + body[target + 1:]


def pinyin(raw):
    parts = [mark(p) for p in raw.split()]
    return ' '.join(parts)


def defs(raw):
    """把 /a/b/c/ 拆成一句中文可读的英文释义。"""
    items = [d.strip() for d in raw.strip('/').split('/') if d.strip()]
    # CL: 是量词标注，对初学者也有用，保留；其余按顺序拼接。
    return '；'.join(items)


rows = {}
with gzip.open(sys.argv[1], 'rt', encoding='utf-8') as f:
    for line in f:
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        m = re.match(r"^(\S+) (\S+) \[([^\]]*)\] /(.*)/$", line)
        if not m:
            continue
        trad, simp, py, meaning = m.groups()
        text = defs(meaning)
        if not text or len(simp) > 12 or len(text) > 400:
            continue
        # 同一简体词可能有多个词条，保留第一条，避免覆盖掉最常用的释义。
        if simp not in rows:
            rows[simp] = [simp, pinyin(py), text, '']
        # 繁体写法指向简体条目，方便繁体输入也能查到。
        if trad != simp and trad not in rows:
            rows[trad] = [trad, pinyin(py), text, '']

with gzip.open(sys.argv[2], 'wt', encoding='utf-8', compresslevel=9) as f:
    for word in sorted(rows):
        f.write(json.dumps(rows[word], ensure_ascii=False, separators=(',', ':')) + '\n')

print('Chinese dictionary:', len(rows), 'entries')
