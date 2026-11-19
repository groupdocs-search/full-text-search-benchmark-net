# govdocs1, GroupDocs.Search 26.11

The reference run of the benchmark: 919,896 documents of the govdocs1 corpus, indexed, optimized and searched
on a desktop computer on 2026-11-18. The raw measurements are in [govdocs1-26.11.csv](govdocs1-26.11.csv).

## Environment

| Component | Value |
| --- | --- |
| Processor | Intel Core i5-11500, 6 cores, 12 logical processors |
| Memory | 32 GB |
| Disk | SATA SSD for the extracted data and the index |
| Operating system | Windows 10 (10.0.19045) |
| Runtime | .NET 10.0.14, x64, workstation garbage collector |
| Library | GroupDocs.Search 26.11, the build for .NET 10 |

## Configuration

| Parameter | Value |
| --- | --- |
| Stages | Indexing, optimization and search; the data had been extracted by an earlier run |
| Indexing threads | 4 |
| Batches | Up to 10,000 documents or 512 MB of extracted data per call of `Index.Add` |
| Search threads | `NumberOfThreads.Default`: as many as there are processor cores |
| Runs of each search | 3, the garbage collected before every run |
| Image data | Not extracted, so the reverse image search was skipped |
| Index settings | The defaults: the text of the documents is not stored in the index |

## Corpus

