# Sonnet language data

Ordinary builds use the checked-in binary resources offline. No Python,
download, JSON reflection serializer, or tokenizer runtime is needed by the app.

Regenerate from pinned upstream sources:

```sh
python3 tools/generate-sonnet-language-data.py
```

The equivalent opt-in build property is `GenerateSonnetLanguageData=true`.
For offline generation, pass `--source-root DIRECTORY` containing the pinned
`jieba/` and `tinysegmenter/` checkouts. SHA-256 is checked before reading data;
upstream Python is parsed as literal data, never executed.

The generator pins source revisions and hashes, and prints the unique dictionary
entry count and runtime data size. Generation rejects a total greater than 8 MiB
without pruning entries. Duplicate dictionary spellings use the last source
entry; probabilities are normalized over the resulting unique entries.

- `Chinese.bin`: `SNL1`, little-endian count / maximum UTF-16 length / maximum
  UTF-8 length / block count, double log total frequency, int block offsets.
  Each block has up to 32 lexically sorted UTF-8 words. Entries contain unsigned
  varint common-prefix and suffix byte lengths, suffix bytes, float log word
  probability, and a byte coarse word class. Only this byte array is retained;
  searches reconstruct one block into a temporary buffer. DAG scanning appends
  one complete grapheme at a time to a reusable UTF-8 prefix buffer and narrows
  the matching sorted-entry interval. An empty interval stops that scan; terminal
  entries feed the existing maximum-probability route directly. No persistent
  trie, new language resource format, or regenerated dictionary is required.
- `ChineseHmm.bin`: `SNH1`, four double initial probabilities and sixteen double
  transition probabilities in BMES order, followed by four sorted emission
  tables (int count, int Unicode scalar / double probability pairs).
- `Japanese.bin`: `SNJ1`, int bias and table count, then TinySegmenter feature
  tables with UTF-8 names/keys (varint lengths) and int weights.

Word boundaries and original offsets are independent of lyric timestamps.
Clipped/missing and internally subdivided timings are marked estimated;
unaltered source-tag endpoints remain available as reliable pause anchors.
Unknown Chinese emission characters stay intact. Chinese is the default for
ambiguous all-Han text; `SonnetLine.LanguageHint` can override routing.

Lexical words and display segments serve different purposes. Display grouping
attaches function words to an adjacent content word (for example, `光` + `的`
becomes `光的`, and `あなた` + `へ` becomes `あなたへ`), while preserving the
original words and offsets in `LexicalWords`. The emphasis core excludes attached
function words. A reliable pause of at least 300 ms prevents grouping across that
boundary. Punctuation may attach to words; symbols such as emoji remain independent
segments, and each complete emoji grapheme keeps its original timing.

The resources and implementation are designed for AOT, but generation and
ordinary compilation do not establish NativeAOT or visual-quality validation.
