using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GroupDocs.Search.Common;
using GroupDocs.Search.Dictionaries;
using GroupDocs.Search.Options;
using GroupDocs.Search.Results;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Measures the operations of GroupDocs.Search for .NET: the extraction of document data, the indexing of the
/// extracted data, the optimization of the index and every kind of search.
/// </summary>
/// <remarks>
/// All parameters are set in the configuration section below. Each stage and each kind of search is measured by
/// its own method: comment out the call of a method to skip its measurement.
/// </remarks>
internal static class Program
{
    // ============================================================================================================
    // Configuration
    // ============================================================================================================

    // The license file. An empty path runs the library in the evaluation mode, which limits the number of indexed
    // documents and of found documents, so its results are not representative.
    private const string LicensePath = @"C:\Benchmark\GroupDocs.Search.lic";

    // The documents are taken from the documents folder and its subfolders. The data extracted from a document is
    // saved in the extracted data folder as <path of the document relative to the documents folder>.dat.
    private const string DocumentsFolder = @"C:\Benchmark\Documents";
    private const string ExtractedDataFolder = @"C:\Benchmark\ExtractedData";
    private const string IndexFolder = @"C:\Benchmark\Index";

    // Each run writes its console output (.log), its measurements (.csv) and the errors reported by the library
    // (.errors.log) to the report folder.
    private const string ReportFolder = @"C:\Benchmark\Reports";

    // The stages. A skipped stage leaves the result of a previous run: the extracted data or the index.
    private static readonly bool RunExtraction = true;
    private static readonly bool RunIndexing = true;
    private static readonly bool RunOptimization = true;
    private static readonly bool RunSearch = true;

    // The extensions of the documents to extract, for example [".html", ".htm"]. If the list is empty, all
    // documents are extracted, and the data of the previous extraction is deleted first. Otherwise, only the
    // documents with these extensions are extracted again, and the data of the other documents is kept.
    private static readonly string[] ExtractionExtensions = [];

    // The number of threads of each stage. The indexing accepts 1 to 4 threads (IndexingOptions.Threads); the
    // default number of search threads is the number of processor cores.
    private static readonly int ExtractionThreads = 1;
    private static readonly int IndexingThreads = 1;
    private const NumberOfThreads SearchThreads = NumberOfThreads.Default;

    // The extraction options: the raw text extraction mode (off by default in the library) and the extraction of
    // the image data that the reverse image search needs.
    private static readonly bool UseRawTextExtraction = false;
    private static readonly bool IndexImages = false;

    // The extracted data is added to the index in batches. A batch is limited by the number of documents and by
    // the total size of their data files. Each call of Index.Add costs more as the index grows, so in a large index
    // larger batches give a faster indexing, at the cost of memory.
    private static readonly int IndexingBatchDocumentCount = 10000;
    private static readonly long IndexingBatchByteCount = 512L * 1024 * 1024;

    // Each search runs SearchRunCount times. The first run is reported apart from the repeated runs, because the
    // repeated searches reuse the words found by the first one. Garbage is collected before each run.
    private static readonly int SearchRunCount = 3;
    private static readonly bool CollectGarbageBeforeSearch = true;

    // The queries. Adapt them to the language and the content of the documents. The syntax: "*0~~2" in a phrase
    // stands for 0 to 2 skipped words, "?(1~6)" in a word for 1 to 6 characters, the first caret marks a regular
    // expression, "~~" separates the bounds of a range; the keyboard layout query is "management" typed in the
    // Russian layout.
    private const string SimpleQuery = "government";
    private const string BooleanQuery = "(budget OR finance) AND report AND NOT draft";
    private const string PhraseQuery = "\"united states\"";
    private const string PhraseWithWildcardsQuery = "\"department *0~~2 agriculture\"";
    private const string WildcardQuery = "manag?(1~6)";
    private const string RegularExpressionQuery = @"^^(.)\1{2,}";
    private const string FuzzyQuery = "goverment";
    private const double FuzzySimilarityLevel = 0.8;
    private const string CaseSensitiveQuery = "NASA";
    private const string FieldQuery = "content: energy";
    private const string NumericRangeQuery = "1000 ~~ 2000";
    private const string DateRangeQuery = "daterange(2005-01-01 ~~ 2005-12-31)";
    private const string WordFormsQuery = "managed";
    private const string SynonymQuery = "improve";
    private const string HomophoneQuery = "right";
    private const string SpellingCorrectionQuery = "managment";
    private const int SpellingCorrectionMaxMistakeCount = 1;
    private const string KeyboardLayoutQuery = "ьфтфпуьуте";
    private const string AliasName = "env";
    private const string AliasText = "(environment OR ecology OR pollution)";
    private const string AliasQuery = "@env AND water";
    private const string DocumentFilterQuery = "government";
    private static readonly string[] DocumentFilterExtensions = [".pdf"];
    private const string ChunkQuery = "government";
    private const string ObjectQueryFuzzyWord = "enviromental";
    private const string ObjectQueryPatternPrefix = "protect";

