# Configuration

All parameters of the benchmark are set in the configuration section at the top of
[Program.cs](../src/GroupDocs.Search.Benchmark/Program.cs); the application has no command line arguments. The
values are checked before the first stage starts, and a value that the library does not accept stops the run with a
message that names it.

## Files and folders

| Value | Default | Description |
| --- | --- | --- |
| `LicensePath` | `C:\Benchmark\GroupDocs.Search.lic` | The license file. An empty string runs the library in the evaluation mode, which limits the number of indexed and found documents, so its results are not representative. |
| `DocumentsFolder` | `C:\Benchmark\Documents` | The documents to extract, taken from the folder and all its subfolders. |
| `ExtractedDataFolder` | `C:\Benchmark\ExtractedData` | The extracted data: a file `<path of the document relative to DocumentsFolder>.dat` for every document. |
| `IndexFolder` | `C:\Benchmark\Index` | The index. The folder is emptied when the indexing starts. |
| `ReportFolder` | `C:\Benchmark\Reports` | The log, the CSV file and the error file of every run. |

The index folder must not contain the other folders or be inside one of them; the extracted data folder and the
documents folder must not contain each other.

## Stages

| Value | Default | Description |
| --- | --- | --- |
| `RunExtraction` | `true` | Extracts the documents. When it is `false`, the indexing uses the data of a previous extraction. |
| `RunIndexing` | `true` | Creates the index and adds the extracted data to it. When it is `false`, the later stages use the existing index. |
| `RunOptimization` | `true` | Merges the segments of the index with `Index.Optimize`. |
| `RunSearch` | `true` | Runs every kind of search. Comment out a call in `RunSearches` to skip a kind of search. |

## Extraction

| Value | Default | Description |
| --- | --- | --- |
| `ExtractionExtensions` | `[]` | The extensions of the documents to extract, for example `[".html", ".htm"]`. When the list is empty, all documents are extracted and the data files of the previous extraction are deleted first. Otherwise, only the documents with these extensions are extracted again, and the data of the other documents is kept. |
| `ExtractionThreads` | the number of logical processors | The number of threads that extract documents; each thread has its own `Extractor`. |
| `UseRawTextExtraction` | `false` | The raw text extraction mode of `ExtractionOptions`: it can speed up the extraction significantly at the cost of the formatting quality of the extracted text. |
| `IndexImages` | `false` | Extracts the image data that the reverse image search needs: separate images, images embedded in documents and images in containers. The image data makes the extraction slower and the index larger. |

A document that fails to extract is written to the error file, and the data file of its previous extraction is
deleted, so that the indexing never takes outdated data.

## Indexing

| Value | Default | Description |
| --- | --- | --- |
| `IndexingThreads` | the number of logical processors, at most 4 | The number of threads of `IndexingOptions.Threads`, which accepts 1 to 4. The data files of a batch are read in the same number of threads. |
| `IndexingBatchDocumentCount` | `10000` | The greatest number of documents in one call of `Index.Add`. |
| `IndexingBatchByteCount` | `512 MB` | The greatest total size of the data files in one call of `Index.Add`. |

A batch ends when either limit is reached. Each call of `Index.Add` costs more as the index grows, so larger batches
give a faster indexing of a large index; the data of a whole batch is kept in memory during the call. A data file
that cannot be read, or holds the data of a failed extraction, is skipped and written to the error file.

## Search

| Value | Default | Description |
| --- | --- | --- |
| `SearchThreads` | `NumberOfThreads.Default` | The threads of the searches of the index (`IndexSettings.SearchThreads`). `Default` uses as many threads as there are processor cores. The benchmark runs one search at a time, so a search can use all of them. |
| `SearchRunCount` | `3` | How many times each search runs. The first run is reported apart from the repeated ones. |
| `CollectGarbageBeforeSearch` | `true` | Collects the garbage before every run, so that a run does not pay for the garbage of the previous one. |

### Queries

Adapt the queries to the language and the content of your documents, so that each of them finds what a user of
your application would search for. The defaults suit English documents.

| Value | Default | Kind of search |
| --- | --- | --- |
| `SimpleQuery` | `government` | A word |
| `BooleanQuery` | `(budget OR finance) AND report AND NOT draft` | Boolean operators |
| `PhraseQuery` | `"united states"` | A phrase |
| `PhraseWithWildcardsQuery` | `"department *0~~2 agriculture"` | A phrase with 0 to 2 arbitrary words between its words |
| `WildcardQuery` | `manag?(1~6)` | A word with 1 to 6 arbitrary characters at its end |
| `RegularExpressionQuery` | `^^(.)\1{2,}` | A regular expression (after the first caret): the words that start with a character repeated 3 or more times |
| `FuzzyQuery`, `FuzzySimilarityLevel` | `goverment`, `0.8` | Fuzzy search with a similarity level |
| `CaseSensitiveQuery` | `NASA` | Case-sensitive search |
| `FieldQuery` | `content: energy` | A word in a field of the documents |
| `NumericRangeQuery` | `1000 ~~ 2000` | The numbers of a range |
| `DateRangeQuery` | `daterange(2005-01-01 ~~ 2005-12-31)` | The dates of a range |
| `WordFormsQuery` | `managed` | The forms of a word |
| `SynonymQuery` | `improve` | The synonyms of a word |
| `HomophoneQuery` | `right` | The homophones of a word |
| `SpellingCorrectionQuery`, `SpellingCorrectionMaxMistakeCount` | `managment`, `1` | A misspelled word, corrected to the best matches |
| `KeyboardLayoutQuery` | `ьфтфпуьуте` | A word typed in another keyboard layout (here "management" typed in the Russian layout) |
| `AliasName`, `AliasText`, `AliasQuery` | `env`, `(environment OR ecology OR pollution)`, `@env AND water` | A query with an alias; the alias is added to the alias dictionary of the index |
| `DocumentFilterQuery`, `DocumentFilterExtensions` | `government`, `[".pdf"]` | A word in the documents of the given formats only |
| `ChunkQuery` | `government` | Search by chunks: every chunk is requested until the last one, and the times of all chunks are added up |
| `ObjectQueryFuzzyWord`, `ObjectQueryPatternPrefix` | `enviromental`, `protect` | A query built of objects: a phrase of a fuzzy word, 0 to 2 arbitrary words and a word that starts with the prefix |

### Reverse image search

| Value | Default | Description |
| --- | --- | --- |
| `ReferenceImagePath` | empty | The image to search for. The search is skipped if the path is empty; it finds images only if `IndexImages` was on during the extraction. |
| `ImageHashDifferences` | `5` | The greatest difference of the image hashes, the default of the library. |
| `ImageMaxResultCount` | `1000` | The greatest number of found images, the default of the library. |

## Progress

| Value | Default | Description |
| --- | --- | --- |
| `ProgressInterval` | `10 s` | The interval of the progress messages of the extraction, the indexing and the optimization. |
