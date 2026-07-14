// The translation tests swap Core's process-global strings table (the real mechanism under test),
// which would race concurrently running classes that assert English output; the suite is fast enough
// to run sequentially.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