    // The image for the reverse image search; the search is skipped if the path is empty. The other values are
    // the defaults of the library.
    private const string ReferenceImagePath = "";
    private const int ImageHashDifferences = 5;
    private const int ImageMaxResultCount = 1000;

    // The interval of the progress messages of long operations.
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(10);

    // ============================================================================================================
    // Implementation
    // ============================================================================================================

    private const string DataExtension = ".dat";
    private const string TemporaryExtension = ".tmp";

#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    private static readonly EnumerationOptions RecursiveEnumeration = new EnumerationOptions
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
    };

    private static BenchmarkReport report = null!;

    /// <summary>
    /// Runs the stages of the benchmark.
    /// </summary>
    /// <returns>0 if all stages are finished; 1 if a stage has failed.</returns>
    private static int Main()
    {
        report = new BenchmarkReport(ReportFolder);
        try
        {
            PrintConfiguration();
            CheckConfiguration();
            CheckFolders();

            // The stages; comment out a call to skip its stage.
            SetLicense();
            if (RunExtraction)
            {
                ExtractDocuments();
            }

            if (RunIndexing)
            {
                IndexExtractedData();
            }

            if (RunOptimization)
            {
                OptimizeIndex();
            }

            if (RunSearch)
            {
                RunSearches();
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine();
            Console.WriteLine("The benchmark is stopped by an error: " + exception);
            return 1;
        }
        finally
        {
            report.PrintSummary();
            report.Dispose();
        }
    }

    /// <summary>
    /// Runs every kind of search on the index.
    /// </summary>
    private static void RunSearches()
    {
        using Index index = OpenIndex("Search");
        if (index.IndexSettings.SearchThreads != SearchThreads)
        {
            index.IndexSettings.SearchThreads = SearchThreads;
        }

        SubscribeToErrors(index, "Search");

        // The kinds of search; comment out a call to skip its search.
        SearchSimple(index);
        SearchBoolean(index);
        SearchPhrase(index);
        SearchPhraseWithWildcards(index);
        SearchWildcard(index);
        SearchRegularExpression(index);
        SearchFuzzy(index);
        SearchCaseSensitive(index);
        SearchInField(index);
        SearchNumericRange(index);
        SearchDateRange(index);
        SearchWordForms(index);
        SearchSynonyms(index);
        SearchHomophones(index);
        SearchWithSpellingCorrection(index);
        SearchWithKeyboardLayoutCorrection(index);
        SearchWithAliases(index);
        SearchWithDocumentFilter(index);
        SearchByChunks(index);
        SearchWithObjectQuery(index);
        SearchImages(index);
    }

    // ============================================================================================================
    // Stages
    // ============================================================================================================

    /// <summary>
    /// Sets the license.
    /// </summary>
    private static void SetLicense()
    {
        if (string.IsNullOrEmpty(LicensePath))
        {
            Console.WriteLine("No license is set: the library works in the evaluation mode, which limits the results.");
            return;
        }

        if (!File.Exists(LicensePath))
        {
            throw new FileNotFoundException(
                "The license file is not found. Set LicensePath, or set it to an empty string to run in the " +
                "evaluation mode.",
                LicensePath);
        }

        report.ResetPeakWorkingSet();
        long start = Stopwatch.GetTimestamp();
        License license = new License();
        license.SetLicense(LicensePath);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        report.Add("License", "Set license", elapsed, string.Empty, 0, report.GetPeakWorkingSet());
    }

    /// <summary>
    /// Extracts the data of the documents in several threads and saves the data of each document to its own file.
    /// </summary>
    private static void ExtractDocuments()
    {
        if (!Directory.Exists(DocumentsFolder))
        {
            throw new DirectoryNotFoundException("The documents folder does not exist: " + DocumentsFolder);
        }

        Directory.CreateDirectory(ExtractedDataFolder);
        bool allDocuments = ExtractionExtensions.Length == 0;
        if (allDocuments)
        {
            DeleteExtractedData();
        }

        HashSet<string> extensions = new HashSet<string>(ExtractionExtensions, StringComparer.OrdinalIgnoreCase);
        IEnumerable<FileInfo> documents = new DirectoryInfo(DocumentsFolder)
            .EnumerateFiles("*", RecursiveEnumeration)
            .Where(document => allDocuments || extensions.Contains(document.Extension));

        ExtractionOptions options = new ExtractionOptions();
        options.UseRawTextExtraction = UseRawTextExtraction;
        SetImageIndexingOptions(options);

        ExtractionCounters counters = new ExtractionCounters();
        ParallelOptions parallelOptions = new ParallelOptions();
        parallelOptions.MaxDegreeOfParallelism = ExtractionThreads;
        int errorsBefore = report.ErrorCount;
        report.ResetPeakWorkingSet();
        long start = Stopwatch.GetTimestamp();
        using (new ProgressTimer(ProgressInterval, counters.ToString))
        {
            // Each thread has its own extractor. The documents are taken one by one, because their extraction
            // times differ greatly.
            Parallel.ForEach(
                Partitioner.Create(documents, EnumerablePartitionerOptions.NoBuffering),
                parallelOptions,
                () => new ExtractionWorker(),
                (document, state, worker) =>
                {
                    ExtractDocument(worker, document, options, counters);
                    return worker;
                },
                worker => { });
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        int errors = report.ErrorCount - errorsBefore;
        report.Add(
            "Extraction", "Extract documents", elapsed, counters.Describe(elapsed), errors, report.GetPeakWorkingSet());
    }

    /// <summary>
    /// Extracts the data of a document and saves it. The data file of a document that fails is deleted, so that
    /// the indexing does not take the data of a previous extraction.
    /// </summary>
    private static void ExtractDocument(
        ExtractionWorker worker,
        FileInfo document,
        ExtractionOptions options,
        ExtractionCounters counters)
    {
        string relativePath = Path.GetRelativePath(DocumentsFolder, document.FullName);
        string dataPath = Path.Combine(ExtractedDataFolder, relativePath + DataExtension);
        worker.DocumentPath = document.FullName;
        try
        {
            ExtractedData data = worker.Extractor.Extract(Document.CreateFromFile(document.FullName), options);
            if (data.IsExtractionSuccessful)
            {
                byte[] bytes = data.Serialize();
                WriteFile(dataPath, bytes);
                counters.AddExtracted(document.Length, bytes.Length);
                return;
            }

            report.LogError("Extraction", document.FullName + ": the extraction is not successful.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            string message = exception.GetType().Name + ": " + exception.Message;
            report.LogError("Extraction", document.FullName + ": " + message);
        }

        DeleteFile(dataPath);
        counters.AddFailed(document.Length);
    }

    /// <summary>
    /// Deletes the data files of the previous extraction.
    /// </summary>
    private static void DeleteExtractedData()
    {
        long start = Stopwatch.GetTimestamp();
        int count = 0;
        foreach (string path in Directory.EnumerateFiles(ExtractedDataFolder, "*", RecursiveEnumeration))
        {
            if (path.EndsWith(DataExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(DataExtension + TemporaryExtension, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                count++;
            }
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        report.Add("Extraction", "Delete previous data", elapsed, Formatting.Count(count, "file", "files"), 0, null);
    }

    /// <summary>
    /// Creates the index and adds the extracted data to it in batches. The indexing of a batch uses several threads.
    /// </summary>
    private static void IndexExtractedData()
    {
        if (!Directory.Exists(ExtractedDataFolder))
        {
            throw new DirectoryNotFoundException("The extracted data folder does not exist: " + ExtractedDataFolder);
        }

        IndexSettings settings = new IndexSettings();
        settings.SearchThreads = SearchThreads;

        report.ResetPeakWorkingSet();
        long start = Stopwatch.GetTimestamp();
        using Index index = new Index(IndexFolder, settings, true);
        TimeSpan creationTime = Stopwatch.GetElapsedTime(start);
        report.Add("Indexing", "Create index", creationTime, string.Empty, 0, report.GetPeakWorkingSet());
        SubscribeToErrors(index, "Indexing");

        IndexingOptions options = new IndexingOptions();
        options.Threads = IndexingThreads;
        SetImageIndexingOptions(options);

        IndexingCounters counters = new IndexingCounters();
        index.Events.OperationProgressChanged += (sender, args) => counters.SetBatchProgress(args.ProcessedDocuments);

        DirectoryInfo dataFolder = new DirectoryInfo(ExtractedDataFolder);
        List<FileInfo> batch = new List<FileInfo>();
        long batchByteCount = 0;
        int errorsBefore = report.ErrorCount;
        report.ResetPeakWorkingSet();
        start = Stopwatch.GetTimestamp();
        using (new ProgressTimer(ProgressInterval, counters.ToString))
        {
            foreach (FileInfo file in dataFolder.EnumerateFiles("*" + DataExtension, RecursiveEnumeration))
            {
                batch.Add(file);
                batchByteCount += file.Length;
                if (batch.Count >= IndexingBatchDocumentCount || batchByteCount >= IndexingBatchByteCount)
                {
                    IndexBatch(index, batch, options, counters);
                    batch.Clear();
                    batchByteCount = 0;
                }
            }

            if (batch.Count > 0)
            {
                IndexBatch(index, batch, options, counters);
            }
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        long peakWorkingSet = report.GetPeakWorkingSet();
        if (counters.BatchCount == 0)
        {
            Console.WriteLine("No data files in the extracted data folder: " + ExtractedDataFolder);
        }

        // The documents that the index holds; the index can refuse a document, for example a too large one.
        IndexingReport? lastReport = index.GetIndexingReports().LastOrDefault();
        string documentsInIndex = lastReport != null
            ? Formatting.Count(lastReport.TotalDocumentsInIndex, "document", "documents") + " in the index; "
            : string.Empty;
        string details = counters.Describe(elapsed) + "; " + documentsInIndex + DescribeIndex(index);
        string calls = Formatting.Count(counters.BatchCount, "call", "calls");
        report.Add("Indexing", "Read data files", counters.ReadTime, "including deserialization", 0, null);
        report.Add("Indexing", "Index.Add", counters.AddTime, calls, 0, null);
        int errors = report.ErrorCount - errorsBefore;
        report.Add("Indexing", "Index extracted data", elapsed, details, errors, peakWorkingSet);
    }

    /// <summary>
    /// Reads a batch of data files in several threads and adds their data to the index.
    /// </summary>
    private static void IndexBatch(
        Index index,
        List<FileInfo> batch,
        IndexingOptions options,
        IndexingCounters counters)
    {
        long start = Stopwatch.GetTimestamp();
        ExtractedData?[] data = new ExtractedData?[batch.Count];
        ParallelOptions parallelOptions = new ParallelOptions();
        parallelOptions.MaxDegreeOfParallelism = IndexingThreads;
        Parallel.For(0, batch.Count, parallelOptions, i => data[i] = ReadExtractedData(batch[i]));
        ExtractedData[] validData = data.OfType<ExtractedData>().ToArray();
        TimeSpan readTime = Stopwatch.GetElapsedTime(start);

        start = Stopwatch.GetTimestamp();
        if (validData.Length > 0)
        {
            index.Add(validData, options);
        }

        TimeSpan addTime = Stopwatch.GetElapsedTime(start);
        counters.AddBatch(validData.Length, batch.Count - validData.Length, readTime, addTime);
        Console.WriteLine(
            "    Batch " + Formatting.Count(counters.BatchCount) + ": " +
            Formatting.Count(validData.Length, "document", "documents") + ", read " + Formatting.Duration(readTime) +
            ", Index.Add " + Formatting.Duration(addTime));
    }

    /// <summary>
    /// Reads the extracted data of a document from its file.
    /// </summary>
    /// <returns>The data, or null if the data cannot be read or its extraction has failed.</returns>
    private static ExtractedData? ReadExtractedData(FileInfo file)
    {
        try
        {
            ExtractedData data = ExtractedData.Deserialize(File.ReadAllBytes(file.FullName));
            if (data.IsExtractionSuccessful)
            {
                return data;
            }

            report.LogError("Indexing", file.FullName + ": the extraction of the data is not successful.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            report.LogError("Indexing", file.FullName + ": " + exception.GetType().Name + ": " + exception.Message);
        }

        return null;
    }

    /// <summary>
    /// Optimizes the index: merges its segments.
    /// </summary>
    private static void OptimizeIndex()
    {
        using Index index = OpenIndex("Optimization");
        SubscribeToErrors(index, "Optimization");

        int processedSegments = 0;
        int totalSegments = 0;
        index.Events.OptimizationProgressChanged += (sender, args) =>
        {
            Volatile.Write(ref processedSegments, args.ProcessedSegments);
            Volatile.Write(ref totalSegments, args.TotalSegments);
        };

        Func<string> describeProgress = () =>
            Formatting.Count(Volatile.Read(ref processedSegments)) + " of " +
            Formatting.Count(Volatile.Read(ref totalSegments)) + " segments processed";

        string before = DescribeIndex(index);
        int errorsBefore = report.ErrorCount;
        report.ResetPeakWorkingSet();
        long start = Stopwatch.GetTimestamp();
        using (new ProgressTimer(ProgressInterval, describeProgress))
        {
            index.Optimize();
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        long peakWorkingSet = report.GetPeakWorkingSet();
        string details = "before: " + before + "; after: " + DescribeIndex(index);
        report.Add("Optimization", "Optimize", elapsed, details, report.ErrorCount - errorsBefore, peakWorkingSet);
    }

    // ============================================================================================================
    // Kinds of search
    // ============================================================================================================

    /// <summary>
    /// Simple search: the documents that contain a word.
    /// </summary>
    private static void SearchSimple(Index index)
    {
        MeasureSearch("Simple", SimpleQuery, () => index.Search(SimpleQuery));
    }

    /// <summary>
    /// Boolean search: subqueries combined by the AND, OR and NOT operators.
    /// </summary>
    private static void SearchBoolean(Index index)
    {
        MeasureSearch("Boolean", BooleanQuery, () => index.Search(BooleanQuery));
    }

    /// <summary>
    /// Phrase search: words that follow one another.
    /// </summary>
    private static void SearchPhrase(Index index)
    {
        MeasureSearch("Phrase", PhraseQuery, () => index.Search(PhraseQuery));
    }

    /// <summary>
    /// Phrase search with wildcards: a phrase in which a wildcard stands for a range of skipped words.
    /// </summary>
    private static void SearchPhraseWithWildcards(Index index)
    {
        MeasureSearch("Phrase with wildcards", PhraseWithWildcardsQuery, () => index.Search(PhraseWithWildcardsQuery));
    }

    /// <summary>
    /// Wildcard search: the words that match a pattern with wildcard characters.
    /// </summary>
    private static void SearchWildcard(Index index)
    {
        MeasureSearch("Wildcard", WildcardQuery, () => index.Search(WildcardQuery));
    }

    /// <summary>
    /// Regular expression search: the words that match a regular expression (the first caret of the query marks it).
    /// </summary>
    private static void SearchRegularExpression(Index index)
    {
        MeasureSearch("Regular expression", RegularExpressionQuery, () => index.Search(RegularExpressionQuery));
    }

    /// <summary>
    /// Fuzzy search: the words similar to the word of the query.
    /// </summary>
    private static void SearchFuzzy(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.FuzzySearch.Enabled = true;
        options.FuzzySearch.FuzzyAlgorithm = new SimilarityLevel(FuzzySimilarityLevel);
        MeasureSearch("Fuzzy", FuzzyQuery, () => index.Search(FuzzyQuery, options));
    }

    /// <summary>
    /// Case-sensitive search: the words written in the same letter case as in the query.
    /// </summary>
    private static void SearchCaseSensitive(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.UseCaseSensitiveSearch = true;
        MeasureSearch("Case-sensitive", CaseSensitiveQuery, () => index.Search(CaseSensitiveQuery, options));
    }

    /// <summary>
    /// Faceted search: the words in a field of the documents.
    /// </summary>
    private static void SearchInField(Index index)
    {
        MeasureSearch("Faceted (field)", FieldQuery, () => index.Search(FieldQuery));
    }

    /// <summary>
    /// Numeric range search: the numbers within a range.
    /// </summary>
    private static void SearchNumericRange(Index index)
    {
        MeasureSearch("Numeric range", NumericRangeQuery, () => index.Search(NumericRangeQuery));
    }

    /// <summary>
    /// Date range search: the dates within a range, in the date formats of the search options.
    /// </summary>
    private static void SearchDateRange(Index index)
    {
        MeasureSearch("Date range", DateRangeQuery, () => index.Search(DateRangeQuery));
    }

    /// <summary>
    /// Search for word forms: the word of the query and its other grammatical forms.
    /// </summary>
    private static void SearchWordForms(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.UseWordFormsSearch = true;
        MeasureSearch("Word forms", WordFormsQuery, () => index.Search(WordFormsQuery, options));
    }

    /// <summary>
    /// Synonym search: the word of the query and its synonyms from the synonym dictionary.
    /// </summary>
    private static void SearchSynonyms(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.UseSynonymSearch = true;
        MeasureSearch("Synonyms", SynonymQuery, () => index.Search(SynonymQuery, options));
    }

    /// <summary>
    /// Homophone search: the word of the query and its homophones from the homophone dictionary.
    /// </summary>
    private static void SearchHomophones(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.UseHomophoneSearch = true;
        MeasureSearch("Homophones", HomophoneQuery, () => index.Search(HomophoneQuery, options));
    }

    /// <summary>
    /// Search with spelling correction: a misspelled word is corrected by the spelling corrector dictionary.
    /// </summary>
    private static void SearchWithSpellingCorrection(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.SpellingCorrector.Enabled = true;
        options.SpellingCorrector.MaxMistakeCount = SpellingCorrectionMaxMistakeCount;
        options.SpellingCorrector.OnlyBestResults = true;
        MeasureSearch(
            "Spelling correction", SpellingCorrectionQuery, () => index.Search(SpellingCorrectionQuery, options));
    }

    /// <summary>
    /// Search with keyboard layout correction: a word typed in a wrong keyboard layout.
    /// </summary>
    private static void SearchWithKeyboardLayoutCorrection(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.KeyboardLayoutCorrector.Enabled = true;
        MeasureSearch(
            "Keyboard layout correction", KeyboardLayoutQuery, () => index.Search(KeyboardLayoutQuery, options));
    }

    /// <summary>
    /// Search with aliases: an alias of the query is replaced with its text from the alias dictionary.
    /// </summary>
    private static void SearchWithAliases(Index index)
    {
        // The alias dictionary is saved in the index, so the alias is added or replaced only if its text differs.
        AliasDictionary aliases = index.Dictionaries.AliasDictionary;
        if (aliases.GetText(AliasName) != AliasText)
        {
            aliases.Add(AliasName, AliasText);
        }

        MeasureSearch("Aliases", AliasQuery, () => index.Search(AliasQuery));
    }

    /// <summary>
    /// Search with a document filter: only the documents that pass the filter are found.
    /// </summary>
    private static void SearchWithDocumentFilter(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.SearchDocumentFilter = SearchDocumentFilter.CreateFileExtension(DocumentFilterExtensions);
        string query = DocumentFilterQuery + " (" + string.Join(", ", DocumentFilterExtensions) + ")";
        MeasureSearch("Document filter", query, () => index.Search(DocumentFilterQuery, options));
    }

    /// <summary>
    /// Search by chunks: the result is returned in parts; the time includes all parts.
    /// </summary>
    private static void SearchByChunks(Index index)
    {
        SearchOptions options = new SearchOptions();
        options.IsChunkSearch = true;
        MeasureSearch("By chunks", ChunkQuery, () => SearchAllChunks(index, ChunkQuery, options), DescribeChunks);
    }

    /// <summary>
    /// Search with a query in the object form: a phrase of a fuzzy word with its own search options, a range of
    /// skipped words and a word pattern.
    /// </summary>
    private static void SearchWithObjectQuery(Index index)
    {
        SearchQuery fuzzyWord = SearchQuery.CreateWordQuery(ObjectQueryFuzzyWord);
        fuzzyWord.SearchOptions = new SearchOptions();
        fuzzyWord.SearchOptions.FuzzySearch.Enabled = true;
        fuzzyWord.SearchOptions.FuzzySearch.FuzzyAlgorithm = new SimilarityLevel(FuzzySimilarityLevel);

        WordPattern pattern = new WordPattern();
        pattern.AppendString(ObjectQueryPatternPrefix);
        pattern.AppendWildcard(0, 4);

        SearchQuery query = SearchQuery.CreatePhraseSearchQuery(new SearchQuery[]
        {
            fuzzyWord,
            SearchQuery.CreateWildcardQuery(0, 2),
            SearchQuery.CreateWordPatternQuery(pattern),
        });
        MeasureSearch("Object query", query.ToString(), () => index.Search(query));
    }

    /// <summary>
    /// Reverse image search: the images similar to the reference image. The index must contain the image data,
    /// see <see cref="IndexImages"/>.
    /// </summary>
    private static void SearchImages(Index index)
    {
        if (string.IsNullOrEmpty(ReferenceImagePath))
        {
            Console.WriteLine("[Search] Reverse image: skipped, the reference image is not set.");
            return;
        }

        SearchImage image;
        try
        {
            image = SearchImage.Create(ReferenceImagePath);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            report.AddFailure("Search", "Reverse image", ReferenceImagePath, exception);
            return;
        }

        ImageSearchOptions options = new ImageSearchOptions();
        options.HashDifferences = ImageHashDifferences;
        options.MaxResultCount = ImageMaxResultCount;
        MeasureSearch(
            "Reverse image", ReferenceImagePath, () => index.Search(image, options), DescribeImages);
    }

    // ============================================================================================================
    // Helpers
    // ============================================================================================================

    /// <summary>
    /// Runs a text search several times and records the time of each run.
    /// </summary>
    private static void MeasureSearch(string operation, string query, Func<SearchResult> search)
    {
        MeasureSearch(operation, query, search, DescribeResult);
    }

    /// <summary>
    /// Runs a search several times and records the time of each run; only the search itself is measured.
    /// </summary>
    private static void MeasureSearch<TResult>(
        string operation, string query, Func<TResult> search, Func<TResult, string> describe)
    {
        Measurement measurement = new Measurement("Search", operation, query);
        int errorsBefore = report.ErrorCount;
        try
        {
            for (int run = 0; run < SearchRunCount; run++)
            {
                if (CollectGarbageBeforeSearch)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                report.ResetPeakWorkingSet();
                long start = Stopwatch.GetTimestamp();
                TResult result = search();
                TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
                long peakWorkingSet = report.GetPeakWorkingSet();
                measurement.AddRun(elapsed, describe(result), peakWorkingSet);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            measurement.Fail(exception);
        }

        measurement.ErrorCount = report.ErrorCount - errorsBefore;
        report.Add(measurement);
    }

    /// <summary>
    /// Searches by chunks: requests the chunks one by one until the last one.
    /// </summary>
    private static ChunkSearchTotals SearchAllChunks(Index index, string query, SearchOptions options)
    {
        SearchResult result = index.Search(query, options);
        int chunkCount = 1;
        long documentCount = result.DocumentCount;
        long occurrenceCount = result.OccurrenceCount;
        while (result.NextChunkSearchToken != null)
        {
            result = index.SearchNext(result.NextChunkSearchToken);
            chunkCount++;
            documentCount += result.DocumentCount;
            occurrenceCount += result.OccurrenceCount;
        }

        return new ChunkSearchTotals(chunkCount, documentCount, occurrenceCount);
    }

    /// <summary>
    /// Opens the index and records the time of the opening.
    /// </summary>
    private static Index OpenIndex(string stage)
    {
        if (!Directory.Exists(IndexFolder) || !Directory.EnumerateFileSystemEntries(IndexFolder).Any())
        {
            throw new InvalidOperationException("The index folder contains no index: " + IndexFolder);
        }

        report.ResetPeakWorkingSet();
        long start = Stopwatch.GetTimestamp();
        Index index = new Index(IndexFolder);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        report.Add(stage, "Open index", elapsed, DescribeIndex(index), 0, report.GetPeakWorkingSet());
        return index;
    }

    /// <summary>
    /// Checks the values of the configuration that the library or the benchmark does not accept, before any stage
    /// starts.
    /// </summary>
    private static void CheckConfiguration()
    {
        if (ExtractionThreads < 1)
        {
            throw new InvalidOperationException("ExtractionThreads must be at least 1.");
        }

        if (IndexingThreads < 1 || IndexingThreads > 4)
        {
            throw new InvalidOperationException(
                "IndexingThreads must be from 1 to 4, as IndexingOptions.Threads accepts.");
        }

        if (IndexingBatchDocumentCount < 1 || IndexingBatchByteCount < 1)
        {
            throw new InvalidOperationException(
                "IndexingBatchDocumentCount and IndexingBatchByteCount must be positive.");
        }

        if (SearchRunCount < 1)
        {
            throw new InvalidOperationException("SearchRunCount must be at least 1.");
        }
    }

    /// <summary>
    /// Writes the errors reported by the index to the error file of the report.
    /// </summary>
    private static void SubscribeToErrors(Index index, string stage)
    {
        index.Events.ErrorOccurred += (sender, args) =>
            report.LogError(stage, (args.IsCritical ? "Critical: " : string.Empty) + args.Message);
    }

    /// <summary>
    /// Enables the extraction of the image data for the reverse image search, if it is required.
    /// </summary>
    private static void SetImageIndexingOptions(BaseOptions options)
    {
        options.ImageIndexingOptions.EnabledForSeparateImages = IndexImages;
        options.ImageIndexingOptions.EnabledForEmbeddedImages = IndexImages;
        options.ImageIndexingOptions.EnabledForContainerItemImages = IndexImages;
    }

    /// <summary>
    /// Checks that the index folder and the extracted data folder cannot delete other data: an index is created
    /// in an emptied folder, and the extraction of all documents deletes the previous data files.
    /// </summary>
    private static void CheckFolders()
    {
        if (IsSameOrInside(ExtractedDataFolder, DocumentsFolder) ||
            IsSameOrInside(DocumentsFolder, ExtractedDataFolder))
        {
            throw new InvalidOperationException(
                "The extracted data folder and the documents folder must not contain each other.");
        }

        foreach (string folder in new[] { DocumentsFolder, ExtractedDataFolder, ReportFolder })
        {
            if (IsSameOrInside(IndexFolder, folder) || IsSameOrInside(folder, IndexFolder))
            {
                throw new InvalidOperationException(
                    "The index folder and the folder " + folder + " must not contain each other.");
            }
        }
    }

    /// <summary>
    /// Determines whether a path is a folder or is inside it.
    /// </summary>
    private static bool IsSameOrInside(string path, string folder)
    {
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        string fullFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes a file through a temporary file, so that an interrupted run never leaves a partial file.
    /// </summary>
    private static void WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + TemporaryExtension;
        File.WriteAllBytes(temporaryPath, bytes);
        File.Move(temporaryPath, path, true);
    }

    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Describes the index: its segments, terms and size on disk.
    /// </summary>
    private static string DescribeIndex(Index index)
    {
        IndexInfo info = index.IndexInfo;
        long size = new DirectoryInfo(IndexFolder).EnumerateFiles("*", RecursiveEnumeration).Sum(file => file.Length);
        return Formatting.Count(info.SegmentCount, "segment", "segments") + ", " +
            Formatting.Count(info.TermCount, "term", "terms") + ", " +
            Formatting.Size(size);
    }

    /// <summary>
    /// Describes the result of a text search.
    /// </summary>
    private static string DescribeResult(SearchResult result)
    {
        StringBuilder text = new StringBuilder();
        text.Append(Formatting.Count(result.DocumentCount, "document", "documents")).Append(", ");
        text.Append(Formatting.Count(result.OccurrenceCount, "occurrence", "occurrences"));
        if (result.Truncated)
        {
            text.Append(", truncated");
        }

        if (!string.IsNullOrEmpty(result.Warnings))
        {
            text.Append(", warnings: ").Append(Formatting.Shorten(result.Warnings, 200));
        }

        return text.ToString();
    }

    /// <summary>
    /// Describes the result of a search by chunks.
    /// </summary>
    private static string DescribeChunks(ChunkSearchTotals totals)
    {
        return Formatting.Count(totals.DocumentCount, "document", "documents") + ", " +
            Formatting.Count(totals.OccurrenceCount, "occurrence", "occurrences") + ", " +
            Formatting.Count(totals.ChunkCount, "chunk", "chunks");
    }

    /// <summary>
    /// Describes the result of a reverse image search.
    /// </summary>
    private static string DescribeImages(ImageSearchResult result)
    {
        return Formatting.Count(result.ImageCount, "image", "images");
    }

    /// <summary>
    /// Prints the parameters of the run.
    /// </summary>
    private static void PrintConfiguration()
    {
        Assembly library = typeof(Index).Assembly;
        AssemblyInformationalVersionAttribute? informationalVersion =
            library.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        string libraryVersion =
            informationalVersion?.InformationalVersion ?? library.GetName().Version?.ToString() ?? "unknown";
        string libraryFramework = library.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "unknown";
        string extraction = !RunExtraction
            ? "off"
            : ExtractionExtensions.Length == 0 ? "all documents" : string.Join(" ", ExtractionExtensions);
        string start = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string garbageCollector = GCSettings.IsServerGC ? "server" : "workstation";
        string benchmarkVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        Console.WriteLine("GroupDocs.Search benchmark " + benchmarkVersion + ", " + start);
        Console.WriteLine("  Library:    GroupDocs.Search " + libraryVersion + " (" + libraryFramework + ")");
        Console.WriteLine(
            "  Runtime:    " + RuntimeInformation.FrameworkDescription + ", " + RuntimeInformation.ProcessArchitecture +
            ", " + garbageCollector + " GC");
        Console.WriteLine(
            "  Build:      " + (IsDebugBuild ? "Debug" : "Release") +
            (Debugger.IsAttached ? ", debugger attached" : string.Empty));
        Console.WriteLine("  System:     " + RuntimeInformation.OSDescription);
        Console.WriteLine(
            "  Hardware:   " + SystemInformation.GetProcessorName() + ", " +
            Formatting.Count(Environment.ProcessorCount, "logical processor", "logical processors") + ", " +
            Formatting.Size(SystemInformation.GetAvailableMemory()) + " of memory available to the process");
        Console.WriteLine("  Documents:  " + DocumentsFolder);
        Console.WriteLine("  Data:       " + ExtractedDataFolder);
        Console.WriteLine("  Index:      " + IndexFolder);
        Console.WriteLine(
            "  Stages:     extraction " + extraction + ", indexing " + OnOff(RunIndexing) + ", optimization " +
            OnOff(RunOptimization) + ", search " + OnOff(RunSearch));
        Console.WriteLine(
            "  Threads:    extraction " + Formatting.Count(ExtractionThreads) + ", indexing " +
            Formatting.Count(IndexingThreads) + ", search " + SearchThreads);
        Console.WriteLine(
            "  Extraction: raw text extraction " + OnOff(UseRawTextExtraction) + ", images " + OnOff(IndexImages));
        Console.WriteLine(
            "  Indexing:   batches of " + Formatting.Count(IndexingBatchDocumentCount, "document", "documents") +
            " or " +
            Formatting.Size(IndexingBatchByteCount));
        Console.WriteLine("  Search:     runs of each search " + Formatting.Count(SearchRunCount));
        Console.WriteLine("  Report:     " + report.LogPath);
        if (IsDebugBuild || Debugger.IsAttached)
        {
            Console.WriteLine("  Warning:    run a Release build without a debugger to get representative results.");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Formats a flag of the configuration.
    /// </summary>
    private static string OnOff(bool value)
    {
        return value ? "on" : "off";
    }

    /// <summary>
    /// The totals of a search by chunks.
    /// </summary>
    private readonly record struct ChunkSearchTotals(int ChunkCount, long DocumentCount, long OccurrenceCount);

    /// <summary>
    /// The extractor of a thread of the extraction, with the path of its current document for the error messages.
    /// </summary>
    private sealed class ExtractionWorker
    {
        public ExtractionWorker()
        {
            Extractor = new Extractor();
            Extractor.ErrorOccurred += (sender, args) =>
                report.LogError("Extraction", DocumentPath + ": " + args.Message);
        }

        public Extractor Extractor { get; }

        public string DocumentPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// The counters of the extraction, shared by its threads.
    /// </summary>
    private sealed class ExtractionCounters
    {
        private long extractedCount;
        private long failedCount;
        private long documentByteCount;
        private long dataByteCount;

        public void AddExtracted(long documentLength, long dataLength)
        {
            Interlocked.Increment(ref extractedCount);
            Interlocked.Add(ref documentByteCount, documentLength);
            Interlocked.Add(ref dataByteCount, dataLength);
        }

        public void AddFailed(long documentLength)
        {
            Interlocked.Increment(ref failedCount);
            Interlocked.Add(ref documentByteCount, documentLength);
        }

        public string Describe(TimeSpan elapsed)
        {
            long documentCount = Interlocked.Read(ref extractedCount) + Interlocked.Read(ref failedCount);
            return ToString() + ", " + Formatting.Size(Interlocked.Read(ref documentByteCount)) + " of documents, " +
                Formatting.Size(Interlocked.Read(ref dataByteCount)) + " of data, " +
                Formatting.Rate(documentCount, elapsed) + " documents/s";
        }

        public override string ToString()
        {
            return Formatting.Count(Interlocked.Read(ref extractedCount), "document", "documents") + " extracted, " +
                Formatting.Count(Interlocked.Read(ref failedCount)) + " failed";
        }
    }

    /// <summary>
    /// The counters of the indexing.
    /// </summary>
    private sealed class IndexingCounters
    {
        private long indexedCount;
        private long skippedCount;
        private int batchProgress;

        public int BatchCount { get; private set; }

        public TimeSpan ReadTime { get; private set; }

        public TimeSpan AddTime { get; private set; }

        public void SetBatchProgress(int processedDocuments)
        {
            Volatile.Write(ref batchProgress, processedDocuments);
        }

        public void AddBatch(int indexed, int skipped, TimeSpan readTime, TimeSpan addTime)
        {
            Interlocked.Add(ref indexedCount, indexed);
            Interlocked.Add(ref skippedCount, skipped);
            Volatile.Write(ref batchProgress, 0);
            BatchCount++;
            ReadTime += readTime;
            AddTime += addTime;
        }

        public string Describe(TimeSpan elapsed)
        {
            long indexed = Interlocked.Read(ref indexedCount);
            return Formatting.Count(indexed, "document", "documents") + " indexed, " +
                Formatting.Count(Interlocked.Read(ref skippedCount)) + " skipped, " +
                Formatting.Count(BatchCount, "batch", "batches") + ", " + Formatting.Rate(indexed, elapsed) +
                " documents/s";
        }

        public override string ToString()
        {
            long indexed = Interlocked.Read(ref indexedCount) + Volatile.Read(ref batchProgress);
            return Formatting.Count(indexed, "document", "documents") + " indexed, " +
                Formatting.Count(Interlocked.Read(ref skippedCount)) + " skipped";
        }
    }
}
