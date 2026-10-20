using Xunit;

// The API uses a static Db connection string, so all integration tests in this
// assembly share one collection and run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
