# GroupDocs.Search for .NET benchmarks

[![Build](https://github.com/groupdocs-search/full-text-search-benchmark-net/actions/workflows/build.yml/badge.svg)](https://github.com/groupdocs-search/full-text-search-benchmark-net/actions/workflows/build.yml)
[![GroupDocs.Search on NuGet](https://img.shields.io/nuget/v/GroupDocs.Search?label=GroupDocs.Search)](https://www.nuget.org/packages/GroupDocs.Search)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A console application that measures the real performance of
[GroupDocs.Search for .NET](https://products.groupdocs.com/search/net/) on your documents and your hardware:
the indexing, the optimization of the index and 20 kinds of search, with the time and the peak memory of every
operation.

Use it to answer the questions that a capacity plan needs before a project starts: how long the indexing of your
documents takes, how large the index grows, how long a search of each kind takes, and how much memory each stage
needs.

## Results on one million documents

The reference run indexed the [govdocs1](https://digitalcorpora.org/corpora/files/) corpus with GroupDocs.Search
26.11 on an Intel Core i5-11500 (6 cores, 12 threads) with 32 GB of RAM and a SATA SSD under Windows 10.

| What | Result |
| --- | --- |
| Documents indexed | 919,896 |
| Indexing | 3 h 48 min, 67 documents per second |
| Index | 60.2 GB in 727 segments, 285.6 million unique terms |
| Optimization | 12 h 24 min: 727 segments merged into 28, 49.5 GB |
| Opening the optimized index | 9.8 s |
| Search, 17 kinds of 20 | 35-539 ms (average of the repeated runs), median 158 ms |
| Search, heaviest queries | numeric range 8.5 s, regular expression 26.5 s, date range of a year 87 s |

Every number, the configuration of the run and the raw measurements are in
[docs/results/govdocs1-26.11.md](docs/results/govdocs1-26.11.md). The
[Performance and benchmarks](https://docs.groupdocs.com/search/net/performance-and-benchmarks/) article of the
documentation explains what the results depend on.

## What is measured

| Stage | Operations |
| --- | --- |
| License | Applying the license |
| Extraction (optional) | Extraction of the document data with `Extractor` in several threads; the data of each document is saved to its own file |
| Indexing | Creation of the index; addition of the extracted data with `Index.Add` in batches, in up to 4 threads |
| Optimization | Opening of the index; `Index.Optimize` |
| Search | Opening of the index; 20 kinds of search, each run several times |

The kinds of search: simple, boolean, phrase, phrase with wildcards, wildcard, regular expression, fuzzy,
case-sensitive, faceted (in a field), numeric range, date range, word forms, synonyms, homophones, spelling
correction, keyboard layout correction, aliases, document filter, search by chunks, object query, and reverse image
search when the image data is indexed.

The extraction is separated from the indexing, as described in
[Separate data extraction](https://docs.groupdocs.com/search/net/separate-data-extraction/): the documents are
extracted once, and the indexing can then be repeated on the same data with other settings, or skipped to measure
the search only.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0); Windows or Linux.
- A license of GroupDocs.Search. Without a license, the library works in the evaluation mode, which limits the
  number of indexed and found documents, so the results are not representative. A free
  [temporary license](https://purchase.groupdocs.com/temporary-license/) is enough for a benchmark.
- Free disk space for the extracted data and the index. For one million documents of govdocs1, the index took
  60.2 GB.

## Quick start

1. Clone the repository:

   ```shell
   git clone https://github.com/groupdocs-search/full-text-search-benchmark-net.git
   cd full-text-search-benchmark-net
   ```

2. Open [src/GroupDocs.Search.Benchmark/Program.cs](src/GroupDocs.Search.Benchmark/Program.cs) and set the values
   of the configuration section at the top: the license file, the folders of the documents, of the extracted data,
   of the index and of the reports, and the queries for the language of your documents.

3. Run the benchmark in the Release configuration:

   ```shell
   dotnet run -c Release --project src/GroupDocs.Search.Benchmark
   ```

The benchmark prints the configuration, the progress of long operations and a line for every measured operation,
and ends with a summary table. The [configuration reference](docs/configuration.md) describes every value.

> [!WARNING]
> The index folder is emptied when the indexing starts, and the extraction of all documents deletes the data files
> (`.dat`) of the previous extraction. The benchmark refuses to start if the index folder contains the documents,
> extracted data or report folder or is inside one of them, and if the extracted data folder and the documents
> folder contain each other.

## Typical runs

| Goal | Configuration |
| --- | --- |
| The whole pipeline on new documents | All stages on (the default) |
| The indexing with other settings on the same data | `RunExtraction = false` |
| The search only, on an existing index | `RunExtraction`, `RunIndexing` and `RunOptimization` set to `false` |
| Extraction again for some formats only | `ExtractionExtensions = [".html", ".htm"]` |
| The search of a not optimized index | `RunOptimization = false` |
| Fewer kinds of search | Comment out their calls in `RunSearches` |

## Reports

Every run writes three files with the same name to the report folder:

| File | Content |
| --- | --- |
| `benchmark-<date>-<time>.log` | A copy of the console output |
| `benchmark-<date>-<time>.csv` | A row for every run of every operation |
| `benchmark-<date>-<time>.errors.log` | The errors reported by the library, for example the documents that cannot be extracted |

The columns of the CSV file:

| Column | Meaning |
| --- | --- |
| `Stage` | License, Extraction, Indexing, Optimization or Search |
| `Operation` | The measured operation or the kind of search |
| `Query` | The query of a search |
| `Run` | The number of the run: a search runs `SearchRunCount` times |
| `TimeMs` | The time of the run in milliseconds |
| `PeakWorkingSetMB` | The peak working set of the process during the run, in megabytes |
| `Errors` | The number of errors reported by the library during the operation |
| `Details` | The result: documents, occurrences, segments, terms, the size of the index |

## How to read the results

- **The first and the repeated runs of a search.** The first run of a query opens the files of the index and finds
  the matching words; a repeated run reuses them. The first run is what the first user of a just started application
  waits for; the repeated runs are the usual case of a running service. The summary reports both.
- **The time of a search depends on its result.** A query that is found in hundreds of thousands of documents takes
  longer than a rare word, mostly to build the result. The `Details` column shows the number of found documents and
  occurrences next to the time.
- **Peak memory.** The working set of the process is sampled 4 times per second; the peak of an operation includes
  the memory held by the earlier stages of the same process.
- **The number of segments.** Every call of `Index.Add` adds segments to the index. A search reads the segments that
  hold its words, so compare the search of indexes with comparable numbers of segments; the optimization merges them.

## Project structure

```text
.
├── .github/                       CI workflow, Dependabot and issue templates
├── docs/
│   ├── configuration.md           Every value of the configuration
│   └── results/                   Results of the reference runs and their raw measurements
├── src/
│   └── GroupDocs.Search.Benchmark/
│       ├── Program.cs             The configuration, the stages and the kinds of search
│       ├── BenchmarkReport.cs     The console output, the log, the CSV and the error files
│       ├── Measurement.cs         The runs of an operation: times, peak memory, results
│       ├── MemoryMonitor.cs       The sampling of the working set
│       ├── ProgressTimer.cs       The progress messages of long operations
│       ├── SystemInformation.cs   The processor and the memory for the report
│       ├── Formatting.cs          Culture-independent formatting of times, sizes and counts
│       └── TeeWriter.cs           The copy of the console output to the log
├── CHANGELOG.md
├── GroupDocs.Search.Benchmarks.slnx
└── global.json
```

## Contributing

Bug reports and pull requests are welcome. Before you open a pull request, build the solution without warnings:

```shell
dotnet build -c Release
```

To share the results of your own run, open an
[issue](https://github.com/groupdocs-search/full-text-search-benchmark-net/issues/new/choose) with the "Share results"
template and attach the `.csv` and `.log` files of the run. Questions about the library itself are answered on the
[free support forum](https://forum.groupdocs.com/c/search/).

## License

The benchmark is distributed under the [MIT License](LICENSE). GroupDocs.Search for .NET is a commercial product
with its own [license terms](https://purchase.groupdocs.com/policies/license/).

## Resources

- [Product page](https://products.groupdocs.com/search/net/)
- [Documentation](https://docs.groupdocs.com/search/net/)
- [API reference](https://reference.groupdocs.com/search/net/)
- [Code examples](https://github.com/groupdocs-search/GroupDocs.Search-for-.NET)
- [Blog](https://blog.groupdocs.com/categories/groupdocs.search-product-family/)
- [Free support forum](https://forum.groupdocs.com/c/search/)
- [Temporary license](https://purchase.groupdocs.com/temporary-license/)
