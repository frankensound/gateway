using System.Diagnostics;

namespace Gateway.Utils
{
    public class ResponseTimeMiddleware
    {
        private readonly RequestDelegate _next;

        public ResponseTimeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                MetricsRegistry.ResponseTimeHistogram
                    .WithLabels(context.Request.Method, context.Request.Path, "gateway")
                    .Observe(stopwatch.Elapsed.TotalMilliseconds / 1000); // Converting milliseconds to seconds
            }
        }
    }
}