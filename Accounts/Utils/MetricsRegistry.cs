using Prometheus;

namespace Accounts.Utils
{
    public static class MetricsRegistry
    {
        public static readonly Histogram ResponseTimeHistogram = Metrics
            .CreateHistogram("request_duration_milliseconds", 
                            "Histogram of request duration in milliseconds",
                            new HistogramConfiguration
                            {
                                Buckets = new double[] { 0.1, 1, 5, 10, 25, 50, 100, 200, 500 }.Select(ms => ms / 1000).ToArray(),
                                LabelNames = new[] { "method", "endpoint" }
                            });
    }
}
