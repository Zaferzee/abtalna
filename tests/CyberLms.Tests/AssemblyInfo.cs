// The test host reads process-wide environment variables, so test classes must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