[govdocs1](https://digitalcorpora.org/corpora/files/) of Digital Corpora: nearly one million files collected from
web servers of the US government - PDF, HTML, Word, Excel, PowerPoint, text, XML, CSV, images and other formats,
including many spreadsheets and data files with numbers and codes. The text of images was not recognized: OCR was
not used.

| | Documents |
| --- | ---: |
| Data files of the extraction | 983,840 |
| Skipped: no text extracted | 63,943 |
| Passed to `Index.Add` | 919,897 |
| Rejected by the index as too large | 1 |
| In the index | 919,896 |

The skipped files are mostly formats whose text was not extracted in this run and files that could not be read:
PostScript (22,015), gzip (13,724), EPS (5,191), files of unknown type (5,186), Word (3,667), Flash (3,476), dBase
(2,601) and others.

The index holds 285,569,892 unique terms: besides the words, every distinct number, code and identifier of the
documents is a term.

## Indexing

| Operation | Time | Result |
| --- | ---: | --- |
| Create the index | 0.64 s | |
| Index the extracted data | 3 h 48 min 33 s | 919,897 documents passed, 67.1 documents per second |
| - of which reading and deserializing the data files | 28 min 27 s | 4 threads |
| - of which `Index.Add` | 3 h 19 min 57 s | 182 calls, 5,054 documents per call on average |

The index after the indexing: 727 segments, 285,569,892 terms, 60.2 GB.

Each call of `Index.Add` took longer as the index grew, whatever the number of documents in the call:

| Calls | Documents in the index before the calls | Average time of a call | Average documents per call |
| --- | ---: | ---: | ---: |
| 1-10 | 0 | 12.7 s | 2,732 |
| 11-30 | 27,324 | 20.7 s | 2,858 |
| 31-60 | 84,485 | 33.3 s | 4,103 |
| 61-90 | 207,583 | 52.7 s | 4,338 |
| 91-120 | 337,721 | 80.1 s | 5,302 |
| 121-150 | 496,775 | 98.9 s | 4,730 |
| 151-182 | 638,677 | 109.4 s | 8,788 |

A linear fit gives 18.4 s plus 12.8 s for every 100,000 documents already in the index (R² = 0.94). The last call
added 1,240 documents in 112.7 s, as long as the calls before it with more than 9,000 documents each. So in a large
index the number of calls, not the number of documents, determines most of the indexing time: batches as large as
the memory allows index faster.

## Optimization

| Operation | Time | Result |
| --- | ---: | --- |
| Open the index | 4.0 s | 727 segments |
| Optimize | 12 h 24 min 22 s | 727 segments merged into 28; 60.2 GB -> 49.5 GB; the terms unchanged |

The size of a merged segment is limited, so a large index is optimized into several segments, not into one.

## Search

The index was opened in 9.8 s (28 segments). Every search ran 3 times: the first run, then two repeated runs.

| Kind of search | Query | Documents | Occurrences | First run | Repeated, min | Repeated, average |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Simple | `government` | 202,079 | 2,296,822 | 539 ms | 193 ms | 210 ms |
| Boolean | `(budget OR finance) AND report AND NOT draft` | 44,626 | 1,000,960 | 251 ms | 140 ms | 149 ms |
| Phrase | `"united states"` | 241,277 | 3,837,085 | 591 ms | 464 ms | 539 ms |
| Phrase with wildcards | `"department *0~~2 agriculture"` | 32,270 | 183,760 | 183 ms | 129 ms | 129 ms |
| Wildcard | `manag?(1~6)` | 267,231 | 4,729,496 | 347 ms | 282 ms | 287 ms |
| Regular expression | `^^(.)\1{2,}` | 609,547 | 310,366,521 | 27.9 s | 26.1 s | 26.5 s |
| Fuzzy, similarity 0.8 | `goverment` | 202,183 | 2,298,177 | 207 ms | 152 ms | 157 ms |
| Case-sensitive | `NASA` | 47,010 | 595,042 | 52 ms | 35 ms | 35 ms |
| Faceted (field) | `content: energy` | 151,413 | 2,389,572 | 148 ms | 118 ms | 124 ms |
| Numeric range | `1000 ~~ 2000` | 703,582 | 230,532,220 | 25.2 s | 8.5 s | 8.5 s |
| Date range | `daterange(2005-01-01 ~~ 2005-12-31)` | 138,876 | 4,554,549 | 92.1 s | 87.1 s | 87.2 s |
| Word forms | `managed` | 150,522 | 966,349 | 163 ms | 133 ms | 140 ms |
| Synonyms | `improve` | 202,299 | 1,265,491 | 211 ms | 164 ms | 168 ms |
| Homophones | `right` | 206,431 | 1,847,199 | 208 ms | 152 ms | 160 ms |
| Spelling correction, 1 mistake | `managment` | 213,795 | 3,466,897 | 168 ms | 169 ms | 174 ms |
| Keyboard layout correction | `ьфтфпуьуте` | 213,720 | 3,463,612 | 182 ms | 159 ms | 168 ms |
| Aliases | `@env AND water` | 78,118 | 3,457,853 | 203 ms | 139 ms | 143 ms |
| Document filter, PDF only | `government` | 88,732 | 1,083,176 | 147 ms | 130 ms | 131 ms |
| By chunks, 28 chunks | `government` | 202,079 | 2,296,822 | 155 ms | 136 ms | 137 ms |
| Object query | `"enviromental *0~~2 protect?(0~4)"` | 37,935 | 215,993 | 456 ms | 229 ms | 236 ms |

Notes:

- The simple search was the first search after the index was opened, so its first run also includes the warm-up
  of the process and of the index files.
- 17 of the 20 kinds of search took 35-539 ms on average in the repeated runs (median 158 ms) and at most 591 ms in
  the first run, although most queries are found in tens or hundreds of thousands of documents.
- The three slowest queries are the heaviest that a user can ask: the regular expression matches 310 million
  occurrences in two thirds of the documents, the numeric range 231 million occurrences in three quarters of them,
  and the date range covers a whole year. A repeated run reuses the words matched by the first one, so the repeated
  times show that most of the time goes to collecting the occurrences: 26.1 s of 27.9 s for the regular expression,
  8.5 s of 25.2 s for the numeric range.
- The search by chunks found the same number of documents and occurrences as the simple search, in 28 chunks.

## Memory

The peak working set of the process over the whole run was 14.4 GB. The run was made by the first version of the
benchmark, which did not measure the memory of each operation; the current version reports it in the
`PeakWorkingSetMB` column of the CSV file.
