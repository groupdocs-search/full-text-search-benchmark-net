# Changelog

All notable changes of the benchmark are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-11-19

### Added

- Measurement of the stages of GroupDocs.Search for .NET: the license, the extraction of the document data in
  several threads, the indexing of the extracted data in batches, the optimization of the index and 20 kinds of
  search.
- Every stage can be skipped, and the extraction can be repeated for some formats only.
- The first and the repeated runs of every search are reported separately.
- The peak working set of the process is measured for every operation.
- The number of documents in the index is reported after the indexing.
- The processor and the available memory are printed with the configuration.
- The configuration is checked before the first stage, and the license file must exist.
- Reports: a copy of the console output, a CSV file with a row for every run and a file with the errors reported by
  the library.
- The results of the reference run on the govdocs1 corpus with GroupDocs.Search 26.11.

[Unreleased]: https://github.com/groupdocs-search/full-text-search-benchmark-net/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/groupdocs-search/full-text-search-benchmark-net/releases/tag/v1.0.0
